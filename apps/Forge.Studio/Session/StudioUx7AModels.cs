using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Studio.Session;

internal enum StudioUx7ASeverity
{
    Information,
    Warning,
    Error,
}

internal sealed record StudioUx7AAreaChoice(AreaId AreaId, string Label);

internal sealed record StudioUx7ACategory(
    string Key,
    string Label,
    IReadOnlyList<string> Routes,
    string Description,
    int IssueCount,
    bool Passed);

internal sealed record StudioUx7AIssue(
    string CategoryKey,
    StudioUx7ASeverity Severity,
    string Code,
    string Message,
    string CanonicalPath,
    string HumanPath,
    CreatorSelection? Target,
    CreatorDiagnostic? Actionable,
    IReadOnlyList<DiagnosticQuickFixDefinition> QuickFixes,
    string Authority);

internal sealed record StudioUx7AReport(
    ProjectRevision Revision,
    AreaId AreaId,
    string Fingerprint,
    IReadOnlyList<StudioUx7ACategory> Categories,
    IReadOnlyList<StudioUx7AIssue> Issues,
    int Errors,
    int Warnings,
    int Information,
    bool ReadOnlyValidation,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx7AApprovalSnapshot(
    DiagnosticId? DiagnosticId,
    DiagnosticQuickFixId? QuickFixId);

internal sealed record StudioUx7ASmokeResult(
    bool Succeeded,
    bool TenRoutes,
    bool CanonicalCategories,
    bool AreaReadiness,
    bool AudioValidation,
    bool LocalizationValidation,
    bool LicenseValidation,
    bool ClickableDiagnostic,
    bool DeepLink,
    bool QuickFixPreview,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryReleased);
