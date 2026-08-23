using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptWorldApisPageUx6H _forgeScriptWorldApisUx6H = null!;
    private Button _forgeScriptWorldApisButtonUx6H = null!;

    private void BuildUx6HActions(HBoxContainer headingRow)
    {
        _forgeScriptWorldApisButtonUx6H = StudioStyle.Button("API monde", compact: true);
        _forgeScriptWorldApisButtonUx6H.TooltipText =
            "Inventaire, quête, dialogue, faction et perception sur la source canonique UX6-F.";
        _forgeScriptWorldApisButtonUx6H.Pressed += OpenForgeScriptWorldApisUx6H;
        headingRow.AddChild(_forgeScriptWorldApisButtonUx6H);
    }

    private void InitializeUx6HOverlay()
    {
        _forgeScriptWorldApisUx6H = new StudioForgeScriptWorldApisPageUx6H(_session);
        _forgeScriptWorldApisUx6H.StatusChanged += SetStatus;
        _forgeScriptWorldApisUx6H.ScriptSourceChanged += RefreshAfterUx6HMutation;
        AddChild(_forgeScriptWorldApisUx6H);
    }

    private void OpenForgeScriptWorldApisUx6H()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer les API monde.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptWorldApisUx6H.Open();
        SetStatus("UX6-H · cinq API monde R098, références humaines et projection immuable.");
    }

    private void RefreshAfterUx6HMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6HActionState();
    }

    private void UpdateUx6HActionState()
    {
        if (_forgeScriptWorldApisButtonUx6H is not null)
            _forgeScriptWorldApisButtonUx6H.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6HOverlay()
    {
        if (_forgeScriptWorldApisUx6H is not null) _forgeScriptWorldApisUx6H.Close();
    }

    internal bool PrepareUx6HEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6FOverlay();
        CloseUx6GOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptWorldApisUx6H.Open();
        return _forgeScriptWorldApisUx6H.PrepareEvidence(mode);
    }

    internal bool RunUx6HSmoke()
    {
        var originalPage = PageId;
        var originalFOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalGOpen = _forgeScriptAuthoringUx6G.IsOpen;
        var originalHOpen = _forgeScriptWorldApisUx6H.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureScriptApprovalUx6F();

        var uiTransaction = _forgeScriptWorldApisUx6H.RunUiTransactionEvidence();
        var prepared = PrepareUx6HEvidence("ux6-h-script-world-apis");
        var uiReady = prepared && _forgeScriptWorldApisUx6H.IsOpen &&
                      _forgeScriptWorldApisUx6H.UsesIsolatedEvidence &&
                      _forgeScriptWorldApisUx6H.HasExplicitSelection &&
                      _forgeScriptWorldApisUx6H.HumanCatalogsReady &&
                      _forgeScriptWorldApisUx6H.ReviewedSourceReady &&
                      _forgeScriptWorldApisUx6H.WorldApisReady &&
                      _forgeScriptWorldApisUx6H.NativeExecutionReady &&
                      _forgeScriptWorldApisUx6H.PreviewReady &&
                      _forgeScriptWorldApisUx6H.ShowsHumanSurface &&
                      _forgeScriptWorldApisUx6H.EvidenceSourceUntouched;
        _forgeScriptWorldApisUx6H.CloseEvidence();
        var released = _forgeScriptWorldApisUx6H.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptWorldApisUx6H.SourceSessionRestored;
        var contract = _session.RunUx6HContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalFOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalGOpen) _forgeScriptAuthoringUx6G.RestoreSourceSurfaceAfterSmoke();
        if (originalHOpen) _forgeScriptWorldApisUx6H.RestoreSourceSurfaceAfterSmoke();
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
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_H_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"shared_authority={contract.SharedSourceAuthority.ToString().ToLowerInvariant()} " +
                 $"human_catalogs={contract.HumanCatalogs.ToString().ToLowerInvariant()} " +
                 $"reviewed_source={contract.ReviewedSource.ToString().ToLowerInvariant()} " +
                 $"inventory={contract.InventoryApi.ToString().ToLowerInvariant()} " +
                 $"quest={contract.QuestApi.ToString().ToLowerInvariant()} " +
                 $"dialogue={contract.DialogueApi.ToString().ToLowerInvariant()} " +
                 $"faction={contract.FactionApi.ToString().ToLowerInvariant()} " +
                 $"perception={contract.PerceptionApi.ToString().ToLowerInvariant()} " +
                 $"optional_required={contract.OptionalRequiredResolution.ToString().ToLowerInvariant()} " +
                 $"rollback_stale={contract.RollbackAndStaleHandle.ToString().ToLowerInvariant()} " +
                 $"native_world={contract.NativeSandboxExecution.ToString().ToLowerInvariant()} " +
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
