using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAudioRangeControlOverlayUx2EnvH _audioRangeControlOverlayUx2EnvH = null!;
    private Button _audioRangeControlButtonUx2EnvH = null!;

    private void BuildUx2EnvHActions(HBoxContainer headingRow)
    {
        _audioRangeControlButtonUx2EnvH = StudioStyle.Button("Portée & scripts audio", compact: true);
        _audioRangeControlButtonUx2EnvH.TooltipText =
            "Éditer la portée audible, l’atténuation et le contrôle ForgeScript de la zone.";
        _audioRangeControlButtonUx2EnvH.Pressed += OpenAudioRangeControlUx2EnvH;
        headingRow.AddChild(_audioRangeControlButtonUx2EnvH);
    }

    private void InitializeUx2EnvHOverlay()
    {
        _audioRangeControlOverlayUx2EnvH = new StudioAudioRangeControlOverlayUx2EnvH(_session);
        _audioRangeControlOverlayUx2EnvH.StatusChanged += SetStatus;
        _audioRangeControlOverlayUx2EnvH.AudioRangeChanged += RefreshAfterUx2EnvHMutation;
        AddChild(_audioRangeControlOverlayUx2EnvH);
    }

    private void OpenAudioRangeControlUx2EnvH()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir la portée audio.", error: true);
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
        SwitchPage("environment", notify: false);
        _audioRangeControlOverlayUx2EnvH.Open();
        SetStatus("Portée & contrôle audio ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvHMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvHActionState();
    }

    private void UpdateUx2EnvHActionState()
    {
        if (_audioRangeControlButtonUx2EnvH is null) return;
        _audioRangeControlButtonUx2EnvH.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvHOverlay()
    {
        if (_audioRangeControlOverlayUx2EnvH is not null) _audioRangeControlOverlayUx2EnvH.Close();
    }

    internal bool PrepareUx2EnvHEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        return _audioRangeControlOverlayUx2EnvH.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvHSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _audioRangeControlOverlayUx2EnvH.IsOpen;
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
        var originalApproval = _session.CaptureAudioRangeApprovalUx2EnvH();

        var uiTransaction = _audioRangeControlOverlayUx2EnvH.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvHEvidence("ux2-env-h-audio-range-control");
        var overlayOpen = _audioRangeControlOverlayUx2EnvH.IsOpen;
        var choiceCount = _audioRangeControlOverlayUx2EnvH.ChoiceCount;
        var explicitSelection = _audioRangeControlOverlayUx2EnvH.HasExplicitSelection;
        var canonicalPreview = _audioRangeControlOverlayUx2EnvH.CanonicalPreviewReady;
        var rangePlan = _audioRangeControlOverlayUx2EnvH.ShowsRangePlan;
        var scriptPlan = _audioRangeControlOverlayUx2EnvH.ShowsScriptControlPlan;
        var structuredDiagnostics = _audioRangeControlOverlayUx2EnvH.ShowsStructuredDiagnostics;
        var boundedRuntime = _audioRangeControlOverlayUx2EnvH.ShowsBoundedRuntimeClaim;
        var humanSurface = _audioRangeControlOverlayUx2EnvH.ShowsHumanSurface;
        var isolatedEvidence = _audioRangeControlOverlayUx2EnvH.UsesIsolatedEvidence;
        var realModuleSource = _audioRangeControlOverlayUx2EnvH.UsesRealModuleSource;
        var canonicalFallback = _audioRangeControlOverlayUx2EnvH.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _audioRangeControlOverlayUx2EnvH.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && rangePlan && scriptPlan && structuredDiagnostics &&
                      boundedRuntime && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _audioRangeControlOverlayUx2EnvH.Close();
        var evidenceResourcesReleased = _audioRangeControlOverlayUx2EnvH.EvidenceResourcesReleased;
        var sourceSessionRestored = _audioRangeControlOverlayUx2EnvH.SourceSessionRestored;

        var result = _session.RunUx2EnvHContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _audioRangeControlOverlayUx2EnvH.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreAudioRangeApprovalUx2EnvH(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _audioRangeControlOverlayUx2EnvH.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAudioRangeApprovalUx2EnvH() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_H_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"range_plan={rangePlan.ToString().ToLowerInvariant()} " +
                $"script_plan={scriptPlan.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_H_{(valid ? "PASS" : "FAILED")} " +
            $"routes=AUD-011,AUD-012 " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_spatial_source={result.RealSpatialSourceDetected.ToString().ToLowerInvariant()} " +
            $"invalid_range={result.InvalidRangeBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_curve={result.InvalidCurveBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_absorption={result.InvalidAbsorptionBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_events={result.InvalidEventsBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_api={result.InvalidApiBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"attenuation={result.AttenuationPlan.ToString().ToLowerInvariant()} " +
            $"air_absorption={result.AirAbsorptionPlan.ToString().ToLowerInvariant()} " +
            $"debug_spheres={result.DebugSpherePlan.ToString().ToLowerInvariant()} " +
            $"script_api={result.ScriptApiCatalog.ToString().ToLowerInvariant()} " +
            $"typed_control={result.TypedControlPlan.ToString().ToLowerInvariant()} " +
            $"authority={result.AuthorityDiagnostic.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"environment_bcdefg={result.EnvironmentLotsPreserved.ToString().ToLowerInvariant()} " +
            $"audio_tails={result.AudioTailsPreserved.ToString().ToLowerInvariant()} " +
            $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"bounded_runtime={result.BoundedRuntimeClaim.ToString().ToLowerInvariant()} " +
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
