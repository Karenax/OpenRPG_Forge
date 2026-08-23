using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkOperationsWorkbenchUx9B _networkOperationsUx9B = null!;
    private Button _networkOperationsButtonUx9B = null!;

    private void BuildUx9BActions(HBoxContainer headingRow)
    {
        _networkOperationsButtonUx9B = StudioStyle.Button("Opérations réseau", compact: true);
        _networkOperationsButtonUx9B.TooltipText =
            "Synchronisation, verrou, rôles, chat et événements testés localement.";
        _networkOperationsButtonUx9B.Pressed += OpenNetworkOperationsUx9B;
        headingRow.AddChild(_networkOperationsButtonUx9B);
    }

    private void InitializeUx9BOverlay()
    {
        _networkOperationsUx9B = new StudioNetworkOperationsWorkbenchUx9B(_session);
        _networkOperationsUx9B.StatusChanged += SetStatus;
        _networkOperationsUx9B.WorkspaceChanged += RefreshAfterUx9BMutation;
        AddChild(_networkOperationsUx9B);
    }

    private void OpenNetworkOperationsUx9B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer les opérations réseau.", error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _networkOperationsUx9B.Open();
        SetStatus("UX9-B · contenu, version, rôles, chat et événements locaux.");
    }

    private void RefreshAfterUx9BMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9BActionState();
    }

    private void UpdateUx9BActionState()
    {
        if (_networkOperationsButtonUx9B is not null)
            _networkOperationsButtonUx9B.Visible = PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9BOverlay()
    {
        if (_networkOperationsUx9B is not null) _networkOperationsUx9B.Close();
    }

    internal bool PrepareUx9BEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _networkOperationsUx9B.PrepareEvidence(mode);
    }

    internal bool RunUx9BSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkOperationsUx9B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureNetworkOperationsApprovalUx9B();

        var prepared = PrepareUx9BEvidence("ux9-b-network-operations");
        var selection = prepared && _networkOperationsUx9B.HasExplicitSelection;
        var human = prepared && _networkOperationsUx9B.ShowsHumanSurface;
        var preview = prepared && _networkOperationsUx9B.PreviewReady;
        var sourceUntouched = prepared && _networkOperationsUx9B.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkOperationsUx9B.RunUiEvidence();
        _networkOperationsUx9B.CloseEvidence();
        var released = _networkOperationsUx9B.EvidenceResourcesReleased;
        var sourceRestored = _networkOperationsUx9B.SourceSessionRestored;
        var contract = _session.RunUx9BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkOperationsUx9B.RestoreSourceSurfaceAfterSmoke();
        else _networkOperationsUx9B.HideSourceSurfaceAfterSmoke();
        _session.RestoreNetworkOperationsApprovalUx9B(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNetworkOperationsApprovalUx9B() == originalApproval;
        var surfaceRestored = PageId == originalPage && _networkOperationsUx9B.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released &&
                      sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_B_{(valid ? "PASS" : "FAILED")} " +
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
                 $"content={contract.ContentRuntime.ToString().ToLowerInvariant()} " +
                 $"version={contract.VersionRuntime.ToString().ToLowerInvariant()} " +
                 $"roles={contract.RolesRuntime.ToString().ToLowerInvariant()} " +
                 $"chat={contract.ChatRuntime.ToString().ToLowerInvariant()} " +
                 $"events={contract.EventsRuntime.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9BAutomatedEvidence()
    {
        if (_networkOperationsUx9B is not null) _networkOperationsUx9B.CloseEvidence();
    }
}
