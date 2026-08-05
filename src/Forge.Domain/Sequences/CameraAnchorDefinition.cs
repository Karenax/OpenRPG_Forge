using Forge.Domain.Spatial;

namespace Forge.Domain.Sequences;

public sealed record CameraAnchorDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public TransformValue Transform { get; }
    public Vector3Value LookAt { get; }
    public CameraFraming Framing { get; }
    public float FieldOfViewDegrees { get; }
    public int DefaultDurationMilliseconds { get; }
    public float SafeAreaMargin { get; }
    public IReadOnlyList<string> Tags { get; }
    public string Provenance { get; }

    public CameraAnchorDefinition(Guid id, string name, TransformValue transform, Vector3Value lookAt,
        CameraFraming framing, float fieldOfViewDegrees, int defaultDurationMilliseconds, float safeAreaMargin,
        IReadOnlyList<string> tags, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || !float.IsFinite(fieldOfViewDegrees) || fieldOfViewDegrees is < 1 or > 179 ||
            defaultDurationMilliseconds is < 1 or > 86_400_000 || !float.IsFinite(safeAreaMargin) ||
            safeAreaMargin is < 0 or > .45f || tags.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Camera anchor is invalid.");
        }

        Id = id;
        Name = name.Trim();
        Transform = transform;
        LookAt = lookAt;
        Framing = framing;
        FieldOfViewDegrees = fieldOfViewDegrees;
        DefaultDurationMilliseconds = defaultDurationMilliseconds;
        SafeAreaMargin = safeAreaMargin;
        Tags = tags.Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Provenance = provenance.Trim();
    }
}
