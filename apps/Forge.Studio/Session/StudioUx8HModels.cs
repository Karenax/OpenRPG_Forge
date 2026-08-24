using Forge.Application.Projects;
using Forge.Domain.Modules;

namespace Forge.Studio.Session;

internal sealed record StudioUx8HTargetDraft(
    Guid Id,
    int SchemaVersion,
    ModuleTargetKind Target,
    bool Enabled,
    bool IncludeEditorData,
    bool IncludeDebugSymbols,
    bool OptimizeContent,
    bool RequireApprovedLicenses,
    string OutputName,
    string SecretsProfile,
    string Provenance);

internal sealed record StudioUx8HDistributionDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid DistributionId,
    int DistributionSchemaVersion,
    string DistributionProvenance,
    Guid ReleaseId,
    int ReleaseSchemaVersion,
    string ReleaseProvenance,
    IReadOnlyList<StudioUx8HTargetDraft> Targets,
    ModuleReleaseChannel Channel,
    IReadOnlyList<ModuleDistributionPlatform> Platforms,
    ModuleSigningMode SigningMode,
    string SigningProfile,
    IReadOnlyList<string> TrustedKeyFingerprints,
    IReadOnlyList<string> RevokedKeyFingerprints,
    bool AllowUnsignedDevelopment,
    int SigningSchemaVersion,
    bool DeltaEnabled,
    int DeltaChunkSizeBytes,
    bool DeltaAllowResume,
    bool DeltaDeduplicateByHash,
    bool DeltaAllowRollback,
    int DeltaRetainedVersions,
    int DeltaSchemaVersion,
    WindowsPackageMode WindowsMode,
    bool DetectWindowsPrerequisites,
    bool WriteWindowsInstallLog,
    bool IncludeWindowsUninstaller,
    IReadOnlyList<string> WindowsPrerequisites,
    int WindowsSchemaVersion,
    bool Approved);

internal sealed record StudioUx8HDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx8HTargetPlan(
    string Target,
    bool Enabled,
    string Output,
    string Authoring,
    string Optimization,
    string LicenseGate);

internal sealed record StudioUx8HDistributionPreview(
    bool Succeeded,
    bool Changed,
    bool CanonicalDryRun,
    bool CanApply,
    string CandidateFingerprint,
    string Patch,
    string Inverse,
    IReadOnlyList<StudioUx8HTargetPlan> Targets,
    IReadOnlyList<string> ReleasePlan,
    IReadOnlyList<string> SigningPlan,
    IReadOnlyList<string> DeltaPlan,
    IReadOnlyList<string> WindowsPlan,
    IReadOnlyList<StudioUx8HDiagnostic> Diagnostics,
    string Message);

internal sealed record StudioUx8HApprovalSnapshot(
    StudioUx8HDistributionDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioUx8HActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8HArtifactReport(
    bool Succeeded,
    bool Cancelled,
    bool SourceUnchanged,
    bool ResourcesReleased,
    IReadOnlyList<string> Bundles,
    string Message);

internal sealed record StudioUx8HSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool TargetProfiles,
    bool MultiPlatform,
    bool WindowsPackaging,
    bool SignatureRoundTrip,
    bool DeltaRoundTrip,
    bool DeterministicBundles,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StablePolicyBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool ResourcesReleased,
    bool SourceUnchanged,
    string Message);
