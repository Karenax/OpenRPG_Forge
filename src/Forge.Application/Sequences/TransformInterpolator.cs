using Forge.Domain.Sequences;
using Forge.Domain.Spatial;

namespace Forge.Application.Sequences;

public static class TransformInterpolator
{
    public static TransformValue Sample(TransformValue from, TransformValue to, double progress,
        SequenceEasing easing)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var t = Ease(Math.Clamp(progress, 0, 1), easing);
        return new TransformValue(
            Lerp(from.Position, to.Position, t),
            InterpolateRotation(from.Rotation, to.Rotation, t),
            Lerp(from.Scale, to.Scale, t));
    }

    public static double Ease(double progress, SequenceEasing easing) => easing switch
    {
        SequenceEasing.Linear => progress,
        SequenceEasing.EaseIn => progress * progress,
        SequenceEasing.EaseOut => 1 - ((1 - progress) * (1 - progress)),
        SequenceEasing.EaseInOut => progress < .5
            ? 2 * progress * progress
            : 1 - (Math.Pow(-2 * progress + 2, 2) / 2),
        SequenceEasing.SmoothStep => progress * progress * (3 - (2 * progress)),
        _ => progress,
    };

    private static Vector3Value Lerp(Vector3Value from, Vector3Value to, double t) => new(
        Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t), Lerp(from.Z, to.Z, t));

    private static float Lerp(float from, float to, double t) => (float)(from + ((to - from) * t));

    private static QuaternionValue InterpolateRotation(QuaternionValue from, QuaternionValue to, double t)
    {
        var dot = (from.X * to.X) + (from.Y * to.Y) + (from.Z * to.Z) + (from.W * to.W);
        var direction = dot < 0 ? -1f : 1f;
        var x = Lerp(from.X, to.X * direction, t);
        var y = Lerp(from.Y, to.Y * direction, t);
        var z = Lerp(from.Z, to.Z * direction, t);
        var w = Lerp(from.W, to.W * direction, t);
        var length = Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
        if (length <= double.Epsilon)
        {
            return from;
        }

        return new QuaternionValue((float)(x / length), (float)(y / length),
            (float)(z / length), (float)(w / length));
    }
}
