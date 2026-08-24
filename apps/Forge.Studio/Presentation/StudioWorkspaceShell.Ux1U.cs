using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _lockEventsPlaceableButton = null!;

    private void BuildUx1UActions(HBoxContainer headingRow)
    {
        _lockEventsPlaceableButton = StudioStyle.Button("Verrouiller / déverrouiller", compact: true);
        _lockEventsPlaceableButton.TooltipText =
            "Nommer les événements avant et après, leur contexte, leur veto et leur exigence runtime.";
        _lockEventsPlaceableButton.Pressed += OpenLockEventsPlaceable;
        headingRow.AddChild(_lockEventsPlaceableButton);
    }

    private void OpenLockEventsPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, lockEventsFocus: true);

    private void UpdateUx1UActionState()
    {
        if (_lockEventsPlaceableButton is null)
        {
            return;
        }
        _lockEventsPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1UEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1USmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunLockEventsUiTransactionEvidence();
        var uiReady = PrepareUx1UEvidence("ux1-u-veto") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.LockEventsPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1UContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1U_{(valid ? "PASS" : "FAILED")} " +
            $"bindings_api={result.BindingsAndApi.ToString().ToLowerInvariant()} " +
            $"canonical_payload={result.CanonicalPayload.ToString().ToLowerInvariant()} " +
            $"before_after_preview={result.BeforeAfterPreview.ToString().ToLowerInvariant()} " +
            $"controlled_veto={result.ControlledVeto.ToString().ToLowerInvariant()} " +
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
