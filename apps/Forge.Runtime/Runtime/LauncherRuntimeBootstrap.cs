using Forge.ModuleFormat.Launcher;
using Forge.Network.Sessions;
using Forge.Network.Transport;
using Godot;

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
        TcpAuthoritativeServer? server)
    {
        ModuleFingerprint = moduleFingerprint;
        RequestedSessionMode = requestedSessionMode;
        Client = client;
        Server = server;
    }

    internal string ModuleFingerprint { get; }

    internal string RequestedSessionMode { get; }

    internal TcpGameClient Client { get; }

    internal TcpAuthoritativeServer? Server { get; }

    internal bool IsHost => Server is not null;

    internal bool IsInternetHost => RequestedSessionMode == "internet-host";

    internal int ConnectedPlayers => Math.Max(1, Client.LatestSnapshot?.Players.Count ?? 0);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Client.Dispose();
        Server?.Dispose();
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
        try
        {
            var address = settings.ServerAddress.Trim();
            if (settings.SessionMode == LauncherSessionMode.LocalHost)
            {
                var internetHost = context.RequestedSessionMode == "internet-host";
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
                server = new TcpAuthoritativeServer(configuration);
                server.Start();
                address = "127.0.0.1";
            }

            var client = await TcpGameClient.ConnectAsync(
                new InternetClientOptions(
                    address,
                    settings.ServerPort,
                    settings.PlayerName.Trim(),
                    moduleFingerprint),
                cancellationToken);
            var networkSession = new LauncherRuntimeNetworkSession(
                moduleFingerprint,
                context.RequestedSessionMode,
                client,
                server);
            GD.Print(
                settings.SessionMode == LauncherSessionMode.LocalHost
                    ? $"OPENRPG_FORGE_RUNTIME_INTERNET_HOST_PASS session={client.SessionId:D} " +
                      $"bind={(networkSession.IsInternetHost ? "0.0.0.0" : "127.0.0.1")} " +
                      $"port={settings.ServerPort} transport=tcp"
                    : $"OPENRPG_FORGE_RUNTIME_INTERNET_JOIN_PASS session={client.SessionId:D} " +
                      $"address={address} port={settings.ServerPort} transport=tcp");
            return networkSession;
        }
        catch
        {
            server?.Dispose();
            throw;
        }
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
