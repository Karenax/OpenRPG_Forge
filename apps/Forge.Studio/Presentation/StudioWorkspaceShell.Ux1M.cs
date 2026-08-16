using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _plotProtectionPlaceableButton = null!;

    private void BuildUx1MActions(HBoxContainer headingRow)
    {
        _plotProtectionPlaceableButton = StudioStyle.Button("Plot", compact: true);
        _plotProtectionPlaceableButton.TooltipText =
            "Configurer la protection plot : actions, droits, feedback et dérogation MJ auditée.";
        _plotProtectionPlaceableButton.Pressed += OpenPlotProtectionPlaceable;
        headingRow.AddChild(_plotProtectionPlaceableButton);
    }

    private void OpenPlotProtectionPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, plotProtectionFocus: true);

    private void UpdateUx1MActionState()
    {
        if (_plotProtectionPlaceableButton is null)
        {
            return;
        }
        _plotProtectionPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1MEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1MSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunPlotProtectionUiTransactionEvidence();
        var uiReady = PrepareUx1MEvidence("ux1-m-actions") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.PlotProtectionPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1MContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1M_{(valid ? "PASS" : "FAILED")} " +
            $"protected_actions={result.ProtectedActions.ToString().ToLowerInvariant()} " +
            $"permissions={result.Permissions.ToString().ToLowerInvariant()} " +
            $"feedback={result.Feedback.ToString().ToLowerInvariant()} " +
            $"gm_override={result.GameMasterOverride.ToString().ToLowerInvariant()} " +
            $"audit_trail={result.AuditTrail.ToString().ToLowerInvariant()} " +
            $"policy_preview={result.PolicyPreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"runtime_enforcement={result.RuntimeEnforcement.ToString().ToLowerInvariant()} " +
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
