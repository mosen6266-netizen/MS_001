using System;
using System.IO;
using System.Text.Json;
using Mosen.GroupScheduler.Core.Shell;

namespace Mosen.GroupScheduler.Desktop;

/// <summary>Settings metadata only; stored outside the installation directory, never sessions or credentials.</summary>
internal sealed class WindowStateStore
{
    private static string FilePath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Local app data not available.");
            return Path.Combine(root, "MOSEN", "TelegramScheduler", "window-state.json");
        }
    }
    internal WindowPlacement? Load()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024) return null;
            return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null; // malformed or inaccessible settings must never stop startup
        }
    }
    internal void Save(WindowPlacement position)
    {
        string path = FilePath;
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(position));
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
