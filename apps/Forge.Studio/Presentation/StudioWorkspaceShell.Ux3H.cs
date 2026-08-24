using Forge.ModuleFormat.Serialization;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioContentDataWorkbenchUx3H _contentDataUx3H = null!;
    private Button _contentDataButtonUx3H = null!;

    private void BuildUx3HActions(HBoxContainer headingRow)
    {
        _contentDataButtonUx3H = StudioStyle.Button("Données & transferts", compact: true);
        _contentDataButtonUx3H.TooltipText =
            "Importer des fichiers ouverts, exporter des blueprints et éditer sources textuelles et tables typées.";
        _contentDataButtonUx3H.Pressed += OpenContentDataUx3H;
        headingRow.AddChild(_contentDataButtonUx3H);
    }

    private void InitializeUx3HOverlay()
    {
        _contentDataUx3H = new StudioContentDataWorkbenchUx3H(_session);
        _contentDataUx3H.StatusChanged += SetStatus;
        _contentDataUx3H.WorkspaceChanged += RefreshAfterUx3HMutation;
        AddChild(_contentDataUx3H);
    }

    private void OpenContentDataUx3H()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses données et transferts.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3IOverlay();
        _contentDataUx3H.Open();
        SetStatus("UX3-H · import de masse, export de blueprints, sources textuelles et tables typées.");
    }

    private void RefreshAfterUx3HMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx3HActionState();
    }

    private void UpdateUx3HActionState()
    {
        if (_contentDataButtonUx3H is not null)
            _contentDataButtonUx3H.Visible = PageId is "blueprints" or "assets" && _session.Project is not null;
    }

    private void CloseUx3HOverlay()
    {
        if (_contentDataUx3H is not null) _contentDataUx3H.Close();
    }

    internal bool PrepareUx3HEvidence(string mode)
    {
        SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3IOverlay();
        _contentDataUx3H.Open();
        return _contentDataUx3H.PrepareEvidence(mode);
    }

    internal void ReleaseUx3HAutomatedEvidence() => _contentDataUx3H.CloseEvidence();

    internal bool RunUx3HSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _contentDataUx3H.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalWorkspace = EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile);
        var approval = _session.CaptureContentApprovalUx3H();

        var uiTransaction = _contentDataUx3H.RunUiTransactionEvidence();
        var prepared = PrepareUx3HEvidence("ux3-h-content-data");
        var uiReady = prepared && _contentDataUx3H.IsOpen &&
                      _contentDataUx3H.UsesIsolatedEvidence &&
                      (_contentDataUx3H.UsesRealModuleEvidence ^
                       _contentDataUx3H.UsesCanonicalFallbackEvidence) &&
                      _contentDataUx3H.HasExplicitSelections &&
                      _contentDataUx3H.ContentPreviewReady &&
                      _contentDataUx3H.BatchPreviewReady &&
                      _contentDataUx3H.ExportPreviewReady &&
                      _contentDataUx3H.ShowsHumanSurface &&
                      _contentDataUx3H.EvidenceSourceUntouched;
        _contentDataUx3H.CloseEvidence();
        var released = _contentDataUx3H.EvidenceResourcesReleased;
        var sourceRestored = _contentDataUx3H.SourceSessionRestored;
        var contract = _session.RunUx3HContractSmoke();
        var contractReleased = _session.LastUx3HTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _contentDataUx3H.RestoreSourceSurfaceAfterSmoke();
        else _contentDataUx3H.HideSourceSurfaceAfterSmoke();
        _session.RestoreContentApprovalUx3H(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile) == originalWorkspace &&
                              _session.CaptureContentApprovalUx3H() == approval;
        var surfaceRestored = PageId == originalPage && _contentDataUx3H.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && contractReleased &&
                    sourceRestored && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_H_{(valid ? "PASS" : "FAILED")} " +
                 "routes=CONT-004,CONT-005,CONT-006,CONT-018 " +
                 $"selection_explicit={contract.ExplicitSelections.ToString().ToLowerInvariant()} " +
                 $"batch_configuration={contract.BatchConfiguration.ToString().ToLowerInvariant()} " +
                 $"batch_dry_run={contract.BatchDryRun.ToString().ToLowerInvariant()} " +
                 $"batch_apply_undo_redo={contract.BatchApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"blueprint_export={contract.BlueprintExport.ToString().ToLowerInvariant()} " +
                 $"blueprint_export_undo_redo={contract.BlueprintExportUndoRedo.ToString().ToLowerInvariant()} " +
                 $"text_sources={contract.TextSources.ToString().ToLowerInvariant()} " +
                 $"data_tables={contract.DataTables.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbor={contract.NeighborPreserved.ToString().ToLowerInvariant()} " +
                 $"project_source={contract.ProjectSourcePreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
