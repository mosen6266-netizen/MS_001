using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Core.Data;
using Mosen.GroupScheduler.Persistence;

var root = Path.Combine(Path.GetTempPath(), "ms001-step04-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;

void Check(bool ok, string title)
{
    if (!ok) throw new Exception("FAIL: " + title);
    passed++;
    Console.WriteLine("PASS " + title);
}

static SqliteConnection Open(string file)
{
    var c = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = file, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true, Pooling = false
    }.ToString());
    c.Open();
    return c;
}

static long Count(SqliteConnection c, string table)
{
    // Table names only come from local compile-time literals.
    using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM " + table;
    return (long)cmd.ExecuteScalar()!;
}

static void Execute(SqliteConnection c, string sql)
{
    using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
}

try
{
    string dbPath = Path.Combine(root, "app.db");
    var db = new AppDatabase(dbPath);
    Check(db.GetSchemaVersion() == 0, "absent DB reports schema v0");
    db.InitializeForTests(1);
    Check(db.GetSchemaVersion() == 1, "initial schema v1 created");
    using (var c = Open(dbPath))
    {
        Check(Count(c, "schema_migrations") == 1, "migration ledger v1");
        // User-configured account metadata, not actual Telegram auth sessions.
        Execute(c, """
            INSERT INTO accounts(account_id, alias, display_name, created_utc, updated_utc)
            VALUES ('sample-account', '客服A', 'Demo', '2026-10-16', '2026-10-16');
            INSERT INTO scripts(script_id, title, created_utc, updated_utc)
            VALUES ('sample-script', '团队通知', '2026-10-16', '2026-10-16');
            INSERT INTO script_versions(script_id, version, created_utc)
            VALUES ('sample-script', 1, '2026-10-16');
            INSERT INTO script_roles(script_id, script_version, role_key, role_label)
            VALUES ('sample-script', 1, 'role-a', '客服A');
            INSERT INTO script_steps(script_id, script_version, step_key, ordinal, role_key,
                                     body, pre_send_delay_ms, typing_duration_ms)
            VALUES ('sample-script', 1, 'step-a', 0, 'role-a', '公告内容', 30000, 5000);
            """);
        Check(Count(c, "accounts") == 1, "metadata insert, no session data required");
        Check(Count(c, "script_steps") == 1, "timing row inserted");
        bool rejected = false;
        try { Execute(c, """
            INSERT INTO script_steps(script_id, script_version, step_key, ordinal, role_key,
                                     body, pre_send_delay_ms, typing_duration_ms)
            VALUES ('sample-script', 1, 'bad-step', 1, 'role-a', 'Invalid', -1, 5000);
            """); } catch (SqliteException) { rejected = true; }
        Check(rejected, "SQLite rejects negative pre-send waiting");
    }

    string snapshotV1 = db.CreateBackup();
    Check(File.Exists(snapshotV1), "online WAL-consistent backup created");

    // A simulated crash just before migration v2 COMMIT must not leave partially applied tables.
    bool failed = false;
    try { db.InitializeForTests(2, version => { if (version == 2) throw new IOException("Injected failure before COMMIT"); }); }
    catch (IOException) { failed = true; }
    Check(failed, "fault injection executed at migration commit boundary");
    Check(db.GetSchemaVersion() == 1, "transaction rollback preserves previous version");
    using (var c = Open(dbPath))
    {
        Check(Count(c, "schema_migrations") == 1, "rollback does not append incomplete migration journal");
        using var sql = c.CreateCommand(); sql.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='outbox';";
        Check(Convert.ToInt64(sql.ExecuteScalar()) == 0, "partially created v2 tables are rolled back");
    }

    db.Initialize(); // normal restart succeeds
    Check(db.GetSchemaVersion() == 9, "restart upgrades to v9");
    db.Initialize();
    using (var c = Open(dbPath))
    {
        Check(Count(c, "schema_migrations") == 9, "migrations idempotent under repeated startup");
        Check(Count(c, "accounts") == 1, "account metadata preserved through upgrade");
        Check(Count(c, "script_steps") == 1, "script content preserved through upgrade");
        Check(Count(c, "outbox") == 0, "outbox prepared but no send automatically started");
        using var sql = c.CreateCommand();
        sql.CommandText = "SELECT pre_send_delay_ms + typing_duration_ms FROM script_steps WHERE step_key='step-a';";
        Check(Convert.ToInt64(sql.ExecuteScalar()) == 35000, "30s waiting then 5s typing equals 35s");
        sql.CommandText = "PRAGMA integrity_check;";
        Check((string)sql.ExecuteScalar()! == "ok", "post-upgrade SQLite integrity");
    }

    string candidate = db.PrepareRestore(snapshotV1);
    Check(File.Exists(candidate) && candidate != dbPath, "rollback creates separate verified restore candidate");
    Check(new AppDatabase(candidate).GetSchemaVersion() == 1, "restore candidate is original v1");
    Check(db.GetSchemaVersion() == 9, "live upgraded DB remains unchanged during restore preparation");
    var restored = new AppDatabase(candidate);
    restored.Initialize();
    using (var c = Open(candidate)) Check(Count(c, "accounts") == 1, "restored and re-upgraded metadata retained");

    // Strict timing is serial, not overlapping.
    var timing = new ScriptStepTiming(30000, 5000, 0);
    Check(timing.EarliestDispatchAfterMs == 35000, "data contract encodes exact sequential wait+typing");
    bool timeRejected = false;
    try { new ScriptStepTiming(-1, 5000, 0).Validate(); } catch (ArgumentOutOfRangeException) { timeRejected = true; }
    Check(timeRejected, "negative delays rejected by core contract");

    var role = new ScriptRole(Guid.NewGuid(), "客服A");
    var valid = new ScriptExportManifest(1, Guid.NewGuid(), "团队通知",
        [role], [new ScriptExportStep(Guid.NewGuid(), role.RoleKey, "合法通知", timing)]);
    ContractValidation.Validate(valid);
    Check(true, "versioned script manifest schema validated");
    bool missingRole = false;
    try { ContractValidation.Validate(valid with {
        Steps = [new ScriptExportStep(Guid.NewGuid(), Guid.NewGuid(), "bad", timing)]
    }); } catch (InvalidDataException) { missingRole = true; }
    Check(missingRole, "export manifest refuses unknown logical role");
    string json = JsonSerializer.Serialize(valid);
    Check(!json.Contains("accountId") && !json.Contains("phoneNumber") &&
          !json.Contains("session", StringComparison.OrdinalIgnoreCase),
          "script export contract has no login/session identifiers");

    Console.WriteLine($"SUMMARY: {passed} checks passed, zero Telegram account/network interactions.");
}
finally
{
    // Test data contains only synthetic content.
    try { Directory.Delete(root, recursive: true); }
    catch (IOException) { /* transient Windows antivirus/locked file: CI temp cleanup only */ }
}
