using Forge.ModuleFormat.Serialization;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssetQualityWorkbenchUx3E _assetQualityUx3E = null!;
    private Button _assetQualityButtonUx3E = null!;

    private void BuildUx3EActions(HBoxContainer headingRow)
    {
        _assetQualityButtonUx3E = StudioStyle.Button("Style & budgets", compact: true);
        _assetQualityButtonUx3E.TooltipText =
            "Mesurer la compatibilité artistique et gouverner les budgets d’asset par plateforme.";
        _assetQualityButtonUx3E.Pressed += OpenAssetQualityUx3E;
        headingRow.AddChild(_assetQualityButtonUx3E);
    }

    private void InitializeUx3EOverlay()
    {
        _assetQualityUx3E = new StudioAssetQualityWorkbenchUx3E(_session);
        _assetQualityUx3E.StatusChanged += SetStatus;
        _assetQualityUx3E.ProjectChanged += RefreshAfterUx3EMutation;
        AddChild(_assetQualityUx3E);
    }

    private void OpenAssetQualityUx3E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de mesurer ses assets.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetQualityUx3E.Open();
        SetStatus("UX3-E · compatibilité artistique mesurée et budgets d’asset gouvernés.");
    }

    private void RefreshAfterUx3EMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx3EActionState();
    }

    private void UpdateUx3EActionState()
    {
        if (_assetQualityButtonUx3E is not null)
            _assetQualityButtonUx3E.Visible = PageId is "blueprints" or "assets" &&
                                              _session.Project is not null;
    }

    private void CloseUx3EOverlay()
    {
        if (_assetQualityUx3E is not null) _assetQualityUx3E.Close();
    }

    internal bool PrepareUx3EEvidence(string mode)
    {
        SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetQualityUx3E.Open();
        return _assetQualityUx3E.PrepareEvidence(mode);
    }

    internal void ReleaseUx3EAutomatedEvidence() => _assetQualityUx3E.CloseEvidence();

    internal bool RunUx3ESmoke()
    {
        var originalPage = PageId;
        var originalOpen = _assetQualityUx3E.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalWorkspace = EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile);
        var approval = _session.CaptureAssetQualityApprovalUx3E();

        var uiTransaction = _assetQualityUx3E.RunUiTransactionEvidence();
        var prepared = PrepareUx3EEvidence("ux3-e-asset-quality");
        var uiReady = prepared && _assetQualityUx3E.IsOpen &&
                      _assetQualityUx3E.UsesIsolatedEvidence &&
                      (_assetQualityUx3E.UsesRealModuleEvidence ^
                       _assetQualityUx3E.UsesCanonicalFallbackEvidence) &&
                      _assetQualityUx3E.HasExplicitSelection &&
                      _assetQualityUx3E.StylePreviewReady &&
                      _assetQualityUx3E.BudgetPreviewReady &&
                      _assetQualityUx3E.ShowsHumanSurface &&
                      _assetQualityUx3E.EvidenceSourceUntouched;
        _assetQualityUx3E.CloseEvidence();
        var released = _assetQualityUx3E.EvidenceResourcesReleased;
        var sourceRestored = _assetQualityUx3E.SourceSessionRestored;
        var contract = _session.RunUx3EContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _assetQualityUx3E.RestoreSourceSurfaceAfterSmoke();
        else _assetQualityUx3E.HideSourceSurfaceAfterSmoke();
        _session.RestoreAssetQualityApprovalUx3E(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile) == originalWorkspace &&
                              _session.CaptureAssetQualityApprovalUx3E() == approval;
        var surfaceRestored = PageId == originalPage && _assetQualityUx3E.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_E_{(valid ? "PASS" : "FAILED")} " +
                 "routes=PAL-017,PAL-018 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"metric_extraction={contract.MetricExtraction.ToString().ToLowerInvariant()} " +
                 $"six_warnings={contract.SixCompatibilityWarnings.ToString().ToLowerInvariant()} " +
                 $"invalid_style={contract.InvalidStyleBlocked.ToString().ToLowerInvariant()} " +
                 $"seven_axes={contract.SevenBudgetAxes.ToString().ToLowerInvariant()} " +
                 $"pass_warning_fail={contract.PlatformPassWarningFail.ToString().ToLowerInvariant()} " +
                 $"build_gate={contract.BuildGateBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"separate_approvals={contract.SeparateApprovals.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"module_tails={contract.ModuleTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"authoring_tails={contract.AuthoringTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"blueprint_tails={contract.BlueprintTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"blueprint_neighbors={contract.BlueprintNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
