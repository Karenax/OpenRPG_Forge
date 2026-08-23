using System.Globalization;

namespace Forge.Studio.Core.Accessibility;

public static class StudioDisplayScale
{
    public const double Minimum = 0.75;

    public const double Maximum = 2.0;

    public static bool TryParseOverride(string? value, out double? scale)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            scale = null;
            return true;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && parsed is >= Minimum and <= Maximum)
        {
            scale = parsed;
            return true;
        }

        scale = null;
        return false;
    }

    public static double Resolve(double? requestedScale, double profileScale)
    {
        var scale = requestedScale ?? profileScale;
        if (!double.IsFinite(scale) || scale is < Minimum or > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                requestedScale is null ? nameof(profileScale) : nameof(requestedScale),
                scale,
                $"Studio display scale must be between {Minimum:0.##} and {Maximum:0.##}.");
        }

        return scale;
    }
}
