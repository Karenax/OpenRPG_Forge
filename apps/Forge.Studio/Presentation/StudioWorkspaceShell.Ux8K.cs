using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBulkTransferWorkbenchUx8K _bulkTransferUx8K = null!;
    private Button _bulkTransferButtonUx8K = null!;

    private void BuildUx8KActions(HBoxContainer headingRow)
    {
        _bulkTransferButtonUx8K = StudioStyle.Button("Transferts de masse", compact: true);
        _bulkTransferButtonUx8K.TooltipText =
            "Importer ou exporter un lot filtré, gouverné, prévisualisé et annulable.";
        _bulkTransferButtonUx8K.Pressed += OpenBulkTransferUx8K;
        headingRow.AddChild(_bulkTransferButtonUx8K);
    }

    private void InitializeUx8KOverlay()
    {
        _bulkTransferUx8K = new StudioBulkTransferWorkbenchUx8K(_session);
        _bulkTransferUx8K.StatusChanged += SetStatus;
        _bulkTransferUx8K.WorkspaceChanged += RefreshAfterUx8KMutation;
        AddChild(_bulkTransferUx8K);
    }

    private void OpenBulkTransferUx8K()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de transférer ses fichiers.", error: true);
            return;
        }
        if (PageId != "export") SwitchPage("export", notify: false);
        CloseUx8HOverlay();
        _bulkTransferUx8K.Open();
        SetStatus("UX8-K · import/export de masse confiné, gouverné et annulable.");
    }

    private void RefreshAfterUx8KMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx8KActionState();
    }

    private void UpdateUx8KActionState()
    {
        if (_bulkTransferButtonUx8K is not null)
            _bulkTransferButtonUx8K.Visible = PageId == "export" && _session.Project is not null;
    }

    private void CloseUx8KOverlay()
    {
        if (_bulkTransferUx8K is not null) _bulkTransferUx8K.Close();
    }

    internal bool PrepareUx8KEvidence(string mode)
    {
        if (PageId != "export") SwitchPage("export", notify: false);
        CloseUx8HOverlay();
        return _bulkTransferUx8K.PrepareEvidence(mode);
    }

    internal bool RunUx8KSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _bulkTransferUx8K.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var approval = _session.CaptureBulkApprovalUx8K();
        var contentApproval = _session.CaptureContentApprovalUx3H();

        var prepared = PrepareUx8KEvidence("ux8-k-bulk-transfer");
        var selection = prepared && _bulkTransferUx8K.HasExplicitSelection;
        var human = prepared && _bulkTransferUx8K.ShowsHumanSurface;
        var preview = prepared && _bulkTransferUx8K.PreviewReady;
        var sourceUntouched = prepared && _bulkTransferUx8K.EvidenceSourceUntouched;
        var uiEvidence = prepared && _bulkTransferUx8K.RunUiEvidence();
        _bulkTransferUx8K.CloseEvidence();
        var released = _bulkTransferUx8K.EvidenceResourcesReleased;
        var sourceRestored = _bulkTransferUx8K.SourceSessionRestored;
        var contract = _session.RunUx8KContractSmoke();
        var contractReleased = _session.LastUx8KTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _bulkTransferUx8K.RestoreSourceSurfaceAfterSmoke();
        else _bulkTransferUx8K.HideSourceSurfaceAfterSmoke();
        _session.RestoreBulkApprovalUx8K(approval);
        _session.RestoreContentApprovalUx3H(contentApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureBulkApprovalUx8K() == approval &&
                              _session.CaptureContentApprovalUx3H() == contentApproval;
        var surfaceRestored = PageId == originalPage && _bulkTransferUx8K.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && contractReleased && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_K_{(valid ? "PASS" : "FAILED")} " +
                 $"route={contract.RouteCovered.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"consent={contract.ConsentRequired.ToString().ToLowerInvariant()} " +
                 $"import={contract.ImportPlanned.ToString().ToLowerInvariant()} " +
                 $"export={contract.ExportPlanned.ToString().ToLowerInvariant()} " +
                 $"collision={contract.CollisionPreserved.ToString().ToLowerInvariant()} " +
                 $"scope={contract.ScopeConfined.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows_count=1");
        return valid;
    }

    internal void ReleaseUx8KAutomatedEvidence()
    {
        if (_bulkTransferUx8K is not null) _bulkTransferUx8K.CloseEvidence();
    }
}
