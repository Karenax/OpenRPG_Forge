using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureRoleBehaviorOverlayUx4M _creatureRoleBehaviorOverlayUx4M = null!;
    private Button _creatureRoleBehaviorButtonUx4M = null!;

    private void BuildUx4MActions(HBoxContainer headingRow)
    {
        _creatureRoleBehaviorButtonUx4M = StudioStyle.Button("Rôle, activité & auto-level", compact: true);
        _creatureRoleBehaviorButtonUx4M.TooltipText =
            "Éditer lore, animations ambiantes et progression déterministe.";
        _creatureRoleBehaviorButtonUx4M.Pressed += OpenCreatureRoleBehaviorUx4M;
        headingRow.AddChild(_creatureRoleBehaviorButtonUx4M);
    }

    private void InitializeUx4MOverlay()
    {
        _creatureRoleBehaviorOverlayUx4M = new StudioCreatureRoleBehaviorOverlayUx4M(_session);
        _creatureRoleBehaviorOverlayUx4M.StatusChanged += SetStatus;
        _creatureRoleBehaviorOverlayUx4M.CreatureChanged += RefreshAfterUx4MMutation;
        AddChild(_creatureRoleBehaviorOverlayUx4M);
    }

    private void OpenCreatureRoleBehaviorUx4M()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        CloseUx4QOverlay(); CloseUx4ROverlay(); _creatureRoleBehaviorOverlayUx4M.Open();
        SetStatus("Rôle, activité et auto-level ouverts · aperçu canonique obligatoire");
    }

    private void RefreshAfterUx4MMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4MActionState();
    }

    private void UpdateUx4MActionState()
    {
        if (_creatureRoleBehaviorButtonUx4M is not null)
            _creatureRoleBehaviorButtonUx4M.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4MOverlay()
    {
        if (_creatureRoleBehaviorOverlayUx4M is not null) _creatureRoleBehaviorOverlayUx4M.Close();
    }

    internal bool PrepareUx4MEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay();
        CloseUx4NOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        return _creatureRoleBehaviorOverlayUx4M.PrepareEvidence(mode);
    }

    internal bool RunUx4MSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureRoleBehaviorOverlayUx4M.IsOpen;
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
        var originalApproval = _session.CaptureCreatureRoleBehaviorApprovalUx4M();

        var uiTransaction = _creatureRoleBehaviorOverlayUx4M.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4MEvidence("ux4-m-creature-role-behavior");
        var overlayOpen = _creatureRoleBehaviorOverlayUx4M.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureRoleBehaviorOverlayUx4M.ChoiceCount >= 1 &&
                      _creatureRoleBehaviorOverlayUx4M.HasExplicitSelection &&
                      _creatureRoleBehaviorOverlayUx4M.CanonicalPreviewReady &&
                      _creatureRoleBehaviorOverlayUx4M.ShowsRoleAndLore &&
                      _creatureRoleBehaviorOverlayUx4M.ShowsAmbientGraph &&
                      _creatureRoleBehaviorOverlayUx4M.ShowsAutoLevel &&
                      _creatureRoleBehaviorOverlayUx4M.ShowsHumanSurface &&
                      _creatureRoleBehaviorOverlayUx4M.UsesIsolatedEvidence &&
                      _creatureRoleBehaviorOverlayUx4M.EvidencePreviewDryRun &&
                      _creatureRoleBehaviorOverlayUx4M.EvidenceSourceUntouched &&
                      (_creatureRoleBehaviorOverlayUx4M.UsesRealProfileEvidence ^
                       _creatureRoleBehaviorOverlayUx4M.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureRoleBehaviorOverlayUx4M.LastEvidenceFailure;
        _creatureRoleBehaviorOverlayUx4M.CloseEvidence();
        var evidenceReleased = _creatureRoleBehaviorOverlayUx4M.EvidenceResourcesReleased;
        var sourceRestored = _creatureRoleBehaviorOverlayUx4M.SourceSessionRestored;

        var result = _session.RunUx4MContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureRoleBehaviorOverlayUx4M.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureRoleBehaviorApprovalUx4M(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureRoleBehaviorOverlayUx4M.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureRoleBehaviorApprovalUx4M() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4M_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4M_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"role_lore={result.RoleAndLore.ToString().ToLowerInvariant()} " +
                 $"ambient_graph={result.AmbientGraph.ToString().ToLowerInvariant()} " +
                 $"auto_level={result.AutoLevel.ToString().ToLowerInvariant()} " +
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
