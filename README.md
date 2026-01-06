<p align="center">
  <a href="https://github.com/roflmuffin/CounterStrikeSharp">
    <img src="https://docs.cssharp.dev/images/cssharp.svg" width="60" height="60" style="vertical-align: middle; margin-right: 10px;" />
  </a>
</p>

<h3 align="center">
  <span style="vertical-align: middle; font-weight: 600;">
    <code style="vertical-align: middle;">CounterStrikeSharp</code>
  </span>
</h3>

---

# EmitSoundEditor
[![Guide](https://img.shields.io/badge/Full%20step--by--step%20guide-Guide-blue?style=for-the-badge)](https://github.com/DearCrazyLeaf/EmitSoundEditor/blob/main/SUPPORT.md)

[![中文版介绍](https://img.shields.io/badge/跳转到中文版-中文介绍-red)](#中文版介绍)
[![Release](https://img.shields.io/github/v/release/DearCrazyLeaf/EmitSoundEditor?include_prereleases&color=blueviolet)](https://github.com/DearCrazyLeaf/EmitSoundEditor/releases/latest)
[![License](https://img.shields.io/badge/License-GPL%203.0-orange)](https://www.gnu.org/licenses/gpl-3.0.txt)
[![Issues](https://img.shields.io/github/issues/DearCrazyLeaf/EmitSoundEditor?color=darkgreen)](https://github.com/DearCrazyLeaf/EmitSoundEditor/issues)
[![Pull Requests](https://img.shields.io/github/issues-pr/DearCrazyLeaf/EmitSoundEditor?color=blue)](https://github.com/DearCrazyLeaf/EmitSoundEditor/pulls)
[![Downloads](https://img.shields.io/github/downloads/DearCrazyLeaf/EmitSoundEditor/total?color=brightgreen)](https://github.com/DearCrazyLeaf/EmitSoundEditor/releases)
[![GitHub Stars](https://img.shields.io/github/stars/DearCrazyLeaf/EmitSoundEditor?color=yellow)](https://github.com/DearCrazyLeaf/EmitSoundEditor/stargazers)

**A Counter-Strike 2 server plugin that replaces weapon fire sounds based on equipped custom subclasses or official weapon definitions**

## Features

- **Custom weapon override**: play a custom soundevent when a player fires a store-equipped subclass
- **Official weapon fallback**: map normal weapons by `item_def_index` to duplicated soundevents
- **Silencer aware**: optional `target_event_unsilenced` for M4A1-S / USP-S
- **Low overhead**: constant-time lookups per fire event

## Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [Store API](https://github.com/schwarper/cs2-store) (required for custom weapon mapping)
- [Resource Precacher](https://github.com/KillStr3aK/ResourcePrecacher) (required when using custom .vsndevts names)

## Installation

1. Download the latest release
2. Extract to `game/csgo/addons/counterstrikesharp/plugins/EmitSoundEditor`
3. Restart the server or load the plugin

## Configuration

Path `.\counterstrikesharp\configs\plugins\EmitSoundEditor\EmitSoundEditor.json`:

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
    },
    {
      "item_def_index": 60,
      "target_event": "dup.Weapon_M4A1.Silenced",
      "target_event_unsilenced": "dup.Weapon_M4A4.Single"
    }
  ],
  "force_mute_all_firebullets": false
}
```

### Fields

| Field | Type | Description |
| --- | --- | --- |
| `overrides` | array | Custom weapon subclass overrides. |
| `overrides[].subclass` | string | Right side of `weapon_base:subclass` |
| `overrides[].target_event` | string | Soundevent to play on fire |
| `overrides[].target_event_unsilenced` | string | Optional; used when the silencer is off |
| `official_overrides` | array | Fallback mapping by `item_def_index` to duplicated soundevents (example prefix: `dup.`) |
| `official_overrides[].item_def_index` | number | Official weapon item definition index |
| `official_overrides[].target_event` | string | Duplicated soundevent to play on fire |
| `official_overrides[].target_event_unsilenced` | string | Optional; used when the silencer is off |
| `force_mute_all_firebullets` | boolean | Optional global mute for native firebullet sounds |


## Store + subclass setup (AG2 custom weapons)

**Short version (see Wiki for the full guide)**:
- Store uses `base:subclass` in the `weapon` field (e.g., `weapon_knife:weapon_knife_karambit+1550`)
- `weapons.vdata` defines the **subclass only** (e.g., `weapon_knife_karambit+1550`)
- `EmitSoundEditor.json` uses **subclass only** in `overrides[].subclass`

**How it works**:
- On equip or entity creation, the Store plugin parses `base:subclass` and calls `ChangeSubclass` when the active base matches
- Inspect temporarily swaps the active weapon's subclass and then resets it
- Only one skin is equipped per weapon base to avoid conflicts

Store config example:

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
> [!NOTE]
> ### Vdata note:
> - Add a subclass entry like `weapon_knife_karambit+1550` and point `m_szModel_AG2` to your model
> - Ensure the `.vmdl_c` exists under your addon path (uploaded/compiled)

## Workflow Overview

1. Author your soundevents (use any naming scheme you like)
2. Compile sounds and `.vsndevts`
3. Package to VPK / Workshop and distribute to clients
4. Configure `EmitSoundEditor.json`

**Full Guide**:

- [Full step-by-step guide](https://github.com/DearCrazyLeaf/EmitSoundEditor/blob/main/SUPPORT.md)

- [Chinese Version](./SUPPORT.md#zh)


## Custom vsndevts naming

- If you do not use `soundevents_addon.vsndevts`, the engine will not auto-register your soundevents file. You must precache the custom `.vsndevts` via Resource Precacher, otherwise events may not play.

**Recommended**: use a custom filename to avoid map conflicts, and always precache it

## Notes

- `target_event_unsilenced` only applies to silencer-capable weapons (M4A1-S / USP-S)
- If a custom subclass is equipped, it takes priority over `official_overrides`

## License

<a href="https://www.gnu.org/licenses/gpl-3.0.txt" target="_blank" style="margin-left: 10px; text-decoration: none;">
    <img src="https://img.shields.io/badge/License-GPL%203.0-orange?style=for-the-badge&logo=gnu" alt="GPL v3 License">
</a>

---

# 中文版介绍
[![Guide](https://img.shields.io/badge/Chinese%20Version-中文指南-blue?style=for-the-badge)](./SUPPORT.md#zh)

[![English](https://img.shields.io/badge/Back%20to%20English-English-red)](#EmitSoundEditor)
[![Release](https://img.shields.io/github/v/release/DearCrazyLeaf/EmitSoundEditor?include_prereleases&color=blueviolet&label=最新版本)](https://github.com/DearCrazyLeaf/EmitSoundEditor/releases/latest)
[![License](https://img.shields.io/badge/许可证-GPL%203.0-orange)](https://www.gnu.org/licenses/gpl-3.0.txt)
[![Issues](https://img.shields.io/github/issues/DearCrazyLeaf/EmitSoundEditor?color=darkgreen&label=反馈)](https://github.com/DearCrazyLeaf/EmitSoundEditor/issues)
[![Pull Requests](https://img.shields.io/github/issues-pr/DearCrazyLeaf/EmitSoundEditor?color=blue&label=请求)](https://github.com/DearCrazyLeaf/EmitSoundEditor/pulls)
[![Downloads](https://img.shields.io/github/downloads/DearCrazyLeaf/EmitSoundEditor/total?color=brightgreen&label=下载)](https://github.com/DearCrazyLeaf/EmitSoundEditor/releases)
[![GitHub Stars](https://img.shields.io/github/stars/DearCrazyLeaf/EmitSoundEditor?color=yellow&label=标星)](https://github.com/DearCrazyLeaf/EmitSoundEditor/stargazers)

**一个用于 CS2 服务器的枪声替换插件，可根据商店自定义武器或官方武器类型播放指定音效事件**

## 功能

- **自定义武器替换**：根据商店装备的 subclass 播放指定音效
- **官方武器回退**：按 `item_def_index` 映射官方武器事件副本
- **消音器识别**：支持 `target_event_unsilenced` 分支
- **性能友好**：开火事件使用常量时间查找

## 依赖

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [Store API](https://github.com/schwarper/cs2-store)（自定义武器映射所需）
- [Resource Precacher](https://github.com/KillStr3aK/ResourcePrecacher)（当使用自定义 .vsndevts 名称时必需）

## 安装

1. 下载最新版本
2. 解压到 `game/csgo/addons/counterstrikesharp/plugins/EmitSoundEditor`
3. 重启服务器或加载插件

## 配置说明

路径 `.\counterstrikesharp\configs\plugins\EmitSoundEditor\EmitSoundEditor.json`：

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
    },
    {
      "item_def_index": 60,
      "target_event": "dup.Weapon_M4A1.Silenced",
      "target_event_unsilenced": "dup.Weapon_M4A4.Single"
    }
  ],
  "force_mute_all_firebullets": false
}
```

### 字段说明

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `overrides` | array | 自定义武器替换列表 |
| `overrides[].subclass` | string | `weapon_base:subclass` 的右侧部分 |
| `overrides[].target_event` | string | 开火时播放的音效事件 |
| `overrides[].target_event_unsilenced` | string | 可选，不消音时使用。 |
| `official_overrides` | array | 官方武器事件副本映射（建议使用 `dup.` 之类的前缀） |
| `official_overrides[].item_def_index` | number | 官方武器定义索引 |
| `official_overrides[].target_event` | string | 对应的副本音效事件 |
| `official_overrides[].target_event_unsilenced` | string | 可选，不消音时使用 |
| `force_mute_all_firebullets` | boolean | 可选，全局静音原始开火音效 |

## 商店与子类配置（AG2）

**简版说明（完整内容见 Wiki）**：
- 商店 `weapon` 使用 `base:subclass`（例如 `weapon_knife:weapon_knife_karambit+1550`）
- `weapons.vdata` 只定义 **subclass**（例如 `weapon_knife_karambit+1550`）
- `EmitSoundEditor.json` 的 `overrides[].subclass` 也只填 **subclass**

**工作流程**：
- 装备或实体创建时，商店插件解析 `base:subclass`，当武器 base 匹配时调用 `ChangeSubclass`
- 检视会临时切换当前武器的 subclass，之后恢复
- 同一 base 只允许装备一个皮肤，避免冲突

商店配置示例：

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
> [!NOTE]
> ### Vdata 说明：
> - 添加 subclass 条目，例如 `weapon_knife_karambit+1550`，并将 `m_szModel_AG2` 指向你的模型路径
> - 确保 `.vmdl_c` 已在 addon 路径中（已上传/编译）

## 使用流程概要

1. 编写音效事件文件
2. 编译 sounds 与 `.vsndevts`
3. 打包并分发资源
4. 配置 `EmitSoundEditor.json`

**详情见**：

- [完整流程](https://github.com/DearCrazyLeaf/EmitSoundEditor/blob/main/SUPPORT.md)

- [中文版本](./SUPPORT.md#zh)

## 自定义 vsndevts 文件名

- 如果不使用 `soundevents_addon.vsndevts`，引擎不会自动注册你的 soundevents 文件，此时必须通过 Resource Precacher 预载该自定义 `.vsndevts`，否则事件可能无法播放

**建议使用自定义文件名以避免与地图冲突，并务必进行预载**

## 注意事项

- `target_event_unsilenced` 仅适用于可装消音器的武器（M4A1-S / USP-S）
- 若装备了自定义 subclass，将优先生效，覆盖 `official_overrides`


## 许可协议

<a href="https://www.gnu.org/licenses/gpl-3.0.txt" target="_blank" style="margin-left: 10px; text-decoration: none;">
    <img src="https://img.shields.io/badge/License-GPL%203.0-orange?style=for-the-badge&logo=gnu" alt="GPL v3 License">
</a>













