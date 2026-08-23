using Forge.Application.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Studio.Session;

internal sealed record StudioUx6TProfileChoice(string Label);

internal sealed record StudioUx6TDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    IReadOnlyList<AreaEffectShape> Shapes,
    float MaxRange,
    float MaxHeightDelta,
    bool EvaluateCover,
    bool RequireServerConfirmation,
    TargetIndicatorColorMode ColorMode,
    string ValidColor,
    string InvalidColor,
    string ToolkitVersion,
    bool ResponsiveLayout,
    bool Forms,
    bool Lists,
    bool Canvas,
    bool Accessibility,
    bool Localization,
    bool DeveloperInspector,
    AreaEffectShape PreviewShape,
    float PreviewRange,
    float PreviewRadius,
    float PreviewWidth,
    float PreviewAngleDegrees,
    bool PreviewServerConfirmed);

internal sealed record StudioUx6TDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6TPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6TDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool AreaEffectResolved,
    bool NuiResolved,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6TActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6TApprovalSnapshot(
    StudioUx6TDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6TSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool ShapeValidation,
    bool RangeValidation,
    bool ColorValidation,
    bool ToolkitValidation,
    bool CanonicalDryRun,
    bool AreaEffectResolved,
    bool NuiResolved,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool ModernNeighborsPreserved,
    bool ExtensionNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
