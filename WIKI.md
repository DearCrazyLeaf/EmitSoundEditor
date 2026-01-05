# EmitSoundEditor Wiki

This document describes the end-to-end flow: author soundevents, compile assets, package, and configure the plugin.

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

Create `EmitSoundEditor.json` next to the plugin DLL:

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
      "target_event": "hlym.Weapon_AK47.Single"
    }
  ],
  "force_mute_all_firebullets": false
}
```

## 7) Validation checklist

- `EmitSoundEditor.json` is loaded and parsed without errors.
- When firing a mapped weapon, the specified soundevent is played.
- For silenced weapons (M4A1-S/USP-S), both silenced and unsilenced events map correctly.

## Troubleshooting

- If no sound plays, confirm the event name exists in your `.vsndevts` file.
- If the compiler reports KV3 errors, ensure the header is correct and the file has a single root object.
- If events play in console but not in-game, confirm the addon is mounted and the paths match.
