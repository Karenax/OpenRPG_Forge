using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioGameplaySimulationWorkbenchUx6D _gameplaySimulationWorkbenchUx6D = null!;
    private Button _gameplaySimulationButtonUx6D = null!;

    private void BuildUx6DActions(HBoxContainer headingRow)
    {
        _gameplaySimulationButtonUx6D = StudioStyle.Button("Simulation gameplay", compact: true);
        _gameplaySimulationButtonUx6D.TooltipText =
            "Vol libre, projectiles, physique 3D et ciblage.";
        _gameplaySimulationButtonUx6D.Pressed += OpenGameplaySimulationUx6D;
        headingRow.AddChild(_gameplaySimulationButtonUx6D);
    }

    private void InitializeUx6DOverlay()
    {
        _gameplaySimulationWorkbenchUx6D = new StudioGameplaySimulationWorkbenchUx6D(_session);
        _gameplaySimulationWorkbenchUx6D.StatusChanged += SetStatus;
        _gameplaySimulationWorkbenchUx6D.GameplaySimulationChanged += RefreshAfterUx6DMutation;
        AddChild(_gameplaySimulationWorkbenchUx6D);
    }

    private void OpenGameplaySimulationUx6D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa simulation gameplay.", error: true);
            return;
        }
        SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
        _gameplaySimulationWorkbenchUx6D.Open();
        SetStatus("UX6-D · quatre profils, dry-run canonique et résolveurs déterministes.");
    }

    private void RefreshAfterUx6DMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6DActionState();
    }

    private void UpdateUx6DActionState()
    {
        if (_gameplaySimulationButtonUx6D is not null)
            _gameplaySimulationButtonUx6D.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6DOverlay()
    {
        if (_gameplaySimulationWorkbenchUx6D is not null) _gameplaySimulationWorkbenchUx6D.Close();
    }

    internal bool PrepareUx6DEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        _gameplaySimulationWorkbenchUx6D.Open();
        return _gameplaySimulationWorkbenchUx6D.PrepareEvidence(mode);
    }

    internal bool RunUx6DSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _gameplaySimulationWorkbenchUx6D.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureGameplaySimulationApprovalUx6D();

        var uiTransaction = _gameplaySimulationWorkbenchUx6D.RunUiTransactionEvidence();
        var prepared = PrepareUx6DEvidence("ux6-d-gameplay-simulation");
        var uiReady = prepared && _gameplaySimulationWorkbenchUx6D.IsOpen &&
                      _gameplaySimulationWorkbenchUx6D.UsesIsolatedEvidence &&
                      _gameplaySimulationWorkbenchUx6D.PreviewReady &&
                      _gameplaySimulationWorkbenchUx6D.HasExplicitSelection &&
                      _gameplaySimulationWorkbenchUx6D.ShowsFourWorkflows &&
                      _gameplaySimulationWorkbenchUx6D.ShowsHumanSurface &&
                      _gameplaySimulationWorkbenchUx6D.EvidenceSourceUntouched;
        _gameplaySimulationWorkbenchUx6D.CloseEvidence();
        var released = _gameplaySimulationWorkbenchUx6D.EvidenceResourcesReleased;
        var sourceRestored = _gameplaySimulationWorkbenchUx6D.SourceSessionRestored;
        var contract = _session.RunUx6DContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplaySimulationApprovalUx6D(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureGameplaySimulationApprovalUx6D() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_D_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleData.ToString().ToLowerInvariant()} " +
                 $"flight={contract.FlightValidation.ToString().ToLowerInvariant()} " +
                 $"projectiles={contract.ProjectileValidation.ToString().ToLowerInvariant()} " +
                 $"physics={contract.PhysicsValidation.ToString().ToLowerInvariant()} " +
                 $"targeting={contract.TargetingValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_preview={contract.RuntimePreview.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"world_time={contract.WorldTimePreserved.ToString().ToLowerInvariant()} " +
                 $"gameplay_tails={contract.GameplayTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"stable_ids={contract.StableIdentities.ToString().ToLowerInvariant()} " +
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
