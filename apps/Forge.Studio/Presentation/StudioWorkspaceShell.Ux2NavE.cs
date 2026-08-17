using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioWaypointLocatorOverlayUx2NavE _waypointLocatorOverlayUx2NavE = null!;
    private Button _waypointLocatorButtonUx2NavE = null!;

    private void BuildUx2NavEActions(HBoxContainer headingRow)
    {
        _waypointLocatorButtonUx2NavE = StudioStyle.Button("Ancres & diagnostics", compact: true);
        _waypointLocatorButtonUx2NavE.TooltipText =
            "Éditer les ancres audio/VFX, groupes déterministes et couches de route.";
        _waypointLocatorButtonUx2NavE.Pressed += OpenWaypointLocatorsUx2NavE;
        headingRow.AddChild(_waypointLocatorButtonUx2NavE);
    }

    private void InitializeUx2NavEOverlay()
    {
        _waypointLocatorOverlayUx2NavE = new StudioWaypointLocatorOverlayUx2NavE(_session,
            WorldViewport);
        _waypointLocatorOverlayUx2NavE.StatusChanged += SetStatus;
        _waypointLocatorOverlayUx2NavE.NavigationChanged += RefreshAfterUx2NavEMutation;
        AddChild(_waypointLocatorOverlayUx2NavE);
    }

    private void OpenWaypointLocatorsUx2NavE()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer ses ancres.", error: true);
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
        CloseUx2NavDOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _waypointLocatorOverlayUx2NavE.Open();
        SetStatus("Ancres ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavEMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavEActionState();
    }

    private void UpdateUx2NavEActionState()
    {
        if (_waypointLocatorButtonUx2NavE is null)
        {
            return;
        }
        _waypointLocatorButtonUx2NavE.Visible = PageId == "navigation" &&
                                                _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavEOverlay()
    {
        if (_waypointLocatorOverlayUx2NavE is not null)
        {
            _waypointLocatorOverlayUx2NavE.Close();
        }
    }

    internal bool PrepareUx2NavEEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2EnvJOverlay();
        return _waypointLocatorOverlayUx2NavE.PrepareEvidence(mode);
    }

    internal bool RunUx2NavESmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _waypointLocatorOverlayUx2NavE.IsOpen;
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
        var originalLocatorApproval = _session.CaptureLocatorApprovalUx2NavE();

        var uiTransaction = _waypointLocatorOverlayUx2NavE.RunUiTransactionEvidence();
        var uiTransactionReleased = _waypointLocatorOverlayUx2NavE.EvidenceResourcesReleased;
        var uiTransactionRestored = _waypointLocatorOverlayUx2NavE.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavEEvidence("ux2-nav-e");
        var overlayOpen = _waypointLocatorOverlayUx2NavE.IsOpen;
        var explicitSelection = _waypointLocatorOverlayUx2NavE.HasExplicitSelection;
        var canonicalPreview = _waypointLocatorOverlayUx2NavE.CanonicalPreviewReady;
        var viewportOverlay = _waypointLocatorOverlayUx2NavE.ShowsViewportOverlay;
        var structuredDiagnostics = _waypointLocatorOverlayUx2NavE.ShowsStructuredDiagnostics;
        var humanSurface = _waypointLocatorOverlayUx2NavE.ShowsHumanSurface;
        var isolatedEvidence = _waypointLocatorOverlayUx2NavE.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _waypointLocatorOverlayUx2NavE.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _waypointLocatorOverlayUx2NavE.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _waypointLocatorOverlayUx2NavE.Close();
        var evidenceReleased = _waypointLocatorOverlayUx2NavE.EvidenceResourcesReleased;
        var sourceSessionRestored = _waypointLocatorOverlayUx2NavE.SourceSessionRestored;

        var result = _session.RunUx2NavEContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _waypointLocatorOverlayUx2NavE.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _waypointLocatorOverlayUx2NavE.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureNavigationApprovalUx2NavB() == originalNavigationApproval &&
                              _session.CaptureWaypointApprovalUx2NavC() == originalWaypointApproval &&
                              _session.CapturePatrolApprovalUx2NavD() == originalPatrolApproval &&
                              _session.CaptureLocatorApprovalUx2NavE() == originalLocatorApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_E_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _waypointLocatorOverlayUx2NavE.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_E_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"locator_creation={result.LocatorCreation.ToString().ToLowerInvariant()} " +
            $"locator_validation={result.LocatorValidation.ToString().ToLowerInvariant()} " +
            $"blueprint_overrides={result.BlueprintAndOverrides.ToString().ToLowerInvariant()} " +
            $"arrival_preserved={result.ArrivalPreserved.ToString().ToLowerInvariant()} " +
            $"collection_creation={result.CollectionCreation.ToString().ToLowerInvariant()} " +
            $"stable_member_order={result.StableMemberOrder.ToString().ToLowerInvariant()} " +
            $"collection_modes={result.CollectionModes.ToString().ToLowerInvariant()} " +
            $"collection_validation={result.CollectionValidation.ToString().ToLowerInvariant()} " +
            $"route_visualization={result.RouteVisualization.ToString().ToLowerInvariant()} " +
            $"real_projection={result.RealProjection.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"waypoint_neighbors={result.WaypointNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"locator_neighbors={result.LocatorNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"collection_neighbors={result.CollectionNeighborsPreserved.ToString().ToLowerInvariant()} " +
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
