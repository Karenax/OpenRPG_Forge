using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRuntimeTransformsWorkbenchUx6W _runtimeTransformsUx6W = null!;
    private Button _runtimeTransformsButtonUx6W = null!;

    private void BuildUx6WActions(HBoxContainer headingRow)
    {
        _runtimeTransformsButtonUx6W = StudioStyle.Button("Transforms runtime", compact: true);
        _runtimeTransformsButtonUx6W.TooltipText =
            "Overrides hiérarchiques et tweens transform, matériau et état sous commande canonique.";
        _runtimeTransformsButtonUx6W.Pressed += OpenRuntimeTransformsUx6W;
        headingRow.AddChild(_runtimeTransformsButtonUx6W);
    }

    private void InitializeUx6WOverlay()
    {
        _runtimeTransformsUx6W = new StudioRuntimeTransformsWorkbenchUx6W(_session);
        _runtimeTransformsUx6W.StatusChanged += SetStatus;
        _runtimeTransformsUx6W.CinematicsChanged += RefreshAfterUx6WMutation;
        AddChild(_runtimeTransformsUx6W);
    }

    private void OpenRuntimeTransformsUx6W()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer ses transforms runtime.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6W();
        _runtimeTransformsUx6W.Open();
        SetStatus("UX6-W · overrides et tweens persistants, simulés sur une copie sans mutation moteur live.");
    }

    private void RefreshAfterUx6WMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6WActionState();
    }

    private void UpdateUx6WActionState()
    {
        if (_runtimeTransformsButtonUx6W is not null)
            _runtimeTransformsButtonUx6W.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6WOverlay()
    {
        if (_runtimeTransformsUx6W is not null) _runtimeTransformsUx6W.Close();
    }

    internal bool PrepareUx6WEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6W();
        _runtimeTransformsUx6W.Open();
        return _runtimeTransformsUx6W.PrepareEvidence(mode);
    }

    internal bool RunUx6WSmoke()
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

        var uiTransaction = _runtimeTransformsUx6W.RunUiTransactionEvidence();
        var prepared = PrepareUx6WEvidence("ux6-w-runtime-transforms");
        var uiReady = prepared && _runtimeTransformsUx6W.IsOpen &&
                      _runtimeTransformsUx6W.UsesIsolatedEvidence &&
                      _runtimeTransformsUx6W.HasExplicitSelection &&
                      _runtimeTransformsUx6W.PreviewReady &&
                      _runtimeTransformsUx6W.ShowsThreeWorkflows &&
                      _runtimeTransformsUx6W.ShowsHumanSurface &&
                      _runtimeTransformsUx6W.EvidenceSourceUntouched;
        _runtimeTransformsUx6W.CloseEvidence();
        var released = _runtimeTransformsUx6W.EvidenceResourcesReleased;
        var sourceRestored = _runtimeTransformsUx6W.SourceSessionRestored;
        var contract = _session.RunUx6WContractSmoke();

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
                              _session.CaptureRuntimeTransformApprovalUx6W() == approvalW;
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
                              _runtimeTransformsUx6W.IsOpen == originalWOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_W_{(valid ? "PASS" : "FAILED")} " +
                 "route=EE-001,EE-027 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"empty_state={contract.EmptyStateHandled.ToString().ToLowerInvariant()} " +
                 $"invalid_configuration={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"override_runtime={contract.OverrideRuntimeVerified.ToString().ToLowerInvariant()} " +
                 $"transform_tween={contract.TransformTweenVerified.ToString().ToLowerInvariant()} " +
                 $"material_tween={contract.MaterialTweenVerified.ToString().ToLowerInvariant()} " +
                 $"state_tween={contract.StateTweenVerified.ToString().ToLowerInvariant()} " +
                 $"authority={contract.AuthorityVerified.ToString().ToLowerInvariant()} " +
                 $"cancellation={contract.CancellationVerified.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"cinematic_neighbors={contract.CinematicNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6W()
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
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
