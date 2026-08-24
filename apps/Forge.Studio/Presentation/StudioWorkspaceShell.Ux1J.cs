using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _preciseTransformPlaceableButton = null!;

    private void BuildUx1JActions(HBoxContainer headingRow)
    {
        _preciseTransformPlaceableButton = StudioStyle.Button("Transformation", compact: true);
        _preciseTransformPlaceableButton.TooltipText =
            "Régler coordonnées, rotation, échelle, pivot, grille, parent, surface et animation.";
        _preciseTransformPlaceableButton.Pressed += OpenPreciseTransformPlaceable;
        headingRow.AddChild(_preciseTransformPlaceableButton);
    }

    private void OpenPreciseTransformPlaceable() =>
        OpenPlaceableBlueprint(
            staticFocus: false,
            interactionFocus: false,
            containerFocus: false,
            identityFocus: false,
            transformFocus: true);

    private void UpdateUx1JActionState()
    {
        if (_preciseTransformPlaceableButton is null)
        {
            return;
        }
        _preciseTransformPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1JEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1JSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunPreciseTransformUiTransactionEvidence();
        var uiReady = PrepareUx1JEvidence("ux1-j-transform") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.PreciseTransformPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1JContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1J_{(valid ? "PASS" : "FAILED")} " +
            $"coordinates={result.Coordinates.ToString().ToLowerInvariant()} " +
            $"pivot_snap={result.PivotSnap.ToString().ToLowerInvariant()} " +
            $"parentage={result.Parentage.ToString().ToLowerInvariant()} " +
            $"surface={result.SurfaceAlignment.ToString().ToLowerInvariant()} " +
            $"animation={result.TransformAnimation.ToString().ToLowerInvariant()} " +
            $"geometry_guard={result.GeometryGuard.ToString().ToLowerInvariant()} " +
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
