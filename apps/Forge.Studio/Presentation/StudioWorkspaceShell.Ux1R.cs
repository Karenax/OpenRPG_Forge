using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _trapPlaceableButton = null!;

    private void BuildUx1RActions(HBoxContainer headingRow)
    {
        _trapPlaceableButton = StudioStyle.Button("Piège", compact: true);
        _trapPlaceableButton.TooltipText =
            "Configurer déclencheur, effet, détection, désamorçage, réarmement, propriétaire, VFX et alliés.";
        _trapPlaceableButton.Pressed += OpenTrapPlaceable;
        headingRow.AddChild(_trapPlaceableButton);
    }

    private void OpenTrapPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, trapFocus: true);

    private void UpdateUx1RActionState()
    {
        if (_trapPlaceableButton is null)
        {
            return;
        }
        _trapPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1REvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1RSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunTrapUiTransactionEvidence();
        var uiReady = PrepareUx1REvidence("ux1-r-trigger") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.TrapPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1RContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1R_{(valid ? "PASS" : "FAILED")} " +
            $"trigger_payload={result.TriggerAndPayload.ToString().ToLowerInvariant()} " +
            $"detect_disarm={result.DetectAndDisarm.ToString().ToLowerInvariant()} " +
            $"reset_one_shot={result.ResetAndOneShot.ToString().ToLowerInvariant()} " +
            $"ownership_vfx_friendly={result.OwnershipVfxFriendly.ToString().ToLowerInvariant()} " +
            $"trap_preview={result.TrapPreview.ToString().ToLowerInvariant()} " +
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
