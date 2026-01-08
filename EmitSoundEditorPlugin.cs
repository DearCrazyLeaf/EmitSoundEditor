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
    private readonly Dictionary<ulong, bool> _customSoundEnabledBySteamId = new();
    private readonly object _customSoundLock = new();
    private CustomSoundSettingsStore? _settingsStore;
    private readonly HashSet<int> _suppressOriginalSoundSlots = new();
    private readonly HashSet<int> _suppressOriginalSoundEntityIndices = new();
    private bool _pendingInitialRefresh;
    private const int EntityIndexMask = 0x7FF;

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

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        InitializeSettingsStore();
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

    private void OnMapStart(string mapName)
    {
        _subclassByWeaponHandle.Clear();
        if (!_pendingInitialRefresh || _storeApi == null)
        {
            return;
        }

        _pendingInitialRefresh = false;
        RefreshAllPlayers();
    }


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

        SetCustomSoundEnabled(player.SteamID, Config.CustomSoundDefaultEnabled);

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

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid)
        {
            return;
        }

        RemoveCustomSoundEnabled(player.SteamID);
    }


    private void InitializeSettingsStore()
    {
        _settingsStore = new CustomSoundSettingsStore(Config.MySql, Logger);
        _ = _settingsStore.InitializeAsync();
    }

    public void OnConfigParsed(EmitSoundEditorConfig config)
    {
        Config = config;
        RebuildOverrideMap();
        InitializeSettingsStore();
    }

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

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
        {
            return HookResult.Continue;
        }

        RefreshPlayerEquipment(player);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _playerSubclassByBase.Remove(player.SteamID);
        }

        return HookResult.Continue;
    }

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

        var itemDefIndex = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;
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

        string? customEvent = null;
        if (customOverride != null)
        {
            customEvent = WeaponSoundUtils.ResolveTargetEvent(@event, weapon, customOverride.TargetEvent, customOverride.TargetEventUnsilenced);
            if (string.IsNullOrWhiteSpace(customEvent))
            {
                customOverride = null;
            }
        }

        string? officialEvent = null;
        if (officialOverride != null)
        {
            officialEvent = WeaponSoundUtils.ResolveTargetEvent(@event, weapon, officialOverride.TargetEvent, officialOverride.TargetEventUnsilenced);
        }

        if (customOverride == null && string.IsNullOrWhiteSpace(officialEvent))
        {
            return HookResult.Continue;
        }

        _suppressOriginalSoundSlots.Add(player.Slot);
        _suppressOriginalSoundEntityIndices.Add((int)player.Index);
        var playerPawn = player.PlayerPawn?.Value;
        if (playerPawn != null && playerPawn.IsValid)
        {
            _suppressOriginalSoundEntityIndices.Add((int)playerPawn.Index);
        }

        if (customOverride != null && !string.IsNullOrWhiteSpace(customEvent))
        {
            EmitToPlayers(player, customEvent, target => IsCustomSoundEnabled(target));

            if (!string.IsNullOrWhiteSpace(officialEvent))
            {
                EmitToPlayers(player, officialEvent, target => !IsCustomSoundEnabled(target));
            }
            else
            {
                EmitToPlayers(player, customEvent, target => !IsCustomSoundEnabled(target));
            }

            return HookResult.Continue;
        }

        if (!string.IsNullOrWhiteSpace(officialEvent))
        {
            EmitToAllPlayers(player, officialEvent);
        }

        return HookResult.Continue;
    }

    private HookResult OnWeaponFireUserMessage(UserMessage userMessage)
    {

        if (Config.ForceMuteAllFireBullets)
        {
            userMessage.Recipients.Clear();
            return HookResult.Continue;
        }

        var playerHandle = (int)userMessage.ReadUInt("player");
        var shouldSuppress = false;
        CCSPlayerController? matchedPlayer = null;

        if (playerHandle > 0)
        {
            var playerEntityIndex = playerHandle & EntityIndexMask;
            if (_suppressOriginalSoundEntityIndices.Remove(playerEntityIndex))
            {
                shouldSuppress = true;
            }
            else
            {
                foreach (var candidate in Utilities.GetPlayers())
                {
                    if (!candidate.IsValid)
                    {
                        continue;
                    }

                    var pawn = candidate.PlayerPawn?.Value;
                    if (candidate.Index == playerEntityIndex || (pawn != null && pawn.IsValid && pawn.Index == playerEntityIndex))
                    {
                        matchedPlayer = candidate;
                        break;
                    }
                }

                if (matchedPlayer != null)
                {
                    shouldSuppress = _suppressOriginalSoundSlots.Remove(matchedPlayer.Slot) || ShouldSuppressForPlayer(matchedPlayer);
                }
            }
        }

        if (!shouldSuppress)
        {
            return HookResult.Continue;
        }

        userMessage.Recipients.Clear();
        return HookResult.Continue;
    }

    private bool ShouldSuppressForPlayer(CCSPlayerController player)
    {
        var weapon = player.PlayerPawn?.Value?.WeaponServices?.ActiveWeapon?.Value;
        if (weapon == null || !weapon.IsValid)
        {
            return false;
        }

        var itemDefIndex = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;

        if (_subclassByWeaponHandle.TryGetValue(weapon.Handle, out var handleSubclass))
        {
            if (WeaponSubclassUtils.IsSubclassMatchWeapon(weapon, itemDefIndex, handleSubclass))
            {
                return _overrideBySubclass.ContainsKey(handleSubclass);
            }

            _subclassByWeaponHandle.Remove(weapon.Handle);
        }

        if (!_playerSubclassByBase.TryGetValue(player.SteamID, out var equippedByBase))
        {
            return false;
        }

        if (WeaponSubclassUtils.TryGetAlternateBase(weapon.DesignerName, itemDefIndex, out var alternateBase) &&
            equippedByBase.TryGetValue(alternateBase, out var alternateSubclass))
        {
            return _overrideBySubclass.ContainsKey(alternateSubclass);
        }

        if (!equippedByBase.TryGetValue(weapon.DesignerName, out var baseSubclass))
        {
            return false;
        }

        return _overrideBySubclass.ContainsKey(baseSubclass);
    }

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

    private void RefreshAllPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            RefreshPlayerEquipment(player);
        }
    }

    private static bool IsCustomWeaponItem(Dictionary<string, string> item)
    {
        return item.TryGetValue("type", out var type) &&
               string.Equals(type, "customweapon", StringComparison.OrdinalIgnoreCase);
    }

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

    private Dictionary<string, string> GetOrCreatePlayerMap(ulong steamId)
    {
        if (!_playerSubclassByBase.TryGetValue(steamId, out var map))
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _playerSubclassByBase[steamId] = map;
        }

        return map;
    }

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

    private void TrackWeaponSubclass(CBasePlayerWeapon weapon, string subclass)
    {
        if (weapon == null || !weapon.IsValid || string.IsNullOrWhiteSpace(subclass))
        {
            return;
        }

        _subclassByWeaponHandle[weapon.Handle] = subclass.Trim();
    }

    private void UntrackWeaponSubclass(CBasePlayerWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        _subclassByWeaponHandle.Remove(weapon.Handle);
    }

    private static void EmitToAllPlayers(CCSPlayerController source, string eventName)
    {
        EmitToPlayers(source, eventName, _ => true);
    }

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


    [ConsoleCommand("css_emsound", "Toggle custom weapon sounds")]
    [CommandHelper(0, "Toggle custom weapon sounds", CommandUsage.CLIENT_ONLY)]
    public void OnToggleCustomSound(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid || player.SteamID == 0)
        {
            return;
        }

        var enabled = !IsCustomSoundEnabled(player);
        SetCustomSoundEnabled(player.SteamID, enabled);
        _ = SaveCustomSoundSettingAsync(player.SteamID, enabled);

        var message = Localizer.ForPlayer(player, enabled ? "emsound.enabled" : "emsound.disabled");
        player.PrintToChat(message);
    }

    private bool IsCustomSoundEnabled(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
        {
            return false;
        }

        return GetCustomSoundEnabled(player.SteamID);
    }

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

    private void SetCustomSoundEnabled(ulong steamId, bool enabled)
    {
        lock (_customSoundLock)
        {
            _customSoundEnabledBySteamId[steamId] = enabled;
        }
    }

    private void RemoveCustomSoundEnabled(ulong steamId)
    {
        lock (_customSoundLock)
        {
            _customSoundEnabledBySteamId.Remove(steamId);
        }
    }


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
            Server.NextFrame(() => SetCustomSoundEnabled(steamId, result.Value));
            return;
        }

        await store.SaveEnabledAsync(steamId, Config.CustomSoundDefaultEnabled);
    }

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
