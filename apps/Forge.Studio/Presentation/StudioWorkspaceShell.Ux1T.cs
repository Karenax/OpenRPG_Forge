using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _containerEventsPlaceableButton = null!;

    private void BuildUx1TActions(HBoxContainer headingRow)
    {
        _containerEventsPlaceableButton = StudioStyle.Button("Ouverture / fermeture", compact: true);
        _containerEventsPlaceableButton.TooltipText =
            "Nommer les événements d’ouverture et de fermeture, leur contexte et leur exigence runtime.";
        _containerEventsPlaceableButton.Pressed += OpenContainerEventsPlaceable;
        headingRow.AddChild(_containerEventsPlaceableButton);
    }

    private void OpenContainerEventsPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, containerEventsFocus: true);

    private void UpdateUx1TActionState()
    {
        if (_containerEventsPlaceableButton is null)
        {
            return;
        }
        _containerEventsPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1TEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1TSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunContainerEventsUiTransactionEvidence();
        var uiReady = PrepareUx1TEvidence("ux1-t-close") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.ContainerEventsPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1TContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1T_{(valid ? "PASS" : "FAILED")} " +
            $"bindings_api={result.BindingsAndApi.ToString().ToLowerInvariant()} " +
            $"canonical_payload={result.CanonicalPayload.ToString().ToLowerInvariant()} " +
            $"open_close_preview={result.OpenClosePreview.ToString().ToLowerInvariant()} " +
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
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
