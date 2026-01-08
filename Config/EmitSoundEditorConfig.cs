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

    [JsonPropertyName("custom_sound_default_enabled")]
    public bool CustomSoundDefaultEnabled { get; set; } = true;

    [JsonPropertyName("mysql")]
    public MySqlSettings MySql { get; set; } = new();
}

public class MySqlSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 3306;

    [JsonPropertyName("database")]
    public string Database { get; set; } = "cs2";

    [JsonPropertyName("user")]
    public string User { get; set; } = "root";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("table")]
    public string Table { get; set; } = "emsound_settings";
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
