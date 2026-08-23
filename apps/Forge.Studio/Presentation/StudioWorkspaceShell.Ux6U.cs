using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBlackboardWorkbenchUx6U _blackboardUx6U = null!;
    private Button _blackboardButtonUx6U = null!;

    private void BuildUx6UActions(HBoxContainer headingRow)
    {
        _blackboardButtonUx6U = StudioStyle.Button("Variables & blackboard", compact: true);
        _blackboardButtonUx6U.TooltipText =
            "Définitions typées, handles runtime, vue debug, réplication et snapshots en mémoire.";
        _blackboardButtonUx6U.Pressed += OpenBlackboardUx6U;
        headingRow.AddChild(_blackboardButtonUx6U);
    }

    private void InitializeUx6UOverlay()
    {
        _blackboardUx6U = new StudioBlackboardWorkbenchUx6U(_session);
        _blackboardUx6U.StatusChanged += SetStatus;
        _blackboardUx6U.BlackboardDefinitionChanged += RefreshAfterUx6UMutation;
        AddChild(_blackboardUx6U);
    }

    private void OpenBlackboardUx6U()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’inspecter son blackboard.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6U();
        _blackboardUx6U.Open();
        SetStatus("UX6-U · variables typées et blackboard sous commande canonique et simulation copiée.");
    }

    private void RefreshAfterUx6UMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6UActionState();
    }

    private void UpdateUx6UActionState()
    {
        if (_blackboardButtonUx6U is not null)
            _blackboardButtonUx6U.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6UOverlay()
    {
        if (_blackboardUx6U is not null) _blackboardUx6U.Close();
    }

    internal bool PrepareUx6UEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6U();
        _blackboardUx6U.Open();
        return _blackboardUx6U.PrepareEvidence(mode);
    }

    internal bool RunUx6USmoke()
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

        var uiTransaction = _blackboardUx6U.RunUiTransactionEvidence();
        var prepared = PrepareUx6UEvidence("ux6-u-blackboard");
        var uiReady = prepared && _blackboardUx6U.IsOpen &&
                      _blackboardUx6U.UsesIsolatedEvidence &&
                      _blackboardUx6U.HasExplicitSelection &&
                      _blackboardUx6U.PreviewReady &&
                      _blackboardUx6U.ShowsTwoWorkflows &&
                      _blackboardUx6U.ShowsHumanSurface &&
                      _blackboardUx6U.EvidenceSourceUntouched;
        _blackboardUx6U.CloseEvidence();
        var released = _blackboardUx6U.EvidenceResourcesReleased;
        var sourceRestored = _blackboardUx6U.SourceSessionRestored;
        var contract = _session.RunUx6UContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalBOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalROpen) _runtimeDataUiUx6R.RestoreSourceSurfaceAfterSmoke();
        if (originalSOpen) _creatorTechnicalUx6S.RestoreSourceSurfaceAfterSmoke();
        if (originalTOpen) _modernRuntimeUx6T.RestoreSourceSurfaceAfterSmoke();
        if (originalUOpen) _blackboardUx6U.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);
        _session.RestoreCreatorPolicyApprovalUx6S(approvalS);
        _session.RestoreModernToolingApprovalUx6T(approvalT);
        _session.RestoreBlackboardApprovalUx6U(approvalU);

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
                              _session.CaptureBlackboardApprovalUx6U() == approvalU;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen &&
                              _creatorTechnicalUx6S.IsOpen == originalSOpen &&
                              _modernRuntimeUx6T.IsOpen == originalTOpen &&
                              _blackboardUx6U.IsOpen == originalUOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_U_{(valid ? "PASS" : "FAILED")} " +
                 "route=GAME-022 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"typed_validation={contract.TypedValidation.ToString().ToLowerInvariant()} " +
                 $"schema_validation={contract.SchemaValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"typed_mutation={contract.TypedMutation.ToString().ToLowerInvariant()} " +
                 $"debug_snapshot={contract.DebugSnapshot.ToString().ToLowerInvariant()} " +
                 $"replication_delta={contract.ReplicationDelta.ToString().ToLowerInvariant()} " +
                 $"save_scope={contract.SaveScopeRoundTrip.ToString().ToLowerInvariant()} " +
                 $"stale_handle={contract.StaleHandleRejected.ToString().ToLowerInvariant()} " +
                 $"legacy_boundary={contract.LegacyBoundary.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"module_neighbors={contract.ModuleNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6U()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
