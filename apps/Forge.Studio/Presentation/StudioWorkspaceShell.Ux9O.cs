using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkClosureWorkbenchUx9O _networkClosureUx9O = null!;
    private Button _networkClosureButtonUx9O = null!;

    private void BuildUx9OActions(HBoxContainer headingRow)
    {
        _networkClosureButtonUx9O = StudioStyle.Button("Clôture réseau", compact: true);
        _networkClosureButtonUx9O.TooltipText =
            "Configurer serveur dédié, PvP, journaux structurés et anti-triche.";
        _networkClosureButtonUx9O.Pressed += OpenNetworkClosureUx9O;
        headingRow.AddChild(_networkClosureButtonUx9O);
    }

    private void InitializeUx9OOverlay()
    {
        _networkClosureUx9O = new StudioNetworkClosureWorkbenchUx9O(_session);
        _networkClosureUx9O.StatusChanged += SetStatus;
        _networkClosureUx9O.WorkspaceChanged += RefreshAfterUx9OMutation;
        AddChild(_networkClosureUx9O);
    }

    private void OpenNetworkClosureUx9O()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de clore le réseau.", error: true);
            return;
        }
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay(); CloseUx9AOverlay(); CloseUx9BOverlay(); CloseUx9COverlay();
        CloseUx9DOverlay(); CloseUx9EOverlay(); CloseUx9FOverlay(); CloseUx9GOverlay();
        CloseUx9HOverlay(); CloseUx9IOverlay(); CloseUx9JOverlay(); CloseUx9KOverlay();
        CloseUx9LOverlay(); CloseUx9MOverlay(); CloseUx9NOverlay();
        _networkClosureUx9O.Open();
        SetStatus("UX9-O · serveur dédié, PvP, logs et anti-triche.");
    }

    private void RefreshAfterUx9OMutation()
    {
        UpdateCommandState(); RefreshInspector(); UpdateUx9OActionState();
    }

    private void UpdateUx9OActionState()
    {
        if (_networkClosureButtonUx9O is not null)
            _networkClosureButtonUx9O.Visible =
                PageId == "network-director" && _session.Project is not null;
    }

    private void CloseUx9OOverlay()
    {
        if (_networkClosureUx9O is not null) _networkClosureUx9O.Close();
    }

    internal bool PrepareUx9OEvidence(string mode)
    {
        if (PageId != "network-director") SwitchPage("network-director", notify: false);
        CloseUx8MOverlay(); CloseUx9AOverlay(); CloseUx9BOverlay(); CloseUx9COverlay();
        CloseUx9DOverlay(); CloseUx9EOverlay(); CloseUx9FOverlay(); CloseUx9GOverlay();
        CloseUx9HOverlay(); CloseUx9IOverlay(); CloseUx9JOverlay(); CloseUx9KOverlay();
        CloseUx9LOverlay(); CloseUx9MOverlay(); CloseUx9NOverlay();
        return _networkClosureUx9O.PrepareEvidence(mode);
    }

    internal bool RunUx9OSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkClosureUx9O.IsOpen;
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
        var foundationApproval = _session.CaptureNetworkFoundationApprovalUx9A();
        var playerApproval = _session.CaptureNetworkExperienceApprovalUx8M();
        var closureApproval = _session.CaptureNetworkClosureApprovalUx9O();

        var prepared = PrepareUx9OEvidence("ux9-o-network-closure");
        var selection = prepared && _networkClosureUx9O.HasExplicitSelection;
        var human = prepared && _networkClosureUx9O.ShowsHumanSurface;
        var preview = prepared && _networkClosureUx9O.PreviewReady;
        var sourceUntouched = prepared && _networkClosureUx9O.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkClosureUx9O.RunUiEvidence();
        _networkClosureUx9O.CloseEvidence();
        var released = _networkClosureUx9O.EvidenceResourcesReleased;
        var sourceRestored = _networkClosureUx9O.SourceSessionRestored;
        var contract = _session.RunUx9OContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkClosureUx9O.RestoreSourceSurfaceAfterSmoke();
        else _networkClosureUx9O.HideSourceSurfaceAfterSmoke();
        _session.RestoreNetworkFoundationApprovalUx9A(foundationApproval);
        _session.RestoreNetworkExperienceApprovalUx8M(playerApproval);
        _session.RestoreNetworkClosureApprovalUx9O(closureApproval);
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
                              _session.CaptureNetworkFoundationApprovalUx9A() == foundationApproval &&
                              _session.CaptureNetworkExperienceApprovalUx8M() == playerApproval &&
                              _session.CaptureNetworkClosureApprovalUx9O() == closureApproval;
        var surfaceRestored = PageId == originalPage &&
                              _networkClosureUx9O.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched &&
                      uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_O_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"invalid={contract.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"permission={contract.PermissionBlocked.ToString().ToLowerInvariant()} " +
                 $"confirmation={contract.ConfirmationRequired.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"dedicated={contract.Dedicated.ToString().ToLowerInvariant()} " +
                 $"combat={contract.Combat.ToString().ToLowerInvariant()} " +
                 $"logs={contract.StructuredLogs.ToString().ToLowerInvariant()} " +
                 $"anti_cheat={contract.AntiCheat.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx9OAutomatedEvidence()
    {
        if (_networkClosureUx9O is not null) _networkClosureUx9O.CloseEvidence();
    }
}
