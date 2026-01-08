using System;
﻿using CounterStrikeSharp.API.Core;

namespace EmitSoundEditor.Utils;

internal static class WeaponSubclassUtils
{
    internal static bool TryParseWeaponSpec(string weaponSpec, out string weaponBase, out string weaponSubclass)
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

    internal static bool TryGetAlternateBase(string designerName, int itemDefIndex, out string alternateBase)
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

    internal static bool IsSubclassMatchWeapon(CBasePlayerWeapon weapon, int itemDefIndex, string subclass)
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
        var colonIndex = subclass.IndexOf(':');
        if (colonIndex >= 0 && colonIndex + 1 < subclass.Length)
        {
            subclass = subclass[(colonIndex + 1)..];
        }

        var plusIndex = subclass.IndexOf('+');
        baseName = (plusIndex >= 0 ? subclass[..plusIndex] : subclass).Trim();
        return !string.IsNullOrWhiteSpace(baseName);
    }
}
