using Godot;

namespace Forge.Runtime;

internal sealed partial class RuntimeAnimatedBar : Control
{
    private readonly RuntimeUiMotionPolicy _policy;
    private readonly RuntimeUiSemanticColor _semanticColor;
    private double _maximum = 1;
    private double _authoritative;
    private double _front;
    private double _frontStart;
    private double _frontAge;
    private double _chip;
    private double _chipStart;
    private double _chipAge;
    private double _chipDelay;
    private bool _healing;
    private bool _hasValue;

    internal RuntimeAnimatedBar(
        RuntimeUiMotionPolicy policy,
        RuntimeUiSemanticColor semanticColor,
        float minimumHeight = 4)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _semanticColor = semanticColor;
        CustomMinimumSize = new Vector2(0, Math.Max(2, minimumHeight));
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal double AuthoritativeValue => _authoritative * _maximum;

    internal double FrontValue => _front * _maximum;

    internal double ChipValue => _chip * _maximum;

    internal bool Animating => Math.Abs(_front - _authoritative) > .0001 ||
                               Math.Abs(_chip - _authoritative) > .0001;

    internal void SetAuthoritativeValue(double current, double maximum)
    {
        if (!double.IsFinite(current) || !double.IsFinite(maximum) || maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Runtime bars require finite bounded values.");
        }

        var next = Math.Clamp(current / maximum, 0, 1);
        _maximum = maximum;
        if (!_hasValue || _policy.ReducedMotion)
        {
            _hasValue = true;
            _authoritative = next;
            _front = next;
            _frontStart = next;
            _chip = next;
            _chipStart = next;
            _frontAge = 0;
            _chipAge = 0;
            _chipDelay = 0;
            QueueRedraw();
            return;
        }

        if (Math.Abs(next - _authoritative) <= .000001)
        {
            return;
        }

        var previous = _authoritative;
        _authoritative = next;
        _frontStart = _front;
        _frontAge = 0;
        _healing = next > previous;
        if (_healing)
        {
            _chip = _front;
            _chipStart = _front;
            _chipDelay = 0;
            _chipAge = 0;
        }
        else
        {
            _chipStart = Math.Max(_chip, previous);
            _chip = _chipStart;
            _chipDelay = _policy.DamageChipDelaySeconds;
            _chipAge = 0;
        }

        QueueRedraw();
    }

    internal void SnapToAuthoritative()
    {
        _front = _authoritative;
        _frontStart = _authoritative;
        _chip = _authoritative;
        _chipStart = _authoritative;
        _frontAge = 0;
        _chipAge = 0;
        _chipDelay = 0;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_hasValue || !Animating)
        {
            return;
        }

        _frontAge += delta;
        var frontProgress = _policy.BarTransitionSeconds <= 0
            ? 1
            : Math.Clamp(_frontAge / _policy.BarTransitionSeconds, 0, 1);
        _front = Lerp(_frontStart, _authoritative, EaseOut(frontProgress));

        if (_healing)
        {
            _chip = _front;
        }
        else if (_chipDelay > 0)
        {
            _chipDelay = Math.Max(0, _chipDelay - delta);
        }
        else
        {
            _chipAge += delta;
            var chipProgress = _policy.DamageChipTransitionSeconds <= 0
                ? 1
                : Math.Clamp(_chipAge / _policy.DamageChipTransitionSeconds, 0, 1);
            _chip = Lerp(_chipStart, _authoritative, EaseOut(chipProgress));
        }

        if (!Animating)
        {
            SnapToAuthoritative();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, _policy.HighContrast ? new Color("050403") : new Color("090706d8"));
        var chipColor = _policy.HighContrast
            ? new Color("fff1b8")
            : _policy.ResolveColor(RuntimeUiSemanticColor.Alert).Lightened(.16f);
        if (_chip > 0)
        {
            DrawRect(new Rect2(0, 0, (float)(Size.X * _chip), Size.Y), chipColor);
        }
        if (_front > 0)
        {
            DrawRect(new Rect2(0, 0, (float)(Size.X * _front), Size.Y),
                _policy.ResolveColor(_semanticColor));
        }
        if (_policy.HighContrast && Size.X > 1 && Size.Y > 1)
        {
            DrawRect(bounds, RuntimeRpgUiTheme.Parchment, filled: false, width: 1);
        }
    }

    private static double EaseOut(double value) => 1 - Math.Pow(1 - value, 3);

    private static double Lerp(double from, double to, double weight) => from + ((to - from) * weight);
}
