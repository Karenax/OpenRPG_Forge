using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterFoundationOverlayUx2LwF _encounterFoundationOverlayUx2LwF = null!;
    private Button _encounterFoundationButtonUx2LwF = null!;

    private void BuildUx2LwFActions(HBoxContainer headingRow)
    {
        _encounterFoundationButtonUx2LwF = StudioStyle.Button("Assistant de rencontre", compact: true);
        _encounterFoundationButtonUx2LwF.TooltipText =
            "Créer ou modifier volume, candidats, difficulté et limites par aperçu sec canonique.";
        _encounterFoundationButtonUx2LwF.Pressed += OpenEncounterFoundationUx2LwF;
        headingRow.AddChild(_encounterFoundationButtonUx2LwF);
    }

    private void InitializeUx2LwFOverlay()
    {
        _encounterFoundationOverlayUx2LwF = new StudioEncounterFoundationOverlayUx2LwF(_session);
        _encounterFoundationOverlayUx2LwF.StatusChanged += SetStatus;
        _encounterFoundationOverlayUx2LwF.EncounterChanged += RefreshAfterUx2LwFMutation;
        AddChild(_encounterFoundationOverlayUx2LwF);
    }

    private void OpenEncounterFoundationUx2LwF()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir l’assistant de rencontre.", error: true);
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
        CloseUx2LwEOverlay(); CloseUx2LwGOverlay(); CloseUx2LwHOverlay(); CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterFoundationOverlayUx2LwF.Open();
        SetStatus("Assistant de rencontre ouvert · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwFMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwFActionState();
    }

    private void UpdateUx2LwFActionState()
    {
        if (_encounterFoundationButtonUx2LwF is null) return;
        _encounterFoundationButtonUx2LwF.Visible = PageId == "living-world" &&
                                                   _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwFOverlay()
    {
        if (_encounterFoundationOverlayUx2LwF is not null)
            _encounterFoundationOverlayUx2LwF.Close();
    }

    internal bool PrepareUx2LwFEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        return _encounterFoundationOverlayUx2LwF.PrepareEvidence(mode);
    }

    internal bool RunUx2LwFSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterFoundationOverlayUx2LwF.IsOpen;
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
        var originalApproval = _session.CaptureEncounterApprovalUx2LwF();

        var uiTransaction = _encounterFoundationOverlayUx2LwF.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwFEvidence("ux2-lw-f-encounter-foundation");
        var overlayOpen = _encounterFoundationOverlayUx2LwF.IsOpen;
        var choiceCount = _encounterFoundationOverlayUx2LwF.ChoiceCount;
        var explicitSelection = _encounterFoundationOverlayUx2LwF.HasExplicitSelection;
        var canonicalPreview = _encounterFoundationOverlayUx2LwF.CanonicalPreviewReady;
        var fiveRoutes = _encounterFoundationOverlayUx2LwF.ShowsFiveRoutes;
        var honestBoundary = _encounterFoundationOverlayUx2LwF.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _encounterFoundationOverlayUx2LwF.ShowsStructuredDiagnostics;
        var humanSurface = _encounterFoundationOverlayUx2LwF.ShowsHumanSurface;
        var realReferences = _encounterFoundationOverlayUx2LwF.UsesRealReferences;
        var isolatedEvidence = _encounterFoundationOverlayUx2LwF.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterFoundationOverlayUx2LwF.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && fiveRoutes && honestBoundary &&
                      structuredDiagnostics && humanSurface && realReferences && isolatedEvidence &&
                      evidenceSourceUntouched;
        _encounterFoundationOverlayUx2LwF.Close();
        var evidenceResourcesReleased = _encounterFoundationOverlayUx2LwF.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterFoundationOverlayUx2LwF.SourceSessionRestored;

        var result = _session.RunUx2LwFContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterFoundationOverlayUx2LwF.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterFoundationOverlayUx2LwF.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterApprovalUx2LwF() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_F_UI_SURFACE_FAILED " +
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
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_F_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"new_assistant={result.NewAssistant.ToString().ToLowerInvariant()} " +
                 $"volume_validation={result.VolumeValidation.ToString().ToLowerInvariant()} " +
                 $"candidate_validation={result.CandidateValidation.ToString().ToLowerInvariant()} " +
                 $"difficulty_validation={result.DifficultyValidation.ToString().ToLowerInvariant()} " +
                 $"count_validation={result.CountValidation.ToString().ToLowerInvariant()} " +
                 $"real_script={result.RealScriptValidation.ToString().ToLowerInvariant()} " +
                 $"deterministic_preview={result.DeterministicPreview.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"collections={result.CollectionPreserved.ToString().ToLowerInvariant()} " +
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
