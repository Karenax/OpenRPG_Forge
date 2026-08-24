using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterValidationOverlayUx2LwL _encounterValidationOverlayUx2LwL = null!;
    private Button _encounterValidationButtonUx2LwL = null!;

    private void BuildUx2LwLActions(HBoxContainer headingRow)
    {
        _encounterValidationButtonUx2LwL = StudioStyle.Button("Validation de rencontre", compact: true);
        _encounterValidationButtonUx2LwL.TooltipText =
            "Auditer les autorités canoniques et authorer une correction bornée avant application.";
        _encounterValidationButtonUx2LwL.Pressed += OpenEncounterValidationUx2LwL;
        headingRow.AddChild(_encounterValidationButtonUx2LwL);
    }

    private void InitializeUx2LwLOverlay()
    {
        _encounterValidationOverlayUx2LwL = new(_session);
        _encounterValidationOverlayUx2LwL.StatusChanged += SetStatus;
        _encounterValidationOverlayUx2LwL.EncounterChanged += RefreshAfterUx2LwLMutation;
        AddChild(_encounterValidationOverlayUx2LwL);
    }

    private void OpenEncounterValidationUx2LwL()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir la validation de rencontre.", error: true);
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
        CloseUx2LwIOverlay(); CloseUx2LwJOverlay(); CloseUx2LwKOverlay();
        CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterValidationOverlayUx2LwL.Open();
        SetStatus("Validation de rencontre ouverte · sélection explicite · dry-run obligatoire");
    }

    private void RefreshAfterUx2LwLMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwLActionState();
    }

    private void UpdateUx2LwLActionState()
    {
        if (_encounterValidationButtonUx2LwL is null) return;
        _encounterValidationButtonUx2LwL.Visible = PageId == "living-world" &&
                                                  _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwLOverlay()
    {
        if (_encounterValidationOverlayUx2LwL is not null)
            _encounterValidationOverlayUx2LwL.Close();
    }

    internal bool PrepareUx2LwLEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwFOverlay(); CloseUx2LwGOverlay(); CloseUx2LwHOverlay(); CloseUx2LwIOverlay();
        CloseUx2LwJOverlay(); CloseUx2LwKOverlay();
        return _encounterValidationOverlayUx2LwL.PrepareEvidence(mode);
    }

    internal bool RunUx2LwLSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterValidationOverlayUx2LwL.IsOpen;
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
        var originalApproval = _session.CaptureEncounterValidationApprovalUx2LwL();

        var uiTransaction = _encounterValidationOverlayUx2LwL.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwLEvidence("ux2-lw-l-encounter-validation");
        var overlayOpen = _encounterValidationOverlayUx2LwL.IsOpen;
        var choiceCount = _encounterValidationOverlayUx2LwL.ChoiceCount;
        var explicitSelection = _encounterValidationOverlayUx2LwL.HasExplicitSelection;
        var canonicalPreview = _encounterValidationOverlayUx2LwL.CanonicalPreviewReady;
        var honestBoundary = _encounterValidationOverlayUx2LwL.ShowsHonestOfflineBoundary;
        var structuredDiagnostics = _encounterValidationOverlayUx2LwL.ShowsStructuredDiagnostics;
        var honestCoverage = _encounterValidationOverlayUx2LwL.ShowsHonestCoverage;
        var humanSurface = _encounterValidationOverlayUx2LwL.ShowsHumanSurface;
        var isolatedEvidence = _encounterValidationOverlayUx2LwL.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterValidationOverlayUx2LwL.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && honestBoundary && structuredDiagnostics &&
                      honestCoverage && humanSurface && isolatedEvidence && evidenceSourceUntouched;
        _encounterValidationOverlayUx2LwL.Close();
        var evidenceResourcesReleased = _encounterValidationOverlayUx2LwL.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterValidationOverlayUx2LwL.SourceSessionRestored;

        var result = _session.RunUx2LwLContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterValidationOverlayUx2LwL.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterValidationOverlayUx2LwL.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterValidationApprovalUx2LwL() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_L_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                        $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                        $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                        $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                        $"coverage={honestCoverage.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_L_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
                 $"canonical_aggregation={result.CanonicalAggregation.ToString().ToLowerInvariant()} " +
                 $"real_anchors={result.RealAnchorValidation.ToString().ToLowerInvariant()} " +
                 $"real_scripts={result.RealScriptValidation.ToString().ToLowerInvariant()} " +
                 $"lifecycle={result.LifecycleValidation.ToString().ToLowerInvariant()} " +
                 $"tactical={result.TacticalValidation.ToString().ToLowerInvariant()} " +
                 $"honest_unresolved={result.HonestUnresolvedCoverage.ToString().ToLowerInvariant()} " +
                 $"correction_required={result.CorrectionRequired.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"target_preserved={result.TargetPreserved.ToString().ToLowerInvariant()} " +
                 $"encounter_neighbors={result.EncounterNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"collections={result.CollectionsPreserved.ToString().ToLowerInvariant()} " +
                 $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
                 $"runtime_boundary={result.RuntimeBoundary.ToString().ToLowerInvariant()} " +
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
