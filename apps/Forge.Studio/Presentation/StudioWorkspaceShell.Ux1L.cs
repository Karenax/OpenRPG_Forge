using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _destructibilityPlaceableButton = null!;

    private void BuildUx1LActions(HBoxContainer headingRow)
    {
        _destructibilityPlaceableButton = StudioStyle.Button("Dégâts", compact: true);
        _destructibilityPlaceableButton.TooltipText =
            "Configurer la destructibilité : santé, armure, résistances, étapes visuelles, débris et réparation.";
        _destructibilityPlaceableButton.Pressed += OpenDestructibilityPlaceable;
        headingRow.AddChild(_destructibilityPlaceableButton);
    }

    private void OpenDestructibilityPlaceable() =>
        OpenPlaceableBlueprint(
            staticFocus: false,
            interactionFocus: false,
            containerFocus: false,
            identityFocus: false,
            transformFocus: false,
            initialAnimationFocus: false,
            destructibilityFocus: true);

    private void UpdateUx1LActionState()
    {
        if (_destructibilityPlaceableButton is null)
        {
            return;
        }
        _destructibilityPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1LEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1LSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunDestructibilityUiTransactionEvidence();
        var uiReady = PrepareUx1LEvidence("ux1-l-health") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.DestructibilityPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1LContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1L_{(valid ? "PASS" : "FAILED")} " +
            $"health_armor={result.HealthArmor.ToString().ToLowerInvariant()} " +
            $"resistances={result.Resistances.ToString().ToLowerInvariant()} " +
            $"visual_stages={result.VisualStages.ToString().ToLowerInvariant()} " +
            $"destruction_debris={result.DestructionDebris.ToString().ToLowerInvariant()} " +
            $"repair={result.Repair.ToString().ToLowerInvariant()} " +
            $"damage_preview={result.DamagePreview.ToString().ToLowerInvariant()} " +
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
