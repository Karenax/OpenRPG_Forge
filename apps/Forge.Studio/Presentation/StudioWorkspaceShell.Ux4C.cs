using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioFactionMatrixOverlayUx4C _factionMatrixOverlayUx4C = null!;
    private Button _factionMatrixButtonUx4C = null!;

    private void BuildUx4CActions(HBoxContainer headingRow)
    {
        _factionMatrixButtonUx4C = StudioStyle.Button("Matrice et validation", compact: true);
        _factionMatrixButtonUx4C.TooltipText =
            "Éditer tags, heatmap, réaction au joueur et lancer la validation factionnelle.";
        _factionMatrixButtonUx4C.Pressed += OpenFactionMatrixUx4C;
        headingRow.AddChild(_factionMatrixButtonUx4C);
    }

    private void InitializeUx4COverlay()
    {
        _factionMatrixOverlayUx4C = new StudioFactionMatrixOverlayUx4C(_session);
        _factionMatrixOverlayUx4C.StatusChanged += SetStatus;
        _factionMatrixOverlayUx4C.FactionChanged += RefreshAfterUx4CMutation;
        AddChild(_factionMatrixOverlayUx4C);
    }

    private void OpenFactionMatrixUx4C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer sa matrice factionnelle.", error: true);
            return;
        }
        CloseUx4AOverlay();
        CloseUx4BOverlay();
        CloseUx4TOverlay();
        CloseUx4UOverlay();
        SwitchPage("social-content", notify: false);
        _factionMatrixOverlayUx4C.Open();
        SetStatus("Matrice factionnelle ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4CMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4CActionState();
    }

    private void UpdateUx4CActionState()
    {
        if (_factionMatrixButtonUx4C is not null)
            _factionMatrixButtonUx4C.Visible = PageId == "social-content" && _session.Project is not null;
    }

    private void CloseUx4COverlay()
    {
        if (_factionMatrixOverlayUx4C is not null) _factionMatrixOverlayUx4C.Close();
    }

    internal bool PrepareUx4CEvidence(string mode)
    {
        SwitchPage("social-content", notify: false);
        CloseUx4AOverlay();
        CloseUx4BOverlay();
        CloseUx4TOverlay();
        CloseUx4UOverlay();
        return _factionMatrixOverlayUx4C.PrepareEvidence(mode);
    }

    internal bool RunUx4CSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _factionMatrixOverlayUx4C.IsOpen;
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
        var originalApproval = _session.CaptureFactionMatrixApprovalUx4C();

        var uiTransaction = _factionMatrixOverlayUx4C.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4CEvidence("ux4-c-faction-matrix");
        var overlayOpen = _factionMatrixOverlayUx4C.IsOpen;
        var uiReady = evidencePrepared && PageId == "social-content" && overlayOpen &&
                      _factionMatrixOverlayUx4C.ChoiceCount >= 3 &&
                      _factionMatrixOverlayUx4C.HasExplicitSelection &&
                      _factionMatrixOverlayUx4C.CanonicalPreviewReady &&
                      _factionMatrixOverlayUx4C.ShowsSemanticTags &&
                      _factionMatrixOverlayUx4C.ShowsMatrixInterface &&
                      _factionMatrixOverlayUx4C.ShowsPlayerReaction &&
                      _factionMatrixOverlayUx4C.ShowsFactionValidation &&
                      _factionMatrixOverlayUx4C.ShowsHumanSurface &&
                      _factionMatrixOverlayUx4C.UsesIsolatedEvidence &&
                      _factionMatrixOverlayUx4C.EvidencePreviewDryRun &&
                      _factionMatrixOverlayUx4C.EvidenceSourceUntouched &&
                      (_factionMatrixOverlayUx4C.UsesRealCryptEvidence ^
                       _factionMatrixOverlayUx4C.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _factionMatrixOverlayUx4C.LastEvidenceFailure;
        _factionMatrixOverlayUx4C.CloseEvidence();
        var evidenceReleased = _factionMatrixOverlayUx4C.EvidenceResourcesReleased;
        var sourceRestored = _factionMatrixOverlayUx4C.SourceSessionRestored;

        var result = _session.RunUx4CContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _factionMatrixOverlayUx4C.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreFactionMatrixApprovalUx4C(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _factionMatrixOverlayUx4C.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureFactionMatrixApprovalUx4C() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4C_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX4C_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"semantic_tags={result.SemanticTags.ToString().ToLowerInvariant()} " +
            $"matrix_interface={result.MatrixInterface.ToString().ToLowerInvariant()} " +
            $"player_reaction={result.PlayerReaction.ToString().ToLowerInvariant()} " +
            $"faction_validation={result.FactionValidation.ToString().ToLowerInvariant()} " +
            $"import_export={result.ImportExport.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
