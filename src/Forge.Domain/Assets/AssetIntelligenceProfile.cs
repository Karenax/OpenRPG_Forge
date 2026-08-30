namespace Forge.Domain.Assets;

public sealed record AssetMetadataVector3(double X, double Y, double Z);

public sealed record AssetMetadataBounds(
    AssetMetadataVector3 Minimum,
    AssetMetadataVector3 Maximum);

public sealed record AssetGeometryIntelligence(
    AssetMetadataBounds? Bounds,
    AssetMetadataVector3? DimensionsMeters,
    AssetMetadataVector3 SourceOriginMeters,
    double MetersPerUnit,
    string UpAxis,
    string ForwardAxis,
    string PivotClass);

public sealed record AssetPlacementPoint(
    string Name,
    string Kind,
    AssetMetadataVector3 PositionMeters,
    AssetMetadataVector3 RotationDegrees,
    double Confidence,
    string EvidenceSource,
    bool RequiresReview);

public sealed record AssetPlacementIntelligence(
    string Surface,
    string SnapMode,
    AssetPlacementPoint? SuggestedPivot,
    IReadOnlyList<AssetPlacementPoint> Anchors);

public sealed record AssetIntelligenceTechnicalProfile(
    int? MeshCount,
    long? VertexCount,
    long? TriangleCount,
    int? MaterialCount,
    int? TextureCount,
    int? ArmatureCount,
    int? BoneCount,
    int? AnimationCount,
    IReadOnlyList<string> Materials,
    IReadOnlyList<string> Animations);

public sealed record AssetMetadataEvidence(
    string Field,
    string Source,
    double Confidence,
    string Detail);

public sealed record AssetIntelligenceProfile(
    int SchemaVersion,
    string ExternalId,
    string ModelContentSha256,
    string DependencyClosureSha256,
    IReadOnlyDictionary<string, string> Descriptions,
    string SemanticType,
    string Family,
    string AssemblyRole,
    IReadOnlyList<string> UseCases,
    AssetGeometryIntelligence? Geometry,
    AssetPlacementIntelligence Placement,
    AssetIntelligenceTechnicalProfile Technical,
    string Readiness,
    string ReviewStatus,
    string GeneratedBy,
    double Confidence,
    IReadOnlyList<AssetMetadataEvidence> Evidence)
{
    public const int CurrentSchemaVersion = 1;

    public string Description(string language = "fr")
    {
        if (Descriptions.TryGetValue(language.Trim().ToLowerInvariant(), out var localized) &&
            !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return Descriptions.TryGetValue("en", out var english)
            ? english
            : Descriptions.Values.FirstOrDefault() ?? string.Empty;
    }

    public static AssetIntelligenceProfile CreateUnclassified(
        string externalId,
        string displayName,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var normalizedSource = source.Trim().ToLowerInvariant();
        return new AssetIntelligenceProfile(
            CurrentSchemaVersion,
            externalId.Trim(),
            string.Empty,
            string.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["en"] = $"{displayName.Trim()} is an unclassified asset from {source.Trim()}.",
                ["fr"] = $"{displayName.Trim()} est un asset non classé provenant de {source.Trim()}.",
            },
            "unclassified",
            normalizedSource,
            "unspecified",
            [],
            null,
            new AssetPlacementIntelligence("unspecified", "none", null, []),
            new AssetIntelligenceTechnicalProfile(null, null, null, null, null, null, null, null, [], []),
            "catalogued",
            "generated-review-required",
            "openrpg-catalog-fallback/1",
            0.1,
            [new AssetMetadataEvidence(
                "profile",
                "catalog",
                0.1,
                "Fallback profile created because no governed enrichment was available.")]);
    }
}
