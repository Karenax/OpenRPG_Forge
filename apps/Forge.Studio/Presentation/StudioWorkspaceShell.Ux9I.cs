using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorGroupWorkbenchUx9I _liveDirectorGroupUx9I = null!;
    private Button _liveDirectorGroupButtonUx9I = null!;

    private void BuildUx9IActions(HBoxContainer headingRow)
    {
        _liveDirectorGroupButtonUx9I = StudioStyle.Button("Groupes live", compact: true);
        _liveDirectorGroupButtonUx9I.TooltipText =
            "Simuler un trigger, sélectionner, déplacer, attaquer et voyager vers un joueur.";
        _liveDirectorGroupButtonUx9I.Pressed += OpenLiveDirectorGroupUx9I;
        headingRow.AddChild(_liveDirectorGroupButtonUx9I);
    }

    private void InitializeUx9IOverlay()
    {
        _liveDirectorGroupUx9I = new StudioLiveDirectorGroupWorkbenchUx9I(_session);
        _liveDirectorGroupUx9I.StatusChanged += SetStatus;
        _liveDirectorGroupUx9I.WorkspaceChanged += RefreshAfterUx9IMutation;
        AddChild(_liveDirectorGroupUx9I);
    }

    private void OpenLiveDirectorGroupUx9I()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de commander un groupe live.",
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
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _liveDirectorGroupUx9I.Open();
        SetStatus("UX9-I · trigger, sélection, déplacement, attaque et voyage joueur.");
    }

    private void RefreshAfterUx9IMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9IActionState();
    }

    private void UpdateUx9IActionState()
    {
        if (_liveDirectorGroupButtonUx9I is not null)
            _liveDirectorGroupButtonUx9I.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9IOverlay()
    {
        if (_liveDirectorGroupUx9I is not null) _liveDirectorGroupUx9I.Close();
    }

    internal bool PrepareUx9IEvidence(string mode)
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
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _liveDirectorGroupUx9I.PrepareEvidence(mode);
    }

    internal bool RunUx9ISmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorGroupUx9I.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveGroupApprovalUx9I();

        var prepared = PrepareUx9IEvidence("ux9-i-live-director-group");
        var selection = prepared && _liveDirectorGroupUx9I.HasExplicitSelection;
        var human = prepared && _liveDirectorGroupUx9I.ShowsHumanSurface;
        var preview = prepared && _liveDirectorGroupUx9I.PreviewReady;
        var sourceUntouched = prepared && _liveDirectorGroupUx9I.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorGroupUx9I.RunUiEvidence();
        _liveDirectorGroupUx9I.CloseEvidence();
        var released = _liveDirectorGroupUx9I.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorGroupUx9I.SourceSessionRestored;
        var contract = _session.RunUx9IContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorGroupUx9I.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorGroupUx9I.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveGroupApprovalUx9I(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveGroupApprovalUx9I() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorGroupUx9I.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_I_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"invalid={contract.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"permission={contract.PermissionBlocked.ToString().ToLowerInvariant()} " +
                 $"confirmation={contract.ConfirmationRequired.ToString().ToLowerInvariant()} " +
                 $"party_consent={contract.PartyConsentRequired.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"trigger={contract.TriggerRuntime.ToString().ToLowerInvariant()} " +
                 $"multi_selection={contract.SelectionRuntime.ToString().ToLowerInvariant()} " +
                 $"movement={contract.MovementRuntime.ToString().ToLowerInvariant()} " +
                 $"attack={contract.AttackRuntime.ToString().ToLowerInvariant()} " +
                 $"travel={contract.TravelRuntime.ToString().ToLowerInvariant()} " +
                 $"compensation={contract.Compensation.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9IAutomatedEvidence()
    {
        if (_liveDirectorGroupUx9I is not null) _liveDirectorGroupUx9I.CloseEvidence();
    }
}
