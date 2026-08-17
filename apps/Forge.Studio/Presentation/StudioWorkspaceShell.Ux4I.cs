using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureLootOverlayUx4I _creatureLootOverlayUx4I = null!;
    private Button _creatureLootButtonUx4I = null!;

    private void BuildUx4IActions(HBoxContainer headingRow)
    {
        _creatureLootButtonUx4I = StudioStyle.Button("Loot, vol et cadavre", compact: true);
        _creatureLootButtonUx4I.TooltipText = "Éditer drops, pickpocket et cadavre de créature.";
        _creatureLootButtonUx4I.Pressed += OpenCreatureLootUx4I; headingRow.AddChild(_creatureLootButtonUx4I);
    }
    private void InitializeUx4IOverlay()
    {
        _creatureLootOverlayUx4I = new StudioCreatureLootOverlayUx4I(_session);
        _creatureLootOverlayUx4I.StatusChanged += SetStatus;
        _creatureLootOverlayUx4I.CreatureChanged += RefreshAfterUx4IMutation; AddChild(_creatureLootOverlayUx4I);
    }
    private void OpenCreatureLootUx4I()
    {
        if (_session.Project is null) { SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true); return; }
        SwitchPage("creatures", notify: false); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay();
        CloseUx4GOverlay(); CloseUx4HOverlay(); _creatureLootOverlayUx4I.Open();
        SetStatus("Loot de créature ouvert · sélection explicite et aperçu canonique obligatoires");
    }
    private void RefreshAfterUx4IMutation() { RefreshAfterUx1AMutation(); UpdateUx4IActionState(); }
    private void UpdateUx4IActionState()
    { if (_creatureLootButtonUx4I is not null) _creatureLootButtonUx4I.Visible = PageId == "creatures" && _session.Project is not null; }
    private void CloseUx4IOverlay()
    { if (_creatureLootOverlayUx4I is not null) _creatureLootOverlayUx4I.Close(); }

    internal bool PrepareUx4IEvidence(string mode)
    {
        SwitchPage("creatures", notify: false); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay();
        CloseUx4GOverlay(); CloseUx4HOverlay(); return _creatureLootOverlayUx4I.PrepareEvidence(mode);
    }

    internal bool RunUx4ISmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _creatureLootOverlayUx4I.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision; var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureCreatureLootApprovalUx4I();

        var uiTransaction = _creatureLootOverlayUx4I.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4IEvidence("ux4-i-creature-loot");
        var overlayOpen = _creatureLootOverlayUx4I.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureLootOverlayUx4I.ChoiceCount >= 1 && _creatureLootOverlayUx4I.HasExplicitSelection &&
                      _creatureLootOverlayUx4I.CanonicalPreviewReady && _creatureLootOverlayUx4I.ShowsDrops &&
                      _creatureLootOverlayUx4I.ShowsPickpocket && _creatureLootOverlayUx4I.ShowsCorpse &&
                      _creatureLootOverlayUx4I.ShowsHumanSurface && _creatureLootOverlayUx4I.UsesIsolatedEvidence &&
                      _creatureLootOverlayUx4I.EvidencePreviewDryRun && _creatureLootOverlayUx4I.EvidenceSourceUntouched &&
                      (_creatureLootOverlayUx4I.UsesRealProfileEvidence ^
                       _creatureLootOverlayUx4I.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureLootOverlayUx4I.LastEvidenceFailure;
        _creatureLootOverlayUx4I.CloseEvidence(); var evidenceReleased = _creatureLootOverlayUx4I.EvidenceResourcesReleased;
        var sourceRestored = _creatureLootOverlayUx4I.SourceSessionRestored;

        var result = _session.RunUx4IContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureLootOverlayUx4I.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureLootApprovalUx4I(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _creatureLootOverlayUx4I.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId && _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureLootApprovalUx4I() == originalApproval;
        if (!uiReady) GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4I_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4I_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"drops={result.Drops.ToString().ToLowerInvariant()} pickpocket={result.Pickpocket.ToString().ToLowerInvariant()} " +
                 $"corpse={result.Corpse.ToString().ToLowerInvariant()} invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"missing_reference_blocked={result.MissingReferenceBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
