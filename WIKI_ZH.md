# EmitSoundEditor 中文 Wiki

本文档描述从音效事件编写到打包、编译、配置插件的完整流程。

## 0) 商店与子类配置（AG2）

### 概要
该流程通过从“直接模型替换”切换为“基于 subclass 的武器替换”，恢复了 AG2 更新后的 customweapon 支持。

### 变更点
- 自定义武器通过 `ChangeSubclass` 应用模型，不再使用 viewmodel/worldmodel 覆盖。
- 装备/检视流程解析 `base:subclass`，仅当当前武器 base 匹配时才应用 subclass。
- 同一 base 只允许装备一个皮肤，避免冲突。
- 旧的 viewmodel/worldmodel 辅助逻辑已注释，并注明 AG2 不再使用这些路径。

### AG2 下的装备逻辑
- 商店中的自定义武器使用 `weapon` 字段：`base:subclass`（例如 `weapon_knife:weapon_knife_karambit+1550`）。
- 实体创建或装备时，插件会遍历玩家已装备的条目，并对匹配 base 的武器调用 `ChangeSubclass`。
- 检视会临时切换当前武器的 subclass，然后恢复。

### 格式说明
- 商店配置使用 `base:subclass`。
- `weapons.vdata` 中只定义 **subclass**（例如 `weapon_knife_karambit+1550`）。
- `EmitSoundEditor.json` 的 `overrides[].subclass` 也只填 **subclass**。

### weapon.vdata 外部配置
要让 AG2 模型生效，必须在 `weapons.vdata` 中定义 subclass（使用 Source2Viewer 从 Valve pak 解包）：

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

- 新增 subclass 条目（例如 `weapon_knife_karambit+1550`），将 `m_szModel_AG2`（或需要时 `m_szWorldModel`）指向你的模型路径。
- 上传模型到工坊，确保 `_c` 文件存在（`.vmdl_c`）并位于你的 addon 路径下。

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

- 商店配置中 `weapon` 必须填写匹配的 `base:subclass`。
- 插件会在运行时应用 subclass 并显示新模型。

### 参考来源
方案参考：
https://github.com/exkludera-cssharp/equipments/issues/5#issuecomment-3603250775

## 1) 准备音频文件

- 源文件建议使用 `.wav`
- 路径必须稳定（路径会写入 soundevents）

示例目录：

```
content/csgo_addons/example_addon/
  sounds/
    example/
      weapon_fire.wav
      weapon_fire_silenced.wav
```

## 2) 创建 soundevents 文件

在你的 addon 中创建 KV3 soundevents 文件：

```
content/csgo_addons/example_addon/soundevents/example_custom_sounds.vsndevts
```

最小示例（请替换为你的名称和路径）：

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

说明：
- `vsnd_files` 必须指向编译后的 `.vsnd` 路径
- 事件名称需保持唯一且稳定

### 自定义 vsndevts 文件名

如果不使用 `soundevents_addon.vsndevts`，引擎不会自动注册你的 soundevents 文件。此时必须通过 Resource Precacher 预载该自定义 `.vsndevts`，否则事件可能无法播放。

建议使用自定义文件名以避免与地图冲突，并务必进行预载。

## 3) 编译 sounds 与 soundevents

使用 Source 2 的 ResourceCompiler 编译音频和事件：

```
resourcecompiler.exe -game "P:\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo" -compile "P:\Steam\steamapps\common\Counter-Strike Global Offensive\content\csgo_addons\example_addon\soundevents\example_custom_sounds.vsndevts"
```

如果要编译整个 addon 目录，请确保目录内没有无效或残留文件。

## 4) 打包资源

- 使用 Workshop Tools 将编译好的资源打包为 VPK
- 确认 VPK 中包含：
  - `soundevents/example_custom_sounds.vsndevts_c`
  - `sounds/.../*.vsnd_c`

## 5) 服务端与客户端分发

- 确保服务器加载该 addon，客户端能下载资源
- 确认运行时能找到对应的 soundevents 文件

## 6) 配置插件

`.\counterstrikesharp\configs\plugins\EmitSoundEditor\EmitSoundEditor.json`：

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

说明：
- `official_overrides` 应指向“副本事件”（建议统一前缀，例如 `dup.`）。

### item_def_index 对照表

| item_def_index 参数 | 武器 |
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

## 7) 验证清单

- `EmitSoundEditor.json` 加载无报错
- 使用对应武器开火能播放指定音效
- M4A1-S / USP-S 在消音与非消音状态能正确切换

## 常见问题

- 没有声音：确认事件名存在于 `.vsndevts` 文件中
- 编译报 KV3 错误：确认 KV3 头正确、只有一个根对象
- 控制台能播放但游戏中不播放：确认 addon 已挂载且路径一致
