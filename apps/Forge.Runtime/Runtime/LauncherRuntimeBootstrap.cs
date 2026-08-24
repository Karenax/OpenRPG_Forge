using Forge.ModuleFormat.Launcher;
using Forge.Network.Sessions;
using Godot;

namespace Forge.Runtime;

internal sealed record LauncherRuntimeContext(
    GameLauncherUserSettings Settings,
    string SettingsPath);

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
        var expectedMode = settings.SessionMode switch
        {
            LauncherSessionMode.Solo => "solo",
            LauncherSessionMode.LocalHost => "local-host",
            _ => "direct-connect-preview",
        };
        if (!string.Equals(requestedMode, expectedMode, StringComparison.Ordinal))
            throw new InvalidOperationException("Launcher arguments do not match the persisted session mode.");
        if (settings.SessionMode == LauncherSessionMode.DirectConnectPreview)
            throw new NotSupportedException(
                "Direct remote connection remains unavailable until the network transport is connected.");

        ApplyDisplay(root, settings);
        ApplyAudio(settings);
        var appliedWindow = root.GetWindow();
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_LAUNCHER_SETTINGS_PASS resolution={settings.ResolutionWidth}x{settings.ResolutionHeight} " +
            $"window={settings.WindowMode.ToString().ToLowerInvariant()} quality={settings.GraphicsQuality.ToString().ToLowerInvariant()} " +
            $"effective={appliedWindow.Size.X}x{appliedWindow.Size.Y} borderless={appliedWindow.Borderless.ToString().ToLowerInvariant()} " +
            $"resizable={(!appliedWindow.Unresizable).ToString().ToLowerInvariant()} " +
            $"vsync={settings.VSync.ToString().ToLowerInvariant()} session={expectedMode}");
        return new LauncherRuntimeContext(settings, Path.GetFullPath(settingsPath));
    }

    internal static AuthoritativeSession? CreateLocalHostSession(
        LauncherRuntimeContext? context,
        string moduleFingerprint,
        string moduleTitle)
    {
        if (context?.Settings.SessionMode != LauncherSessionMode.LocalHost) return null;
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleTitle);
        var settings = context.Settings;
        var configuration = new DedicatedServerConfiguration(
            Guid.NewGuid(),
            1,
            $"{moduleTitle} · Partie locale",
            "127.0.0.1",
            settings.ServerPort,
            8,
            DiscoveryMode.Lan,
            moduleFingerprint,
            "1");
        var session = new AuthoritativeSession(configuration);
        var decision = session.Connect(
            Guid.NewGuid(),
            new SessionIdentity(Guid.NewGuid(), settings.PlayerName.Trim(), SessionRole.Player),
            moduleFingerprint,
            configuration.ProtocolVersion);
        if (!decision.Accepted)
            throw new InvalidOperationException($"Local authoritative session refused the player: {decision.Code}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_LOCAL_HOST_PASS session={session.SessionId:D} port={settings.ServerPort} " +
            $"players={session.Snapshot.Members.Count} revision={session.Snapshot.Revision}");
        return session;
    }

    private static void ApplyDisplay(Node root, GameLauncherUserSettings settings)
    {
        var window = root.GetWindow();
        var requestedSize = new Vector2I(settings.ResolutionWidth, settings.ResolutionHeight);
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.GetPrimaryScreen());
        if (usable.Size.X <= 0 || usable.Size.Y <= 0)
            usable = new Rect2I(Vector2I.Zero, requestedSize);
        window.AlwaysOnTop = false;
        switch (settings.WindowMode)
        {
            case LauncherWindowMode.Windowed:
                window.Mode = Window.ModeEnum.Windowed;
                window.Borderless = false;
                window.Unresizable = false;
                var maximumWindowedSize = new Vector2I(
                    Math.Max(800, usable.Size.X - 96),
                    Math.Max(600, usable.Size.Y - 96));
                var windowedSize = new Vector2I(
                    Math.Min(requestedSize.X, maximumWindowedSize.X),
                    Math.Min(requestedSize.Y, maximumWindowedSize.Y));
                window.Size = windowedSize;
                window.Position = new Vector2I(
                    usable.Position.X + Math.Max(0, (usable.Size.X - windowedSize.X) / 2),
                    usable.Position.Y + Math.Max(0, (usable.Size.Y - windowedSize.Y) / 2));
                break;
            case LauncherWindowMode.Borderless:
                window.Mode = Window.ModeEnum.Windowed;
                window.Borderless = true;
                window.Unresizable = true;
                window.Size = usable.Size;
                window.Position = usable.Position;
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
