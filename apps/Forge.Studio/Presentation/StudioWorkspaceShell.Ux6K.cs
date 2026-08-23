using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioModulePermissionsPageUx6K _modulePermissionsUx6K = null!;
    private Button _modulePermissionsButtonUx6K = null!;

    private void BuildUx6KActions(HBoxContainer headingRow)
    {
        _modulePermissionsButtonUx6K = StudioStyle.Button("Permissions module", compact: true);
        _modulePermissionsButtonUx6K.TooltipText =
            "Allowlist, kits et liaisons d’événements ForgeScript R015, avec autorité serveur et signature.";
        _modulePermissionsButtonUx6K.Pressed += OpenModulePermissionsUx6K;
        headingRow.AddChild(_modulePermissionsButtonUx6K);
    }

    private void InitializeUx6KOverlay()
    {
        _modulePermissionsUx6K = new StudioModulePermissionsPageUx6K(_session);
        _modulePermissionsUx6K.StatusChanged += SetStatus;
        AddChild(_modulePermissionsUx6K);
    }

    private void OpenModulePermissionsUx6K()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer ses permissions ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _modulePermissionsUx6K.Open();
        SetStatus("UX6-K · permissions ForgeScript par module R015.");
    }

    private void UpdateUx6KActionState()
    {
        if (_modulePermissionsButtonUx6K is not null)
            _modulePermissionsButtonUx6K.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6KOverlay()
    {
        if (_modulePermissionsUx6K is not null) _modulePermissionsUx6K.Close();
    }

    internal bool PrepareUx6KEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _modulePermissionsUx6K.Open();
        return _modulePermissionsUx6K.PrepareEvidence(mode);
    }

    internal bool RunUx6KSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalHOpen = _forgeScriptWorldApisUx6H.IsOpen;
        var originalIOpen = _forgeScriptWorldMovementUx6I.IsOpen;
        var originalJOpen = _forgeScriptSafetyPolicyUx6J.IsOpen;
        var originalKOpen = _modulePermissionsUx6K.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalScriptApproval = _session.CaptureScriptApprovalUx6F();
        var originalExecutionApproval = _session.CaptureExecutionPolicyApprovalUx6J();
        var originalPermissionApproval = _session.CaptureModulePermissionApprovalUx6K();

        var uiTransaction = _modulePermissionsUx6K.RunUiTransactionEvidence();
        var prepared = PrepareUx6KEvidence("ux6-k-script-module-permissions");
        var uiReady = prepared && _modulePermissionsUx6K.IsOpen &&
                      _modulePermissionsUx6K.UsesIsolatedEvidence &&
                      _modulePermissionsUx6K.HasExplicitSelection &&
                      _modulePermissionsUx6K.HumanCapabilitiesReady &&
                      _modulePermissionsUx6K.PreviewReady &&
                      _modulePermissionsUx6K.SimulationReady &&
                      _modulePermissionsUx6K.ShowsHumanSurface &&
                      _modulePermissionsUx6K.EvidenceSourceUntouched;
        _modulePermissionsUx6K.CloseEvidence();
        var released = _modulePermissionsUx6K.EvidenceResourcesReleased;
        var sourceRestored = _modulePermissionsUx6K.SourceSessionRestored;
        var contract = _session.RunUx6KContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
        if (originalIOpen) _forgeScriptWorldMovementUx6I.RestoreSourceSurfaceAfterSmoke();
        if (originalJOpen) _forgeScriptSafetyPolicyUx6J.RestoreSourceSurfaceAfterSmoke();
        if (originalKOpen) _modulePermissionsUx6K.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalExecutionApproval);
        _session.RestoreModulePermissionApprovalUx6K(originalPermissionApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalScriptApproval &&
                              _session.CaptureExecutionPolicyApprovalUx6J() == originalExecutionApproval &&
                              _session.CaptureModulePermissionApprovalUx6K() == originalPermissionApproval;
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptSafetyPolicyUx6J.IsOpen == originalJOpen &&
                              _modulePermissionsUx6K.IsOpen == originalKOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_K_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"human_capabilities={contract.HumanCapabilities.ToString().ToLowerInvariant()} " +
                 $"canonical_manifest={contract.CanonicalManifest.ToString().ToLowerInvariant()} " +
                 $"deny_default={contract.DenyByDefault.ToString().ToLowerInvariant()} " +
                 $"server_enforced={contract.ServerEnforced.ToString().ToLowerInvariant()} " +
                 $"signature={contract.SignaturePolicy.ToString().ToLowerInvariant()} " +
                 $"intersection={contract.KitBindingIntersection.ToString().ToLowerInvariant()} " +
                 $"authority_sandbox={contract.AuthoritySandbox.ToString().ToLowerInvariant()} " +
                 $"invalid_manifest={contract.InvalidManifestBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
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
