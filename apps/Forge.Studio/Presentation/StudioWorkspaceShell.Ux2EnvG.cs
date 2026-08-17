using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAudioWorkbenchOverlayUx2EnvG _audioWorkbenchOverlayUx2EnvG = null!;
    private Button _audioWorkbenchButtonUx2EnvG = null!;

    private void BuildUx2EnvGActions(HBoxContainer headingRow)
    {
        _audioWorkbenchButtonUx2EnvG = StudioStyle.Button("Atelier audio", compact: true);
        _audioWorkbenchButtonUx2EnvG.TooltipText =
            "Rechercher les ressources, ordonner les cues et simuler modulation, volume et auditeurs.";
        _audioWorkbenchButtonUx2EnvG.Pressed += OpenAudioWorkbenchUx2EnvG;
        headingRow.AddChild(_audioWorkbenchButtonUx2EnvG);
    }

    private void InitializeUx2EnvGOverlay()
    {
        _audioWorkbenchOverlayUx2EnvG = new StudioAudioWorkbenchOverlayUx2EnvG(_session);
        _audioWorkbenchOverlayUx2EnvG.StatusChanged += SetStatus;
        _audioWorkbenchOverlayUx2EnvG.AudioWorkbenchChanged += RefreshAfterUx2EnvGMutation;
        AddChild(_audioWorkbenchOverlayUx2EnvG);
    }

    private void OpenAudioWorkbenchUx2EnvG()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir l’atelier audio.", error: true);
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
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        CloseUx2EnvKOverlay();
        SwitchPage("environment", notify: false);
        _audioWorkbenchOverlayUx2EnvG.Open();
        SetStatus("Atelier audio ouvert · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvGMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvGActionState();
    }

    private void UpdateUx2EnvGActionState()
    {
        if (_audioWorkbenchButtonUx2EnvG is null) return;
        _audioWorkbenchButtonUx2EnvG.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvGOverlay()
    {
        if (_audioWorkbenchOverlayUx2EnvG is not null) _audioWorkbenchOverlayUx2EnvG.Close();
    }

    internal bool PrepareUx2EnvGEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        CloseUx2EnvKOverlay();
        return _audioWorkbenchOverlayUx2EnvG.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvGSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _audioWorkbenchOverlayUx2EnvG.IsOpen;
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
        var originalApproval = _session.CaptureAudioWorkbenchApprovalUx2EnvG();

        var uiTransaction = _audioWorkbenchOverlayUx2EnvG.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvGEvidence("ux2-env-g-audio-workbench");
        var overlayOpen = _audioWorkbenchOverlayUx2EnvG.IsOpen;
        var choiceCount = _audioWorkbenchOverlayUx2EnvG.ChoiceCount;
        var explicitSelection = _audioWorkbenchOverlayUx2EnvG.HasExplicitSelection;
        var canonicalPreview = _audioWorkbenchOverlayUx2EnvG.CanonicalPreviewReady;
        var completeCollections = _audioWorkbenchOverlayUx2EnvG.ShowsCompleteCollections;
        var searchAndWaveform = _audioWorkbenchOverlayUx2EnvG.ShowsSearchAndWaveform;
        var structuredDiagnostics = _audioWorkbenchOverlayUx2EnvG.ShowsStructuredDiagnostics;
        var boundedRuntime = _audioWorkbenchOverlayUx2EnvG.ShowsBoundedRuntimeClaim;
        var humanSurface = _audioWorkbenchOverlayUx2EnvG.ShowsHumanSurface;
        var isolatedEvidence = _audioWorkbenchOverlayUx2EnvG.UsesIsolatedEvidence;
        var realModuleSource = _audioWorkbenchOverlayUx2EnvG.UsesRealModuleSource;
        var canonicalFallback = _audioWorkbenchOverlayUx2EnvG.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _audioWorkbenchOverlayUx2EnvG.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeCollections && searchAndWaveform &&
                      structuredDiagnostics && boundedRuntime && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _audioWorkbenchOverlayUx2EnvG.Close();
        var evidenceResourcesReleased = _audioWorkbenchOverlayUx2EnvG.EvidenceResourcesReleased;
        var sourceSessionRestored = _audioWorkbenchOverlayUx2EnvG.SourceSessionRestored;

        var result = _session.RunUx2EnvGContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _audioWorkbenchOverlayUx2EnvG.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreAudioWorkbenchApprovalUx2EnvG(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _audioWorkbenchOverlayUx2EnvG.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAudioWorkbenchApprovalUx2EnvG() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_G_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete={completeCollections.ToString().ToLowerInvariant()} " +
                $"search_waveform={searchAndWaveform.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_G_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"empty_source={result.EmptySourceSupported.ToString().ToLowerInvariant()} " +
            $"search_facets={result.SearchFacets.ToString().ToLowerInvariant()} " +
            $"waveform_governance={result.WaveformAndGovernance.ToString().ToLowerInvariant()} " +
            $"audition_plan_only={result.AuditionPlansOnly.ToString().ToLowerInvariant()} " +
            $"invalid_playlist={result.InvalidPlaylistBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_weight={result.InvalidWeightBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_modulation={result.InvalidModulationBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_mix={result.InvalidMixBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_listener={result.InvalidListenerBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"deterministic_sequence={result.DeterministicSequence.ToString().ToLowerInvariant()} " +
            $"listener_policies={result.GlobalAndPersonalizedMix.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"environment_bcdef={result.EnvironmentLotsPreserved.ToString().ToLowerInvariant()} " +
            $"audio_tails={result.AudioTailsPreserved.ToString().ToLowerInvariant()} " +
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
