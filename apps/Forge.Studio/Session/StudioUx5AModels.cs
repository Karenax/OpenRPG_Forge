using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioDialogueChoiceUx5A(
    DialogueId Id,
    string Label,
    int LineCount,
    int ChoiceCount);

internal sealed record StudioDialogueSpeakerChoiceUx5A(
    BlueprintId? Id,
    string Label);

internal sealed record StudioDialogueChoiceDraftUx5A(
    DialogueChoice Original,
    string Text,
    DialogueNodeId? TargetNodeId,
    DialogueEdgeKind EdgeKind,
    string PriorityText);

internal sealed record StudioDialogueLineDraftUx5A(
    DialogueNode Original,
    string Text,
    DialogueNodeRole Role,
    BlueprintId? SpeakerBlueprintId,
    Guid ParticipantId,
    IReadOnlyList<StudioDialogueChoiceDraftUx5A> Choices);

internal sealed record StudioDialogueDraftUx5A(
    DialogueId DialogueId,
    string DisplayName,
    string Slug,
    DialogueNodeId EntryNodeId,
    IReadOnlyList<StudioDialogueLineDraftUx5A> Lines,
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    DialogueDefinition Source,
    bool IsNew);

internal sealed record StudioDialogueDiagnosticUx5A(
    string Code,
    string Section,
    string Message);

internal sealed record StudioDialoguePreviewUx5A(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    int LineCount,
    int ChoiceCount,
    int ReferenceCount,
    IReadOnlyList<StudioDialogueDiagnosticUx5A> Diagnostics);

internal sealed record StudioDialogueActionResultUx5A(
    bool Succeeded,
    string Message);

internal sealed record StudioDialogueApprovalSnapshotUx5A(
    DialogueDefinition? Candidate,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioDialogueEvidenceSourceSnapshotUx5A(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioDialogueApprovalSnapshotUx5A Approval);

internal sealed record StudioUx5ASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool BranchesUnlimited,
    bool MultipleEntries,
    bool ReferenceCycleAccepted,
    bool FlowCycleBlocked,
    bool MissingTargetBlocked,
    bool NegativePriorityBlocked,
    bool DryRunPure,
    bool StaleRevisionBlocked,
    bool FingerprintGuard,
    bool CanonicalCommand,
    bool EdgesCanonical,
    bool ReorderDiffHuman,
    bool UndoRedo,
    bool RoundTrip,
    bool StructuralPreservation,
    bool SourceUntouched,
    bool ResourcesReleased,
    string Message);

internal sealed class StudioDialogueEvidenceWorkspaceUx5A : IDisposable
{
    private const string DirectoryPrefix = "openrpg-forge-studio-ux5a-evidence-";
    private bool _disposed;

    internal StudioSessionController Session { get; }

    internal string RootPath { get; }

    internal bool ResourcesReleased { get; private set; }

    internal StudioDialogueEvidenceWorkspaceUx5A(
        StudioSessionController session,
        string rootPath)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
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
            Session.CloseUx5A();
            Session.Close();
        }
        catch (Exception exception)
        {
            released = false;
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_SESSION_CLOSE_FAILED type={exception.GetType().Name}");
        }

        try
        {
            var root = Path.GetFullPath(RootPath);
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
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
                $"OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_CLEANUP_FAILED type={exception.GetType().Name}");
        }

        ResourcesReleased = released;
    }

    internal static string CreateRootPath() => Path.Combine(
        Path.GetTempPath(),
        DirectoryPrefix + Guid.NewGuid().ToString("N"));
}
