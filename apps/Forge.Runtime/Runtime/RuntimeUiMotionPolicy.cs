using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Runtime;

internal enum RuntimeUiSemanticColor
{
    Damage,
    Healing,
    Resource,
    Objective,
    Alert,
    Neutral,
}

internal sealed record RuntimeUiMotionPolicy
{
    private const string TextScaleArgument = "--combat-feedback-text-scale";

    internal bool ReducedMotion { get; init; }

    internal bool HighContrast { get; init; }

    internal float TextScale { get; init; }

    internal RuntimeColorVisionMode ColorVisionMode { get; init; }

    internal double BarTransitionSeconds => ReducedMotion ? 0 : .14;

    internal double DamageChipDelaySeconds => ReducedMotion ? 0 : .13;

    internal double DamageChipTransitionSeconds => ReducedMotion ? 0 : .28;

    internal double FloatingNumberSeconds => ReducedMotion ? 1.1 : .7;

    internal double NameplateUpdateSeconds => 1d / 30d;

    internal double ImpactSeconds => ReducedMotion ? 0 : .22;

    internal double CameraImpulseSeconds => ReducedMotion ? 0 : .12;

    internal double HitStopSeconds => ReducedMotion ? 0 : .045;

    internal double ToastSeconds => 3.4;

    internal double MapPingSeconds => 2.4;

    internal float CameraImpulseMeters => ReducedMotion ? 0 : .12f;

    internal static RuntimeUiMotionPolicy Create(
        AccessibilityQualityProfile? accessibility,
        IReadOnlyList<string> userArguments)
    {
        ArgumentNullException.ThrowIfNull(userArguments);
        var textScale = accessibility?.RuntimeTextScale ?? 1;
        for (var index = 0; index < userArguments.Count; index++)
        {
            if (!string.Equals(userArguments[index], TextScaleArgument, StringComparison.Ordinal)) continue;
            if (index + 1 >= userArguments.Count ||
                !double.TryParse(userArguments[index + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out textScale) ||
                !double.IsFinite(textScale))
                throw new ArgumentException($"{TextScaleArgument} requires a finite numeric value.",
                    nameof(userArguments));
            break;
        }
        return new RuntimeUiMotionPolicy
        {
            ReducedMotion = accessibility?.RuntimeReducedMotion == true ||
                            userArguments.Contains("--reduce-motion", StringComparer.Ordinal),
            HighContrast = accessibility?.RuntimeHighContrast == true,
            TextScale = (float)Math.Clamp(textScale, .8, 2),
            ColorVisionMode = accessibility?.RuntimeColorVisionMode ?? RuntimeColorVisionMode.Standard,
        };
    }

    internal Color ResolveColor(RuntimeUiSemanticColor semantic) => (ColorVisionMode, semantic) switch
    {
        (_, RuntimeUiSemanticColor.Neutral) => RuntimeRpgUiTheme.Parchment,
        (_, RuntimeUiSemanticColor.Objective) => RuntimeRpgUiTheme.Gold,
        (_, RuntimeUiSemanticColor.Alert) => HighContrast ? new Color("ffd36f") : RuntimeRpgUiTheme.Ember,
        (RuntimeColorVisionMode.Deuteranopia or RuntimeColorVisionMode.Protanopia,
            RuntimeUiSemanticColor.Damage) => new Color("d7a24f"),
        (RuntimeColorVisionMode.Deuteranopia or RuntimeColorVisionMode.Protanopia,
            RuntimeUiSemanticColor.Healing) => new Color("65aeb0"),
        (RuntimeColorVisionMode.Tritanopia, RuntimeUiSemanticColor.Damage) => new Color("d46170"),
        (RuntimeColorVisionMode.Tritanopia, RuntimeUiSemanticColor.Healing) => new Color("78a85d"),
        (_, RuntimeUiSemanticColor.Damage) => RuntimeRpgUiTheme.Health,
        (_, RuntimeUiSemanticColor.Healing) => RuntimeRpgUiTheme.Sage,
        (_, RuntimeUiSemanticColor.Resource) => RuntimeRpgUiTheme.FocusBlue,
        _ => RuntimeRpgUiTheme.Parchment,
    };
}
