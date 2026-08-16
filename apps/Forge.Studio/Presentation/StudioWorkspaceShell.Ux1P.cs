using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _lockPlaceableButton = null!;

    private void BuildUx1PActions(HBoxContainer headingRow)
    {
        _lockPlaceableButton = StudioStyle.Button("Verrouillage", compact: true);
        _lockPlaceableButton.TooltipText =
            "Configurer état, clés, compétence, code, piratage, droits, reverrouillage et brouillage.";
        _lockPlaceableButton.Pressed += OpenLockPlaceable;
        headingRow.AddChild(_lockPlaceableButton);
    }

    private void OpenLockPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, lockFocus: true);

    private void UpdateUx1PActionState()
    {
        if (_lockPlaceableButton is null)
        {
            return;
        }
        _lockPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1PEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1PSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunLockUiTransactionEvidence();
        var uiReady = PrepareUx1PEvidence("ux1-p-mechanisms") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.LockPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1PContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1P_{(valid ? "PASS" : "FAILED")} " +
            $"states_mechanisms={result.StatesAndMechanisms.ToString().ToLowerInvariant()} " +
            $"unlock_methods={result.UnlockMethods.ToString().ToLowerInvariant()} " +
            $"relock_jam={result.RelockAndJam.ToString().ToLowerInvariant()} " +
            $"lock_preview={result.LockPreview.ToString().ToLowerInvariant()} " +
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
