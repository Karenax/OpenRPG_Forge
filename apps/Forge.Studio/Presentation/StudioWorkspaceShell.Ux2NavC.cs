using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioWaypointMarkerOverlayUx2NavC _waypointMarkerOverlayUx2NavC = null!;
    private Button _waypointMarkerButtonUx2NavC = null!;

    private void BuildUx2NavCActions(HBoxContainer headingRow)
    {
        _waypointMarkerButtonUx2NavC = StudioStyle.Button("Repères & carte", compact: true);
        _waypointMarkerButtonUx2NavC.TooltipText =
            "Éditer l’apparence, la note cartographique et sa révélation avec aperçu sec.";
        _waypointMarkerButtonUx2NavC.Pressed += OpenWaypointMarkerUx2NavC;
        headingRow.AddChild(_waypointMarkerButtonUx2NavC);
    }

    private void InitializeUx2NavCOverlay()
    {
        _waypointMarkerOverlayUx2NavC = new StudioWaypointMarkerOverlayUx2NavC(_session, WorldViewport);
        _waypointMarkerOverlayUx2NavC.StatusChanged += SetStatus;
        _waypointMarkerOverlayUx2NavC.NavigationChanged += RefreshAfterUx2NavCMutation;
        AddChild(_waypointMarkerOverlayUx2NavC);
    }

    private void OpenWaypointMarkerUx2NavC()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer ses repères.", error: true);
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
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        SwitchPage("navigation", notify: false);
        _waypointMarkerOverlayUx2NavC.Open();
        SetStatus("Repères ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavCMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavCActionState();
    }

    private void UpdateUx2NavCActionState()
    {
        if (_waypointMarkerButtonUx2NavC is null)
        {
            return;
        }
        _waypointMarkerButtonUx2NavC.Visible = PageId == "navigation" &&
                                               _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavCOverlay()
    {
        if (_waypointMarkerOverlayUx2NavC is not null)
        {
            _waypointMarkerOverlayUx2NavC.Close();
        }
    }

    internal bool PrepareUx2NavCEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        return _waypointMarkerOverlayUx2NavC.PrepareEvidence(mode);
    }

    internal bool RunUx2NavCSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _waypointMarkerOverlayUx2NavC.IsOpen;
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
        var originalApproval = _session.CaptureWaypointApprovalUx2NavC();

        var uiTransaction = _waypointMarkerOverlayUx2NavC.RunUiTransactionEvidence();
        var uiTransactionReleased = _waypointMarkerOverlayUx2NavC.EvidenceResourcesReleased;
        var uiTransactionRestored = _waypointMarkerOverlayUx2NavC.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavCEvidence("ux2-nav-c");
        var overlayOpen = _waypointMarkerOverlayUx2NavC.IsOpen;
        var explicitSelection = _waypointMarkerOverlayUx2NavC.HasExplicitSelection;
        var canonicalPreview = _waypointMarkerOverlayUx2NavC.CanonicalPreviewReady;
        var viewportOverlay = _waypointMarkerOverlayUx2NavC.ShowsViewportOverlay;
        var structuredDiagnostics = _waypointMarkerOverlayUx2NavC.ShowsStructuredDiagnostics;
        var humanSurface = _waypointMarkerOverlayUx2NavC.ShowsHumanSurface;
        var isolatedEvidence = _waypointMarkerOverlayUx2NavC.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _waypointMarkerOverlayUx2NavC.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _waypointMarkerOverlayUx2NavC.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _waypointMarkerOverlayUx2NavC.Close();
        var evidenceReleased = _waypointMarkerOverlayUx2NavC.EvidenceResourcesReleased;
        var sourceSessionRestored = _waypointMarkerOverlayUx2NavC.SourceSessionRestored;

        var result = _session.RunUx2NavCContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _waypointMarkerOverlayUx2NavC.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _waypointMarkerOverlayUx2NavC.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureWaypointApprovalUx2NavC() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_C_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _waypointMarkerOverlayUx2NavC.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_C_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"create={result.CreateSupported.ToString().ToLowerInvariant()} " +
            $"appearance_validation={result.AppearanceValidation.ToString().ToLowerInvariant()} " +
            $"marker_validation={result.MarkerValidation.ToString().ToLowerInvariant()} " +
            $"reveal_policy={result.RevealPolicy.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"hidden_fields={result.HiddenFieldsPreserved.ToString().ToLowerInvariant()} " +
            $"waypoint_neighbors={result.WaypointNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"navigation_neighbors={result.NavigationNeighborsPreserved.ToString().ToLowerInvariant()} " +
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
