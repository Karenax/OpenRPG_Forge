using Forge.Application.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx6MModuleChoice(Guid ModuleId, string Label, string Detail);

internal sealed record StudioUx6MAutomationDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid ProfileId,
    int SchemaVersion,
    string Provenance,
    ScriptAutomationProfile.CameraSequencePolicy Camera,
    ScriptAutomationProfile.TagRoutingPolicy TagRouting,
    IReadOnlyList<ScriptAutomationProfile.FeatureFlag> ModuleFlags,
    IReadOnlyList<ScriptAutomationProfile.AiSetting> AiSettings,
    ScriptAutomationProfile OriginalProfile);

internal sealed record StudioUx6MDiagnostic(string Code, string Path, string Severity, string Message);

internal sealed record StudioUx6MResolution(
    bool CameraSequencePlanned,
    bool CameraControlLocked,
    bool CameraControlRestored,
    bool AccessibilitySkip,
    bool TagRouteResolved,
    string ResolvedScript,
    bool ModuleFlagResolved,
    string ModuleFlag,
    string ModuleFlagValue,
    bool AiSettingResolved,
    string AiSetting,
    string AiSettingValue,
    bool InvalidAiFallback,
    IReadOnlyList<string> Trace);

internal sealed record StudioUx6MPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx6MDiagnostic> Diagnostics,
    StudioUx6MResolution? Resolution,
    bool DryRunPure);

internal sealed record StudioUx6MActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6MApprovalSnapshot(
    StudioUx6MAutomationDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx6MSmokeResult(
    bool Succeeded,
    bool ExplicitModule,
    bool CameraAutomation,
    bool TagRouting,
    bool ModuleFlags,
    bool AiSettings,
    bool InvalidCameraBlocked,
    bool InvalidTagBlocked,
    bool InvalidFlagBlocked,
    bool InvalidAiBlocked,
    bool CanonicalDryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool ApplyUndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
