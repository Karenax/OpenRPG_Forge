using Forge.Application.Projects;
using Forge.Domain.Authoring;
using Forge.Domain.Gameplay;

namespace Forge.Studio.Session;

internal sealed record StudioUx6XRuntimeShellDraft(
    bool Enabled,
    bool RequiredAtRuntime,
    string KitId,
    string SkinId,
    string LayoutDocument,
    double Scale,
    bool MapVisible,
    bool JournalVisible,
    bool HudVisible,
    bool InputAgnostic,
    bool Scalable,
    IReadOnlyList<string> InputActions);

internal sealed record StudioUx6XTypographyDraft(
    double UiScale,
    double TextScale,
    string PrimaryFontFamily,
    IReadOnlyList<string> FallbackFontFamilies);

internal sealed record StudioUx6XDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    string SourceWorkspaceFingerprint,
    ModuleGameplayControlProfile SourceGameplay,
    EditorWorkspaceProfile SourceWorkspace,
    StudioUx6XRuntimeShellDraft RuntimeShell,
    StudioUx6XTypographyDraft Typography);

internal sealed record StudioUx6XFontGovernance(
    string Family,
    bool Governed,
    IReadOnlyList<string> ShapingScripts,
    IReadOnlyList<string> Locales,
    IReadOnlyList<int> DpiBuckets,
    decimal MinimumTextScale,
    decimal MaximumTextScale);

internal sealed record StudioUx6XDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6XPreview(
    bool Succeeded,
    bool CanApply,
    bool ProjectDryRun,
    bool WorkspaceDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6XDiagnostic> Diagnostics,
    IReadOnlyList<string> Results,
    bool RuntimeShellResolved,
    bool TypographyResolved,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6XActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6XApprovalSnapshot(
    StudioUx6XDraft? Draft,
    ProjectRevision? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6XSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool InvalidConfigurationBlocked,
    bool ProjectDryRun,
    bool WorkspaceDryRun,
    bool RuntimeShellResolved,
    bool TypographyResolved,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalProjectCommand,
    bool CanonicalWorkspaceCommand,
    bool UndoRedo,
    bool Persistence,
    bool GameplayNeighborsPreserved,
    bool WorkspaceNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
