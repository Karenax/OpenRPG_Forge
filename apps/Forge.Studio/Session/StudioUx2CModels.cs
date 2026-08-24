using Forge.Application.Projects;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal enum StudioUx2CTerrainOperation
{
    Sculpt,
    Feature,
    TileProperty,
}

internal sealed record StudioUx2CAreaChoice(
    AreaId AreaId,
    string Label,
    bool HasEditableTerrain,
    int FeatureCount,
    int PlacementCount);

internal sealed record StudioUx2CNamedChoice(Guid Id, string Label);

internal sealed record StudioUx2CPlacementChoice(Guid Id, string Label);

internal sealed record StudioUx2CTerrainDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    StudioUx2CTerrainOperation Operation,
    TerrainBrushOperation BrushOperation,
    float WorldX,
    float WorldZ,
    float Radius,
    float Strength,
    float TargetHeight,
    Guid MaterialLayerId,
    Guid FeatureId,
    bool FeatureIsNew,
    string FeatureName,
    TerrainFeatureKind FeatureKind,
    IReadOnlyList<Vector3Value> FeatureControlPoints,
    float FeatureWidth,
    float FeatureHeight,
    float FeatureBankDegrees,
    TerrainFeatureCrossingPolicy FeatureCrossingPolicy,
    IReadOnlyList<string> FeatureBiomeTags,
    string FeatureMaterialResource,
    Guid PlacementId,
    Guid PropertyId,
    string SemanticState,
    string Animation,
    bool LoopAnimation,
    IReadOnlyDictionary<string, string> MaterialOverrides,
    bool LightEnabled,
    string LightColor,
    float LightIntensity,
    float LightRangeMeters,
    bool CollisionEnabled,
    bool NavigationEnabled,
    IReadOnlyList<string> SocketOverrides,
    IReadOnlyDictionary<string, string> PropertyOverrides);

internal sealed record StudioUx2CTerrainPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Diagnostics,
    int ChangedSamples,
    int ChangedCells,
    int FeatureCount,
    int PropertyCount);

internal sealed record StudioUx2CActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2CApprovalSnapshot(
    StudioUx2CTerrainDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2CSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool DirectBrushInput,
    bool Sculpt,
    bool Feature,
    bool TileProperty,
    bool FullCollections,
    bool DryRun,
    bool InvalidValuesBlocked,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool NeighboursPreserved,
    bool HumanText);
