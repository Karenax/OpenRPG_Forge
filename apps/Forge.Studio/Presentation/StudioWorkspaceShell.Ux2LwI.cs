using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterLifecycleOverlayUx2LwI _encounterLifecycleOverlayUx2LwI = null!;
    private Button _encounterLifecycleButtonUx2LwI = null!;

    private void BuildUx2LwIActions(HBoxContainer headingRow)
    {
        _encounterLifecycleButtonUx2LwI = StudioStyle.Button("Respawn, faction et événements", compact: true);
        _encounterLifecycleButtonUx2LwI.TooltipText =
            "Configurer respawn, faction réelle et scheduler par aperçu sec canonique.";
        _encounterLifecycleButtonUx2LwI.Pressed += OpenEncounterLifecycleUx2LwI;
        headingRow.AddChild(_encounterLifecycleButtonUx2LwI);
    }

    private void InitializeUx2LwIOverlay()
    {
        _encounterLifecycleOverlayUx2LwI = new(_session);
        _encounterLifecycleOverlayUx2LwI.StatusChanged += SetStatus;
        _encounterLifecycleOverlayUx2LwI.EncounterChanged += RefreshAfterUx2LwIMutation;
        AddChild(_encounterLifecycleOverlayUx2LwI);
    }

    private void OpenEncounterLifecycleUx2LwI()
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
        CloseUx2LwEOverlay(); CloseUx2LwFOverlay(); CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterLifecycleOverlayUx2LwI.Open();
        SetStatus("Cycle de vie de rencontre ouvert · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwIMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwIActionState();
    }

    private void UpdateUx2LwIActionState()
    {
        if (_encounterLifecycleButtonUx2LwI is null) return;
        _encounterLifecycleButtonUx2LwI.Visible = PageId == "living-world" &&
                                                 _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwIOverlay()
    {
        if (_encounterLifecycleOverlayUx2LwI is not null)
            _encounterLifecycleOverlayUx2LwI.Close();
    }

    internal bool PrepareUx2LwIEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwFOverlay(); CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        return _encounterLifecycleOverlayUx2LwI.PrepareEvidence(mode);
    }

    internal bool RunUx2LwISmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterLifecycleOverlayUx2LwI.IsOpen;
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
        var originalApproval = _session.CaptureEncounterLifecycleApprovalUx2LwI();

        var uiTransaction = _encounterLifecycleOverlayUx2LwI.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwIEvidence("ux2-lw-i-encounter-runtime-policies");
        var overlayOpen = _encounterLifecycleOverlayUx2LwI.IsOpen;
        var choiceCount = _encounterLifecycleOverlayUx2LwI.ChoiceCount;
        var explicitSelection = _encounterLifecycleOverlayUx2LwI.HasExplicitSelection;
        var canonicalPreview = _encounterLifecycleOverlayUx2LwI.CanonicalPreviewReady;
        var policyRoutes = _encounterLifecycleOverlayUx2LwI.ShowsPolicyRoutes;
        var honestBoundary = _encounterLifecycleOverlayUx2LwI.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _encounterLifecycleOverlayUx2LwI.ShowsStructuredDiagnostics;
        var humanSurface = _encounterLifecycleOverlayUx2LwI.ShowsHumanSurface;
        var realFactions = _encounterLifecycleOverlayUx2LwI.UsesOnlyRealFactions;
        var isolatedEvidence = _encounterLifecycleOverlayUx2LwI.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterLifecycleOverlayUx2LwI.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && policyRoutes && honestBoundary &&
                      structuredDiagnostics && humanSurface && realFactions && isolatedEvidence &&
                      evidenceSourceUntouched;
        _encounterLifecycleOverlayUx2LwI.Close();
        var evidenceResourcesReleased = _encounterLifecycleOverlayUx2LwI.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterLifecycleOverlayUx2LwI.SourceSessionRestored;

        var result = _session.RunUx2LwIContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterLifecycleOverlayUx2LwI.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterLifecycleOverlayUx2LwI.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterLifecycleApprovalUx2LwI() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_I_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                        $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                        $"routes={policyRoutes.ToString().ToLowerInvariant()} " +
                        $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                        $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"real_factions={realFactions.ToString().ToLowerInvariant()} " +
                        $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_I_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
                 $"optional_fallback={result.OptionalFallback.ToString().ToLowerInvariant()} " +
                 $"respawn_validation={result.RespawnValidation.ToString().ToLowerInvariant()} " +
                 $"faction_validation={result.FactionValidation.ToString().ToLowerInvariant()} " +
                 $"scheduler_validation={result.SchedulerValidation.ToString().ToLowerInvariant()} " +
                 $"runtime_resolution={result.RuntimeResolution.ToString().ToLowerInvariant()} " +
                 $"dormant_preserved={result.DormantPreserved.ToString().ToLowerInvariant()} " +
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
