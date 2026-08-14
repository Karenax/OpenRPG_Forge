#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

internal static class CreatorWindowLayoutPolicy
{
    private static readonly Vector2I FallbackUsableSize = new(1920, 1080);
    private static readonly Vector2I ScreenMargin = new(96, 96);
    private static readonly Vector2I DefaultHeadroom = new(240, 160);

    internal static void Apply(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var window in root.GetChildren().OfType<Window>())
        {
            Apply(window);
        }
    }

    internal static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var maximumSize = GetMaximumWindowSize();
        window.MinSize = new Vector2I(
            Math.Min(window.MinSize.X, maximumSize.X),
            Math.Min(window.MinSize.Y, maximumSize.Y));
        window.Size = new Vector2I(
            Math.Min(Math.Max(window.Size.X, window.MinSize.X + DefaultHeadroom.X), maximumSize.X),
            Math.Min(Math.Max(window.Size.Y, window.MinSize.Y + DefaultHeadroom.Y), maximumSize.Y));
    }

    internal static bool IsResponsive(Node root, out int windowCount)
    {
        ArgumentNullException.ThrowIfNull(root);
        var windows = root.GetChildren().OfType<Window>().ToArray();
        var maximumSize = GetMaximumWindowSize();
        windowCount = windows.Length;
        return windowCount > 0 && windows.Count(window => window.Visible) <= 1 && windows.All(window =>
            !window.Borderless && !window.AlwaysOnTop && !window.Exclusive && !window.Unresizable &&
            window.MinSize.X <= maximumSize.X && window.MinSize.Y <= maximumSize.Y &&
            window.Size.X >= window.MinSize.X && window.Size.Y >= window.MinSize.Y &&
            window.Size.X <= maximumSize.X && window.Size.Y <= maximumSize.Y);
    }

    private static Vector2I GetMaximumWindowSize()
    {
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.GetPrimaryScreen()).Size;
        if (usable.X <= 0 || usable.Y <= 0)
        {
            usable = FallbackUsableSize;
        }

        return new Vector2I(
            Math.Max(640, usable.X - ScreenMargin.X),
            Math.Max(480, usable.Y - ScreenMargin.Y));
    }
}
#endif
