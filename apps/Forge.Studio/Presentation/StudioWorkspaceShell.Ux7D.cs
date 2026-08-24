using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioPerformanceLabWorkbenchUx7D _performanceLabUx7D = null!;
    private Button _performanceLabButtonUx7D = null!;

    private void BuildUx7DActions(HBoxContainer headingRow)
    {
        _performanceLabButtonUx7D = StudioStyle.Button("Performance et profilage", compact: true);
        _performanceLabButtonUx7D.TooltipText =
            "Mesurer trois surfaces isolées : benchmarks de zone, budgets et profilage intégré.";
        _performanceLabButtonUx7D.Pressed += OpenPerformanceLabUx7D;
        headingRow.AddChild(_performanceLabButtonUx7D);
    }

    private void InitializeUx7DOverlay()
    {
        _performanceLabUx7D = new StudioPerformanceLabWorkbenchUx7D(_session);
        _performanceLabUx7D.StatusChanged += SetStatus;
        AddChild(_performanceLabUx7D);
    }

    private void OpenPerformanceLabUx7D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer ses mesures de performance.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _performanceLabUx7D.Open();
        SetStatus("UX7-D · benchmarks, budgets et profilage dans une copie isolée.");
    }

    private void UpdateUx7DActionState()
    {
        if (_performanceLabButtonUx7D is not null)
            _performanceLabButtonUx7D.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7DOverlay()
    {
        if (_performanceLabUx7D is not null) _performanceLabUx7D.Close();
    }

    internal bool PrepareUx7DEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _performanceLabUx7D.Open();
        return _performanceLabUx7D.PrepareEvidence(mode);
    }

    internal bool RunUx7DSmoke()
    {
        var originalPage = PageId;
        var originalAOpen = _canonicalValidationUx7A.IsOpen;
        var originalBOpen = _qualityLabUx7B.IsOpen;
        var originalCOpen = _qualitySuitesUx7C.IsOpen;
        var originalDOpen = _performanceLabUx7D.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalAApproval = _session.CaptureValidationApprovalUx7A();
        var originalBApproval = _session.CaptureQualityFoundationApprovalUx7B();

        var uiNavigation = _performanceLabUx7D.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _performanceLabUx7D.IsOpen &&
                      _performanceLabUx7D.UsesIsolatedEvidence &&
                      _performanceLabUx7D.HasExplicitSelection &&
                      _performanceLabUx7D.ThreeResultsReady &&
                      _performanceLabUx7D.ResultNavigationReady &&
                      _performanceLabUx7D.CancellationReady &&
                      _performanceLabUx7D.ArtifactsConfined &&
                      _performanceLabUx7D.ShowsHumanSurface &&
                      _performanceLabUx7D.EvidenceSourceUntouched;
        var artifactsConfined = _performanceLabUx7D.ArtifactsConfined;
        var released = _performanceLabUx7D.EvidenceResourcesReleased;
        _performanceLabUx7D.CloseEvidence();
        var sourceRestored = _performanceLabUx7D.SourceSessionRestored;
        var contract = _session.RunUx7DContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalAOpen) _canonicalValidationUx7A.RestoreSourceSurfaceAfterSmoke();
        if (originalBOpen) _qualityLabUx7B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _qualitySuitesUx7C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _performanceLabUx7D.RestoreSourceSurfaceAfterSmoke();
        else _performanceLabUx7D.HideSourceSurfaceAfterSmoke();
        _session.RestoreValidationApprovalUx7A(originalAApproval);
        _session.RestoreQualityFoundationApprovalUx7B(originalBApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureValidationApprovalUx7A() == originalAApproval &&
                              _session.CaptureQualityFoundationApprovalUx7B() == originalBApproval;
        var surfaceRestored = PageId == originalPage &&
                              _canonicalValidationUx7A.IsOpen == originalAOpen &&
                              _qualityLabUx7B.IsOpen == originalBOpen &&
                              _qualitySuitesUx7C.IsOpen == originalCOpen &&
                              _performanceLabUx7D.IsOpen == originalDOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_D_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.ThreeRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"benchmarks={contract.Benchmarks.ToString().ToLowerInvariant()} " +
                 $"budgets={contract.Budgets.ToString().ToLowerInvariant()} " +
                 $"profiling={contract.Profiling.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"artifacts={(contract.ArtifactsConfined && artifactsConfined).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7DAutomatedEvidence()
    {
        if (_performanceLabUx7D is not null) _performanceLabUx7D.CloseEvidence();
    }
}
