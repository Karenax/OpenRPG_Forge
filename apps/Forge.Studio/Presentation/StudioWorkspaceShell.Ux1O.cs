using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _conversationPlaceableButton = null!;

    private void BuildUx1OActions(HBoxContainer headingRow)
    {
        _conversationPlaceableButton = StudioStyle.Button("Conversation", compact: true);
        _conversationPlaceableButton.TooltipText =
            "Relier le placeable à des dialogues selon cinq situations et prévisualiser la résolution.";
        _conversationPlaceableButton.Pressed += OpenConversationPlaceable;
        headingRow.AddChild(_conversationPlaceableButton);
    }

    private void OpenConversationPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, conversationFocus: true);

    private void UpdateUx1OActionState()
    {
        if (_conversationPlaceableButton is null)
        {
            return;
        }
        _conversationPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1OEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1OSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunConversationUiTransactionEvidence();
        var uiReady = PrepareUx1OEvidence("ux1-o-bindings") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.ConversationPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1OContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1O_{(valid ? "PASS" : "FAILED")} " +
            $"five_triggers={result.FiveTriggers.ToString().ToLowerInvariant()} " +
            $"dialogue_entries={result.DialogueAndEntryPoints.ToString().ToLowerInvariant()} " +
            $"priority_once={result.PriorityAndOnce.ToString().ToLowerInvariant()} " +
            $"conversation_preview={result.ConversationPreview.ToString().ToLowerInvariant()} " +
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
