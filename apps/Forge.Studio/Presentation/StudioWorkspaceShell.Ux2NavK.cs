using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCrossAreaPatrolOverlayUx2NavK _crossAreaPatrolOverlayUx2NavK = null!;
    private Button _crossAreaPatrolButtonUx2NavK = null!;

    private void BuildUx2NavKActions(HBoxContainer headingRow)
    {
        _crossAreaPatrolButtonUx2NavK = StudioStyle.Button("Patrouille inter-zones", compact: true);
        _crossAreaPatrolButtonUx2NavK.TooltipText =
            "Éditer passages, reprises, préchargement et autorité avec aperçu sec";
        _crossAreaPatrolButtonUx2NavK.Pressed += OpenCrossAreaPatrolUx2NavK;
        headingRow.AddChild(_crossAreaPatrolButtonUx2NavK);
    }

    private void InitializeUx2NavKOverlay()
    {
        _crossAreaPatrolOverlayUx2NavK = new StudioCrossAreaPatrolOverlayUx2NavK(_session,
            WorldViewport);
        _crossAreaPatrolOverlayUx2NavK.StatusChanged += SetStatus;
        _crossAreaPatrolOverlayUx2NavK.CrossAreaPatrolChanged += RefreshAfterUx2NavKMutation;
        AddChild(_crossAreaPatrolOverlayUx2NavK);
    }

    private void OpenCrossAreaPatrolUx2NavK()
    {
        if (_session.Project?.Areas.Count is not > 1)
        {
            SetStatus("Créez au moins deux zones avant d’éditer une patrouille inter-zones.", error: true);
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
        CloseUx2NavLOverlay();
        CloseUx2NavMOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _crossAreaPatrolOverlayUx2NavK.Open();
        SetStatus("Patrouille inter-zones ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavKMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavKActionState();
    }

    private void UpdateUx2NavKActionState()
    {
        if (_crossAreaPatrolButtonUx2NavK is null) return;
        _crossAreaPatrolButtonUx2NavK.Visible = PageId == "navigation" &&
                                                _session.Project?.Areas.Count > 1;
    }

    private void CloseUx2NavKOverlay()
    {
        if (_crossAreaPatrolOverlayUx2NavK is not null) _crossAreaPatrolOverlayUx2NavK.Close();
    }

    internal bool PrepareUx2NavKEvidence(string mode)
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
        CloseUx2NavLOverlay();
        CloseUx2NavMOverlay();
        CloseUx2EnvJOverlay();
        return _crossAreaPatrolOverlayUx2NavK.PrepareEvidence(mode);
    }

    internal bool RunUx2NavKSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _crossAreaPatrolOverlayUx2NavK.IsOpen;
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
        var originalApproval = _session.CaptureCrossAreaPatrolApprovalUx2NavK();

        var uiTransaction = _crossAreaPatrolOverlayUx2NavK.RunUiTransactionEvidence();
        var uiTransactionReleased = _crossAreaPatrolOverlayUx2NavK.EvidenceResourcesReleased;
        var uiTransactionRestored = _crossAreaPatrolOverlayUx2NavK.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavKEvidence("ux2-nav-k");
        var overlayOpen = _crossAreaPatrolOverlayUx2NavK.IsOpen;
        var explicitSelection = _crossAreaPatrolOverlayUx2NavK.HasExplicitSelection;
        var canonicalPreview = _crossAreaPatrolOverlayUx2NavK.CanonicalPreviewReady;
        var viewportOverlay = _crossAreaPatrolOverlayUx2NavK.ShowsViewportOverlay;
        var structuredDiagnostics = _crossAreaPatrolOverlayUx2NavK.ShowsStructuredDiagnostics;
        var boundedRuntime = _crossAreaPatrolOverlayUx2NavK.ShowsBoundedRuntimeClaim;
        var humanSurface = _crossAreaPatrolOverlayUx2NavK.ShowsHumanSurface;
        var isolatedEvidence = _crossAreaPatrolOverlayUx2NavK.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _crossAreaPatrolOverlayUx2NavK.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _crossAreaPatrolOverlayUx2NavK.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && boundedRuntime &&
                      humanSurface && isolatedEvidence && evidenceSourceUntouched;
        _crossAreaPatrolOverlayUx2NavK.Close();
        var evidenceReleased = _crossAreaPatrolOverlayUx2NavK.EvidenceResourcesReleased;
        var sourceSessionRestored = _crossAreaPatrolOverlayUx2NavK.SourceSessionRestored;

        var result = _session.RunUx2NavKContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _crossAreaPatrolOverlayUx2NavK.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _crossAreaPatrolOverlayUx2NavK.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureCrossAreaPatrolApprovalUx2NavK() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_K_UI_SURFACE_FAILED " +
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
                    _crossAreaPatrolOverlayUx2NavK.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_K_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"create_edit_remove={result.CreateEditRemove.ToString().ToLowerInvariant()} " +
            $"segment_order={result.SegmentOrder.ToString().ToLowerInvariant()} " +
            $"portal_waypoint={result.PortalWaypointResolution.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"runtime_boundary={result.RuntimeBoundary.ToString().ToLowerInvariant()} " +
            $"validation={result.Validation.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"experience_tails={result.ExperienceTailsPreserved.ToString().ToLowerInvariant()} " +
            $"neighbor_routes={result.NeighborRoutesPreserved.ToString().ToLowerInvariant()} " +
            $"nav_b_j={result.NavigationLotsPreserved.ToString().ToLowerInvariant()} " +
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
