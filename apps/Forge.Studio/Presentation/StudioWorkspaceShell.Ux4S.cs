using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioItemAuthoringOverlayUx4S _itemAuthoringOverlayUx4S = null!;
    private Button _itemAuthoringButtonUx4S = null!;

    private void BuildUx4SActions(HBoxContainer headingRow)
    {
        _itemAuthoringButtonUx4S = StudioStyle.Button("Éditer les objets", compact: true);
        _itemAuthoringButtonUx4S.TooltipText = "Assistant atomique : fondation, gameplay, économie, loot, inventaire et événements.";
        _itemAuthoringButtonUx4S.Pressed += OpenItemAuthoringUx4S; headingRow.AddChild(_itemAuthoringButtonUx4S);
    }
    private void InitializeUx4SOverlay()
    {
        _itemAuthoringOverlayUx4S = new(_session); _itemAuthoringOverlayUx4S.StatusChanged += SetStatus;
        _itemAuthoringOverlayUx4S.ItemChanged += RefreshAfterUx4SMutation; AddChild(_itemAuthoringOverlayUx4S);
    }
    private void OpenItemAuthoringUx4S()
    {
        SwitchPage("items"); _itemAuthoringOverlayUx4S.Open();
        SetStatus("Objets · sélection ou création explicite, cinq onglets et commande atomique.", false);
    }
    private void RefreshAfterUx4SMutation() { RefreshAfterUx1AMutation(); UpdateUx4SActionState(); }
    private void UpdateUx4SActionState()
    { if (_itemAuthoringButtonUx4S is not null) _itemAuthoringButtonUx4S.Visible = PageId == "items" && _session.Project is not null; }
    private void CloseUx4SOverlay()
    { if (_itemAuthoringOverlayUx4S is not null) _itemAuthoringOverlayUx4S.Close(); }
    internal bool PrepareUx4SEvidence(string mode)
    {
        if (PageId != "items") SwitchPage("items", notify: false); OpenItemAuthoringUx4S();
        return _itemAuthoringOverlayUx4S.PrepareEvidence(mode);
    }
    internal void ReleaseUx4SAutomatedEvidence() => _itemAuthoringOverlayUx4S.CloseEvidence();
    internal bool RunUx4SSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _itemAuthoringOverlayUx4S.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision; var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureItemApprovalUx4S();
        var uiTransaction = _itemAuthoringOverlayUx4S.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4SEvidence("ux4-s-item-authoring");
        var overlayOpen = _itemAuthoringOverlayUx4S.IsOpen;
        var uiReady = evidencePrepared && PageId == "items" && overlayOpen &&
                      _itemAuthoringOverlayUx4S.ItemChoiceCount > 0 && _itemAuthoringOverlayUx4S.HasExplicitSelection &&
                      _itemAuthoringOverlayUx4S.CanonicalPreviewReady && _itemAuthoringOverlayUx4S.ShowsCreation &&
                      _itemAuthoringOverlayUx4S.ShowsGameplay && _itemAuthoringOverlayUx4S.ShowsAdvanced &&
                      _itemAuthoringOverlayUx4S.ShowsOperations && _itemAuthoringOverlayUx4S.ShowsHumanSurface &&
                      _itemAuthoringOverlayUx4S.UsesIsolatedEvidence && _itemAuthoringOverlayUx4S.EvidencePreviewDryRun &&
                      _itemAuthoringOverlayUx4S.EvidenceSourceUntouched &&
                      (_itemAuthoringOverlayUx4S.UsesRealModuleEvidence ^
                       _itemAuthoringOverlayUx4S.UsesCanonicalFallbackEvidence);
        var failure = _itemAuthoringOverlayUx4S.LastEvidenceFailure; _itemAuthoringOverlayUx4S.CloseEvidence();
        var released = _itemAuthoringOverlayUx4S.EvidenceResourcesReleased;
        var restored = _itemAuthoringOverlayUx4S.SourceSessionRestored;
        var result = _session.RunUx4SContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _itemAuthoringOverlayUx4S.RestoreSourceSurfaceAfterSmoke();
        else _itemAuthoringOverlayUx4S.HideSourceSurfaceAfterSmoke();
        _session.RestoreItemApprovalUx4S(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _itemAuthoringOverlayUx4S.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) && ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId && _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureItemApprovalUx4S() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4S_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={failure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && released && restored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4S_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"creation={result.Creation.ToString().ToLowerInvariant()} identity_localization={result.IdentityLocalization.ToString().ToLowerInvariant()} " +
                 $"appearance_inventory_usage={result.AppearanceInventoryUsage.ToString().ToLowerInvariant()} gameplay={result.Gameplay.ToString().ToLowerInvariant()} " +
                 $"equipment_combat={result.EquipmentCombat.ToString().ToLowerInvariant()} loot_craft_generation={result.LootCraftGeneration.ToString().ToLowerInvariant()} " +
                 $"inspector_events={result.InspectorEvents.ToString().ToLowerInvariant()} validation={result.Validation.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
