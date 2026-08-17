using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaTransitionsOverlayUx2NavF _areaTransitionsOverlayUx2NavF = null!;
    private Button _areaTransitionsButtonUx2NavF = null!;

    private void BuildUx2NavFActions(HBoxContainer headingRow)
    {
        _areaTransitionsButtonUx2NavF = StudioStyle.Button("Transitions de zone", compact: true);
        _areaTransitionsButtonUx2NavF.TooltipText =
            "Éditer départs, destinations, retour, orientation, vitesse et hooks par aperçu sec.";
        _areaTransitionsButtonUx2NavF.Pressed += OpenAreaTransitionsUx2NavF;
        headingRow.AddChild(_areaTransitionsButtonUx2NavF);
    }

    private void InitializeUx2NavFOverlay()
    {
        _areaTransitionsOverlayUx2NavF = new StudioAreaTransitionsOverlayUx2NavF(_session);
        _areaTransitionsOverlayUx2NavF.StatusChanged += SetStatus;
        _areaTransitionsOverlayUx2NavF.TransitionChanged += RefreshAfterUx2NavFMutation;
        AddChild(_areaTransitionsOverlayUx2NavF);
    }

    private void OpenAreaTransitionsUx2NavF()
    {
        if (_session.Project?.Areas.Count is not > 1)
        {
            SetStatus("Créez au moins deux zones avant d’éditer une transition.", error: true);
            return;
        }
        _globalSearchOverlay.Close(); _safeRenameOverlay.Close(); _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close(); _viewportToolsOverlay.Close(); _areaCreationOverlay.Close();
        _areaLoadingOverlay.Close(); _areaLayersOverlay.Close(); _areaDocumentOperationsOverlay.Close();
        _placeableBlueprintOverlay.Close(); CloseUx1WOverlay(); CloseUx1XOverlay(); CloseUx1YOverlay();
        CloseUx1ZOverlay(); CloseUx1AAOverlay(); CloseUx1ABOverlay(); CloseUx1ACOverlay(); CloseUx1ADOverlay();
        CloseUx1AEOverlay(); CloseUx1AFWorkspace(); CloseUx1AGOverlay(); CloseUx1AHOverlay(); CloseUx1AIOverlay();
        CloseUx1AJOverlay(); CloseUx2AOverlay(); CloseUx4AOverlay(); CloseUx2NavBOverlay();
        CloseUx2NavCOverlay(); CloseUx2NavDOverlay(); CloseUx2NavEOverlay(); CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _areaTransitionsOverlayUx2NavF.Open();
        SetStatus("Transitions ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavFMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx2NavFActionState();
    }

    private void UpdateUx2NavFActionState()
    {
        if (_areaTransitionsButtonUx2NavF is null) return;
        _areaTransitionsButtonUx2NavF.Visible = PageId == "navigation" && _session.Project?.Areas.Count > 1;
    }

    private void CloseUx2NavFOverlay()
    {
        if (_areaTransitionsOverlayUx2NavF is not null) _areaTransitionsOverlayUx2NavF.Close();
    }

    internal bool PrepareUx2NavFEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay(); CloseUx2NavCOverlay(); CloseUx2NavDOverlay(); CloseUx2NavEOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        return _areaTransitionsOverlayUx2NavF.PrepareEvidence(mode);
    }

    internal bool RunUx2NavFSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _areaTransitionsOverlayUx2NavF.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalUndo = originalHistory?.CanUndo;
        var originalRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalNav = _session.CaptureNavigationApprovalUx2NavB();
        var originalWaypoint = _session.CaptureWaypointApprovalUx2NavC();
        var originalPatrol = _session.CapturePatrolApprovalUx2NavD();
        var originalPortal = _session.CapturePortalApprovalUx2NavF();

        var uiTransaction = _areaTransitionsOverlayUx2NavF.RunUiTransactionEvidence();
        var evidence = PrepareUx2NavFEvidence("ux2-nav-f");
        var uiReady = evidence && PageId == "navigation" && _areaTransitionsOverlayUx2NavF.IsOpen &&
                      _areaTransitionsOverlayUx2NavF.ChoiceCount >= 2 &&
                      _areaTransitionsOverlayUx2NavF.HasExplicitSelection &&
                      _areaTransitionsOverlayUx2NavF.CanonicalPreviewReady &&
                      _areaTransitionsOverlayUx2NavF.ShowsRuntimeResolution &&
                      _areaTransitionsOverlayUx2NavF.ShowsStructuredDiagnostics &&
                      _areaTransitionsOverlayUx2NavF.ShowsHumanSurface &&
                      _areaTransitionsOverlayUx2NavF.UsesIsolatedEvidence &&
                      _areaTransitionsOverlayUx2NavF.EvidenceSourceUntouched;
        var isolatedEvidence = _areaTransitionsOverlayUx2NavF.UsesIsolatedEvidence;
        _areaTransitionsOverlayUx2NavF.Close();
        var released = _areaTransitionsOverlayUx2NavF.EvidenceResourcesReleased;
        var restored = _areaTransitionsOverlayUx2NavF.SourceSessionRestored;
        var result = _session.RunUx2NavFContractSmoke();
        SwitchPage(originalPage, notify: false);
        if (originalOpen) _areaTransitionsOverlayUx2NavF.RestoreSourceSurfaceAfterSmoke();
        _session.RestorePortalApprovalUx2NavF(originalPortal);
        var surfaceRestored = PageId == originalPage && _areaTransitionsOverlayUx2NavF.IsOpen == originalOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, _session.History) &&
                              _session.History?.State.Revision == originalRevision &&
                              (_session.History?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.History?.CanUndo == originalUndo &&
                              _session.History?.CanRedo == originalRedo && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNavigationApprovalUx2NavB() == originalNav &&
                              _session.CaptureWaypointApprovalUx2NavC() == originalWaypoint &&
                              _session.CapturePatrolApprovalUx2NavD() == originalPatrol &&
                              _session.CapturePortalApprovalUx2NavF() == originalPortal;
        var valid = result.Succeeded && uiReady && uiTransaction && released && restored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_NAV_F_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
                 $"create_update_remove={result.CreateUpdateRemove.ToString().ToLowerInvariant()} " +
                 $"invalid_references={result.InvalidReferencesBlocked.ToString().ToLowerInvariant()} " +
                 $"hook_validation={result.HookValidation.ToString().ToLowerInvariant()} " +
                 $"runtime_resolution={result.RuntimeResolution.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
                 $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
                 $"cleanup={result.Cleanup.ToString().ToLowerInvariant()} ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"evidence_released={released.ToString().ToLowerInvariant()} source_restored={restored.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
