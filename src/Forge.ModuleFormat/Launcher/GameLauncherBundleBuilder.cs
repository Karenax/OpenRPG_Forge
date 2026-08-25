using System.IO.Compression;
using System.Text;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Packages;

namespace Forge.ModuleFormat.Launcher;

public sealed record GameLauncherBundleResult(
    string OutputDirectory,
    GameLauncherManifest Manifest,
    int FileCount,
    long TotalBytes);

public static class GameLauncherBundleBuilder
{
    public const string TemplateExecutableName = "OpenRPG_Forge_GameLauncher.exe";
    public const string RuntimeExecutableRelativePath = "runtime/OpenRPG_Forge_Runtime.exe";
    public const string RuntimePackageRelativePath = "runtime/OpenRPG_Forge_Runtime.pck";

    public static GameLauncherBundleResult Build(
        string modulePackagePath,
        string templateDirectory,
        string destinationDirectory,
        GameLauncherConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.Enabled)
            throw new InvalidOperationException("The game launcher profile is disabled.");

        var package = Path.GetFullPath(modulePackagePath);
        var template = Path.GetFullPath(templateDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        if (!File.Exists(package)) throw new FileNotFoundException("The module package is missing.", package);
        if (!Directory.Exists(template)) throw new DirectoryNotFoundException($"Launcher template is missing: {template}");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Game distribution destination already exists: {destination}");

        var packageValidation = ModulePackageValidator.Validate(package);
        if (!packageValidation.IsValid || packageValidation.Manifest is null || packageValidation.Project is null)
            throw new InvalidDataException("The module package is invalid: " +
                string.Join("; ", packageValidation.Diagnostics.Select(item => item.Message)));
        if (packageValidation.Manifest.Target is not (ModuleTargetKind.Client or ModuleTargetKind.Shipping))
            throw new InvalidDataException("A game launcher requires a Client or Shipping .orpmod package.");

        var templateExecutable = RequireTemplateFile(template, TemplateExecutableName);
        _ = RequireTemplateFile(template, RuntimeExecutableRelativePath);
        _ = RequireTemplateFile(template, RuntimePackageRelativePath);
        var parent = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Game distribution destination has no parent directory.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        if (Directory.Exists(temporary))
            throw new IOException($"Temporary game distribution path already exists: {temporary}");

        try
        {
            Directory.CreateDirectory(temporary);
            CopyTree(Path.Combine(template, "runtime"), Path.Combine(temporary, "runtime"));
            var executableRelativePath = configuration.ExecutableName + ".exe";
            var launcherPath = Path.Combine(temporary, executableRelativePath);
            File.Copy(templateExecutable, launcherPath);
            var gameDirectory = Path.Combine(temporary, "game");
            Directory.CreateDirectory(gameDirectory);
            var moduleRelativePath = $"game/{packageValidation.Manifest.Slug}.orpmod";
            var copiedPackage = Path.Combine(temporary, moduleRelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(package, copiedPackage);

            var backgroundRelativePath = CopyBackgroundImage(package, temporary, configuration.BackgroundImagePath);
            var musicRelativePath = CopyLauncherMusic(package, temporary, configuration.MusicPath);
            var runtimePath = Path.Combine(temporary,
                RuntimeExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var manifest = new GameLauncherManifest(
                GameLauncherManifest.CurrentSchemaVersion,
                packageValidation.Manifest.ModuleId.Value,
                packageValidation.Manifest.Slug,
                configuration.DisplayName,
                configuration.Tagline,
                packageValidation.Manifest.ModuleVersion,
                configuration.AccentColor,
                executableRelativePath,
                RuntimeExecutableRelativePath,
                moduleRelativePath,
                GameLauncherManifestStore.Sha256(launcherPath),
                GameLauncherManifestStore.Sha256(runtimePath),
                GameLauncherManifestStore.Sha256(copiedPackage),
                backgroundRelativePath,
                configuration.NewsUrl,
                configuration.SupportUrl,
                configuration.SettingsEnabled,
                configuration.Multiplayer,
                configuration.DefaultServerPort,
                musicRelativePath);
            GameLauncherManifestStore.Save(temporary, manifest);
            File.WriteAllText(
                Path.Combine(temporary, "LISEZ-MOI.txt"),
                BuildReadme(manifest),
                new UTF8Encoding(false));
            _ = GameLauncherManifestStore.Verify(temporary, manifest);

            var distributionFiles = Directory.EnumerateFiles(temporary, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    GameLauncherManifestStore.DistributionFileName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => new GameDistributionFile(
                    Path.GetRelativePath(temporary, path).Replace('\\', '/'),
                    new FileInfo(path).Length,
                    GameLauncherManifestStore.Sha256(path)))
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .ToArray();
            GameLauncherManifestStore.SaveDistribution(
                temporary,
                new GameDistributionManifest(
                    GameDistributionManifest.CurrentSchemaVersion,
                    manifest.GameId,
                    manifest.DisplayName,
                    manifest.Version,
                    "windows-x64",
                    distributionFiles));
            Directory.Move(temporary, destination);
            var outputFiles = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).ToArray();
            return new GameLauncherBundleResult(
                destination,
                manifest,
                outputFiles.Length,
                outputFiles.Sum(path => new FileInfo(path).Length));
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
        }
    }

    private static string RequireTemplateFile(string template, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(template, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = template.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new FileNotFoundException($"Launcher template file is missing: {relativePath}", path);
        return path;
    }

    private static void CopyTree(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Runtime template directory is missing: {sourceDirectory}");
        var source = Path.GetFullPath(sourceDirectory);
        var prefix = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = file[prefix.Length..];
            var destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private static string? CopyBackgroundImage(string packagePath, string destinationRoot, string? imagePath)
    {
        if (imagePath is null) return null;
        var packageEntryPath = $"content/{imagePath}";
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.Entries.SingleOrDefault(item =>
            string.Equals(item.FullName, packageEntryPath, StringComparison.Ordinal));
        if (entry is null)
            throw new FileNotFoundException($"Launcher background is missing from the module package: {imagePath}");
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        var relativePath = $"branding/launcher-background{extension}";
        var destination = Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var source = entry.Open();
        using var target = File.Create(destination);
        source.CopyTo(target);
        return relativePath;
    }

    private static string? CopyLauncherMusic(string packagePath, string destinationRoot, string? musicPath)
    {
        if (musicPath is null) return null;
        var packageEntryPath = $"content/{musicPath}";
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.Entries.SingleOrDefault(item =>
            string.Equals(item.FullName, packageEntryPath, StringComparison.Ordinal));
        if (entry is null)
            throw new FileNotFoundException($"Launcher music is missing from the module package: {musicPath}");
        var relativePath = "branding/launcher-music.wav";
        var destination = Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var source = entry.Open();
        using var target = File.Create(destination);
        source.CopyTo(target);
        return relativePath;
    }

    private static string BuildReadme(GameLauncherManifest manifest) =>
        $"{manifest.DisplayName}{Environment.NewLine}" +
        $"Version {manifest.Version}{Environment.NewLine}{Environment.NewLine}" +
        $"Double-cliquez {manifest.ExecutableFile} pour jouer.{Environment.NewLine}" +
        "Le lanceur vérifie le runtime et le module avant chaque démarrage, puis conserve les préférences " +
        "dans les données locales de l'utilisateur." + Environment.NewLine +
        (manifest.Multiplayer == GameLauncherMultiplayerMode.DirectConnectPreview
            ? $"Pour héberger par Internet, autorisez et redirigez le port TCP {manifest.DefaultServerPort}. " +
              "Les autres joueurs rejoignent avec votre adresse IP publique ou votre nom DNS." + Environment.NewLine
            : string.Empty);
}
