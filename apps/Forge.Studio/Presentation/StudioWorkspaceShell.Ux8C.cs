using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBuildCollaborationWorkbenchUx8C _buildCollaborationUx8C = null!;
    private Button _buildCollaborationButtonUx8C = null!;

    private void BuildUx8CActions(HBoxContainer headingRow)
    {
        _buildCollaborationButtonUx8C = StudioStyle.Button("Collaborer et gouverner", compact: true);
        _buildCollaborationButtonUx8C.TooltipText =
            "Merge trois voies, worktrees, CI et cinq fixtures de référence.";
        _buildCollaborationButtonUx8C.Pressed += OpenBuildCollaborationUx8C;
        headingRow.AddChild(_buildCollaborationButtonUx8C);
    }

    private void InitializeUx8COverlay()
    {
        _buildCollaborationUx8C = new StudioBuildCollaborationWorkbenchUx8C(_session);
        _buildCollaborationUx8C.StatusChanged += SetStatus;
        AddChild(_buildCollaborationUx8C);
    }

    private void OpenBuildCollaborationUx8C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant sa gouvernance.", error: true);
            return;
        }
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        _buildCollaborationUx8C.Open();
        SetStatus("UX8-C · merge en mémoire et gouvernance transactionnelle, sans agent ni CI distante.");
    }

    private void UpdateUx8CActionState()
    {
        if (_buildCollaborationButtonUx8C is not null)
            _buildCollaborationButtonUx8C.Visible = PageId == "build" && _session.Project is not null;
    }

    private void CloseUx8COverlay()
    {
        if (_buildCollaborationUx8C is not null) _buildCollaborationUx8C.Close();
    }

    internal bool PrepareUx8CEvidence(string mode)
    {
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        return _buildCollaborationUx8C.PrepareEvidence(mode);
    }

    internal bool RunUx8CSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _buildCollaborationUx8C.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureGovernanceApprovalUx8C();

        var prepared = PrepareUx8CEvidence("ux8-c-build-governance");
        var transaction = prepared && _buildCollaborationUx8C.RunUiTransactionEvidence();
        var mergeReady = _buildCollaborationUx8C.MergeReady;
        var governanceReady = _buildCollaborationUx8C.GovernanceReady;
        var human = _buildCollaborationUx8C.ShowsHumanSurface;
        var sourceEvidence = _buildCollaborationUx8C.EvidenceSourceUntouched;
        var selectionReady = _buildCollaborationUx8C.HasExplicitSelection;
        var uiReady = prepared && transaction && _buildCollaborationUx8C.IsOpen &&
                      selectionReady && mergeReady && governanceReady &&
                      _buildCollaborationUx8C.TransactionReady && human && sourceEvidence;
        _buildCollaborationUx8C.CloseEvidence();
        var evidenceReleased = _buildCollaborationUx8C.EvidenceResourcesReleased;
        var contract = _session.RunUx8CContractSmoke();
        var contractReleased = _session.LastUx8CTemporaryReleased;
        _session.RestoreGovernanceApprovalUx8C(originalApproval);

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _buildCollaborationUx8C.RestoreSourceSurfaceAfterSmoke();
        else _buildCollaborationUx8C.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureGovernanceApprovalUx8C() == originalApproval;
        var surfaceRestored = PageId == originalPage && _buildCollaborationUx8C.IsOpen == originalOpen;
        var released = evidenceReleased && contractReleased;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored && released;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_C_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selectionReady.ToString().ToLowerInvariant()} " +
                 $"merge_conflict={contract.MergeConflict.ToString().ToLowerInvariant()} " +
                 $"preserve_both={contract.PreserveBoth.ToString().ToLowerInvariant()} " +
                 $"worktrees={contract.WorktreePolicy.ToString().ToLowerInvariant()} " +
                 $"ci={contract.CiPolicy.ToString().ToLowerInvariant()} " +
                 $"fixtures={contract.FixtureCatalog.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_guard={contract.StaleGuard.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={released.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8CAutomatedEvidence()
    {
        if (_buildCollaborationUx8C is not null) _buildCollaborationUx8C.CloseEvidence();
    }
}
