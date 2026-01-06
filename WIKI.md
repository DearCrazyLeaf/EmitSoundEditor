# EmitSoundEditor Wiki

Chinese version: https://github.com/DearCrazyLeaf/EmitSoundEditor/blob/main/WIKI_ZH.md

This document describes the end-to-end flow: author soundevents, compile assets, package, and configure the plugin.

## 0) Store + subclass setup (AG2 custom weapons)

### Summary
This plugin restores customweapon support for the animgraph2 (AG2) update by switching from direct model swapping to subclass-based weapon replacement.

### Changes
- Custom weapons now apply models via `ChangeSubclass` instead of viewmodel/worldmodel overrides.
- Equip/inspect paths parse `base:subclass` and apply the subclass only when the active weapon base matches.
- Conflicts are resolved per weapon base so only one skin is equipped per weapon.
- Legacy viewmodel/worldmodel helpers are commented out with notes since AG2 no longer uses them.

### How AG2 equipment works now
- Each custom weapon item in Store uses `weapon` in the format `base:subclass` (e.g., `weapon_knife:weapon_knife_karambit+1550`).
- On entity creation and item equip, the plugin checks the player?s equipped items and calls `ChangeSubclass` for the matching base weapon.
- Inspect temporarily changes the active weapon?s subclass and then resets it.

### Format notes
- Store config uses `base:subclass`.
- `weapons.vdata` defines the **subclass only** (e.g., `weapon_knife_karambit+1550`).
- `EmitSoundEditor.json` uses **subclass only** for `overrides[].subclass`.

### External weapon.vdata setup
To make AG2 models work, you must define subclasses in `weapons.vdata` (decompile it from Valve's pak using Source2Viewer):

```code
   "weapon_knife_karambit+1550" = 
    {
        m_nKillAward = 1500
        m_nDamage = 50
        m_iMaxClip1 = 0
        m_iMaxClip2 = 0
        m_iDefaultClip1 = 1
        m_iDefaultClip2 = 1
        m_bAllowFlipping = true
        m_bBuiltRightHanded = true
        m_bIsFullAuto = false
        m_nNumBullets = 1
        m_bMeleeWeapon = true
        m_iWeight = 0
        m_iRumbleEffect = 9
        m_nPrimaryReserveAmmoMax = 0
        m_nSecondaryReserveAmmoMax = 0
        m_flInaccuracyJumpInitial = 0.000000
        m_flInaccuracyJumpApex = 0.000000
        m_flInaccuracyReload = 0.000000
        m_flDeployDuration = 1.000000
        m_flDisallowAttackAfterReloadStartDuration = 5.000000
        m_nSpreadSeed = 0
        m_flRecoveryTimeCrouch = 1.000000
        m_flRecoveryTimeStand = 1.000000
        m_flRecoveryTimeCrouchFinal = -1.000000
        m_flRecoveryTimeStandFinal = -1.000000
        m_nRecoveryTransitionStartBullet = 0
        m_nRecoveryTransitionEndBullet = 0
        m_flHeadshotMultiplier = 4.000000
        m_flArmorRatio = 1.700000
        m_flPenetration = 1.000000
        m_flFlinchVelocityModifierLarge = 0.300000
        m_flFlinchVelocityModifierSmall = 0.300000
        m_flRange = 4096.000000
        m_flRangeModifier = 0.990000
        m_eSilencerType = "WEAPONSILENCER_NONE"
        m_nCrosshairMinDistance = 7
        m_nCrosshairDeltaDistance = 3
        m_flAttackMovespeedFactor = 1.000000
        m_bUnzoomsAfterShot = false
        m_bHideViewModelWhenZoomed = false
        m_nZoomLevels = 0
        m_nZoomFOV1 = 90
        m_nZoomFOV2 = 90
        m_flZoomTime0 = 0.000000
        m_flZoomTime1 = 0.000000
        m_flZoomTime2 = 0.000000
        m_flInaccuracyPitchShift = 0.000000
        m_flInaccuracyAltSoundThreshold = 0.000000
        m_bHasBurstMode = false
        m_bIsRevolver = false
        m_bCannotShootUnderwater = false
        m_flCycleTime = 
        [
            0.150000,
            0.300000,
        ]
        m_flMaxSpeed = 
        [
            250.000000,
            250.000000,
        ]
        m_flSpread = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyCrouch = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyStand = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyJump = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyLand = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyLadder = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyFire = 
        [
            0.000000,
            0.000000,
        ]
        m_flInaccuracyMove = 
        [
            0.000000,
            0.000000,
        ]
        m_flRecoilAngle = 
        [
            0.000000,
            0.000000,
        ]
        m_flRecoilAngleVariance = 
        [
            0.000000,
            0.000000,
        ]
        m_flRecoilMagnitude = 
        [
            0.000000,
            0.000000,
        ]
        m_flRecoilMagnitudeVariance = 
        [
            0.000000,
            0.000000,
        ]
        m_nTracerFrequency = 0
        m_bAutoSwitchFrom = true
        m_bAutoSwitchTo = true
        _base = "507"
        taxonomy = 
        {
            weapon = true
            self_damage_on_miss__inflicts_damage = true
            melee = true
        }
        _class = "weapon_knife"
        m_GearSlot = "GEAR_SLOT_KNIFE"
        m_GearSlotPosition = 0
        m_DefaultLoadoutPosition = "LOADOUT_POSITION_MELEE"
        m_szWorldModel = resource_name:"phase2/weapons/models/aur1c/karambit_mfsn/karambit_mfsn_ag2.vmdl"
        m_WeaponType = "WEAPONTYPE_KNIFE"
        m_aShootSounds = 
        {
            WEAPON_SOUND_EMPTY = soundevent:"Default.ClipEmpty_Rifle"
            WEAPON_SOUND_SINGLE = soundevent:"Weapon_DEagle.Single"
            WEAPON_SOUND_RELOAD = soundevent:"Default.Reload"
            WEAPON_SOUND_NEARLYEMPTY = soundevent:"Default.nearlyempty"
        }
        m_nPrice = 0
        m_nRecoilSeed = 26701
        m_szModel_AG2 = resource_name:"phase2/weapons/models/aur1c/karambit_mfsn/karambit_mfsn_ag2.vmdl"
        m_szAnimSkeleton = resource_name:"animation/skeletons/weapons/knife_karambit.vnmskel"
        m_szName = "weapon_knife_karambit"
    }
```

- Add a new subclass entry (e.g., `weapon_knife_karambit+1550`) with `m_szModel_AG2` (or `m_szWorldModel` if needed) pointing to your model path.
- Upload the model to workshop so the `_c` file exists (`.vmdl_c`) under your addon path.

```json
"Custom Weapon": {
        "karambit": {
          "karambit_custom": {
            "uniqueid": "karambitcustom",
            "type": "customweapon",
            "weapon": "weapon_knife:weapon_knife_karambit+1550",
            "price": "1000",
            "slot": "1"
          }
        }
    }
```

- In the Store config, set `weapon` to the matching `base:subclass`.
- The plugin will then apply the subclass at runtime and the new model will appear.

### Inspiration
Approach based on the method described in:
https://github.com/exkludera-cssharp/equipments/issues/5#issuecomment-3603250775

## 1) Prepare audio files

- Use `.wav` for source audio.
- Keep file paths stable (these paths are baked into the soundevents).

Example layout:

```
content/csgo_addons/example_addon/
  sounds/
    example/
      weapon_fire.wav
      weapon_fire_silenced.wav
```

## 2) Create a soundevents file

Create a KV3 soundevents file in your addon:

```
content/csgo_addons/example_addon/soundevents/example_custom_sounds.vsndevts
```

Minimal example (use your own names and paths):

```kv3
<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
{
  "weapon.example.fire" =
  {
    type = "csgo_mega"
    volume = 1.000000
    pitch = 1.000000
    vsnd_files = "sounds/example/weapon_fire.vsnd"
  }

  "weapon.example.fire_silenced" =
  {
    type = "csgo_mega"
    volume = 1.000000
    pitch = 1.000000
    vsnd_files = "sounds/example/weapon_fire_silenced.vsnd"
  }
}
```

Notes:
- `vsnd_files` must point to the compiled `.vsnd` path produced by the audio compiler.
- Keep names unique and stable.

### Custom vsndevts filenames

If you do not use `soundevents_addon.vsndevts`, the engine will not auto-register your soundevents file. You must precache the custom `.vsndevts` via Resource Precacher, otherwise events may not play.

Recommended: use a custom filename to avoid map conflicts, and always precache it.

## 3) Compile sounds and soundevents

Use Source 2 ResourceCompiler to compile both sounds and soundevents.

Example (adjust paths):

```
resourcecompiler.exe -game "P:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo" -compile "P:\Steam\steamapps\common\Counter-Strike Global Offensive\content\csgo_addons\example_addon\soundevents\example_custom_sounds.vsndevts"
```

If you want to compile all assets under the addon folder, you can run the compiler on the folder, but ensure there are no stale or invalid files.

## 4) Package the addon

- Package the compiled files into a VPK via Workshop Tools.
- Confirm the VPK includes:
  - `soundevents/example_custom_sounds.vsndevts_c`
  - `sounds/.../*.vsnd_c`

## 5) Server and client distribution

- Ensure the server loads the addon and clients download it (Workshop or other distribution).
- Verify the game can find the compiled soundevents file at runtime.

## 6) Configure the plugin

`EmitSoundEditor.json`:

```json
{
  "overrides": [
    {
      "subclass": "weapon_ak47+13",
      "target_event": "weapon.example.fire"
    },
    {
      "subclass": "weapon_m4a1_silencer+25",
      "target_event": "weapon.example.fire_silenced",
      "target_event_unsilenced": "weapon.example.fire"
    }
  ],
  "official_overrides": [
    {
      "item_def_index": 7,
      "target_event": "dup.Weapon_AK47.Single"
    }
  ],
  "force_mute_all_firebullets": false
}
```

Notes:
- `official_overrides` should point to duplicated soundevents (example prefix: `dup.`).

### item_def_index reference

| item_def_index | weapon |
| --- | --- |
| 1 | DEagle |
| 2 | ELITE |
| 3 | FiveSeven |
| 4 | Glock |
| 7 | AK47 |
| 8 | AUG |
| 9 | AWP |
| 10 | FAMAS |
| 11 | G3SG1 |
| 13 | GalilAR |
| 14 | M249 |
| 16 | M4A4 |
| 17 | MAC10 |
| 19 | P90 |
| 23 | MP5 |
| 24 | UMP45 |
| 25 | XM1014 |
| 26 | bizon |
| 27 | Mag7 |
| 28 | Negev |
| 29 | Sawedoff |
| 30 | tec9 |
| 31 | Taser |
| 32 | hkp2000 |
| 33 | MP7 |
| 34 | MP9 |
| 35 | Nova |
| 36 | P250 |
| 38 | SCAR20 |
| 39 | sg556 |
| 40 | SSG08 |
| 60 | M4A1 |
| 61 | USP |
| 63 | CZ75A |
| 64 | Revolver |

## 7) Validation checklist

- `EmitSoundEditor.json` is loaded and parsed without errors.
- When firing a mapped weapon, the specified soundevent is played.
- For silenced weapons (M4A1-S/USP-S), both silenced and unsilenced events map correctly.

## Troubleshooting

- If no sound plays, confirm the event name exists in your `.vsndevts` file.
- If the compiler reports KV3 errors, ensure the header is correct and the file has a single root object.
- If events play in console but not in-game, confirm the addon is mounted and the paths match.

