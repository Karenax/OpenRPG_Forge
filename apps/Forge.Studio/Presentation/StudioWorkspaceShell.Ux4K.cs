using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureMovementOverlayUx4K _creatureMovementOverlayUx4K = null!;
    private Button _creatureMovementButtonUx4K = null!;

    private void BuildUx4KActions(HBoxContainer headingRow)
    {
        _creatureMovementButtonUx4K = StudioStyle.Button("Déplacement", compact: true);
        _creatureMovementButtonUx4K.TooltipText =
            "Éditer vitesse, accélération, altitude, pente et mode de déplacement.";
        _creatureMovementButtonUx4K.Pressed += OpenCreatureMovementUx4K;
        headingRow.AddChild(_creatureMovementButtonUx4K);
    }

    private void InitializeUx4KOverlay()
    {
        _creatureMovementOverlayUx4K = new StudioCreatureMovementOverlayUx4K(_session);
        _creatureMovementOverlayUx4K.StatusChanged += SetStatus;
        _creatureMovementOverlayUx4K.CreatureChanged += RefreshAfterUx4KMutation;
        AddChild(_creatureMovementOverlayUx4K);
    }

    private void OpenCreatureMovementUx4K()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay();
        _creatureMovementOverlayUx4K.Open();
        SetStatus("Déplacement de créature ouvert · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4KMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4KActionState();
    }

    private void UpdateUx4KActionState()
    {
        if (_creatureMovementButtonUx4K is not null)
            _creatureMovementButtonUx4K.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4KOverlay()
    {
        if (_creatureMovementOverlayUx4K is not null) _creatureMovementOverlayUx4K.Close();
    }

    internal bool PrepareUx4KEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay();
        return _creatureMovementOverlayUx4K.PrepareEvidence(mode);
    }

    internal bool RunUx4KSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureMovementOverlayUx4K.IsOpen;
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
        var originalApproval = _session.CaptureCreatureMovementApprovalUx4K();

        var uiTransaction = _creatureMovementOverlayUx4K.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4KEvidence("ux4-k-creature-movement");
        var overlayOpen = _creatureMovementOverlayUx4K.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureMovementOverlayUx4K.ChoiceCount >= 1 &&
                      _creatureMovementOverlayUx4K.HasExplicitSelection &&
                      _creatureMovementOverlayUx4K.CanonicalPreviewReady &&
                      _creatureMovementOverlayUx4K.ShowsSixModes &&
                      _creatureMovementOverlayUx4K.ShowsCompleteValues &&
                      _creatureMovementOverlayUx4K.ShowsHumanSurface &&
                      _creatureMovementOverlayUx4K.UsesIsolatedEvidence &&
                      _creatureMovementOverlayUx4K.EvidencePreviewDryRun &&
                      _creatureMovementOverlayUx4K.EvidenceSourceUntouched &&
                      (_creatureMovementOverlayUx4K.UsesRealProfileEvidence ^
                       _creatureMovementOverlayUx4K.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureMovementOverlayUx4K.LastEvidenceFailure;
        _creatureMovementOverlayUx4K.CloseEvidence();
        var evidenceReleased = _creatureMovementOverlayUx4K.EvidenceResourcesReleased;
        var sourceRestored = _creatureMovementOverlayUx4K.SourceSessionRestored;

        var result = _session.RunUx4KContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureMovementOverlayUx4K.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureMovementApprovalUx4K(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureMovementOverlayUx4K.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureMovementApprovalUx4K() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4K_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4K_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"six_modes={result.SixModes.ToString().ToLowerInvariant()} " +
                 $"complete_values={result.CompleteValues.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"flight_altitude_blocked={result.FlightAltitudeBlocked.ToString().ToLowerInvariant()} " +
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
