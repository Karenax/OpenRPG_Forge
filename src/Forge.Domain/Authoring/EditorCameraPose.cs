using Forge.Domain.Spatial;

namespace Forge.Domain.Authoring;

public sealed record EditorCameraPose
{
    public Vector3Value Pivot { get; }

    public float YawDegrees { get; }

    public float PitchDegrees { get; }

    public float Distance { get; }

    public EditorCameraProjection Projection { get; }

    public float FieldOfView { get; }

    public float OrthographicSize { get; }

    public EditorCameraPose(
        Vector3Value pivot,
        float yawDegrees,
        float pitchDegrees,
        float distance,
        EditorCameraProjection projection = EditorCameraProjection.Perspective,
        float fieldOfView = 55,
        float orthographicSize = 20)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees) ||
            !float.IsFinite(distance) || !float.IsFinite(fieldOfView) ||
            !float.IsFinite(orthographicSize))
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Camera pose values must be finite.");
        }

        if (pitchDegrees is < -89.5f or > 89.5f)
        {
            throw new ArgumentOutOfRangeException(nameof(pitchDegrees), "Camera pitch must remain between -89.5 and 89.5 degrees.");
        }

        if (distance is < 0.5f or > 500f)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Camera distance must remain between 0.5 and 500 units.");
        }

        if (fieldOfView is < 10f or > 120f)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfView), "Camera field of view must remain between 10 and 120 degrees.");
        }

        if (orthographicSize is < 0.5f or > 1000f)
        {
            throw new ArgumentOutOfRangeException(nameof(orthographicSize), "Orthographic size must remain between 0.5 and 1000 units.");
        }

        Pivot = pivot;
        YawDegrees = NormalizeAngle(yawDegrees);
        PitchDegrees = pitchDegrees;
        Distance = distance;
        Projection = projection;
        FieldOfView = fieldOfView;
        OrthographicSize = orthographicSize;
    }

    private static float NormalizeAngle(float value)
    {
        var normalized = value % 360f;
        return normalized <= -180f ? normalized + 360f : normalized > 180f ? normalized - 360f : normalized;
    }
}
