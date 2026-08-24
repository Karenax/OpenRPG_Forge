using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _lifecycleEventsPlaceableButton = null!;

    private void BuildUx1VActions(HBoxContainer headingRow)
    {
        _lifecycleEventsPlaceableButton = StudioStyle.Button("Événements du placeable", compact: true);
        _lifecycleEventsPlaceableButton.TooltipText =
            "Configurer et prévisualiser OnUsed, OnDisturbed, OnDamaged et OnDeath.";
        _lifecycleEventsPlaceableButton.Pressed += OpenLifecycleEventsPlaceable;
        headingRow.AddChild(_lifecycleEventsPlaceableButton);
    }

    private void OpenLifecycleEventsPlaceable()
    {
        OpenPlaceableBlueprint(staticFocus: false);
        _placeableBlueprintOverlay.OpenLifecycleEvents();
        SetStatus("Événements du placeable ouverts · OnUsed, OnDisturbed, OnDamaged et OnDeath sont prévisualisables sans écriture");
    }

    private void UpdateUx1VActionState()
    {
        if (_lifecycleEventsPlaceableButton is not null)
            _lifecycleEventsPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1VEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1VSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunLifecycleEventsUiTransactionEvidence();
        var uiReady = PrepareUx1VEvidence("ux1-v-death") &&
                      PageId == "placement" && _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.LifecycleEventsPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1VContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1V_{(valid ? "PASS" : "FAILED")} " +
            $"on_used_binding_api={result.OnUsedBindingAndApi.ToString().ToLowerInvariant()} " +
            $"on_used_payload_scripts={result.OnUsedPayloadAndScripts.ToString().ToLowerInvariant()} " +
            $"contents_changed={result.ContentsChangedBindingAndPayload.ToString().ToLowerInvariant()} " +
            $"damage_death_bindings={result.DamageAndDeathBindings.ToString().ToLowerInvariant()} " +
            $"damage_death_payload={result.DamageAndDeathPayload.ToString().ToLowerInvariant()} " +
            $"isolated_on_used={result.IsolatedOnUsed.ToString().ToLowerInvariant()} " +
            $"isolated_contents_changed={result.IsolatedContentsChanged.ToString().ToLowerInvariant()} " +
            $"isolated_static_damage={result.IsolatedStaticDamage.ToString().ToLowerInvariant()} " +
            $"neighbor_preservation={result.NeighborPreservation.ToString().ToLowerInvariant()} " +
            $"dual_authority_sync={result.DualAuthoritySynchronization.ToString().ToLowerInvariant()} " +
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
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
