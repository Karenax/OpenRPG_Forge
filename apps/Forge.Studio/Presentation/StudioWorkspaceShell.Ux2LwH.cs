using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterScenarioOverlayUx2LwH _encounterScenarioOverlayUx2LwH = null!;
    private Button _encounterScenarioButtonUx2LwH = null!;

    private void BuildUx2LwHActions(HBoxContainer headingRow)
    {
        _encounterScenarioButtonUx2LwH = StudioStyle.Button("Scénario de rencontre", compact: true);
        _encounterScenarioButtonUx2LwH.TooltipText =
            "Configurer ancres, activateur, lifecycle, phases et vagues par aperçu sec canonique.";
        _encounterScenarioButtonUx2LwH.Pressed += OpenEncounterScenarioUx2LwH;
        headingRow.AddChild(_encounterScenarioButtonUx2LwH);
    }

    private void InitializeUx2LwHOverlay()
    {
        _encounterScenarioOverlayUx2LwH = new StudioEncounterScenarioOverlayUx2LwH(_session);
        _encounterScenarioOverlayUx2LwH.StatusChanged += SetStatus;
        _encounterScenarioOverlayUx2LwH.EncounterChanged += RefreshAfterUx2LwHMutation;
        AddChild(_encounterScenarioOverlayUx2LwH);
    }

    private void OpenEncounterScenarioUx2LwH()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer un scénario de rencontre.", error: true);
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
        CloseUx1AGOverlay();
        CloseUx1AHOverlay();
        CloseUx1AIOverlay();
        CloseUx1AJOverlay();
        CloseUx2AOverlay();
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        CloseUx2LwLOverlay();
        CloseUx4AOverlay();
        CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterScenarioOverlayUx2LwH.Open();
        SetStatus("Scénario de rencontre ouvert · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwHMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwHActionState();
    }

    private void UpdateUx2LwHActionState()
    {
        if (_encounterScenarioButtonUx2LwH is null) return;
        _encounterScenarioButtonUx2LwH.Visible = PageId == "living-world" &&
                                                 _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwHOverlay()
    {
        if (_encounterScenarioOverlayUx2LwH is not null)
            _encounterScenarioOverlayUx2LwH.Close();
    }

    internal bool PrepareUx2LwHEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        CloseUx2LwLOverlay();
        return _encounterScenarioOverlayUx2LwH.PrepareEvidence(mode);
    }

    internal bool RunUx2LwHSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterScenarioOverlayUx2LwH.IsOpen;
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
        var originalApproval = _session.CaptureEncounterScenarioApprovalUx2LwH();

        var uiTransaction = _encounterScenarioOverlayUx2LwH.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwHEvidence("ux2-lw-h-encounter-scenario");
        var overlayOpen = _encounterScenarioOverlayUx2LwH.IsOpen;
        var choiceCount = _encounterScenarioOverlayUx2LwH.ChoiceCount;
        var explicitSelection = _encounterScenarioOverlayUx2LwH.HasExplicitSelection;
        var canonicalPreview = _encounterScenarioOverlayUx2LwH.CanonicalPreviewReady;
        var fiveRoutes = _encounterScenarioOverlayUx2LwH.ShowsFiveRoutes;
        var honestBoundary = _encounterScenarioOverlayUx2LwH.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _encounterScenarioOverlayUx2LwH.ShowsStructuredDiagnostics;
        var humanSurface = _encounterScenarioOverlayUx2LwH.ShowsHumanSurface;
        var realReferences = _encounterScenarioOverlayUx2LwH.UsesRealReferences;
        var isolatedEvidence = _encounterScenarioOverlayUx2LwH.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterScenarioOverlayUx2LwH.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && fiveRoutes && honestBoundary &&
                      structuredDiagnostics && humanSurface && realReferences && isolatedEvidence &&
                      evidenceSourceUntouched;
        _encounterScenarioOverlayUx2LwH.Close();
        var evidenceResourcesReleased = _encounterScenarioOverlayUx2LwH.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterScenarioOverlayUx2LwH.SourceSessionRestored;

        var result = _session.RunUx2LwHContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
            _encounterScenarioOverlayUx2LwH.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterScenarioOverlayUx2LwH.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterScenarioApprovalUx2LwH() == originalApproval;

        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_H_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"routes={fiveRoutes.ToString().ToLowerInvariant()} " +
                $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"real_references={realReferences.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }

        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_LW_H_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"creation_explicit={result.ExplicitCreation.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"anchor_validation={result.AnchorValidation.ToString().ToLowerInvariant()} " +
            $"activator_validation={result.ActivatorValidation.ToString().ToLowerInvariant()} " +
            $"lifecycle_validation={result.LifecycleValidation.ToString().ToLowerInvariant()} " +
            $"real_script={result.RealScriptValidation.ToString().ToLowerInvariant()} " +
            $"wave_validation={result.WaveValidation.ToString().ToLowerInvariant()} " +
            $"deterministic_preview={result.DeterministicPreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"collections={result.CollectionsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
