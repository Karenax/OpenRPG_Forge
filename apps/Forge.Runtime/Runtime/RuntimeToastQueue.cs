using Godot;

namespace Forge.Runtime;

internal enum RuntimeToastKind
{
    Objective,
    Combat,
    Loot,
    Alert,
    System,
}

internal partial class RuntimeToastQueue : CanvasLayer
{
    private const int Capacity = 3;
    private const float PanelRight = -24;

    private readonly ToastEntry[] _entries = new ToastEntry[Capacity];
    private readonly StyleBoxFlat[] _styles = new StyleBoxFlat[Enum.GetValues<RuntimeToastKind>().Length];
    private bool _interfaceVisible = true;
    private long _sequence;

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    private bool LargeText => MotionPolicy.TextScale > 1.5f;

    private float PanelLeft => LargeText ? -590 : -390;

    // Keep the notification stack below the objective tracker in both density modes.
    private float PanelTop => LargeText ? 342 : 270;

    private float PanelHeight => LargeText ? 88 : 55;

    private float PanelGap => LargeText ? 10 : 7;

    internal int ActiveCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < _entries.Length; index++)
                if (_entries[index]?.Active == true) count++;
            return count;
        }
    }

    internal int PeakActiveCount { get; private set; }

    internal string LastText { get; private set; } = string.Empty;

    public override void _Ready()
    {
        Layer = 31;
        ProcessMode = ProcessModeEnum.Always;
        BuildStyles();
        for (var index = 0; index < Capacity; index++)
        {
            var panel = new PanelContainer
            {
                Name = $"RuntimeToast{index + 1}",
                AnchorLeft = 1,
                AnchorRight = 1,
                OffsetLeft = PanelLeft,
                OffsetTop = PanelTop + (PanelHeight + PanelGap) * index,
                OffsetRight = PanelRight,
                OffsetBottom = PanelTop + PanelHeight + (PanelHeight + PanelGap) * index,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
            };
            var label = new Label
            {
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RuntimeRpgUiTheme.StyleBody(label,
                Math.Max(9, Mathf.RoundToInt(11 * MotionPolicy.TextScale)), RuntimeRpgUiTheme.Bone);
            panel.AddChild(label);
            AddChild(panel);
            _entries[index] = new ToastEntry(panel, label);
        }
    }

    public override void _Process(double delta)
    {
        for (var index = 0; index < _entries.Length; index++)
        {
            var entry = _entries[index];
            if (!entry.Active) continue;
            entry.AgeSeconds += delta;
            entry.RemainingSeconds = Math.Max(0, entry.RemainingSeconds - delta);
            if (entry.RemainingSeconds <= 0)
            {
                entry.Active = false;
                entry.Panel.Visible = false;
                continue;
            }

            var alpha = MotionPolicy.ReducedMotion
                ? 1f
                : (float)Math.Clamp(Math.Min(entry.AgeSeconds / .12, entry.RemainingSeconds / .24), 0, 1);
            var slide = MotionPolicy.ReducedMotion
                ? 0
                : (float)(18 * (1 - Math.Clamp(entry.AgeSeconds / .14, 0, 1)));
            entry.Panel.OffsetLeft = PanelLeft + slide;
            entry.Panel.OffsetRight = PanelRight + slide;
            entry.Panel.Modulate = new Color(1, 1, 1, alpha);
            entry.Panel.Visible = _interfaceVisible;
        }
    }

    internal void Show(RuntimeToastKind kind, string text, string? deduplicationKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var cleanText = text.Trim();
        var key = string.IsNullOrWhiteSpace(deduplicationKey)
            ? $"{kind}:{cleanText}"
            : deduplicationKey.Trim();
        ToastEntry? entry = null;
        for (var index = 0; index < _entries.Length; index++)
        {
            if (_entries[index].Active && string.Equals(_entries[index].Key, key, StringComparison.Ordinal))
            {
                entry = _entries[index];
                break;
            }
        }

        if (entry is null)
        {
            for (var index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].Active) continue;
                entry = _entries[index];
                break;
            }
        }

        if (entry is null)
        {
            entry = _entries[0];
            for (var index = 1; index < _entries.Length; index++)
                if (_entries[index].Sequence < entry.Sequence) entry = _entries[index];
        }

        entry.Active = true;
        entry.Key = key;
        entry.Sequence = ++_sequence;
        entry.AgeSeconds = 0;
        entry.RemainingSeconds = MotionPolicy.ToastSeconds;
        entry.Label.Text = Prefix(kind) + cleanText;
        entry.Label.SetMeta("accessible_label", $"{AccessibleKind(kind)} : {cleanText}");
        entry.Panel.AddThemeStyleboxOverride("panel", _styles[(int)kind]);
        entry.Panel.OffsetLeft = PanelLeft;
        entry.Panel.OffsetRight = PanelRight;
        entry.Panel.Modulate = Colors.White;
        entry.Panel.Visible = _interfaceVisible;
        LastText = cleanText;
        PeakActiveCount = Math.Max(PeakActiveCount, ActiveCount);
    }

    internal void SetInterfaceVisible(bool visible)
    {
        _interfaceVisible = visible;
        for (var index = 0; index < _entries.Length; index++)
            _entries[index].Panel.Visible = visible && _entries[index].Active;
    }

    internal void ClearAll()
    {
        for (var index = 0; index < _entries.Length; index++)
        {
            _entries[index].Active = false;
            _entries[index].Panel.Visible = false;
            _entries[index].RemainingSeconds = 0;
        }
    }

    private void BuildStyles()
    {
        foreach (var kind in Enum.GetValues<RuntimeToastKind>())
        {
            var color = kind switch
            {
                RuntimeToastKind.Objective => MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Objective),
                RuntimeToastKind.Combat => MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Damage),
                RuntimeToastKind.Loot => MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Healing),
                RuntimeToastKind.Alert => MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Alert),
                _ => MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Neutral),
            };
            _styles[(int)kind] = RuntimeRpgUiTheme.Frame(new Color("17110ef2"), color, 13, 9);
            if (MotionPolicy.HighContrast)
            {
                _styles[(int)kind].BorderWidthLeft = 3;
                _styles[(int)kind].BorderWidthTop = 3;
                _styles[(int)kind].BorderWidthRight = 3;
                _styles[(int)kind].BorderWidthBottom = 3;
            }
        }
    }

    private static string Prefix(RuntimeToastKind kind) => kind switch
    {
        RuntimeToastKind.Objective => "◆  ",
        RuntimeToastKind.Combat => "⚔  ",
        RuntimeToastKind.Loot => "+  ",
        RuntimeToastKind.Alert => "!  ",
        _ => "•  ",
    };

    private static string AccessibleKind(RuntimeToastKind kind) => kind switch
    {
        RuntimeToastKind.Objective => "Objectif",
        RuntimeToastKind.Combat => "Combat",
        RuntimeToastKind.Loot => "Butin",
        RuntimeToastKind.Alert => "Alerte",
        _ => "Système",
    };

    private sealed class ToastEntry(PanelContainer panel, Label label)
    {
        internal PanelContainer Panel { get; } = panel;

        internal Label Label { get; } = label;

        internal bool Active { get; set; }

        internal string Key { get; set; } = string.Empty;

        internal long Sequence { get; set; }

        internal double AgeSeconds { get; set; }

        internal double RemainingSeconds { get; set; }
    }
}
