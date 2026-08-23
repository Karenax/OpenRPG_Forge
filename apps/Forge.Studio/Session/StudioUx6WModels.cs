using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx6WAreaChoice(AreaId Id, string Label);

internal sealed record StudioUx6WEntityChoice(AreaId AreaId, EntityId Id, string Label);

internal sealed record StudioUx6WItemChoice(Guid Id, string Label);

internal sealed record StudioUx6WDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId CommandAreaId,
    ModuleCinematicProfile SourceProfile,
    ModuleCinematicProfile CandidateProfile);

internal sealed record StudioUx6WOverrideInput(
    Guid? Id,
    EntityId EntityId,
    EntityId? ParentEntityId,
    TransformValue From,
    TransformValue To,
    int DurationMilliseconds,
    SequenceEasing Easing,
    TransformAuthority Authority,
    TransformSaveBehavior SaveBehavior,
    bool Replicate);

internal sealed record StudioUx6WTweenInput(
    Guid? Id,
    EntityId EntityId,
    AnimationChannelKind Channel,
    string Property,
    TransformValue? FromTransform,
    TransformValue? ToTransform,
    AnimationTweenValue FromValue,
    AnimationTweenValue ToValue,
    int DurationMilliseconds,
    SequenceEasing Easing,
    TransformAuthority Authority,
    TransformSaveBehavior SaveBehavior,
    bool Replicate);

internal sealed record StudioUx6WDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6WPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6WDiagnostic> Diagnostics,
    IReadOnlyList<string> Results,
    bool OverrideRuntimeVerified,
    bool TransformTweenVerified,
    bool MaterialTweenVerified,
    bool StateTweenVerified,
    bool AuthorityVerified,
    bool CancellationVerified,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6WActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6WApprovalSnapshot(
    StudioUx6WDraft? Draft,
    ProjectRevision? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6WSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool EmptyStateHandled,
    bool InvalidConfigurationBlocked,
    bool CanonicalDryRun,
    bool OverrideRuntimeVerified,
    bool TransformTweenVerified,
    bool MaterialTweenVerified,
    bool StateTweenVerified,
    bool AuthorityVerified,
    bool CancellationVerified,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool CinematicNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
