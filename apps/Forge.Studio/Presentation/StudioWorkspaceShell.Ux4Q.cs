using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaVoiceOverlayUx4Q _areaVoiceOverlayUx4Q = null!;
    private Button _areaVoiceButtonUx4Q = null!;

    private void BuildUx4QActions(HBoxContainer headingRow)
    {
        _areaVoiceButtonUx4Q = StudioStyle.Button("Voix localisées", compact: true);
        _areaVoiceButtonUx4Q.TooltipText = "Prises, sous-titres, phonèmes, fallback TTS et consentement.";
        _areaVoiceButtonUx4Q.Pressed += OpenAreaVoiceUx4Q; headingRow.AddChild(_areaVoiceButtonUx4Q);
    }
    private void InitializeUx4QOverlay()
    {
        _areaVoiceOverlayUx4Q = new(_session); _areaVoiceOverlayUx4Q.StatusChanged += SetStatus;
        _areaVoiceOverlayUx4Q.VoiceChanged += RefreshAfterUx4QMutation; AddChild(_areaVoiceOverlayUx4Q);
    }
    private void OpenAreaVoiceUx4Q()
    {
        SwitchPage("creatures"); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay(); CloseUx4LOverlay();
        CloseUx4MOverlay(); CloseUx4NOverlay(); CloseUx4OOverlay(); CloseUx4POverlay();
        CloseUx4ROverlay();
        _areaVoiceOverlayUx4Q.Open(); SetStatus("Voix localisées · sélection explicite requise.", false);
    }
    private void RefreshAfterUx4QMutation() { RefreshAfterUx1AMutation(); UpdateUx4QActionState(); }
    private void UpdateUx4QActionState()
    { if (_areaVoiceButtonUx4Q is not null) _areaVoiceButtonUx4Q.Visible = PageId == "creatures" && _session.Project is not null; }
    private void CloseUx4QOverlay()
    { if (_areaVoiceOverlayUx4Q is not null) _areaVoiceOverlayUx4Q.Close(); }
    internal bool PrepareUx4QEvidence(string mode)
    {
        if (PageId != "creatures") SwitchPage("creatures", notify: false); OpenAreaVoiceUx4Q();
        return _areaVoiceOverlayUx4Q.PrepareEvidence(mode);
    }
    internal bool RunUx4QSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _areaVoiceOverlayUx4Q.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision; var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureAreaVoiceApprovalUx4Q();
        var uiTransaction = _areaVoiceOverlayUx4Q.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4QEvidence("ux4-q-area-voice"); var overlayOpen = _areaVoiceOverlayUx4Q.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _areaVoiceOverlayUx4Q.AreaChoiceCount > 0 && _areaVoiceOverlayUx4Q.CreatureChoiceCount > 0 &&
                      _areaVoiceOverlayUx4Q.HasExplicitSelection && _areaVoiceOverlayUx4Q.CanonicalPreviewReady &&
                      _areaVoiceOverlayUx4Q.ShowsLocalizedTakes && _areaVoiceOverlayUx4Q.ShowsSubtitlePhoneme &&
                      _areaVoiceOverlayUx4Q.ShowsTtsConsent && _areaVoiceOverlayUx4Q.ShowsHumanSurface &&
                      _areaVoiceOverlayUx4Q.UsesIsolatedEvidence && _areaVoiceOverlayUx4Q.EvidencePreviewDryRun &&
                      _areaVoiceOverlayUx4Q.EvidenceSourceUntouched &&
                      (_areaVoiceOverlayUx4Q.UsesRealModuleEvidence ^ _areaVoiceOverlayUx4Q.UsesCanonicalFallbackEvidence);
        var failure = _areaVoiceOverlayUx4Q.LastEvidenceFailure; _areaVoiceOverlayUx4Q.CloseEvidence();
        var released = _areaVoiceOverlayUx4Q.EvidenceResourcesReleased; var restored = _areaVoiceOverlayUx4Q.SourceSessionRestored;
        var result = _session.RunUx4QContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _areaVoiceOverlayUx4Q.RestoreSourceSurfaceAfterSmoke();
        else _areaVoiceOverlayUx4Q.HideSourceSurfaceAfterSmoke();
        _session.RestoreAreaVoiceApprovalUx4Q(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _areaVoiceOverlayUx4Q.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) && ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId && _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAreaVoiceApprovalUx4Q() == originalApproval;
        if (!uiReady) GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4Q_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} open={overlayOpen.ToString().ToLowerInvariant()} failure={failure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction && sourceUnchanged &&
                    released && restored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4Q_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"localized_takes={result.LocalizedTakes.ToString().ToLowerInvariant()} subtitle_phoneme={result.SubtitlePhoneme.ToString().ToLowerInvariant()} " +
                 $"tts_fallback={result.TtsFallback.ToString().ToLowerInvariant()} consent={result.ConsentGovernance.ToString().ToLowerInvariant()} " +
                 $"invalid_consent={result.InvalidConsentBlocked.ToString().ToLowerInvariant()} duplicate_locale={result.DuplicateLocaleBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
