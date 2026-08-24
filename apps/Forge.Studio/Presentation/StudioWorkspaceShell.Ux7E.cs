using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioGovernanceLabWorkbenchUx7E _governanceLabUx7E = null!;
    private Button _governanceLabButtonUx7E = null!;

    private void BuildUx7EActions(HBoxContainer headingRow)
    {
        _governanceLabButtonUx7E = StudioStyle.Button("Gouvernance et sécurité", compact: true);
        _governanceLabButtonUx7E.TooltipText =
            "Inspecter santé projet, accessibilité, fuzzing et fixtures dans une copie isolée.";
        _governanceLabButtonUx7E.Pressed += OpenGovernanceLabUx7E;
        headingRow.AddChild(_governanceLabButtonUx7E);
    }

    private void InitializeUx7EOverlay()
    {
        _governanceLabUx7E = new StudioGovernanceLabWorkbenchUx7E(_session);
        _governanceLabUx7E.StatusChanged += SetStatus;
        AddChild(_governanceLabUx7E);
    }

    private void OpenGovernanceLabUx7E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer sa gouvernance.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _governanceLabUx7E.Open();
        SetStatus("UX7-E · santé, accessibilité, fuzzing et fixtures dans un isolat.");
    }

    private void UpdateUx7EActionState()
    {
        if (_governanceLabButtonUx7E is not null)
            _governanceLabButtonUx7E.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7EOverlay()
    {
        if (_governanceLabUx7E is not null) _governanceLabUx7E.Close();
    }

    internal bool PrepareUx7EEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _governanceLabUx7E.Open();
        return _governanceLabUx7E.PrepareEvidence(mode);
    }

    internal bool RunUx7ESmoke()
    {
        var originalPage = PageId;
        var originalAOpen = _canonicalValidationUx7A.IsOpen;
        var originalBOpen = _qualityLabUx7B.IsOpen;
        var originalCOpen = _qualitySuitesUx7C.IsOpen;
        var originalDOpen = _performanceLabUx7D.IsOpen;
        var originalEOpen = _governanceLabUx7E.IsOpen;
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

        var uiNavigation = _governanceLabUx7E.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _governanceLabUx7E.IsOpen &&
                      _governanceLabUx7E.UsesIsolatedEvidence &&
                      _governanceLabUx7E.HasExplicitSelection &&
                      _governanceLabUx7E.NineResultsReady &&
                      _governanceLabUx7E.ResultNavigationReady &&
                      _governanceLabUx7E.CancellationReady &&
                      _governanceLabUx7E.ArtifactConfined &&
                      _governanceLabUx7E.ShowsHumanSurface &&
                      _governanceLabUx7E.EvidenceSourceUntouched;
        var artifactConfined = _governanceLabUx7E.ArtifactConfined;
        var released = _governanceLabUx7E.EvidenceResourcesReleased;
        _governanceLabUx7E.CloseEvidence();
        var sourceRestored = _governanceLabUx7E.SourceSessionRestored;
        var contract = _session.RunUx7EContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalAOpen) _canonicalValidationUx7A.RestoreSourceSurfaceAfterSmoke();
        if (originalBOpen) _qualityLabUx7B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _qualitySuitesUx7C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _performanceLabUx7D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _governanceLabUx7E.RestoreSourceSurfaceAfterSmoke();
        else _governanceLabUx7E.HideSourceSurfaceAfterSmoke();
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
                              _performanceLabUx7D.IsOpen == originalDOpen &&
                              _governanceLabUx7E.IsOpen == originalEOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_E_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.NineRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"health={contract.ProjectHealth.ToString().ToLowerInvariant()} " +
                 $"editor_access={contract.EditorAccessibility.ToString().ToLowerInvariant()} " +
                 $"runtime_access={contract.RuntimeAccessibility.ToString().ToLowerInvariant()} " +
                 $"fuzz={contract.SecurityFuzz.ToString().ToLowerInvariant()} " +
                 $"fixtures={contract.MaliciousFixtures.ToString().ToLowerInvariant()} " +
                 $"coverage={contract.AuroraCoverage.ToString().ToLowerInvariant()} " +
                 $"gates={contract.AcceptanceGates.ToString().ToLowerInvariant()} " +
                 $"network={contract.DeterministicNetwork.ToString().ToLowerInvariant()} " +
                 $"soak={contract.SoakTests.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"artifact={(contract.ArtifactConfined && artifactConfined).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7EAutomatedEvidence()
    {
        if (_governanceLabUx7E is not null) _governanceLabUx7E.CloseEvidence();
    }
}
