using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorActionsWorkbenchUx9G _liveDirectorActionsUx9G = null!;
    private Button _liveDirectorActionsButtonUx9G = null!;

    private void BuildUx9GActions(HBoxContainer headingRow)
    {
        _liveDirectorActionsButtonUx9G = StudioStyle.Button("Actions live", compact: true);
        _liveDirectorActionsButtonUx9G.TooltipText =
            "Téléporter, détruire, soigner, placer en limbo et posséder sous autorité.";
        _liveDirectorActionsButtonUx9G.Pressed += OpenLiveDirectorActionsUx9G;
        headingRow.AddChild(_liveDirectorActionsButtonUx9G);
    }

    private void InitializeUx9GOverlay()
    {
        _liveDirectorActionsUx9G = new StudioLiveDirectorActionsWorkbenchUx9G(_session);
        _liveDirectorActionsUx9G.StatusChanged += SetStatus;
        _liveDirectorActionsUx9G.WorkspaceChanged += RefreshAfterUx9GMutation;
        AddChild(_liveDirectorActionsUx9G);
    }

    private void OpenLiveDirectorActionsUx9G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser les actions live.",
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
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _liveDirectorActionsUx9G.Open();
        SetStatus("UX9-G · téléportation, destruction, soin, limbo et possession.");
    }

    private void RefreshAfterUx9GMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9GActionState();
    }

    private void UpdateUx9GActionState()
    {
        if (_liveDirectorActionsButtonUx9G is not null)
            _liveDirectorActionsButtonUx9G.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9GOverlay()
    {
        if (_liveDirectorActionsUx9G is not null) _liveDirectorActionsUx9G.Close();
    }

    internal bool PrepareUx9GEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _liveDirectorActionsUx9G.PrepareEvidence(mode);
    }

    internal bool RunUx9GSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorActionsUx9G.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveActionApprovalUx9G();

        var prepared = PrepareUx9GEvidence("ux9-g-live-director-actions");
        var selection = prepared && _liveDirectorActionsUx9G.HasExplicitSelection;
        var human = prepared && _liveDirectorActionsUx9G.ShowsHumanSurface;
        var preview = prepared && _liveDirectorActionsUx9G.PreviewReady;
        var sourceUntouched = prepared &&
                              _liveDirectorActionsUx9G.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorActionsUx9G.RunUiEvidence();
        _liveDirectorActionsUx9G.CloseEvidence();
        var released = _liveDirectorActionsUx9G.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorActionsUx9G.SourceSessionRestored;
        var contract = _session.RunUx9GContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorActionsUx9G.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorActionsUx9G.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveActionApprovalUx9G(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveActionApprovalUx9G() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorActionsUx9G.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_G_{(valid ? "PASS" : "FAILED")} " +
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
                 $"relocation={contract.RelocationRuntime.ToString().ToLowerInvariant()} " +
                 $"destruction={contract.DestructionRuntime.ToString().ToLowerInvariant()} " +
                 $"healing={contract.HealingRuntime.ToString().ToLowerInvariant()} " +
                 $"limbo={contract.LimboRuntime.ToString().ToLowerInvariant()} " +
                 $"possession={contract.PossessionRuntime.ToString().ToLowerInvariant()} " +
                 $"compensation={contract.Compensation.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9GAutomatedEvidence()
    {
        if (_liveDirectorActionsUx9G is not null) _liveDirectorActionsUx9G.CloseEvidence();
    }
}
