using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureInteractionOverlayUx4J _creatureInteractionOverlayUx4J = null!;
    private Button _creatureInteractionButtonUx4J = null!;

    private void BuildUx4JActions(HBoxContainer headingRow)
    {
        _creatureInteractionButtonUx4J = StudioStyle.Button("Factions et interactions", compact: true);
        _creatureInteractionButtonUx4J.TooltipText =
            "Éditer factions, perception, dialogues et protection de créature.";
        _creatureInteractionButtonUx4J.Pressed += OpenCreatureInteractionUx4J;
        headingRow.AddChild(_creatureInteractionButtonUx4J);
    }

    private void InitializeUx4JOverlay()
    {
        _creatureInteractionOverlayUx4J = new StudioCreatureInteractionOverlayUx4J(_session);
        _creatureInteractionOverlayUx4J.StatusChanged += SetStatus;
        _creatureInteractionOverlayUx4J.CreatureChanged += RefreshAfterUx4JMutation;
        AddChild(_creatureInteractionOverlayUx4J);
    }

    private void OpenCreatureInteractionUx4J()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay();
        CloseUx4GOverlay(); CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4KOverlay(); CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay(); _creatureInteractionOverlayUx4J.Open();
        SetStatus("Interactions de créature ouvertes · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4JMutation() { RefreshAfterUx1AMutation(); UpdateUx4JActionState(); }
    private void UpdateUx4JActionState()
    {
        if (_creatureInteractionButtonUx4J is not null)
            _creatureInteractionButtonUx4J.Visible = PageId == "creatures" && _session.Project is not null;
    }
    private void CloseUx4JOverlay()
    { if (_creatureInteractionOverlayUx4J is not null) _creatureInteractionOverlayUx4J.Close(); }

    internal bool PrepareUx4JEvidence(string mode)
    {
        SwitchPage("creatures", notify: false); CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay();
        CloseUx4GOverlay(); CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4KOverlay(); CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay();
        return _creatureInteractionOverlayUx4J.PrepareEvidence(mode);
    }

    internal bool RunUx4JSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _creatureInteractionOverlayUx4J.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureCreatureInteractionApprovalUx4J();

        var uiTransaction = _creatureInteractionOverlayUx4J.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4JEvidence("ux4-j-creature-interaction");
        var overlayOpen = _creatureInteractionOverlayUx4J.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureInteractionOverlayUx4J.ChoiceCount >= 1 &&
                      _creatureInteractionOverlayUx4J.HasExplicitSelection &&
                      _creatureInteractionOverlayUx4J.CanonicalPreviewReady &&
                      _creatureInteractionOverlayUx4J.ShowsFactions &&
                      _creatureInteractionOverlayUx4J.ShowsPerception &&
                      _creatureInteractionOverlayUx4J.ShowsDialogue &&
                      _creatureInteractionOverlayUx4J.ShowsProtection &&
                      _creatureInteractionOverlayUx4J.ShowsHumanSurface &&
                      _creatureInteractionOverlayUx4J.UsesIsolatedEvidence &&
                      _creatureInteractionOverlayUx4J.EvidencePreviewDryRun &&
                      _creatureInteractionOverlayUx4J.EvidenceSourceUntouched &&
                      (_creatureInteractionOverlayUx4J.UsesRealProfileEvidence ^
                       _creatureInteractionOverlayUx4J.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureInteractionOverlayUx4J.LastEvidenceFailure;
        _creatureInteractionOverlayUx4J.CloseEvidence();
        var evidenceReleased = _creatureInteractionOverlayUx4J.EvidenceResourcesReleased;
        var sourceRestored = _creatureInteractionOverlayUx4J.SourceSessionRestored;

        var result = _session.RunUx4JContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureInteractionOverlayUx4J.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureInteractionApprovalUx4J(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureInteractionOverlayUx4J.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureInteractionApprovalUx4J() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4J_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4J_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"factions={result.Factions.ToString().ToLowerInvariant()} perception={result.Perception.ToString().ToLowerInvariant()} " +
                 $"dialogue={result.Dialogue.ToString().ToLowerInvariant()} protection={result.Protection.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"missing_faction_blocked={result.MissingFactionBlocked.ToString().ToLowerInvariant()} " +
                 $"missing_dialogue_blocked={result.MissingDialogueBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
