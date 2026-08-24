using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTerrainSurfaceWorkspaceUx2C _terrainSurfaceWorkspaceUx2C = null!;
    private Button _terrainSurfaceButtonUx2C = null!;

    private void BuildUx2CActions(HBoxContainer headingRow)
    {
        _terrainSurfaceButtonUx2C = StudioStyle.Button("Relief & propriétés", compact: true);
        _terrainSurfaceButtonUx2C.TooltipText =
            "Sculpter le relief, éditer toutes les features et les propriétés complètes des tuiles.";
        _terrainSurfaceButtonUx2C.Pressed += OpenTerrainSurfaceUx2C;
        headingRow.AddChild(_terrainSurfaceButtonUx2C);
    }

    private void InitializeUx2CWorkspace()
    {
        _terrainSurfaceWorkspaceUx2C = new StudioTerrainSurfaceWorkspaceUx2C(_session);
        _terrainSurfaceWorkspaceUx2C.StatusChanged += SetStatus;
        _terrainSurfaceWorkspaceUx2C.TerrainChanged += RefreshAfterUx2CMutation;
        AddChild(_terrainSurfaceWorkspaceUx2C);
    }

    private void OpenTerrainSurfaceUx2C()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les outils de terrain.", error: true);
            return;
        }
        SwitchPage("terrain", notify: false);
        CloseUx2BWorkspace();
        _terrainSurfaceWorkspaceUx2C.Open();
        SetStatus("Terrain avancé ouvert · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2CMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2CActionState();
    }

    private void UpdateUx2CActionState()
    {
        if (_terrainSurfaceButtonUx2C is null) return;
        _terrainSurfaceButtonUx2C.Visible = PageId == "terrain" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2CWorkspace()
    {
        if (_terrainSurfaceWorkspaceUx2C is not null) _terrainSurfaceWorkspaceUx2C.Close();
    }

    internal bool PrepareUx2CEvidence(string mode)
    {
        SwitchPage("terrain", notify: false);
        CloseUx2BWorkspace();
        return _terrainSurfaceWorkspaceUx2C.PrepareEvidence(mode);
    }

    internal bool RunUx2CSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _terrainSurfaceWorkspaceUx2C.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalApproval = _session.CaptureTerrainSurfaceApprovalUx2C();

        var uiTransaction = _terrainSurfaceWorkspaceUx2C.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2CEvidence("ux2-c-terrain-surface");
        var uiReady = evidencePrepared && PageId == "terrain" && _terrainSurfaceWorkspaceUx2C.IsOpen &&
                      _terrainSurfaceWorkspaceUx2C.HasExplicitSelection &&
                      _terrainSurfaceWorkspaceUx2C.CanonicalPreviewReady &&
                      _terrainSurfaceWorkspaceUx2C.DirectBrushInputExercised &&
                      _terrainSurfaceWorkspaceUx2C.ShowsAllModes &&
                      _terrainSurfaceWorkspaceUx2C.ShowsHumanSurface &&
                      _terrainSurfaceWorkspaceUx2C.UsesIsolatedEvidence;
        _terrainSurfaceWorkspaceUx2C.Close();
        var evidenceReleased = _terrainSurfaceWorkspaceUx2C.EvidenceResourcesReleased;
        var sourceRestored = _terrainSurfaceWorkspaceUx2C.SourceSessionRestored;
        var result = _session.RunTerrainSurfaceContractSmokeUx2C();

        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _terrainSurfaceWorkspaceUx2C.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreTerrainSurfaceApprovalUx2C(originalApproval);
        var history = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, history) &&
                              history?.State.Revision == originalRevision &&
                              (history?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.CaptureTerrainSurfaceApprovalUx2C() == originalApproval;
        var surfaceRestored = PageId == originalPageId &&
                              _terrainSurfaceWorkspaceUx2C.IsOpen == originalSurfaceOpen;
        var valid = result.Succeeded && uiTransaction && uiReady && evidenceReleased && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2C_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"direct_brush={result.DirectBrushInput.ToString().ToLowerInvariant()} " +
            $"sculpt={result.Sculpt.ToString().ToLowerInvariant()} " +
            $"features={result.Feature.ToString().ToLowerInvariant()} " +
            $"tile_property={result.TileProperty.ToString().ToLowerInvariant()} " +
            $"full_collections={result.FullCollections.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_values={result.InvalidValuesBlocked.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_commands={result.CanonicalCommands.ToString().ToLowerInvariant()} " +
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
