# EmitSoundEditor 中文 Wiki

本文档描述从音效事件编写到打包、编译、配置插件的完整流程。

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

`EmitSoundEditor.json`：

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

## 7) 验证清单

- `EmitSoundEditor.json` 加载无报错
- 使用对应武器开火能播放指定音效
- M4A1-S / USP-S 在消音与非消音状态能正确切换

## 常见问题

- 没有声音：确认事件名存在于 `.vsndevts` 文件中
- 编译报 KV3 错误：确认 KV3 头正确、只有一个根对象
- 控制台能播放但游戏中不播放：确认 addon 已挂载且路径一致
