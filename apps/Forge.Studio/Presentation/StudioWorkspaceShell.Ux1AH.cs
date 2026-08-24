using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerAutomationDirectorOverlay _triggerAutomationDirectorOverlay = null!;
    private Button _triggerAutomationDirectorButton = null!;

    private void BuildUx1AHActions(HBoxContainer headingRow)
    {
        _triggerAutomationDirectorButton = StudioStyle.Button("Heartbeat & MJ", compact: true);
        _triggerAutomationDirectorButton.TooltipText =
            "Configurer cadence, événement personnalisé et autorisations manuelles sans lancer de timer ni de session Live Director.";
        _triggerAutomationDirectorButton.Pressed += OpenTriggerAutomationDirectorUx1AH;
        headingRow.AddChild(_triggerAutomationDirectorButton);
    }

    private void InitializeUx1AHOverlay()
    {
        _triggerAutomationDirectorOverlay = new StudioTriggerAutomationDirectorOverlay(_session);
        _triggerAutomationDirectorOverlay.StatusChanged += SetStatus;
        _triggerAutomationDirectorOverlay.TriggerChanged += RefreshAfterUx1AHMutation;
        AddChild(_triggerAutomationDirectorOverlay);

        _triggerFoundationButton.Pressed += CloseUx1AHOverlay;
        _triggerAdvancedButton.Pressed += CloseUx1AHOverlay;
        _triggerDiscoveryAccessButton.Pressed += CloseUx1AHOverlay;
    }

    private void OpenTriggerAutomationDirectorUx1AH()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de configurer Heartbeat et contrôle MJ.", error: true);
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
        CloseUx1YOverlay();
        CloseUx1ACOverlay();
        CloseUx1AEOverlay();
        SwitchPage("placement", notify: false);
        _triggerAutomationDirectorOverlay.Open();
        SetStatus(
            "Heartbeat & MJ ouvert · sélection explicite · simulation de politique, sans envoi Live Director");
    }

    private void RefreshAfterUx1AHMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1AHActionState();
    }

    private void UpdateUx1AHActionState()
    {
        if (_triggerAutomationDirectorButton is null)
        {
            return;
        }
        _triggerAutomationDirectorButton.Visible = PageId == "placement" && _session.ActiveArea is not null;
    }

    private void CloseUx1AHOverlay()
    {
        if (_triggerAutomationDirectorOverlay is not null)
        {
            _triggerAutomationDirectorOverlay.Close();
        }
    }

    internal bool PrepareUx1AHEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _triggerAutomationDirectorOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1AHSmoke()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureTriggerAutomationDirectorApprovalUx1AH();

        var uiTransaction = _triggerAutomationDirectorOverlay.RunUiTransactionEvidence();
        var placeholderReset = _triggerAutomationDirectorOverlay.PlaceholderResetEvidence;
        var evidencePrepared = PrepareUx1AHEvidence("ux1-ah-trigger-automation-director");
        var overlayOpen = _triggerAutomationDirectorOverlay.IsOpen;
        var sectionCount = _triggerAutomationDirectorOverlay.SectionCount;
        var choiceCount = _triggerAutomationDirectorOverlay.ChoiceCount;
        var explicitSelection = _triggerAutomationDirectorOverlay.HasExplicitSelection;
        var runtimePreview = _triggerAutomationDirectorOverlay.RuntimePreviewReady;
        var noLiveDispatch = _triggerAutomationDirectorOverlay.NoLiveDispatch;
        var humanSurface = _triggerAutomationDirectorOverlay.ShowsHumanSurface;
        var isolatedEvidence = _triggerAutomationDirectorOverlay.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _triggerAutomationDirectorOverlay.EvidenceSourceUntouched;
        var evidenceSourceApprovalPreserved =
            _triggerAutomationDirectorOverlay.EvidenceSourceApprovalPreserved;
        var unrepresentableCanonicalValuesBlocked =
            _triggerAutomationDirectorOverlay.UnrepresentableCanonicalValuesBlocked;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      overlayOpen &&
                      sectionCount == 3 &&
                      choiceCount >= 1 &&
                      explicitSelection &&
                      runtimePreview &&
                      noLiveDispatch &&
                      humanSurface &&
                      isolatedEvidence &&
                      evidenceSourceUntouched &&
                      evidenceSourceApprovalPreserved &&
                      unrepresentableCanonicalValuesBlocked;
        _triggerAutomationDirectorOverlay.Close();
        var evidenceResourcesReleased = _triggerAutomationDirectorOverlay.EvidenceResourcesReleased;
        var sourceSessionRestored = _triggerAutomationDirectorOverlay.SourceSessionRestored;
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);

        var result = _session.RunUx1AHContractSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.IsDirty == originalDirty &&
                              _session.TriggerAutomationDirectorApprovalMatchesUx1AH(originalApproval);
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AH_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} sections={sectionCount} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"runtime_preview={runtimePreview.ToString().ToLowerInvariant()} " +
                $"no_live_dispatch={noLiveDispatch.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                $"source_approval_preserved={evidenceSourceApprovalPreserved.ToString().ToLowerInvariant()} " +
                $"unrepresentable_values_blocked={unrepresentableCanonicalValuesBlocked.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && placeholderReset && sourceUnchanged &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AH_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.SelectionExplicit.ToString().ToLowerInvariant()} " +
            $"labels_disambiguated={result.ChoiceLabelsDisambiguated.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"scheduler={result.Scheduler.ToString().ToLowerInvariant()} " +
            $"custom_event={result.CustomEvent.ToString().ToLowerInvariant()} " +
            $"director={result.Director.ToString().ToLowerInvariant()} " +
            $"runtime_policy={result.RuntimePolicy.ToString().ToLowerInvariant()} " +
            $"no_live_dispatch={result.NoLiveDispatch.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_interval={result.InvalidIntervalBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_budget={result.InvalidBudgetBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_namespace={result.InvalidNamespaceBlocked.ToString().ToLowerInvariant()} " +
            $"unsafe_director={result.UnsafeDirectorBlocked.ToString().ToLowerInvariant()} " +
            $"stale_draft_blocked={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval_blocked={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"discovery_preserved={result.DiscoveryPreserved.ToString().ToLowerInvariant()} " +
            $"interaction_preserved={result.InteractionPreserved.ToString().ToLowerInvariant()} " +
            $"access_preserved={result.AccessPreserved.ToString().ToLowerInvariant()} " +
            $"foundation_preserved={result.FoundationPreserved.ToString().ToLowerInvariant()} " +
            $"portal_preserved={result.PortalPreserved.ToString().ToLowerInvariant()} " +
            $"validation_preserved={result.ValidationPreserved.ToString().ToLowerInvariant()} " +
            $"advanced_preserved={result.AdvancedPreserved.ToString().ToLowerInvariant()} " +
            $"ux2_neighbors_preserved={result.Ux2NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"source_approval_preserved={evidenceSourceApprovalPreserved.ToString().ToLowerInvariant()} " +
            $"unrepresentable_values_blocked={unrepresentableCanonicalValuesBlocked.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"placeholder_reset={placeholderReset.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
