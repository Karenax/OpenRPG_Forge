using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCompanionAuthoringOverlayUx4U _companionAuthoringOverlayUx4U = null!;
    private Button _companionAuthoringButtonUx4U = null!;

    private void BuildUx4UActions(HBoxContainer headingRow)
    {
        _companionAuthoringButtonUx4U = StudioStyle.Button("Compagnons", compact: true);
        _companionAuthoringButtonUx4U.TooltipText =
            "Recrutement, contrôle, progression, récit et validation dans une commande canonique.";
        _companionAuthoringButtonUx4U.Pressed += OpenCompanionAuthoringUx4U;
        headingRow.AddChild(_companionAuthoringButtonUx4U);
    }

    private void InitializeUx4UOverlay()
    {
        _companionAuthoringOverlayUx4U = new(_session);
        _companionAuthoringOverlayUx4U.StatusChanged += SetStatus;
        _companionAuthoringOverlayUx4U.CompanionChanged += RefreshAfterUx4UMutation;
        AddChild(_companionAuthoringOverlayUx4U);
    }

    private void OpenCompanionAuthoringUx4U()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses compagnons.", error: true);
            return;
        }
        SwitchPage("social-content", notify: false);
        CloseUx4AOverlay(); CloseUx4BOverlay(); CloseUx4COverlay(); CloseUx4TOverlay();
        _companionAuthoringOverlayUx4U.Open();
        SetStatus("Compagnons · sélection ou création explicite, cinq onglets et aperçu canonique.", false);
    }

    private void RefreshAfterUx4UMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx4UActionState();
    }

    private void UpdateUx4UActionState()
    {
        if (_companionAuthoringButtonUx4U is not null)
            _companionAuthoringButtonUx4U.Visible = PageId == "social-content" && _session.Project is not null;
    }

    private void CloseUx4UOverlay()
    {
        if (_companionAuthoringOverlayUx4U is not null) _companionAuthoringOverlayUx4U.Close();
    }

    internal bool PrepareUx4UEvidence(string mode)
    {
        if (PageId != "social-content") SwitchPage("social-content", notify: false);
        CloseUx4AOverlay(); CloseUx4BOverlay(); CloseUx4COverlay(); CloseUx4TOverlay();
        _companionAuthoringOverlayUx4U.Open();
        return _companionAuthoringOverlayUx4U.PrepareEvidence(mode);
    }

    internal void ReleaseUx4UAutomatedEvidence() => _companionAuthoringOverlayUx4U.CloseEvidence();

    internal bool RunUx4USmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _companionAuthoringOverlayUx4U.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureCompanionApprovalUx4U();
        var uiTransaction = _companionAuthoringOverlayUx4U.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4UEvidence("ux4-u-companion-authoring");
        var overlayOpen = _companionAuthoringOverlayUx4U.IsOpen;
        var uiReady = evidencePrepared && PageId == "social-content" && overlayOpen &&
                      _companionAuthoringOverlayUx4U.CompanionChoiceCount > 0 &&
                      _companionAuthoringOverlayUx4U.HasExplicitSelection &&
                      _companionAuthoringOverlayUx4U.CanonicalPreviewReady &&
                      _companionAuthoringOverlayUx4U.ShowsRecruitment &&
                      _companionAuthoringOverlayUx4U.ShowsControl &&
                      _companionAuthoringOverlayUx4U.ShowsNarrative &&
                      _companionAuthoringOverlayUx4U.ShowsExperience &&
                      _companionAuthoringOverlayUx4U.ShowsValidation &&
                      _companionAuthoringOverlayUx4U.ShowsHumanSurface &&
                      _companionAuthoringOverlayUx4U.UsesIsolatedEvidence &&
                      _companionAuthoringOverlayUx4U.EvidencePreviewDryRun &&
                      _companionAuthoringOverlayUx4U.EvidenceSourceUntouched &&
                      (_companionAuthoringOverlayUx4U.UsesRealModuleEvidence ^
                       _companionAuthoringOverlayUx4U.UsesCanonicalFallbackEvidence);
        var failure = _companionAuthoringOverlayUx4U.LastEvidenceFailure;
        _companionAuthoringOverlayUx4U.CloseEvidence();
        var released = _companionAuthoringOverlayUx4U.EvidenceResourcesReleased;
        var restored = _companionAuthoringOverlayUx4U.SourceSessionRestored;
        var result = _session.RunUx4UContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _companionAuthoringOverlayUx4U.RestoreSourceSurfaceAfterSmoke();
        else _companionAuthoringOverlayUx4U.HideSourceSurfaceAfterSmoke();
        _session.RestoreCompanionApprovalUx4U(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _companionAuthoringOverlayUx4U.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCompanionApprovalUx4U() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4U_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={failure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && released && restored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4U_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"recruitable_dialogues={result.RecruitableDialogues.ToString().ToLowerInvariant()} " +
                 $"recruit_dismiss_party={result.RecruitDismissParty.ToString().ToLowerInvariant()} " +
                 $"ownership_movement={result.OwnershipMovement.ToString().ToLowerInvariant()} " +
                 $"tactical_ai_formation={result.TacticalAiFormation.ToString().ToLowerInvariant()} " +
                 $"equipment_progression={result.EquipmentProgression.ToString().ToLowerInvariant()} " +
                 $"relationship_banter_quest={result.RelationshipBanterQuest.ToString().ToLowerInvariant()} " +
                 $"death_cohesion={result.DeathCohesion.ToString().ToLowerInvariant()} " +
                 $"trap_abilities_direct={result.TrapAbilitiesDirectControl.ToString().ToLowerInvariant()} " +
                 $"hud_party_split={result.HudPartySplit.ToString().ToLowerInvariant()} " +
                 $"validation={result.Validation.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
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
