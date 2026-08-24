using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssistedBossOverlayUx4R _assistedBossOverlayUx4R = null!;
    private Button _assistedBossButtonUx4R = null!;

    private void BuildUx4RActions(HBoxContainer headingRow)
    {
        _assistedBossButtonUx4R = StudioStyle.Button("Créer un boss", compact: true);
        _assistedBossButtonUx4R.TooltipText = "Brief, composition déterministe, équilibrage et approbation atomique.";
        _assistedBossButtonUx4R.Pressed += OpenAssistedBossUx4R; headingRow.AddChild(_assistedBossButtonUx4R);
    }
    private void InitializeUx4ROverlay()
    {
        _assistedBossOverlayUx4R = new(_session); _assistedBossOverlayUx4R.StatusChanged += SetStatus;
        _assistedBossOverlayUx4R.BossChanged += RefreshAfterUx4RMutation; AddChild(_assistedBossOverlayUx4R);
    }
    private void OpenAssistedBossUx4R()
    {
        SwitchPage("creatures"); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay(); CloseUx4LOverlay();
        CloseUx4MOverlay(); CloseUx4NOverlay(); CloseUx4OOverlay(); CloseUx4POverlay(); CloseUx4QOverlay();
        _assistedBossOverlayUx4R.Open(); SetStatus("Création de boss · zone et approbation explicites requises.", false);
    }
    private void RefreshAfterUx4RMutation() { RefreshAfterUx1AMutation(); UpdateUx4RActionState(); }
    private void UpdateUx4RActionState()
    { if (_assistedBossButtonUx4R is not null) _assistedBossButtonUx4R.Visible = PageId == "creatures" && _session.Project is not null; }
    private void CloseUx4ROverlay()
    { if (_assistedBossOverlayUx4R is not null) _assistedBossOverlayUx4R.Close(); }
    internal bool PrepareUx4REvidence(string mode)
    {
        if (PageId != "creatures") SwitchPage("creatures", notify: false); OpenAssistedBossUx4R();
        return _assistedBossOverlayUx4R.PrepareEvidence(mode);
    }
    internal void ReleaseUx4RAutomatedEvidence() => _assistedBossOverlayUx4R.CloseEvidence();
    internal bool RunUx4RSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _assistedBossOverlayUx4R.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision; var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureBossApprovalUx4R();
        var uiTransaction = _assistedBossOverlayUx4R.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4REvidence("ux4-r-assisted-boss"); var overlayOpen = _assistedBossOverlayUx4R.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _assistedBossOverlayUx4R.AreaChoiceCount > 0 && _assistedBossOverlayUx4R.HasExplicitSelection &&
                      _assistedBossOverlayUx4R.CanonicalPreviewReady && _assistedBossOverlayUx4R.ShowsBossPackage &&
                      _assistedBossOverlayUx4R.ShowsBalance && _assistedBossOverlayUx4R.ShowsApproval &&
                      _assistedBossOverlayUx4R.ShowsRealScripts && _assistedBossOverlayUx4R.ShowsHumanSurface &&
                      _assistedBossOverlayUx4R.UsesIsolatedEvidence && _assistedBossOverlayUx4R.EvidencePreviewDryRun &&
                      _assistedBossOverlayUx4R.EvidenceSourceUntouched &&
                      (_assistedBossOverlayUx4R.UsesRealModuleEvidence ^ _assistedBossOverlayUx4R.UsesCanonicalFallbackEvidence);
        var failure = _assistedBossOverlayUx4R.LastEvidenceFailure; _assistedBossOverlayUx4R.CloseEvidence();
        var released = _assistedBossOverlayUx4R.EvidenceResourcesReleased;
        var restored = _assistedBossOverlayUx4R.SourceSessionRestored;
        var result = _session.RunUx4RContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _assistedBossOverlayUx4R.RestoreSourceSurfaceAfterSmoke();
        else _assistedBossOverlayUx4R.HideSourceSurfaceAfterSmoke();
        _session.RestoreBossApprovalUx4R(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _assistedBossOverlayUx4R.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) && ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId && _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureBossApprovalUx4R() == originalApproval;
        if (!uiReady) GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4R_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} open={overlayOpen.ToString().ToLowerInvariant()} failure={failure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction && sourceUnchanged &&
                    released && restored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4R_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} real_scripts={result.RealScripts.ToString().ToLowerInvariant()} " +
                 $"abilities={result.Abilities.ToString().ToLowerInvariant()} phases={result.Phases.ToString().ToLowerInvariant()} balance={result.Balance.ToString().ToLowerInvariant()} " +
                 $"approval_required={result.ApprovalRequired.ToString().ToLowerInvariant()} invalid_script={result.InvalidScriptBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
