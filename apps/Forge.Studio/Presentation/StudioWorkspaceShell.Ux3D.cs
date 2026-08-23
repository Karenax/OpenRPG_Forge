using Forge.ModuleFormat.Serialization;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioBlueprintGovernanceWorkbenchUx3D _blueprintGovernanceUx3D = null!;
    private Button _blueprintGovernanceButtonUx3D = null!;

    private void BuildUx3DActions(HBoxContainer headingRow)
    {
        _blueprintGovernanceButtonUx3D = StudioStyle.Button("Dépendances & licences", compact: true);
        _blueprintGovernanceButtonUx3D.TooltipText =
            "Fermer automatiquement le manifeste et gouverner source, SPDX, permission et SHA-256.";
        _blueprintGovernanceButtonUx3D.Pressed += OpenBlueprintGovernanceUx3D;
        headingRow.AddChild(_blueprintGovernanceButtonUx3D);
    }

    private void InitializeUx3DOverlay()
    {
        _blueprintGovernanceUx3D = new StudioBlueprintGovernanceWorkbenchUx3D(_session);
        _blueprintGovernanceUx3D.StatusChanged += SetStatus;
        _blueprintGovernanceUx3D.ProjectChanged += RefreshAfterUx3DMutation;
        AddChild(_blueprintGovernanceUx3D);
    }

    private void OpenBlueprintGovernanceUx3D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de gouverner ses blueprints.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("blueprints", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintGovernanceUx3D.Open();
        SetStatus("UX3-D · dépendances automatiques, provenance et licence distribuable.");
    }

    private void RefreshAfterUx3DMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx3DActionState();
    }

    private void UpdateUx3DActionState()
    {
        if (_blueprintGovernanceButtonUx3D is not null)
            _blueprintGovernanceButtonUx3D.Visible = PageId is "blueprints" or "assets" &&
                                                        _session.Project is not null;
    }

    private void CloseUx3DOverlay()
    {
        if (_blueprintGovernanceUx3D is not null) _blueprintGovernanceUx3D.Close();
    }

    internal bool PrepareUx3DEvidence(string mode)
    {
        SwitchPage(mode.EndsWith("-dependencies", StringComparison.OrdinalIgnoreCase)
            ? "blueprints" : "assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _blueprintGovernanceUx3D.Open();
        return _blueprintGovernanceUx3D.PrepareEvidence(mode);
    }

    internal void ReleaseUx3DAutomatedEvidence() => _blueprintGovernanceUx3D.CloseEvidence();

    internal bool RunUx3DSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _blueprintGovernanceUx3D.IsOpen;
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
        var approval = _session.CaptureBlueprintGovernanceApprovalUx3D();

        var uiTransaction = _blueprintGovernanceUx3D.RunUiTransactionEvidence();
        var prepared = PrepareUx3DEvidence("ux3-d-blueprint-governance");
        var uiReady = prepared && _blueprintGovernanceUx3D.IsOpen &&
                      _blueprintGovernanceUx3D.UsesIsolatedEvidence &&
                      (_blueprintGovernanceUx3D.UsesRealModuleEvidence ^
                       _blueprintGovernanceUx3D.UsesCanonicalFallbackEvidence) &&
                      _blueprintGovernanceUx3D.HasExplicitSelection &&
                      _blueprintGovernanceUx3D.DependencyPreviewReady &&
                      _blueprintGovernanceUx3D.ProvenancePreviewReady &&
                      _blueprintGovernanceUx3D.ShowsHumanSurface &&
                      _blueprintGovernanceUx3D.EvidenceSourceUntouched;
        _blueprintGovernanceUx3D.CloseEvidence();
        var released = _blueprintGovernanceUx3D.EvidenceResourcesReleased;
        var sourceRestored = _blueprintGovernanceUx3D.SourceSessionRestored;
        var contract = _session.RunUx3DContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _blueprintGovernanceUx3D.RestoreSourceSurfaceAfterSmoke();
        else _blueprintGovernanceUx3D.HideSourceSurfaceAfterSmoke();
        _session.RestoreBlueprintGovernanceApprovalUx3D(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile) == originalWorkspace &&
                              _session.CaptureBlueprintGovernanceApprovalUx3D() == approval;
        var surfaceRestored = PageId == originalPage && _blueprintGovernanceUx3D.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_D_{(valid ? "PASS" : "FAILED")} " +
                 "routes=PAL-015,PAL-016 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"missing_dependency={contract.MissingDependencyBlocked.ToString().ToLowerInvariant()} " +
                 $"transitive_closure={contract.TransitiveClosure.ToString().ToLowerInvariant()} " +
                 $"build_graph={contract.BuildGraphResolved.ToString().ToLowerInvariant()} " +
                 $"invalid_provenance={contract.InvalidProvenanceBlocked.ToString().ToLowerInvariant()} " +
                 $"distribution_ready={contract.DistributionReady.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"separate_approvals={contract.SeparateApprovals.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
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
