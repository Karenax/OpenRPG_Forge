using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaAudioOverlayUx2EnvE _areaAudioOverlayUx2EnvE = null!;
    private Button _areaAudioButtonUx2EnvE = null!;

    private void BuildUx2EnvEActions(HBoxContainer headingRow)
    {
        _areaAudioButtonUx2EnvE = StudioStyle.Button("Audio & musique", compact: true);
        _areaAudioButtonUx2EnvE.TooltipText =
            "Authorer audio de zone, couches adaptatives, musique, bus et snapshots en deux transactions.";
        _areaAudioButtonUx2EnvE.Pressed += OpenAreaAudioUx2EnvE;
        headingRow.AddChild(_areaAudioButtonUx2EnvE);
    }

    private void InitializeUx2EnvEOverlay()
    {
        _areaAudioOverlayUx2EnvE = new StudioAreaAudioOverlayUx2EnvE(_session);
        _areaAudioOverlayUx2EnvE.StatusChanged += SetStatus;
        _areaAudioOverlayUx2EnvE.AreaAudioChanged += RefreshAfterUx2EnvEMutation;
        AddChild(_areaAudioOverlayUx2EnvE);
    }

    private void OpenAreaAudioUx2EnvE()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son audio.", error: true); return;
        }
        _globalSearchOverlay.Close(); _safeRenameOverlay.Close(); _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close(); _viewportToolsOverlay.Close(); _areaCreationOverlay.Close();
        _areaLoadingOverlay.Close(); _areaLayersOverlay.Close(); _areaDocumentOperationsOverlay.Close();
        _placeableBlueprintOverlay.Close();
        CloseUx1WOverlay(); CloseUx1XOverlay(); CloseUx1YOverlay(); CloseUx1ZOverlay();
        CloseUx1AAOverlay(); CloseUx1ABOverlay(); CloseUx1ACOverlay(); CloseUx1ADOverlay();
        CloseUx1AEOverlay(); CloseUx1AFWorkspace(); CloseUx1AGOverlay(); CloseUx1AHOverlay();
        CloseUx1AIOverlay(); CloseUx1AJOverlay(); CloseUx2AOverlay(); CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay(); CloseUx2EnvDOverlay(); CloseUx2EnvFOverlay(); CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("environment", notify: false); _areaAudioOverlayUx2EnvE.Open();
        SetStatus("Audio de zone ouvert · sélection explicite · deux aperçus secs obligatoires");
    }

    private void RefreshAfterUx2EnvEMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx2EnvEActionState();
    }

    private void UpdateUx2EnvEActionState()
    {
        if (_areaAudioButtonUx2EnvE is null) return;
        _areaAudioButtonUx2EnvE.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvEOverlay()
    {
        if (_areaAudioOverlayUx2EnvE is not null) _areaAudioOverlayUx2EnvE.Close();
    }

    internal bool PrepareUx2EnvEEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvBOverlay(); CloseUx2EnvCOverlay(); CloseUx2EnvDOverlay(); CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        return _areaAudioOverlayUx2EnvE.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvESmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _areaAudioOverlayUx2EnvE.IsOpen;
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
        var originalApproval = _session.CaptureAudioApprovalUx2EnvE();

        var uiTransaction = _areaAudioOverlayUx2EnvE.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvEEvidence("ux2-env-e-area-audio");
        var overlayOpen = _areaAudioOverlayUx2EnvE.IsOpen;
        var choiceCount = _areaAudioOverlayUx2EnvE.ChoiceCount;
        var explicitSelection = _areaAudioOverlayUx2EnvE.HasExplicitSelection;
        var audioPreview = _areaAudioOverlayUx2EnvE.AudioPreviewReady;
        var snapshotPreview = _areaAudioOverlayUx2EnvE.SnapshotPreviewReady;
        var completeCollections = _areaAudioOverlayUx2EnvE.ShowsCompleteCollections;
        var structuredDiagnostics = _areaAudioOverlayUx2EnvE.ShowsStructuredDiagnostics;
        var twoTransactions = _areaAudioOverlayUx2EnvE.ShowsTwoTransactions;
        var humanSurface = _areaAudioOverlayUx2EnvE.ShowsHumanSurface;
        var isolatedEvidence = _areaAudioOverlayUx2EnvE.UsesIsolatedEvidence;
        var realModuleSource = _areaAudioOverlayUx2EnvE.UsesRealModuleSource;
        var canonicalFallback = _areaAudioOverlayUx2EnvE.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _areaAudioOverlayUx2EnvE.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && audioPreview && snapshotPreview && completeCollections &&
                      structuredDiagnostics && twoTransactions && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _areaAudioOverlayUx2EnvE.Close();
        var evidenceResourcesReleased = _areaAudioOverlayUx2EnvE.EvidenceResourcesReleased;
        var sourceSessionRestored = _areaAudioOverlayUx2EnvE.SourceSessionRestored;

        var result = _session.RunUx2EnvEContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _areaAudioOverlayUx2EnvE.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreAudioApprovalUx2EnvE(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _areaAudioOverlayUx2EnvE.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAudioApprovalUx2EnvE() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_E_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"audio_preview={audioPreview.ToString().ToLowerInvariant()} " +
                $"snapshot_preview={snapshotPreview.ToString().ToLowerInvariant()} " +
                $"complete={completeCollections.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"two_transactions={twoTransactions.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
                $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_E_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"empty_source={result.EmptySourceSupported.ToString().ToLowerInvariant()} " +
            $"invalid_audio={result.InvalidAudioBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_layer={result.InvalidLayerBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_music={result.InvalidMusicBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_bus={result.InvalidBusBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_snapshot={result.InvalidSnapshotBlocked.ToString().ToLowerInvariant()} " +
            $"audio_dry_run={result.AudioDryRun.ToString().ToLowerInvariant()} " +
            $"snapshot_dry_run={result.SnapshotDryRun.ToString().ToLowerInvariant()} " +
            $"deterministic_plans={result.DeterministicPlans.ToString().ToLowerInvariant()} " +
            $"no_live_playback={result.NoLivePlaybackClaim.ToString().ToLowerInvariant()} " +
            $"stale_audio={result.StaleAudioBlocked.ToString().ToLowerInvariant()} " +
            $"stale_snapshot={result.StaleSnapshotBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"separate_commands={result.SeparateCommands.ToString().ToLowerInvariant()} " +
            $"two_step_undo_redo={result.TwoStepUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"environment_bcd={result.EnvironmentLotsPreserved.ToString().ToLowerInvariant()} " +
            $"audio_tails={result.AudioTailsPreserved.ToString().ToLowerInvariant()} " +
            $"authoring_tails={result.AuthoringTailsPreserved.ToString().ToLowerInvariant()} " +
            $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
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
