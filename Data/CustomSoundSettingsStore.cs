using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace EmitSoundEditor.Data;

internal sealed class CustomSoundSettingsStore
{
    private readonly MySqlSettings _settings;
    private readonly ILogger _logger;
    private readonly string? _connectionString;
    private Task? _initTask;
    private bool _ready;

    public bool Enabled => _settings.Enabled;

    public CustomSoundSettingsStore(MySqlSettings settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
        _connectionString = settings.Enabled ? BuildConnectionString(settings) : null;
    }

    public Task InitializeAsync()
    {
        if (!Enabled)
        {
            return Task.CompletedTask;
        }

        if (_initTask != null)
        {
            return _initTask;
        }

        _initTask = InitializeInternalAsync();
        return _initTask;
    }

    public async Task<bool?> LoadEnabledAsync(ulong steamId)
    {
        if (steamId == 0 || !await EnsureReadyAsync())
        {
            return null;
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT enabled FROM `{_settings.Table}` WHERE steam_id=@steamId";
            command.Parameters.AddWithValue("@steamId", steamId);

            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(result) != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmitSoundEditor] Failed to load sound settings.");
            return null;
        }
    }

    public async Task SaveEnabledAsync(ulong steamId, bool enabled)
    {
        if (steamId == 0 || !await EnsureReadyAsync())
        {
            return;
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"INSERT INTO `{_settings.Table}` (steam_id, enabled) VALUES (@steamId, @enabled) ON DUPLICATE KEY UPDATE enabled = VALUES(enabled);";
            command.Parameters.AddWithValue("@steamId", steamId);
            command.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmitSoundEditor] Failed to save sound settings.");
        }
    }

    private async Task<bool> EnsureReadyAsync()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        if (_initTask != null)
        {
            await _initTask;
        }

        return _ready;
    }

    private async Task InitializeInternalAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $"CREATE TABLE IF NOT EXISTS `{_settings.Table}` (steam_id BIGINT UNSIGNED NOT NULL PRIMARY KEY, enabled TINYINT(1) NOT NULL DEFAULT 1);";
            await command.ExecuteNonQueryAsync();

            _ready = true;
        }
        catch (Exception ex)
        {
            _ready = false;
            _logger.LogError(ex, "[EmitSoundEditor] Failed to initialize MySQL.");
        }
    }

    private static string BuildConnectionString(MySqlSettings settings)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Host,
            Port = (uint)settings.Port,
            Database = settings.Database,
            UserID = settings.User,
            Password = settings.Password,
            DefaultCommandTimeout = 5,
            ConnectionTimeout = 5
        };

        return builder.ConnectionString;
    }
}
