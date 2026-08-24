using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _forgeScriptLiveToolsButtonUx6N = null!;

    private void BuildUx6NActions(HBoxContainer headingRow)
    {
        _forgeScriptLiveToolsButtonUx6N = StudioStyle.Button("Reload & debug", compact: true);
        _forgeScriptLiveToolsButtonUx6N.TooltipText =
            "Hot reload versionné et débogueur R099 sur une copie isolée de la source UX6-F.";
        _forgeScriptLiveToolsButtonUx6N.Pressed += OpenForgeScriptLiveToolsUx6N;
        headingRow.AddChild(_forgeScriptLiveToolsButtonUx6N);
    }

    private void OpenForgeScriptLiveToolsUx6N()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser le hot reload ou le débogueur.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6N();
        _forgeScriptRuntimeToolsUx6L.OpenLiveToolsUx6N("ux6-n-hot-reload");
        SetStatus("UX6-N · hot reload et débogueur ForgeScript R099.");
    }

    private void UpdateUx6NActionState()
    {
        if (_forgeScriptLiveToolsButtonUx6N is not null)
            _forgeScriptLiveToolsButtonUx6N.Visible = PageId == "scripts" && _session.Project is not null;
    }

    internal bool PrepareUx6NEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6N();
        _forgeScriptRuntimeToolsUx6L.OpenLiveToolsUx6N(mode);
        return _forgeScriptRuntimeToolsUx6L.PrepareEvidence(mode);
    }

    internal bool RunUx6NSmoke()
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

        var uiTransaction = _forgeScriptRuntimeToolsUx6L.RunUiTransactionEvidence(
            "ux6-n-hot-reload-debugger");
        var prepared = PrepareUx6NEvidence("ux6-n-hot-reload-debugger");
        var uiReady = prepared && _forgeScriptRuntimeToolsUx6L.IsOpen &&
                      _forgeScriptRuntimeToolsUx6L.UsesIsolatedEvidence &&
                      _forgeScriptRuntimeToolsUx6L.HasExplicitSelection &&
                      _forgeScriptRuntimeToolsUx6L.ReviewedSourceReady &&
                      _forgeScriptRuntimeToolsUx6L.RuntimeReady &&
                      _forgeScriptRuntimeToolsUx6L.LiveToolsReadyUx6N &&
                      _forgeScriptRuntimeToolsUx6L.CompilationReady &&
                      _forgeScriptRuntimeToolsUx6L.PreviewReady &&
                      _forgeScriptRuntimeToolsUx6L.ShowsHumanSurface &&
                      _forgeScriptRuntimeToolsUx6L.ShowsLiveHumanSurfaceUx6N &&
                      _forgeScriptRuntimeToolsUx6L.EvidenceSourceUntouched;
        _forgeScriptRuntimeToolsUx6L.CloseEvidence();
        var released = _forgeScriptRuntimeToolsUx6L.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptRuntimeToolsUx6L.SourceSessionRestored;
        var contract = _session.RunUx6NContractSmoke();

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
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_N_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"shared_authority={contract.SharedSourceAuthority.ToString().ToLowerInvariant()} " +
                 $"reviewed_source={contract.ReviewedSource.ToString().ToLowerInvariant()} " +
                 $"hot_reload={contract.HotReload.ToString().ToLowerInvariant()} " +
                 $"debugger={contract.Debugger.ToString().ToLowerInvariant()} " +
                 $"adapter={contract.Adapter.ToString().ToLowerInvariant()} " +
                 $"native={contract.NativeExecution.ToString().ToLowerInvariant()} " +
                 $"invalid_version={contract.InvalidVersionBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_permission={contract.InvalidPermissionBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"compilation={contract.Compilation.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"apply_undo_redo={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherScriptSurfacesUx6N()
    {
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
    }
}
