using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterTacticalOverlayUx2LwJ _encounterTacticalOverlayUx2LwJ = null!;
    private Button _encounterTacticalButtonUx2LwJ = null!;

    private void BuildUx2LwJActions(HBoxContainer headingRow)
    {
        _encounterTacticalButtonUx2LwJ = StudioStyle.Button("Renforts, visibilité et nettoyage", compact: true);
        _encounterTacticalButtonUx2LwJ.TooltipText =
            "Configurer décisions de renfort, contraintes caméra/LOS et cleanup par aperçu sec canonique.";
        _encounterTacticalButtonUx2LwJ.Pressed += OpenEncounterTacticalUx2LwJ;
        headingRow.AddChild(_encounterTacticalButtonUx2LwJ);
    }

    private void InitializeUx2LwJOverlay()
    {
        _encounterTacticalOverlayUx2LwJ = new(_session);
        _encounterTacticalOverlayUx2LwJ.StatusChanged += SetStatus;
        _encounterTacticalOverlayUx2LwJ.EncounterChanged += RefreshAfterUx2LwJMutation;
        AddChild(_encounterTacticalOverlayUx2LwJ);
    }

    private void OpenEncounterTacticalUx2LwJ()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les politiques de rencontre.", error: true);
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
        CloseUx2LwIOverlay(); CloseUx2LwKOverlay(); CloseUx2LwLOverlay();
        CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterTacticalOverlayUx2LwJ.Open();
        SetStatus("Tactique de rencontre ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwJMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwJActionState();
    }

    private void UpdateUx2LwJActionState()
    {
        if (_encounterTacticalButtonUx2LwJ is null) return;
        _encounterTacticalButtonUx2LwJ.Visible = PageId == "living-world" &&
                                                _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwJOverlay()
    {
        if (_encounterTacticalOverlayUx2LwJ is not null)
            _encounterTacticalOverlayUx2LwJ.Close();
    }

    internal bool PrepareUx2LwJEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwFOverlay(); CloseUx2LwGOverlay(); CloseUx2LwHOverlay(); CloseUx2LwIOverlay();
        CloseUx2LwKOverlay(); CloseUx2LwLOverlay();
        return _encounterTacticalOverlayUx2LwJ.PrepareEvidence(mode);
    }

    internal bool RunUx2LwJSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterTacticalOverlayUx2LwJ.IsOpen;
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
        var originalApproval = _session.CaptureEncounterTacticalApprovalUx2LwJ();

        var uiTransaction = _encounterTacticalOverlayUx2LwJ.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwJEvidence("ux2-lw-j-encounter-tactical");
        var overlayOpen = _encounterTacticalOverlayUx2LwJ.IsOpen;
        var choiceCount = _encounterTacticalOverlayUx2LwJ.ChoiceCount;
        var explicitSelection = _encounterTacticalOverlayUx2LwJ.HasExplicitSelection;
        var canonicalPreview = _encounterTacticalOverlayUx2LwJ.CanonicalPreviewReady;
        var policyRoutes = _encounterTacticalOverlayUx2LwJ.ShowsPolicyRoutes;
        var honestBoundary = _encounterTacticalOverlayUx2LwJ.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _encounterTacticalOverlayUx2LwJ.ShowsStructuredDiagnostics;
        var humanSurface = _encounterTacticalOverlayUx2LwJ.ShowsHumanSurface;
        var realReferences = _encounterTacticalOverlayUx2LwJ.UsesOnlyRealReferences;
        var isolatedEvidence = _encounterTacticalOverlayUx2LwJ.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterTacticalOverlayUx2LwJ.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && policyRoutes && honestBoundary &&
                      structuredDiagnostics && humanSurface && realReferences && isolatedEvidence &&
                      evidenceSourceUntouched;
        _encounterTacticalOverlayUx2LwJ.Close();
        var evidenceResourcesReleased = _encounterTacticalOverlayUx2LwJ.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterTacticalOverlayUx2LwJ.SourceSessionRestored;

        var result = _session.RunUx2LwJContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterTacticalOverlayUx2LwJ.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterTacticalOverlayUx2LwJ.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterTacticalApprovalUx2LwJ() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_J_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                        $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                        $"routes={policyRoutes.ToString().ToLowerInvariant()} " +
                        $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                        $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"real_references={realReferences.ToString().ToLowerInvariant()} " +
                        $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_J_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
                 $"real_references={result.RealReferencesOnly.ToString().ToLowerInvariant()} " +
                 $"optional_fallback={result.OptionalFallback.ToString().ToLowerInvariant()} " +
                 $"reinforcement_validation={result.ReinforcementValidation.ToString().ToLowerInvariant()} " +
                 $"visibility_validation={result.VisibilityValidation.ToString().ToLowerInvariant()} " +
                 $"cleanup_validation={result.CleanupValidation.ToString().ToLowerInvariant()} " +
                 $"runtime_resolution={result.RuntimeResolution.ToString().ToLowerInvariant()} " +
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
