#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

internal static class CreatorWindowLayoutPolicy
{
    private static readonly Vector2I MaximumMinimumSize = new(960, 600);
    private static readonly Vector2I DefaultWindowSize = new(1180, 640);

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
        window.MinSize = new Vector2I(
            Math.Min(window.MinSize.X, MaximumMinimumSize.X),
            Math.Min(window.MinSize.Y, MaximumMinimumSize.Y));
        window.Size = new Vector2I(
            Math.Min(Math.Max(window.Size.X, window.MinSize.X), DefaultWindowSize.X),
            Math.Min(Math.Max(window.Size.Y, window.MinSize.Y), DefaultWindowSize.Y));
    }

    internal static bool IsResponsive(Node root, out int windowCount)
    {
        ArgumentNullException.ThrowIfNull(root);
        var windows = root.GetChildren().OfType<Window>().ToArray();
        windowCount = windows.Length;
        return windowCount > 0 && windows.Count(window => window.Visible) <= 1 && windows.All(window =>
            !window.Borderless && !window.AlwaysOnTop && !window.Exclusive && !window.Unresizable &&
            window.MinSize.X <= MaximumMinimumSize.X && window.MinSize.Y <= MaximumMinimumSize.Y);
    }
}
#endif
