using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureEventsOverlayUx4N _creatureEventsOverlayUx4N = null!;
    private Button _creatureEventsButtonUx4N = null!;

    private void BuildUx4NActions(HBoxContainer headingRow)
    {
        _creatureEventsButtonUx4N = StudioStyle.Button("Événements avancés", compact: true);
        _creatureEventsButtonUx4N.TooltipText =
            "Éditer combat, inventaire, repos, heartbeat et événements utilisateur typés.";
        _creatureEventsButtonUx4N.Pressed += OpenCreatureEventsUx4N;
        headingRow.AddChild(_creatureEventsButtonUx4N);
    }

    private void InitializeUx4NOverlay()
    {
        _creatureEventsOverlayUx4N = new StudioCreatureEventsOverlayUx4N(_session);
        _creatureEventsOverlayUx4N.StatusChanged += SetStatus;
        _creatureEventsOverlayUx4N.CreatureChanged += RefreshAfterUx4NMutation;
        AddChild(_creatureEventsOverlayUx4N);
    }

    private void OpenCreatureEventsUx4N()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay(); CloseUx4MOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        _creatureEventsOverlayUx4N.Open();
        SetStatus("Événements avancés ouverts · aperçu canonique obligatoire");
    }

    private void RefreshAfterUx4NMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4NActionState();
    }

    private void UpdateUx4NActionState()
    {
        if (_creatureEventsButtonUx4N is not null)
            _creatureEventsButtonUx4N.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4NOverlay()
    {
        if (_creatureEventsOverlayUx4N is not null) _creatureEventsOverlayUx4N.Close();
    }

    internal bool PrepareUx4NEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay(); CloseUx4MOverlay();
        CloseUx4OOverlay();
        CloseUx4POverlay();
        return _creatureEventsOverlayUx4N.PrepareEvidence(mode);
    }

    internal bool RunUx4NSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureEventsOverlayUx4N.IsOpen;
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
        var originalApproval = _session.CaptureCreatureEventApprovalUx4N();

        var uiTransaction = _creatureEventsOverlayUx4N.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4NEvidence("ux4-n-creature-events");
        var overlayOpen = _creatureEventsOverlayUx4N.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureEventsOverlayUx4N.ChoiceCount >= 1 &&
                      _creatureEventsOverlayUx4N.HasExplicitSelection &&
                      _creatureEventsOverlayUx4N.CanonicalPreviewReady &&
                      _creatureEventsOverlayUx4N.ShowsLifecycle &&
                      _creatureEventsOverlayUx4N.ShowsHeartbeat &&
                      _creatureEventsOverlayUx4N.ShowsCustomEvents &&
                      _creatureEventsOverlayUx4N.ShowsHumanSurface &&
                      _creatureEventsOverlayUx4N.UsesIsolatedEvidence &&
                      _creatureEventsOverlayUx4N.EvidencePreviewDryRun &&
                      _creatureEventsOverlayUx4N.EvidenceSourceUntouched &&
                      (_creatureEventsOverlayUx4N.UsesRealProfileEvidence ^
                       _creatureEventsOverlayUx4N.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureEventsOverlayUx4N.LastEvidenceFailure;
        _creatureEventsOverlayUx4N.CloseEvidence();
        var evidenceReleased = _creatureEventsOverlayUx4N.EvidenceResourcesReleased;
        var sourceRestored = _creatureEventsOverlayUx4N.SourceSessionRestored;

        var result = _session.RunUx4NContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureEventsOverlayUx4N.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureEventApprovalUx4N(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureEventsOverlayUx4N.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureEventApprovalUx4N() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4N_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4N_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"combat_boundary={result.CombatBoundary.ToString().ToLowerInvariant()} " +
                 $"inventory={result.Inventory.ToString().ToLowerInvariant()} " +
                 $"rest={result.Rest.ToString().ToLowerInvariant()} " +
                 $"heartbeat={result.Heartbeat.ToString().ToLowerInvariant()} " +
                 $"custom_events={result.CustomEvents.ToString().ToLowerInvariant()} " +
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
                 $"damage_death_preserved={result.DamageDeathPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
