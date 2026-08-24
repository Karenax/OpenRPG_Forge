using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _initialAnimationPlaceableButton = null!;

    private void BuildUx1KActions(HBoxContainer headingRow)
    {
        _initialAnimationPlaceableButton = StudioStyle.Button("Animation initiale", compact: true);
        _initialAnimationPlaceableButton.TooltipText =
            "Choisir l’état chargé, ses animations, ses apparences et les transitions prévisualisées.";
        _initialAnimationPlaceableButton.Pressed += OpenInitialAnimationPlaceable;
        headingRow.AddChild(_initialAnimationPlaceableButton);
    }

    private void OpenInitialAnimationPlaceable() =>
        OpenPlaceableBlueprint(
            staticFocus: false,
            interactionFocus: false,
            containerFocus: false,
            identityFocus: false,
            transformFocus: false,
            initialAnimationFocus: true);

    private void UpdateUx1KActionState()
    {
        if (_initialAnimationPlaceableButton is null)
        {
            return;
        }
        _initialAnimationPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1KEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1KSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunInitialAnimationUiTransactionEvidence();
        var uiReady = PrepareUx1KEvidence("ux1-k-initial") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.InitialAnimationPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1KContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1K_{(valid ? "PASS" : "FAILED")} " +
            $"initial_state={result.InitialState.ToString().ToLowerInvariant()} " +
            $"six_states={result.SixStates.ToString().ToLowerInvariant()} " +
            $"animations_overrides={result.AnimationsAndOverrides.ToString().ToLowerInvariant()} " +
            $"interactable={result.InteractableStates.ToString().ToLowerInvariant()} " +
            $"transitions={result.Transitions.ToString().ToLowerInvariant()} " +
            $"transition_preview={result.PreviewTransition.ToString().ToLowerInvariant()} " +
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
