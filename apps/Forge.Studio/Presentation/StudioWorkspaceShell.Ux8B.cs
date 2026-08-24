using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBuildMaintenanceWorkbenchUx8B _buildMaintenanceUx8B = null!;
    private Button _buildMaintenanceButtonUx8B = null!;

    private void BuildUx8BActions(HBoxContainer headingRow)
    {
        _buildMaintenanceButtonUx8B = StudioStyle.Button("Maintenir le workspace", compact: true);
        _buildMaintenanceButtonUx8B.TooltipText =
            "Cache, normalisation, migration, références inverses et diff dans une copie temporaire.";
        _buildMaintenanceButtonUx8B.Pressed += OpenBuildMaintenanceUx8B;
        headingRow.AddChild(_buildMaintenanceButtonUx8B);
    }

    private void InitializeUx8BOverlay()
    {
        _buildMaintenanceUx8B = new StudioBuildMaintenanceWorkbenchUx8B(_session);
        _buildMaintenanceUx8B.StatusChanged += SetStatus;
        AddChild(_buildMaintenanceUx8B);
    }

    private void OpenBuildMaintenanceUx8B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant sa maintenance.", error: true);
            return;
        }
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8AOverlay();
        CloseUx8COverlay();
        _buildMaintenanceUx8B.Open();
        SetStatus("UX8-B · cinq opérations canoniques dans une copie temporaire, sans écriture source.");
    }

    private void UpdateUx8BActionState()
    {
        if (_buildMaintenanceButtonUx8B is not null)
            _buildMaintenanceButtonUx8B.Visible = PageId == "build" && _session.Project is not null;
    }

    private void CloseUx8BOverlay()
    {
        if (_buildMaintenanceUx8B is not null) _buildMaintenanceUx8B.Close();
    }

    internal bool PrepareUx8BEvidence(string mode)
    {
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8AOverlay();
        CloseUx8COverlay();
        _buildMaintenanceUx8B.Open();
        return _buildMaintenanceUx8B.PrepareEvidence(mode);
    }

    internal bool RunUx8BSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _buildMaintenanceUx8B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var uiNavigation = _buildMaintenanceUx8B.RunUiNavigationEvidence();
        var navigationReady = uiNavigation && _buildMaintenanceUx8B.ResultNavigationReady;
        var logsSanitized = _buildMaintenanceUx8B.LogsSanitized;
        var uiReady = navigationReady && _buildMaintenanceUx8B.IsOpen &&
                      _buildMaintenanceUx8B.HasExplicitSelection &&
                      _buildMaintenanceUx8B.FiveResultsReady &&
                      _buildMaintenanceUx8B.ResultNavigationReady &&
                      _buildMaintenanceUx8B.CancellationReady &&
                      logsSanitized &&
                      _buildMaintenanceUx8B.WorkspaceConfined &&
                      _buildMaintenanceUx8B.ShowsHumanSurface &&
                      _buildMaintenanceUx8B.EvidenceSourceUntouched;
        var confined = _buildMaintenanceUx8B.WorkspaceConfined;
        _buildMaintenanceUx8B.CloseEvidence();
        var released = _buildMaintenanceUx8B.EvidenceResourcesReleased;
        var sourceRestored = _buildMaintenanceUx8B.SourceSessionRestored;
        var contract = _session.RunUx8BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _buildMaintenanceUx8B.RestoreSourceSurfaceAfterSmoke();
        else _buildMaintenanceUx8B.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var surfaceRestored = PageId == originalPage && _buildMaintenanceUx8B.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_B_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"cache={contract.Cache.ToString().ToLowerInvariant()} " +
                 $"normalization={contract.Normalization.ToString().ToLowerInvariant()} " +
                 $"migration={contract.Migration.ToString().ToLowerInvariant()} " +
                 $"references={contract.ReverseReferences.ToString().ToLowerInvariant()} " +
                 $"semantic_diff={contract.SemanticDiff.ToString().ToLowerInvariant()} " +
                 $"uuid_free={logsSanitized.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={navigationReady.ToString().ToLowerInvariant()} " +
                 $"isolated={(contract.Isolated && confined).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceRestored && sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8BAutomatedEvidence()
    {
        if (_buildMaintenanceUx8B is not null) _buildMaintenanceUx8B.CloseEvidence();
    }
}
