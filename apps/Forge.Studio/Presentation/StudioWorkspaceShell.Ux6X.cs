using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRuntimeUiWorkbenchUx6X _runtimeUiUx6X = null!;
    private Button _runtimeUiButtonUx6X = null!;

    private void BuildUx6XActions(HBoxContainer headingRow)
    {
        _runtimeUiButtonUx6X = StudioStyle.Button("HUD & typographie", compact: true);
        _runtimeUiButtonUx6X.TooltipText =
            "Carte, journal, HUD, police et échelles sous deux commandes canoniques explicites.";
        _runtimeUiButtonUx6X.Pressed += OpenRuntimeUiUx6X;
        headingRow.AddChild(_runtimeUiButtonUx6X);
    }

    private void InitializeUx6XOverlay()
    {
        _runtimeUiUx6X = new StudioRuntimeUiWorkbenchUx6X(_session);
        _runtimeUiUx6X.StatusChanged += SetStatus;
        _runtimeUiUx6X.RuntimeUiChanged += RefreshAfterUx6XMutation;
        AddChild(_runtimeUiUx6X);
    }

    private void OpenRuntimeUiUx6X()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son HUD et sa typographie.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6X();
        _runtimeUiUx6X.Open();
        SetStatus("UX6-X · HUD projet et profil typographique dans deux historiques annulables.");
    }

    private void RefreshAfterUx6XMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6XActionState();
    }

    private void UpdateUx6XActionState()
    {
        if (_runtimeUiButtonUx6X is not null)
            _runtimeUiButtonUx6X.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6XOverlay()
    {
        if (_runtimeUiUx6X is not null) _runtimeUiUx6X.Close();
    }

    internal bool PrepareUx6XEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6X();
        _runtimeUiUx6X.Open();
        return _runtimeUiUx6X.PrepareEvidence(mode);
    }

    internal bool RunUx6XSmoke()
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
        var originalWOpen = _runtimeTransformsUx6W.IsOpen;
        var originalXOpen = _runtimeUiUx6X.IsOpen;
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
        var approvalW = _session.CaptureRuntimeTransformApprovalUx6W();
        var approvalX = _session.CaptureRuntimeUiApprovalUx6X();

        var uiTransaction = _runtimeUiUx6X.RunUiTransactionEvidence();
        var prepared = PrepareUx6XEvidence("ux6-x-runtime-ui");
        var uiReady = prepared && _runtimeUiUx6X.IsOpen &&
                      _runtimeUiUx6X.UsesIsolatedEvidence &&
                      _runtimeUiUx6X.HasExplicitSelection &&
                      _runtimeUiUx6X.PreviewReady &&
                      _runtimeUiUx6X.ShowsTwoWorkflows &&
                      _runtimeUiUx6X.ShowsHumanSurface &&
                      _runtimeUiUx6X.EvidenceSourceUntouched;
        _runtimeUiUx6X.CloseEvidence();
        var released = _runtimeUiUx6X.EvidenceResourcesReleased;
        var sourceRestored = _runtimeUiUx6X.SourceSessionRestored;
        var contract = _session.RunUx6XContractSmoke();

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
        if (originalWOpen) _runtimeTransformsUx6W.RestoreSourceSurfaceAfterSmoke();
        if (originalXOpen) _runtimeUiUx6X.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);
        _session.RestoreCreatorPolicyApprovalUx6S(approvalS);
        _session.RestoreModernToolingApprovalUx6T(approvalT);
        _session.RestoreBlackboardApprovalUx6U(approvalU);
        _session.RestorePathfindingApprovalUx6V(approvalV);
        _session.RestoreRuntimeTransformApprovalUx6W(approvalW);
        _session.RestoreRuntimeUiApprovalUx6X(approvalX);

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
                              _session.CapturePathfindingApprovalUx6V() == approvalV &&
                              _session.CaptureRuntimeTransformApprovalUx6W() == approvalW &&
                              _session.CaptureRuntimeUiApprovalUx6X() == approvalX;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen &&
                              _creatorTechnicalUx6S.IsOpen == originalSOpen &&
                              _modernRuntimeUx6T.IsOpen == originalTOpen &&
                              _blackboardUx6U.IsOpen == originalUOpen &&
                              _pathfindingUx6V.IsOpen == originalVOpen &&
                              _runtimeTransformsUx6W.IsOpen == originalWOpen &&
                              _runtimeUiUx6X.IsOpen == originalXOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_X_{(valid ? "PASS" : "FAILED")} " +
                 "route=GAME-026,EE-026 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"invalid_configuration={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"project_dry_run={contract.ProjectDryRun.ToString().ToLowerInvariant()} " +
                 $"workspace_dry_run={contract.WorkspaceDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_shell={contract.RuntimeShellResolved.ToString().ToLowerInvariant()} " +
                 $"typography={contract.TypographyResolved.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"project_command={contract.CanonicalProjectCommand.ToString().ToLowerInvariant()} " +
                 $"workspace_command={contract.CanonicalWorkspaceCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"gameplay_neighbors={contract.GameplayNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"workspace_neighbors={contract.WorkspaceNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6X()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6YOverlay();
    }
}
