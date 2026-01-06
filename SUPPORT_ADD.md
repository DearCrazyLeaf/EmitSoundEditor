## 2.1) Mute + duplicate fire events (required)

AG2 does **not** apply subclass fire‑sound overrides in `weapons.vdata`. To make custom fire sounds work reliably, you must separate soundevents into **three** files and compile/package them together.

### A) `mute_sounds.vsndevts` (silence original fire events)
Goal: prevent double sound from the engine’s original fire events (which are still referenced by `weapons.vdata`).

- Put **official parent fire events** here (e.g., `Weapon_AK47.Single`, `Weapon_AK47.SingleDistant`, and silenced variants).
- Set `volume = 0` (or other absolute mute) for those events.
- Keep names identical to the official events used by `weapons.vdata`.

Example:
```kv3
"Weapon_AK47.Single" =
{
  type = "csgo_mega"
  volume = 0.000000
  pitch = 1.000000
  vsnd_files = "sounds/weapons/ak47/ak47_01.vsnd"
}
```

### B) `dup_sounds.vsndevts` (normal‑volume duplicates)
Goal: provide a **normal** fire sound for non‑custom weapons after you muted the originals.

- Copy the same events from A), but **rename** them with a prefix (example: `dup.`).
- Keep normal volume (1.0).
- These **duplicates are never called by the engine**, only by this plugin.

Example:
```kv3
"dup.Weapon_AK47.Single" =
{
  type = "csgo_mega"
  volume = 1.000000
  pitch = 1.000000
  vsnd_files = "sounds/weapons/ak47/ak47_01.vsnd"
}
```

### C) `custom_sounds.vsndevts` (your custom fire events)
Goal: your custom fire events for store-equipped subclasses.

- Use any naming scheme you want (e.g., `weapon.example.fire`).
- Point to your compiled `.vsnd` paths.

### D) Plugin mapping
- **Custom weapons** (store subclasses): map `overrides[].subclass` → `target_event` (and `target_event_unsilenced` when needed).
- **Normal weapons**: map `official_overrides[].item_def_index` → `dup.*` events.
  - This is the key: if you muted originals, normal weapons must use duplicates.

### E) Compile/package rules
- All three `.vsndevts` files must be compiled **together** with all required `.vsnd`.
- If you change any event names, **update plugin config** to match.
- If you do not use `soundevents_addon.vsndevts`, you must **precache** your custom `.vsndevts` file (e.g., via Resource Precacher), or events may not play.

### F) Quick validation
- Custom weapons: fire → hear `overrides[].target_event`
- Non‑custom weapons: fire → hear `official_overrides[].target_event` (dup)
- If everything is silent → duplicates missing or config mismatch

---

## 2.1) 静音与副本音效事件（必需）

AG2 下 **subclass 的开火音效不会被 `weapons.vdata` 正常应用**。要稳定替换开火音效，必须拆成**三份** soundevents 文件，并一起编译打包。

### A) `mute_sounds.vsndevts`（静音原始开火事件）
目的：阻止引擎原本的开火事件播放（`weapons.vdata` 仍然引用它们）。

- 写入**父级官方开火事件**（例如 `Weapon_AK47.Single`、`Weapon_AK47.SingleDistant` 及消音变体）。
- 事件名必须与官方一致。
- 将 `volume = 0`（或完全静音的写法）。

示例：
```kv3
"Weapon_AK47.Single" =
{
  type = "csgo_mega"
  volume = 0.000000
  pitch = 1.000000
  vsnd_files = "sounds/weapons/ak47/ak47_01.vsnd"
}
```

### B) `dup_sounds.vsndevts`（副本事件，正常音量）
目的：在你静音官方事件后，**让非自定义武器也能正常有声音**。

- 从 A) 复制事件内容，但**改名**（建议统一前缀，如 `dup.`）。
- 音量保持正常（1.0）。
- 这些副本事件**不会被引擎自动调用**，只能由插件播放。

示例：
```kv3
"dup.Weapon_AK47.Single" =
{
  type = "csgo_mega"
  volume = 1.000000
  pitch = 1.000000
  vsnd_files = "sounds/weapons/ak47/ak47_01.vsnd"
}
```

### C) `custom_sounds.vsndevts`（自定义音效）
目的：放置你要给自定义武器播放的开火事件。

- 名称可自定义（如 `weapon.example.fire`）。
- 指向编译后的 `.vsnd` 路径。

### D) 插件映射规则
- **自定义武器**（商店 subclass）：用 `overrides[].subclass` → `target_event`（必要时 `target_event_unsilenced`）。
- **普通武器**：用 `official_overrides[].item_def_index` → `dup.*` 事件。
  - 这是关键：官方事件已被静音，普通武器必须走副本事件。

### E) 编译与打包要点
- 三份 `.vsndevts` 必须**一起**编译，并确保对应 `.vsnd` 完整。
- 任何事件名变更，都必须同步到插件配置。
- 若不用 `soundevents_addon.vsndevts`，必须**预载自定义 `.vsndevts`**（如 Resource Precacher），否则事件可能不响。

### F) 快速检查
- 自定义武器开火 → 听到 `overrides[].target_event`
- 普通武器开火 → 听到 `official_overrides[].target_event`（dup）
- 全部无声 → 副本缺失或配置不匹配
