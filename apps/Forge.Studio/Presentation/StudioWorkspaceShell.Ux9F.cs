using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorWorkspaceWorkbenchUx9F _liveDirectorWorkspaceUx9F = null!;
    private Button _liveDirectorWorkspaceButtonUx9F = null!;

    private void BuildUx9FActions(HBoxContainer headingRow)
    {
        _liveDirectorWorkspaceButtonUx9F =
            StudioStyle.Button("Live Director", compact: true);
        _liveDirectorWorkspaceButtonUx9F.TooltipText =
            "Configurer le client séparé, Creator, spawn, Chooser et focus spectateur.";
        _liveDirectorWorkspaceButtonUx9F.Pressed += OpenLiveDirectorWorkspaceUx9F;
        headingRow.AddChild(_liveDirectorWorkspaceButtonUx9F);
    }

    private void InitializeUx9FOverlay()
    {
        _liveDirectorWorkspaceUx9F = new StudioLiveDirectorWorkspaceWorkbenchUx9F(_session);
        _liveDirectorWorkspaceUx9F.StatusChanged += SetStatus;
        _liveDirectorWorkspaceUx9F.WorkspaceChanged += RefreshAfterUx9FMutation;
        AddChild(_liveDirectorWorkspaceUx9F);
    }

    private void OpenLiveDirectorWorkspaceUx9F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer Live Director.",
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
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _liveDirectorWorkspaceUx9F.Open();
        SetStatus("UX9-F · client séparé, Creator, spawn, Chooser et focus spectateur.");
    }

    private void RefreshAfterUx9FMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9FActionState();
    }

    private void UpdateUx9FActionState()
    {
        if (_liveDirectorWorkspaceButtonUx9F is not null)
            _liveDirectorWorkspaceButtonUx9F.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9FOverlay()
    {
        if (_liveDirectorWorkspaceUx9F is not null)
            _liveDirectorWorkspaceUx9F.Close();
    }

    internal bool PrepareUx9FEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _liveDirectorWorkspaceUx9F.PrepareEvidence(mode);
    }

    internal bool RunUx9FSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorWorkspaceUx9F.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveDirectorApprovalUx9F();

        var prepared = PrepareUx9FEvidence("ux9-f-live-director-workspace");
        var selection = prepared && _liveDirectorWorkspaceUx9F.HasExplicitSelection;
        var human = prepared && _liveDirectorWorkspaceUx9F.ShowsHumanSurface;
        var preview = prepared && _liveDirectorWorkspaceUx9F.PreviewReady;
        var sourceUntouched = prepared &&
                              _liveDirectorWorkspaceUx9F.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorWorkspaceUx9F.RunUiEvidence();
        _liveDirectorWorkspaceUx9F.CloseEvidence();
        var released = _liveDirectorWorkspaceUx9F.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorWorkspaceUx9F.SourceSessionRestored;
        var contract = _session.RunUx9FContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorWorkspaceUx9F.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorWorkspaceUx9F.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveDirectorApprovalUx9F(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveDirectorApprovalUx9F() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorWorkspaceUx9F.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_F_{(valid ? "PASS" : "FAILED")} " +
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
                 $"client={contract.ClientRuntime.ToString().ToLowerInvariant()} " +
                 $"creator={contract.CreatorRuntime.ToString().ToLowerInvariant()} " +
                 $"spawn={contract.SpawnRuntime.ToString().ToLowerInvariant()} " +
                 $"chooser={contract.ChooserRuntime.ToString().ToLowerInvariant()} " +
                 $"focus={contract.FocusRuntime.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9FAutomatedEvidence()
    {
        if (_liveDirectorWorkspaceUx9F is not null)
            _liveDirectorWorkspaceUx9F.CloseEvidence();
    }
}
