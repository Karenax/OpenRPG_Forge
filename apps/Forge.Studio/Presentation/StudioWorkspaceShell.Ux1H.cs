using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _containerPlaceableButton = null!;

    private void BuildUx1HActions(HBoxContainer headingRow)
    {
        _containerPlaceableButton = StudioStyle.Button("Conteneur", compact: true);
        _containerPlaceableButton.TooltipText =
            "Configurer capacité, masse, propriétaire, accès, sauvegarde, butin, interface et réplication.";
        _containerPlaceableButton.Pressed += OpenContainerPlaceable;
        headingRow.AddChild(_containerPlaceableButton);
    }

    private void OpenContainerPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, interactionFocus: false, containerFocus: true);

    private void UpdateUx1HActionState()
    {
        if (_containerPlaceableButton is null)
        {
            return;
        }
        _containerPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1HEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1HSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunContainerUiTransactionEvidence();
        var uiReady = PrepareUx1HEvidence("ux1-h-capacity") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.ContainerEnabled &&
                      _placeableBlueprintOverlay.ContainerPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1HContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1H_{(valid ? "PASS" : "FAILED")} " +
            $"container_component={result.ContainerComponent.ToString().ToLowerInvariant()} " +
            $"capacity={result.Capacity.ToString().ToLowerInvariant()} " +
            $"ownership_access={result.OwnershipAccess.ToString().ToLowerInvariant()} " +
            $"persistence_replication={result.PersistenceReplication.ToString().ToLowerInvariant()} " +
            $"references={result.References.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
            $"script_read={result.ScriptRead.ToString().ToLowerInvariant()} " +
            $"optional_required={result.OptionalRequired.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
