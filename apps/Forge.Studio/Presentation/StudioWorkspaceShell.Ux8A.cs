using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBuildWorkbenchUx8A _buildWorkbenchUx8A = null!;
    private Button _buildButtonUx8A = null!;

    private void BuildUx8AActions(HBoxContainer headingRow)
    {
        _buildButtonUx8A = StudioStyle.Button("Construire le module", compact: true);
        _buildButtonUx8A.TooltipText =
            "Exécuter Forge.Cli dans une copie temporaire et expliquer déterminisme et réutilisation.";
        _buildButtonUx8A.Pressed += OpenBuildWorkbenchUx8A;
        headingRow.AddChild(_buildButtonUx8A);
    }

    private void InitializeUx8AOverlay()
    {
        _buildWorkbenchUx8A = new StudioBuildWorkbenchUx8A(_session);
        _buildWorkbenchUx8A.StatusChanged += SetStatus;
        AddChild(_buildWorkbenchUx8A);
    }

    private void OpenBuildWorkbenchUx8A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de le construire.", error: true);
            return;
        }
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8BOverlay();
        CloseUx8COverlay();
        _buildWorkbenchUx8A.Open();
        SetStatus("UX8-A · vrai build CLI dans une copie temporaire, sans publication ni écriture source.");
    }

    private void UpdateUx8AActionState()
    {
        if (_buildButtonUx8A is not null)
            _buildButtonUx8A.Visible = PageId == "build" && _session.Project is not null;
    }

    private void CloseUx8AOverlay()
    {
        if (_buildWorkbenchUx8A is not null) _buildWorkbenchUx8A.Close();
    }

    internal bool PrepareUx8AEvidence(string mode)
    {
        if (PageId != "build") SwitchPage("build", notify: false);
        CloseUx8BOverlay();
        CloseUx8COverlay();
        _buildWorkbenchUx8A.Open();
        return _buildWorkbenchUx8A.PrepareEvidence(mode);
    }

    internal bool RunUx8ASmoke()
    {
        var originalPage = PageId;
        var originalOpen = _buildWorkbenchUx8A.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var uiNavigation = _buildWorkbenchUx8A.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _buildWorkbenchUx8A.IsOpen &&
                      _buildWorkbenchUx8A.HasExplicitSelection &&
                      _buildWorkbenchUx8A.UsesRealCli &&
                      _buildWorkbenchUx8A.FiveResultsReady &&
                      _buildWorkbenchUx8A.ResultNavigationReady &&
                      _buildWorkbenchUx8A.CancellationReady &&
                      _buildWorkbenchUx8A.ArtifactConfined &&
                      _buildWorkbenchUx8A.ShowsHumanSurface &&
                      _buildWorkbenchUx8A.EvidenceSourceUntouched;
        var artifactConfined = _buildWorkbenchUx8A.ArtifactConfined;
        _buildWorkbenchUx8A.CloseEvidence();
        var released = _buildWorkbenchUx8A.EvidenceResourcesReleased;
        var sourceRestored = _buildWorkbenchUx8A.SourceSessionRestored;
        var contract = _session.RunUx8AContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _buildWorkbenchUx8A.RestoreSourceSurfaceAfterSmoke();
        else _buildWorkbenchUx8A.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var surfaceRestored = PageId == originalPage && _buildWorkbenchUx8A.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        if (!contract.Succeeded)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_A_CONTRACT_DIAGNOSTIC " +
                        $"cli={contract.CliProcess.ToString().ToLowerInvariant()} " +
                        $"package={contract.ModulePackage.ToString().ToLowerInvariant()} " +
                        $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                        $"incremental={contract.Incremental.ToString().ToLowerInvariant()} " +
                        $"message={contract.RunMessage}");
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_A_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"package={contract.ModulePackage.ToString().ToLowerInvariant()} " +
                 $"optimized={contract.OptimizedBuild.ToString().ToLowerInvariant()} " +
                 $"cli={contract.CliBuild.ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                 $"incremental={contract.Incremental.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"artifacts={(contract.ArtifactsConfined && artifactConfined).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceRestored && sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8AAutomatedEvidence()
    {
        if (_buildWorkbenchUx8A is not null) _buildWorkbenchUx8A.CloseEvidence();
    }
}
