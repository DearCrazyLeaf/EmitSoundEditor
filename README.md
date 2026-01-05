# EmitSoundEditor

Overrides weapon fire sounds using Store API equipment state and per-weapon configuration.

## Quick start

1) Build the plugin and copy the output under `counterstrikesharp/plugins/EmitSoundEditor/`.
2) Create `EmitSoundEditor.json` next to the plugin DLL.
3) Add overrides for custom subclasses and (optionally) official weapon defindexes.
4) Restart the server.

## Config

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
    },
    {
      "item_def_index": 60,
      "target_event": "hlym.Weapon_M4A1.Silenced",
      "target_event_unsilenced": "hlym.Weapon_M4A4.Single"
    }
  ],
  "force_mute_all_firebullets": false
}
```

- `subclass`: the subclass from store config (right side of `weapon_base:subclass`).
- `target_event`: soundevent to play when the weapon fires.
- `target_event_unsilenced`: optional; used when a silencer-capable weapon fires unsilenced.
- `official_overrides`: optional fallback by weapon `item_def_index`.
- `force_mute_all_firebullets`: optional global mute for native firebullet sounds.

## Notes

- Requires Store API capability to be loaded.
- The plugin caches equipment per player and uses constant-time lookups on fire.
- See `WIKI.md` for full authoring, packaging, and deployment steps.
