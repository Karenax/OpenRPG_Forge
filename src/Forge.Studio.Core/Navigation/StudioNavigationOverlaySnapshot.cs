namespace Forge.Studio.Core.Navigation;

public enum StudioNavigationOverlaySemantic
{
    Surface,
    OffMeshLink,
    Anchor,
    Route,
    Transition,
    Volume,
    Visibility,
    Lod,
}

public sealed record StudioNavigationPoint
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public StudioNavigationPoint(double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Navigation overlay points must be finite.");
        }
        X = x;
        Y = y;
        Z = z;
    }
}

public sealed record StudioNavigationOverlayMarker(
    string StableKey,
    string HumanLabel,
    StudioNavigationOverlaySemantic Semantic,
    StudioNavigationPoint Position,
    bool Selected = false,
    bool Broken = false);

public sealed record StudioNavigationOverlaySegment(
    string StableKey,
    string HumanLabel,
    StudioNavigationOverlaySemantic Semantic,
    StudioNavigationPoint From,
    StudioNavigationPoint To,
    bool Directed = false,
    bool Selected = false,
    bool Broken = false);

public sealed record StudioNavigationOverlayPolygon(
    string StableKey,
    string HumanLabel,
    StudioNavigationOverlaySemantic Semantic,
    IReadOnlyList<StudioNavigationPoint> Points,
    bool Selected = false,
    bool Broken = false);

public sealed class StudioNavigationOverlaySnapshot
{
    public IReadOnlyList<StudioNavigationOverlayMarker> Markers { get; }
    public IReadOnlyList<StudioNavigationOverlaySegment> Segments { get; }
    public IReadOnlyList<StudioNavigationOverlayPolygon> Polygons { get; }

    public StudioNavigationOverlaySnapshot(
        IReadOnlyList<StudioNavigationOverlayMarker>? markers,
        IReadOnlyList<StudioNavigationOverlaySegment>? segments,
        IReadOnlyList<StudioNavigationOverlayPolygon>? polygons)
    {
        Markers = Normalize(markers ?? [], ValidateMarker);
        Segments = Normalize(segments ?? [], ValidateSegment);
        Polygons = Normalize(polygons ?? [], ValidatePolygon);
        var duplicate = Markers.Select(item => item.StableKey)
            .Concat(Segments.Select(item => item.StableKey))
            .Concat(Polygons.Select(item => item.StableKey))
            .GroupBy(item => item, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Navigation overlay key '{duplicate.Key}' is duplicated.");
        }
    }

    public static StudioNavigationOverlaySnapshot Empty { get; } = new([], [], []);

    private static IReadOnlyList<T> Normalize<T>(IReadOnlyList<T> source, Func<T, string> validate)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var item in source)
        {
            _ = validate(item);
        }
        return source.OrderBy(validate, StringComparer.Ordinal).ToArray();
    }

    private static string ValidateMarker(StudioNavigationOverlayMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        ValidateText(marker.StableKey, marker.HumanLabel);
        ArgumentNullException.ThrowIfNull(marker.Position);
        return marker.StableKey;
    }

    private static string ValidateSegment(StudioNavigationOverlaySegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ValidateText(segment.StableKey, segment.HumanLabel);
        ArgumentNullException.ThrowIfNull(segment.From);
        ArgumentNullException.ThrowIfNull(segment.To);
        return segment.StableKey;
    }

    private static string ValidatePolygon(StudioNavigationOverlayPolygon polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ValidateText(polygon.StableKey, polygon.HumanLabel);
        ArgumentNullException.ThrowIfNull(polygon.Points);
        if (polygon.Points.Count < 3 || polygon.Points.Any(item => item is null))
        {
            throw new ArgumentException("Navigation overlay polygons require at least three finite points.");
        }
        return polygon.StableKey;
    }

    private static void ValidateText(string key, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
    }
}
