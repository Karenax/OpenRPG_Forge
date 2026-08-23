using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioModernRuntimeWorkbenchUx6T _modernRuntimeUx6T = null!;
    private Button _modernRuntimeButtonUx6T = null!;

    private void BuildUx6TActions(HBoxContainer headingRow)
    {
        _modernRuntimeButtonUx6T = StudioStyle.Button("AOE & NUI 2.0", compact: true);
        _modernRuntimeButtonUx6T.TooltipText =
            "Indicateurs de zone d’effet gouvernés et capacités du toolkit NUI 2.0.";
        _modernRuntimeButtonUx6T.Pressed += OpenModernRuntimeUx6T;
        headingRow.AddChild(_modernRuntimeButtonUx6T);
    }

    private void InitializeUx6TOverlay()
    {
        _modernRuntimeUx6T = new StudioModernRuntimeWorkbenchUx6T(_session);
        _modernRuntimeUx6T.StatusChanged += SetStatus;
        _modernRuntimeUx6T.ModernToolingChanged += RefreshAfterUx6TMutation;
        AddChild(_modernRuntimeUx6T);
    }

    private void OpenModernRuntimeUx6T()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer AOE et NUI.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6T();
        _modernRuntimeUx6T.Open();
        SetStatus("UX6-T · indicateur AOE et toolkit NUI 2.0 sous une commande canonique.");
    }

    private void RefreshAfterUx6TMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6TActionState();
    }

    private void UpdateUx6TActionState()
    {
        if (_modernRuntimeButtonUx6T is not null)
            _modernRuntimeButtonUx6T.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6TOverlay()
    {
        if (_modernRuntimeUx6T is not null) _modernRuntimeUx6T.Close();
    }

    internal bool PrepareUx6TEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6T();
        _modernRuntimeUx6T.Open();
        return _modernRuntimeUx6T.PrepareEvidence(mode);
    }

    internal bool RunUx6TSmoke()
    {
        var originalPage = PageId;
        var originalBOpen = _gameplayFoundationWorkbenchUx6B.IsOpen;
        var originalCOpen = _gameplayCapabilitiesWorkbenchUx6C.IsOpen;
        var originalDOpen = _gameplaySimulationWorkbenchUx6D.IsOpen;
        var originalEOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalROpen = _runtimeDataUiUx6R.IsOpen;
        var originalSOpen = _creatorTechnicalUx6S.IsOpen;
        var originalTOpen = _modernRuntimeUx6T.IsOpen;
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

        var uiTransaction = _modernRuntimeUx6T.RunUiTransactionEvidence();
        var prepared = PrepareUx6TEvidence("ux6-t-aoe");
        var uiReady = prepared && _modernRuntimeUx6T.IsOpen &&
                      _modernRuntimeUx6T.UsesIsolatedEvidence &&
                      _modernRuntimeUx6T.HasExplicitSelection &&
                      _modernRuntimeUx6T.PreviewReady &&
                      _modernRuntimeUx6T.ShowsTwoPolicies &&
                      _modernRuntimeUx6T.ShowsHumanSurface &&
                      _modernRuntimeUx6T.EvidenceSourceUntouched;
        _modernRuntimeUx6T.CloseEvidence();
        var released = _modernRuntimeUx6T.EvidenceResourcesReleased;
        var sourceRestored = _modernRuntimeUx6T.SourceSessionRestored;
        var contract = _session.RunUx6TContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalBOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalROpen) _runtimeDataUiUx6R.RestoreSourceSurfaceAfterSmoke();
        if (originalSOpen) _creatorTechnicalUx6S.RestoreSourceSurfaceAfterSmoke();
        if (originalTOpen) _modernRuntimeUx6T.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);
        _session.RestoreCreatorPolicyApprovalUx6S(approvalS);
        _session.RestoreModernToolingApprovalUx6T(approvalT);

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
                              _session.CaptureModernToolingApprovalUx6T() == approvalT;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen &&
                              _creatorTechnicalUx6S.IsOpen == originalSOpen &&
                              _modernRuntimeUx6T.IsOpen == originalTOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_T_{(valid ? "PASS" : "FAILED")} " +
                 "routes=EE-022,EE-024 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"shape_validation={contract.ShapeValidation.ToString().ToLowerInvariant()} " +
                 $"range_validation={contract.RangeValidation.ToString().ToLowerInvariant()} " +
                 $"color_validation={contract.ColorValidation.ToString().ToLowerInvariant()} " +
                 $"toolkit_validation={contract.ToolkitValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"aoe_resolved={contract.AreaEffectResolved.ToString().ToLowerInvariant()} " +
                 $"nui_resolved={contract.NuiResolved.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"modern_neighbors={contract.ModernNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"extension_neighbors={contract.ExtensionNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6T()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
