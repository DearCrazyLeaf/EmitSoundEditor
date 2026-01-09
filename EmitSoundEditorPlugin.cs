using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmitSoundEditor.Data;
using EmitSoundEditor.Utils;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using StoreApi;

namespace EmitSoundEditor;

[MinimumApiVersion(80)]
public class EmitSoundEditorPlugin : BasePlugin, IPluginConfig<EmitSoundEditorConfig>
{
    public override string ModuleName => "EmitSound Editor";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "DearCrazyLeaf";
    public override string ModuleDescription => "Overrides custom weapon fire sounds using Store API equipment state.";

    public EmitSoundEditorConfig Config { get; set; } = new();

    private IStoreApi? _storeApi;
    private readonly Dictionary<string, WeaponSoundOverride> _overrideBySubclass = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, WeaponItemDefOverride> _overrideByItemDefIndex = new();
    private readonly Dictionary<ulong, Dictionary<string, string>> _playerSubclassByBase = new();
    private readonly Dictionary<nint, string> _subclassByWeaponHandle = new();
    private readonly LastFireSound?[] _lastFireSoundsBySlot = new LastFireSound?[MaxPlayerSlots];
    private readonly Dictionary<int, CCSPlayerController> _playerByPawnIndex = new();
    private readonly Dictionary<int, int> _pawnIndexBySlot = new();
    private readonly bool[] _customSoundEnabledBySlot = new bool[MaxPlayerSlots];
    private readonly Dictionary<ulong, bool> _customSoundEnabledBySteamId = new();
    private readonly object _customSoundLock = new();
    private CustomSoundSettingsStore? _settingsStore;
    private bool _pendingInitialRefresh;
    private const int MaxPlayerSlots = 65;
    private const int EntityIndexMask = 0x3FFF;
    private const long FireCacheTtlMs = 1500;

    /// <summary>
    /// Registers event handlers, user message hooks, and server listeners.
    /// </summary>
    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Pre);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Post);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        HookUserMessage(452, OnWeaponFireUserMessage, HookMode.Pre);
    }

    /// <summary>
    /// Resolves Store API and schedules initial equipment sync.
    /// </summary>
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        InitializeSettingsStore();

        try
        {
            RefreshCustomSoundCacheForAllPlayers();
        }
        catch (NativeException)
        {
            _pendingInitialRefresh = true;
            Logger.LogInformation("[EmitSoundEditor] Delaying initial sound cache sync until map start.");
        }

        _storeApi = IStoreApi.Capability.Get();
        if (_storeApi == null)
        {
            Logger.LogWarning("[EmitSoundEditor] StoreApi not available. Sound overrides will be disabled.");
            return;
        }

        _storeApi.OnPlayerEquipItem += OnPlayerEquipItem;
        _storeApi.OnPlayerUnequipItem += OnPlayerUnequipItem;
        _storeApi.OnPlayerSellItem += OnPlayerUnequipItem;

        try
        {
            RefreshAllPlayers();
        }
        catch (NativeException)
        {
            _pendingInitialRefresh = true;
            Logger.LogInformation("[EmitSoundEditor] Delaying initial equipment sync until map start.");
        }
    }

    /// <summary>
    /// Runs delayed initialization once a map is loaded.
    /// </summary>
    private void OnMapStart(string mapName)
    {
        _subclassByWeaponHandle.Clear();
        _playerByPawnIndex.Clear();
        _pawnIndexBySlot.Clear();
        Array.Clear(_lastFireSoundsBySlot, 0, _lastFireSoundsBySlot.Length);

        RefreshCustomSoundCacheForAllPlayers();

        if (!_pendingInitialRefresh || _storeApi == null)
        {
            return;
        }

        _pendingInitialRefresh = false;
        RefreshAllPlayers();
    }


    /// <summary>
    /// Initializes per-player state and loads toggle settings.
    /// </summary>
    private void OnClientPutInServer(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || player.IsBot)
        {
            return;
        }

        if (player.SteamID == 0)
        {
            return;
        }

        SetCustomSoundEnabledForPlayer(player, Config.CustomSoundDefaultEnabled);
        UpdatePawnIndexCache(player);

        if (_settingsStore == null)
        {
            InitializeSettingsStore();
        }

        if (_settingsStore == null || !_settingsStore.Enabled)
        {
            return;
        }

        _ = LoadCustomSoundSettingAsync(player.SteamID);
    }

    /// <summary>
    /// Cleans up per-player caches when a client leaves.
    /// </summary>
    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid)
        {
            return;
        }

        RemovePawnIndexCache(player);
        ClearCustomSoundEnabledForPlayer(player);
        RemoveCustomSoundEnabled(player.SteamID);
    }


    /// <summary>
    /// Initializes the optional MySQL-backed settings store.
    /// </summary>
    private void InitializeSettingsStore()
    {
        _settingsStore = new CustomSoundSettingsStore(Config.MySql, Logger);
        _ = _settingsStore.InitializeAsync();
    }

    /// <summary>
    /// Applies config changes and rebuilds lookup maps.
    /// </summary>
    public void OnConfigParsed(EmitSoundEditorConfig config)
    {
        Config = config;
        RebuildOverrideMap();
        InitializeSettingsStore();
    }

    /// <summary>
    /// Rebuilds subclass and item-definition override dictionaries.
    /// </summary>
    private void RebuildOverrideMap()
    {
        _overrideBySubclass.Clear();
        _overrideByItemDefIndex.Clear();

        foreach (var entry in Config.Overrides)
        {
            if (string.IsNullOrWhiteSpace(entry.Subclass) || string.IsNullOrWhiteSpace(entry.TargetEvent))
            {
                continue;
            }

            _overrideBySubclass[entry.Subclass.Trim()] = entry;
        }

        foreach (var entry in Config.OfficialOverrides)
        {
            if (entry.ItemDefIndex <= 0 || string.IsNullOrWhiteSpace(entry.TargetEvent))
            {
                continue;
            }

            _overrideByItemDefIndex[entry.ItemDefIndex] = entry;
        }
    }

    /// <summary>
    /// Refreshes equipment mapping when a player spawns.
    /// </summary>
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
        {
            return HookResult.Continue;
        }

        UpdatePawnIndexCache(player);
        RefreshPlayerEquipment(player);
        return HookResult.Continue;
    }

    /// <summary>
    /// Removes tracked equipment and cached fire events.
    /// </summary>
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _playerSubclassByBase.Remove(player.SteamID);
            ClearLastFireSound(player.Slot);
            RemovePawnIndexCache(player);
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// Resolves custom/official fire events and caches them for 452 playback.
    /// </summary>
    
    
    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || !player.PawnIsAlive)
        {
            return HookResult.Continue;
        }

        var weapon = player.PlayerPawn?.Value?.WeaponServices?.ActiveWeapon?.Value;
        if (weapon == null || !weapon.IsValid)
        {
            return HookResult.Continue;
        }

        UpdatePawnIndexCache(player);

        if (!TryResolveFireEvents(player, weapon, @event, out var customEvent, out var officialEvent, out var itemDefIndex))
        {
            RemoveLastFireSound(player.Slot);
            return HookResult.Continue;
        }

        UpdateLastFireSound(player.Slot, itemDefIndex, customEvent, officialEvent);
        return HookResult.Continue;
    }

    /// <summary>
    /// Plays cached fire events per bullet and suppresses the original message.
    /// </summary>
    
    
    private HookResult OnWeaponFireUserMessage(UserMessage userMessage)
    {
        var forceMute = Config.ForceMuteAllFireBullets;

        var playerHandle = (int)userMessage.ReadUInt("player");
        if (playerHandle <= 0)
        {
            return HookResult.Continue;
        }

        var playerEntityIndex = playerHandle & EntityIndexMask;
        var shooter = FindShooterByPawnIndex(playerEntityIndex);

        if (shooter == null || !shooter.IsValid)
        {
            return HookResult.Continue;
        }

        var lastFire = TryGetLastFireSound(shooter.Slot);

        var nowMs = Environment.TickCount64;
        if (lastFire != null && nowMs - lastFire.UpdatedAtMs > FireCacheTtlMs)
        {
            ClearLastFireSound(shooter.Slot);
            lastFire = null;
        }

        var itemDefIndex = (int)userMessage.ReadUInt("item_def_index");
        if (lastFire != null && itemDefIndex > 0 && lastFire.ItemDefIndex > 0 && itemDefIndex != lastFire.ItemDefIndex)
        {
            ClearLastFireSound(shooter.Slot);
            lastFire = null;
        }

        if (lastFire == null)
        {
            var weapon = shooter.PlayerPawn?.Value?.WeaponServices?.ActiveWeapon?.Value;
            if (weapon == null || !weapon.IsValid)
            {
                return HookResult.Continue;
            }

            if (!TryResolveFireEvents(shooter, weapon, null, out var customEvent, out var officialEvent, out var resolvedDefIndex))
            {
                ClearLastFireSound(shooter.Slot);
                return HookResult.Continue;
            }

            lastFire = new LastFireSound(resolvedDefIndex, customEvent, officialEvent, nowMs);
            SetLastFireSound(shooter.Slot, lastFire);
        }

        var hasCustom = !string.IsNullOrWhiteSpace(lastFire.CustomEvent);
        var hasOfficial = !string.IsNullOrWhiteSpace(lastFire.OfficialEvent);
        if (!hasCustom && !hasOfficial)
        {
            if (forceMute)
            {
                userMessage.Recipients.Clear();
            }

            return HookResult.Continue;
        }

        RecipientFilter? customRecipients = null;
        RecipientFilter? officialRecipients = null;

        if (userMessage.Recipients != null)
        {
            foreach (var recipient in userMessage.Recipients)
            {
                if (recipient == null || !recipient.IsValid)
                {
                    continue;
                }

                if (hasCustom && IsCustomSoundEnabled(recipient))
                {
                    customRecipients ??= new RecipientFilter();
                    customRecipients.Add(recipient);
                    continue;
                }

                if (hasOfficial)
                {
                    officialRecipients ??= new RecipientFilter();
                    officialRecipients.Add(recipient);
                }
            }
        }

        var shooterWantsCustom = hasCustom && IsCustomSoundEnabled(shooter);
        if (shooterWantsCustom)
        {
            customRecipients ??= new RecipientFilter();
            customRecipients.Add(shooter);
        }
        else if (hasOfficial)
        {
            officialRecipients ??= new RecipientFilter();
            officialRecipients.Add(shooter);
        }

        if (customRecipients != null && hasCustom)
        {
            shooter.EmitSound(lastFire.CustomEvent!, customRecipients);
        }

        if (officialRecipients != null && hasOfficial)
        {
            shooter.EmitSound(lastFire.OfficialEvent!, officialRecipients);
        }

        if (forceMute || customRecipients != null || officialRecipients != null)
        {
            userMessage.Recipients.Clear();
        }

        return HookResult.Continue;
    }


    /// <summary>
    /// Tracks Store equip events and maps weapon base to subclass.
    /// </summary>
    private void OnPlayerEquipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (!IsCustomWeaponItem(item))
        {
            return;
        }

        if (!TryGetWeaponSpec(item, out var weaponBase, out var weaponSubclass))
        {
            return;
        }

        var map = GetOrCreatePlayerMap(player.SteamID);
        map[weaponBase] = weaponSubclass;

        var weapon = FindWeaponByBase(player, weaponBase);
        if (weapon != null)
        {
            TrackWeaponSubclass(weapon, weaponSubclass);
        }
    }

    /// <summary>
    /// Removes Store equip mapping and untracks the weapon subclass.
    /// </summary>
    private void OnPlayerUnequipItem(CCSPlayerController player, Dictionary<string, string> item)
    {
        if (!IsCustomWeaponItem(item))
        {
            return;
        }

        if (!TryGetWeaponSpec(item, out var weaponBase, out var weaponSubclass))
        {
            return;
        }

        if (!_playerSubclassByBase.TryGetValue(player.SteamID, out var map))
        {
            return;
        }

        if (map.TryGetValue(weaponBase, out var currentSubclass) &&
            string.Equals(currentSubclass, weaponSubclass, StringComparison.OrdinalIgnoreCase))
        {
            map.Remove(weaponBase);

            var weapon = FindWeaponByBase(player, weaponBase);
            if (weapon != null)
            {
                UntrackWeaponSubclass(weapon);
            }
        }

        if (map.Count == 0)
        {
            _playerSubclassByBase.Remove(player.SteamID);
        }
    }

    /// <summary>
    /// Loads equipped custom weapons from Store API for one player.
    /// </summary>
    private void RefreshPlayerEquipment(CCSPlayerController player)
    {
        if (_storeApi == null || !player.IsValid)
        {
            return;
        }

        var equipment = _storeApi.GetPlayerEquipments(player, "customweapon");
        if (equipment.Count == 0)
        {
            _playerSubclassByBase.Remove(player.SteamID);
            return;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var equip in equipment)
        {
            var item = _storeApi.GetItem(equip.UniqueId);
            if (item == null)
            {
                continue;
            }

            if (!TryGetWeaponSpec(item, out var weaponBase, out var weaponSubclass))
            {
                continue;
            }

            map[weaponBase] = weaponSubclass;

            var weapon = FindWeaponByBase(player, weaponBase);
            if (weapon != null)
            {
                TrackWeaponSubclass(weapon, weaponSubclass);
            }
        }

        if (map.Count > 0)
        {
            _playerSubclassByBase[player.SteamID] = map;
        }
        else
        {
            _playerSubclassByBase.Remove(player.SteamID);
        }
    }

    /// <summary>
    /// Refreshes custom weapon mappings for all connected players.
    /// </summary>
    private void RefreshAllPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            RefreshPlayerEquipment(player);
        }
    }

    /// <summary>
    /// Refreshes the per-slot custom sound cache for connected players.
    /// </summary>
    private void RefreshCustomSoundCacheForAllPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot || player.SteamID == 0)
            {
                continue;
            }

            UpdatePawnIndexCache(player);

            if (IsValidSlot(player.Slot))
            {
                _customSoundEnabledBySlot[player.Slot] = GetCustomSoundEnabled(player.SteamID);
            }

            if (_settingsStore != null && _settingsStore.Enabled)
            {
                _ = LoadCustomSoundSettingAsync(player.SteamID);
            }
        }
    }

    /// <summary>
    /// Checks whether a Store item is a custom weapon entry.
    /// </summary>
    private static bool IsCustomWeaponItem(Dictionary<string, string> item)
    {
        return item.TryGetValue("type", out var type) &&
               string.Equals(type, "customweapon", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses weapon base and subclass from a Store item.
    /// </summary>
    private static bool TryGetWeaponSpec(Dictionary<string, string> item, out string weaponBase, out string weaponSubclass)
    {
        weaponBase = string.Empty;
        weaponSubclass = string.Empty;

        if (!item.TryGetValue("weapon", out var weaponSpec) || string.IsNullOrWhiteSpace(weaponSpec))
        {
            return false;
        }

        return WeaponSubclassUtils.TryParseWeaponSpec(weaponSpec, out weaponBase, out weaponSubclass);
    }

    /// <summary>
    /// Gets or creates the per-player base-to-subclass map.
    /// </summary>
    private Dictionary<string, string> GetOrCreatePlayerMap(ulong steamId)
    {
        if (!_playerSubclassByBase.TryGetValue(steamId, out var map))
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _playerSubclassByBase[steamId] = map;
        }

        return map;
    }

    /// <summary>
    /// Finds a weapon entity by base name on the player.
    /// </summary>
    private static CBasePlayerWeapon? FindWeaponByBase(CCSPlayerController player, string weaponBase)
    {
        var weaponServices = player.PlayerPawn?.Value?.WeaponServices;
        if (weaponServices == null)
        {
            return null;
        }

        var activeWeapon = weaponServices.ActiveWeapon?.Value;
        if (activeWeapon != null && activeWeapon.IsValid &&
            string.Equals(activeWeapon.DesignerName, weaponBase, StringComparison.OrdinalIgnoreCase))
        {
            return activeWeapon;
        }

        foreach (var handle in weaponServices.MyWeapons)
        {
            var weapon = handle.Value;
            if (weapon != null && weapon.IsValid &&
                string.Equals(weapon.DesignerName, weaponBase, StringComparison.OrdinalIgnoreCase))
            {
                return weapon;
            }
        }

        return null;
    }

    /// <summary>
    /// Associates a weapon handle with a subclass for quick lookup.
    /// </summary>
    private void TrackWeaponSubclass(CBasePlayerWeapon weapon, string subclass)
    {
        if (weapon == null || !weapon.IsValid || string.IsNullOrWhiteSpace(subclass))
        {
            return;
        }

        _subclassByWeaponHandle[weapon.Handle] = subclass.Trim();
    }

    /// <summary>
    /// Removes the cached subclass for a weapon handle.
    /// </summary>
    private void UntrackWeaponSubclass(CBasePlayerWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        _subclassByWeaponHandle.Remove(weapon.Handle);
    }

    /// <summary>
    /// Caches resolved fire events for per-bullet playback.
    /// </summary>
    private void UpdateLastFireSound(int slot, int itemDefIndex, string? customEvent, string? officialEvent)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _lastFireSoundsBySlot[slot] = new LastFireSound(
            itemDefIndex,
            string.IsNullOrWhiteSpace(customEvent) ? null : customEvent,
            string.IsNullOrWhiteSpace(officialEvent) ? null : officialEvent,
            Environment.TickCount64);
    }

    /// <summary>
    /// Clears cached fire events for a player slot.
    /// </summary>
    private void RemoveLastFireSound(int slot)
    {
        ClearLastFireSound(slot);
    }

    /// <summary>
    /// Holds the last resolved fire events for a player slot.
    /// </summary>
    private sealed class LastFireSound
    {
        public int ItemDefIndex { get; }
        public string? CustomEvent { get; }
        public string? OfficialEvent { get; }
        public long UpdatedAtMs { get; }

            /// <summary>
        /// Initializes a cached fire event entry.
        /// </summary>
        public LastFireSound(int itemDefIndex, string? customEvent, string? officialEvent, long updatedAtMs)
        {
            ItemDefIndex = itemDefIndex;
            CustomEvent = customEvent;
            OfficialEvent = officialEvent;
            UpdatedAtMs = updatedAtMs;
        }
    }

    /// <summary>
    /// Emits a sound event to all players.
    /// </summary>
    private static void EmitToAllPlayers(CCSPlayerController source, string eventName)
    {
        EmitToPlayers(source, eventName, _ => true);
    }

    /// <summary>
    /// Emits a sound event to a filtered set of players.
    /// </summary>
    private static void EmitToPlayers(CCSPlayerController source, string eventName, Func<CCSPlayerController, bool> filter)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        RecipientFilter recipientFilter = new();
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid && filter(player))
            {
                recipientFilter.Add(player);
            }
        }

        source.EmitSound(eventName, recipientFilter);
    }



    /// <summary>
    /// Resolves custom/official fire events for a weapon, optionally using event flags.
    /// </summary>
    private bool TryResolveFireEvents(CCSPlayerController player, CBasePlayerWeapon weapon, EventWeaponFire? @event,
        out string? customEvent, out string? officialEvent, out int itemDefIndex)
    {
        customEvent = null;
        officialEvent = null;
        itemDefIndex = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;

        WeaponSoundOverride? customOverride = null;
        string? mappedSubclass = null;

        if (_subclassByWeaponHandle.TryGetValue(weapon.Handle, out var handleSubclass))
        {
            if (WeaponSubclassUtils.IsSubclassMatchWeapon(weapon, itemDefIndex, handleSubclass) &&
                _overrideBySubclass.TryGetValue(handleSubclass, out customOverride))
            {
                mappedSubclass = handleSubclass;
            }
            else
            {
                _subclassByWeaponHandle.Remove(weapon.Handle);
            }
        }

        if (customOverride == null && _playerSubclassByBase.TryGetValue(player.SteamID, out var equippedByBase))
        {
            if (WeaponSubclassUtils.TryGetAlternateBase(weapon.DesignerName, itemDefIndex, out var alternateBase) &&
                equippedByBase.TryGetValue(alternateBase, out var alternateSubclass))
            {
                mappedSubclass = alternateSubclass;
                _overrideBySubclass.TryGetValue(alternateSubclass, out customOverride);
            }
            else if (equippedByBase.TryGetValue(weapon.DesignerName, out var baseSubclass))
            {
                mappedSubclass = baseSubclass;
                _overrideBySubclass.TryGetValue(baseSubclass, out customOverride);
            }
        }

        if (customOverride != null && mappedSubclass != null)
        {
            TrackWeaponSubclass(weapon, mappedSubclass);
        }

        _overrideByItemDefIndex.TryGetValue(itemDefIndex, out var officialOverride);

        if (officialOverride == null)
        {
            if (@event != null)
            {
                if (WeaponSoundUtils.TryResolveFallbackItemDefIndex(@event, weapon, out var fallbackIndex))
                {
                    _overrideByItemDefIndex.TryGetValue(fallbackIndex, out officialOverride);
                }
            }
            else
            {
                if (WeaponSoundUtils.TryResolveFallbackItemDefIndex(weapon, out var fallbackIndex))
                {
                    _overrideByItemDefIndex.TryGetValue(fallbackIndex, out officialOverride);
                }
            }
        }

        if (customOverride != null)
        {
            customEvent = @event != null
                ? WeaponSoundUtils.ResolveTargetEvent(@event, weapon, customOverride.TargetEvent, customOverride.TargetEventUnsilenced)
                : WeaponSoundUtils.ResolveTargetEvent(weapon, customOverride.TargetEvent, customOverride.TargetEventUnsilenced);

            if (string.IsNullOrWhiteSpace(customEvent))
            {
                customOverride = null;
            }
        }

        if (officialOverride != null)
        {
            officialEvent = @event != null
                ? WeaponSoundUtils.ResolveTargetEvent(@event, weapon, officialOverride.TargetEvent, officialOverride.TargetEventUnsilenced)
                : WeaponSoundUtils.ResolveTargetEvent(weapon, officialOverride.TargetEvent, officialOverride.TargetEventUnsilenced);
        }

        return customOverride != null || !string.IsNullOrWhiteSpace(officialEvent);
    }

    /// <summary>
    /// Updates the pawn-index cache for fast 452 lookups.
    /// </summary>
    private void UpdatePawnIndexCache(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid)
        {
            return;
        }

        var pawnIndex = (int)pawn.Index;
        if (_pawnIndexBySlot.TryGetValue(player.Slot, out var oldIndex) && oldIndex != pawnIndex)
        {
            _playerByPawnIndex.Remove(oldIndex);
        }

        _pawnIndexBySlot[player.Slot] = pawnIndex;
        _playerByPawnIndex[pawnIndex] = player;
    }

    /// <summary>
    /// Removes pawn-index cache entries for a player.
    /// </summary>
    private void RemovePawnIndexCache(CCSPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (_pawnIndexBySlot.TryGetValue(player.Slot, out var pawnIndex))
        {
            _pawnIndexBySlot.Remove(player.Slot);
            _playerByPawnIndex.Remove(pawnIndex);
        }
    }

    /// <summary>
    /// Resolves the shooter controller from a pawn index.
    /// </summary>
    private CCSPlayerController? FindShooterByPawnIndex(int pawnIndex)
    {
        if (pawnIndex <= 0)
        {
            return null;
        }

        if (_playerByPawnIndex.TryGetValue(pawnIndex, out var cached) &&
            cached.IsValid && cached.PlayerPawn?.Value?.Index == pawnIndex)
        {
            return cached;
        }

        foreach (var candidate in Utilities.GetPlayers())
        {
            if (!candidate.IsValid)
            {
                continue;
            }

            var pawn = candidate.PlayerPawn?.Value;
            if (candidate.Index == pawnIndex || (pawn != null && pawn.IsValid && pawn.Index == pawnIndex))
            {
                UpdatePawnIndexCache(candidate);
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the custom sound toggle for a player and caches it by slot.
    /// </summary>
    private void SetCustomSoundEnabledForPlayer(CCSPlayerController player, bool enabled)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        SetCustomSoundEnabled(player.SteamID, enabled);
        if (IsValidSlot(player.Slot))
        {
            _customSoundEnabledBySlot[player.Slot] = enabled;
        }
    }

    /// <summary>
    /// Clears the cached toggle for a player slot.
    /// </summary>
    private void ClearCustomSoundEnabledForPlayer(CCSPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (IsValidSlot(player.Slot))
        {
            _customSoundEnabledBySlot[player.Slot] = Config.CustomSoundDefaultEnabled;
        }
    }

    /// <summary>
    /// Updates the slot cache for a SteamID if the player is connected.
    /// </summary>
    private void SetCustomSoundEnabledFromSteamId(ulong steamId, bool enabled)
    {
        SetCustomSoundEnabled(steamId, enabled);
        foreach (var candidate in Utilities.GetPlayers())
        {
            if (!candidate.IsValid || candidate.SteamID != steamId)
            {
                continue;
            }

            if (IsValidSlot(candidate.Slot))
            {
                _customSoundEnabledBySlot[candidate.Slot] = enabled;
            }

            break;
        }
    }

    /// <summary>
    /// Returns a cached fire sound entry if present.
    /// </summary>
    private LastFireSound? TryGetLastFireSound(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return null;
        }

        return _lastFireSoundsBySlot[slot];
    }

    /// <summary>
    /// Writes a cached fire sound entry for a slot.
    /// </summary>
    private void SetLastFireSound(int slot, LastFireSound sound)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _lastFireSoundsBySlot[slot] = sound;
    }

    /// <summary>
    /// Clears cached fire sound data for a slot.
    /// </summary>
    private void ClearLastFireSound(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _lastFireSoundsBySlot[slot] = null;
    }

    /// <summary>
    /// Validates a player slot index.
    /// </summary>
    private static bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < MaxPlayerSlots;
    }

    [ConsoleCommand("css_emsound", "Toggle custom weapon sounds")]
    [CommandHelper(0, "Toggle custom weapon sounds", CommandUsage.CLIENT_ONLY)]
    /// <summary>
    /// Toggles custom sound playback for a player.
    /// </summary>
    public void OnToggleCustomSound(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.SteamID == 0)
        {
            return;
        }

        var enabled = !IsCustomSoundEnabled(player);
        SetCustomSoundEnabledForPlayer(player, enabled);
        _ = SaveCustomSoundSettingAsync(player.SteamID, enabled);

        var message = Localizer.ForPlayer(player, enabled ? "emsound.enabled" : "emsound.disabled");
        player.PrintToChat(message);
    }

    /// <summary>
    /// Checks whether custom sounds are enabled for a player.
    /// </summary>
    private bool IsCustomSoundEnabled(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
        {
            return false;
        }

        if (IsValidSlot(player.Slot))
        {
            return _customSoundEnabledBySlot[player.Slot];
        }

        return GetCustomSoundEnabled(player.SteamID);
    }

    /// <summary>
    /// Returns the cached toggle state or the default.
    /// </summary>
    private bool GetCustomSoundEnabled(ulong steamId)
    {
        lock (_customSoundLock)
        {
            if (_customSoundEnabledBySteamId.TryGetValue(steamId, out var enabled))
            {
                return enabled;
            }
        }

        return Config.CustomSoundDefaultEnabled;
    }

    /// <summary>
    /// Updates the cached toggle state for a player.
    /// </summary>
    private void SetCustomSoundEnabled(ulong steamId, bool enabled)
    {
        lock (_customSoundLock)
        {
            _customSoundEnabledBySteamId[steamId] = enabled;
        }
    }

    /// <summary>
    /// Removes the cached toggle state for a player.
    /// </summary>
    private void RemoveCustomSoundEnabled(ulong steamId)
    {
        lock (_customSoundLock)
        {
            _customSoundEnabledBySteamId.Remove(steamId);
        }
    }


    /// <summary>
    /// Loads the toggle from storage and caches it.
    /// </summary>
    private async Task LoadCustomSoundSettingAsync(ulong steamId)
    {
        var store = _settingsStore;
        if (steamId == 0 || store == null || !store.Enabled)
        {
            return;
        }

        var result = await store.LoadEnabledAsync(steamId);
        if (result.HasValue)
        {
            Server.NextFrame(() => SetCustomSoundEnabledFromSteamId(steamId, result.Value));
            return;
        }

        await store.SaveEnabledAsync(steamId, Config.CustomSoundDefaultEnabled);
    }

    /// <summary>
    /// Persists the toggle state to storage.
    /// </summary>
    private Task SaveCustomSoundSettingAsync(ulong steamId, bool enabled)
    {
        var store = _settingsStore;
        if (steamId == 0 || store == null || !store.Enabled)
        {
            return Task.CompletedTask;
        }

        return store.SaveEnabledAsync(steamId, enabled);
    }
}
