using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorProductionWorkbenchUx9M _liveProductionUx9M = null!;
    private Button _liveProductionButtonUx9M = null!;

    private void BuildUx9MActions(HBoxContainer headingRow)
    {
        _liveProductionButtonUx9M = StudioStyle.Button("Production live", compact: true);
        _liveProductionButtonUx9M.TooltipText =
            "Configurer repos, niveau, apparence, groupes et enregistrement du Live Director.";
        _liveProductionButtonUx9M.Pressed += OpenLiveProductionUx9M;
        headingRow.AddChild(_liveProductionButtonUx9M);
    }

    private void InitializeUx9MOverlay()
    {
        _liveProductionUx9M = new StudioLiveDirectorProductionWorkbenchUx9M(_session);
        _liveProductionUx9M.StatusChanged += SetStatus;
        _liveProductionUx9M.WorkspaceChanged += RefreshAfterUx9MMutation;
        AddChild(_liveProductionUx9M);
    }

    private void OpenLiveProductionUx9M()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer la production live.",
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
        CloseUx9LOverlay();
        _liveProductionUx9M.Open();
        SetStatus("UX9-M · repos, niveau, apparence, groupes et enregistrement.");
    }

    private void RefreshAfterUx9MMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9MActionState();
    }

    private void UpdateUx9MActionState()
    {
        if (_liveProductionButtonUx9M is not null)
            _liveProductionButtonUx9M.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9MOverlay()
    {
        if (_liveProductionUx9M is not null) _liveProductionUx9M.Close();
        CloseUx9NOverlay();
    }

    internal bool PrepareUx9MEvidence(string mode)
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
        CloseUx9LOverlay();
        return _liveProductionUx9M.PrepareEvidence(mode);
    }

    internal bool RunUx9MSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveProductionUx9M.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveProductionApprovalUx9M();

        var prepared = PrepareUx9MEvidence("ux9-m-live-director-production");
        var selection = prepared && _liveProductionUx9M.HasExplicitSelection;
        var human = prepared && _liveProductionUx9M.ShowsHumanSurface;
        var preview = prepared && _liveProductionUx9M.PreviewReady;
        var sourceUntouched = prepared && _liveProductionUx9M.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveProductionUx9M.RunUiEvidence();
        _liveProductionUx9M.CloseEvidence();
        var released = _liveProductionUx9M.EvidenceResourcesReleased;
        var sourceRestored = _liveProductionUx9M.SourceSessionRestored;
        var contract = _session.RunUx9MContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveProductionUx9M.RestoreSourceSurfaceAfterSmoke();
        else _liveProductionUx9M.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveProductionApprovalUx9M(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              (currentHistory?.CanUndo ?? false) == originalCanUndo &&
                              (currentHistory?.CanRedo ?? false) == originalCanRedo &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveProductionApprovalUx9M() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveProductionUx9M.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_M_{(valid ? "PASS" : "FAILED")} " +
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
                 $"forced_rest={contract.ForcedRest.ToString().ToLowerInvariant()} " +
                 $"level={contract.LevelAdjustment.ToString().ToLowerInvariant()} " +
                 $"appearance={contract.Appearance.ToString().ToLowerInvariant()} " +
                 $"group={contract.SavedGroup.ToString().ToLowerInvariant()} " +
                 $"recording={contract.Recording.ToString().ToLowerInvariant()} " +
                 $"privacy={contract.Privacy.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9MAutomatedEvidence()
    {
        if (_liveProductionUx9M is not null) _liveProductionUx9M.CloseEvidence();
    }
}
