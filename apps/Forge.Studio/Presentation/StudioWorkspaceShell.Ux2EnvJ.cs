using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioMapFowLodOverlayUx2EnvJ _mapFowLodOverlayUx2EnvJ = null!;
    private Button _mapFowLodButtonUx2EnvJ = null!;

    private void BuildUx2EnvJActions(HBoxContainer headingRow)
    {
        _mapFowLodButtonUx2EnvJ = StudioStyle.Button("Carte & LOD", compact: true);
        _mapFowLodButtonUx2EnvJ.TooltipText =
            "Éditer carte, brouillard, repères, exploration et budgets LOD/HLOD de la zone.";
        _mapFowLodButtonUx2EnvJ.Pressed += OpenMapFowLodUx2EnvJ;
        headingRow.AddChild(_mapFowLodButtonUx2EnvJ);
    }

    private void InitializeUx2EnvJOverlay()
    {
        _mapFowLodOverlayUx2EnvJ = new StudioMapFowLodOverlayUx2EnvJ(_session);
        _mapFowLodOverlayUx2EnvJ.StatusChanged += SetStatus;
        _mapFowLodOverlayUx2EnvJ.MapLodChanged += RefreshAfterUx2EnvJMutation;
        AddChild(_mapFowLodOverlayUx2EnvJ);
    }

    private void OpenMapFowLodUx2EnvJ()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir sa carte et son LOD.", error: true);
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
        CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        SwitchPage("environment", notify: false);
        _mapFowLodOverlayUx2EnvJ.Open();
        SetStatus("Carte & LOD ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvJMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvJActionState();
    }

    private void UpdateUx2EnvJActionState()
    {
        if (_mapFowLodButtonUx2EnvJ is null) return;
        _mapFowLodButtonUx2EnvJ.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvJOverlay()
    {
        if (_mapFowLodOverlayUx2EnvJ is not null) _mapFowLodOverlayUx2EnvJ.Close();
    }

    internal bool PrepareUx2EnvJEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        return _mapFowLodOverlayUx2EnvJ.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvJSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _mapFowLodOverlayUx2EnvJ.IsOpen;
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
        var originalApproval = _session.CaptureMapLodApprovalUx2EnvJ();

        var uiTransaction = _mapFowLodOverlayUx2EnvJ.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvJEvidence("ux2-env-j-map-fow-lod");
        var overlayOpen = _mapFowLodOverlayUx2EnvJ.IsOpen;
        var choiceCount = _mapFowLodOverlayUx2EnvJ.ChoiceCount;
        var explicitSelection = _mapFowLodOverlayUx2EnvJ.HasExplicitSelection;
        var canonicalPreview = _mapFowLodOverlayUx2EnvJ.CanonicalPreviewReady;
        var mapPlan = _mapFowLodOverlayUx2EnvJ.ShowsMapPlan;
        var hlodPlan = _mapFowLodOverlayUx2EnvJ.ShowsHlodPlan;
        var performancePlan = _mapFowLodOverlayUx2EnvJ.ShowsPerformancePlan;
        var completeCollections = _mapFowLodOverlayUx2EnvJ.ShowsCompleteCollections;
        var structuredDiagnostics = _mapFowLodOverlayUx2EnvJ.ShowsStructuredDiagnostics;
        var boundedRuntime = _mapFowLodOverlayUx2EnvJ.ShowsBoundedRuntimeClaim;
        var humanSurface = _mapFowLodOverlayUx2EnvJ.ShowsHumanSurface;
        var isolatedEvidence = _mapFowLodOverlayUx2EnvJ.UsesIsolatedEvidence;
        var realModuleSource = _mapFowLodOverlayUx2EnvJ.UsesRealModuleSource;
        var canonicalFallback = _mapFowLodOverlayUx2EnvJ.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _mapFowLodOverlayUx2EnvJ.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && mapPlan && hlodPlan && performancePlan &&
                      completeCollections && structuredDiagnostics && boundedRuntime && humanSurface &&
                      isolatedEvidence && (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _mapFowLodOverlayUx2EnvJ.Close();
        var evidenceResourcesReleased = _mapFowLodOverlayUx2EnvJ.EvidenceResourcesReleased;
        var sourceSessionRestored = _mapFowLodOverlayUx2EnvJ.SourceSessionRestored;

        var result = _session.RunUx2EnvJContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _mapFowLodOverlayUx2EnvJ.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreMapLodApprovalUx2EnvJ(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _mapFowLodOverlayUx2EnvJ.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureMapLodApprovalUx2EnvJ() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_J_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"map_plan={mapPlan.ToString().ToLowerInvariant()} " +
                $"hlod_plan={hlodPlan.ToString().ToLowerInvariant()} " +
                $"performance={performancePlan.ToString().ToLowerInvariant()} " +
                $"collections={completeCollections.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"bounded_runtime={boundedRuntime.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
                $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_J_{(valid ? "PASS" : "FAILED")} " +
            $"routes=AREA-029,EE-010 " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_map_source={result.RealMapSourceDetected.ToString().ToLowerInvariant()} " +
            $"invalid_map={result.InvalidMapBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_pin={result.InvalidPinBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_lod={result.InvalidLodBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_override={result.InvalidOverrideBlocked.ToString().ToLowerInvariant()} " +
            $"hlod_budget={result.HlodBudgetBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"map_render_plan={result.MapRenderPlan.ToString().ToLowerInvariant()} " +
            $"reveal_plan={result.RevealPlan.ToString().ToLowerInvariant()} " +
            $"deterministic_hlod={result.DeterministicHlodPlan.ToString().ToLowerInvariant()} " +
            $"performance={result.PerformancePlan.ToString().ToLowerInvariant()} " +
            $"no_runtime_claim={result.NoRuntimeClaim.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"map_identity={result.MapIdentityPreserved.ToString().ToLowerInvariant()} " +
            $"authoring_tails={result.AuthoringTailsPreserved.ToString().ToLowerInvariant()} " +
            $"experience_tails={result.ExperienceTailsPreserved.ToString().ToLowerInvariant()} " +
            $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
            $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
