using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioScriptAutomationPageUx6M _scriptAutomationUx6M = null!;
    private Button _scriptAutomationButtonUx6M = null!;

    private void BuildUx6MActions(HBoxContainer headingRow)
    {
        _scriptAutomationButtonUx6M = StudioStyle.Button("Automatisation G13", compact: true);
        _scriptAutomationButtonUx6M.TooltipText =
            "Caméra, routes par tags, switches module et switches IA via WorldFeatures.Scripts.";
        _scriptAutomationButtonUx6M.Pressed += OpenScriptAutomationUx6M;
        headingRow.AddChild(_scriptAutomationButtonUx6M);
    }

    private void InitializeUx6MOverlay()
    {
        _scriptAutomationUx6M = new StudioScriptAutomationPageUx6M(_session);
        _scriptAutomationUx6M.StatusChanged += SetStatus;
        _scriptAutomationUx6M.AutomationChanged += RefreshAfterUx6MMutation;
        AddChild(_scriptAutomationUx6M);
    }

    private void OpenScriptAutomationUx6M()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer son automatisation ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6M();
        _scriptAutomationUx6M.Open();
        SetStatus("UX6-M · caméra, tags et switches G13-AA/AB.");
    }

    private void RefreshAfterUx6MMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6MActionState();
    }

    private void UpdateUx6MActionState()
    {
        if (_scriptAutomationButtonUx6M is not null)
            _scriptAutomationButtonUx6M.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6MOverlay()
    {
        if (_scriptAutomationUx6M is not null) _scriptAutomationUx6M.Close();
    }

    internal bool PrepareUx6MEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6M();
        _scriptAutomationUx6M.Open();
        return _scriptAutomationUx6M.PrepareEvidence(mode);
    }

    internal bool RunUx6MSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalHOpen = _forgeScriptWorldApisUx6H.IsOpen;
        var originalIOpen = _forgeScriptWorldMovementUx6I.IsOpen;
        var originalJOpen = _forgeScriptSafetyPolicyUx6J.IsOpen;
        var originalKOpen = _modulePermissionsUx6K.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalMOpen = _scriptAutomationUx6M.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalScriptApproval = _session.CaptureScriptApprovalUx6F();
        var originalExecutionApproval = _session.CaptureExecutionPolicyApprovalUx6J();
        var originalPermissionApproval = _session.CaptureModulePermissionApprovalUx6K();
        var originalAutomationApproval = _session.CaptureScriptAutomationApprovalUx6M();

        var uiTransaction = _scriptAutomationUx6M.RunUiTransactionEvidence();
        var prepared = PrepareUx6MEvidence("ux6-m-script-automation");
        var uiReady = prepared && _scriptAutomationUx6M.IsOpen &&
                      _scriptAutomationUx6M.UsesIsolatedEvidence &&
                      _scriptAutomationUx6M.HasExplicitSelection &&
                      _scriptAutomationUx6M.PreviewReady &&
                      _scriptAutomationUx6M.RuntimeReady &&
                      _scriptAutomationUx6M.ShowsHumanSurface &&
                      _scriptAutomationUx6M.EvidenceSourceUntouched;
        _scriptAutomationUx6M.CloseEvidence();
        var released = _scriptAutomationUx6M.EvidenceResourcesReleased;
        var sourceRestored = _scriptAutomationUx6M.SourceSessionRestored;
        var contract = _session.RunUx6MContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
        if (originalIOpen) _forgeScriptWorldMovementUx6I.RestoreSourceSurfaceAfterSmoke();
        if (originalJOpen) _forgeScriptSafetyPolicyUx6J.RestoreSourceSurfaceAfterSmoke();
        if (originalKOpen) _modulePermissionsUx6K.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        if (originalMOpen) _scriptAutomationUx6M.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalExecutionApproval);
        _session.RestoreModulePermissionApprovalUx6K(originalPermissionApproval);
        _session.RestoreScriptAutomationApprovalUx6M(originalAutomationApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalScriptApproval &&
                              _session.CaptureExecutionPolicyApprovalUx6J() == originalExecutionApproval &&
                              _session.CaptureModulePermissionApprovalUx6K() == originalPermissionApproval &&
                              Equals(_session.CaptureScriptAutomationApprovalUx6M(), originalAutomationApproval);
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptSafetyPolicyUx6J.IsOpen == originalJOpen &&
                              _modulePermissionsUx6K.IsOpen == originalKOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen &&
                              _scriptAutomationUx6M.IsOpen == originalMOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_M_{(valid ? "PASS" : "FAILED")} " +
                 $"module={contract.ExplicitModule.ToString().ToLowerInvariant()} " +
                 $"camera={contract.CameraAutomation.ToString().ToLowerInvariant()} " +
                 $"tags={contract.TagRouting.ToString().ToLowerInvariant()} " +
                 $"module_flags={contract.ModuleFlags.ToString().ToLowerInvariant()} " +
                 $"ai_settings={contract.AiSettings.ToString().ToLowerInvariant()} " +
                 $"invalid_camera={contract.InvalidCameraBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_tag={contract.InvalidTagBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_flag={contract.InvalidFlagBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_ai={contract.InvalidAiBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"apply_undo_redo={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherScriptSurfacesUx6M()
    {
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
    }
}
