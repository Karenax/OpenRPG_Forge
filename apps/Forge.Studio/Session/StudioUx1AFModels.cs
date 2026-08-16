using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx1AFChoice(Guid Id, string DisplayName);

internal sealed record StudioUx1AFPlanChoice(
    Guid Id,
    string DisplayName,
    bool Approved,
    bool AlreadyBuilt,
    string ProviderName,
    decimal EstimatedCostUsd);

internal sealed record StudioUx1AFWorkspaceSnapshot(
    IReadOnlyList<StudioUx1AFChoice> Areas,
    IReadOnlyList<StudioUx1AFChoice> Kits,
    IReadOnlyList<StudioUx1AFPlanChoice> Plans,
    string ProviderName,
    string ProviderKind,
    string ProviderProvenance,
    AssistantPermissionLevel MaximumPermission,
    bool NetworkAllowed,
    bool LocalOnly,
    bool ShareProjectContent,
    int LoreEntryCount,
    bool Ready,
    string Message);

internal sealed record StudioUx1AFPlanDraft(
    Guid AreaId,
    Guid KitBlueprintId,
    string Title,
    AssistedAreaArchetype Archetype,
    int Seed,
    float WidthMeters,
    float DepthMeters,
    int NodeCount,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Objectives,
    bool ApproveForConstruction);

internal sealed record StudioUx1AFPlanPreview(
    bool Succeeded,
    string Message,
    string Summary,
    AssistedAreaPlanDefinition? Plan,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderProvenance,
    bool GeometryApplied,
    bool NetworkAuthorized,
    bool ProjectContentAuthorized,
    bool CanApply);

internal sealed record StudioUx1AFBuildPreview(
    bool Succeeded,
    string Message,
    string Summary,
    Guid PlanId,
    int PlacementCount,
    int NavigationDiagnosticsBefore,
    int NavigationDiagnosticsAfter,
    bool LightingPass,
    bool GeometryApplied,
    bool CanApply);

internal sealed record StudioUx1AFActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AFSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId);

internal sealed record StudioUx1AFSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool PrivacyVisible,
    bool ProvenanceVisible,
    bool PlanDryRunPure,
    bool PlanApprovalPersisted,
    bool PlanHistorySeparated,
    bool PlanUndoRedo,
    bool PlanRevisionGuard,
    bool AsyncStaleBlocked,
    bool MixedCatalogPermission,
    bool ProjectRevisionGuard,
    bool BuildDryRunPure,
    bool BuildAppliedGeometry,
    bool BuildHistorySeparated,
    bool BuildUndoRedo,
    bool BuildRevisionGuard,
    bool SaveReopen,
    bool SourceUntouched,
    bool ResourcesReleased,
    string Message);

internal sealed class StudioUx1AFEvidenceWorkspace : IDisposable
{
    private const string DirectoryPrefix = "openrpg-forge-studio-ux1af-evidence-";
    private bool _disposed;

    internal StudioSessionController Session { get; }

    internal string RootPath { get; }

    internal bool ResourcesReleased { get; private set; }

    internal StudioUx1AFEvidenceWorkspace(StudioSessionController session, string rootPath)
    {
        Session = session;
        RootPath = rootPath;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var released = true;
        try
        {
            Session.CloseUx1AF();
            Session.Close();
        }
        catch (Exception exception)
        {
            released = false;
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_SESSION_CLOSE_FAILED type={exception.GetType().Name}");
        }

        try
        {
            var root = Path.GetFullPath(RootPath);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
            var directoryName = Path.GetFileName(root);
            if (root.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(directoryName) &&
                directoryName.StartsWith(DirectoryPrefix, StringComparison.Ordinal) &&
                Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            released &= !Directory.Exists(root);
        }
        catch (Exception exception)
        {
            released = false;
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AF_EVIDENCE_CLEANUP_FAILED type={exception.GetType().Name}");
        }

        ResourcesReleased = released;
    }

    internal static string CreateRootPath() => Path.Combine(
        Path.GetTempPath(),
        DirectoryPrefix + Guid.NewGuid().ToString("N"));
}
