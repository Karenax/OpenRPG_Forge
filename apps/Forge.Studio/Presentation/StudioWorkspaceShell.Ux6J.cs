using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptSafetyPolicyPageUx6J _forgeScriptSafetyPolicyUx6J = null!;
    private Button _forgeScriptSafetyPolicyButtonUx6J = null!;

    private void BuildUx6JActions(HBoxContainer headingRow)
    {
        _forgeScriptSafetyPolicyButtonUx6J = StudioStyle.Button("Sécurité & quotas", compact: true);
        _forgeScriptSafetyPolicyButtonUx6J.TooltipText =
            "Handles opaques, quotas d’instructions et mémoire, déterminisme et version d’API R014.";
        _forgeScriptSafetyPolicyButtonUx6J.Pressed += OpenForgeScriptSafetyPolicyUx6J;
        headingRow.AddChild(_forgeScriptSafetyPolicyButtonUx6J);
    }

    private void InitializeUx6JOverlay()
    {
        _forgeScriptSafetyPolicyUx6J = new StudioForgeScriptSafetyPolicyPageUx6J(_session);
        _forgeScriptSafetyPolicyUx6J.StatusChanged += SetStatus;
        AddChild(_forgeScriptSafetyPolicyUx6J);
    }

    private void OpenForgeScriptSafetyPolicyUx6J()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer la sécurité ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptSafetyPolicyUx6J.Open();
        SetStatus("UX6-J · handles opaques, quotas, déterminisme et compatibilité API R014.");
    }

    private void UpdateUx6JActionState()
    {
        if (_forgeScriptSafetyPolicyButtonUx6J is not null)
            _forgeScriptSafetyPolicyButtonUx6J.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6JOverlay()
    {
        if (_forgeScriptSafetyPolicyUx6J is not null) _forgeScriptSafetyPolicyUx6J.Close();
    }

    internal bool PrepareUx6JEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptSafetyPolicyUx6J.Open();
        return _forgeScriptSafetyPolicyUx6J.PrepareEvidence(mode);
    }

    internal bool RunUx6JSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalHOpen = _forgeScriptWorldApisUx6H.IsOpen;
        var originalIOpen = _forgeScriptWorldMovementUx6I.IsOpen;
        var originalJOpen = _forgeScriptSafetyPolicyUx6J.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalScriptApproval = _session.CaptureScriptApprovalUx6F();
        var originalPolicyApproval = _session.CaptureExecutionPolicyApprovalUx6J();

        var uiTransaction = _forgeScriptSafetyPolicyUx6J.RunUiTransactionEvidence();
        var prepared = PrepareUx6JEvidence("ux6-j-script-safety-policy");
        var uiReady = prepared && _forgeScriptSafetyPolicyUx6J.IsOpen &&
                      _forgeScriptSafetyPolicyUx6J.UsesIsolatedEvidence &&
                      _forgeScriptSafetyPolicyUx6J.HasExplicitSelection &&
                      _forgeScriptSafetyPolicyUx6J.HumanCatalogsReady &&
                      _forgeScriptSafetyPolicyUx6J.PreviewReady &&
                      _forgeScriptSafetyPolicyUx6J.SimulationReady &&
                      _forgeScriptSafetyPolicyUx6J.ShowsHumanSurface &&
                      _forgeScriptSafetyPolicyUx6J.EvidenceSourceUntouched;
        _forgeScriptSafetyPolicyUx6J.CloseEvidence();
        var released = _forgeScriptSafetyPolicyUx6J.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptSafetyPolicyUx6J.SourceSessionRestored;
        var contract = _session.RunUx6JContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
        if (originalIOpen) _forgeScriptWorldMovementUx6I.RestoreSourceSurfaceAfterSmoke();
        if (originalJOpen) _forgeScriptSafetyPolicyUx6J.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalScriptApproval);
        _session.RestoreExecutionPolicyApprovalUx6J(originalPolicyApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalScriptApproval &&
                              _session.CaptureExecutionPolicyApprovalUx6J() == originalPolicyApproval;
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptSafetyPolicyUx6J.IsOpen == originalJOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_J_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"human_catalogs={contract.HumanCatalogs.ToString().ToLowerInvariant()} " +
                 $"canonical_policy={contract.CanonicalPolicy.ToString().ToLowerInvariant()} " +
                 $"handles={contract.OpaqueHandles.ToString().ToLowerInvariant()} " +
                 $"instruction_budget={contract.InstructionBudgets.ToString().ToLowerInvariant()} " +
                 $"memory_budget={contract.MemoryBudgets.ToString().ToLowerInvariant()} " +
                 $"determinism={contract.DeterministicReplay.ToString().ToLowerInvariant()} " +
                 $"api={contract.ApiCompatibility.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"invalid_policy={contract.InvalidPolicyBlocked.ToString().ToLowerInvariant()} " +
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
