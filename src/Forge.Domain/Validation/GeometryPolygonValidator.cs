using Forge.Domain.Spatial;

namespace Forge.Domain.Validation;

public static class GeometryPolygonValidator
{
    public const string SelfIntersectionCode = "area.geometry.polygon_self_intersection";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        IReadOnlyList<Vector3Value> vertices, string path)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (vertices.Count < 3) return [];
        var diagnostics = new List<DomainDiagnostic>();
        for (var left = 0; left < vertices.Count; left++)
        {
            var leftNext = (left + 1) % vertices.Count;
            for (var right = left + 1; right < vertices.Count; right++)
            {
                var rightNext = (right + 1) % vertices.Count;
                if (left == right || leftNext == right || rightNext == left) continue;
                if (Intersects(vertices[left], vertices[leftNext], vertices[right], vertices[rightNext]))
                {
                    diagnostics.Add(new(SelfIntersectionCode, "Geometry polygon intersects itself.", path));
                    return diagnostics;
                }
            }
        }
        return diagnostics;
    }

    private static bool Intersects(Vector3Value a, Vector3Value b, Vector3Value c, Vector3Value d)
    {
        static float Cross(Vector3Value p, Vector3Value q, Vector3Value r) =>
            (q.X - p.X) * (r.Z - p.Z) - (q.Z - p.Z) * (r.X - p.X);
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        return abC * abD < 0 && cdA * cdB < 0;
    }
}
