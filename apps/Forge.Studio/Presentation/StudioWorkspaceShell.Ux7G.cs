using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioPlaytestLabWorkbenchUx7G _playtestLabUx7G = null!;
    private Button _playtestLabButtonUx7G = null!;
    private Button _runtimePlaytestButtonUx7G = null!;
    private bool _runtimePlaytestWasRunningUx7G;

    private void BuildUx7GActions(HBoxContainer headingRow)
    {
        _runtimePlaytestButtonUx7G = StudioStyle.Button("Lancer le jeu", primary: true, compact: true);
        _runtimePlaytestButtonUx7G.TooltipText =
            "Créer une copie temporaire de l'état courant et lancer Forge.Runtime en Forward+ sans fermer le Studio.";
        _runtimePlaytestButtonUx7G.Pressed += LaunchRuntimePlaytestUx7G;
        headingRow.AddChild(_runtimePlaytestButtonUx7G);
        _playtestLabButtonUx7G = StudioStyle.Button("Laboratoire playtest", compact: true);
        _playtestLabButtonUx7G.TooltipText =
            "Inspecter aperçu, Play From Here, replay, agent de test et simulation de combat dans une copie isolée.";
        _playtestLabButtonUx7G.Pressed += OpenPlaytestLabUx7G;
        headingRow.AddChild(_playtestLabButtonUx7G);
    }

    private void InitializeUx7GOverlay()
    {
        _playtestLabUx7G = new StudioPlaytestLabWorkbenchUx7G(_session);
        _playtestLabUx7G.StatusChanged += SetStatus;
        AddChild(_playtestLabUx7G);
    }

    private void OpenPlaytestLabUx7G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer le laboratoire playtest.", error: true);
            return;
        }
        if (PageId != "playtest") SwitchPage("playtest", notify: false);
        _playtestLabUx7G.Open();
        SetStatus("UX7-G · aperçu, Play From Here, replay, agent et combat dans un isolat.");
    }

    private void UpdateUx7GActionState()
    {
        if (_playtestLabButtonUx7G is not null)
            _playtestLabButtonUx7G.Visible = PageId == "playtest" && _session.Project is not null;
        if (_runtimePlaytestButtonUx7G is not null)
        {
            _runtimePlaytestButtonUx7G.Visible = PageId == "playtest" && _session.Project is not null;
            _runtimePlaytestButtonUx7G.Disabled = _runtimePlaytestWasRunningUx7G;
            _runtimePlaytestButtonUx7G.Text = _runtimePlaytestWasRunningUx7G ? "Jeu en cours" : "Lancer le jeu";
        }
    }

    private void LaunchRuntimePlaytestUx7G()
    {
        if (PageId != "playtest") SwitchPage("playtest", notify: false);
        var result = _session.LaunchRuntimePlaytestUx7G();
        _runtimePlaytestWasRunningUx7G = result.Running;
        UpdateUx7GActionState();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        var status = _session.PollRuntimePlaytestUx7G();
        if (status.Running)
        {
            if (!_runtimePlaytestWasRunningUx7G)
            {
                _runtimePlaytestWasRunningUx7G = true;
                UpdateUx7GActionState();
            }
            return;
        }
        if (!_runtimePlaytestWasRunningUx7G || !status.Completed) return;
        _runtimePlaytestWasRunningUx7G = false;
        UpdateUx7GActionState();
        SetStatus(status.Message, error: status.ExitCode is not 0 || !status.TemporaryWorkspaceReleased);
    }

    private void CloseUx7GOverlay()
    {
        if (_playtestLabUx7G is not null) _playtestLabUx7G.Close();
    }

    internal bool PrepareUx7GEvidence(string mode)
    {
        if (PageId != "playtest") SwitchPage("playtest", notify: false);
        _playtestLabUx7G.Open();
        return _playtestLabUx7G.PrepareEvidence(mode);
    }

    internal bool RunUx7GSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _playtestLabUx7G.IsOpen;
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

        var uiNavigation = _playtestLabUx7G.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _playtestLabUx7G.IsOpen &&
                      _playtestLabUx7G.UsesIsolatedEvidence &&
                      _playtestLabUx7G.HasExplicitSelection &&
                      _playtestLabUx7G.FiveResultsReady &&
                      _playtestLabUx7G.ResultNavigationReady &&
                      _playtestLabUx7G.CancellationReady &&
                      _playtestLabUx7G.ArtifactConfined &&
                      _playtestLabUx7G.ShowsHumanSurface &&
                      _playtestLabUx7G.EvidenceSourceUntouched;
        var artifactConfined = _playtestLabUx7G.ArtifactConfined;
        var released = _playtestLabUx7G.EvidenceResourcesReleased;
        _playtestLabUx7G.CloseEvidence();
        var sourceRestored = _playtestLabUx7G.SourceSessionRestored;
        var contract = _session.RunUx7GContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _playtestLabUx7G.RestoreSourceSurfaceAfterSmoke();
        else _playtestLabUx7G.HideSourceSurfaceAfterSmoke();
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
        var surfaceRestored = PageId == originalPage && _playtestLabUx7G.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_G_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={contract.PreviewMode.ToString().ToLowerInvariant()} " +
                 $"play_from_here={contract.PlayFromHere.ToString().ToLowerInvariant()} " +
                 $"replay={contract.DeterministicReplay.ToString().ToLowerInvariant()} " +
                 $"agent={contract.TestAgent.ToString().ToLowerInvariant()} " +
                 $"combat={contract.CombatSimulation.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={(contract.ResultNavigation && uiNavigation).ToString().ToLowerInvariant()} " +
                 $"deterministic={contract.Deterministic.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"artifacts={(contract.ArtifactsConfined && artifactConfined).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7GAutomatedEvidence()
    {
        if (_playtestLabUx7G is not null) _playtestLabUx7G.CloseEvidence();
    }
}
