using System.Reflection;
using CounterStrikeSharp.API.Core;

namespace EmitSoundEditor.Utils;

internal static class WeaponSoundUtils
{
    /// <summary>
    /// Selects the silenced or unsilenced event name based on current weapon state.
    /// </summary>
    internal static string ResolveTargetEvent(EventWeaponFire @event, CBasePlayerWeapon weapon, string targetEvent, string targetEventUnsilenced)
    {
        if (!string.IsNullOrWhiteSpace(targetEventUnsilenced) && !IsSilenced(@event, weapon))
        {
            return targetEventUnsilenced;
        }

        return targetEvent;
    }

    /// <summary>
    /// Selects the silenced or unsilenced event name based on weapon state when no event is available.
    /// </summary>
    internal static string ResolveTargetEvent(CBasePlayerWeapon weapon, string targetEvent, string targetEventUnsilenced)
    {
        if (!string.IsNullOrWhiteSpace(targetEventUnsilenced) && !IsSilenced(weapon))
        {
            return targetEventUnsilenced;
        }

        return targetEvent;
    }

    /// <summary>
    /// Maps weapon names to a fallback item definition index for M4A1/USP variants.
    /// </summary>
    internal static bool TryResolveFallbackItemDefIndex(EventWeaponFire @event, CBasePlayerWeapon weapon, out int itemDefIndex)
    {
        itemDefIndex = 0;
        var weaponName = @event.Weapon;
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            weaponName = weapon.DesignerName;
        }

        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return false;
        }

        weaponName = weaponName.Trim().ToLowerInvariant();

        switch (weaponName)
        {
            case "weapon_m4a1":
            case "weapon_m4a1_silencer":
                itemDefIndex = @event.Silenced ? 60 : 16;
                return true;
            case "weapon_hkp2000":
            case "weapon_usp_silencer":
                itemDefIndex = @event.Silenced ? 61 : 32;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Maps weapon names to a fallback item definition index using weapon state only.
    /// </summary>
    internal static bool TryResolveFallbackItemDefIndex(CBasePlayerWeapon weapon, out int itemDefIndex)
    {
        itemDefIndex = 0;
        var weaponName = weapon.DesignerName;
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return false;
        }

        weaponName = weaponName.Trim().ToLowerInvariant();

        switch (weaponName)
        {
            case "weapon_m4a1":
            case "weapon_m4a1_silencer":
                itemDefIndex = IsSilenced(weapon) ? 60 : 16;
                return true;
            case "weapon_hkp2000":
            case "weapon_usp_silencer":
                itemDefIndex = IsSilenced(weapon) ? 61 : 32;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the weapon is currently silenced using event flags and weapon properties.
    /// </summary>
    private static bool IsSilenced(EventWeaponFire @event, CBasePlayerWeapon weapon)
    {
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

        return IsSilenced(weapon);
    }

    /// <summary>
    /// Determines whether the weapon is currently silenced using weapon properties only.
    /// </summary>
    private static bool IsSilenced(CBasePlayerWeapon weapon)
    {
        var itemDefIndex = (int)weapon.AttributeManager.Item.ItemDefinitionIndex;
        if (itemDefIndex != 60 && itemDefIndex != 61)
        {
            return false;
        }

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

        return false;
    }

    /// <summary>
    /// Reads a boolean value from a game event field if available.
    /// </summary>
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

    /// <summary>
    /// Reads a boolean property via reflection.
    /// </summary>
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

    /// <summary>
    /// Reads a boolean field via reflection.
    /// </summary>
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
