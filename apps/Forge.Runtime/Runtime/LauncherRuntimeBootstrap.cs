using Forge.ModuleFormat.Launcher;
using Forge.Network.Sessions;
using Forge.Network.Transport;
using Godot;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Runtime;

internal sealed record LauncherRuntimeContext(
    GameLauncherUserSettings Settings,
    string SettingsPath,
    string RequestedSessionMode);

internal sealed class LauncherRuntimeNetworkSession : IDisposable
{
    private bool _disposed;

    internal LauncherRuntimeNetworkSession(
        string moduleFingerprint,
        string requestedSessionMode,
        TcpGameClient client,
        TcpAuthoritativeServer? server,
        X509Certificate2? hostCertificate = null)
    {
        ModuleFingerprint = moduleFingerprint;
        RequestedSessionMode = requestedSessionMode;
        Client = client;
        Server = server;
        HostCertificate = hostCertificate;
    }

    internal string ModuleFingerprint { get; }

    internal string RequestedSessionMode { get; }

    internal TcpGameClient Client { get; }

    internal TcpAuthoritativeServer? Server { get; }

    private X509Certificate2? HostCertificate { get; }

    internal bool IsHost => Server is not null;

    internal bool IsInternetHost => RequestedSessionMode == "internet-host";

    internal int ConnectedPlayers => Math.Max(1, Client.LatestSnapshot?.Players.Count ?? 0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Client.Dispose();
        Server?.Dispose();
        HostCertificate?.Dispose();
    }
}

internal static class LauncherRuntimeBootstrap
{
    internal static LauncherRuntimeContext? LoadAndApply(Node root, IReadOnlyList<string> userArguments)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(userArguments);
        var settingsPath = GetArgumentValue(userArguments, GameLaunchPlanBuilder.SettingsArgument);
        if (settingsPath is null) return null;
        if (!Path.IsPathFullyQualified(settingsPath) ||
            !string.Equals(Path.GetExtension(settingsPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Launcher settings must use an absolute JSON path.");
        }

        var settings = GameLauncherSettingsStore.Load(settingsPath);
        var requestedMode = GetArgumentValue(userArguments, GameLaunchPlanBuilder.SessionModeArgument)
            ?? throw new InvalidOperationException("Launcher session mode is missing.");
        var modeMatches = settings.SessionMode switch
        {
            LauncherSessionMode.Solo => requestedMode == "solo",
            LauncherSessionMode.LocalHost => requestedMode is "local-host" or "internet-host",
            LauncherSessionMode.DirectConnectPreview => requestedMode == "internet-join",
            _ => false,
        };
        if (!modeMatches)
            throw new InvalidOperationException("Launcher arguments do not match the persisted session mode.");
        var requestedPlayer = GetArgumentValue(userArguments, GameLaunchPlanBuilder.PlayerNameArgument);
        if (!string.Equals(requestedPlayer, settings.PlayerName.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Launcher player name does not match the persisted settings.");
        var requestedPort = GetArgumentValue(userArguments, GameLaunchPlanBuilder.ServerPortArgument);
        if (!int.TryParse(requestedPort, out var port) || port != settings.ServerPort)
            throw new InvalidOperationException("Launcher server port does not match the persisted settings.");
        if (settings.SessionMode == LauncherSessionMode.DirectConnectPreview)
        {
            var requestedAddress = GetArgumentValue(userArguments, GameLaunchPlanBuilder.ServerAddressArgument);
            if (!string.Equals(requestedAddress, settings.ServerAddress.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("Launcher server address does not match the persisted settings.");
        }

        ApplyDisplay(root, settings);
        ApplyAudio(settings);
        var appliedWindow = root.GetWindow();
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_LAUNCHER_SETTINGS_PASS resolution={settings.ResolutionWidth}x{settings.ResolutionHeight} " +
            $"window={settings.WindowMode.ToString().ToLowerInvariant()} quality={settings.GraphicsQuality.ToString().ToLowerInvariant()} " +
            $"effective={appliedWindow.Size.X}x{appliedWindow.Size.Y} borderless={appliedWindow.Borderless.ToString().ToLowerInvariant()} " +
            $"resizable={(!appliedWindow.Unresizable).ToString().ToLowerInvariant()} " +
            $"vsync={settings.VSync.ToString().ToLowerInvariant()} session={requestedMode}");
        return new LauncherRuntimeContext(settings, Path.GetFullPath(settingsPath), requestedMode);
    }

    internal static async Task<LauncherRuntimeNetworkSession?> CreateNetworkSessionAsync(
        LauncherRuntimeContext? context,
        string moduleFingerprint,
        string moduleTitle,
        CancellationToken cancellationToken = default)
    {
        if (context is null || context.Settings.SessionMode == LauncherSessionMode.Solo) return null;
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleTitle);
        var settings = context.Settings;
        TcpAuthoritativeServer? server = null;
        X509Certificate2? hostCertificate = null;
        try
        {
            var address = settings.ServerAddress.Trim();
            var secureInternet = context.RequestedSessionMode is "internet-host" or "internet-join";
            if (settings.SessionMode == LauncherSessionMode.LocalHost)
            {
                var internetHost = context.RequestedSessionMode == "internet-host";
                InternetServerSecurityOptions? serverSecurity = null;
                if (internetHost)
                {
                    hostCertificate = LoadHostCertificate(settings, context.SettingsPath);
                    var actualFingerprint = InternetTransportSecurity.CertificateSha256(hostCertificate);
                    if (!string.Equals(
                            actualFingerprint,
                            settings.ServerCertificateSha256,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Launcher host certificate does not match the persisted invitation pin.");
                    serverSecurity = new InternetServerSecurityOptions(
                        hostCertificate,
                        [new(Guid.Empty, InternetTransportSecurity.TokenSha256(settings.NetworkJoinToken))]);
                }
                var configuration = new DedicatedServerConfiguration(
                    Guid.NewGuid(),
                    1,
                    internetHost ? $"{moduleTitle} · Partie Internet" : $"{moduleTitle} · Partie locale",
                    internetHost ? "0.0.0.0" : "127.0.0.1",
                    settings.ServerPort,
                    8,
                    internetHost ? DiscoveryMode.Direct : DiscoveryMode.Lan,
                    moduleFingerprint,
                    InternetTransportProtocol.GameProtocolVersion);
                server = new TcpAuthoritativeServer(configuration, security: serverSecurity);
                server.Start();
                address = "127.0.0.1";
            }

            var client = await TcpGameClient.ConnectAsync(
                new InternetClientOptions(
                    address,
                    settings.ServerPort,
                    settings.PlayerName.Trim(),
                    moduleFingerprint,
                    AccountId: settings.NetworkAccountId,
                    AuthenticationToken: secureInternet ? settings.NetworkJoinToken : null,
                    RequireTls: secureInternet,
                    PinnedServerCertificateSha256: secureInternet
                        ? settings.ServerCertificateSha256
                        : null),
                cancellationToken);
            var networkSession = new LauncherRuntimeNetworkSession(
                moduleFingerprint,
                context.RequestedSessionMode,
                client,
                server,
                hostCertificate);
            hostCertificate = null;
            GD.Print(
                settings.SessionMode == LauncherSessionMode.LocalHost
                    ? $"OPENRPG_FORGE_RUNTIME_INTERNET_HOST_PASS session={client.SessionId:D} " +
                      $"bind={(networkSession.IsInternetHost ? "0.0.0.0" : "127.0.0.1")} " +
                      $"port={settings.ServerPort} transport={(client.IsEncrypted ? "tls-tcp" : "tcp-loopback-dev")} " +
                      $"authenticated={client.IsAuthenticated.ToString().ToLowerInvariant()}"
                    : $"OPENRPG_FORGE_RUNTIME_INTERNET_JOIN_PASS session={client.SessionId:D} " +
                      $"address={address} port={settings.ServerPort} transport=tls-tcp " +
                      $"authenticated={client.IsAuthenticated.ToString().ToLowerInvariant()}");
            return networkSession;
        }
        catch
        {
            server?.Dispose();
            hostCertificate?.Dispose();
            throw;
        }
    }

    private static X509Certificate2 LoadHostCertificate(
        GameLauncherUserSettings settings,
        string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settings.NetworkJoinToken) ||
            string.IsNullOrWhiteSpace(settings.ServerCertificateSha256) ||
            string.IsNullOrWhiteSpace(settings.HostCertificatePath) ||
            string.IsNullOrWhiteSpace(settings.HostCertificatePassword))
            throw new InvalidOperationException("Secure Internet host material is incomplete.");
        var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsPath))
            ?? throw new InvalidOperationException("Launcher settings directory is unavailable.");
        var certificatePath = Path.GetFullPath(settings.HostCertificatePath);
        if (!string.Equals(
                Path.GetDirectoryName(certificatePath),
                settingsDirectory,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(certificatePath), ".pfx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Launcher host certificate must stay beside its settings file.");
        var file = new FileInfo(certificatePath);
        if (!file.Exists || file.Length is < 1 or > 1024 * 1024 ||
            file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Launcher host certificate file is invalid.");
        return X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            settings.HostCertificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
    }

    private static void ApplyDisplay(Node root, GameLauncherUserSettings settings)
    {
        var window = root.GetWindow();
        var requestedSize = new Vector2I(settings.ResolutionWidth, settings.ResolutionHeight);
        var primaryScreen = DisplayServer.GetPrimaryScreen();
        var screen = new Rect2I(
            DisplayServer.ScreenGetPosition(primaryScreen),
            DisplayServer.ScreenGetSize(primaryScreen));
        if (screen.Size.X <= 0 || screen.Size.Y <= 0)
            screen = new Rect2I(Vector2I.Zero, requestedSize);
        window.AlwaysOnTop = false;
        switch (LauncherWindowModePolicy.Canonicalize(settings.WindowMode))
        {
            case LauncherWindowMode.Windowed:
                window.Mode = Window.ModeEnum.Windowed;
                window.Borderless = true;
                window.Unresizable = true;
                window.Size = screen.Size;
                window.Position = screen.Position;
                break;
            case LauncherWindowMode.Fullscreen:
                window.Borderless = false;
                window.Unresizable = false;
                window.Mode = Window.ModeEnum.Fullscreen;
                break;
            default:
                throw new InvalidOperationException($"Unsupported launcher window mode: {settings.WindowMode}");
        }

        DisplayServer.WindowSetVsyncMode(settings.VSync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        root.GetViewport().Scaling3DScale = settings.GraphicsQuality switch
        {
            LauncherGraphicsQuality.Low => .72f,
            LauncherGraphicsQuality.Medium => .85f,
            _ => 1f,
        };
    }

    private static void ApplyAudio(GameLauncherUserSettings settings)
    {
        SetBusVolume("Master", settings.MasterVolume);
        SetBusVolume("Music", settings.MusicVolume);
        SetBusVolume("SFX", settings.EffectsVolume);
    }

    private static void SetBusVolume(string busName, int value)
    {
        var index = AudioServer.GetBusIndex(busName);
        if (index < 0) return;
        var linear = Math.Max(.0001f, value / 100f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(index, value == 0);
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, name, StringComparison.Ordinal))
            {
                if (index + 1 >= arguments.Count)
                    throw new InvalidOperationException($"Missing value after {name}.");
                return arguments[index + 1];
            }

            var prefix = name + "=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal)) return argument[prefix.Length..];
        }

        return null;
    }
}
