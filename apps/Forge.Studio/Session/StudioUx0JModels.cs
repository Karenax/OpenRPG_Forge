using Forge.Application.Creator;

namespace Forge.Studio.Session;

internal sealed record StudioSafeRenameDraft(
    CreatorSearchDocument Document,
    string KindLabel,
    string CurrentDisplayName,
    string CurrentAddress,
    int ReferenceCount,
    bool IsWorkspaceResource,
    bool HasEditableDisplayName,
    bool HasEditableAddress);

internal sealed record StudioSafeRenamePreviewResult(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply);

internal sealed record StudioDiagnosticCorrelation(string? Id, string Label);

internal sealed record StudioStatusMessage(string Message, bool Error, string CorrelationLabel);

internal sealed record StudioDiagnosticOverview(
    IReadOnlyList<CreatorDiagnostic> Diagnostics,
    int Errors,
    int Warnings,
    int Information,
    IReadOnlyList<StudioDiagnosticCorrelation> Correlations,
    IReadOnlyList<StudioStatusMessage> RecentMessages);

internal sealed record StudioDiagnosticFixPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply);

internal sealed record StudioDiagnosticBundleReport(
    bool Succeeded,
    string Message,
    string RelativePath,
    int DiagnosticCount,
    bool IncludesSessionLog,
    bool Reused);

internal sealed record StudioUx0JSmokeResult(
    bool Succeeded,
    bool ContentRename,
    bool ResourceRename,
    bool StableIdentity,
    bool ReferencesMigrated,
    bool DryRun,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool InvalidRenameBlocked,
    bool Diagnostics,
    bool DeepLink,
    bool QuickFixPreview,
    bool QuickFixUndo,
    bool DiagnosticExport,
    bool HumanDiagnostics);
