using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkWorldAdministrationWorkbenchUx9C _networkWorldAdministrationUx9C = null!;
    private Button _networkWorldAdministrationButtonUx9C = null!;

    private void BuildUx9CActions(HBoxContainer headingRow)
    {
        _networkWorldAdministrationButtonUx9C = StudioStyle.Button("Monde & administration", compact: true);
        _networkWorldAdministrationButtonUx9C.TooltipText =
            "Configurer instances, zones dynamiques, comptes, administration et modération sous autorité serveur.";
        _networkWorldAdministrationButtonUx9C.Pressed += OpenNetworkWorldAdministrationUx9C;
        headingRow.AddChild(_networkWorldAdministrationButtonUx9C);
    }

    private void InitializeUx9COverlay()
    {
        _networkWorldAdministrationUx9C = new StudioNetworkWorldAdministrationWorkbenchUx9C(_session);
        _networkWorldAdministrationUx9C.StatusChanged += SetStatus;
        _networkWorldAdministrationUx9C.WorkspaceChanged += RefreshAfterUx9CMutation;
        AddChild(_networkWorldAdministrationUx9C);
    }

    private void OpenNetworkWorldAdministrationUx9C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son monde réseau.", error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _networkWorldAdministrationUx9C.Open();
        SetStatus("UX9-C · instances, zones dynamiques, comptes, administration et modération.");
    }

    private void RefreshAfterUx9CMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9CActionState();
    }

    private void UpdateUx9CActionState()
    {
        if (_networkWorldAdministrationButtonUx9C is not null)
            _networkWorldAdministrationButtonUx9C.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9COverlay()
    {
        if (_networkWorldAdministrationUx9C is not null) _networkWorldAdministrationUx9C.Close();
    }

    internal bool PrepareUx9CEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay();
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9GOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _networkWorldAdministrationUx9C.PrepareEvidence(mode);
    }

    internal bool RunUx9CSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkWorldAdministrationUx9C.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureWorldAdministrationApprovalUx9C();

        var prepared = PrepareUx9CEvidence("ux9-c-network-world-administration");
        var selection = prepared && _networkWorldAdministrationUx9C.HasExplicitSelection;
        var human = prepared && _networkWorldAdministrationUx9C.ShowsHumanSurface;
        var preview = prepared && _networkWorldAdministrationUx9C.PreviewReady;
        var sourceUntouched = prepared && _networkWorldAdministrationUx9C.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkWorldAdministrationUx9C.RunUiEvidence();
        _networkWorldAdministrationUx9C.CloseEvidence();
        var released = _networkWorldAdministrationUx9C.EvidenceResourcesReleased;
        var sourceRestored = _networkWorldAdministrationUx9C.SourceSessionRestored;
        var contract = _session.RunUx9CContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkWorldAdministrationUx9C.RestoreSourceSurfaceAfterSmoke();
        else _networkWorldAdministrationUx9C.HideSourceSurfaceAfterSmoke();
        _session.RestoreWorldAdministrationApprovalUx9C(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureWorldAdministrationApprovalUx9C() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _networkWorldAdministrationUx9C.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released &&
                      sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_C_{(valid ? "PASS" : "FAILED")} " +
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
                 $"instances={contract.InstancesRuntime.ToString().ToLowerInvariant()} " +
                 $"dynamic_areas={contract.DynamicAreasRuntime.ToString().ToLowerInvariant()} " +
                 $"authentication={contract.AuthenticationRuntime.ToString().ToLowerInvariant()} " +
                 $"administration={contract.AdministrationRuntime.ToString().ToLowerInvariant()} " +
                 $"moderation={contract.ModerationRuntime.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9CAutomatedEvidence()
    {
        if (_networkWorldAdministrationUx9C is not null) _networkWorldAdministrationUx9C.CloseEvidence();
    }
}
