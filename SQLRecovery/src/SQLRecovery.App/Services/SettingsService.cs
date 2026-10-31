using System.IO;
using System.Text.Json;

namespace SQLRecovery.App.Services;

public sealed class AppSettings
{
    public List<string> RecentFiles { get; set; } = new();
    public string? LastExportFolder { get; set; }
    public string? LastDatabaseFolder { get; set; }
    public bool IsDarkTheme { get; set; } = true;
}

/// <summary>Persists small user preferences in %APPDATA%\SQLRecoveryTool\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SQLRecoveryTool");

        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");

        Load();
    }

    public IReadOnlyList<string> RecentFiles => Settings.RecentFiles;

    public string? LastExportFolder
    {
        get => Settings.LastExportFolder;
        set
        {
            Settings.LastExportFolder = value;
            Save();
        }
    }

    public string? LastDatabaseFolder
    {
        get => Settings.LastDatabaseFolder;
        set
        {
            Settings.LastDatabaseFolder = value;
            Save();
        }
    }

    public bool IsDarkTheme
    {
        get => Settings.IsDarkTheme;
        set
        {
            Settings.IsDarkTheme = value;
            Save();
        }
    }

    public void AddRecentFile(string path)
    {
        Settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Settings.RecentFiles.Insert(0, path);

        if (Settings.RecentFiles.Count > 10)
            Settings.RecentFiles.RemoveRange(10, Settings.RecentFiles.Count - 10);

        Save();
    }

    public void RemoveRecentFile(string path)
    {
        if (Settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
            Save();
    }

    public void ClearRecentFiles()
    {
        Settings.RecentFiles.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch
        {
            // Settings persistence is best-effort only.
        }
    }
}
