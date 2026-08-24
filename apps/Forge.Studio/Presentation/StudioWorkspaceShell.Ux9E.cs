using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkResilienceCapacityWorkbenchUx9E _networkResilienceCapacityUx9E = null!;
    private Button _networkResilienceCapacityButtonUx9E = null!;

    private void BuildUx9EActions(HBoxContainer headingRow)
    {
        _networkResilienceCapacityButtonUx9E =
            StudioStyle.Button("Résilience & capacité", compact: true);
        _networkResilienceCapacityButtonUx9E.TooltipText =
            "Configurer snapshots, rollback, rétention, slots réservés et file d’attente sous autorité serveur.";
        _networkResilienceCapacityButtonUx9E.Pressed += OpenNetworkResilienceCapacityUx9E;
        headingRow.AddChild(_networkResilienceCapacityButtonUx9E);
    }

    private void InitializeUx9EOverlay()
    {
        _networkResilienceCapacityUx9E =
            new StudioNetworkResilienceCapacityWorkbenchUx9E(_session);
        _networkResilienceCapacityUx9E.StatusChanged += SetStatus;
        _networkResilienceCapacityUx9E.WorkspaceChanged += RefreshAfterUx9EMutation;
        AddChild(_networkResilienceCapacityUx9E);
    }

    private void OpenNetworkResilienceCapacityUx9E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa résilience réseau.",
                error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _networkResilienceCapacityUx9E.Open();
        SetStatus("UX9-E · sauvegarde, rollback, capacité et file d’attente locale.");
    }

    private void RefreshAfterUx9EMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9EActionState();
    }

    private void UpdateUx9EActionState()
    {
        if (_networkResilienceCapacityButtonUx9E is not null)
            _networkResilienceCapacityButtonUx9E.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9EOverlay()
    {
        if (_networkResilienceCapacityUx9E is not null)
            _networkResilienceCapacityUx9E.Close();
    }

    internal bool PrepareUx9EEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _networkResilienceCapacityUx9E.PrepareEvidence(mode);
    }

    internal bool RunUx9ESmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkResilienceCapacityUx9E.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureResilienceCapacityApprovalUx9E();

        var prepared = PrepareUx9EEvidence("ux9-e-network-resilience-capacity");
        var selection = prepared && _networkResilienceCapacityUx9E.HasExplicitSelection;
        var human = prepared && _networkResilienceCapacityUx9E.ShowsHumanSurface;
        var preview = prepared && _networkResilienceCapacityUx9E.PreviewReady;
        var sourceUntouched = prepared && _networkResilienceCapacityUx9E.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkResilienceCapacityUx9E.RunUiEvidence();
        _networkResilienceCapacityUx9E.CloseEvidence();
        var released = _networkResilienceCapacityUx9E.EvidenceResourcesReleased;
        var sourceRestored = _networkResilienceCapacityUx9E.SourceSessionRestored;
        var contract = _session.RunUx9EContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkResilienceCapacityUx9E.RestoreSourceSurfaceAfterSmoke();
        else _networkResilienceCapacityUx9E.HideSourceSurfaceAfterSmoke();
        _session.RestoreResilienceCapacityApprovalUx9E(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureResilienceCapacityApprovalUx9E() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _networkResilienceCapacityUx9E.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence &&
                      released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_E_{(valid ? "PASS" : "FAILED")} " +
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
                 $"rollback={contract.RollbackRuntime.ToString().ToLowerInvariant()} " +
                 $"capacity={contract.CapacityRuntime.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9EAutomatedEvidence()
    {
        if (_networkResilienceCapacityUx9E is not null)
            _networkResilienceCapacityUx9E.CloseEvidence();
    }
}
