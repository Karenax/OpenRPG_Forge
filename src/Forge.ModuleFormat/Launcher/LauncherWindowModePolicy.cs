namespace Forge.ModuleFormat.Launcher;

public static class LauncherWindowModePolicy
{
    public static LauncherWindowMode Canonicalize(LauncherWindowMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported launcher window mode.");

        return mode == LauncherWindowMode.Borderless
            ? LauncherWindowMode.Windowed
            : mode;
    }

    public static bool UsesBorderlessFullscreen(LauncherWindowMode mode) =>
        Canonicalize(mode) == LauncherWindowMode.Windowed;
}
