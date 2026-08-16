using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _interactablePlaceableButton = null!;

    private void BuildUx1GActions(HBoxContainer headingRow)
    {
        _interactablePlaceableButton = StudioStyle.Button("Objet utilisable", compact: true);
        _interactablePlaceableButton.TooltipText =
            "Composer les actions, la portée, le ciblage, l’autorité, les conditions et l’événement OnUsed.";
        _interactablePlaceableButton.Pressed += OpenInteractablePlaceable;
        headingRow.AddChild(_interactablePlaceableButton);
    }

    private void OpenInteractablePlaceable() => OpenPlaceableBlueprint(staticFocus: false, interactionFocus: true);

    private void UpdateUx1GActionState()
    {
        if (_interactablePlaceableButton is null)
        {
            return;
        }
        _interactablePlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1GEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1GSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunInteractionUiTransactionEvidence();
        var uiReady = PrepareUx1GEvidence("ux1-g-actions") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.InteractableRoleSelected &&
                      _placeableBlueprintOverlay.InteractionVerbCount == 2 &&
                      _placeableBlueprintOverlay.InteractionPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1GContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1G_{(valid ? "PASS" : "FAILED")} " +
            $"interactable_role={result.InteractableRole.ToString().ToLowerInvariant()} " +
            $"verbs={result.Verbs.ToString().ToLowerInvariant()} " +
            $"range_focus={result.RangeFocus.ToString().ToLowerInvariant()} " +
            $"authority_conditions={result.AuthorityConditions.ToString().ToLowerInvariant()} " +
            $"on_used={result.OnUsed.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"runtime_event={result.RuntimeEvent.ToString().ToLowerInvariant()} " +
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
