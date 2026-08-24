using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorNarrativeWorkbenchUx9K _liveDirectorNarrativeUx9K = null!;
    private Button _liveDirectorNarrativeButtonUx9K = null!;

    private void BuildUx9KActions(HBoxContainer headingRow)
    {
        _liveDirectorNarrativeButtonUx9K = StudioStyle.Button("Narration live", compact: true);
        _liveDirectorNarrativeButtonUx9K.TooltipText =
            "Gérer une quête ou une rencontre dans une sandbox narrative restaurée.";
        _liveDirectorNarrativeButtonUx9K.Pressed += OpenLiveDirectorNarrativeUx9K;
        headingRow.AddChild(_liveDirectorNarrativeButtonUx9K);
    }

    private void InitializeUx9KOverlay()
    {
        _liveDirectorNarrativeUx9K = new StudioLiveDirectorNarrativeWorkbenchUx9K(_session);
        _liveDirectorNarrativeUx9K.StatusChanged += SetStatus;
        _liveDirectorNarrativeUx9K.WorkspaceChanged += RefreshAfterUx9KMutation;
        AddChild(_liveDirectorNarrativeUx9K);
    }

    private void OpenLiveDirectorNarrativeUx9K()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de diriger la narration live.",
                error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9LOverlay();
        _liveDirectorNarrativeUx9K.Open();
        SetStatus("UX9-K · quêtes et rencontres narratives en direct.");
    }

    private void RefreshAfterUx9KMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9KActionState();
    }

    private void UpdateUx9KActionState()
    {
        if (_liveDirectorNarrativeButtonUx9K is not null)
            _liveDirectorNarrativeButtonUx9K.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9KOverlay()
    {
        if (_liveDirectorNarrativeUx9K is not null) _liveDirectorNarrativeUx9K.Close();
        // Every earlier network overlay already closes UX9-K; keep UX9-L in that shared chain.
        if (_liveDirectorAccessUx9L is not null) _liveDirectorAccessUx9L.Close();
    }

    internal bool PrepareUx9KEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9LOverlay();
        return _liveDirectorNarrativeUx9K.PrepareEvidence(mode);
    }

    internal bool RunUx9KSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorNarrativeUx9K.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveNarrativeApprovalUx9K();

        var prepared = PrepareUx9KEvidence("ux9-k-live-director-narrative");
        var selection = prepared && _liveDirectorNarrativeUx9K.HasExplicitSelection;
        var human = prepared && _liveDirectorNarrativeUx9K.ShowsHumanSurface;
        var preview = prepared && _liveDirectorNarrativeUx9K.PreviewReady;
        var sourceUntouched = prepared && _liveDirectorNarrativeUx9K.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorNarrativeUx9K.RunUiEvidence();
        _liveDirectorNarrativeUx9K.CloseEvidence();
        var released = _liveDirectorNarrativeUx9K.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorNarrativeUx9K.SourceSessionRestored;
        var contract = _session.RunUx9KContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorNarrativeUx9K.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorNarrativeUx9K.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveNarrativeApprovalUx9K(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveNarrativeApprovalUx9K() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorNarrativeUx9K.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_K_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.TwoRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"invalid={contract.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"permission={contract.PermissionBlocked.ToString().ToLowerInvariant()} " +
                 $"confirmation={contract.ConfirmationRequired.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"quest={contract.QuestRuntime.ToString().ToLowerInvariant()} " +
                 $"encounter={contract.EncounterRuntime.ToString().ToLowerInvariant()} " +
                 $"compensation={contract.Compensation.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9KAutomatedEvidence()
    {
        if (_liveDirectorNarrativeUx9K is not null) _liveDirectorNarrativeUx9K.CloseEvidence();
    }
}
