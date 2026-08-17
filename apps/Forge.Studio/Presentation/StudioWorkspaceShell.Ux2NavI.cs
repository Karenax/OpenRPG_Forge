using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAerialNavigationOverlayUx2NavI _aerialNavigationOverlayUx2NavI = null!;
    private Button _aerialNavigationButtonUx2NavI = null!;

    private void BuildUx2NavIActions(HBoxContainer headingRow)
    {
        _aerialNavigationButtonUx2NavI = StudioStyle.Button("Navigation 3D", compact: true);
        _aerialNavigationButtonUx2NavI.TooltipText =
            "Éditer grille volumétrique, obstacles, coûts et routes aériennes avec aperçu sec";
        _aerialNavigationButtonUx2NavI.Pressed += OpenAerialNavigationUx2NavI;
        headingRow.AddChild(_aerialNavigationButtonUx2NavI);
    }

    private void InitializeUx2NavIOverlay()
    {
        _aerialNavigationOverlayUx2NavI = new StudioAerialNavigationOverlayUx2NavI(_session,
            WorldViewport);
        _aerialNavigationOverlayUx2NavI.StatusChanged += SetStatus;
        _aerialNavigationOverlayUx2NavI.NavigationChanged += RefreshAfterUx2NavIMutation;
        AddChild(_aerialNavigationOverlayUx2NavI);
    }

    private void OpenAerialNavigationUx2NavI()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer la navigation 3D.", error: true);
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
        SwitchPage("navigation", notify: false);
        _aerialNavigationOverlayUx2NavI.Open();
        SetStatus("Navigation 3D ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavIMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavIActionState();
    }

    private void UpdateUx2NavIActionState()
    {
        if (_aerialNavigationButtonUx2NavI is null)
        {
            return;
        }
        _aerialNavigationButtonUx2NavI.Visible = PageId == "navigation" &&
                                                 _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavIOverlay()
    {
        if (_aerialNavigationOverlayUx2NavI is not null)
        {
            _aerialNavigationOverlayUx2NavI.Close();
        }
    }

    internal bool PrepareUx2NavIEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        return _aerialNavigationOverlayUx2NavI.PrepareEvidence(mode);
    }

    internal bool RunUx2NavISmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _aerialNavigationOverlayUx2NavI.IsOpen;
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
        var originalApproval = _session.CaptureAerialApprovalUx2NavI();

        var uiTransaction = _aerialNavigationOverlayUx2NavI.RunUiTransactionEvidence();
        var uiTransactionReleased = _aerialNavigationOverlayUx2NavI.EvidenceResourcesReleased;
        var uiTransactionRestored = _aerialNavigationOverlayUx2NavI.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavIEvidence("ux2-nav-i");
        var overlayOpen = _aerialNavigationOverlayUx2NavI.IsOpen;
        var explicitSelection = _aerialNavigationOverlayUx2NavI.HasExplicitSelection;
        var canonicalPreview = _aerialNavigationOverlayUx2NavI.CanonicalPreviewReady;
        var viewportOverlay = _aerialNavigationOverlayUx2NavI.ShowsViewportOverlay;
        var structuredDiagnostics = _aerialNavigationOverlayUx2NavI.ShowsStructuredDiagnostics;
        var humanSurface = _aerialNavigationOverlayUx2NavI.ShowsHumanSurface;
        var isolatedEvidence = _aerialNavigationOverlayUx2NavI.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _aerialNavigationOverlayUx2NavI.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _aerialNavigationOverlayUx2NavI.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _aerialNavigationOverlayUx2NavI.Close();
        var evidenceReleased = _aerialNavigationOverlayUx2NavI.EvidenceResourcesReleased;
        var sourceSessionRestored = _aerialNavigationOverlayUx2NavI.SourceSessionRestored;

        var result = _session.RunUx2NavIContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _aerialNavigationOverlayUx2NavI.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _aerialNavigationOverlayUx2NavI.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureAerialApprovalUx2NavI() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_I_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _aerialNavigationOverlayUx2NavI.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_I_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"volumetric_authoring={result.VolumetricAuthoring.ToString().ToLowerInvariant()} " +
            $"flight_route={result.FlightRouteAuthoring.ToString().ToLowerInvariant()} " +
            $"stable_order={result.StableOrder.ToString().ToLowerInvariant()} " +
            $"validation={result.Validation.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"world_neighbors={result.WorldNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"route_neighbors={result.RouteNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
