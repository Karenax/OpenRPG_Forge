using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRuntimeOperationsWorkbenchUx6Y _runtimeOperationsUx6Y = null!;
    private Button _runtimeOperationsButtonUx6Y = null!;

    private void BuildUx6YActions(HBoxContainer headingRow)
    {
        _runtimeOperationsButtonUx6Y = StudioStyle.Button("Météo & streaming", compact: true);
        _runtimeOperationsButtonUx6Y.TooltipText =
            "Météo runtime autoritaire et chargement de zone amélioré dans deux commandes explicites.";
        _runtimeOperationsButtonUx6Y.Pressed += OpenRuntimeOperationsUx6Y;
        headingRow.AddChild(_runtimeOperationsButtonUx6Y);
    }

    private void InitializeUx6YOverlay()
    {
        _runtimeOperationsUx6Y = new StudioRuntimeOperationsWorkbenchUx6Y(_session);
        _runtimeOperationsUx6Y.StatusChanged += SetStatus;
        _runtimeOperationsUx6Y.RuntimeOperationsChanged += RefreshAfterUx6YMutation;
        AddChild(_runtimeOperationsUx6Y);
    }

    private void OpenRuntimeOperationsUx6Y()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa météo et son streaming.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6Y();
        _runtimeOperationsUx6Y.Open();
        SetStatus("UX6-Y · météo runtime et streaming de zone dans deux commandes annulables.");
    }

    private void RefreshAfterUx6YMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6YActionState();
    }

    private void UpdateUx6YActionState()
    {
        if (_runtimeOperationsButtonUx6Y is not null)
            _runtimeOperationsButtonUx6Y.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6YOverlay()
    {
        if (_runtimeOperationsUx6Y is not null) _runtimeOperationsUx6Y.Close();
    }

    internal bool PrepareUx6YEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6Y();
        _runtimeOperationsUx6Y.Open();
        return _runtimeOperationsUx6Y.PrepareEvidence(mode);
    }

    internal bool RunUx6YSmoke()
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
        var originalYOpen = _runtimeOperationsUx6Y.IsOpen;
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
        var approvalY = _session.CaptureRuntimeOperationsApprovalUx6Y();

        var uiTransaction = _runtimeOperationsUx6Y.RunUiTransactionEvidence();
        var prepared = PrepareUx6YEvidence("ux6-y-runtime-operations");
        var uiReady = prepared && _runtimeOperationsUx6Y.IsOpen &&
                      _runtimeOperationsUx6Y.UsesIsolatedEvidence &&
                      _runtimeOperationsUx6Y.HasExplicitSelection &&
                      _runtimeOperationsUx6Y.PreviewReady &&
                      _runtimeOperationsUx6Y.ShowsTwoWorkflows &&
                      _runtimeOperationsUx6Y.ShowsHumanSurface &&
                      _runtimeOperationsUx6Y.EvidenceSourceUntouched;
        _runtimeOperationsUx6Y.CloseEvidence();
        var released = _runtimeOperationsUx6Y.EvidenceResourcesReleased;
        var sourceRestored = _runtimeOperationsUx6Y.SourceSessionRestored;
        var contract = _session.RunUx6YContractSmoke();

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
        if (originalYOpen) _runtimeOperationsUx6Y.RestoreSourceSurfaceAfterSmoke();
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
        _session.RestoreRuntimeOperationsApprovalUx6Y(approvalY);

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
                              _session.CaptureRuntimeUiApprovalUx6X() == approvalX &&
                              _session.CaptureRuntimeOperationsApprovalUx6Y() == approvalY;
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
                              _runtimeUiUx6X.IsOpen == originalXOpen &&
                              _runtimeOperationsUx6Y.IsOpen == originalYOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_Y_{(valid ? "PASS" : "FAILED")} " +
                 "route=GAME-018,EE-023 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"invalid_configuration={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"weather_dry_run={contract.WeatherDryRun.ToString().ToLowerInvariant()} " +
                 $"loading_dry_run={contract.LoadingDryRun.ToString().ToLowerInvariant()} " +
                 $"weather_runtime={contract.WeatherRuntimeResolved.ToString().ToLowerInvariant()} " +
                 $"typed_event={contract.TypedWeatherEvent.ToString().ToLowerInvariant()} " +
                 $"authority={contract.AuthorityPolicyResolved.ToString().ToLowerInvariant()} " +
                 $"streaming={contract.StreamingRuntimeResolved.ToString().ToLowerInvariant()} " +
                 $"cache_progress={contract.CacheAndProgressResolved.ToString().ToLowerInvariant()} " +
                 $"benchmark={contract.BenchmarkResolved.ToString().ToLowerInvariant()} " +
                 $"cancellation={contract.CancellationPolicyResolved.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"environment_neighbors={contract.EnvironmentNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"world_neighbors={contract.WorldNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6Y()
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
        CloseUx6XOverlay();
    }
}
