using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _lootPlaceableButton = null!;

    private void BuildUx1SActions(HBoxContainer headingRow)
    {
        _lootPlaceableButton = StudioStyle.Button("Trésor", compact: true);
        _lootPlaceableButton.TooltipText =
            "Composer objets, catégories, poids, quantités, qualité, moment, graine et état généré.";
        _lootPlaceableButton.Pressed += OpenInitialTreasurePlaceable;
        headingRow.AddChild(_lootPlaceableButton);
    }

    private void OpenInitialTreasurePlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, lootFocus: true);

    private void UpdateUx1SActionState()
    {
        if (_lootPlaceableButton is null)
        {
            return;
        }
        _lootPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1SEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1SSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunLootUiTransactionEvidence();
        var uiReady = PrepareUx1SEvidence("ux1-s-reset-preview") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.LootPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1SContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1S_{(valid ? "PASS" : "FAILED")} " +
            $"table_entries={result.TableEntries.ToString().ToLowerInvariant()} " +
            $"timing_seed={result.TimingAndSeed.ToString().ToLowerInvariant()} " +
            $"generated_state_preserved={result.GeneratedStatePreserved.ToString().ToLowerInvariant()} " +
            $"explicit_reset_guard={result.ExplicitResetGuard.ToString().ToLowerInvariant()} " +
            $"loot_preview={result.LootPreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
            $"script_read={result.ScriptRead.ToString().ToLowerInvariant()} " +
            $"optional_required={result.OptionalRequired.ToString().ToLowerInvariant()} " +
            $"stable_entry_selection={result.StableEntrySelection.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
