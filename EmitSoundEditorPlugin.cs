using System;
using System.Collections.Generic;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
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
        HookUserMessage(452, OnWeaponFireUserMessage, HookMode.Pre);
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
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

    public void OnConfigParsed(EmitSoundEditorConfig config)
    {
        Config = config;
        RebuildOverrideMap();
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
            if (IsSubclassMatchWeapon(weapon, itemDefIndex, handleSubclass) &&
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
            if (TryGetAlternateBase(weapon.DesignerName, itemDefIndex, out var alternateBase) &&
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

        string? targetEvent = null;
        if (customOverride != null)
        {
            targetEvent = ResolveTargetEvent(@event, weapon, customOverride.TargetEvent, customOverride.TargetEventUnsilenced);
        }
        else if (officialOverride != null)
        {
            targetEvent = ResolveTargetEvent(@event, weapon, officialOverride.TargetEvent, officialOverride.TargetEventUnsilenced);
        }

        if (string.IsNullOrWhiteSpace(targetEvent))
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

        EmitToAllPlayers(player, targetEvent);
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
            if (IsSubclassMatchWeapon(weapon, itemDefIndex, handleSubclass))
            {
                return _overrideBySubclass.ContainsKey(handleSubclass);
            }

            _subclassByWeaponHandle.Remove(weapon.Handle);
        }

        if (!_playerSubclassByBase.TryGetValue(player.SteamID, out var equippedByBase))
        {
            return false;
        }

        if (TryGetAlternateBase(weapon.DesignerName, itemDefIndex, out var alternateBase) &&
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

        return TryParseWeaponSpec(weaponSpec, out weaponBase, out weaponSubclass);
    }

    private static bool TryParseWeaponSpec(string weaponSpec, out string weaponBase, out string weaponSubclass)
    {
        weaponBase = string.Empty;
        weaponSubclass = string.Empty;

        var parts = weaponSpec.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        weaponBase = parts[0];
        weaponSubclass = parts[1];
        return !string.IsNullOrEmpty(weaponBase) && !string.IsNullOrEmpty(weaponSubclass);
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

    private static bool TryGetAlternateBase(string designerName, int itemDefIndex, out string alternateBase)
    {
        alternateBase = string.Empty;
        if (itemDefIndex == 60 && designerName.Equals("weapon_m4a1", StringComparison.OrdinalIgnoreCase))
        {
            alternateBase = "weapon_m4a1_silencer";
            return true;
        }

        if (itemDefIndex == 61 && designerName.Equals("weapon_hkp2000", StringComparison.OrdinalIgnoreCase))
        {
            alternateBase = "weapon_usp_silencer";
            return true;
        }

        return false;
    }

    private static bool IsSubclassMatchWeapon(CBasePlayerWeapon weapon, int itemDefIndex, string subclass)
    {
        if (!TryGetSubclassBase(subclass, out var subclassBase))
        {
            return false;
        }

        if (string.Equals(subclassBase, weapon.DesignerName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetAlternateBase(weapon.DesignerName, itemDefIndex, out var alternateBase) &&
            string.Equals(subclassBase, alternateBase, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetSubclassBase(string rawSubclass, out string baseName)
    {
        baseName = string.Empty;
        if (string.IsNullOrWhiteSpace(rawSubclass))
        {
            return false;
        }

        var subclass = rawSubclass;
        var colonIndex = subclass.IndexOf(":", StringComparison.Ordinal);
        if (colonIndex >= 0 && colonIndex + 1 < subclass.Length)
        {
            subclass = subclass[(colonIndex + 1)..];
        }

        var plusIndex = subclass.IndexOf("+", StringComparison.Ordinal);
        baseName = (plusIndex >= 0 ? subclass[..plusIndex] : subclass).Trim();
        return !string.IsNullOrWhiteSpace(baseName);
    }

    private static void EmitToAllPlayers(CCSPlayerController source, string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        RecipientFilter filter = new();
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid)
            {
                filter.Add(player);
            }
        }

        source.EmitSound(eventName, filter);
    }

    private static string ResolveTargetEvent(EventWeaponFire @event, CBasePlayerWeapon weapon, string targetEvent, string targetEventUnsilenced)
    {
        if (!string.IsNullOrWhiteSpace(targetEventUnsilenced) && !IsSilenced(@event, weapon))
        {
            return targetEventUnsilenced;
        }

        return targetEvent;
    }

    private static bool IsSilenced(EventWeaponFire @event, CBasePlayerWeapon weapon)
    {
        var itemDefIndex = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;
        if (itemDefIndex == 60 || itemDefIndex == 61)
        {
            var schemaWeapon = weapon.As<CCSWeaponBase>();
            if (schemaWeapon != null)
            {
                if (TryGetBoolProperty(schemaWeapon, "SilencerOn", out var weaponSilenced))
                {
                    return weaponSilenced;
                }

                if (TryGetBoolProperty(schemaWeapon, "IsSilenced", out weaponSilenced))
                {
                    return weaponSilenced;
                }

                if (TryGetBoolField(schemaWeapon, "m_bSilencerOn", out weaponSilenced))
                {
                    return weaponSilenced;
                }

                if (TryGetBoolField(schemaWeapon, "m_bIsSilenced", out weaponSilenced))
                {
                    return weaponSilenced;
                }
            }

            if (TryGetBoolProperty(weapon, "SilencerOn", out var fallbackSilenced))
            {
                return fallbackSilenced;
            }

            if (TryGetBoolProperty(weapon, "IsSilenced", out fallbackSilenced))
            {
                return fallbackSilenced;
            }

            if (TryGetBoolField(weapon, "m_bSilencerOn", out fallbackSilenced))
            {
                return fallbackSilenced;
            }

            if (TryGetBoolField(weapon, "m_bIsSilenced", out fallbackSilenced))
            {
                return fallbackSilenced;
            }
        }

        if (TryGetGameEventBool(@event, "silenced", out var eventSilenced))
        {
            return eventSilenced;
        }

        if (TryGetGameEventBool(@event, "is_silenced", out eventSilenced))
        {
            return eventSilenced;
        }

        if (TryGetBoolProperty(@event, "Silenced", out eventSilenced))
        {
            return eventSilenced;
        }

        if (TryGetBoolProperty(@event, "IsSilenced", out eventSilenced))
        {
            return eventSilenced;
        }

        return false;
    }

    private static bool TryGetGameEventBool(object target, string name, out bool value)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        var method = target.GetType().GetMethod("GetBool", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (method == null)
        {
            return false;
        }

        var raw = method.Invoke(target, new object[] { name });
        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

    private static bool TryGetBoolProperty(object target, string name, out bool value)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return false;
        }

        var propertyType = property.PropertyType;
        if (propertyType != typeof(bool) && !(propertyType.IsByRef && propertyType.GetElementType() == typeof(bool)))
        {
            return false;
        }

        var raw = property.GetValue(target);
        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

    private static bool TryGetBoolField(object target, string name, out bool value)
    {
        value = false;
        if (target == null)
        {
            return false;
        }

        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(bool))
        {
            return false;
        }

        var raw = field.GetValue(target);
        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

}



