using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _reactivePhysicsPlaceableButton = null!;

    private void BuildUx1ZActions(HBoxContainer headingRow)
    {
        _reactivePhysicsPlaceableButton = StudioStyle.Button("Réactions & physique", compact: true);
        _reactivePhysicsPlaceableButton.TooltipText =
            "Configurer les attaques, capacités, rythmes et la politique physique d’un placeable.";
        _reactivePhysicsPlaceableButton.Pressed += OpenReactivePhysicsPlaceable;
        headingRow.AddChild(_reactivePhysicsPlaceableButton);
    }

    private void OpenReactivePhysicsPlaceable()
    {
        OpenPlaceableBlueprint(staticFocus: false);
        _placeableBlueprintOverlay.OpenReactivePhysics();
        SetStatus(
            "Réactions et physique ouvertes · sélection explicite et aperçu obligatoire avant application");
    }

    private void UpdateUx1ZActionState()
    {
        if (_reactivePhysicsPlaceableButton is null)
        {
            return;
        }

        _reactivePhysicsPlaceableButton.Visible = PageId == "placement" &&
                                                  _session.Project?.Blueprints.Any(blueprint =>
                                                      blueprint.Kind == BlueprintKind.Placeable) == true;
    }

    private void CloseUx1ZOverlay()
    {
        if (_placeableBlueprintOverlay is not null && _placeableBlueprintOverlay.ReactivePhysicsOpen)
        {
            _placeableBlueprintOverlay.CloseReactivePhysics();
        }
    }

    internal bool PrepareUx1ZEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareReactivePhysicsEvidence(mode);
    }

    internal bool RunUx1ZSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var uiTransaction = _placeableBlueprintOverlay.RunReactivePhysicsUiTransactionEvidence();
        var uiReady = PrepareUx1ZEvidence("ux1-z-physics") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.ReactivePhysicsOpen &&
                      _placeableBlueprintOverlay.ReactivePhysicsPlaceableCount >= 1 &&
                      _placeableBlueprintOverlay.ReactivePhysicsPreviewReady &&
                      _placeableBlueprintOverlay.ReactivePhysicsShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1ZContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      _session.IsDirty == originalDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1Z_{(valid ? "PASS" : "FAILED")} " +
            $"physical_attack={result.PhysicalAttackPreview.ToString().ToLowerInvariant()} " +
            $"spell_cast={result.SpellCastPreview.ToString().ToLowerInvariant()} " +
            $"heartbeat_custom={result.HeartbeatAndCustomPreview.ToString().ToLowerInvariant()} " +
            $"physics_policy={result.PhysicsPreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"invalid_cadence={result.InvalidCadenceBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_budget={result.InvalidBudgetBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_namespace={result.InvalidNamespaceBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_mass={result.InvalidMassBlocked.ToString().ToLowerInvariant()} " +
            $"stale_revision_blocked={result.StalePreviewBlocked.ToString().ToLowerInvariant()} " +
            $"explicit_selection={result.ExplicitSelectionRequired.ToString().ToLowerInvariant()} " +
            $"safe_profile_creation={result.SafeProfileCreation.ToString().ToLowerInvariant()} " +
            $"dormant_scheduler={result.DormantSchedulerPreserved.ToString().ToLowerInvariant()} " +
            $"capabilities_preserved={result.NeighborCapabilitiesPreserved.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPlaceablePreserved.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
