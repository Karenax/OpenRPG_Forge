using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkProductionWorkbenchUx9N _networkProductionUx9N = null!;
    private Button _networkProductionButtonUx9N = null!;

    private void BuildUx9NActions(HBoxContainer headingRow)
    {
        _networkProductionButtonUx9N = StudioStyle.Button("Réseau avancé", compact: true);
        _networkProductionButtonUx9N.TooltipText =
            "Configurer contenu, voix, shards, hot reload, extensions et protocole.";
        _networkProductionButtonUx9N.Pressed += OpenNetworkProductionUx9N;
        headingRow.AddChild(_networkProductionButtonUx9N);
    }

    private void InitializeUx9NOverlay()
    {
        _networkProductionUx9N = new StudioNetworkProductionWorkbenchUx9N(_session);
        _networkProductionUx9N.StatusChanged += SetStatus;
        _networkProductionUx9N.WorkspaceChanged += RefreshAfterUx9NMutation;
        AddChild(_networkProductionUx9N);
    }

    private void OpenNetworkProductionUx9N()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer le réseau avancé.",
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
        CloseUx9MOverlay();
        _networkProductionUx9N.Open();
        SetStatus("UX9-N · contenu, voix, shards, reload, extensions et protocole.");
    }

    private void RefreshAfterUx9NMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx9NActionState();
    }

    private void UpdateUx9NActionState()
    {
        if (_networkProductionButtonUx9N is not null)
            _networkProductionButtonUx9N.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9NOverlay()
    {
        if (_networkProductionUx9N is not null) _networkProductionUx9N.Close();
        CloseUx9OOverlay();
    }

    internal bool PrepareUx9NEvidence(string mode)
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
        CloseUx9MOverlay();
        return _networkProductionUx9N.PrepareEvidence(mode);
    }

    internal bool RunUx9NSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkProductionUx9N.IsOpen;
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
        var originalApproval = _session.CaptureNetworkProductionApprovalUx9N();

        var prepared = PrepareUx9NEvidence("ux9-n-network-production");
        var selection = prepared && _networkProductionUx9N.HasExplicitSelection;
        var human = prepared && _networkProductionUx9N.ShowsHumanSurface;
        var preview = prepared && _networkProductionUx9N.PreviewReady;
        var sourceUntouched = prepared && _networkProductionUx9N.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkProductionUx9N.RunUiEvidence();
        _networkProductionUx9N.CloseEvidence();
        var released = _networkProductionUx9N.EvidenceResourcesReleased;
        var sourceRestored = _networkProductionUx9N.SourceSessionRestored;
        var contract = _session.RunUx9NContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkProductionUx9N.RestoreSourceSurfaceAfterSmoke();
        else _networkProductionUx9N.HideSourceSurfaceAfterSmoke();
        _session.RestoreNetworkProductionApprovalUx9N(originalApproval);
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
                              _session.CaptureNetworkProductionApprovalUx9N() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _networkProductionUx9N.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_N_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.SixRoutes.ToString().ToLowerInvariant()} " +
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
                 $"content={contract.CuratedContent.ToString().ToLowerInvariant()} " +
                 $"voice={contract.Voice.ToString().ToLowerInvariant()} " +
                 $"shard={contract.Shard.ToString().ToLowerInvariant()} " +
                 $"hot_reload={contract.HotReload.ToString().ToLowerInvariant()} " +
                 $"extensions={contract.Extensions.ToString().ToLowerInvariant()} " +
                 $"protocol={contract.Protocol.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9NAutomatedEvidence()
    {
        if (_networkProductionUx9N is not null) _networkProductionUx9N.CloseEvidence();
    }
}
