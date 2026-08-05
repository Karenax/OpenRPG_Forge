namespace Forge.Domain.Spatial;

public sealed record AnimationTweenValue
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float W { get; }
    public string Text { get; }

    public AnimationTweenValue(float x, float y, float z, float w, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
            throw new ArgumentOutOfRangeException(nameof(x), "Tween values must be finite.");
        X = x; Y = y; Z = z; W = w; Text = text.Trim();
    }

    public static AnimationTweenValue Number(float value) => new(value, 0, 0, 0, string.Empty);
    public static AnimationTweenValue Color(float red, float green, float blue, float alpha) =>
        new(red, green, blue, alpha, string.Empty);
    public static AnimationTweenValue State(string value) => new(0, 0, 0, 0, value);
}
