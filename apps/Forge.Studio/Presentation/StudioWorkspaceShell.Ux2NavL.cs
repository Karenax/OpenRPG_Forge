using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioVehicleRouteOverlayUx2NavL _vehicleRouteOverlayUx2NavL = null!;
    private Button _vehicleRouteButtonUx2NavL = null!;

    private void BuildUx2NavLActions(HBoxContainer headingRow)
    {
        _vehicleRouteButtonUx2NavL = StudioStyle.Button("Routes de véhicules", compact: true);
        _vehicleRouteButtonUx2NavL.TooltipText =
            "Éditer points, voies, arrêts, vitesse, dépassement et synchronisation avec aperçu sec";
        _vehicleRouteButtonUx2NavL.Pressed += OpenVehicleRouteUx2NavL;
        headingRow.AddChild(_vehicleRouteButtonUx2NavL);
    }

    private void InitializeUx2NavLOverlay()
    {
        _vehicleRouteOverlayUx2NavL = new StudioVehicleRouteOverlayUx2NavL(_session, WorldViewport);
        _vehicleRouteOverlayUx2NavL.StatusChanged += SetStatus;
        _vehicleRouteOverlayUx2NavL.VehicleRouteChanged += RefreshAfterUx2NavLMutation;
        AddChild(_vehicleRouteOverlayUx2NavL);
    }

    private void OpenVehicleRouteUx2NavL()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez une zone avant d’éditer une route de véhicules.", error: true);
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
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2NavMOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _vehicleRouteOverlayUx2NavL.Open();
        SetStatus("Routes de véhicules ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavLMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavLActionState();
    }

    private void UpdateUx2NavLActionState()
    {
        if (_vehicleRouteButtonUx2NavL is null) return;
        _vehicleRouteButtonUx2NavL.Visible = PageId == "navigation" &&
                                             _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavLOverlay()
    {
        if (_vehicleRouteOverlayUx2NavL is not null) _vehicleRouteOverlayUx2NavL.Close();
    }

    internal bool PrepareUx2NavLEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2NavMOverlay();
        CloseUx2EnvJOverlay();
        return _vehicleRouteOverlayUx2NavL.PrepareEvidence(mode);
    }

    internal bool RunUx2NavLSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _vehicleRouteOverlayUx2NavL.IsOpen;
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
        var originalApproval = _session.CaptureVehicleRouteApprovalUx2NavL();

        var uiTransaction = _vehicleRouteOverlayUx2NavL.RunUiTransactionEvidence();
        var uiTransactionReleased = _vehicleRouteOverlayUx2NavL.EvidenceResourcesReleased;
        var uiTransactionRestored = _vehicleRouteOverlayUx2NavL.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavLEvidence("ux2-nav-l");
        var overlayOpen = _vehicleRouteOverlayUx2NavL.IsOpen;
        var explicitSelection = _vehicleRouteOverlayUx2NavL.HasExplicitSelection;
        var canonicalPreview = _vehicleRouteOverlayUx2NavL.CanonicalPreviewReady;
        var viewportOverlay = _vehicleRouteOverlayUx2NavL.ShowsViewportOverlay;
        var structuredDiagnostics = _vehicleRouteOverlayUx2NavL.ShowsStructuredDiagnostics;
        var boundedRuntime = _vehicleRouteOverlayUx2NavL.ShowsBoundedRuntimeClaim;
        var humanSurface = _vehicleRouteOverlayUx2NavL.ShowsHumanSurface;
        var isolatedEvidence = _vehicleRouteOverlayUx2NavL.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _vehicleRouteOverlayUx2NavL.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _vehicleRouteOverlayUx2NavL.ChoiceCount >= 1 && explicitSelection && canonicalPreview &&
                      viewportOverlay && structuredDiagnostics && boundedRuntime && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _vehicleRouteOverlayUx2NavL.Close();
        var evidenceReleased = _vehicleRouteOverlayUx2NavL.EvidenceResourcesReleased;
        var sourceSessionRestored = _vehicleRouteOverlayUx2NavL.SourceSessionRestored;

        var result = _session.RunUx2NavLContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _vehicleRouteOverlayUx2NavL.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _vehicleRouteOverlayUx2NavL.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureVehicleRouteApprovalUx2NavL() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_L_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"bounded={boundedRuntime.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _vehicleRouteOverlayUx2NavL.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_L_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"full_surface={result.FullSurface.ToString().ToLowerInvariant()} " +
            $"create_edit_remove={result.CreateEditRemove.ToString().ToLowerInvariant()} " +
            $"ordered_collections={result.OrderedCollections.ToString().ToLowerInvariant()} " +
            $"validation={result.Validation.ToString().ToLowerInvariant()} " +
            $"no_silent_clamp={result.NoSilentClamp.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"runtime_sample={result.RuntimeSample.ToString().ToLowerInvariant()} " +
            $"runtime_boundary={result.RuntimeBoundary.ToString().ToLowerInvariant()} " +
            $"world_neighbors={result.WorldFeatureNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"vehicle_neighbor={result.VehicleNeighborPreserved.ToString().ToLowerInvariant()} " +
            $"nav_b_k={result.NavigationLotsPreserved.ToString().ToLowerInvariant()} " +
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
