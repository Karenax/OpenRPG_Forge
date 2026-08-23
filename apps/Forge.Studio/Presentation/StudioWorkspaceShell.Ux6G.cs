using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptAuthoringPageUx6G _forgeScriptAuthoringUx6G = null!;
    private Button _forgeScriptAuthoringButtonUx6G = null!;

    private void BuildUx6GActions(HBoxContainer headingRow)
    {
        _forgeScriptAuthoringButtonUx6G = StudioStyle.Button("Authoring & APIs", compact: true);
        _forgeScriptAuthoringButtonUx6G.TooltipText =
            "Éditeur assisté, Script Wizard, actions différées, combat et effets sur la source UX6-F.";
        _forgeScriptAuthoringButtonUx6G.Pressed += OpenForgeScriptAuthoringUx6G;
        headingRow.AddChild(_forgeScriptAuthoringButtonUx6G);
    }

    private void InitializeUx6GOverlay()
    {
        _forgeScriptAuthoringUx6G = new StudioForgeScriptAuthoringPageUx6G(_session);
        _forgeScriptAuthoringUx6G.StatusChanged += SetStatus;
        _forgeScriptAuthoringUx6G.ScriptSourceChanged += RefreshAfterUx6GMutation;
        AddChild(_forgeScriptAuthoringUx6G);
    }

    private void OpenForgeScriptAuthoringUx6G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser l’authoring ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptAuthoringUx6G.Open();
        SetStatus("UX6-G · éditeur assisté, Wizard revu et simulations action/combat/effet.");
    }

    private void RefreshAfterUx6GMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6GActionState();
    }

    private void UpdateUx6GActionState()
    {
        if (_forgeScriptAuthoringButtonUx6G is not null)
            _forgeScriptAuthoringButtonUx6G.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6GOverlay()
    {
        if (_forgeScriptAuthoringUx6G is not null) _forgeScriptAuthoringUx6G.Close();
    }

    internal bool PrepareUx6GEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptAuthoringUx6G.Open();
        return _forgeScriptAuthoringUx6G.PrepareEvidence(mode);
    }

    internal bool RunUx6GSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureScriptApprovalUx6F();

        var uiTransaction = _forgeScriptAuthoringUx6G.RunUiTransactionEvidence();
        var prepared = PrepareUx6GEvidence("ux6-g-script-authoring-gameplay");
        var uiReady = prepared && _forgeScriptAuthoringUx6G.IsOpen &&
                      _forgeScriptAuthoringUx6G.UsesIsolatedEvidence &&
                      _forgeScriptAuthoringUx6G.PreviewReady &&
                      _forgeScriptAuthoringUx6G.EditorReady &&
                      _forgeScriptAuthoringUx6G.WizardReady &&
                      _forgeScriptAuthoringUx6G.GameplayReady &&
                      _forgeScriptAuthoringUx6G.NativeExecutionReady &&
                      _forgeScriptAuthoringUx6G.HasExplicitSelection &&
                      _forgeScriptAuthoringUx6G.ShowsHumanSurface &&
                      _forgeScriptAuthoringUx6G.EvidenceSourceUntouched;
        _forgeScriptAuthoringUx6G.CloseEvidence();
        var released = _forgeScriptAuthoringUx6G.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptAuthoringUx6G.SourceSessionRestored;
        var contract = _session.RunUx6GContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _forgeScriptFoundationUx6F.IsOpen == originalFOpen &&
                              _forgeScriptAuthoringUx6G.IsOpen == originalGOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_G_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"shared_authority={contract.SharedSourceAuthority.ToString().ToLowerInvariant()} " +
                 $"editor={contract.EditorAnalysis.ToString().ToLowerInvariant()} " +
                 $"completion_signature={contract.CompletionAndSignature.ToString().ToLowerInvariant()} " +
                 $"formatting={contract.Formatting.ToString().ToLowerInvariant()} " +
                 $"clickable_diagnostics={contract.ClickableDiagnostics.ToString().ToLowerInvariant()} " +
                 $"wizard={contract.WizardReviewedSource.ToString().ToLowerInvariant()} " +
                 $"invalid_wizard={contract.InvalidWizardBlocked.ToString().ToLowerInvariant()} " +
                 $"actions={contract.DeferredActions.ToString().ToLowerInvariant()} " +
                 $"combat={contract.CombatApi.ToString().ToLowerInvariant()} " +
                 $"effects={contract.EffectApi.ToString().ToLowerInvariant()} " +
                 $"authority={contract.AuthorityBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"compilation={contract.Compilation.ToString().ToLowerInvariant()} " +
                 $"sandbox={contract.SandboxExecution.ToString().ToLowerInvariant()} " +
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
