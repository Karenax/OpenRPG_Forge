using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureVoiceOverlayUx4L _creatureVoiceOverlayUx4L = null!;
    private Button _creatureVoiceButtonUx4L = null!;

    private void BuildUx4LActions(HBoxContainer headingRow)
    {
        _creatureVoiceButtonUx4L = StudioStyle.Button("Voix et soundset", compact: true);
        _creatureVoiceButtonUx4L.TooltipText =
            "Éditer banques audio, émotions, langue, lipsync, variantes et droits.";
        _creatureVoiceButtonUx4L.Pressed += OpenCreatureVoiceUx4L;
        headingRow.AddChild(_creatureVoiceButtonUx4L);
    }

    private void InitializeUx4LOverlay()
    {
        _creatureVoiceOverlayUx4L = new StudioCreatureVoiceOverlayUx4L(_session);
        _creatureVoiceOverlayUx4L.StatusChanged += SetStatus;
        _creatureVoiceOverlayUx4L.CreatureChanged += RefreshAfterUx4LMutation;
        AddChild(_creatureVoiceOverlayUx4L);
    }

    private void OpenCreatureVoiceUx4L()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4MOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        _creatureVoiceOverlayUx4L.Open();
        SetStatus("Voix de créature ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4LMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4LActionState();
    }
    private void UpdateUx4LActionState()
    {
        if (_creatureVoiceButtonUx4L is not null)
            _creatureVoiceButtonUx4L.Visible = PageId == "creatures" && _session.Project is not null;
    }
    private void CloseUx4LOverlay()
    {
        if (_creatureVoiceOverlayUx4L is not null) _creatureVoiceOverlayUx4L.Close();
    }

    internal bool PrepareUx4LEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4MOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        return _creatureVoiceOverlayUx4L.PrepareEvidence(mode);
    }

    internal bool RunUx4LSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureVoiceOverlayUx4L.IsOpen;
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
        var originalApproval = _session.CaptureCreatureVoiceApprovalUx4L();

        var uiTransaction = _creatureVoiceOverlayUx4L.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4LEvidence("ux4-l-creature-voice");
        var overlayOpen = _creatureVoiceOverlayUx4L.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureVoiceOverlayUx4L.ChoiceCount >= 1 &&
                      _creatureVoiceOverlayUx4L.HasExplicitSelection &&
                      _creatureVoiceOverlayUx4L.CanonicalPreviewReady &&
                      _creatureVoiceOverlayUx4L.ShowsBanksAndEmotions &&
                      _creatureVoiceOverlayUx4L.ShowsLipSyncAndVariants &&
                      _creatureVoiceOverlayUx4L.ShowsGovernance &&
                      _creatureVoiceOverlayUx4L.ShowsHumanSurface &&
                      _creatureVoiceOverlayUx4L.UsesIsolatedEvidence &&
                      _creatureVoiceOverlayUx4L.EvidencePreviewDryRun &&
                      _creatureVoiceOverlayUx4L.EvidenceSourceUntouched &&
                      (_creatureVoiceOverlayUx4L.UsesRealProfileEvidence ^
                       _creatureVoiceOverlayUx4L.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureVoiceOverlayUx4L.LastEvidenceFailure;
        _creatureVoiceOverlayUx4L.CloseEvidence();
        var evidenceReleased = _creatureVoiceOverlayUx4L.EvidenceResourcesReleased;
        var sourceRestored = _creatureVoiceOverlayUx4L.SourceSessionRestored;

        var result = _session.RunUx4LContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureVoiceOverlayUx4L.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureVoiceApprovalUx4L(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _creatureVoiceOverlayUx4L.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureVoiceApprovalUx4L() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4L_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4L_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"banks_emotions={result.BanksAndEmotions.ToString().ToLowerInvariant()} " +
                 $"lipsync_variants={result.LipSyncAndVariants.ToString().ToLowerInvariant()} " +
                 $"governance={result.Governance.ToString().ToLowerInvariant()} " +
                 $"optional_removal={result.OptionalRemoval.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"duplicate_blocked={result.DuplicateBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
