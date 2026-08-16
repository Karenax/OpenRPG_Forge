using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorReactiveComplexOverlay _doorReactiveComplexOverlay = null!;
    private Button _doorReactiveComplexButton = null!;

    private void BuildUx1ADActions(HBoxContainer headingRow)
    {
        _doorReactiveComplexButton = StudioStyle.Button("Réactions & panneaux", compact: true);
        _doorReactiveComplexButton.TooltipText =
            "Configurer capacités, rythme, événement personnalisé choisi, conversation, faction et assemblage d’une porte.";
        _doorReactiveComplexButton.Pressed += OpenDoorReactiveComplexUx1AD;
        headingRow.AddChild(_doorReactiveComplexButton);
    }

    private void InitializeUx1ADOverlay()
    {
        _doorReactiveComplexOverlay = new StudioDoorReactiveComplexOverlay(_session);
        _doorReactiveComplexOverlay.StatusChanged += SetStatus;
        _doorReactiveComplexOverlay.DoorChanged += RefreshAfterUx1ADMutation;
        AddChild(_doorReactiveComplexOverlay);
    }

    private void OpenDoorReactiveComplexUx1AD()
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
        SwitchPage("placement", notify: false);
        _doorReactiveComplexOverlay.Open();
        SetStatus("Réactions et panneaux ouverts · sélection et aperçu obligatoires avant application");
    }

    private void RefreshAfterUx1ADMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1ADActionState();
    }

    private void UpdateUx1ADActionState()
    {
        if (_doorReactiveComplexButton is null)
        {
            return;
        }
        _doorReactiveComplexButton.Visible = PageId == "placement" &&
                                              _session.Project?.Blueprints.Any(blueprint =>
                                                  blueprint.Kind ==
                                                  Forge.Domain.Blueprints.BlueprintKind.Door) == true;
    }

    private void CloseUx1ADOverlay()
    {
        if (_doorReactiveComplexOverlay is not null)
        {
            _doorReactiveComplexOverlay.Close();
        }
    }

    internal bool PrepareUx1ADEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _doorReactiveComplexOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1ADSmoke()
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
        var uiTransaction = _doorReactiveComplexOverlay.RunUiTransactionEvidence();
        var uiTransactionSourceUntouched = _doorReactiveComplexOverlay.UiTransactionSourceUntouched;
        var evidencePrepared = PrepareUx1ADEvidence("ux1-ad-reactions");
        var uiExplicitSelection = _doorReactiveComplexOverlay.ExplicitSelectionProven;
        var uiCustomSelection = _doorReactiveComplexOverlay.ExplicitCustomSelectionProven;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      _doorReactiveComplexOverlay.IsOpen &&
                      _doorReactiveComplexOverlay.SectionCount == 3 &&
                      _doorReactiveComplexOverlay.PreviewReady &&
                      uiExplicitSelection &&
                      uiCustomSelection &&
                      _doorReactiveComplexOverlay.ShowsHumanSurface;
        _doorReactiveComplexOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1ADContractSmoke();
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
            $"OPENRPG_FORGE_STUDIO_UX1AD_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitDoorSelection.ToString().ToLowerInvariant()} " +
            $"duplicate_human_labels={result.DuplicateHumanLabels.ToString().ToLowerInvariant()} " +
            $"unsafe_assembly_blocked={result.UnsafeAssemblyBlocked.ToString().ToLowerInvariant()} " +
            $"ability_events={result.AbilityEvents.ToString().ToLowerInvariant()} " +
            $"heartbeat_custom={result.HeartbeatAndCustomEvents.ToString().ToLowerInvariant()} " +
            $"narrative_references={result.NarrativeReferences.ToString().ToLowerInvariant()} " +
            $"complex_assembly={result.ComplexAssembly.ToString().ToLowerInvariant()} " +
            $"neighbor_event_merge={result.NeighborEventMerge.ToString().ToLowerInvariant()} " +
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
            $"ui_custom_selection={uiCustomSelection.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"ui_transaction_isolated={uiTransactionSourceUntouched.ToString().ToLowerInvariant()} " +
            $"source_session_untouched={currentProjectUntouched.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
