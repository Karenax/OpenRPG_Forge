using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Launcher;

public sealed record GameLaunchPlan(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments);

public static class GameLaunchPlanBuilder
{
    public const string ModulePackageArgument = "--module-package";
    public const string SettingsArgument = "--launcher-settings";
    public const string SessionModeArgument = "--launcher-mode";
    public const string PlayerNameArgument = "--player-name";
    public const string ServerAddressArgument = "--server-address";
    public const string ServerPortArgument = "--server-port";

    public static GameLaunchPlan Create(
        GameLauncherVerification verification,
        GameLauncherUserSettings settings,
        string settingsPath)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        var diagnostics = GameLauncherUserSettingsValidator.Validate(settings);
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException("Launcher settings are invalid: " + string.Join("; ", diagnostics));
        }

        var capability = verification.Manifest.Multiplayer;
        if (settings.SessionMode == LauncherSessionMode.LocalHost &&
            capability == GameLauncherMultiplayerMode.Disabled)
        {
            throw new InvalidOperationException("This game does not enable local multiplayer hosting.");
        }

        if (settings.SessionMode == LauncherSessionMode.DirectConnectPreview)
        {
            if (capability != GameLauncherMultiplayerMode.DirectConnectPreview)
                throw new InvalidOperationException("This game does not expose direct-connect preparation.");
            throw new NotSupportedException(
                "Direct remote connection is prepared in the launcher but remains unavailable until the network transport is connected.");
        }

        var fullSettingsPath = Path.GetFullPath(settingsPath);
        if (!File.Exists(fullSettingsPath))
            throw new FileNotFoundException("Launcher settings must be saved before starting the game.", fullSettingsPath);
        var arguments = new List<string>
        {
            "--",
            ModulePackageArgument,
            verification.ModulePath,
            SettingsArgument,
            fullSettingsPath,
            SessionModeArgument,
            settings.SessionMode switch
            {
                LauncherSessionMode.Solo => "solo",
                LauncherSessionMode.LocalHost => "local-host",
                _ => "direct-connect-preview",
            },
            PlayerNameArgument,
            settings.PlayerName.Trim(),
            ServerPortArgument,
            settings.ServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (settings.SessionMode == LauncherSessionMode.DirectConnectPreview)
        {
            arguments.Add(ServerAddressArgument);
            arguments.Add(settings.ServerAddress.Trim());
        }

        if (settings.ReduceMotion) arguments.Add("--reduce-motion");
        return new GameLaunchPlan(verification.RuntimePath, verification.RootDirectory, arguments);
    }
}
