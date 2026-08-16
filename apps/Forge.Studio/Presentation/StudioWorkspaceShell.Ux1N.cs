using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _factionPlaceableButton = null!;

    private void BuildUx1NActions(HBoxContainer headingRow)
    {
        _factionPlaceableButton = StudioStyle.Button("Faction", compact: true);
        _factionPlaceableButton.TooltipText =
            "Relier le placeable à une faction et prévisualiser hostilité, crime, accès et ciblage.";
        _factionPlaceableButton.Pressed += OpenFactionPlaceable;
        headingRow.AddChild(_factionPlaceableButton);
    }

    private void OpenFactionPlaceable() =>
        OpenPlaceableBlueprint(staticFocus: false, factionOwnershipFocus: true);

    private void UpdateUx1NActionState()
    {
        if (_factionPlaceableButton is null)
        {
            return;
        }
        _factionPlaceableButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1NEvidence(string mode)
    {
        _session.EnsureUx1NEvidenceFactions();
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1NSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var seeded = _session.EnsureUx1NEvidenceFactions();
        var uiTransaction = _placeableBlueprintOverlay.RunFactionOwnershipUiTransactionEvidence();
        var uiReady = PrepareUx1NEvidence("ux1-n-memberships") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.FactionOwnershipPreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1NContractSmoke();
        var seedRestored = !seeded || _session.Undo().Succeeded;
        var currentProjectUntouched = seedRestored && ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1N_{(valid ? "PASS" : "FAILED")} " +
            $"primary_faction={result.PrimaryFaction.ToString().ToLowerInvariant()} " +
            $"memberships={result.Memberships.ToString().ToLowerInvariant()} " +
            $"hostility_crime={result.HostilityAndCrime.ToString().ToLowerInvariant()} " +
            $"access_targeting={result.AccessAndTargeting.ToString().ToLowerInvariant()} " +
            $"decision_preview={result.DecisionPreview.ToString().ToLowerInvariant()} " +
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
