using Forge.Application.Projects;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Studio.Session;

internal sealed record StudioUx6RProfileChoice(string Label);

internal sealed record StudioUx6RRuleDraft(
    Guid Id,
    string Key,
    string Kind,
    string JsonSchema,
    string DefaultsJson,
    bool Required,
    string Provenance);

internal sealed record StudioUx6RWidgetDraft(
    Guid Id,
    string Key,
    string Kind,
    string ParentKey,
    string Text,
    string BindingKey,
    bool Interactive,
    int FocusOrder,
    string Permission);

internal sealed record StudioUx6REventDraft(
    Guid Id,
    string WidgetKey,
    string Event,
    string Command,
    string InputContext,
    string Permission);

internal sealed record StudioUx6RUiDocumentDraft(
    Guid Id,
    string Key,
    string Title,
    string ThemeKey,
    bool Required,
    IReadOnlyList<StudioUx6RWidgetDraft> Widgets,
    IReadOnlyList<StudioUx6REventDraft> Events,
    string Provenance);

internal sealed record StudioUx6RJsonDocumentDraft(
    Guid Id,
    string Key,
    string JsonSchema,
    string InitialValue,
    int MaxBytes,
    int MaxDepth,
    bool Mutable,
    string Provenance);

internal sealed record StudioUx6RSnapshotDraft(
    Guid Id,
    IReadOnlyList<string> Components,
    int MaxReferences,
    int MaxInventoryItems,
    int MaxBlackboardBytes,
    bool RestoreReferences,
    string Provenance);

internal sealed record StudioUx6RDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    ModuleRuntimeExtensionProfile SourceExtensions,
    IReadOnlyList<StudioUx6RRuleDraft> Rules,
    IReadOnlyList<StudioUx6RUiDocumentDraft> UiDocuments,
    IReadOnlyList<StudioUx6RJsonDocumentDraft> JsonDocuments,
    StudioUx6RSnapshotDraft Snapshot);

internal sealed record StudioUx6RDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6RPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6RDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool RuleResolved,
    bool UiEventPublished,
    bool JsonMutationSucceeded,
    bool SnapshotPolicyReady,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6RActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6RApprovalSnapshot(
    StudioUx6RDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6RSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool RuleValidation,
    bool UiValidation,
    bool JsonValidation,
    bool SnapshotValidation,
    bool CanonicalDryRun,
    bool RuleResolved,
    bool UiEventPublished,
    bool JsonMutation,
    bool SnapshotReady,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborProfilesPreserved,
    bool StableIdentities,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
