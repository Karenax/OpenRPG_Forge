namespace Forge.ModuleFormat.Launcher;

public enum LauncherWindowMode
{
    Windowed,
    Borderless,
    Fullscreen,
}

public enum LauncherGraphicsQuality
{
    Low,
    Medium,
    High,
    Ultra,
}

public enum LauncherSessionMode
{
    Solo,
    LocalHost,
    DirectConnectPreview,
}

public sealed record GameLauncherUserSettings(
    int SchemaVersion,
    int ResolutionWidth,
    int ResolutionHeight,
    LauncherWindowMode WindowMode,
    LauncherGraphicsQuality GraphicsQuality,
    bool VSync,
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool ReduceMotion,
    LauncherSessionMode SessionMode,
    string PlayerName,
    string ServerAddress,
    int ServerPort,
    Guid NetworkAccountId = default,
    string NetworkJoinToken = "",
    string ServerCertificateSha256 = "",
    string HostCertificatePath = "",
    string HostCertificatePassword = "")
{
    public const int CurrentSchemaVersion = 2;

    public static GameLauncherUserSettings CreateDefault(GameLauncherManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new GameLauncherUserSettings(
            CurrentSchemaVersion,
            1600,
            900,
            LauncherWindowMode.Fullscreen,
            LauncherGraphicsQuality.High,
            VSync: true,
            MasterVolume: 85,
            MusicVolume: 75,
            EffectsVolume: 85,
            ReduceMotion: false,
            LauncherSessionMode.Solo,
            "Joueur",
            "127.0.0.1",
            manifest.DefaultServerPort,
            Guid.NewGuid());
    }
}

public static class GameLauncherUserSettingsValidator
{
    public static IReadOnlyList<string> Validate(GameLauncherUserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var diagnostics = new List<string>();
        if (settings.SchemaVersion != GameLauncherUserSettings.CurrentSchemaVersion)
            diagnostics.Add("launcher.settings.schema_unsupported");
        if (settings.ResolutionWidth is < 800 or > 7680 || settings.ResolutionHeight is < 600 or > 4320)
            diagnostics.Add("launcher.settings.resolution_invalid");
        if (!Enum.IsDefined(settings.WindowMode)) diagnostics.Add("launcher.settings.window_mode_invalid");
        if (!Enum.IsDefined(settings.GraphicsQuality)) diagnostics.Add("launcher.settings.quality_invalid");
        if (settings.MasterVolume is < 0 or > 100 || settings.MusicVolume is < 0 or > 100 ||
            settings.EffectsVolume is < 0 or > 100)
            diagnostics.Add("launcher.settings.volume_invalid");
        if (!Enum.IsDefined(settings.SessionMode)) diagnostics.Add("launcher.settings.session_mode_invalid");
        if (string.IsNullOrWhiteSpace(settings.PlayerName) || settings.PlayerName.Trim().Length > 40)
            diagnostics.Add("launcher.settings.player_name_invalid");
        if (string.IsNullOrWhiteSpace(settings.ServerAddress) || settings.ServerAddress.Length > 253 ||
            settings.ServerAddress.Any(char.IsWhiteSpace))
            diagnostics.Add("launcher.settings.server_address_invalid");
        if (settings.ServerPort is < 1024 or > 65_535) diagnostics.Add("launcher.settings.server_port_invalid");
        if (settings.NetworkAccountId == Guid.Empty)
            diagnostics.Add("launcher.settings.network_account_invalid");
        var token = settings.NetworkJoinToken ?? string.Empty;
        var certificate = settings.ServerCertificateSha256 ?? string.Empty;
        var hostCertificatePath = settings.HostCertificatePath ?? string.Empty;
        var hostCertificatePassword = settings.HostCertificatePassword ?? string.Empty;
        if (token.Length > 0 && (token.Length is < 24 or > 256 || token.Any(char.IsWhiteSpace)))
            diagnostics.Add("launcher.settings.network_token_invalid");
        if (certificate.Length > 0 &&
            (certificate.Length != 64 || certificate.Any(character => !Uri.IsHexDigit(character))))
            diagnostics.Add("launcher.settings.server_certificate_invalid");
        if ((hostCertificatePath.Length == 0) != (hostCertificatePassword.Length == 0) ||
            (hostCertificatePath.Length > 0 &&
             (!Path.IsPathFullyQualified(hostCertificatePath) ||
              !string.Equals(Path.GetExtension(hostCertificatePath), ".pfx", StringComparison.OrdinalIgnoreCase) ||
              hostCertificatePassword.Length is < 24 or > 256)))
            diagnostics.Add("launcher.settings.host_certificate_invalid");
        if (settings.SessionMode == LauncherSessionMode.DirectConnectPreview &&
            (token.Length == 0 || certificate.Length == 0))
            diagnostics.Add("launcher.settings.secure_invite_required");
        return diagnostics;
    }
}
