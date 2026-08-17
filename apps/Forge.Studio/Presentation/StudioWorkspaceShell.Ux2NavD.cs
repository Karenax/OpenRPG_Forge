using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioWaypointPatrolOverlayUx2NavD _waypointPatrolOverlayUx2NavD = null!;
    private Button _waypointPatrolButtonUx2NavD = null!;

    private void BuildUx2NavDActions(HBoxContainer headingRow)
    {
        _waypointPatrolButtonUx2NavD = StudioStyle.Button("Patrouilles & arrivées", compact: true);
        _waypointPatrolButtonUx2NavD.TooltipText =
            "Éditer routes ordonnées, parcours et comportements d’arrivée avec aperçu sec.";
        _waypointPatrolButtonUx2NavD.Pressed += OpenWaypointPatrolUx2NavD;
        headingRow.AddChild(_waypointPatrolButtonUx2NavD);
    }

    private void InitializeUx2NavDOverlay()
    {
        _waypointPatrolOverlayUx2NavD = new StudioWaypointPatrolOverlayUx2NavD(_session,
            WorldViewport);
        _waypointPatrolOverlayUx2NavD.StatusChanged += SetStatus;
        _waypointPatrolOverlayUx2NavD.NavigationChanged += RefreshAfterUx2NavDMutation;
        AddChild(_waypointPatrolOverlayUx2NavD);
    }

    private void OpenWaypointPatrolUx2NavD()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer ses patrouilles.", error: true);
            return;
        }
        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaCreationOverlay.Close();
        _areaLoadingOverlay.Close();
        _areaLayersOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _placeableBlueprintOverlay.Close();
        CloseUx1WOverlay();
        CloseUx1XOverlay();
        CloseUx1YOverlay();
        CloseUx1ZOverlay();
        CloseUx1AAOverlay();
        CloseUx1ABOverlay();
        CloseUx1ACOverlay();
        CloseUx1ADOverlay();
        CloseUx1AEOverlay();
        CloseUx1AFWorkspace();
        CloseUx1AGOverlay();
        CloseUx1AHOverlay();
        CloseUx1AIOverlay();
        CloseUx1AJOverlay();
        CloseUx2AOverlay();
        CloseUx4AOverlay();
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _waypointPatrolOverlayUx2NavD.Open();
        SetStatus("Patrouilles ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavDMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavDActionState();
    }

    private void UpdateUx2NavDActionState()
    {
        if (_waypointPatrolButtonUx2NavD is null)
        {
            return;
        }
        _waypointPatrolButtonUx2NavD.Visible = PageId == "navigation" &&
                                               _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavDOverlay()
    {
        if (_waypointPatrolOverlayUx2NavD is not null)
        {
            _waypointPatrolOverlayUx2NavD.Close();
        }
    }

    internal bool PrepareUx2NavDEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2EnvJOverlay();
        return _waypointPatrolOverlayUx2NavD.PrepareEvidence(mode);
    }

    internal bool RunUx2NavDSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _waypointPatrolOverlayUx2NavD.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalDirty = _session.IsDirty;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalNavigationApproval = _session.CaptureNavigationApprovalUx2NavB();
        var originalWaypointApproval = _session.CaptureWaypointApprovalUx2NavC();
        var originalPatrolApproval = _session.CapturePatrolApprovalUx2NavD();

        var uiTransaction = _waypointPatrolOverlayUx2NavD.RunUiTransactionEvidence();
        var uiTransactionReleased = _waypointPatrolOverlayUx2NavD.EvidenceResourcesReleased;
        var uiTransactionRestored = _waypointPatrolOverlayUx2NavD.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavDEvidence("ux2-nav-d");
        var overlayOpen = _waypointPatrolOverlayUx2NavD.IsOpen;
        var explicitSelection = _waypointPatrolOverlayUx2NavD.HasExplicitSelection;
        var canonicalPreview = _waypointPatrolOverlayUx2NavD.CanonicalPreviewReady;
        var viewportOverlay = _waypointPatrolOverlayUx2NavD.ShowsViewportOverlay;
        var structuredDiagnostics = _waypointPatrolOverlayUx2NavD.ShowsStructuredDiagnostics;
        var humanSurface = _waypointPatrolOverlayUx2NavD.ShowsHumanSurface;
        var isolatedEvidence = _waypointPatrolOverlayUx2NavD.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _waypointPatrolOverlayUx2NavD.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _waypointPatrolOverlayUx2NavD.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _waypointPatrolOverlayUx2NavD.Close();
        var evidenceReleased = _waypointPatrolOverlayUx2NavD.EvidenceResourcesReleased;
        var sourceSessionRestored = _waypointPatrolOverlayUx2NavD.SourceSessionRestored;

        var result = _session.RunUx2NavDContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _waypointPatrolOverlayUx2NavD.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _waypointPatrolOverlayUx2NavD.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNavigationApprovalUx2NavB() ==
                              originalNavigationApproval &&
                              _session.CaptureWaypointApprovalUx2NavC() == originalWaypointApproval &&
                              _session.CapturePatrolApprovalUx2NavD() == originalPatrolApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_D_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _waypointPatrolOverlayUx2NavD.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_D_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"route_creation={result.RouteCreation.ToString().ToLowerInvariant()} " +
            $"stable_point_order={result.StablePointOrder.ToString().ToLowerInvariant()} " +
            $"route_validation={result.RouteValidation.ToString().ToLowerInvariant()} " +
            $"arrival_validation={result.ArrivalValidation.ToString().ToLowerInvariant()} " +
            $"traversal_preview={result.TraversalPreview.ToString().ToLowerInvariant()} " +
            $"arrival_authority={result.ArrivalAuthority.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"route_neighbors={result.RouteNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"waypoint_neighbors={result.WaypointNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"waypoint_tails={result.WaypointTailsPreserved.ToString().ToLowerInvariant()} " +
            $"navigation_neighbors={result.NavigationNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
