using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptCompilerAssistantPageUx6Q _forgeScriptCompilerAssistantUx6Q = null!;
    private Button _forgeScriptCompilerAssistantButtonUx6Q = null!;

    private void BuildUx6QActions(HBoxContainer headingRow)
    {
        _forgeScriptCompilerAssistantButtonUx6Q = StudioStyle.Button("Compiler & assister", compact: true);
        _forgeScriptCompilerAssistantButtonUx6Q.TooltipText =
            "Compilation incrémentale EE-031 et création/réparation ForgeScript assistée AI-018.";
        _forgeScriptCompilerAssistantButtonUx6Q.Pressed += OpenForgeScriptCompilerAssistantUx6Q;
        headingRow.AddChild(_forgeScriptCompilerAssistantButtonUx6Q);
    }

    private void InitializeUx6QOverlay()
    {
        _forgeScriptCompilerAssistantUx6Q = new StudioForgeScriptCompilerAssistantPageUx6Q(_session);
        _forgeScriptCompilerAssistantUx6Q.StatusChanged += SetStatus;
        AddChild(_forgeScriptCompilerAssistantUx6Q);
    }

    private void OpenForgeScriptCompilerAssistantUx6Q()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser le compilateur ou l’assistant.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6Q();
        _forgeScriptCompilerAssistantUx6Q.Open();
        SetStatus("UX6-Q · compilation moderne et création assistée ForgeScript.");
    }

    private void UpdateUx6QActionState()
    {
        if (_forgeScriptCompilerAssistantButtonUx6Q is not null)
            _forgeScriptCompilerAssistantButtonUx6Q.Visible = PageId == "scripts" &&
                                                               _session.Project is not null;
    }

    private void CloseUx6QOverlay()
    {
        if (_forgeScriptCompilerAssistantUx6Q is not null) _forgeScriptCompilerAssistantUx6Q.Close();
    }

    internal bool PrepareUx6QEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6Q();
        _forgeScriptCompilerAssistantUx6Q.Open();
        return _forgeScriptCompilerAssistantUx6Q.PrepareEvidence(mode);
    }

    internal bool RunUx6QSmoke()
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
        var originalOOpen = _forgeScriptQualityUx6O.IsOpen;
        var originalPOpen = _forgeScriptInteropUx6P.IsOpen;
        var originalQOpen = _forgeScriptCompilerAssistantUx6Q.IsOpen;
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
        var originalQualityApproval = _session.CaptureScriptQualityApprovalUx6O();
        var originalInteropApproval = _session.CaptureScriptInteropApprovalUx6P();
        var originalCompilerAssistantApproval = _session.CaptureCompilerAssistantApprovalUx6Q();

        var uiTransaction = _forgeScriptCompilerAssistantUx6Q.RunUiTransactionEvidence();
        var prepared = PrepareUx6QEvidence("ux6-q-compiler-assistant");
        var uiReady = prepared && _forgeScriptCompilerAssistantUx6Q.IsOpen &&
                      _forgeScriptCompilerAssistantUx6Q.UsesIsolatedEvidence &&
                      _forgeScriptCompilerAssistantUx6Q.HasExplicitSelection &&
                      _forgeScriptCompilerAssistantUx6Q.HumanControlsReady &&
                      _forgeScriptCompilerAssistantUx6Q.CompilerPreviewReady &&
                      _forgeScriptCompilerAssistantUx6Q.CompilerEvidenceReady &&
                      _forgeScriptCompilerAssistantUx6Q.ProposalReady &&
                      _forgeScriptCompilerAssistantUx6Q.AssistantPreviewReady &&
                      _forgeScriptCompilerAssistantUx6Q.ShowsHumanSurface &&
                      _forgeScriptCompilerAssistantUx6Q.EvidenceSourceUntouched;
        _forgeScriptCompilerAssistantUx6Q.CloseEvidence();
        var released = _forgeScriptCompilerAssistantUx6Q.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptCompilerAssistantUx6Q.SourceSessionRestored;
        var contract = _session.RunUx6QContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
        if (originalIOpen) _forgeScriptWorldMovementUx6I.RestoreSourceSurfaceAfterSmoke();
        if (originalJOpen) _forgeScriptSafetyPolicyUx6J.RestoreSourceSurfaceAfterSmoke();
        if (originalKOpen) _modulePermissionsUx6K.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        if (originalMOpen) _scriptAutomationUx6M.RestoreSourceSurfaceAfterSmoke();
        if (originalOOpen) _forgeScriptQualityUx6O.RestoreSourceSurfaceAfterSmoke();
        if (originalPOpen) _forgeScriptInteropUx6P.RestoreSourceSurfaceAfterSmoke();
        if (originalQOpen) _forgeScriptCompilerAssistantUx6Q.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalExecutionApproval);
        _session.RestoreModulePermissionApprovalUx6K(originalPermissionApproval);
        _session.RestoreScriptAutomationApprovalUx6M(originalAutomationApproval);
        _session.RestoreScriptQualityApprovalUx6O(originalQualityApproval);
        _session.RestoreScriptInteropApprovalUx6P(originalInteropApproval);
        _session.RestoreCompilerAssistantApprovalUx6Q(originalCompilerAssistantApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalScriptApproval &&
                              _session.CaptureExecutionPolicyApprovalUx6J() == originalExecutionApproval &&
                              _session.CaptureModulePermissionApprovalUx6K() == originalPermissionApproval &&
                              _session.CaptureScriptAutomationApprovalUx6M() == originalAutomationApproval &&
                              _session.CaptureScriptQualityApprovalUx6O() == originalQualityApproval &&
                              _session.CaptureScriptInteropApprovalUx6P() == originalInteropApproval &&
                              _session.CaptureCompilerAssistantApprovalUx6Q() ==
                              originalCompilerAssistantApproval;
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptSafetyPolicyUx6J.IsOpen == originalJOpen &&
                              _modulePermissionsUx6K.IsOpen == originalKOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen &&
                              _scriptAutomationUx6M.IsOpen == originalMOpen &&
                              _forgeScriptQualityUx6O.IsOpen == originalOOpen &&
                              _forgeScriptInteropUx6P.IsOpen == originalPOpen &&
                              _forgeScriptCompilerAssistantUx6Q.IsOpen == originalQOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_Q_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"canonical_profile={contract.CanonicalProfile.ToString().ToLowerInvariant()} " +
                 $"invalid_compiler={contract.InvalidCompilerBlocked.ToString().ToLowerInvariant()} " +
                 $"compiler_dry_run={contract.CompilerDryRun.ToString().ToLowerInvariant()} " +
                 $"dependencies={contract.DependencyCompilation.ToString().ToLowerInvariant()} " +
                 $"cache={contract.CacheReuse.ToString().ToLowerInvariant()} " +
                 $"invalidation={contract.DependencyInvalidation.ToString().ToLowerInvariant()} " +
                 $"stable_diagnostics={contract.StableDiagnostics.ToString().ToLowerInvariant()} " +
                 $"telemetry={contract.Telemetry.ToString().ToLowerInvariant()} " +
                 $"conformance={contract.Conformance.ToString().ToLowerInvariant()} " +
                 $"compiler_transactions={contract.CompilerApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"compiler_persistence={contract.CompilerPersistence.ToString().ToLowerInvariant()} " +
                 $"invalid_assistant={contract.InvalidAssistantBlocked.ToString().ToLowerInvariant()} " +
                 $"proposal_compile={contract.ProposalCompiled.ToString().ToLowerInvariant()} " +
                 $"sandbox={contract.SandboxTests.ToString().ToLowerInvariant()} " +
                 $"offline={contract.OfflineProvider.ToString().ToLowerInvariant()} " +
                 $"approval={contract.ApprovalRequired.ToString().ToLowerInvariant()} " +
                 $"permission={contract.PermissionRequired.ToString().ToLowerInvariant()} " +
                 $"stale_draft={contract.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_disk={contract.StaleDiskBlocked.ToString().ToLowerInvariant()} " +
                 $"assistant_dry_run={contract.CanonicalAssistantDryRun.ToString().ToLowerInvariant()} " +
                 $"assistant_transactions={contract.AssistantApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"source_metadata={contract.SourceMetadataRoundTrip.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherScriptSurfacesUx6Q()
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
        CloseUx6QOverlay();
    }
}
