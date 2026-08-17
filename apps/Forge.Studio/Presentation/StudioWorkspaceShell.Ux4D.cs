using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureFoundationOverlayUx4D _creatureFoundationOverlayUx4D = null!;
    private Button _creatureFoundationButtonUx4D = null!;

    private void BuildUx4DActions(HBoxContainer headingRow)
    {
        _creatureFoundationButtonUx4D = StudioStyle.Button("Assistant créature", compact: true);
        _creatureFoundationButtonUx4D.TooltipText =
            "Éditer le blueprint, l’identité localisée, le rôle, la menace et l’espèce par commande canonique.";
        _creatureFoundationButtonUx4D.Pressed += OpenCreatureFoundationUx4D;
        headingRow.AddChild(_creatureFoundationButtonUx4D);
    }

    private void InitializeUx4DOverlay()
    {
        _creatureFoundationOverlayUx4D = new StudioCreatureFoundationOverlayUx4D(_session);
        _creatureFoundationOverlayUx4D.StatusChanged += SetStatus;
        _creatureFoundationOverlayUx4D.CreatureChanged += RefreshAfterUx4DMutation;
        AddChild(_creatureFoundationOverlayUx4D);
    }

    private void OpenCreatureFoundationUx4D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", error: true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4EOverlay();
        CloseUx4FOverlay();
        _creatureFoundationOverlayUx4D.Open();
        SetStatus("Assistant créature ouvert · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4DMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4DActionState();
    }

    private void UpdateUx4DActionState()
    {
        if (_creatureFoundationButtonUx4D is not null)
            _creatureFoundationButtonUx4D.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4DOverlay()
    {
        if (_creatureFoundationOverlayUx4D is not null) _creatureFoundationOverlayUx4D.Close();
    }

    internal bool PrepareUx4DEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4EOverlay();
        CloseUx4FOverlay();
        return _creatureFoundationOverlayUx4D.PrepareEvidence(mode);
    }

    internal bool RunUx4DSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureFoundationOverlayUx4D.IsOpen;
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
        var originalApproval = _session.CaptureCreatureFoundationApprovalUx4D();

        var uiTransaction = _creatureFoundationOverlayUx4D.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4DEvidence("ux4-d-creature-foundation");
        var overlayOpen = _creatureFoundationOverlayUx4D.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureFoundationOverlayUx4D.ChoiceCount >= 1 &&
                      _creatureFoundationOverlayUx4D.HasExplicitSelection &&
                      _creatureFoundationOverlayUx4D.CanonicalPreviewReady &&
                      _creatureFoundationOverlayUx4D.ShowsFoundation &&
                      _creatureFoundationOverlayUx4D.ShowsLocalizedIdentity &&
                      _creatureFoundationOverlayUx4D.ShowsWizard &&
                      _creatureFoundationOverlayUx4D.ShowsRoleAndThreat &&
                      _creatureFoundationOverlayUx4D.ShowsSpecies &&
                      _creatureFoundationOverlayUx4D.ShowsHumanSurface &&
                      _creatureFoundationOverlayUx4D.UsesIsolatedEvidence &&
                      _creatureFoundationOverlayUx4D.EvidencePreviewDryRun &&
                      _creatureFoundationOverlayUx4D.EvidenceSourceUntouched &&
                      (_creatureFoundationOverlayUx4D.UsesRealCryptEvidence ^
                       _creatureFoundationOverlayUx4D.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureFoundationOverlayUx4D.LastEvidenceFailure;
        _creatureFoundationOverlayUx4D.CloseEvidence();
        var evidenceReleased = _creatureFoundationOverlayUx4D.EvidenceResourcesReleased;
        var sourceRestored = _creatureFoundationOverlayUx4D.SourceSessionRestored;

        var result = _session.RunUx4DContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureFoundationOverlayUx4D.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureFoundationApprovalUx4D(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureFoundationOverlayUx4D.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureFoundationApprovalUx4D() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4D_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        }
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX4D_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"foundation={result.FoundationInitialization.ToString().ToLowerInvariant()} " +
            $"localized_identity={result.LocalizedIdentity.ToString().ToLowerInvariant()} " +
            $"wizard={result.Wizard.ToString().ToLowerInvariant()} " +
            $"role_threat={result.RoleAndThreat.ToString().ToLowerInvariant()} " +
            $"species={result.Species.ToString().ToLowerInvariant()} " +
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
