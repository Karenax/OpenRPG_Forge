using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLiveDirectorOperationsWorkbenchUx9J _liveDirectorOperationsUx9J = null!;
    private Button _liveDirectorOperationsButtonUx9J = null!;

    private void BuildUx9JActions(HBoxContainer headingRow)
    {
        _liveDirectorOperationsButtonUx9J = StudioStyle.Button("Opérations live", compact: true);
        _liveDirectorOperationsButtonUx9J.TooltipText =
            "Rechercher, exécuter ForgeScript, modifier une variable, lancer une scène et projeter la visibilité.";
        _liveDirectorOperationsButtonUx9J.Pressed += OpenLiveDirectorOperationsUx9J;
        headingRow.AddChild(_liveDirectorOperationsButtonUx9J);
    }

    private void InitializeUx9JOverlay()
    {
        _liveDirectorOperationsUx9J = new StudioLiveDirectorOperationsWorkbenchUx9J(_session);
        _liveDirectorOperationsUx9J.StatusChanged += SetStatus;
        _liveDirectorOperationsUx9J.WorkspaceChanged += RefreshAfterUx9JMutation;
        AddChild(_liveDirectorOperationsUx9J);
    }

    private void OpenLiveDirectorOperationsUx9J()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’exécuter une opération live.",
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
        CloseUx9KOverlay();
        _liveDirectorOperationsUx9J.Open();
        SetStatus("UX9-J · recherche, ForgeScript, variables, scènes et visibilité MJ.");
    }

    private void RefreshAfterUx9JMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9JActionState();
    }

    private void UpdateUx9JActionState()
    {
        if (_liveDirectorOperationsButtonUx9J is not null)
            _liveDirectorOperationsButtonUx9J.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9JOverlay()
    {
        if (_liveDirectorOperationsUx9J is not null) _liveDirectorOperationsUx9J.Close();
    }

    internal bool PrepareUx9JEvidence(string mode)
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
        CloseUx9KOverlay();
        return _liveDirectorOperationsUx9J.PrepareEvidence(mode);
    }

    internal bool RunUx9JSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _liveDirectorOperationsUx9J.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureLiveOperationsApprovalUx9J();

        var prepared = PrepareUx9JEvidence("ux9-j-live-director-operations");
        var selection = prepared && _liveDirectorOperationsUx9J.HasExplicitSelection;
        var human = prepared && _liveDirectorOperationsUx9J.ShowsHumanSurface;
        var preview = prepared && _liveDirectorOperationsUx9J.PreviewReady;
        var sourceUntouched = prepared &&
                              _liveDirectorOperationsUx9J.EvidenceSourceUntouched;
        var uiEvidence = prepared && _liveDirectorOperationsUx9J.RunUiEvidence();
        _liveDirectorOperationsUx9J.CloseEvidence();
        var released = _liveDirectorOperationsUx9J.EvidenceResourcesReleased;
        var sourceRestored = _liveDirectorOperationsUx9J.SourceSessionRestored;
        var contract = _session.RunUx9JContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _liveDirectorOperationsUx9J.RestoreSourceSurfaceAfterSmoke();
        else _liveDirectorOperationsUx9J.HideSourceSurfaceAfterSmoke();
        _session.RestoreLiveOperationsApprovalUx9J(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureLiveOperationsApprovalUx9J() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _liveDirectorOperationsUx9J.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_J_{(valid ? "PASS" : "FAILED")} " +
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
                 $"search={contract.SearchRuntime.ToString().ToLowerInvariant()} " +
                 $"script={contract.ScriptRuntime.ToString().ToLowerInvariant()} " +
                 $"variables={contract.VariableRuntime.ToString().ToLowerInvariant()} " +
                 $"scenes={contract.SceneRuntime.ToString().ToLowerInvariant()} " +
                 $"visibility={contract.VisibilityRuntime.ToString().ToLowerInvariant()} " +
                 $"compensation={contract.Compensation.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audit.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9JAutomatedEvidence()
    {
        if (_liveDirectorOperationsUx9J is not null)
            _liveDirectorOperationsUx9J.CloseEvidence();
    }
}
