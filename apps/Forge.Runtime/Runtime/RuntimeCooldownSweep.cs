using Godot;

namespace Forge.Runtime;

internal partial class RuntimeCooldownSweep : Control
{
    private const int SegmentCount = 48;

    private readonly Vector2[] _fan = new Vector2[SegmentCount + 2];
    private Label? _secondsLabel;
    private double _durationSeconds;
    private double _remainingSeconds;

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    internal bool Active => _remainingSeconds > 0 && _durationSeconds > 0;

    internal double RemainingSeconds => _remainingSeconds;

    internal double DurationSeconds => _durationSeconds;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _secondsLabel = new Label
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = Active,
        };
        RuntimeRpgUiTheme.StyleTitle(_secondsLabel,
            Math.Max(8, Mathf.RoundToInt(10 * MotionPolicy.TextScale)), RuntimeRpgUiTheme.Bone);
        AddChild(_secondsLabel);
        RefreshLabel();
    }

    internal void Configure(double durationSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _durationSeconds = durationSeconds;
        SetRemaining(Math.Min(_remainingSeconds, durationSeconds));
    }

    internal void SetRemaining(double remainingSeconds)
    {
        if (!double.IsFinite(remainingSeconds) || remainingSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
        var bounded = _durationSeconds <= 0 ? 0 : Math.Min(remainingSeconds, _durationSeconds);
        if (Math.Abs(_remainingSeconds - bounded) < .001) return;
        _remainingSeconds = bounded;
        RefreshLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Active) return;
        var center = Size * .5f;
        var radius = Math.Max(2, Math.Min(Size.X, Size.Y) * .48f);
        var ratio = MotionPolicy.ReducedMotion
            ? 1f
            : (float)Math.Clamp(_remainingSeconds / _durationSeconds, 0, 1);
        var start = -Mathf.Pi / 2;
        var sweep = Mathf.Tau * ratio;
        _fan[0] = center;
        for (var index = 0; index <= SegmentCount; index++)
        {
            var angle = start + (sweep * index / SegmentCount);
            _fan[index + 1] = center + Vector2.FromAngle(angle) * radius;
        }

        DrawColoredPolygon(_fan, new Color("090604c9"));
        var border = MotionPolicy.HighContrast ? RuntimeRpgUiTheme.Bone : RuntimeRpgUiTheme.Gold;
        DrawArc(center, radius, start, start + sweep, SegmentCount, border, 1.5f, true);
    }

    private void RefreshLabel()
    {
        if (_secondsLabel is null) return;
        _secondsLabel.Visible = Active;
        _secondsLabel.Text = Active ? $"{_remainingSeconds:0.0}" : string.Empty;
        SetMeta("accessible_label", Active ? $"Recharge, {_remainingSeconds:0.0} secondes restantes" : "Prêt");
    }
}
