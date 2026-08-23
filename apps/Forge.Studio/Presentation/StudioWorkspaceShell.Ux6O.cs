using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptQualityPageUx6O _forgeScriptQualityUx6O = null!;
    private Button _forgeScriptQualityButtonUx6O = null!;

    private void BuildUx6OActions(HBoxContainer headingRow)
    {
        _forgeScriptQualityButtonUx6O = StudioStyle.Button("Qualité scripts", compact: true);
        _forgeScriptQualityButtonUx6O.TooltipText =
            "Tests, profilage, documentation, analyse statique et état persistant ForgeScript R100.";
        _forgeScriptQualityButtonUx6O.Pressed += OpenForgeScriptQualityUx6O;
        headingRow.AddChild(_forgeScriptQualityButtonUx6O);
    }

    private void InitializeUx6OOverlay()
    {
        _forgeScriptQualityUx6O = new StudioForgeScriptQualityPageUx6O(_session);
        _forgeScriptQualityUx6O.StatusChanged += SetStatus;
        AddChild(_forgeScriptQualityUx6O);
    }

    private void OpenForgeScriptQualityUx6O()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser la qualité ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6POverlay();
        _forgeScriptQualityUx6O.Open();
        SetStatus("UX6-O · qualité ForgeScript R100.");
    }

    private void UpdateUx6OActionState()
    {
        if (_forgeScriptQualityButtonUx6O is not null)
            _forgeScriptQualityButtonUx6O.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6OOverlay()
    {
        if (_forgeScriptQualityUx6O is not null) _forgeScriptQualityUx6O.Close();
    }

    internal bool PrepareUx6OEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6POverlay();
        _forgeScriptQualityUx6O.Open();
        return _forgeScriptQualityUx6O.PrepareEvidence(mode);
    }

    internal bool RunUx6OSmoke()
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

        var uiTransaction = _forgeScriptQualityUx6O.RunUiTransactionEvidence();
        var prepared = PrepareUx6OEvidence("ux6-o-script-quality");
        var uiReady = prepared && _forgeScriptQualityUx6O.IsOpen &&
                      _forgeScriptQualityUx6O.UsesIsolatedEvidence &&
                      _forgeScriptQualityUx6O.HasExplicitSelection &&
                      _forgeScriptQualityUx6O.HumanControlsReady &&
                      _forgeScriptQualityUx6O.PreviewReady &&
                      _forgeScriptQualityUx6O.SimulationReady &&
                      _forgeScriptQualityUx6O.ShowsHumanSurface &&
                      _forgeScriptQualityUx6O.EvidenceSourceUntouched;
        _forgeScriptQualityUx6O.CloseEvidence();
        var released = _forgeScriptQualityUx6O.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptQualityUx6O.SourceSessionRestored;
        var contract = _session.RunUx6OContractSmoke();

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
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalExecutionApproval);
        _session.RestoreModulePermissionApprovalUx6K(originalPermissionApproval);
        _session.RestoreScriptAutomationApprovalUx6M(originalAutomationApproval);
        _session.RestoreScriptQualityApprovalUx6O(originalQualityApproval);
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
                              _session.CaptureScriptQualityApprovalUx6O() == originalQualityApproval;
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptSafetyPolicyUx6J.IsOpen == originalJOpen &&
                              _modulePermissionsUx6K.IsOpen == originalKOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen &&
                              _scriptAutomationUx6M.IsOpen == originalMOpen &&
                              _forgeScriptQualityUx6O.IsOpen == originalOOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_O_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"canonical_profile={contract.CanonicalProfile.ToString().ToLowerInvariant()} " +
                 $"unit_tests={contract.UnitTests.ToString().ToLowerInvariant()} " +
                 $"profiler={contract.Profiler.ToString().ToLowerInvariant()} " +
                 $"documentation={contract.Documentation.ToString().ToLowerInvariant()} " +
                 $"static_analysis={contract.StaticAnalysis.ToString().ToLowerInvariant()} " +
                 $"state={contract.StateSerialization.ToString().ToLowerInvariant()} " +
                 $"adapter={contract.RuntimeAdapter.ToString().ToLowerInvariant()} " +
                 $"native={contract.NativeSandbox.ToString().ToLowerInvariant()} " +
                 $"invalid_profiles={contract.InvalidProfilesBlocked.ToString().ToLowerInvariant()} " +
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
}
