using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureEventAiOverlayUx4P _creatureEventAiOverlayUx4P = null!;
    private Button _creatureEventAiButtonUx4P = null!;

    private void BuildUx4PActions(HBoxContainer headingRow)
    {
        _creatureEventAiButtonUx4P = StudioStyle.Button("Événements & IA", compact: true);
        _creatureEventAiButtonUx4P.TooltipText =
            "Composer les événements de perception, combat, blocage et le contrôleur IA d’une créature.";
        _creatureEventAiButtonUx4P.Pressed += OpenCreatureEventAiUx4P;
        headingRow.AddChild(_creatureEventAiButtonUx4P);
    }

    private void InitializeUx4POverlay()
    {
        _creatureEventAiOverlayUx4P = new StudioCreatureEventAiOverlayUx4P(_session);
        _creatureEventAiOverlayUx4P.StatusChanged += SetStatus;
        _creatureEventAiOverlayUx4P.CreatureChanged += RefreshAfterUx4PMutation;
        AddChild(_creatureEventAiOverlayUx4P);
    }

    private void OpenCreatureEventAiUx4P()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseOtherCreatureOverlaysUx4P();
        _creatureEventAiOverlayUx4P.Open();
        SetStatus("Événements et contrôleur IA ouverts · aperçu canonique obligatoire");
    }

    private void RefreshAfterUx4PMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4PActionState();
    }

    private void UpdateUx4PActionState()
    {
        if (_creatureEventAiButtonUx4P is not null)
            _creatureEventAiButtonUx4P.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4POverlay()
    {
        if (_creatureEventAiOverlayUx4P is not null) _creatureEventAiOverlayUx4P.Close();
    }

    internal bool PrepareUx4PEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseOtherCreatureOverlaysUx4P();
        return _creatureEventAiOverlayUx4P.PrepareEvidence(mode);
    }

    internal bool RunUx4PSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureEventAiOverlayUx4P.IsOpen;
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
        var originalApproval = _session.CaptureCreatureEventAiApprovalUx4P();

        var uiTransaction = _creatureEventAiOverlayUx4P.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4PEvidence("ux4-p-creature-event-ai");
        var overlayOpen = _creatureEventAiOverlayUx4P.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureEventAiOverlayUx4P.ChoiceCount >= 1 &&
                      _creatureEventAiOverlayUx4P.HasExplicitSelection &&
                      _creatureEventAiOverlayUx4P.CanonicalPreviewReady &&
                      _creatureEventAiOverlayUx4P.ShowsEvents &&
                      _creatureEventAiOverlayUx4P.ShowsController &&
                      _creatureEventAiOverlayUx4P.ShowsHumanSurface &&
                      _creatureEventAiOverlayUx4P.UsesIsolatedEvidence &&
                      _creatureEventAiOverlayUx4P.EvidencePreviewDryRun &&
                      _creatureEventAiOverlayUx4P.EvidenceSourceUntouched &&
                      (_creatureEventAiOverlayUx4P.UsesRealProfileEvidence ^
                       _creatureEventAiOverlayUx4P.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureEventAiOverlayUx4P.LastEvidenceFailure;
        _creatureEventAiOverlayUx4P.CloseEvidence();
        var evidenceReleased = _creatureEventAiOverlayUx4P.EvidenceResourcesReleased;
        var sourceRestored = _creatureEventAiOverlayUx4P.SourceSessionRestored;

        var result = _session.RunUx4PContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureEventAiOverlayUx4P.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureEventAiApprovalUx4P(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureEventAiOverlayUx4P.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureEventAiApprovalUx4P() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4P_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4P_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"perception={result.Perception.ToString().ToLowerInvariant()} " +
                 $"physical_attack={result.PhysicalAttack.ToString().ToLowerInvariant()} " +
                 $"ability={result.Ability.ToString().ToLowerInvariant()} " +
                 $"blocked_recovery={result.BlockedRecovery.ToString().ToLowerInvariant()} " +
                 $"ai_controller={result.AiController.ToString().ToLowerInvariant()} " +
                 $"optional_removal={result.OptionalRemoval.ToString().ToLowerInvariant()} " +
                 $"invalid_binding={result.InvalidBindingBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_blackboard={result.InvalidBlackboardBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_recovery={result.InvalidRecoveryBlocked.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherCreatureOverlaysUx4P()
    {
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay(); CloseUx4OOverlay();
    }
}
