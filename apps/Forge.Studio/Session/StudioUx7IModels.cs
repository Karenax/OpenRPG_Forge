using Forge.Application.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;

namespace Forge.Studio.Session;

internal sealed record StudioUx7IProfileChoice(string Label, bool Enabled);

internal sealed record StudioUx7IDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool Enabled,
    bool TracyCompatible,
    bool OpenTelemetryCompatible,
    int MaximumCaptureSeconds,
    bool IncludeScripts,
    ObservabilityCaptureBackend Backend,
    int RequestedCaptureSeconds);

internal sealed record StudioUx7IDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx7IDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx7IPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    StudioUx7IDiff Diff,
    IReadOnlyList<StudioUx7IDiagnostic> Diagnostics,
    string BackendStatus,
    string Summary);

internal sealed record StudioUx7IActionResult(bool Succeeded, string Message);

internal sealed record StudioUx7IApprovalSnapshot(
    StudioUx7IDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx7ICaptureResult(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool ArtifactConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    ObservabilityCaptureBackend Backend,
    int SpanCount,
    IReadOnlyList<QualityTraceSurface> Surfaces,
    IReadOnlyList<string> Categories,
    string Fingerprint,
    string ArtifactLabel,
    IReadOnlyList<StudioUx7IDiagnostic> Diagnostics,
    string Message);

internal sealed record StudioUx7ISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CanonicalCommand,
    bool InvalidBlocked,
    bool StaleBlocked,
    bool UndoRedo,
    bool RoundTrip,
    bool Preservation,
    bool OpenTelemetryCapture,
    bool NativeTracyDiagnostic,
    bool Cancellation,
    bool ResultNavigation,
    bool Isolated,
    bool ArtifactConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
