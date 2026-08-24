using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _credentialPlaceableButton = null!;

    private void BuildUx1QActions(HBoxContainer headingRow)
    {
        _credentialPlaceableButton = StudioStyle.Button("Clé consommée", compact: true);
        _credentialPlaceableButton.TooltipText =
            "Configurer conservation, consommation, charges, partage, permission virtuelle et audit.";
        _credentialPlaceableButton.Pressed += OpenCredentialPlaceable;
        headingRow.AddChild(_credentialPlaceableButton);
    }

    private void OpenCredentialPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, credentialFocus: true);

    private void UpdateUx1QActionState()
    {
        if (_credentialPlaceableButton is null)
        {
            return;
        }
        _credentialPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1QEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1QSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunCredentialUiTransactionEvidence();
        var uiReady = PrepareUx1QEvidence("ux1-q-charge") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.CredentialPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1QContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1Q_{(valid ? "PASS" : "FAILED")} " +
            $"policy_modes={result.PolicyModes.ToString().ToLowerInvariant()} " +
            $"consumption_decisions={result.ConsumptionDecisions.ToString().ToLowerInvariant()} " +
            $"virtual_permission={result.VirtualPermission.ToString().ToLowerInvariant()} " +
            $"credential_preview={result.CredentialPreview.ToString().ToLowerInvariant()} " +
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
