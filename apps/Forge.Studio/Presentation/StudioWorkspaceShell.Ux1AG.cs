using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorAccessEventsOverlay _doorAccessEventsOverlay = null!;
    private Button _doorAccessEventsButton = null!;

    private void BuildUx1AGActions(HBoxContainer headingRow)
    {
        _doorAccessEventsButton = StudioStyle.Button("Accès & événements", compact: true);
        _doorAccessEventsButton.TooltipText =
            "Configurer verrou, clés, refus, piège et événements terminaux d’une porte.";
        _doorAccessEventsButton.Pressed += OpenDoorAccessEventsUx1AG;
        headingRow.AddChild(_doorAccessEventsButton);
    }

    private void InitializeUx1AGOverlay()
    {
        _doorAccessEventsOverlay = new StudioDoorAccessEventsOverlay(_session);
        _doorAccessEventsOverlay.StatusChanged += SetStatus;
        _doorAccessEventsOverlay.DoorChanged += RefreshAfterUx1AGMutation;
        AddChild(_doorAccessEventsOverlay);
    }

    private void OpenDoorAccessEventsUx1AG()
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
        CloseUx1AAOverlay();
        CloseUx1ABOverlay();
        CloseUx1ACOverlay();
        CloseUx1ADOverlay();
        CloseUx1AEOverlay();
        CloseUx1AFWorkspace();
        SwitchPage("placement", notify: false);
        _doorAccessEventsOverlay.Open();
        SetStatus("Accès et événements ouverts · sélection et aperçu obligatoires avant application");
    }

    private void RefreshAfterUx1AGMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1AGActionState();
    }

    private void UpdateUx1AGActionState()
    {
        if (_doorAccessEventsButton is null)
        {
            return;
        }
        _doorAccessEventsButton.Visible = PageId == "placement" &&
                                          _session.Project?.Blueprints.Any(blueprint =>
                                              blueprint.Kind ==
                                              Forge.Domain.Blueprints.BlueprintKind.Door) == true;
    }

    private void CloseUx1AGOverlay()
    {
        if (_doorAccessEventsOverlay is not null)
        {
            _doorAccessEventsOverlay.Close();
        }
    }

    internal bool PrepareUx1AGEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _doorAccessEventsOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1AGSmoke()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalDirty = _session.IsDirty;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _doorAccessEventsOverlay.RunUiTransactionEvidence();
        var uiTransactionSourceUntouched = _doorAccessEventsOverlay.UiTransactionSourceUntouched;
        var evidencePrepared = PrepareUx1AGEvidence("ux1-ag-events");
        var uiExplicitSelection = _doorAccessEventsOverlay.ExplicitSelectionProven;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      _doorAccessEventsOverlay.IsOpen &&
                      _doorAccessEventsOverlay.SectionCount == 3 &&
                      _doorAccessEventsOverlay.PreviewReady &&
                      uiExplicitSelection &&
                      _doorAccessEventsOverlay.ShowsHumanSurface;
        _doorAccessEventsOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1AGContractSmoke();
        var currentHistory = _session.History;
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      _session.IsDirty == originalDirty &&
                                      originalHistory is not null &&
                                      currentHistory is not null &&
                                      ReferenceEquals(originalHistory, currentHistory) &&
                                      currentHistory.State.Revision == originalRevision &&
                                      currentHistory.CanUndo == originalCanUndo &&
                                      currentHistory.CanRedo == originalCanRedo &&
                                      currentHistory.Journal.SequenceEqual(originalJournal);
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    uiTransactionSourceUntouched && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AG_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitDoorSelection.ToString().ToLowerInvariant()} " +
            $"fresh_hidden_scaffold={result.FreshHiddenRequiredScaffold.ToString().ToLowerInvariant()} " +
            $"hidden_scaffold_preserved={result.HiddenRequiredScaffoldPreserved.ToString().ToLowerInvariant()} " +
            $"lock_credentials={result.LockAndCredentials.ToString().ToLowerInvariant()} " +
            $"rejection={result.Rejection.ToString().ToLowerInvariant()} " +
            $"trap={result.Trap.ToString().ToLowerInvariant()} " +
            $"terminal_state_events={result.TerminalStateEvents.ToString().ToLowerInvariant()} " +
            $"terminal_lock_events={result.TerminalLockEvents.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"approval_revision_guard={result.ApprovalRevisionGuard.ToString().ToLowerInvariant()} " +
            $"command_revision_guard={result.CommandRevisionGuard.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"preservation={result.Preservation.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui_explicit_selection={uiExplicitSelection.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"ui_transaction_isolated={uiTransactionSourceUntouched.ToString().ToLowerInvariant()} " +
            $"source_session_untouched={currentProjectUntouched.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
