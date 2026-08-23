using Forge.Application.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx6SProfileChoice(string Label);

internal sealed record StudioUx6SDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool DeviceQueriesEnabled,
    IReadOnlyList<string> AllowedDeviceQueries,
    bool RequireConsent,
    bool RedactStableIdentifiers,
    bool ResourceCommandsEnabled,
    int MaximumResourceResults,
    long MaximumResourceBytes,
    IReadOnlyList<string> AllowedResourceNamespaces,
    bool PartyControlEnabled,
    int MaximumControlledMembers,
    bool AllowDirectSwitch,
    IReadOnlyList<string> PartyCommands,
    int MaximumProgressionTracks,
    bool DataDrivenProgression,
    bool SupportLargeSetUi,
    IReadOnlyList<string> RequestedDeviceQueries,
    int RequestedResourceResults,
    int RequestedPartyMembers,
    int RequestedProgressionTracks);

internal sealed record StudioUx6SDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6SPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6SDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool DevicePolicyResolved,
    bool ResourcePolicyResolved,
    bool PartyPolicyResolved,
    bool ProgressionPolicyResolved,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6SActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6SApprovalSnapshot(
    StudioUx6SDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6SSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool DeviceValidation,
    bool ResourceValidation,
    bool PartyValidation,
    bool ProgressionValidation,
    bool CanonicalDryRun,
    bool DeviceResolved,
    bool ResourceResolved,
    bool PartyResolved,
    bool ProgressionResolved,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool CreatorNeighborsPreserved,
    bool PostLaunchNeighborsPreserved,
    bool ExtensionNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
