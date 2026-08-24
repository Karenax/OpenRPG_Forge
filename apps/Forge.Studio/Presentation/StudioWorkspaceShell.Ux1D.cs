using Forge.Domain.Entities;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaLayersOverlay _areaLayersOverlay = null!;
    private Button _areaLayersButton = null!;

    private void BuildUx1DActions(HBoxContainer headingRow)
    {
        _areaLayersButton = StudioStyle.Button("Calques", compact: true);
        _areaLayersButton.TooltipText =
            "Organiser les objets par groupes, visibilité, verrouillage, responsable et export partiel.";
        _areaLayersButton.Pressed += OpenAreaLayers;
        headingRow.AddChild(_areaLayersButton);
    }

    private void InitializeUx1DOverlay()
    {
        _areaLayersOverlay = new StudioAreaLayersOverlay(_session);
        _areaLayersOverlay.StatusChanged += SetStatus;
        _areaLayersOverlay.AreaChanged += RefreshAfterUx1DMutation;
        _areaLayersOverlay.GroupSelectionRequested += SelectLayerGroupInViewport;
        AddChild(_areaLayersOverlay);
    }

    private void OpenAreaLayers()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant d’organiser ses calques.", error: true);
            return;
        }

        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaCreationOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _areaLoadingOverlay.Close();
        _placeableBlueprintOverlay.Close();
        SwitchPage("zones", notify: false);
        _areaLayersOverlay.Open();
        SetStatus("Calques ouverts · glisser-déposer, aperçu et sélection de groupe disponibles");
    }

    private void SelectLayerGroupInViewport(IReadOnlyList<EntityId> entityIds, EntityId? activeEntityId)
    {
        SelectEntities(entityIds, activeEntityId);
    }

    private void RefreshAfterUx1DMutation()
    {
        RefreshAfterUx1CMutation();
        UpdateUx1DActionState();
    }

    private void UpdateUx1DActionState()
    {
        if (_areaLayersButton is null)
        {
            return;
        }
        _areaLayersButton.Visible = PageId == "zones" && _session.ActiveArea is not null;
    }

    internal bool PrepareUx1DEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        return _areaLayersOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1DSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _areaLayersOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1DEvidence("ux1-d-tree") &&
                      PageId == "zones" &&
                      _areaLayersOverlay.IsOpen &&
                      _areaLayersOverlay.LayerCount >= 3 &&
                      _areaLayersOverlay.DirectManipulationAvailable &&
                      _areaLayersOverlay.ShowsHumanSurface;
        _areaLayersOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1DContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1D_{(valid ? "PASS" : "FAILED")} " +
            $"preview={result.Preview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"hierarchy={result.NestedHierarchy.ToString().ToLowerInvariant()} " +
            $"visibility_lock={result.VisibilityAndLocking.ToString().ToLowerInvariant()} " +
            $"ownership={result.Ownership.ToString().ToLowerInvariant()} " +
            $"group_selection={result.GroupSelection.ToString().ToLowerInvariant()} " +
            $"partial_export={result.PartialExport.ToString().ToLowerInvariant()} " +
            $"drag_reparent={result.DirectReparent.ToString().ToLowerInvariant()} " +
            $"entity_assignment={result.EntityAssignment.ToString().ToLowerInvariant()} " +
            $"other_authoring_preserved={result.OtherAuthoringPreserved.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"runtime={result.RuntimeSemantics.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
