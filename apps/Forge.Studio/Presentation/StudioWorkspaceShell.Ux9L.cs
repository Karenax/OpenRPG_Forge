using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorAccessWorkbenchUx9L _liveDirectorAccessUx9L = null!;
    private Button _liveDirectorAccessButtonUx9L = null!;

    private void BuildUx9LActions(HBoxContainer headingRow)
    {
        _liveDirectorAccessButtonUx9L = StudioStyle.Button("Accès live", compact: true);
        _liveDirectorAccessButtonUx9L.TooltipText =
            "Configurer authentification, inspection, joueurs, audit et chat du Live Director.";
        _liveDirectorAccessButtonUx9L.Pressed += OpenLiveDirectorAccessUx9L;
        headingRow.AddChild(_liveDirectorAccessButtonUx9L);
    }

    private void InitializeUx9LOverlay()
    {
        _liveDirectorAccessUx9L = new StudioLiveDirectorAccessWorkbenchUx9L(_session);
        _liveDirectorAccessUx9L.StatusChanged += SetStatus;
        _liveDirectorAccessUx9L.WorkspaceChanged += RefreshAfterUx9LMutation;
        AddChild(_liveDirectorAccessUx9L);
    }

    private void OpenLiveDirectorAccessUx9L()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer l’accès Live Director.",
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
        CloseUx9KOverlay();
        _liveDirectorAccessUx9L.Open();
        SetStatus("UX9-L · authentification, inspection, joueurs, audit et chat.");
    }

    private void RefreshAfterUx9LMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9LActionState();
    }

    private void UpdateUx9LActionState()
    {
        if (_liveDirectorAccessButtonUx9L is not null)
            _liveDirectorAccessButtonUx9L.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9LOverlay()
    {
        if (_liveDirectorAccessUx9L is not null) _liveDirectorAccessUx9L.Close();
        CloseUx9MOverlay();
    }

    internal bool PrepareUx9LEvidence(string mode)
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
        CloseUx9KOverlay();
        return _liveDirectorAccessUx9L.PrepareEvidence(mode);
    }

    internal bool RunUx9LSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorAccessUx9L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveAccessApprovalUx9L();

        var prepared = PrepareUx9LEvidence("ux9-l-live-director-access");
        var selection = prepared && _liveDirectorAccessUx9L.HasExplicitSelection;
        var human = prepared && _liveDirectorAccessUx9L.ShowsHumanSurface;
        var preview = prepared && _liveDirectorAccessUx9L.PreviewReady;
        var sourceUntouched = prepared && _liveDirectorAccessUx9L.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorAccessUx9L.RunUiEvidence();
        _liveDirectorAccessUx9L.CloseEvidence();
        var released = _liveDirectorAccessUx9L.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorAccessUx9L.SourceSessionRestored;
        var contract = _session.RunUx9LContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorAccessUx9L.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorAccessUx9L.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveAccessApprovalUx9L(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveAccessApprovalUx9L() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorAccessUx9L.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_L_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
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
                 $"authentication={contract.Authentication.ToString().ToLowerInvariant()} " +
                 $"inspection={contract.Inspection.ToString().ToLowerInvariant()} " +
                 $"players={contract.Players.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"chat={contract.Chat.ToString().ToLowerInvariant()} " +
                 $"scripts={contract.ScriptHandles.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9LAutomatedEvidence()
    {
        if (_liveDirectorAccessUx9L is not null) _liveDirectorAccessUx9L.CloseEvidence();
    }
}
