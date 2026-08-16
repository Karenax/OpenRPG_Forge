using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _staticDecorationButton = null!;

    private void BuildUx1FActions(HBoxContainer headingRow)
    {
        _staticDecorationButton = StudioStyle.Button("Décor statique", compact: true);
        _staticDecorationButton.TooltipText =
            "Configurer regroupement, instanciation, occlusion, fusion et collision optionnelle avec aperçu.";
        _staticDecorationButton.Pressed += OpenStaticDecoration;
        headingRow.AddChild(_staticDecorationButton);
    }

    private void OpenStaticDecoration() => OpenPlaceableBlueprint(staticFocus: true);

    private void UpdateUx1FActionState()
    {
        if (_staticDecorationButton is null)
        {
            return;
        }
        _staticDecorationButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1FEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1FSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunStaticUiTransactionEvidence();
        var uiReady = PrepareUx1FEvidence("ux1-f-static") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.StaticRoleSelected &&
                      _placeableBlueprintOverlay.StaticPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1FContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1F_{(valid ? "PASS" : "FAILED")} " +
            $"static_role={result.StaticRole.ToString().ToLowerInvariant()} " +
            $"optimization={result.Optimization.ToString().ToLowerInvariant()} " +
            $"optional_collision={result.OptionalCollision.ToString().ToLowerInvariant()} " +
            $"merge_guard={result.MergeGuard.ToString().ToLowerInvariant()} " +
            $"optimization_guard={result.OptimizationGuard.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"runtime={result.RuntimeSemantics.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
