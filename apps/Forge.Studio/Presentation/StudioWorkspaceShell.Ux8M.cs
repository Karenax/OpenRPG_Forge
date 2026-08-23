using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNetworkPlayerExperienceWorkbenchUx8M _networkPlayerExperienceUx8M = null!;
    private Button _networkPlayerExperienceButtonUx8M = null!;

    private void BuildUx8MActions(HBoxContainer headingRow)
    {
        _networkPlayerExperienceButtonUx8M = StudioStyle.Button("Connexion & personnages", compact: true);
        _networkPlayerExperienceButtonUx8M.TooltipText =
            "Configurer découverte, lobby, création et autorité des personnages sans lancer de transport distant.";
        _networkPlayerExperienceButtonUx8M.Pressed += OpenNetworkPlayerExperienceUx8M;
        headingRow.AddChild(_networkPlayerExperienceButtonUx8M);
    }

    private void InitializeUx8MOverlay()
    {
        _networkPlayerExperienceUx8M = new StudioNetworkPlayerExperienceWorkbenchUx8M(_session);
        _networkPlayerExperienceUx8M.StatusChanged += SetStatus;
        _networkPlayerExperienceUx8M.WorkspaceChanged += RefreshAfterUx8MMutation;
        AddChild(_networkPlayerExperienceUx8M);
    }

    private void OpenNetworkPlayerExperienceUx8M()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son expérience multijoueur.", error: true);
            return;
        }
        if (PageId != "launcher") SwitchPage("launcher", notify: false);
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        _networkPlayerExperienceUx8M.Open();
        SetStatus("UX8-M · découverte, lobby, création et autorité des personnages.");
    }

    private void RefreshAfterUx8MMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx8MActionState();
    }

    private void UpdateUx8MActionState()
    {
        if (_networkPlayerExperienceButtonUx8M is not null)
            _networkPlayerExperienceButtonUx8M.Visible =
                PageId is "launcher" or "network-director" && _session.Project is not null;
    }

    private void CloseUx8MOverlay()
    {
        if (_networkPlayerExperienceUx8M is not null) _networkPlayerExperienceUx8M.Close();
    }

    internal bool PrepareUx8MEvidence(string mode)
    {
        if (PageId != "launcher") SwitchPage("launcher", notify: false);
        CloseUx9AOverlay();
        CloseUx9BOverlay();
        CloseUx9COverlay();
        CloseUx9DOverlay();
        CloseUx9EOverlay();
        CloseUx9FOverlay();
        CloseUx9HOverlay();
        CloseUx9IOverlay();
        CloseUx9JOverlay();
        CloseUx9KOverlay();
        return _networkPlayerExperienceUx8M.PrepareEvidence(mode);
    }

    internal bool RunUx8MSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _networkPlayerExperienceUx8M.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var approval = _session.CaptureNetworkExperienceApprovalUx8M();

        var prepared = PrepareUx8MEvidence("ux8-m-network-player-experience");
        var selection = prepared && _networkPlayerExperienceUx8M.HasExplicitSelection;
        var human = prepared && _networkPlayerExperienceUx8M.ShowsHumanSurface;
        var preview = prepared && _networkPlayerExperienceUx8M.PreviewReady;
        var sourceUntouched = prepared && _networkPlayerExperienceUx8M.EvidenceSourceUntouched;
        var uiEvidence = prepared && _networkPlayerExperienceUx8M.RunUiEvidence();
        _networkPlayerExperienceUx8M.CloseEvidence();
        var released = _networkPlayerExperienceUx8M.EvidenceResourcesReleased;
        var sourceRestored = _networkPlayerExperienceUx8M.SourceSessionRestored;
        var contract = _session.RunUx8MContractSmoke();
        var contractReleased = _session.LastUx8MTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _networkPlayerExperienceUx8M.RestoreSourceSurfaceAfterSmoke();
        else _networkPlayerExperienceUx8M.HideSourceSurfaceAfterSmoke();
        _session.RestoreNetworkExperienceApprovalUx8M(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNetworkExperienceApprovalUx8M() == approval;
        var surfaceRestored = PageId == originalPage && _networkPlayerExperienceUx8M.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && contractReleased && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_M_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"invalid={contract.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"runtime_script={contract.RuntimeAndScriptProjection.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"combat_preserved={contract.CombatPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows_count=1");
        return valid;
    }

    internal void ReleaseUx8MAutomatedEvidence()
    {
        if (_networkPlayerExperienceUx8M is not null) _networkPlayerExperienceUx8M.CloseEvidence();
    }
}
