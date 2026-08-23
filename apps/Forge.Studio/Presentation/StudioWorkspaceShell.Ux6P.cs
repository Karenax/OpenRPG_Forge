using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptInteropPageUx6P _forgeScriptInteropUx6P = null!;
    private Button _forgeScriptInteropButtonUx6P = null!;

    private void BuildUx6PActions(HBoxContainer headingRow)
    {
        _forgeScriptInteropButtonUx6P = StudioStyle.Button("Interop MCP", compact: true);
        _forgeScriptInteropButtonUx6P.TooltipText =
            "Façade commune éditeur/MCP R101, plans source et liaison, snapshot et sandbox.";
        _forgeScriptInteropButtonUx6P.Pressed += OpenForgeScriptInteropUx6P;
        headingRow.AddChild(_forgeScriptInteropButtonUx6P);
    }

    private void InitializeUx6POverlay()
    {
        _forgeScriptInteropUx6P = new StudioForgeScriptInteropPageUx6P(_session);
        _forgeScriptInteropUx6P.StatusChanged += SetStatus;
        AddChild(_forgeScriptInteropUx6P);
    }

    private void OpenForgeScriptInteropUx6P()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser l’interop ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6P();
        _forgeScriptInteropUx6P.Open();
        SetStatus("UX6-P · interop éditeur/MCP ForgeScript R101.");
    }

    private void UpdateUx6PActionState()
    {
        if (_forgeScriptInteropButtonUx6P is not null)
            _forgeScriptInteropButtonUx6P.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6POverlay()
    {
        if (_forgeScriptInteropUx6P is not null) _forgeScriptInteropUx6P.Close();
        // UX6-Q is the next exclusive full-screen Scripts surface. Every existing
        // Scripts lane already closes UX6-P before opening, so closing Q here keeps
        // mutual exclusion centralized without rewriting sixteen established lanes.
        if (_forgeScriptCompilerAssistantUx6Q is not null) _forgeScriptCompilerAssistantUx6Q.Close();
    }

    internal bool PrepareUx6PEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseOtherScriptSurfacesUx6P();
        _forgeScriptInteropUx6P.Open();
        return _forgeScriptInteropUx6P.PrepareEvidence(mode);
    }

    internal bool RunUx6PSmoke()
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

        var uiTransaction = _forgeScriptInteropUx6P.RunUiTransactionEvidence();
        var prepared = PrepareUx6PEvidence("ux6-p-script-interop");
        var uiReady = prepared && _forgeScriptInteropUx6P.IsOpen &&
                      _forgeScriptInteropUx6P.UsesIsolatedEvidence &&
                      _forgeScriptInteropUx6P.HasExplicitSelection &&
                      _forgeScriptInteropUx6P.HumanControlsReady &&
                      _forgeScriptInteropUx6P.PreviewReady &&
                      _forgeScriptInteropUx6P.SimulationReady &&
                      _forgeScriptInteropUx6P.ShowsHumanSurface &&
                      _forgeScriptInteropUx6P.EvidenceSourceUntouched;
        _forgeScriptInteropUx6P.CloseEvidence();
        var released = _forgeScriptInteropUx6P.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptInteropUx6P.SourceSessionRestored;
        var contract = _session.RunUx6PContractSmoke();

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
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalExecutionApproval);
        _session.RestoreModulePermissionApprovalUx6K(originalPermissionApproval);
        _session.RestoreScriptAutomationApprovalUx6M(originalAutomationApproval);
        _session.RestoreScriptQualityApprovalUx6O(originalQualityApproval);
        _session.RestoreScriptInteropApprovalUx6P(originalInteropApproval);
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
                              _session.CaptureScriptInteropApprovalUx6P() == originalInteropApproval;
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
                              _forgeScriptInteropUx6P.IsOpen == originalPOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_P_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"canonical_profile={contract.CanonicalProfile.ToString().ToLowerInvariant()} " +
                 $"api={contract.Api.ToString().ToLowerInvariant()} " +
                 $"source_transactions={contract.SourceTransactions.ToString().ToLowerInvariant()} " +
                 $"compile={contract.Compilation.ToString().ToLowerInvariant()} " +
                 $"tests={contract.SandboxTests.ToString().ToLowerInvariant()} " +
                 $"binding_transactions={contract.BindingTransactions.ToString().ToLowerInvariant()} " +
                 $"snapshot={contract.SnapshotRoundTrip.ToString().ToLowerInvariant()} " +
                 $"adapter={contract.RuntimeAdapter.ToString().ToLowerInvariant()} " +
                 $"native={contract.NativeSandbox.ToString().ToLowerInvariant()} " +
                 $"quota_recovery={contract.QuotaRecovery.ToString().ToLowerInvariant()} " +
                 $"invalid_profiles={contract.InvalidProfilesBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_source={contract.InvalidSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_binding={contract.InvalidBindingBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"apply_undo_redo={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"source_preserved={contract.SourcePreserved.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private void CloseOtherScriptSurfacesUx6P()
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
