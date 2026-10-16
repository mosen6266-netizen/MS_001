using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Mosen.GroupScheduler.Persistence;

public sealed record UserPreferences(
    string Density,
    int SearchDebounceMs,
    int PageSize,
    bool DesktopNotifications)
{
    public static UserPreferences Default => new("comfortable", 200, 20, true);

    public void Validate()
    {
        if (Density is not ("comfortable" or "compact") ||
            SearchDebounceMs is < 150 or > 250 ||
            PageSize is not (10 or 20 or 50))
            throw new ArgumentException("Invalid non-secret UI preferences.");
    }
}

public sealed record PreferencesSnapshot(long Revision, UserPreferences Preferences);
public sealed class PreferencesConflictException : Exception
{
    public PreferencesConflictException() : base("Settings were modified elsewhere; reload before saving.") {}
}

/// <summary>
/// Durable revisioned settings in the existing SQLite metadata database.
/// No Telegram account, API secret, phone, chat contents or sessions are persisted here.
/// </summary>
public sealed class PreferencesRepository
{
    private const string Key = "app.preferences.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDatabase _database;

    public PreferencesRepository(AppDatabase database) => _database = database;

    public PreferencesSnapshot Read()
    {
        _database.Initialize();
        using var connection = Open();
        return ReadStored(connection, null).Snapshot;
    }

    public PreferencesSnapshot Save(long expectedRevision, UserPreferences preferences)
    {
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Validate();
        _database.Initialize();
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        var (current, raw) = ReadStored(connection, tx);
        if (current.Revision != expectedRevision) throw new PreferencesConflictException();
        var updated = new PreferencesSnapshot(checked(current.Revision + 1), preferences);
        string json = JsonSerializer.Serialize(updated, JsonOptions);
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        if (raw is null)
        {
            command.CommandText = """
                INSERT INTO settings(setting_key,value_json,updated_utc)
                VALUES($key,$value,$timestamp);
                """;
        }
        else
        {
            command.CommandText = """
                UPDATE settings SET value_json=$value,updated_utc=$timestamp
                WHERE setting_key=$key AND value_json=$old;
                """;
            command.Parameters.AddWithValue("$old", raw);
        }
        command.Parameters.AddWithValue("$key", Key);
        command.Parameters.AddWithValue("$value", json);
        command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        if (command.ExecuteNonQuery() != 1) throw new PreferencesConflictException();
        tx.Commit();
        return updated;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = _database.FilePath, Mode = SqliteOpenMode.ReadWrite, Cache = SqliteCacheMode.Private,
            Pooling = false, ForeignKeys = true, DefaultTimeout = 10
        }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static (PreferencesSnapshot Snapshot, string? Raw) ReadStored(SqliteConnection connection, SqliteTransaction? tx)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT value_json FROM settings WHERE setting_key=$key;";
        command.Parameters.AddWithValue("$key", Key);
        string? raw = command.ExecuteScalar() as string;
        if (raw is null) return (new PreferencesSnapshot(0, UserPreferences.Default), null);
        PreferencesSnapshot? snapshot;
        try { snapshot = JsonSerializer.Deserialize<PreferencesSnapshot>(raw, JsonOptions); }
        catch (JsonException) { throw new InvalidDataException("Stored preference JSON is corrupt."); }
        if (snapshot is null || snapshot.Revision < 1 || snapshot.Preferences is null)
            throw new InvalidDataException("Stored preferences have invalid structure.");
        try { snapshot.Preferences.Validate(); }
        catch (ArgumentException) { throw new InvalidDataException("Stored preferences have unsupported values."); }
        return (snapshot, raw);
    }
}
