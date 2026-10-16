using Mosen.GroupScheduler.Persistence;

var root = Path.Combine(Path.GetTempPath(), "ms001-step07-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
void Assert(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL: " + name);
    passed++;
    Console.WriteLine("PASS " + name);
}

try
{
    var file = Path.Combine(root, "settings.db");
    var repository = new PreferencesRepository(new AppDatabase(file));
    var empty = repository.Read();
    Assert(empty.Revision == 0, "new settings revision zero");
    Assert(empty.Preferences == UserPreferences.Default, "new settings use safe defaults");

    var userChoice = new UserPreferences("compact", 150, 50, false);
    var saved = repository.Save(0, userChoice);
    Assert(saved.Revision == 1, "save returns revision 1");
    Assert(saved.Preferences == userChoice, "save returns confirmed values");
    Assert(new PreferencesRepository(new AppDatabase(file)).Read() == saved, "settings survive app restart");

    var newChoice = userChoice with { SearchDebounceMs = 250 };
    var next = repository.Save(1, newChoice);
    Assert(next.Revision == 2 && next.Preferences.SearchDebounceMs == 250, "saving revision 2 updates one value");

    bool conflict = false;
    try { repository.Save(1, userChoice); } catch (PreferencesConflictException) { conflict = true; }
    Assert(conflict, "stale revision refuses duplicate write");
    Assert(repository.Read() == next, "conflict does not modify current saved settings");

    bool bad = false;
    try { repository.Save(2, newChoice with { SearchDebounceMs = 5 }); } catch (ArgumentException) { bad = true; }
    Assert(bad, "unsupported debounce is rejected");
    bad = false;
    try { repository.Save(2, newChoice with { PageSize = 300 }); } catch (ArgumentException) { bad = true; }
    Assert(bad, "unsafe page size is rejected");
    bad = false;
    try { repository.Save(2, newChoice with { Density = "custom-css" }); } catch (ArgumentException) { bad = true; }
    Assert(bad, "arbitrary density strings rejected");
    bad = false;
    try { repository.Save(-1, newChoice); } catch (ArgumentOutOfRangeException) { bad = true; }
    Assert(bad, "negative expected revision rejected");
    Assert(repository.Read() == next, "invalid saves never damage persisted values");

    var backup = new AppDatabase(file).CreateBackup();
    Assert(File.Exists(backup), "settings database supports existing snapshot/backup");
    var restored = new AppDatabase(Path.Combine(root, "recovered.db"));
    var candidate = restored.PrepareRestore(backup);
    Assert(new PreferencesRepository(new AppDatabase(candidate)).Read() == next, "settings preserved in consistent backup");
    Assert(new AppDatabase(file).GetSchemaVersion() == 9, "settings DB stays at schema v9");
    Console.WriteLine($"SUMMARY: {passed} settings tests passed, no Telegram network activity.");
}
finally { try { Directory.Delete(root, true); } catch (IOException) { } }
