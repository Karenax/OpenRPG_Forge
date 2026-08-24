using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkFoundationWorkbenchUx9A _networkFoundationUx9A = null!;
    private Button _networkFoundationButtonUx9A = null!;

    private void BuildUx9AActions(HBoxContainer headingRow)
    {
        _networkFoundationButtonUx9A = StudioStyle.Button("Session réseau locale", compact: true);
        _networkFoundationButtonUx9A.TooltipText =
            "Autorité serveur, groupes, réplication et gestion d’intérêt sur loopback.";
        _networkFoundationButtonUx9A.Pressed += OpenNetworkFoundationUx9A;
        headingRow.AddChild(_networkFoundationButtonUx9A);
    }

    private void InitializeUx9AOverlay()
    {
        _networkFoundationUx9A = new StudioNetworkFoundationWorkbenchUx9A(_session);
        _networkFoundationUx9A.StatusChanged += SetStatus;
        _networkFoundationUx9A.WorkspaceChanged += RefreshAfterUx9AMutation;
        AddChild(_networkFoundationUx9A);
    }

    private void OpenNetworkFoundationUx9A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de démarrer une session réseau locale.", error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
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
        _networkFoundationUx9A.Open();
        SetStatus("UX9-A · autorité locale, groupes, réplication et gestion d’intérêt.");
    }

    private void RefreshAfterUx9AMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9AActionState();
    }

    private void UpdateUx9AActionState()
    {
        if (_networkFoundationButtonUx9A is not null)
            _networkFoundationButtonUx9A.Visible = PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9AOverlay()
    {
        if (_networkFoundationUx9A is not null) _networkFoundationUx9A.Close();
    }

    internal bool PrepareUx9AEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
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
        return _networkFoundationUx9A.PrepareEvidence(mode);
    }

    internal bool RunUx9ASmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkFoundationUx9A.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureNetworkFoundationApprovalUx9A();

        var prepared = PrepareUx9AEvidence("ux9-a-network-foundation");
        var selection = prepared && _networkFoundationUx9A.HasExplicitSelection;
        var human = prepared && _networkFoundationUx9A.ShowsHumanSurface;
        var preview = prepared && _networkFoundationUx9A.PreviewReady;
        var sourceUntouched = prepared && _networkFoundationUx9A.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkFoundationUx9A.RunUiEvidence();
        _networkFoundationUx9A.CloseEvidence();
        var released = _networkFoundationUx9A.EvidenceResourcesReleased;
        var sourceRestored = _networkFoundationUx9A.SourceSessionRestored;
        var contract = _session.RunUx9AContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkFoundationUx9A.RestoreSourceSurfaceAfterSmoke();
        else _networkFoundationUx9A.HideSourceSurfaceAfterSmoke();
        _session.RestoreNetworkFoundationApprovalUx9A(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNetworkFoundationApprovalUx9A() == originalApproval;
        var surfaceRestored = PageId == originalPage && _networkFoundationUx9A.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released &&
                      sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_A_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
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
                 $"authority={contract.AuthoritativeRuntime.ToString().ToLowerInvariant()} " +
                 $"party={contract.PartyRuntime.ToString().ToLowerInvariant()} " +
                 $"replication={contract.ReplicationRuntime.ToString().ToLowerInvariant()} " +
                 $"interest={contract.InterestRuntime.ToString().ToLowerInvariant()} " +
                 $"disconnect={contract.Disconnect.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9AAutomatedEvidence()
    {
        if (_networkFoundationUx9A is not null) _networkFoundationUx9A.CloseEvidence();
    }
}
