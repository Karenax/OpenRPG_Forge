using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTerrainTilesWorkspaceUx2B _terrainTilesWorkspaceUx2B = null!;
    private Button _terrainTilesButtonUx2B = null!;

    private void BuildUx2BActions(HBoxContainer headingRow)
    {
        _terrainTilesButtonUx2B = StudioStyle.Button("Tuiles & raccords", compact: true);
        _terrainTilesButtonUx2B.TooltipText =
            "Construire sur une grille, grouper, remplacer, orienter et ajuster les tuiles sans identifiants techniques.";
        _terrainTilesButtonUx2B.Pressed += OpenTerrainTilesUx2B;
        headingRow.AddChild(_terrainTilesButtonUx2B);
    }

    private void InitializeUx2BWorkspace()
    {
        _terrainTilesWorkspaceUx2B = new StudioTerrainTilesWorkspaceUx2B(_session);
        _terrainTilesWorkspaceUx2B.StatusChanged += SetStatus;
        _terrainTilesWorkspaceUx2B.TerrainChanged += RefreshAfterUx2BMutation;
        AddChild(_terrainTilesWorkspaceUx2B);
    }

    private void OpenTerrainTilesUx2B()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir la construction modulaire.", error: true);
            return;
        }
        SwitchPage("terrain", notify: false);
        _terrainTilesWorkspaceUx2B.Open();
        SetStatus("Construction modulaire ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2BMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2BActionState();
    }

    private void UpdateUx2BActionState()
    {
        if (_terrainTilesButtonUx2B is null) return;
        _terrainTilesButtonUx2B.Visible = PageId == "terrain" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2BWorkspace()
    {
        if (_terrainTilesWorkspaceUx2B is not null) _terrainTilesWorkspaceUx2B.Close();
    }

    internal bool PrepareUx2BEvidence(string mode)
    {
        SwitchPage("terrain", notify: false);
        return _terrainTilesWorkspaceUx2B.PrepareEvidence(mode);
    }

    internal bool RunUx2BSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _terrainTilesWorkspaceUx2B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalApproval = _session.CaptureTerrainApprovalUx2B();

        var uiTransaction = _terrainTilesWorkspaceUx2B.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2BEvidence("ux2-b-terrain-tiles");
        var uiReady = evidencePrepared && PageId == "terrain" && _terrainTilesWorkspaceUx2B.IsOpen &&
                      _terrainTilesWorkspaceUx2B.HasExplicitSelection &&
                      _terrainTilesWorkspaceUx2B.CanonicalPreviewReady &&
                      _terrainTilesWorkspaceUx2B.ShowsCompleteKit &&
                      _terrainTilesWorkspaceUx2B.ShowsDirectGrid &&
                      _terrainTilesWorkspaceUx2B.DirectGridInputExercised &&
                      _terrainTilesWorkspaceUx2B.ShowsHumanSurface &&
                      _terrainTilesWorkspaceUx2B.UsesIsolatedEvidence;
        _terrainTilesWorkspaceUx2B.Close();
        var evidenceReleased = _terrainTilesWorkspaceUx2B.EvidenceResourcesReleased;
        var sourceRestored = _terrainTilesWorkspaceUx2B.SourceSessionRestored;
        var result = _session.RunTerrainContractSmokeUx2B();

        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _terrainTilesWorkspaceUx2B.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreTerrainApprovalUx2B(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.CaptureTerrainApprovalUx2B() == originalApproval;
        var surfaceRestored = PageId == originalPageId &&
                              _terrainTilesWorkspaceUx2B.IsOpen == originalSurfaceOpen;
        var valid = result.Succeeded && uiTransaction && uiReady && evidenceReleased && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2B_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"complete_kit={result.CompleteKit.ToString().ToLowerInvariant()} " +
            $"direct_grid={result.DirectGridInput.ToString().ToLowerInvariant()} " +
            $"placement={result.Placement.ToString().ToLowerInvariant()} " +
            $"group={result.Group.ToString().ToLowerInvariant()} " +
            $"erase_replace={result.EraseReplace.ToString().ToLowerInvariant()} " +
            $"transform={result.Transform.ToString().ToLowerInvariant()} " +
            $"autofit={result.AutoFit.ToString().ToLowerInvariant()} " +
            $"invalid_values={result.InvalidValuesBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"neighbors={result.NeighboursPreserved.ToString().ToLowerInvariant()} " +
            $"human={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_restored={sourceRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
