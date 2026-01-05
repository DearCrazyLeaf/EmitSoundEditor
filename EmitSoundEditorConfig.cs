using System.Collections.Generic;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace EmitSoundEditor;

public class EmitSoundEditorConfig : BasePluginConfig
{
    [JsonPropertyName("overrides")]
    public List<WeaponSoundOverride> Overrides { get; set; } = new();

    [JsonPropertyName("official_overrides")]
    public List<WeaponItemDefOverride> OfficialOverrides { get; set; } = new();

    [JsonPropertyName("force_mute_all_firebullets")]
    public bool ForceMuteAllFireBullets { get; set; } = false;
}

public class WeaponSoundOverride
{
    [JsonPropertyName("subclass")]
    public string Subclass { get; set; } = string.Empty;

    [JsonPropertyName("target_event")]
    public string TargetEvent { get; set; } = string.Empty;

    [JsonPropertyName("target_event_unsilenced")]
    public string TargetEventUnsilenced { get; set; } = string.Empty;
}

public class WeaponItemDefOverride
{
    [JsonPropertyName("item_def_index")]
    public int ItemDefIndex { get; set; }

    [JsonPropertyName("target_event")]
    public string TargetEvent { get; set; } = string.Empty;

    [JsonPropertyName("target_event_unsilenced")]
    public string TargetEventUnsilenced { get; set; } = string.Empty;
}
