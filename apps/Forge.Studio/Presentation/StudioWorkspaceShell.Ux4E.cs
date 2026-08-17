using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreaturePresentationOverlayUx4E _creaturePresentationOverlayUx4E = null!;
    private Button _creaturePresentationButtonUx4E = null!;

    private void BuildUx4EActions(HBoxContainer headingRow)
    {
        _creaturePresentationButtonUx4E = StudioStyle.Button("Présentation créature", compact: true);
        _creaturePresentationButtonUx4E.TooltipText =
            "Éditer genre, pronoms, portraits, modèle, parties du corps, couleurs, ailes et queues.";
        _creaturePresentationButtonUx4E.Pressed += OpenCreaturePresentationUx4E;
        headingRow.AddChild(_creaturePresentationButtonUx4E);
    }

    private void InitializeUx4EOverlay()
    {
        _creaturePresentationOverlayUx4E = new StudioCreaturePresentationOverlayUx4E(_session);
        _creaturePresentationOverlayUx4E.StatusChanged += SetStatus;
        _creaturePresentationOverlayUx4E.CreatureChanged += RefreshAfterUx4EMutation;
        AddChild(_creaturePresentationOverlayUx4E);
    }

    private void OpenCreaturePresentationUx4E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", error: true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay();
        CloseUx4FOverlay();
        CloseUx4GOverlay();
        CloseUx4HOverlay();
        CloseUx4IOverlay();
        CloseUx4JOverlay();
        CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4MOverlay();
        _creaturePresentationOverlayUx4E.Open();
        SetStatus("Présentation créature ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4EMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4EActionState();
    }

    private void UpdateUx4EActionState()
    {
        if (_creaturePresentationButtonUx4E is not null)
            _creaturePresentationButtonUx4E.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4EOverlay()
    {
        if (_creaturePresentationOverlayUx4E is not null) _creaturePresentationOverlayUx4E.Close();
    }

    internal bool PrepareUx4EEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay();
        CloseUx4FOverlay();
        CloseUx4GOverlay();
        CloseUx4HOverlay();
        CloseUx4IOverlay();
        CloseUx4JOverlay();
        CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4MOverlay();
        return _creaturePresentationOverlayUx4E.PrepareEvidence(mode);
    }

    internal bool RunUx4ESmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creaturePresentationOverlayUx4E.IsOpen;
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
        var originalApproval = _session.CaptureCreaturePresentationApprovalUx4E();

        var uiTransaction = _creaturePresentationOverlayUx4E.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4EEvidence("ux4-e-creature-presentation");
        var overlayOpen = _creaturePresentationOverlayUx4E.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creaturePresentationOverlayUx4E.ChoiceCount >= 1 &&
                      _creaturePresentationOverlayUx4E.HasExplicitSelection &&
                      _creaturePresentationOverlayUx4E.CanonicalPreviewReady &&
                      _creaturePresentationOverlayUx4E.ShowsGenderAndPronouns &&
                      _creaturePresentationOverlayUx4E.ShowsPortraits &&
                      _creaturePresentationOverlayUx4E.ShowsAppearanceAndModel &&
                      _creaturePresentationOverlayUx4E.ShowsBodyParts &&
                      _creaturePresentationOverlayUx4E.ShowsColorsWingsTails &&
                      _creaturePresentationOverlayUx4E.PortraitPreviewReady &&
                      _creaturePresentationOverlayUx4E.ShowsHumanSurface &&
                      _creaturePresentationOverlayUx4E.UsesIsolatedEvidence &&
                      _creaturePresentationOverlayUx4E.EvidencePreviewDryRun &&
                      _creaturePresentationOverlayUx4E.EvidenceSourceUntouched &&
                      (_creaturePresentationOverlayUx4E.UsesRealPresentationEvidence ^
                       _creaturePresentationOverlayUx4E.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creaturePresentationOverlayUx4E.LastEvidenceFailure;
        _creaturePresentationOverlayUx4E.CloseEvidence();
        var evidenceReleased = _creaturePresentationOverlayUx4E.EvidenceResourcesReleased;
        var sourceRestored = _creaturePresentationOverlayUx4E.SourceSessionRestored;

        var result = _session.RunUx4EContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creaturePresentationOverlayUx4E.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreaturePresentationApprovalUx4E(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creaturePresentationOverlayUx4E.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreaturePresentationApprovalUx4E() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4E_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        }
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX4E_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"gender_pronouns={result.GenderAndPronouns.ToString().ToLowerInvariant()} " +
            $"portraits={result.Portraits.ToString().ToLowerInvariant()} " +
            $"appearance_model={result.AppearanceAndModel.ToString().ToLowerInvariant()} " +
            $"body_parts={result.BodyParts.ToString().ToLowerInvariant()} " +
            $"colors_wings_tails={result.ColorsWingsTails.ToString().ToLowerInvariant()} " +
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
