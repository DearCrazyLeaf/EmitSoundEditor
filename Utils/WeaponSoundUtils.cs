using System.Reflection;
using CounterStrikeSharp.API.Core;

namespace EmitSoundEditor.Utils;

internal static class WeaponSoundUtils
{
    internal static string ResolveTargetEvent(EventWeaponFire @event, CBasePlayerWeapon weapon, string targetEvent, string targetEventUnsilenced)
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
