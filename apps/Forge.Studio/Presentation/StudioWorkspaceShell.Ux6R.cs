using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRuntimeDataUiWorkbenchUx6R _runtimeDataUiUx6R = null!;
    private Button _runtimeDataUiButtonUx6R = null!;

    private void BuildUx6RActions(HBoxContainer headingRow)
    {
        _runtimeDataUiButtonUx6R = StudioStyle.Button("Données & UI runtime", compact: true);
        _runtimeDataUiButtonUx6R.TooltipText =
            "Règles data-driven, interface scriptable, événements GUI, JSON et snapshots.";
        _runtimeDataUiButtonUx6R.Pressed += OpenRuntimeDataUiUx6R;
        headingRow.AddChild(_runtimeDataUiButtonUx6R);
    }

    private void InitializeUx6ROverlay()
    {
        _runtimeDataUiUx6R = new StudioRuntimeDataUiWorkbenchUx6R(_session);
        _runtimeDataUiUx6R.StatusChanged += SetStatus;
        _runtimeDataUiUx6R.RuntimeDataChanged += RefreshAfterUx6RMutation;
        AddChild(_runtimeDataUiUx6R);
    }

    private void OpenRuntimeDataUiUx6R()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer ses données runtime.", error: true);
            return;
        }
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6R();
        _runtimeDataUiUx6R.Open();
        SetStatus("UX6-R · règles, UI, événements, JSON et état sous une commande canonique.");
    }

    private void RefreshAfterUx6RMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6RActionState();
    }

    private void UpdateUx6RActionState()
    {
        if (_runtimeDataUiButtonUx6R is not null)
            _runtimeDataUiButtonUx6R.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6ROverlay()
    {
        if (_runtimeDataUiUx6R is not null) _runtimeDataUiUx6R.Close();
    }

    internal bool PrepareUx6REvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseOtherRuntimeSurfacesUx6R();
        _runtimeDataUiUx6R.Open();
        return _runtimeDataUiUx6R.PrepareEvidence(mode);
    }

    internal bool RunUx6RSmoke()
    {
        var originalPage = PageId;
        var originalBOpen = _gameplayFoundationWorkbenchUx6B.IsOpen;
        var originalCOpen = _gameplayCapabilitiesWorkbenchUx6C.IsOpen;
        var originalDOpen = _gameplaySimulationWorkbenchUx6D.IsOpen;
        var originalEOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalROpen = _runtimeDataUiUx6R.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var approvalB = _session.CaptureGameplayFoundationApprovalUx6B();
        var approvalC = _session.CaptureGameplayCapabilityApprovalUx6C();
        var approvalD = _session.CaptureGameplaySimulationApprovalUx6D();
        var approvalE = _session.CaptureGameplayProductionApprovalUx6E();
        var approvalR = _session.CaptureRuntimeDataApprovalUx6R();

        var uiTransaction = _runtimeDataUiUx6R.RunUiTransactionEvidence();
        var multiRecordPreserved = _runtimeDataUiUx6R.MultiRecordDraftPreserved;
        var prepared = PrepareUx6REvidence("ux6-r-runtime-data-ui");
        var uiReady = prepared && _runtimeDataUiUx6R.IsOpen &&
                      _runtimeDataUiUx6R.UsesIsolatedEvidence &&
                      _runtimeDataUiUx6R.HasExplicitSelection &&
                      _runtimeDataUiUx6R.PreviewReady &&
                      _runtimeDataUiUx6R.ShowsFiveWorkflows &&
                      _runtimeDataUiUx6R.ShowsHumanSurface &&
                      _runtimeDataUiUx6R.EvidenceSourceUntouched;
        _runtimeDataUiUx6R.CloseEvidence();
        var released = _runtimeDataUiUx6R.EvidenceResourcesReleased;
        var sourceRestored = _runtimeDataUiUx6R.SourceSessionRestored;
        var contract = _session.RunUx6RContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalBOpen) _gameplayFoundationWorkbenchUx6B.RestoreSourceSurfaceAfterSmoke();
        if (originalCOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        if (originalDOpen) _gameplaySimulationWorkbenchUx6D.RestoreSourceSurfaceAfterSmoke();
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalROpen) _runtimeDataUiUx6R.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayFoundationApprovalUx6B(approvalB);
        _session.RestoreGameplayCapabilityApprovalUx6C(approvalC);
        _session.RestoreGameplaySimulationApprovalUx6D(approvalD);
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreRuntimeDataApprovalUx6R(approvalR);

        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureGameplayFoundationApprovalUx6B() == approvalB &&
                              _session.CaptureGameplayCapabilityApprovalUx6C() == approvalC &&
                              _session.CaptureGameplaySimulationApprovalUx6D() == approvalD &&
                              _session.CaptureGameplayProductionApprovalUx6E() == approvalE &&
                              _session.CaptureRuntimeDataApprovalUx6R() == approvalR;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayFoundationWorkbenchUx6B.IsOpen == originalBOpen &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalCOpen &&
                              _gameplaySimulationWorkbenchUx6D.IsOpen == originalDOpen &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _runtimeDataUiUx6R.IsOpen == originalROpen;
        var valid = contract.Succeeded && uiTransaction && multiRecordPreserved && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_R_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleReadable.ToString().ToLowerInvariant()} " +
                 $"rules={contract.RuleValidation.ToString().ToLowerInvariant()} " +
                 $"ui={contract.UiValidation.ToString().ToLowerInvariant()} " +
                 $"json={contract.JsonValidation.ToString().ToLowerInvariant()} " +
                 $"snapshot={contract.SnapshotValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"rule_handle={contract.RuleResolved.ToString().ToLowerInvariant()} " +
                 $"ui_event={contract.UiEventPublished.ToString().ToLowerInvariant()} " +
                 $"json_revision={contract.JsonMutation.ToString().ToLowerInvariant()} " +
                 $"snapshot_policy={contract.SnapshotReady.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbor_profiles={contract.NeighborProfilesPreserved.ToString().ToLowerInvariant()} " +
                 $"stable_ids={contract.StableIdentities.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"multi_record_preserved={multiRecordPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherRuntimeSurfacesUx6R()
    {
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
    }
}
