using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptRuntimeToolsPageUx6L _forgeScriptRuntimeToolsUx6L = null!;
    private Button _forgeScriptRuntimeToolsButtonUx6L = null!;

    private void BuildUx6LActions(HBoxContainer headingRow)
    {
        _forgeScriptRuntimeToolsButtonUx6L = StudioStyle.Button("Runtime R099", compact: true);
        _forgeScriptRuntimeToolsButtonUx6L.TooltipText =
            "Audio et musique, horloge et timers, événements custom versionnés sur la source UX6-F.";
        _forgeScriptRuntimeToolsButtonUx6L.Pressed += OpenForgeScriptRuntimeToolsUx6L;
        headingRow.AddChild(_forgeScriptRuntimeToolsButtonUx6L);
    }

    private void InitializeUx6LOverlay()
    {
        _forgeScriptRuntimeToolsUx6L = new StudioForgeScriptRuntimeToolsPageUx6L(_session);
        _forgeScriptRuntimeToolsUx6L.StatusChanged += SetStatus;
        _forgeScriptRuntimeToolsUx6L.ScriptSourceChanged += RefreshAfterUx6LMutation;
        AddChild(_forgeScriptRuntimeToolsUx6L);
    }

    private void OpenForgeScriptRuntimeToolsUx6L()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser les outils runtime ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptRuntimeToolsUx6L.Open();
        SetStatus("UX6-L · audio, temps et événements custom ForgeScript R099.");
    }

    private void RefreshAfterUx6LMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6LActionState();
    }

    private void UpdateUx6LActionState()
    {
        if (_forgeScriptRuntimeToolsButtonUx6L is not null)
            _forgeScriptRuntimeToolsButtonUx6L.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6LOverlay()
    {
        if (_forgeScriptRuntimeToolsUx6L is not null) _forgeScriptRuntimeToolsUx6L.Close();
    }

    internal bool PrepareUx6LEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptRuntimeToolsUx6L.Open();
        return _forgeScriptRuntimeToolsUx6L.PrepareEvidence(mode);
    }

    internal bool RunUx6LSmoke()
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

        var uiTransaction = _forgeScriptRuntimeToolsUx6L.RunUiTransactionEvidence();
        var prepared = PrepareUx6LEvidence("ux6-l-script-runtime-tools");
        var uiReady = prepared && _forgeScriptRuntimeToolsUx6L.IsOpen &&
                      _forgeScriptRuntimeToolsUx6L.UsesIsolatedEvidence &&
                      _forgeScriptRuntimeToolsUx6L.HasExplicitSelection &&
                      _forgeScriptRuntimeToolsUx6L.ReviewedSourceReady &&
                      _forgeScriptRuntimeToolsUx6L.RuntimeReady &&
                      _forgeScriptRuntimeToolsUx6L.CompilationReady &&
                      _forgeScriptRuntimeToolsUx6L.PreviewReady &&
                      _forgeScriptRuntimeToolsUx6L.ShowsHumanSurface &&
                      _forgeScriptRuntimeToolsUx6L.EvidenceSourceUntouched;
        _forgeScriptRuntimeToolsUx6L.CloseEvidence();
        var released = _forgeScriptRuntimeToolsUx6L.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptRuntimeToolsUx6L.SourceSessionRestored;
        var contract = _session.RunUx6LContractSmoke();

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
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_L_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"shared_authority={contract.SharedSourceAuthority.ToString().ToLowerInvariant()} " +
                 $"reviewed_source={contract.ReviewedSource.ToString().ToLowerInvariant()} " +
                 $"audio={contract.AudioRuntime.ToString().ToLowerInvariant()} " +
                 $"time={contract.TimeRuntime.ToString().ToLowerInvariant()} " +
                 $"custom_events={contract.CustomEvents.ToString().ToLowerInvariant()} " +
                 $"api_catalog={contract.ApiCatalog.ToString().ToLowerInvariant()} " +
                 $"native={contract.NativeExecution.ToString().ToLowerInvariant()} " +
                 $"invalid_audio={contract.InvalidAudioBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_event={contract.InvalidEventBlocked.ToString().ToLowerInvariant()} " +
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
}
