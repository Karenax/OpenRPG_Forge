using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBlueprintLibraryWorkbenchUx3B _blueprintLibraryUx3B = null!;
    private Button _blueprintLibraryButtonUx3B = null!;

    private void BuildUx3BActions(HBoxContainer headingRow)
    {
        _blueprintLibraryButtonUx3B = StudioStyle.Button("Propagation & prefabs", compact: true);
        _blueprintLibraryButtonUx3B.TooltipText =
            "Mettre à jour un blueprint, migrer ses instances, organiser ses catégories et composer ses prefabs.";
        _blueprintLibraryButtonUx3B.Pressed += OpenBlueprintLibraryUx3B;
        headingRow.AddChild(_blueprintLibraryButtonUx3B);
    }

    private void InitializeUx3BOverlay()
    {
        _blueprintLibraryUx3B = new StudioBlueprintLibraryWorkbenchUx3B(_session);
        _blueprintLibraryUx3B.StatusChanged += SetStatus;
        _blueprintLibraryUx3B.BlueprintChanged += RefreshAfterUx3BMutation;
        AddChild(_blueprintLibraryUx3B);
    }

    private void OpenBlueprintLibraryUx3B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer sa bibliothèque de blueprints.", error: true);
            return;
        }
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3AOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintLibraryUx3B.Open();
        SetStatus("UX3-B · propagation, migration, catégories, variantes et compositions.");
    }

    private void RefreshAfterUx3BMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx3BActionState();
    }

    private void UpdateUx3BActionState()
    {
        if (_blueprintLibraryButtonUx3B is not null)
            _blueprintLibraryButtonUx3B.Visible = PageId == "blueprints" && _session.Project is not null;
    }

    private void CloseUx3BOverlay()
    {
        if (_blueprintLibraryUx3B is not null) _blueprintLibraryUx3B.Close();
    }

    internal bool PrepareUx3BEvidence(string mode)
    {
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3AOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintLibraryUx3B.Open();
        return _blueprintLibraryUx3B.PrepareEvidence(mode);
    }

    internal void ReleaseUx3BAutomatedEvidence() => _blueprintLibraryUx3B.CloseEvidence();

    internal bool RunUx3BSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _blueprintLibraryUx3B.IsOpen;
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
        var approval = _session.CaptureBlueprintLibraryApprovalUx3B();

        var uiTransaction = _blueprintLibraryUx3B.RunUiTransactionEvidence();
        var prepared = PrepareUx3BEvidence("ux3-b-blueprint-library");
        var uiReady = prepared && _blueprintLibraryUx3B.IsOpen &&
                      _blueprintLibraryUx3B.UsesIsolatedEvidence &&
                      (_blueprintLibraryUx3B.UsesRealModuleEvidence ^
                       _blueprintLibraryUx3B.UsesCanonicalFallbackEvidence) &&
                      _blueprintLibraryUx3B.HasExplicitSelection &&
                      _blueprintLibraryUx3B.UpdatePreviewReady &&
                      _blueprintLibraryUx3B.PrefabPreviewReady &&
                      _blueprintLibraryUx3B.ShowsTwoTransactions &&
                      _blueprintLibraryUx3B.ShowsHumanSurface &&
                      _blueprintLibraryUx3B.EvidenceSourceUntouched;
        _blueprintLibraryUx3B.CloseEvidence();
        var released = _blueprintLibraryUx3B.EvidenceResourcesReleased;
        var sourceRestored = _blueprintLibraryUx3B.SourceSessionRestored;
        var contract = _session.RunUx3BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _blueprintLibraryUx3B.RestoreSourceSurfaceAfterSmoke();
        else _blueprintLibraryUx3B.HideSourceSurfaceAfterSmoke();
        _session.RestoreBlueprintLibraryApprovalUx3B(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.CaptureBlueprintLibraryApprovalUx3B() == approval;
        var surfaceRestored = PageId == originalPage && _blueprintLibraryUx3B.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_B_{(valid ? "PASS" : "FAILED")} " +
                 "routes=PAL-004,PAL-005,PAL-008,PAL-013,PAL-014 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"update_plan={contract.UpdatePlan.ToString().ToLowerInvariant()} " +
                 $"selective_promotion={contract.SelectivePromotion.ToString().ToLowerInvariant()} " +
                 $"semantic_version={contract.SemanticVersionBump.ToString().ToLowerInvariant()} " +
                 $"migration_impact={contract.MigrationImpact.ToString().ToLowerInvariant()} " +
                 $"exclusion_preserved={contract.ExclusionPreserved.ToString().ToLowerInvariant()} " +
                 $"overrides_preserved={contract.OverridesPreserved.ToString().ToLowerInvariant()} " +
                 $"categories_hierarchical={contract.CategoriesHierarchical.ToString().ToLowerInvariant()} " +
                 $"variant={contract.VariantConfigured.ToString().ToLowerInvariant()} " +
                 $"composition={contract.CompositionConfigured.ToString().ToLowerInvariant()} " +
                 $"runtime_projection={contract.RuntimeProjection.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"separate_approvals={contract.SeparateApprovals.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"blueprint_neighbors={contract.BlueprintNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"area_neighbors={contract.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
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
