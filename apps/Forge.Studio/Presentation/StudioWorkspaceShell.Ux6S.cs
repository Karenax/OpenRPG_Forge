using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatorTechnicalWorkbenchUx6S _creatorTechnicalUx6S = null!;
    private Button _creatorTechnicalButtonUx6S = null!;

    private void BuildUx6SActions(HBoxContainer headingRow)
    {
        _creatorTechnicalButtonUx6S = StudioStyle.Button("Creator technique", compact: true);
        _creatorTechnicalButtonUx6S.TooltipText =
            "Périphériques gouvernés, ressources VM, groupe et progression data-driven.";
        _creatorTechnicalButtonUx6S.Pressed += OpenCreatorTechnicalUx6S;
        headingRow.AddChild(_creatorTechnicalButtonUx6S);
    }

    private void InitializeUx6SOverlay()
    {
        _creatorTechnicalUx6S = new StudioCreatorTechnicalWorkbenchUx6S(_session);
        _creatorTechnicalUx6S.StatusChanged += SetStatus;
        _creatorTechnicalUx6S.CreatorPolicyChanged += RefreshAfterUx6SMutation;
        AddChild(_creatorTechnicalUx6S);
    }

    private void OpenCreatorTechnicalUx6S()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer ses politiques Creator.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6S();
        _creatorTechnicalUx6S.Open();
        SetStatus("UX6-S · périphériques, ressources, groupe et progression sous une commande canonique.");
    }

    private void RefreshAfterUx6SMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6SActionState();
    }

    private void UpdateUx6SActionState()
    {
        if (_creatorTechnicalButtonUx6S is not null)
            _creatorTechnicalButtonUx6S.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6SOverlay()
    {
        if (_creatorTechnicalUx6S is not null) _creatorTechnicalUx6S.Close();
    }

    internal bool PrepareUx6SEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6S();
        _creatorTechnicalUx6S.Open();
        return _creatorTechnicalUx6S.PrepareEvidence(mode);
    }

    internal bool RunUx6SSmoke()
    {
        var originalPage = PageId;
        var originalBOpen = _gameplayFoundationWorkbenchUx6B.IsOpen;
        var originalCOpen = _gameplayCapabilitiesWorkbenchUx6C.IsOpen;
        var originalDOpen = _gameplaySimulationWorkbenchUx6D.IsOpen;
        var originalEOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalROpen = _runtimeDataUiUx6R.IsOpen;
        var originalSOpen = _creatorTechnicalUx6S.IsOpen;
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

        var uiTransaction = _creatorTechnicalUx6S.RunUiTransactionEvidence();
        var prepared = PrepareUx6SEvidence("ux6-s-creator-technical");
        var uiReady = prepared && _creatorTechnicalUx6S.IsOpen &&
                      _creatorTechnicalUx6S.UsesIsolatedEvidence &&
                      _creatorTechnicalUx6S.HasExplicitSelection &&
                      _creatorTechnicalUx6S.PreviewReady &&
                      _creatorTechnicalUx6S.ShowsFourPolicies &&
                      _creatorTechnicalUx6S.ShowsHumanSurface &&
                      _creatorTechnicalUx6S.EvidenceSourceUntouched;
        _creatorTechnicalUx6S.CloseEvidence();
        var released = _creatorTechnicalUx6S.EvidenceResourcesReleased;
        var sourceRestored = _creatorTechnicalUx6S.SourceSessionRestored;
        var contract = _session.RunUx6SContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalBOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalROpen) _runtimeDataUiUx6R.RestoreSourceSurfaceAfterSmoke();
        if (originalSOpen) _creatorTechnicalUx6S.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);
        _session.RestoreCreatorPolicyApprovalUx6S(approvalS);

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
                              _session.CaptureCreatorPolicyApprovalUx6S() == approvalS;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen &&
                              _creatorTechnicalUx6S.IsOpen == originalSOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_S_{(valid ? "PASS" : "FAILED")} " +
                 $"routes=EE-014,EE-017,EE-020,EE-021 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"device_validation={contract.DeviceValidation.ToString().ToLowerInvariant()} " +
                 $"resource_validation={contract.ResourceValidation.ToString().ToLowerInvariant()} " +
                 $"party_validation={contract.PartyValidation.ToString().ToLowerInvariant()} " +
                 $"progression_validation={contract.ProgressionValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"device_resolved={contract.DeviceResolved.ToString().ToLowerInvariant()} " +
                 $"resource_resolved={contract.ResourceResolved.ToString().ToLowerInvariant()} " +
                 $"party_resolved={contract.PartyResolved.ToString().ToLowerInvariant()} " +
                 $"progression_resolved={contract.ProgressionResolved.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"creator_neighbors={contract.CreatorNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"post_launch_neighbors={contract.PostLaunchNeighborsPreserved.ToString().ToLowerInvariant()} " +
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

    private void CloseOtherRuntimeSurfacesUx6S()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
