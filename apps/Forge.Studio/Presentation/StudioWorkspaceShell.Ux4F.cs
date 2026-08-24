using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureRulesOverlayUx4F _creatureRulesOverlayUx4F = null!;
    private Button _creatureRulesButtonUx4F = null!;

    private void BuildUx4FActions(HBoxContainer headingRow)
    {
        _creatureRulesButtonUx4F = StudioStyle.Button("Animation et règles", compact: true);
        _creatureRulesButtonUx4F.TooltipText =
            "Éditer animation set, état initial, ressources, attributs, défenses et sauvegardes.";
        _creatureRulesButtonUx4F.Pressed += OpenCreatureRulesUx4F;
        headingRow.AddChild(_creatureRulesButtonUx4F);
    }

    private void InitializeUx4FOverlay()
    {
        _creatureRulesOverlayUx4F = new StudioCreatureRulesOverlayUx4F(_session);
        _creatureRulesOverlayUx4F.StatusChanged += SetStatus;
        _creatureRulesOverlayUx4F.CreatureChanged += RefreshAfterUx4FMutation;
        AddChild(_creatureRulesOverlayUx4F);
    }

    private void OpenCreatureRulesUx4F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", error: true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay();
        CloseUx4EOverlay();
        CloseUx4GOverlay();
        CloseUx4HOverlay();
        CloseUx4IOverlay();
        CloseUx4JOverlay();
        CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4MOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        CloseUx4QOverlay(); CloseUx4ROverlay(); _creatureRulesOverlayUx4F.Open();
        SetStatus("Animation et règles ouvertes · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4FMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4FActionState();
    }

    private void UpdateUx4FActionState()
    {
        if (_creatureRulesButtonUx4F is not null)
            _creatureRulesButtonUx4F.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4FOverlay()
    {
        if (_creatureRulesOverlayUx4F is not null) _creatureRulesOverlayUx4F.Close();
    }

    internal bool PrepareUx4FEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay();
        CloseUx4EOverlay();
        CloseUx4GOverlay();
        CloseUx4HOverlay();
        CloseUx4IOverlay();
        CloseUx4JOverlay();
        CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4MOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        return _creatureRulesOverlayUx4F.PrepareEvidence(mode);
    }

    internal bool RunUx4FSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureRulesOverlayUx4F.IsOpen;
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
        var originalApproval = _session.CaptureCreatureRulesApprovalUx4F();

        var uiTransaction = _creatureRulesOverlayUx4F.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4FEvidence("ux4-f-creature-animation-rules");
        var overlayOpen = _creatureRulesOverlayUx4F.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureRulesOverlayUx4F.ChoiceCount >= 1 &&
                      _creatureRulesOverlayUx4F.HasExplicitSelection &&
                      _creatureRulesOverlayUx4F.CanonicalPreviewReady &&
                      _creatureRulesOverlayUx4F.ShowsAnimationGraph &&
                      _creatureRulesOverlayUx4F.ShowsInitialState &&
                      _creatureRulesOverlayUx4F.ShowsResources &&
                      _creatureRulesOverlayUx4F.ShowsAttributes &&
                      _creatureRulesOverlayUx4F.ShowsDefenses &&
                      _creatureRulesOverlayUx4F.ShowsHumanSurface &&
                      _creatureRulesOverlayUx4F.UsesIsolatedEvidence &&
                      _creatureRulesOverlayUx4F.EvidencePreviewDryRun &&
                      _creatureRulesOverlayUx4F.EvidenceSourceUntouched &&
                      (_creatureRulesOverlayUx4F.UsesRealProfileEvidence ^
                       _creatureRulesOverlayUx4F.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureRulesOverlayUx4F.LastEvidenceFailure;
        _creatureRulesOverlayUx4F.CloseEvidence();
        var evidenceReleased = _creatureRulesOverlayUx4F.EvidenceResourcesReleased;
        var sourceRestored = _creatureRulesOverlayUx4F.SourceSessionRestored;

        var result = _session.RunUx4FContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureRulesOverlayUx4F.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureRulesApprovalUx4F(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureRulesOverlayUx4F.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureRulesApprovalUx4F() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4F_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4F_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"animation_graph={result.AnimationGraph.ToString().ToLowerInvariant()} " +
                 $"initial_state={result.InitialState.ToString().ToLowerInvariant()} " +
                 $"resources={result.Resources.ToString().ToLowerInvariant()} " +
                 $"attributes={result.Attributes.ToString().ToLowerInvariant()} " +
                 $"defenses={result.Defenses.ToString().ToLowerInvariant()} " +
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
