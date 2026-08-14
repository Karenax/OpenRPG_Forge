#if TOOLS
using Godot;

namespace Forge.Toolset.Editor;

/// <summary>
/// Owns the lifecycle of Creator tool windows. The Forge workspace is the only surface visible at
/// startup and tool windows behave like reusable documents: opening one closes the previous one.
/// </summary>
internal sealed class CreatorToolWindowManager : IDisposable
{
    private readonly Node _host;
    private readonly Action<Window> _brand;
    private readonly List<Window> _windows = [];
    private readonly Dictionary<Window, Action> _visibilityHandlers = [];
    private readonly Dictionary<Window, Action> _closeHandlers = [];
    private Window? _activeWindow;
    private bool _changingVisibility;
    private bool _disposed;

    internal CreatorToolWindowManager(Node host, Action<Window> brand)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
    }

    internal int RegisteredCount => _windows.Count;

    internal int VisibleCount => _windows.Count(window =>
        GodotObject.IsInstanceValid(window) && window.Visible);

    internal bool HasSingleVisibleTool => VisibleCount <= 1;

    internal bool StartupWasClear { get; private set; }

    /// <summary>
    /// Must run before the host enters the editor tree. Some Window subclasses rely on the engine
    /// default (visible) state, which previously made every one of them appear during startup.
    /// </summary>
    internal void PrepareExistingWindows()
    {
        ThrowIfDisposed();
        foreach (var window in _host.GetChildren().OfType<Window>())
        {
            Register(window);
        }
    }

    internal void NormalizeAfterTreeReady()
    {
        ThrowIfDisposed();
        foreach (var window in _windows)
        {
            Configure(window);
        }

        HideAll();
        StartupWasClear = VisibleCount == 0;
        GD.Print($"OPENRPG_FORGE_TOOL_WINDOW_MANAGER_READY registered={RegisteredCount} " +
                 $"visible={VisibleCount} mode=single-active startup={(StartupWasClear ? "clear" : "blocked")}");
    }

    internal void Open(Window? window, Action open)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(open);
        if (window is null || !GodotObject.IsInstanceValid(window))
        {
            return;
        }

        Register(window);
        HideAllExcept(window);
        open();
        HandleVisibilityChanged(window);
    }

    internal void HideActive()
    {
        if (_disposed)
        {
            return;
        }

        if (_activeWindow is not null && GodotObject.IsInstanceValid(_activeWindow))
        {
            _activeWindow.Hide();
        }

        _activeWindow = null;
    }

    private void Register(Window window)
    {
        if (_windows.Contains(window))
        {
            return;
        }

        // Hiding before the node enters the SceneTree prevents native or embedded window flashes.
        window.Visible = false;
        Configure(window);

        Action visibilityHandler = () => HandleVisibilityChanged(window);
        Action closeHandler = () => Close(window);
        window.VisibilityChanged += visibilityHandler;
        window.CloseRequested += closeHandler;
        _visibilityHandlers.Add(window, visibilityHandler);
        _closeHandlers.Add(window, closeHandler);
        _windows.Add(window);
    }

    private void HandleVisibilityChanged(Window window)
    {
        if (_disposed || _changingVisibility || !GodotObject.IsInstanceValid(window))
        {
            return;
        }

        if (!window.Visible)
        {
            if (_activeWindow == window)
            {
                _activeWindow = null;
            }

            return;
        }

        Configure(window);
        HideAllExcept(window);
        _activeWindow = window;
        _brand(window);
    }

    private void Close(Window window)
    {
        if (_disposed || !GodotObject.IsInstanceValid(window))
        {
            return;
        }

        window.Hide();
        if (_activeWindow == window)
        {
            _activeWindow = null;
        }
    }

    private void HideAll()
    {
        _changingVisibility = true;
        try
        {
            foreach (var window in _windows.Where(GodotObject.IsInstanceValid))
            {
                window.Hide();
            }
        }
        finally
        {
            _changingVisibility = false;
            _activeWindow = null;
        }
    }

    private void HideAllExcept(Window selected)
    {
        _changingVisibility = true;
        try
        {
            foreach (var window in _windows.Where(window =>
                         window != selected && GodotObject.IsInstanceValid(window) && window.Visible))
            {
                window.Hide();
            }
        }
        finally
        {
            _changingVisibility = false;
        }
    }

    private static void Configure(Window window)
    {
        window.Borderless = false;
        window.AlwaysOnTop = false;
        window.Exclusive = false;
        window.Unresizable = false;
        window.Transient = true;
        CreatorWindowLayoutPolicy.Apply(window);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        HideAll();
        foreach (var window in _windows.Where(GodotObject.IsInstanceValid))
        {
            window.VisibilityChanged -= _visibilityHandlers[window];
            window.CloseRequested -= _closeHandlers[window];
        }

        _visibilityHandlers.Clear();
        _closeHandlers.Clear();
        _windows.Clear();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
#endif
