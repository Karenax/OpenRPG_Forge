using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioGameplayFoundationWorkbenchUx6B _gameplayFoundationWorkbenchUx6B = null!;
    private Button _gameplayFoundationButtonUx6B = null!;

    private void BuildUx6BActions(HBoxContainer headingRow)
    {
        _gameplayFoundationButtonUx6B = StudioStyle.Button("Contrôles & gameplay", compact: true);
        _gameplayFoundationButtonUx6B.TooltipText =
            "Déplacement au sol, interactions contextuelles, caméras configurables et entrées humaines.";
        _gameplayFoundationButtonUx6B.Pressed += OpenGameplayFoundationUx6B;
        headingRow.AddChild(_gameplayFoundationButtonUx6B);
    }

    private void InitializeUx6BOverlay()
    {
        _gameplayFoundationWorkbenchUx6B = new StudioGameplayFoundationWorkbenchUx6B(_session);
        _gameplayFoundationWorkbenchUx6B.StatusChanged += SetStatus;
        _gameplayFoundationWorkbenchUx6B.GameplayFoundationChanged += RefreshAfterUx6BMutation;
        AddChild(_gameplayFoundationWorkbenchUx6B);
    }

    private void OpenGameplayFoundationUx6B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son gameplay.", error: true);
            return;
        }
        SwitchPage("runtime", notify: false);
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
        _gameplayFoundationWorkbenchUx6B.Open();
        SetStatus("UX6-B · sélection explicite, dry-run canonique et quatre résolveurs purs.");
    }

    private void RefreshAfterUx6BMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6BActionState();
    }

    private void UpdateUx6BActionState()
    {
        if (_gameplayFoundationButtonUx6B is not null)
            _gameplayFoundationButtonUx6B.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6BOverlay()
    {
        if (_gameplayFoundationWorkbenchUx6B is not null) _gameplayFoundationWorkbenchUx6B.Close();
    }

    internal bool PrepareUx6BEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        _gameplayFoundationWorkbenchUx6B.Open();
        return _gameplayFoundationWorkbenchUx6B.PrepareEvidence(mode);
    }

    internal bool RunUx6BSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _gameplayFoundationWorkbenchUx6B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureGameplayFoundationApprovalUx6B();

        var uiTransaction = _gameplayFoundationWorkbenchUx6B.RunUiTransactionEvidence();
        var prepared = PrepareUx6BEvidence("ux6-b-gameplay-foundation");
        var uiReady = prepared && _gameplayFoundationWorkbenchUx6B.IsOpen &&
                      _gameplayFoundationWorkbenchUx6B.UsesIsolatedEvidence &&
                      _gameplayFoundationWorkbenchUx6B.PreviewReady &&
                      _gameplayFoundationWorkbenchUx6B.HasExplicitSelection &&
                      _gameplayFoundationWorkbenchUx6B.ShowsFourWorkflows &&
                      _gameplayFoundationWorkbenchUx6B.ShowsHumanSurface &&
                      _gameplayFoundationWorkbenchUx6B.EvidenceSourceUntouched;
        _gameplayFoundationWorkbenchUx6B.CloseEvidence();
        var released = _gameplayFoundationWorkbenchUx6B.EvidenceResourcesReleased;
        var sourceRestored = _gameplayFoundationWorkbenchUx6B.SourceSessionRestored;
        var contract = _session.RunUx6BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureGameplayFoundationApprovalUx6B() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_B_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleData.ToString().ToLowerInvariant()} " +
                 $"movement={contract.MovementValidation.ToString().ToLowerInvariant()} " +
                 $"interaction={contract.InteractionValidation.ToString().ToLowerInvariant()} " +
                 $"camera={contract.CameraValidation.ToString().ToLowerInvariant()} " +
                 $"input={contract.InputValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_preview={contract.RuntimePreview.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"save_tail={contract.SavePolicyPreserved.ToString().ToLowerInvariant()} " +
                 $"gameplay_tails={contract.GameplayTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
