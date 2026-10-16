using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Mosen.GroupScheduler.Persistence;

/// <summary>
/// Safe schema migration and verified backup foundation for NON-SECRET local metadata.
/// TDLib session databases and cryptographic keys are stored separately and never logged.
/// </summary>
public sealed class AppDatabase
{
    private const int LatestSchemaVersion = 6;
    private static readonly string[] Migrations = ["0001_initial.sql", "0002_tasks.sql", "0003_accounts.sql", "0004_groups.sql", "0005_script_editor.sql", "0006_published_snapshots.sql"];
    private readonly string _path;

    public AppDatabase(string dbFilePath)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath))
            throw new ArgumentException("Missing database path.", nameof(dbFilePath));
        _path = Path.GetFullPath(dbFilePath);
    }

    public static AppDatabase ForCurrentUser()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Local app data is unavailable.");
        return new AppDatabase(Path.Combine(root, "MOSEN", "TelegramScheduler", "app.db"));
    }

    public string FilePath => _path;
    public string BackupDirectory => Path.Combine(Path.GetDirectoryName(_path)!, "backups");

    public void Initialize() => InitializeCore(LatestSchemaVersion, null);

    // Test-only hooks: permit validation of v1 -> v2 upgrades and an exception just before COMMIT.
    internal void InitializeForTests(int targetVersion, Action<int>? beforeCommit = null) =>
        InitializeCore(targetVersion, beforeCommit);

    private void InitializeCore(int targetVersion, Action<int>? beforeCommit)
    {
        if (targetVersion is < 1 or > LatestSchemaVersion) throw new ArgumentOutOfRangeException(nameof(targetVersion));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        using var connection = OpenConnection();
        int preflightVersion = ReadVersion(connection, null);
        if (preflightVersion > LatestSchemaVersion)
            throw new InvalidDataException("The database was created by a newer application version.");

        // Verified snapshot BEFORE any upgrade. An interrupted migration is transactional;
        // the backup is for explicit recovery, never automatic uncontrolled downgrading.
        if (preflightVersion > 0 && preflightVersion < targetVersion)
            _ = CreateBackupFromConnection(connection);

        using var transaction = connection.BeginTransaction();
        int currentVersion = ReadVersion(connection, transaction);
        ValidateAppliedHistory(connection, transaction, currentVersion);

        for (int version = currentVersion + 1; version <= targetVersion; version++)
        {
            string sql = ReadMigration(version);
            Execute(connection, transaction, sql);
            if (version == 1 && currentVersion == 0)
            {
                // Table is created by the first migration, so no separate bootstrap DDL.
            }
            string hash = ComputeHash(sql);
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO schema_migrations(version, sha256, applied_utc) VALUES ($version, $sha, $date);";
                insert.Parameters.AddWithValue("$version", version);
                insert.Parameters.AddWithValue("$sha", hash);
                insert.Parameters.AddWithValue("$date", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                insert.ExecuteNonQuery();
            }
            Execute(connection, transaction, $"PRAGMA user_version = {version};");
            beforeCommit?.Invoke(version);
        }
        transaction.Commit();
        VerifyIntegrity(connection);
    }

    public int GetSchemaVersion()
    {
        if (!File.Exists(_path)) return 0;
        using var connection = OpenConnection();
        return ReadVersion(connection, null);
    }

    /// <summary>Create an online SQLite-consistent, integrity-checked snapshot in the local backups folder.</summary>
    public string CreateBackup()
    {
        if (!File.Exists(_path)) throw new FileNotFoundException("No database to back up.");
        using var connection = OpenConnection();
        VerifyIntegrity(connection);
        return CreateBackupFromConnection(connection);
    }

    /// <summary>
    /// Prepare restore as a NEW database file; never overwrite a live app.db or its WAL.
    /// A separate maintenance operation after all connections close may promote the file.
    /// </summary>
    public string PrepareRestore(string backupFile)
    {
        if (string.IsNullOrWhiteSpace(backupFile)) throw new ArgumentException("Missing backup.", nameof(backupFile));
        string sourcePath = Path.GetFullPath(backupFile);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Backup does not exist.", sourcePath);
        using var source = NewConnection(sourcePath, SqliteOpenMode.ReadOnly);
        source.Open();
        VerifyIntegrity(source);
        int version = ReadVersion(source, null);
        if (version is < 1 or > LatestSchemaVersion) throw new InvalidDataException("Unsupported backup schema.");

        // The restored candidate is a separate file; caller must explicitly complete
        // promotion only after the app has stopped all database connections.
        string destination = Path.Combine(Path.GetDirectoryName(_path)!, $"restore-candidate-{Guid.NewGuid():N}.db");
        try
        {
            using (var dest = NewConnection(destination, SqliteOpenMode.ReadWriteCreate))
            {
                dest.Open();
                source.BackupDatabase(dest);
            }
            using (var verified = NewConnection(destination, SqliteOpenMode.ReadOnly))
            {
                verified.Open();
                VerifyIntegrity(verified);
                if (ReadVersion(verified, null) != version)
                    throw new InvalidDataException("Restored candidate does not match source schema.");
            }
            return destination;
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    private string CreateBackupFromConnection(SqliteConnection source)
    {
        Directory.CreateDirectory(BackupDirectory);
        string name = $"backup-v{ReadVersion(source, null)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db";
        string tmp = Path.Combine(BackupDirectory, ".creating-" + Guid.NewGuid().ToString("N") + ".db");
        string final = Path.Combine(BackupDirectory, name);
        try
        {
            using (var target = NewConnection(tmp, SqliteOpenMode.ReadWriteCreate))
            {
                target.Open();
                source.BackupDatabase(target);
            }
            using (var check = NewConnection(tmp, SqliteOpenMode.ReadOnly))
            {
                check.Open();
                VerifyIntegrity(check);
            }
            File.Move(tmp, final);
            return final;
        }
        catch
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            throw;
        }
    }

    private SqliteConnection OpenConnection()
    {
        var db = NewConnection(_path, SqliteOpenMode.ReadWriteCreate);
        db.Open();
        try
        {
            using var command = db.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
            return db;
        }
        catch { db.Dispose(); throw; }
    }

    private static SqliteConnection NewConnection(string path, SqliteOpenMode mode) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            ForeignKeys = true,
            DefaultTimeout = 10
        }.ToString());

    private static string ReadMigration(int version)
    {
        string resource = $"{typeof(AppDatabase).Namespace}.Migrations.{Migrations[version - 1]}";
        using Stream? input = typeof(AppDatabase).Assembly.GetManifestResourceStream(resource);
        if (input is null) throw new InvalidDataException($"Migration resource missing: v{version}");
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ComputeHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static int ReadVersion(SqliteConnection connection, SqliteTransaction? tx)
    {
        using var c = connection.CreateCommand();
        c.Transaction = tx;
        c.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(c.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql)
    {
        using var c = connection.CreateCommand();
        c.Transaction = tx;
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private static void ValidateAppliedHistory(SqliteConnection connection, SqliteTransaction tx, int version)
    {
        if (version > LatestSchemaVersion)
            throw new InvalidDataException("A newer database schema cannot be downgraded.");
        if (version == 0) return;

        using var c = connection.CreateCommand();
        c.Transaction = tx;
        c.CommandText = "SELECT version, sha256 FROM schema_migrations ORDER BY version;";
        using var rows = c.ExecuteReader();
        int seen = 0;
        while (rows.Read())
        {
            int number = rows.GetInt32(0);
            string storedHash = rows.GetString(1);
            if (number != ++seen || number > version || !string.Equals(storedHash, ComputeHash(ReadMigration(number)), StringComparison.Ordinal))
                throw new InvalidDataException("Schema migration journal is inconsistent.");
        }
        if (seen != version) throw new InvalidDataException("Schema migration history is incomplete.");
    }

    private static void VerifyIntegrity(SqliteConnection connection)
    {
        using var c = connection.CreateCommand();
        c.CommandText = "PRAGMA integrity_check;";
        string? answer = Convert.ToString(c.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.Equals(answer, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SQLite integrity check failed.");
    }
}
