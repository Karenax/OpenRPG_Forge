using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioForgeScriptFoundationPageUx6F _forgeScriptFoundationUx6F = null!;
    private Button _forgeScriptFoundationButtonUx6F = null!;

    private void BuildUx6FActions(HBoxContainer headingRow)
    {
        _forgeScriptFoundationButtonUx6F = StudioStyle.Button("Atelier ForgeScript", compact: true);
        _forgeScriptFoundationButtonUx6F.TooltipText =
            "Sources C-like, compilation, bindings d’événements, fonction main et sandbox.";
        _forgeScriptFoundationButtonUx6F.Pressed += OpenForgeScriptFoundationUx6F;
        headingRow.AddChild(_forgeScriptFoundationButtonUx6F);
    }

    private void InitializeUx6FOverlay()
    {
        _forgeScriptFoundationUx6F = new StudioForgeScriptFoundationPageUx6F(_session);
        _forgeScriptFoundationUx6F.StatusChanged += SetStatus;
        _forgeScriptFoundationUx6F.ScriptSourceChanged += RefreshAfterUx6FMutation;
        AddChild(_forgeScriptFoundationUx6F);
    }

    private void OpenForgeScriptFoundationUx6F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses sources ForgeScript.", error: true);
            return;
        }
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptFoundationUx6F.Open();
        SetStatus("UX6-F · source canonique, événements typés, compilateur et sandbox bornée.");
    }

    private void RefreshAfterUx6FMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6FActionState();
    }

    private void UpdateUx6FActionState()
    {
        if (_forgeScriptFoundationButtonUx6F is not null)
            _forgeScriptFoundationButtonUx6F.Visible = PageId == "scripts" && _session.Project is not null;
    }

    private void CloseUx6FOverlay()
    {
        if (_forgeScriptFoundationUx6F is not null) _forgeScriptFoundationUx6F.Close();
    }

    internal bool PrepareUx6FEvidence(string mode)
    {
        if (PageId != "scripts") SwitchPage("scripts", notify: false);
        CloseUx6GOverlay();
        CloseUx6HOverlay();
        CloseUx6IOverlay();
        CloseUx6JOverlay();
        CloseUx6KOverlay();
        CloseUx6LOverlay();
        CloseUx6MOverlay();
        CloseUx6OOverlay();
        CloseUx6POverlay();
        _forgeScriptFoundationUx6F.Open();
        return _forgeScriptFoundationUx6F.PrepareEvidence(mode);
    }

    internal bool RunUx6FSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _forgeScriptFoundationUx6F.IsOpen;
        var originalLOpen = _forgeScriptRuntimeToolsUx6L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureScriptApprovalUx6F();

        var uiTransaction = _forgeScriptFoundationUx6F.RunUiTransactionEvidence();
        var prepared = PrepareUx6FEvidence("ux6-f-script-foundation");
        var uiReady = prepared && _forgeScriptFoundationUx6F.IsOpen &&
                      _forgeScriptFoundationUx6F.UsesIsolatedEvidence &&
                      _forgeScriptFoundationUx6F.PreviewReady &&
                      _forgeScriptFoundationUx6F.HasExplicitSelection &&
                      _forgeScriptFoundationUx6F.LegacySourcesVisible &&
                      _forgeScriptFoundationUx6F.ShowsHumanSurface &&
                      _forgeScriptFoundationUx6F.EvidenceSourceUntouched;
        _forgeScriptFoundationUx6F.CloseEvidence();
        var released = _forgeScriptFoundationUx6F.EvidenceResourcesReleased;
        var sourceRestored = _forgeScriptFoundationUx6F.SourceSessionRestored;
        var contract = _session.RunUx6FContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _forgeScriptFoundationUx6F.RestoreSourceSurfaceAfterSmoke();
        if (originalLOpen) _forgeScriptRuntimeToolsUx6L.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreScriptApprovalUx6F(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureScriptApprovalUx6F() == originalApproval;
        var surfaceRestored = PageId == originalPage && _forgeScriptFoundationUx6F.IsOpen == originalOpen &&
                              _forgeScriptRuntimeToolsUx6L.IsOpen == originalLOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_F_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"canonical_source={contract.CanonicalSource.ToString().ToLowerInvariant()} " +
                 $"legacy_protected={contract.LegacyProtected.ToString().ToLowerInvariant()} " +
                 $"typed_bindings={contract.TypedBindings.ToString().ToLowerInvariant()} " +
                 $"entry_point={contract.EntryPoint.ToString().ToLowerInvariant()} " +
                 $"invalid_path={contract.InvalidPathBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_handler={contract.InvalidHandlerBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"compilation={contract.Compilation.ToString().ToLowerInvariant()} " +
                 $"bytecode_cache={contract.BytecodeCache.ToString().ToLowerInvariant()} " +
                 $"sandbox={contract.SandboxExecution.ToString().ToLowerInvariant()} " +
                 $"quota={contract.QuotaAbuseBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_disk={contract.StaleDiskBlocked.ToString().ToLowerInvariant()} " +
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
