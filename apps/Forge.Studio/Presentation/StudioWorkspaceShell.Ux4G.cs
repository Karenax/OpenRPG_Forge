using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureProgressionOverlayUx4G _creatureProgressionOverlayUx4G = null!;
    private Button _creatureProgressionButtonUx4G = null!;

    private void BuildUx4GActions(HBoxContainer headingRow)
    {
        _creatureProgressionButtonUx4G = StudioStyle.Button("Progression et pouvoirs", compact: true);
        _creatureProgressionButtonUx4G.TooltipText =
            "Éditer classes, alignement, compétences, talents et pouvoirs préparés.";
        _creatureProgressionButtonUx4G.Pressed += OpenCreatureProgressionUx4G;
        headingRow.AddChild(_creatureProgressionButtonUx4G);
    }

    private void InitializeUx4GOverlay()
    {
        _creatureProgressionOverlayUx4G = new StudioCreatureProgressionOverlayUx4G(_session);
        _creatureProgressionOverlayUx4G.StatusChanged += SetStatus;
        _creatureProgressionOverlayUx4G.CreatureChanged += RefreshAfterUx4GMutation;
        AddChild(_creatureProgressionOverlayUx4G);
    }

    private void OpenCreatureProgressionUx4G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", error: true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay();
        _creatureProgressionOverlayUx4G.Open();
        SetStatus("Progression ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4GMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx4GActionState();
    }

    private void UpdateUx4GActionState()
    {
        if (_creatureProgressionButtonUx4G is not null)
            _creatureProgressionButtonUx4G.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4GOverlay()
    {
        if (_creatureProgressionOverlayUx4G is not null) _creatureProgressionOverlayUx4G.Close();
    }

    internal bool PrepareUx4GEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay();
        return _creatureProgressionOverlayUx4G.PrepareEvidence(mode);
    }

    internal bool RunUx4GSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureProgressionOverlayUx4G.IsOpen;
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
        var originalApproval = _session.CaptureCreatureProgressionApprovalUx4G();

        var uiTransaction = _creatureProgressionOverlayUx4G.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4GEvidence("ux4-g-creature-progression");
        var overlayOpen = _creatureProgressionOverlayUx4G.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureProgressionOverlayUx4G.ChoiceCount >= 1 &&
                      _creatureProgressionOverlayUx4G.HasExplicitSelection &&
                      _creatureProgressionOverlayUx4G.CanonicalPreviewReady &&
                      _creatureProgressionOverlayUx4G.ShowsTracks &&
                      _creatureProgressionOverlayUx4G.ShowsDisposition &&
                      _creatureProgressionOverlayUx4G.ShowsSkills &&
                      _creatureProgressionOverlayUx4G.ShowsTalents &&
                      _creatureProgressionOverlayUx4G.ShowsLoadout &&
                      _creatureProgressionOverlayUx4G.ShowsHumanSurface &&
                      _creatureProgressionOverlayUx4G.UsesIsolatedEvidence &&
                      _creatureProgressionOverlayUx4G.EvidencePreviewDryRun &&
                      _creatureProgressionOverlayUx4G.EvidenceSourceUntouched &&
                      (_creatureProgressionOverlayUx4G.UsesRealProfileEvidence ^
                       _creatureProgressionOverlayUx4G.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureProgressionOverlayUx4G.LastEvidenceFailure;
        _creatureProgressionOverlayUx4G.CloseEvidence();
        var evidenceReleased = _creatureProgressionOverlayUx4G.EvidenceResourcesReleased;
        var sourceRestored = _creatureProgressionOverlayUx4G.SourceSessionRestored;

        var result = _session.RunUx4GContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureProgressionOverlayUx4G.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureProgressionApprovalUx4G(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureProgressionOverlayUx4G.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureProgressionApprovalUx4G() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4G_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4G_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"classes_levels={result.Tracks.ToString().ToLowerInvariant()} " +
                 $"alignment={result.Disposition.ToString().ToLowerInvariant()} " +
                 $"skills={result.Skills.ToString().ToLowerInvariant()} " +
                 $"talents={result.Talents.ToString().ToLowerInvariant()} " +
                 $"loadout={result.Loadout.ToString().ToLowerInvariant()} " +
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
