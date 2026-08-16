using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _identityAppearancePlaceableButton = null!;

    private void BuildUx1IActions(HBoxContainer headingRow)
    {
        _identityAppearancePlaceableButton = StudioStyle.Button("Identité & apparence", compact: true);
        _identityAppearancePlaceableButton.TooltipText =
            "Configurer nom court, langues, tags, notes, meshes, matériaux, icône, portrait et VFX.";
        _identityAppearancePlaceableButton.Pressed += OpenIdentityAppearancePlaceable;
        headingRow.AddChild(_identityAppearancePlaceableButton);
    }

    private void OpenIdentityAppearancePlaceable() =>
        OpenPlaceableBlueprint(
            staticFocus: false,
            interactionFocus: false,
            containerFocus: false,
            identityFocus: true);

    private void UpdateUx1IActionState()
    {
        if (_identityAppearancePlaceableButton is null)
        {
            return;
        }
        _identityAppearancePlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1IEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1ISmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunIdentityAppearanceUiTransactionEvidence();
        var uiReady = PrepareUx1IEvidence("ux1-i-identity") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.IdentityAppearancePreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1IContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1I_{(valid ? "PASS" : "FAILED")} " +
            $"identity={result.Identity.ToString().ToLowerInvariant()} " +
            $"localization={result.Localization.ToString().ToLowerInvariant()} " +
            $"appearance={result.Appearance.ToString().ToLowerInvariant()} " +
            $"metadata={result.Metadata.ToString().ToLowerInvariant()} " +
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
