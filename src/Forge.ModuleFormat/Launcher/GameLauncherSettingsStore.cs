using System.Text;
using System.Text.Json;

namespace Forge.ModuleFormat.Launcher;

public static class GameLauncherSettingsStore
{
    public static string GetDefaultPath(GameLauncherManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return Path.Combine(local, "OpenRPG Forge", "Games", manifest.Slug, "launcher-settings.json");
    }

    public static GameLauncherUserSettings LoadOrDefault(string path, GameLauncherManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(manifest);
        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath) ? Load(fullPath) : GameLauncherUserSettings.CreateDefault(manifest);
    }

    public static GameLauncherUserSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Launcher settings are missing.", fullPath);
        var settings = JsonSerializer.Deserialize<GameLauncherUserSettings>(
                           File.ReadAllText(fullPath),
                           GameLauncherManifestStore.CreateOptions())
                       ?? throw new InvalidDataException("Launcher settings are empty.");
        RequireValid(settings);
        return settings;
    }

    public static void Save(string path, GameLauncherUserSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        RequireValid(settings);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Launcher settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(settings, GameLauncherManifestStore.CreateOptions()) + Environment.NewLine;
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RequireValid(GameLauncherUserSettings settings)
    {
        var diagnostics = GameLauncherUserSettingsValidator.Validate(settings);
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException("Launcher settings are invalid: " + string.Join("; ", diagnostics));
        }
    }
}
