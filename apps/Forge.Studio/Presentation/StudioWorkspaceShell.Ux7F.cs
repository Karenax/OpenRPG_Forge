using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioHistoryRecoveryWorkbenchUx7F _historyRecoveryUx7F = null!;
    private Button _historyRecoveryButtonUx7F = null!;

    private void BuildUx7FActions(HBoxContainer headingRow)
    {
        _historyRecoveryButtonUx7F = StudioStyle.Button("Historique et récupération", compact: true);
        _historyRecoveryButtonUx7F.TooltipText =
            "Inspecter autosaves, archives, portées, checkpoints et crash recovery dans une copie isolée.";
        _historyRecoveryButtonUx7F.Pressed += OpenHistoryRecoveryUx7F;
        headingRow.AddChild(_historyRecoveryButtonUx7F);
    }

    private void InitializeUx7FOverlay()
    {
        _historyRecoveryUx7F = new StudioHistoryRecoveryWorkbenchUx7F(_session);
        _historyRecoveryUx7F.StatusChanged += SetStatus;
        AddChild(_historyRecoveryUx7F);
    }

    private void OpenHistoryRecoveryUx7F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’inspecter son historique.", error: true);
            return;
        }
        if (PageId != "history") SwitchPage("history", notify: false);
        _historyRecoveryUx7F.Open();
        SetStatus("UX7-F · autosaves, archives, portées, checkpoints et crash recovery dans un isolat.");
    }

    private void UpdateUx7FActionState()
    {
        if (_historyRecoveryButtonUx7F is not null)
            _historyRecoveryButtonUx7F.Visible = PageId == "history" && _session.Project is not null;
    }

    private void CloseUx7FOverlay()
    {
        if (_historyRecoveryUx7F is not null) _historyRecoveryUx7F.Close();
    }

    internal bool PrepareUx7FEvidence(string mode)
    {
        if (PageId != "history") SwitchPage("history", notify: false);
        _historyRecoveryUx7F.Open();
        return _historyRecoveryUx7F.PrepareEvidence(mode);
    }

    internal bool RunUx7FSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _historyRecoveryUx7F.IsOpen;
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

        var uiNavigation = _historyRecoveryUx7F.RunUiNavigationEvidence();
        var uiReady = uiNavigation && _historyRecoveryUx7F.IsOpen &&
                      _historyRecoveryUx7F.UsesIsolatedEvidence &&
                      _historyRecoveryUx7F.HasExplicitSelection &&
                      _historyRecoveryUx7F.FiveResultsReady &&
                      _historyRecoveryUx7F.ResultNavigationReady &&
                      _historyRecoveryUx7F.CancellationReady &&
                      _historyRecoveryUx7F.ArtifactConfined &&
                      _historyRecoveryUx7F.ShowsHumanSurface &&
                      _historyRecoveryUx7F.EvidenceSourceUntouched;
        var artifactConfined = _historyRecoveryUx7F.ArtifactConfined;
        var released = _historyRecoveryUx7F.EvidenceResourcesReleased;
        _historyRecoveryUx7F.CloseEvidence();
        var sourceRestored = _historyRecoveryUx7F.SourceSessionRestored;
        var contract = _session.RunUx7FContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _historyRecoveryUx7F.RestoreSourceSurfaceAfterSmoke();
        else _historyRecoveryUx7F.HideSourceSurfaceAfterSmoke();
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
        var surfaceRestored = PageId == originalPage && _historyRecoveryUx7F.IsOpen == originalFOpen;
        var valid = contract.Succeeded && uiNavigation && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_F_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"autosave={contract.AutosaveRecovery.ToString().ToLowerInvariant()} " +
                 $"save_load={contract.SaveLoad.ToString().ToLowerInvariant()} " +
                 $"scopes={contract.SaveScopes.ToString().ToLowerInvariant()} " +
                 $"checkpoint={contract.CheckpointRespawn.ToString().ToLowerInvariant()} " +
                 $"crash={contract.CrashRecovery.ToString().ToLowerInvariant()} " +
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

    internal void ReleaseUx7FAutomatedEvidence()
    {
        if (_historyRecoveryUx7F is not null) _historyRecoveryUx7F.CloseEvidence();
    }
}
