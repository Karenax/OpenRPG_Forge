using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorFoundationOverlay _doorFoundationOverlay = null!;
    private Button _doorFoundationButton = null!;

    private void BuildUx1XActions(HBoxContainer headingRow)
    {
        _doorFoundationButton = StudioStyle.Button("Fondation de porte", compact: true);
        _doorFoundationButton.TooltipText =
            "Configurer le placement, l’état initial et l’interaction d’ouverture d’une porte.";
        _doorFoundationButton.Pressed += OpenDoorFoundation;
        headingRow.AddChild(_doorFoundationButton);
    }

    private void InitializeUx1XOverlay()
    {
        _doorFoundationOverlay = new StudioDoorFoundationOverlay(_session);
        _doorFoundationOverlay.StatusChanged += SetStatus;
        _doorFoundationOverlay.DoorChanged += RefreshAfterUx1XMutation;
        AddChild(_doorFoundationOverlay);
    }

    private void OpenDoorFoundation()
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
        SwitchPage("placement", notify: false);
        _doorFoundationOverlay.Open();
        SetStatus("Fondation de porte ouverte · sélection et aperçu obligatoires avant application");
    }

    private void RefreshAfterUx1XMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1XActionState();
    }

    private void UpdateUx1XActionState()
    {
        if (_doorFoundationButton is null)
        {
            return;
        }

        _doorFoundationButton.Visible = PageId == "placement" &&
                                        _session.Project?.Blueprints.Any(blueprint =>
                                            blueprint.Kind == Forge.Domain.Blueprints.BlueprintKind.Door) == true;
    }

    private void CloseUx1XOverlay()
    {
        if (_doorFoundationOverlay is not null)
        {
            _doorFoundationOverlay.Close();
        }
    }

    internal bool PrepareUx1XEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _doorFoundationOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1XSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _doorFoundationOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1XEvidence("ux1-x-interaction") &&
                      PageId == "placement" &&
                      _doorFoundationOverlay.IsOpen &&
                      _doorFoundationOverlay.SectionCount == 3 &&
                      _doorFoundationOverlay.PreviewReady &&
                      _doorFoundationOverlay.ShowsHumanSurface;
        _doorFoundationOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1XContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1X_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitDoorSelection.ToString().ToLowerInvariant()} " +
            $"socket_free={result.SocketAndUniversalPlacement.ToString().ToLowerInvariant()} " +
            $"nine_states={result.NineStateMachine.ToString().ToLowerInvariant()} " +
            $"opening_interaction={result.OpeningInteraction.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision_blocked={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"navigation_internal={result.NavigationInternalOnly.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
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
