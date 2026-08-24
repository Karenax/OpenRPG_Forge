using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkPersistenceObservabilityWorkbenchUx9D _networkPersistenceObservabilityUx9D = null!;
    private Button _networkPersistenceObservabilityButtonUx9D = null!;

    private void BuildUx9DActions(HBoxContainer headingRow)
    {
        _networkPersistenceObservabilityButtonUx9D =
            StudioStyle.Button("Persistance & métriques", compact: true);
        _networkPersistenceObservabilityButtonUx9D.TooltipText =
            "Configurer snapshots, SQL sécurisé, métriques, health, profilage et reconnexion sous autorité serveur.";
        _networkPersistenceObservabilityButtonUx9D.Pressed += OpenNetworkPersistenceObservabilityUx9D;
        headingRow.AddChild(_networkPersistenceObservabilityButtonUx9D);
    }

    private void InitializeUx9DOverlay()
    {
        _networkPersistenceObservabilityUx9D =
            new StudioNetworkPersistenceObservabilityWorkbenchUx9D(_session);
        _networkPersistenceObservabilityUx9D.StatusChanged += SetStatus;
        _networkPersistenceObservabilityUx9D.WorkspaceChanged += RefreshAfterUx9DMutation;
        AddChild(_networkPersistenceObservabilityUx9D);
    }

    private void OpenNetworkPersistenceObservabilityUx9D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa persistance réseau.", error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _networkPersistenceObservabilityUx9D.Open();
        SetStatus("UX9-D · persistance, SQL sécurisé, métriques, profilage et reconnexion.");
    }

    private void RefreshAfterUx9DMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9DActionState();
    }

    private void UpdateUx9DActionState()
    {
        if (_networkPersistenceObservabilityButtonUx9D is not null)
            _networkPersistenceObservabilityButtonUx9D.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9DOverlay()
    {
        if (_networkPersistenceObservabilityUx9D is not null) _networkPersistenceObservabilityUx9D.Close();
    }

    internal bool PrepareUx9DEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _networkPersistenceObservabilityUx9D.PrepareEvidence(mode);
    }

    internal bool RunUx9DSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkPersistenceObservabilityUx9D.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CapturePersistenceObservabilityApprovalUx9D();

        var prepared = PrepareUx9DEvidence("ux9-d-network-persistence-observability");
        var selection = prepared && _networkPersistenceObservabilityUx9D.HasExplicitSelection;
        var human = prepared && _networkPersistenceObservabilityUx9D.ShowsHumanSurface;
        var preview = prepared && _networkPersistenceObservabilityUx9D.PreviewReady;
        var sourceUntouched = prepared && _networkPersistenceObservabilityUx9D.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkPersistenceObservabilityUx9D.RunUiEvidence();
        _networkPersistenceObservabilityUx9D.CloseEvidence();
        var released = _networkPersistenceObservabilityUx9D.EvidenceResourcesReleased;
        var sourceRestored = _networkPersistenceObservabilityUx9D.SourceSessionRestored;
        var contract = _session.RunUx9DContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkPersistenceObservabilityUx9D.RestoreSourceSurfaceAfterSmoke();
        else _networkPersistenceObservabilityUx9D.HideSourceSurfaceAfterSmoke();
        _session.RestorePersistenceObservabilityApprovalUx9D(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CapturePersistenceObservabilityApprovalUx9D() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _networkPersistenceObservabilityUx9D.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released &&
                      sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_D_{(valid ? "PASS" : "FAILED")} " +
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
                 $"snapshot={contract.PersistenceRuntime.ToString().ToLowerInvariant()} " +
                 $"sql={contract.SqlRuntime.ToString().ToLowerInvariant()} " +
                 $"observability={contract.ObservabilityRuntime.ToString().ToLowerInvariant()} " +
                 $"profiler={contract.ProfilerRuntime.ToString().ToLowerInvariant()} " +
                 $"reconnection={contract.ReconnectionRuntime.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9DAutomatedEvidence()
    {
        if (_networkPersistenceObservabilityUx9D is not null)
            _networkPersistenceObservabilityUx9D.CloseEvidence();
    }
}
