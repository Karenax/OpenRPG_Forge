using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAdvancedPathfindingWorkbenchUx6V _pathfindingUx6V = null!;
    private Button _pathfindingButtonUx6V = null!;

    private void BuildUx6VActions(HBoxContainer headingRow)
    {
        _pathfindingButtonUx6V = StudioStyle.Button("Pathfinding avancé", compact: true);
        _pathfindingButtonUx6V.TooltipText =
            "Surface, agents, A* déterministe, obstacles dynamiques, coûts et budget de recherche.";
        _pathfindingButtonUx6V.Pressed += OpenPathfindingUx6V;
        headingRow.AddChild(_pathfindingButtonUx6V);
    }

    private void InitializeUx6VOverlay()
    {
        _pathfindingUx6V = new StudioAdvancedPathfindingWorkbenchUx6V(_session);
        _pathfindingUx6V.StatusChanged += SetStatus;
        _pathfindingUx6V.NavigationChanged += RefreshAfterUx6VMutation;
        AddChild(_pathfindingUx6V);
    }

    private void OpenPathfindingUx6V()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son pathfinding.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6V();
        _pathfindingUx6V.Open();
        SetStatus("UX6-V · pathfinding A* canonique, simulation copiée et frontières moteur explicites.");
    }

    private void RefreshAfterUx6VMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6VActionState();
    }

    private void UpdateUx6VActionState()
    {
        if (_pathfindingButtonUx6V is not null)
            _pathfindingButtonUx6V.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6VOverlay()
    {
        if (_pathfindingUx6V is not null) _pathfindingUx6V.Close();
    }

    internal bool PrepareUx6VEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6V();
        _pathfindingUx6V.Open();
        return _pathfindingUx6V.PrepareEvidence(mode);
    }

    internal bool RunUx6VSmoke()
    {
        var originalPage = PageId;
        var originalBOpen = _gameplayFoundationWorkbenchUx6B.IsOpen;
        var originalCOpen = _gameplayCapabilitiesWorkbenchUx6C.IsOpen;
        var originalDOpen = _gameplaySimulationWorkbenchUx6D.IsOpen;
        var originalEOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalROpen = _runtimeDataUiUx6R.IsOpen;
        var originalSOpen = _creatorTechnicalUx6S.IsOpen;
        var originalTOpen = _modernRuntimeUx6T.IsOpen;
        var originalUOpen = _blackboardUx6U.IsOpen;
        var originalVOpen = _pathfindingUx6V.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var approvalB = _session.CaptureGameplayFoundationApprovalUx6B();
        var approvalC = _session.CaptureGameplayCapabilityApprovalUx6C();
        var approvalD = _session.CaptureGameplaySimulationApprovalUx6D();
        var approvalE = _session.CaptureGameplayProductionApprovalUx6E();
        var approvalR = _session.CaptureRuntimeDataApprovalUx6R();
        var approvalS = _session.CaptureCreatorPolicyApprovalUx6S();
        var approvalT = _session.CaptureModernToolingApprovalUx6T();
        var approvalU = _session.CaptureBlackboardApprovalUx6U();
        var approvalV = _session.CapturePathfindingApprovalUx6V();

        var uiTransaction = _pathfindingUx6V.RunUiTransactionEvidence();
        var prepared = PrepareUx6VEvidence("ux6-v-pathfinding");
        var uiReady = prepared && _pathfindingUx6V.IsOpen &&
                      _pathfindingUx6V.UsesIsolatedEvidence &&
                      _pathfindingUx6V.HasExplicitSelection &&
                      _pathfindingUx6V.PreviewReady &&
                      _pathfindingUx6V.ShowsThreeWorkflows &&
                      _pathfindingUx6V.ShowsHumanSurface &&
                      _pathfindingUx6V.EvidenceSourceUntouched;
        _pathfindingUx6V.CloseEvidence();
        var released = _pathfindingUx6V.EvidenceResourcesReleased;
        var sourceRestored = _pathfindingUx6V.SourceSessionRestored;
        var contract = _session.RunUx6VContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalBOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalROpen) _runtimeDataUiUx6R.RestoreSourceSurfaceAfterSmoke();
        if (originalSOpen) _creatorTechnicalUx6S.RestoreSourceSurfaceAfterSmoke();
        if (originalTOpen) _modernRuntimeUx6T.RestoreSourceSurfaceAfterSmoke();
        if (originalUOpen) _blackboardUx6U.RestoreSourceSurfaceAfterSmoke();
        if (originalVOpen) _pathfindingUx6V.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);
        _session.RestoreCreatorPolicyApprovalUx6S(approvalS);
        _session.RestoreModernToolingApprovalUx6T(approvalT);
        _session.RestoreBlackboardApprovalUx6U(approvalU);
        _session.RestorePathfindingApprovalUx6V(approvalV);

        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.CaptureGameplayFoundationApprovalUx6B() == approvalB &&
                              _session.CaptureGameplayCapabilityApprovalUx6C() == approvalC &&
                              _session.CaptureGameplaySimulationApprovalUx6D() == approvalD &&
                              _session.CaptureGameplayProductionApprovalUx6E() == approvalE &&
                              _session.CaptureRuntimeDataApprovalUx6R() == approvalR &&
                              _session.CaptureCreatorPolicyApprovalUx6S() == approvalS &&
                              _session.CaptureModernToolingApprovalUx6T() == approvalT &&
                              _session.CaptureBlackboardApprovalUx6U() == approvalU &&
                              _session.CapturePathfindingApprovalUx6V() == approvalV;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen &&
                              _creatorTechnicalUx6S.IsOpen == originalSOpen &&
                              _modernRuntimeUx6T.IsOpen == originalTOpen &&
                              _blackboardUx6U.IsOpen == originalUOpen &&
                              _pathfindingUx6V.IsOpen == originalVOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_V_{(valid ? "PASS" : "FAILED")} " +
                 "route=EE-028 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"invalid_configuration={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"surface={contract.SurfaceBuilt.ToString().ToLowerInvariant()} " +
                 $"engine_plan={contract.EnginePlanBuilt.ToString().ToLowerInvariant()} " +
                 $"dynamic_obstacle={contract.DynamicObstacleAvoided.ToString().ToLowerInvariant()} " +
                 $"cost_detour={contract.CostDetour.ToString().ToLowerInvariant()} " +
                 $"recovery={contract.RecoveryVerified.ToString().ToLowerInvariant()} " +
                 $"budget={contract.BudgetFailureVerified.ToString().ToLowerInvariant()} " +
                 $"partial_update={contract.PartialUpdatePlanned.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"navigation_neighbors={contract.NavigationNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherRuntimeSurfacesUx6V()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
