using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorDamageLoadingOverlay _doorDamageLoadingOverlay = null!;
    private Button _doorDamageLoadingButton = null!;

    private void BuildUx1ABActions(HBoxContainer headingRow)
    {
        _doorDamageLoadingButton = StudioStyle.Button("Dégâts et chargement", compact: true);
        _doorDamageLoadingButton.TooltipText =
            "Configurer destructibilité, présentation de chargement et réactions serveur d’une porte.";
        _doorDamageLoadingButton.Pressed += OpenDoorDamageLoading;
        headingRow.AddChild(_doorDamageLoadingButton);
    }

    private void InitializeUx1ABOverlay()
    {
        _doorDamageLoadingOverlay = new StudioDoorDamageLoadingOverlay(_session);
        _doorDamageLoadingOverlay.StatusChanged += SetStatus;
        _doorDamageLoadingOverlay.DoorChanged += RefreshAfterUx1ABMutation;
        AddChild(_doorDamageLoadingOverlay);
    }

    private void OpenDoorDamageLoading()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer une porte.", error: true);
            return;
        }

        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaCreationOverlay.Close();
        _areaLoadingOverlay.Close();
        _areaLayersOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _placeableBlueprintOverlay.Close();
        CloseUx1WOverlay();
        CloseUx1XOverlay();
        CloseUx1YOverlay();
        CloseUx1ZOverlay();
        SwitchPage("placement", notify: false);
        _doorDamageLoadingOverlay.Open();
        SetStatus("Dégâts et chargement ouverts · sélection et aperçu obligatoires avant application");
    }

    private void RefreshAfterUx1ABMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1ABActionState();
    }

    private void UpdateUx1ABActionState()
    {
        if (_doorDamageLoadingButton is null)
        {
            return;
        }
        _doorDamageLoadingButton.Visible = PageId == "placement" &&
                                           _session.Project?.Blueprints.Any(blueprint =>
                                               blueprint.Kind == Forge.Domain.Blueprints.BlueprintKind.Door) == true;
    }

    private void CloseUx1ABOverlay()
    {
        if (_doorDamageLoadingOverlay is not null)
        {
            _doorDamageLoadingOverlay.Close();
        }
    }

    internal bool PrepareUx1ABEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _doorDamageLoadingOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1ABSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _doorDamageLoadingOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1ABEvidence("ux1-ab-events");
        var uiExplicitSelection = _doorDamageLoadingOverlay.ExplicitSelectionProven;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      _doorDamageLoadingOverlay.IsOpen &&
                      _doorDamageLoadingOverlay.SectionCount == 3 &&
                      _doorDamageLoadingOverlay.PreviewReady &&
                      _doorDamageLoadingOverlay.ExplicitSelectionProven &&
                      _doorDamageLoadingOverlay.ShowsHumanSurface;
        _doorDamageLoadingOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1ABContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AB_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitDoorSelection.ToString().ToLowerInvariant()} " +
            $"damage={result.DamagePolicy.ToString().ToLowerInvariant()} " +
            $"loading={result.LoadingPresentation.ToString().ToLowerInvariant()} " +
            $"authoritative_events={result.AuthoritativeEvents.ToString().ToLowerInvariant()} " +
            $"neighbor_event_merge={result.NeighborEventMerge.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision_blocked={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui_explicit_selection={uiExplicitSelection.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
