using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAudioScatterEventsOverlayUx2EnvI _audioScatterEventsOverlayUx2EnvI = null!;
    private Button _audioScatterEventsButtonUx2EnvI = null!;

    private void BuildUx2EnvIActions(HBoxContainer headingRow)
    {
        _audioScatterEventsButtonUx2EnvI = StudioStyle.Button("Dispersion & événements", compact: true);
        _audioScatterEventsButtonUx2EnvI.TooltipText =
            "Éditer les dispersions spatiales et simuler les événements audio typés sans playback Studio.";
        _audioScatterEventsButtonUx2EnvI.Pressed += OpenAudioScatterEventsUx2EnvI;
        headingRow.AddChild(_audioScatterEventsButtonUx2EnvI);
    }

    private void InitializeUx2EnvIOverlay()
    {
        _audioScatterEventsOverlayUx2EnvI = new StudioAudioScatterEventsOverlayUx2EnvI(_session);
        _audioScatterEventsOverlayUx2EnvI.StatusChanged += SetStatus;
        _audioScatterEventsOverlayUx2EnvI.AudioScatterChanged += RefreshAfterUx2EnvIMutation;
        AddChild(_audioScatterEventsOverlayUx2EnvI);
    }

    private void OpenAudioScatterEventsUx2EnvI()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les dispersions audio.", error: true);
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
        SwitchPage("environment", notify: false);
        _audioScatterEventsOverlayUx2EnvI.Open();
        SetStatus("Dispersion & événements ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvIMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvIActionState();
    }

    private void UpdateUx2EnvIActionState()
    {
        if (_audioScatterEventsButtonUx2EnvI is null) return;
        _audioScatterEventsButtonUx2EnvI.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvIOverlay()
    {
        if (_audioScatterEventsOverlayUx2EnvI is not null) _audioScatterEventsOverlayUx2EnvI.Close();
    }

    internal bool PrepareUx2EnvIEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        return _audioScatterEventsOverlayUx2EnvI.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvISmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _audioScatterEventsOverlayUx2EnvI.IsOpen;
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
        var originalApproval = _session.CaptureAudioScatterApprovalUx2EnvI();

        var uiTransaction = _audioScatterEventsOverlayUx2EnvI.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvIEvidence("ux2-env-i-audio-scatter-events");
        var overlayOpen = _audioScatterEventsOverlayUx2EnvI.IsOpen;
        var choiceCount = _audioScatterEventsOverlayUx2EnvI.ChoiceCount;
        var explicitSelection = _audioScatterEventsOverlayUx2EnvI.HasExplicitSelection;
        var canonicalPreview = _audioScatterEventsOverlayUx2EnvI.CanonicalPreviewReady;
        var scatterPlan = _audioScatterEventsOverlayUx2EnvI.ShowsScatterPlan;
        var eventPlan = _audioScatterEventsOverlayUx2EnvI.ShowsTypedEventPlan;
        var structuredDiagnostics = _audioScatterEventsOverlayUx2EnvI.ShowsStructuredDiagnostics;
        var boundedRuntime = _audioScatterEventsOverlayUx2EnvI.ShowsBoundedRuntimeClaim;
        var humanSurface = _audioScatterEventsOverlayUx2EnvI.ShowsHumanSurface;
        var isolatedEvidence = _audioScatterEventsOverlayUx2EnvI.UsesIsolatedEvidence;
        var realModuleSource = _audioScatterEventsOverlayUx2EnvI.UsesRealModuleSource;
        var canonicalFallback = _audioScatterEventsOverlayUx2EnvI.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _audioScatterEventsOverlayUx2EnvI.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && scatterPlan && eventPlan && structuredDiagnostics &&
                      boundedRuntime && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _audioScatterEventsOverlayUx2EnvI.Close();
        var evidenceResourcesReleased = _audioScatterEventsOverlayUx2EnvI.EvidenceResourcesReleased;
        var sourceSessionRestored = _audioScatterEventsOverlayUx2EnvI.SourceSessionRestored;

        var result = _session.RunUx2EnvIContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _audioScatterEventsOverlayUx2EnvI.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreAudioScatterApprovalUx2EnvI(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _audioScatterEventsOverlayUx2EnvI.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAudioScatterApprovalUx2EnvI() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_I_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"scatter_plan={scatterPlan.ToString().ToLowerInvariant()} " +
                $"event_plan={eventPlan.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_I_{(valid ? "PASS" : "FAILED")} " +
            $"routes=AUD-010,AUD-017 " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_module_scatter={result.RealModuleScatterDetected.ToString().ToLowerInvariant()} " +
            $"invalid_cue={result.InvalidCueBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_geometry={result.InvalidGeometryBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_interval={result.InvalidIntervalBlocked.ToString().ToLowerInvariant()} " +
            $"non_finite={result.NonFiniteBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_volume={result.InvalidVolumeBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"deterministic_scatter={result.DeterministicScatter.ToString().ToLowerInvariant()} " +
            $"surface_volume={result.SurfaceAndVolumeScatter.ToString().ToLowerInvariant()} " +
            $"nav_biome_exclusion={result.NavigationBiomeExclusion.ToString().ToLowerInvariant()} " +
            $"typed_events={result.TypedEventSequence.ToString().ToLowerInvariant()} " +
            $"script_snapshot={result.ScriptSnapshotCopied.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"experience_tails={result.ExperienceTailsPreserved.ToString().ToLowerInvariant()} " +
            $"runtime_policy_tails={result.RuntimePolicyTailsPreserved.ToString().ToLowerInvariant()} " +
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
