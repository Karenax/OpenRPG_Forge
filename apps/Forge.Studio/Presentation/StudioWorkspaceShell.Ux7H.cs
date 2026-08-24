using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioVisualRegressionWorkbenchUx7H _visualRegressionUx7H = null!;
    private Button _visualRegressionButtonUx7H = null!;

    private void BuildUx7HActions(HBoxContainer headingRow)
    {
        _visualRegressionButtonUx7H = StudioStyle.Button("Tests visuels Runtime", compact: true);
        _visualRegressionButtonUx7H.TooltipText =
            "Lancer les huit comparaisons visuelles avec Godot 4.7.1 .NET officiel dans une copie temporaire.";
        _visualRegressionButtonUx7H.Pressed += OpenVisualRegressionUx7H;
        headingRow.AddChild(_visualRegressionButtonUx7H);
    }

    private void InitializeUx7HOverlay()
    {
        _visualRegressionUx7H = new StudioVisualRegressionWorkbenchUx7H(_session);
        _visualRegressionUx7H.StatusChanged += SetStatus;
        AddChild(_visualRegressionUx7H);
    }

    private void OpenVisualRegressionUx7H()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer les tests visuels.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7IOverlay();
        _visualRegressionUx7H.Open();
        SetStatus("UX7-H · huit comparaisons Runtime réelles; Tracy reste explicitement non qualifié.");
    }

    private void UpdateUx7HActionState()
    {
        if (_visualRegressionButtonUx7H is not null)
            _visualRegressionButtonUx7H.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7HOverlay()
    {
        if (_visualRegressionUx7H is not null) _visualRegressionUx7H.Close();
    }

    internal bool PrepareUx7HEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7IOverlay();
        _visualRegressionUx7H.Open();
        return _visualRegressionUx7H.PrepareEvidence(mode);
    }

    internal bool RunUx7HSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _visualRegressionUx7H.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var uiNavigation = _visualRegressionUx7H.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _visualRegressionUx7H.IsOpen &&
                      _visualRegressionUx7H.HasExplicitSelection &&
                      _visualRegressionUx7H.UsesOfficialEngine &&
                      _visualRegressionUx7H.VisualResultReady &&
                      _visualRegressionUx7H.ResultNavigationReady &&
                      _visualRegressionUx7H.CancellationReady &&
                      _visualRegressionUx7H.ArtifactConfined &&
                      _visualRegressionUx7H.ShowsHumanSurface &&
                      _visualRegressionUx7H.TracyDeferred &&
                      _visualRegressionUx7H.EvidenceSourceUntouched;
        var artifactConfined = _visualRegressionUx7H.ArtifactConfined;
        var released = _visualRegressionUx7H.EvidenceResourcesReleased;
        _visualRegressionUx7H.CloseEvidence();
        var sourceRestored = _visualRegressionUx7H.SourceSessionRestored;
        var contract = _session.RunUx7HContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _visualRegressionUx7H.RestoreSourceSurfaceAfterSmoke();
        else _visualRegressionUx7H.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var surfaceRestored = PageId == originalPage && _visualRegressionUx7H.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        if (!contract.Succeeded)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_H_CONTRACT_DIAGNOSTIC " +
                        $"run_succeeded={contract.RunSucceeded.ToString().ToLowerInvariant()} " +
                        $"run_cancelled={contract.RunCancelled.ToString().ToLowerInvariant()} " +
                        $"message={contract.RunMessage}");
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_H_{(valid ? "PASS" : "FAILED")} " +
                 $"route={contract.VisualRoute.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"official_engine={contract.OfficialEngine.ToString().ToLowerInvariant()} " +
                 $"runtime_process={contract.RuntimeProcess.ToString().ToLowerInvariant()} " +
                 $"cases={contract.EightCases.ToString().ToLowerInvariant()} " +
                 $"artifacts={(contract.ArtifactSet && contract.ArtifactsConfined && artifactConfined).ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.DeterministicPlan.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"tracy_deferred={contract.TracyDeferred.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceRestored && sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7HAutomatedEvidence()
    {
        if (_visualRegressionUx7H is not null) _visualRegressionUx7H.CloseEvidence();
    }
}
