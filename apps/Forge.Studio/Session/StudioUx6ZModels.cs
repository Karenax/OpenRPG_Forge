using Forge.Application.Projects;
using Forge.Domain.Combat;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Abilities;

namespace Forge.Studio.Session;

internal sealed record StudioUx6ZOwnerChoice(Guid Id, string Label, int MaximumResource, int ExistingAbilities);

internal sealed record StudioUx6ZScriptChoice(string RelativePath, string Label);

internal sealed record StudioUx6ZItemChoice(string Slug, string Label);

internal sealed record StudioUx6ZEffectDraft(AbilityEffectKind Kind, int Magnitude);

internal sealed record StudioUx6ZDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid AbilityId,
    Guid OwnerBlueprintId,
    string Slug,
    string DisplayName,
    int ResourceCost,
    double CooldownSeconds,
    AbilityTargeting Targeting,
    IReadOnlyList<StudioUx6ZEffectDraft> Effects,
    string VfxCueKey,
    string AudioCueKey,
    string ScriptPath,
    string? RequiredItemSlug,
    IReadOnlySet<string> AiTags,
    string BalanceIntent,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed,
    bool Approved);

internal sealed record StudioUx6ZDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6ZPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool PermissionSatisfied,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx6ZDiagnostic> Diagnostics,
    AssistedAbilityProposal? Proposal);

internal sealed record StudioUx6ZActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6ZApprovalSnapshot(
    StudioUx6ZDraft? Draft,
    AssistedAbilityProposal? Proposal,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    Guid TransactionId,
    Guid ExecutionPlanId);

internal sealed record StudioUx6ZEvidenceProject(
    ProjectDefinition Project,
    Guid OwnerId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx6ZSmokeResult(
    bool Succeeded,
    bool ExplicitOwnerSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool ScriptResolved,
    bool OptionalItemResolved,
    bool InvalidConfigurationBlocked,
    bool ApprovalRequired,
    bool PermissionRequired,
    bool FiveCombatTests,
    bool BalanceResolved,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool RuntimeDocument,
    bool UndoRedo,
    bool Persistence,
    bool OwnerNeighborsPreserved,
    bool ModuleNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
