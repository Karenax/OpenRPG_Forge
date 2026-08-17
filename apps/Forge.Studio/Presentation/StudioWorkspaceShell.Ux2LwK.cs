using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterStatisticsOverlayUx2LwK _encounterStatisticsOverlayUx2LwK = null!;
    private Button _encounterStatisticsButtonUx2LwK = null!;

    private void BuildUx2LwKActions(HBoxContainer headingRow)
    {
        _encounterStatisticsButtonUx2LwK = StudioStyle.Button("Statistiques de rencontre", compact: true);
        _encounterStatisticsButtonUx2LwK.TooltipText =
            "Simuler offline coûts, durées, récompenses, variances et percentiles avant application.";
        _encounterStatisticsButtonUx2LwK.Pressed += OpenEncounterStatisticsUx2LwK;
        headingRow.AddChild(_encounterStatisticsButtonUx2LwK);
    }

    private void InitializeUx2LwKOverlay()
    {
        _encounterStatisticsOverlayUx2LwK = new(_session);
        _encounterStatisticsOverlayUx2LwK.StatusChanged += SetStatus;
        _encounterStatisticsOverlayUx2LwK.EncounterChanged += RefreshAfterUx2LwKMutation;
        AddChild(_encounterStatisticsOverlayUx2LwK);
    }

    private void OpenEncounterStatisticsUx2LwK()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les statistiques de rencontre.", error: true);
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
        CloseUx1WOverlay(); CloseUx1XOverlay(); CloseUx1YOverlay(); CloseUx1ZOverlay();
        CloseUx1AAOverlay(); CloseUx1ABOverlay(); CloseUx1ACOverlay(); CloseUx1ADOverlay();
        CloseUx1AEOverlay(); CloseUx1AFWorkspace(); CloseUx1AGOverlay(); CloseUx1AHOverlay();
        CloseUx1AIOverlay(); CloseUx1AJOverlay();
        CloseUx2AOverlay(); CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay();
        CloseUx2LwEOverlay(); CloseUx2LwFOverlay(); CloseUx2LwGOverlay(); CloseUx2LwHOverlay();
        CloseUx2LwIOverlay(); CloseUx2LwJOverlay();
        CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterStatisticsOverlayUx2LwK.Open();
        SetStatus("Statistiques de rencontre ouvertes · sélection explicite · simulation offline obligatoire");
    }

    private void RefreshAfterUx2LwKMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwKActionState();
    }

    private void UpdateUx2LwKActionState()
    {
        if (_encounterStatisticsButtonUx2LwK is null) return;
        _encounterStatisticsButtonUx2LwK.Visible = PageId == "living-world" &&
                                                  _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwKOverlay()
    {
        if (_encounterStatisticsOverlayUx2LwK is not null)
            _encounterStatisticsOverlayUx2LwK.Close();
    }

    internal bool PrepareUx2LwKEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwFOverlay(); CloseUx2LwGOverlay(); CloseUx2LwHOverlay(); CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        return _encounterStatisticsOverlayUx2LwK.PrepareEvidence(mode);
    }

    internal bool RunUx2LwKSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterStatisticsOverlayUx2LwK.IsOpen;
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
        var originalApproval = _session.CaptureEncounterStatisticsApprovalUx2LwK();

        var uiTransaction = _encounterStatisticsOverlayUx2LwK.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwKEvidence("ux2-lw-k-encounter-statistics");
        var overlayOpen = _encounterStatisticsOverlayUx2LwK.IsOpen;
        var choiceCount = _encounterStatisticsOverlayUx2LwK.ChoiceCount;
        var explicitSelection = _encounterStatisticsOverlayUx2LwK.HasExplicitSelection;
        var canonicalPreview = _encounterStatisticsOverlayUx2LwK.CanonicalPreviewReady;
        var completeMetrics = _encounterStatisticsOverlayUx2LwK.ShowsCompleteMetrics;
        var honestBoundary = _encounterStatisticsOverlayUx2LwK.ShowsHonestOfflineBoundary;
        var structuredDiagnostics = _encounterStatisticsOverlayUx2LwK.ShowsStructuredDiagnostics;
        var humanSurface = _encounterStatisticsOverlayUx2LwK.ShowsHumanSurface;
        var isolatedEvidence = _encounterStatisticsOverlayUx2LwK.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterStatisticsOverlayUx2LwK.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeMetrics && honestBoundary &&
                      structuredDiagnostics && humanSurface && isolatedEvidence && evidenceSourceUntouched;
        _encounterStatisticsOverlayUx2LwK.Close();
        var evidenceResourcesReleased = _encounterStatisticsOverlayUx2LwK.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterStatisticsOverlayUx2LwK.SourceSessionRestored;

        var result = _session.RunUx2LwKContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterStatisticsOverlayUx2LwK.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterStatisticsOverlayUx2LwK.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterStatisticsApprovalUx2LwK() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_K_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                        $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                        $"metrics={completeMetrics.ToString().ToLowerInvariant()} " +
                        $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                        $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_K_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"dependencies={result.DependenciesRequired.ToString().ToLowerInvariant()} " +
                 $"optional_required={result.OptionalAndRequiredResolution.ToString().ToLowerInvariant()} " +
                 $"configuration_validation={result.ConfigurationValidation.ToString().ToLowerInvariant()} " +
                 $"percentiles_validation={result.PercentileValidation.ToString().ToLowerInvariant()} " +
                 $"overrides_validation={result.OverrideValidation.ToString().ToLowerInvariant()} " +
                 $"seed_range={result.SeedRangeValidation.ToString().ToLowerInvariant()} " +
                 $"deterministic={result.DeterministicSimulation.ToString().ToLowerInvariant()} " +
                 $"metrics={result.CompleteMetrics.ToString().ToLowerInvariant()} " +
                 $"details_policy={result.DetailsPolicy.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"tails={result.EncounterTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"encounter_neighbors={result.EncounterNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"collections={result.CollectionsPreserved.ToString().ToLowerInvariant()} " +
                 $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
                 $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
                 $"offline_boundary={result.OfflineBoundary.ToString().ToLowerInvariant()} " +
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
