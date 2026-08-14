using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Launcher;

public sealed record GameLauncherManifest(
    int SchemaVersion,
    Guid GameId,
    string Slug,
    string DisplayName,
    string Tagline,
    string Version,
    string AccentColor,
    string ExecutableFile,
    string RuntimeExecutable,
    string ModulePackage,
    string LauncherSha256,
    string RuntimeSha256,
    string ModuleSha256,
    string? BackgroundImage,
    string? NewsUrl,
    string? SupportUrl,
    bool SettingsEnabled,
    GameLauncherMultiplayerMode Multiplayer,
    int DefaultServerPort)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record GameLauncherVerification(
    GameLauncherManifest Manifest,
    string RootDirectory,
    string LauncherPath,
    string RuntimePath,
    string ModulePath,
    string? BackgroundImagePath);

public sealed record GameDistributionFile(string Path, long Bytes, string Sha256);

public sealed record GameDistributionManifest(
    int SchemaVersion,
    Guid GameId,
    string Product,
    string Version,
    string Platform,
    IReadOnlyList<GameDistributionFile> Files)
{
    public const int CurrentSchemaVersion = 1;
}
