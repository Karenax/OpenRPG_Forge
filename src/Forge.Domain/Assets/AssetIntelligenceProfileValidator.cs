using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public static class AssetIntelligenceProfileValidator
{
    public const string InvalidCode = "asset.intelligence.invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        AssetIntelligenceProfile profile,
        string path = "assets/intelligence")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.SchemaVersion != AssetIntelligenceProfile.CurrentSchemaVersion)
        {
            Add(diagnostics, path, "schemaVersion", "Asset intelligence schema is unsupported.");
        }

        if (string.IsNullOrWhiteSpace(profile.ExternalId) ||
            !ValidOptionalHash(profile.ModelContentSha256) ||
            !ValidOptionalHash(profile.DependencyClosureSha256))
        {
            Add(diagnostics, path, "identity", "External ID and optional SHA-256 values must be valid.");
        }

        if (profile.Descriptions is null || profile.Descriptions.Count == 0 ||
            profile.Descriptions.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)) ||
            string.IsNullOrWhiteSpace(profile.SemanticType) ||
            string.IsNullOrWhiteSpace(profile.Family) ||
            string.IsNullOrWhiteSpace(profile.AssemblyRole) ||
            profile.UseCases is null || profile.UseCases.Any(string.IsNullOrWhiteSpace))
        {
            Add(diagnostics, path, "semantics", "Descriptions, semantic type, family, assembly role and use cases must be governed.");
        }

        if (!ValidConfidence(profile.Confidence) ||
            string.IsNullOrWhiteSpace(profile.Readiness) ||
            string.IsNullOrWhiteSpace(profile.ReviewStatus) ||
            string.IsNullOrWhiteSpace(profile.GeneratedBy))
        {
            Add(diagnostics, path, "governance", "Readiness, review status, generator and confidence are required.");
        }

        ValidateGeometry(profile.Geometry, path, diagnostics);
        ValidatePlacement(profile.Placement, path, diagnostics);
        ValidateTechnical(profile.Technical, path, diagnostics);
        if (profile.Evidence is null || profile.Evidence.Count == 0 || profile.Evidence.Any(item =>
                item is null || string.IsNullOrWhiteSpace(item.Field) || string.IsNullOrWhiteSpace(item.Source) ||
                string.IsNullOrWhiteSpace(item.Detail) || !ValidConfidence(item.Confidence)))
        {
            Add(diagnostics, path, "evidence", "Every profile requires typed evidence with a confidence from zero to one.");
        }

        return diagnostics;
    }

    private static void ValidateGeometry(
        AssetGeometryIntelligence? geometry,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (geometry is null) return;
        if (!ValidVector(geometry.SourceOriginMeters) || !double.IsFinite(geometry.MetersPerUnit) ||
            geometry.MetersPerUnit <= 0 || string.IsNullOrWhiteSpace(geometry.UpAxis) ||
            string.IsNullOrWhiteSpace(geometry.ForwardAxis) || string.IsNullOrWhiteSpace(geometry.PivotClass))
        {
            Add(diagnostics, path, "geometry", "Geometry axes, unit scale, source origin and pivot class are invalid.");
        }

        if (geometry.DimensionsMeters is { } dimensions &&
            (!ValidVector(dimensions) || dimensions.X < 0 || dimensions.Y < 0 || dimensions.Z < 0))
        {
            Add(diagnostics, path, "geometry/dimensionsMeters", "Geometry dimensions must be finite non-negative metres.");
        }

        if (geometry.Bounds is { } bounds &&
            (!ValidVector(bounds.Minimum) || !ValidVector(bounds.Maximum) ||
             bounds.Minimum.X > bounds.Maximum.X || bounds.Minimum.Y > bounds.Maximum.Y ||
             bounds.Minimum.Z > bounds.Maximum.Z))
        {
            Add(diagnostics, path, "geometry/bounds", "Geometry bounds must be finite and ordered.");
        }
    }

    private static void ValidatePlacement(
        AssetPlacementIntelligence? placement,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (placement is null || string.IsNullOrWhiteSpace(placement.Surface) ||
            string.IsNullOrWhiteSpace(placement.SnapMode) || placement.Anchors is null)
        {
            Add(diagnostics, path, "placement", "Placement surface, snap mode and anchors are required.");
            return;
        }

        var points = placement.Anchors.Concat(placement.SuggestedPivot is null ? [] : [placement.SuggestedPivot]);
        if (points.Any(point => point is null || string.IsNullOrWhiteSpace(point.Name) || string.IsNullOrWhiteSpace(point.Kind) ||
                string.IsNullOrWhiteSpace(point.EvidenceSource) || !ValidVector(point.PositionMeters) ||
                !ValidVector(point.RotationDegrees) || !ValidConfidence(point.Confidence)) ||
            placement.Anchors.Where(anchor => anchor is not null).Select(anchor => anchor.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            placement.Anchors.Count)
        {
            Add(diagnostics, path, "placement/points", "Placement points require unique names, finite transforms and governed confidence.");
        }
    }

    private static void ValidateTechnical(
        AssetIntelligenceTechnicalProfile? technical,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (technical is null || technical.Materials is null || technical.Animations is null ||
            new long?[]
            {
                technical.MeshCount, technical.VertexCount, technical.TriangleCount, technical.MaterialCount,
                technical.TextureCount, technical.ArmatureCount, technical.BoneCount, technical.AnimationCount,
            }.Any(value => value < 0) || technical.Materials.Any(string.IsNullOrWhiteSpace) ||
            technical.Animations.Any(string.IsNullOrWhiteSpace))
        {
            Add(diagnostics, path, "technical", "Technical counts and names cannot be negative or blank.");
        }
    }

    private static bool ValidOptionalHash(string value) => string.IsNullOrEmpty(value) ||
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool ValidConfidence(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool ValidVector(AssetMetadataVector3? value) => value is not null &&
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    private static void Add(
        ICollection<DomainDiagnostic> diagnostics,
        string root,
        string suffix,
        string message) => diagnostics.Add(new(InvalidCode, message, $"{root.TrimEnd('/')}/{suffix}"));
}
