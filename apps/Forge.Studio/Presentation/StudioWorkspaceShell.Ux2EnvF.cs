using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAudioEmittersOverlayUx2EnvF _audioEmittersOverlayUx2EnvF = null!;
    private Button _audioEmittersButtonUx2EnvF = null!;

    private void BuildUx2EnvFActions(HBoxContainer headingRow)
    {
        _audioEmittersButtonUx2EnvF = StudioStyle.Button("Émetteurs audio", compact: true);
        _audioEmittersButtonUx2EnvF.TooltipText =
            "Authorer émetteurs, clips, boucles et instances 3D avec un aperçu déterministe.";
        _audioEmittersButtonUx2EnvF.Pressed += OpenAudioEmittersUx2EnvF;
        headingRow.AddChild(_audioEmittersButtonUx2EnvF);
    }

    private void InitializeUx2EnvFOverlay()
    {
        _audioEmittersOverlayUx2EnvF = new StudioAudioEmittersOverlayUx2EnvF(_session);
        _audioEmittersOverlayUx2EnvF.StatusChanged += SetStatus;
        _audioEmittersOverlayUx2EnvF.AudioEmittersChanged += RefreshAfterUx2EnvFMutation;
        AddChild(_audioEmittersOverlayUx2EnvF);
    }

    private void OpenAudioEmittersUx2EnvF()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer ses émetteurs audio.", error: true); return;
        }
        _globalSearchOverlay.Close(); _safeRenameOverlay.Close(); _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close(); _viewportToolsOverlay.Close(); _areaCreationOverlay.Close();
        _areaLoadingOverlay.Close(); _areaLayersOverlay.Close(); _areaDocumentOperationsOverlay.Close();
        _placeableBlueprintOverlay.Close();
        CloseUx1WOverlay(); CloseUx1XOverlay(); CloseUx1YOverlay(); CloseUx1ZOverlay();
        CloseUx1AAOverlay(); CloseUx1ABOverlay(); CloseUx1ACOverlay(); CloseUx1ADOverlay();
        CloseUx1AEOverlay(); CloseUx1AFWorkspace(); CloseUx1AGOverlay(); CloseUx1AHOverlay();
        CloseUx1AIOverlay(); CloseUx1AJOverlay(); CloseUx2AOverlay(); CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay(); CloseUx2EnvDOverlay(); CloseUx2EnvEOverlay(); CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        SwitchPage("environment", notify: false); _audioEmittersOverlayUx2EnvF.Open();
        SetStatus("Émetteurs audio ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvFMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx2EnvFActionState();
    }

    private void UpdateUx2EnvFActionState()
    {
        if (_audioEmittersButtonUx2EnvF is null) return;
        _audioEmittersButtonUx2EnvF.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvFOverlay()
    {
        if (_audioEmittersOverlayUx2EnvF is not null) _audioEmittersOverlayUx2EnvF.Close();
    }

    internal bool PrepareUx2EnvFEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        return _audioEmittersOverlayUx2EnvF.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvFSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _audioEmittersOverlayUx2EnvF.IsOpen;
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
        var originalApproval = _session.CaptureAudioEmittersApprovalUx2EnvF();

        var uiTransaction = _audioEmittersOverlayUx2EnvF.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvFEvidence("ux2-env-f-audio-emitters");
        var overlayOpen = _audioEmittersOverlayUx2EnvF.IsOpen;
        var choiceCount = _audioEmittersOverlayUx2EnvF.ChoiceCount;
        var explicitSelection = _audioEmittersOverlayUx2EnvF.HasExplicitSelection;
        var canonicalPreview = _audioEmittersOverlayUx2EnvF.CanonicalPreviewReady;
        var completeCollections = _audioEmittersOverlayUx2EnvF.ShowsCompleteCollections;
        var structuredDiagnostics = _audioEmittersOverlayUx2EnvF.ShowsStructuredDiagnostics;
        var boundedRuntime = _audioEmittersOverlayUx2EnvF.ShowsBoundedRuntimeClaim;
        var humanSurface = _audioEmittersOverlayUx2EnvF.ShowsHumanSurface;
        var isolatedEvidence = _audioEmittersOverlayUx2EnvF.UsesIsolatedEvidence;
        var realModuleSource = _audioEmittersOverlayUx2EnvF.UsesRealModuleSource;
        var canonicalFallback = _audioEmittersOverlayUx2EnvF.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _audioEmittersOverlayUx2EnvF.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeCollections && structuredDiagnostics &&
                      boundedRuntime && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _audioEmittersOverlayUx2EnvF.Close();
        var evidenceResourcesReleased = _audioEmittersOverlayUx2EnvF.EvidenceResourcesReleased;
        var sourceSessionRestored = _audioEmittersOverlayUx2EnvF.SourceSessionRestored;

        var result = _session.RunUx2EnvFContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _audioEmittersOverlayUx2EnvF.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreAudioEmittersApprovalUx2EnvF(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _audioEmittersOverlayUx2EnvF.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAudioEmittersApprovalUx2EnvF() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_F_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete={completeCollections.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_F_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"empty_source={result.EmptySourceSupported.ToString().ToLowerInvariant()} " +
            $"invalid_definition={result.InvalidDefinitionBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_clip={result.InvalidClipBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_loop={result.InvalidLoopBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_spatial={result.InvalidSpatialBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_instance={result.InvalidInstanceBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"deterministic_plans={result.DeterministicPlans.ToString().ToLowerInvariant()} " +
            $"no_live_playback={result.NoLivePlaybackClaim.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"clip_order={result.ClipOrderPreserved.ToString().ToLowerInvariant()} " +
            $"clip_tails={result.HiddenClipTailsPreserved.ToString().ToLowerInvariant()} " +
            $"environment_bcde={result.EnvironmentLotsPreserved.ToString().ToLowerInvariant()} " +
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
