using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioQualitySuiteWorkbenchUx7C _qualitySuitesUx7C = null!;
    private Button _qualitySuitesButtonUx7C = null!;

    private void BuildUx7CActions(HBoxContainer headingRow)
    {
        _qualitySuitesButtonUx7C = StudioStyle.Button("Suites qualité", compact: true);
        _qualitySuitesButtonUx7C.TooltipText =
            "Lancer et inspecter cinq suites isolées : ForgeScript, dialogue, quête, rencontre et réseau.";
        _qualitySuitesButtonUx7C.Pressed += OpenQualitySuitesUx7C;
        headingRow.AddChild(_qualitySuitesButtonUx7C);
    }

    private void InitializeUx7COverlay()
    {
        _qualitySuitesUx7C = new StudioQualitySuiteWorkbenchUx7C(_session);
        _qualitySuitesUx7C.StatusChanged += SetStatus;
        AddChild(_qualitySuitesUx7C);
    }

    private void OpenQualitySuitesUx7C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer ses suites qualité.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _qualitySuitesUx7C.Open();
        SetStatus("UX7-C · cinq suites qualité isolées et annulables.");
    }

    private void UpdateUx7CActionState()
    {
        if (_qualitySuitesButtonUx7C is not null)
            _qualitySuitesButtonUx7C.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7COverlay()
    {
        if (_qualitySuitesUx7C is not null) _qualitySuitesUx7C.Close();
    }

    internal bool PrepareUx7CEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _qualitySuitesUx7C.Open();
        return _qualitySuitesUx7C.PrepareEvidence(mode);
    }

    internal bool RunUx7CSmoke()
    {
        var originalPage = PageId;
        var originalAOpen = _canonicalValidationUx7A.IsOpen;
        var originalBOpen = _qualityLabUx7B.IsOpen;
        var originalCOpen = _qualitySuitesUx7C.IsOpen;
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

        var uiNavigation = _qualitySuitesUx7C.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _qualitySuitesUx7C.IsOpen &&
                      _qualitySuitesUx7C.UsesIsolatedEvidence &&
                      _qualitySuitesUx7C.HasExplicitSelection &&
                      _qualitySuitesUx7C.FiveResultsReady &&
                      _qualitySuitesUx7C.ResultNavigationReady &&
                      _qualitySuitesUx7C.CancellationReady &&
                      _qualitySuitesUx7C.ShowsHumanSurface &&
                      _qualitySuitesUx7C.EvidenceSourceUntouched;
        var released = _qualitySuitesUx7C.EvidenceResourcesReleased;
        _qualitySuitesUx7C.CloseEvidence();
        var sourceRestored = _qualitySuitesUx7C.SourceSessionRestored;
        var contract = _session.RunUx7CContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalAOpen) _canonicalValidationUx7A.RestoreSourceSurfaceAfterSmoke();
        if (originalBOpen) _qualityLabUx7B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _qualitySuitesUx7C.RestoreSourceSurfaceAfterSmoke();
        else _qualitySuitesUx7C.HideSourceSurfaceAfterSmoke();
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
                              _qualitySuitesUx7C.IsOpen == originalCOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_C_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"forgescript={contract.ForgeScriptTests.ToString().ToLowerInvariant()} " +
                 $"dialogue={contract.DialogueTests.ToString().ToLowerInvariant()} " +
                 $"quest={contract.QuestTests.ToString().ToLowerInvariant()} " +
                 $"encounter={contract.EncounterTests.ToString().ToLowerInvariant()} " +
                 $"network={contract.NetworkTests.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7CAutomatedEvidence()
    {
        if (_qualitySuitesUx7C is not null) _qualitySuitesUx7C.CloseEvidence();
    }
}
