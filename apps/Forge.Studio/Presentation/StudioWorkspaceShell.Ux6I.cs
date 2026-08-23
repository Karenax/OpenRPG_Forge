using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptWorldMovementPageUx6I _forgeScriptWorldMovementUx6I = null!;
    private Button _forgeScriptWorldMovementButtonUx6I = null!;

    private void BuildUx6IActions(HBoxContainer headingRow)
    {
        _forgeScriptWorldMovementButtonUx6I = StudioStyle.Button("Monde & mouvement", compact: true);
        _forgeScriptWorldMovementButtonUx6I.TooltipText =
            "Zones, objets, mouvement, math, chaînes, variables locales et sandbox R013.";
        _forgeScriptWorldMovementButtonUx6I.Pressed += OpenForgeScriptWorldMovementUx6I;
        headingRow.AddChild(_forgeScriptWorldMovementButtonUx6I);
    }

    private void InitializeUx6IOverlay()
    {
        _forgeScriptWorldMovementUx6I = new StudioForgeScriptWorldMovementPageUx6I(_session);
        _forgeScriptWorldMovementUx6I.StatusChanged += SetStatus;
        _forgeScriptWorldMovementUx6I.ScriptSourceChanged += RefreshAfterUx6IMutation;
        AddChild(_forgeScriptWorldMovementUx6I);
    }

    private void OpenForgeScriptWorldMovementUx6I()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer monde et mouvement.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptWorldMovementUx6I.Open();
        SetStatus("UX6-I · monde, mouvement, utilitaires, variables locales et sandbox R013.");
    }

    private void RefreshAfterUx6IMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6IActionState();
    }

    private void UpdateUx6IActionState()
    {
        if (_forgeScriptWorldMovementButtonUx6I is not null)
            _forgeScriptWorldMovementButtonUx6I.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6IOverlay()
    {
        if (_forgeScriptWorldMovementUx6I is not null) _forgeScriptWorldMovementUx6I.Close();
    }

    internal bool PrepareUx6IEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptWorldMovementUx6I.Open();
        return _forgeScriptWorldMovementUx6I.PrepareEvidence(mode);
    }

    internal bool RunUx6ISmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalHOpen = _forgeScriptWorldApisUx6H.IsOpen;
        var originalIOpen = _forgeScriptWorldMovementUx6I.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureScriptApprovalUx6F();

        var uiTransaction = _forgeScriptWorldMovementUx6I.RunUiTransactionEvidence();
        var prepared = PrepareUx6IEvidence("ux6-i-world-movement-utilities");
        var uiReady = prepared && _forgeScriptWorldMovementUx6I.IsOpen &&
                      _forgeScriptWorldMovementUx6I.UsesIsolatedEvidence &&
                      _forgeScriptWorldMovementUx6I.HasExplicitSelection &&
                      _forgeScriptWorldMovementUx6I.HumanCatalogsReady &&
                      _forgeScriptWorldMovementUx6I.ReviewedSourceReady &&
                      _forgeScriptWorldMovementUx6I.WorldMovementReady &&
                      _forgeScriptWorldMovementUx6I.NativeExecutionReady &&
                      _forgeScriptWorldMovementUx6I.PreviewReady &&
                      _forgeScriptWorldMovementUx6I.ShowsHumanSurface &&
                      _forgeScriptWorldMovementUx6I.EvidenceSourceUntouched;
        _forgeScriptWorldMovementUx6I.CloseEvidence();
        var released = _forgeScriptWorldMovementUx6I.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptWorldMovementUx6I.SourceSessionRestored;
        var contract = _session.RunUx6IContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
        if (originalIOpen) _forgeScriptWorldMovementUx6I.RestoreSourceSurfaceAfterSmoke();
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
                              _forgeScriptWorldApisUx6H.IsOpen == originalHOpen &&
                              _forgeScriptWorldMovementUx6I.IsOpen == originalIOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_I_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"shared_authority={contract.SharedSourceAuthority.ToString().ToLowerInvariant()} " +
                 $"human_catalogs={contract.HumanCatalogs.ToString().ToLowerInvariant()} " +
                 $"reviewed_source={contract.ReviewedSource.ToString().ToLowerInvariant()} " +
                 $"world={contract.WorldApi.ToString().ToLowerInvariant()} " +
                 $"movement={contract.MovementApi.ToString().ToLowerInvariant()} " +
                 $"utilities={contract.UtilityApi.ToString().ToLowerInvariant()} " +
                 $"locals={contract.LocalVariables.ToString().ToLowerInvariant()} " +
                 $"sandbox_boundary={contract.SandboxBoundary.ToString().ToLowerInvariant()} " +
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
