using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBlueprintCatalogWorkbenchUx3A _blueprintCatalogUx3A = null!;
    private Button _blueprintCatalogButtonUx3A = null!;

    private void BuildUx3AActions(HBoxContainer headingRow)
    {
        _blueprintCatalogButtonUx3A = StudioStyle.Button("Blueprints & palettes", compact: true);
        _blueprintCatalogButtonUx3A.TooltipText =
            "Définition réutilisable, dérivation d’instance et palettes Standard/Projet.";
        _blueprintCatalogButtonUx3A.Pressed += OpenBlueprintCatalogUx3A;
        headingRow.AddChild(_blueprintCatalogButtonUx3A);
    }

    private void InitializeUx3AOverlay()
    {
        _blueprintCatalogUx3A = new StudioBlueprintCatalogWorkbenchUx3A(_session);
        _blueprintCatalogUx3A.StatusChanged += SetStatus;
        _blueprintCatalogUx3A.BlueprintChanged += RefreshAfterUx3AMutation;
        AddChild(_blueprintCatalogUx3A);
    }

    private void OpenBlueprintCatalogUx3A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses blueprints.", error: true);
            return;
        }
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintCatalogUx3A.Open();
        SetStatus("UX3-A · blueprints réutilisables, instances dérivées et palettes humaines.");
    }

    private void RefreshAfterUx3AMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx3AActionState();
    }

    private void UpdateUx3AActionState()
    {
        if (_blueprintCatalogButtonUx3A is not null)
            _blueprintCatalogButtonUx3A.Visible = PageId == "blueprints" && _session.Project is not null;
    }

    private void CloseUx3AOverlay()
    {
        if (_blueprintCatalogUx3A is not null) _blueprintCatalogUx3A.Close();
    }

    internal bool PrepareUx3AEvidence(string mode)
    {
        if (PageId != "blueprints") SwitchPage("blueprints", notify: false);
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintCatalogUx3A.Open();
        return _blueprintCatalogUx3A.PrepareEvidence(mode);
    }

    internal bool RunUx3ASmoke()
    {
        var originalPage = PageId;
        var originalOpen = _blueprintCatalogUx3A.IsOpen;
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
        var approval = _session.CaptureBlueprintApprovalUx3A();

        var uiTransaction = _blueprintCatalogUx3A.RunUiTransactionEvidence();
        var prepared = PrepareUx3AEvidence("ux3-a-blueprint-foundation");
        var uiReady = prepared && _blueprintCatalogUx3A.IsOpen &&
                      _blueprintCatalogUx3A.UsesIsolatedEvidence &&
                      (_blueprintCatalogUx3A.UsesRealModuleEvidence ^
                       _blueprintCatalogUx3A.UsesCanonicalFallbackEvidence) &&
                      _blueprintCatalogUx3A.HasExplicitSelection &&
                      _blueprintCatalogUx3A.PreviewReady &&
                      _blueprintCatalogUx3A.ShowsThreeWorkflows &&
                      _blueprintCatalogUx3A.ShowsStandardAndProject &&
                      _blueprintCatalogUx3A.ShowsInstanceWorkflow &&
                      _blueprintCatalogUx3A.ShowsHumanSurface &&
                      _blueprintCatalogUx3A.EvidenceSourceUntouched;
        _blueprintCatalogUx3A.CloseEvidence();
        var released = _blueprintCatalogUx3A.EvidenceResourcesReleased;
        var sourceRestored = _blueprintCatalogUx3A.SourceSessionRestored;
        var contract = _session.RunUx3AContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _blueprintCatalogUx3A.RestoreSourceSurfaceAfterSmoke();
        else _blueprintCatalogUx3A.HideSourceSurfaceAfterSmoke();
        _session.RestoreBlueprintApprovalUx3A(approval);
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
                              _session.CaptureBlueprintApprovalUx3A() == approval;
        var surfaceRestored = PageId == originalPage && _blueprintCatalogUx3A.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_A_{(valid ? "PASS" : "FAILED")} " +
                 "routes=PAL-001,PAL-002,PAL-003,PAL-006,PAL-007 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"standard_palette={contract.StandardPalette.ToString().ToLowerInvariant()} " +
                 $"project_palette={contract.ProjectPalette.ToString().ToLowerInvariant()} " +
                 $"standard_read_only={contract.StandardReadOnly.ToString().ToLowerInvariant()} " +
                 $"reusable_blueprint={contract.ReusableBlueprint.ToString().ToLowerInvariant()} " +
                 $"instance_resolved={contract.InstanceResolved.ToString().ToLowerInvariant()} " +
                 $"derivation_retargets={contract.DerivationRetargets.ToString().ToLowerInvariant()} " +
                 $"overrides_preserved={contract.OverridesPreserved.ToString().ToLowerInvariant()} " +
                 $"version_pinned={contract.VersionPinned.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
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
