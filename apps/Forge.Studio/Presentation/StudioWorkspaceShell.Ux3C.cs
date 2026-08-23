using Forge.ModuleFormat.Serialization;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBlueprintDiscoveryWorkbenchUx3C _blueprintDiscoveryUx3C = null!;
    private Button _blueprintDiscoveryButtonUx3C = null!;

    private void BuildUx3CActions(HBoxContainer headingRow)
    {
        _blueprintDiscoveryButtonUx3C = StudioStyle.Button("Découvrir & déposer", compact: true);
        _blueprintDiscoveryButtonUx3C.TooltipText =
            "Favoris, récents, recherche avancée, miniatures et drag-and-drop de blueprints.";
        _blueprintDiscoveryButtonUx3C.Pressed += OpenBlueprintDiscoveryUx3C;
        headingRow.AddChild(_blueprintDiscoveryButtonUx3C);
    }

    private void InitializeUx3COverlay()
    {
        _blueprintDiscoveryUx3C = new StudioBlueprintDiscoveryWorkbenchUx3C(_session);
        _blueprintDiscoveryUx3C.StatusChanged += SetStatus;
        _blueprintDiscoveryUx3C.ProjectChanged += RefreshAfterUx3CMutation;
        AddChild(_blueprintDiscoveryUx3C);
    }

    private void OpenBlueprintDiscoveryUx3C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de parcourir ses blueprints.", error: true);
            return;
        }
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintDiscoveryUx3C.Open();
        SetStatus("UX3-C · favoris, récents, recherche avancée, miniatures et drag-and-drop.");
    }

    private void RefreshAfterUx3CMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx3CActionState();
    }

    private void UpdateUx3CActionState()
    {
        if (_blueprintDiscoveryButtonUx3C is not null)
            _blueprintDiscoveryButtonUx3C.Visible = PageId == "blueprints" && _session.Project is not null;
    }

    private void CloseUx3COverlay()
    {
        if (_blueprintDiscoveryUx3C is not null) _blueprintDiscoveryUx3C.Close();
    }

    internal bool PrepareUx3CEvidence(string mode)
    {
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintDiscoveryUx3C.Open();
        return _blueprintDiscoveryUx3C.PrepareEvidence(mode);
    }

    internal void ReleaseUx3CAutomatedEvidence() => _blueprintDiscoveryUx3C.CloseEvidence();

    internal bool RunUx3CSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _blueprintDiscoveryUx3C.IsOpen;
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
        var approval = _session.CaptureBlueprintDiscoveryApprovalUx3C();

        var uiTransaction = _blueprintDiscoveryUx3C.RunUiTransactionEvidence();
        var prepared = PrepareUx3CEvidence("ux3-c-blueprint-discovery");
        var dragPayload = prepared && _blueprintDiscoveryUx3C.DragDropObserved;
        var uiReady = prepared && _blueprintDiscoveryUx3C.IsOpen &&
                      _blueprintDiscoveryUx3C.UsesIsolatedEvidence &&
                      (_blueprintDiscoveryUx3C.UsesRealModuleEvidence ^
                       _blueprintDiscoveryUx3C.UsesCanonicalFallbackEvidence) &&
                      _blueprintDiscoveryUx3C.HasExplicitSelection &&
                      _blueprintDiscoveryUx3C.DiscoveryPreviewReady &&
                      _blueprintDiscoveryUx3C.DropPreviewReady &&
                      dragPayload &&
                      _blueprintDiscoveryUx3C.ThumbnailRendered &&
                      _blueprintDiscoveryUx3C.ShowsHumanSurface &&
                      _blueprintDiscoveryUx3C.EvidenceSourceUntouched;
        _blueprintDiscoveryUx3C.CloseEvidence();
        var released = _blueprintDiscoveryUx3C.EvidenceResourcesReleased;
        var sourceRestored = _blueprintDiscoveryUx3C.SourceSessionRestored;
        var contract = _session.RunUx3CContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _blueprintDiscoveryUx3C.RestoreSourceSurfaceAfterSmoke();
        else _blueprintDiscoveryUx3C.HideSourceSurfaceAfterSmoke();
        _session.RestoreBlueprintDiscoveryApprovalUx3C(approval);
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
                              EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile) ==
                              originalWorkspace &&
                              _session.CaptureBlueprintDiscoveryApprovalUx3C() == approval;
        var surfaceRestored = PageId == originalPage && _blueprintDiscoveryUx3C.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_C_{(valid ? "PASS" : "FAILED")} " +
                 "routes=PAL-009,PAL-010,PAL-011,PAL-012 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"favorites={contract.Favorites.ToString().ToLowerInvariant()} " +
                 $"recents={contract.Recents.ToString().ToLowerInvariant()} " +
                 $"advanced_search={contract.AdvancedSearch.ToString().ToLowerInvariant()} " +
                 $"thumbnail_metadata={contract.ThumbnailMetadata.ToString().ToLowerInvariant()} " +
                 $"thumbnail_loaded={(contract.ThumbnailLoaded && uiReady).ToString().ToLowerInvariant()} " +
                 $"honest_fallback={contract.HonestFallback.ToString().ToLowerInvariant()} " +
                 $"drag_payload={(contract.DragPayload && dragPayload).ToString().ToLowerInvariant()} " +
                 $"drop_plan={contract.DropPlan.ToString().ToLowerInvariant()} " +
                 $"invalid_drop={contract.InvalidDropBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"separate_approvals={contract.SeparateApprovals.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"blueprint_neighbors={contract.BlueprintNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"other_areas={contract.OtherAreasPreserved.ToString().ToLowerInvariant()} " +
                 $"existing_entities={contract.ExistingEntitiesPreserved.ToString().ToLowerInvariant()} " +
                 $"target_area_tails={contract.TargetAreaTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"area_neighbors={contract.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"workspace_neighbors={contract.WorkspaceNeighborsPreserved.ToString().ToLowerInvariant()} " +
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
