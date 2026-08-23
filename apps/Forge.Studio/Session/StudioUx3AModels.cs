using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal enum StudioUx3AOperation
{
    EditBlueprint,
    DeriveFromInstance,
}

internal sealed record StudioUx3ABlueprintChoice(
    Guid Id,
    string Label,
    BlueprintKind Kind,
    BlueprintPaletteOrigin Origin,
    bool IsReadOnly,
    bool IsFavorite,
    string Version,
    int UsageCount);

internal sealed record StudioUx3AEntityChoice(
    Guid AreaId,
    Guid EntityId,
    Guid BlueprintId,
    string Label,
    int OverrideCount,
    string VersionRequirement);

internal sealed record StudioUx3ADraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    StudioUx3AOperation Operation,
    Guid BlueprintId,
    Guid? SourceAreaId,
    Guid? SourceEntityId,
    Guid? SourceBlueprintId,
    string Slug,
    string DisplayName,
    BlueprintKind Kind,
    string Version,
    string PackageNamespace,
    string ManifestPath,
    IReadOnlyList<string> ExposedParameters,
    IReadOnlyList<string> Dependencies,
    string? PreviewPath,
    long PlatformByteBudget,
    bool Approved);

internal sealed record StudioUx3ADiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3APreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool ReadOnlyProtected,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3ADiagnostic> Diagnostics);

internal sealed record StudioUx3AActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3AApprovalSnapshot(
    StudioUx3ADraft? Draft,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx3AEvidenceProject(
    ProjectDefinition Project,
    Guid EditableBlueprintId,
    Guid StandardBlueprintId,
    Guid AreaId,
    Guid EntityId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3ASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool StandardPalette,
    bool ProjectPalette,
    bool StandardReadOnly,
    bool ReusableBlueprint,
    bool InstanceResolved,
    bool DerivationRetargets,
    bool OverridesPreserved,
    bool VersionPinned,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool BlueprintNeighborsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
