namespace Mosen.GroupScheduler.Installation;

/// <summary>Publisher-fixed, per-user data layout. Installation location is NEVER
/// a data root. Deliberately no delete/reformat method in installer policy.</summary>
public static class UserDataPaths
{
    public const string Company="MOSEN";
    public const string Product="TelegramScheduler";
    public static string ForCurrentUser()
    {
        var appData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Resolve(appData);
    }
    public static string Resolve(string localAppData)
    {
        if(string.IsNullOrWhiteSpace(localAppData) ||
           !Path.IsPathFullyQualified(localAppData))
            throw new ArgumentException("A fully qualified per-user LocalAppData path is required.");
        var canonical=Path.GetFullPath(localAppData);
        return Path.Combine(canonical,Company,Product);
    }
    public static string WebView2(string root) => Subfolder(root,"webview2");
    public static string DiagnosticLogs(string root)=>Subfolder(root,"diagnostics");
    public static string AppDatabase(string root)=>Path.Combine(Subfolder(root),"app.db");
    public static string Backups(string root)=>Subfolder(root,"backups");
    public static string Subfolder(string root,string? child=null)
    {
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))
            throw new ArgumentException("Invalid user data root.");
        var canonical=Path.GetFullPath(root);
        return child is null ? canonical : Path.Combine(canonical,child);
    }
    public static void AssertNoInstallDirectoryOverlap(string installDirectory,string dataRoot)
    {
        if(!Path.IsPathFullyQualified(installDirectory)||!Path.IsPathFullyQualified(dataRoot))
            throw new ArgumentException("Both locations must be absolute.");
        static string Clean(string p)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
        var install=Clean(installDirectory);
        var data=Clean(dataRoot);
        if(data.Equals(install,StringComparison.OrdinalIgnoreCase) ||
           data.StartsWith(install+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
           install.StartsWith(data+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Install directory and application user data must be isolated.");
    }
}
