using Forge.Application.Blueprints;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx3BBlueprintChoice(
    Guid Id,
    string Label,
    BlueprintKind Kind,
    BlueprintPaletteOrigin Origin,
    string Version,
    int InstanceCount,
    bool IsReadOnly,
    bool HasVariant,
    bool HasComposition);

internal sealed record StudioUx3BInstanceChoice(
    Guid AreaId,
    Guid EntityId,
    Guid BlueprintId,
    string Label,
    string VersionRequirement,
    IReadOnlyList<EntityPropertyOverride> Overrides);

internal sealed record StudioUx3BMigrationImpact(
    Guid EntityId,
    string Label,
    string VersionRequirement,
    string Action,
    bool Excluded);

internal sealed record StudioUx3BUpdateDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    Guid SourceAreaId,
    Guid SourceEntityId,
    IReadOnlyList<string> PromotedOverrideKeys,
    BlueprintVersionBump VersionBump,
    IReadOnlyList<Guid> ExcludedEntityIds,
    IReadOnlyList<string> CategoryPaths,
    bool Approved);

internal sealed record StudioUx3BVariantOverrideDraft(
    PrefabVariantOverrideKind Kind,
    string Key,
    string Value);

internal sealed record StudioUx3BCompositionNodeDraft(
    Guid Id,
    Guid BlueprintId,
    Guid? ParentNodeId,
    string? Socket,
    TransformValue Transform);

internal sealed record StudioUx3BPrefabDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    bool VariantEnabled,
    Guid VariantId,
    string VariantProvenance,
    Guid? BaseBlueprintId,
    IReadOnlyList<StudioUx3BVariantOverrideDraft> VariantOverrides,
    bool VariantRequiredAtRuntime,
    bool CompositionEnabled,
    Guid CompositionId,
    string CompositionProvenance,
    IReadOnlyList<StudioUx3BCompositionNodeDraft> CompositionNodes,
    IReadOnlyList<string> AssemblyRules,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> AssemblyScripts,
    bool CompositionRequiredAtRuntime,
    IReadOnlyList<string> AuthorCollections,
    bool Approved);

internal sealed record StudioUx3BDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3BUpdatePreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    string PreviousVersion,
    string TargetVersion,
    IReadOnlyList<EntityPropertyOverride> PromotedDefaults,
    IReadOnlyList<StudioUx3BMigrationImpact> Instances,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3BDiagnostic> Diagnostics);

internal sealed record StudioUx3BPrefabPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<PrefabVariantOverride> EffectiveVariantOverrides,
    int CompositionNodeCount,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3BDiagnostic> Diagnostics);

internal sealed record StudioUx3BActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3BApprovalSnapshot(
    StudioUx3BUpdateDraft? UpdateDraft,
    ProjectRevision? UpdateRevision,
    string? UpdateProjectFingerprint,
    string? UpdateDraftFingerprint,
    StudioUx3BPrefabDraft? PrefabDraft,
    ProjectRevision? PrefabRevision,
    string? PrefabProjectFingerprint,
    string? PrefabDraftFingerprint);

internal sealed record StudioUx3BEvidenceProject(
    ProjectDefinition Project,
    Guid BlueprintId,
    Guid BaseBlueprintId,
    Guid AreaId,
    Guid SourceEntityId,
    Guid MigratedEntityId,
    Guid ExcludedEntityId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3BSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool UpdatePlan,
    bool SelectivePromotion,
    bool SemanticVersionBump,
    bool MigrationImpact,
    bool ExclusionPreserved,
    bool OverridesPreserved,
    bool CategoriesHierarchical,
    bool VariantConfigured,
    bool CompositionConfigured,
    bool RuntimeProjection,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool SeparateApprovals,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool BlueprintNeighborsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
