using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorAuthorityWorkbenchUx9H _liveDirectorAuthorityUx9H = null!;
    private Button _liveDirectorAuthorityButtonUx9H = null!;

    private void BuildUx9HActions(HBoxContainer headingRow)
    {
        _liveDirectorAuthorityButtonUx9H = StudioStyle.Button("Autorité live", compact: true);
        _liveDirectorAuthorityButtonUx9H.TooltipText =
            "Incarner, protéger, ajuster or et expérience, puis commander les portes.";
        _liveDirectorAuthorityButtonUx9H.Pressed += OpenLiveDirectorAuthorityUx9H;
        headingRow.AddChild(_liveDirectorAuthorityButtonUx9H);
    }

    private void InitializeUx9HOverlay()
    {
        _liveDirectorAuthorityUx9H = new StudioLiveDirectorAuthorityWorkbenchUx9H(_session);
        _liveDirectorAuthorityUx9H.StatusChanged += SetStatus;
        _liveDirectorAuthorityUx9H.WorkspaceChanged += RefreshAfterUx9HMutation;
        AddChild(_liveDirectorAuthorityUx9H);
    }

    private void OpenLiveDirectorAuthorityUx9H()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer l’autorité live.",
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
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _liveDirectorAuthorityUx9H.Open();
        SetStatus("UX9-H · incarnation, protection, économie, progression et portes.");
    }

    private void RefreshAfterUx9HMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9HActionState();
    }

    private void UpdateUx9HActionState()
    {
        if (_liveDirectorAuthorityButtonUx9H is not null)
            _liveDirectorAuthorityButtonUx9H.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9HOverlay()
    {
        if (_liveDirectorAuthorityUx9H is not null) _liveDirectorAuthorityUx9H.Close();
    }

    internal bool PrepareUx9HEvidence(string mode)
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
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _liveDirectorAuthorityUx9H.PrepareEvidence(mode);
    }

    internal bool RunUx9HSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorAuthorityUx9H.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveAuthorityApprovalUx9H();

        var prepared = PrepareUx9HEvidence("ux9-h-live-director-authority");
        var selection = prepared && _liveDirectorAuthorityUx9H.HasExplicitSelection;
        var human = prepared && _liveDirectorAuthorityUx9H.ShowsHumanSurface;
        var preview = prepared && _liveDirectorAuthorityUx9H.PreviewReady;
        var sourceUntouched = prepared &&
                              _liveDirectorAuthorityUx9H.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorAuthorityUx9H.RunUiEvidence();
        _liveDirectorAuthorityUx9H.CloseEvidence();
        var released = _liveDirectorAuthorityUx9H.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorAuthorityUx9H.SourceSessionRestored;
        var contract = _session.RunUx9HContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorAuthorityUx9H.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorAuthorityUx9H.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveAuthorityApprovalUx9H(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveAuthorityApprovalUx9H() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorAuthorityUx9H.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_H_{(valid ? "PASS" : "FAILED")} " +
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
                 $"impersonation={contract.ImpersonationRuntime.ToString().ToLowerInvariant()} " +
                 $"protection={contract.ProtectionRuntime.ToString().ToLowerInvariant()} " +
                 $"currency={contract.CurrencyRuntime.ToString().ToLowerInvariant()} " +
                 $"experience={contract.ExperienceRuntime.ToString().ToLowerInvariant()} " +
                 $"door={contract.DoorRuntime.ToString().ToLowerInvariant()} " +
                 $"compensation={contract.Compensation.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9HAutomatedEvidence()
    {
        if (_liveDirectorAuthorityUx9H is not null) _liveDirectorAuthorityUx9H.CloseEvidence();
    }
}
