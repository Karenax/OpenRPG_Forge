using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioVisibilityOcclusionOverlayUx2NavJ _visibilityOverlayUx2NavJ = null!;
    private Button _visibilityButtonUx2NavJ = null!;

    private void BuildUx2NavJActions(HBoxContainer headingRow)
    {
        _visibilityButtonUx2NavJ = StudioStyle.Button("Visibilité", compact: true);
        _visibilityButtonUx2NavJ.TooltipText =
            "Éditer volumes, passages, écrans d’occlusion, cellules et streaming avec aperçu sec";
        _visibilityButtonUx2NavJ.Pressed += OpenVisibilityUx2NavJ;
        headingRow.AddChild(_visibilityButtonUx2NavJ);
    }

    private void InitializeUx2NavJOverlay()
    {
        _visibilityOverlayUx2NavJ = new StudioVisibilityOcclusionOverlayUx2NavJ(_session,
            WorldViewport);
        _visibilityOverlayUx2NavJ.StatusChanged += SetStatus;
        _visibilityOverlayUx2NavJ.VisibilityProfileChanged += RefreshAfterUx2NavJMutation;
        AddChild(_visibilityOverlayUx2NavJ);
    }

    private void OpenVisibilityUx2NavJ()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer sa visibilité.", error: true);
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
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _visibilityOverlayUx2NavJ.Open();
        SetStatus("Visibilité ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavJMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavJActionState();
    }

    private void UpdateUx2NavJActionState()
    {
        if (_visibilityButtonUx2NavJ is null) return;
        _visibilityButtonUx2NavJ.Visible = PageId == "navigation" &&
                                           _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavJOverlay()
    {
        if (_visibilityOverlayUx2NavJ is not null) _visibilityOverlayUx2NavJ.Close();
    }

    internal bool PrepareUx2NavJEvidence(string mode)
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
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        return _visibilityOverlayUx2NavJ.PrepareEvidence(mode);
    }

    internal bool RunUx2NavJSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _visibilityOverlayUx2NavJ.IsOpen;
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
        var originalApproval = _session.CaptureVisibilityApprovalUx2NavJ();

        var uiTransaction = _visibilityOverlayUx2NavJ.RunUiTransactionEvidence();
        var uiTransactionReleased = _visibilityOverlayUx2NavJ.EvidenceResourcesReleased;
        var uiTransactionRestored = _visibilityOverlayUx2NavJ.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavJEvidence("ux2-nav-j");
        var overlayOpen = _visibilityOverlayUx2NavJ.IsOpen;
        var explicitSelection = _visibilityOverlayUx2NavJ.HasExplicitSelection;
        var canonicalPreview = _visibilityOverlayUx2NavJ.CanonicalPreviewReady;
        var viewportOverlay = _visibilityOverlayUx2NavJ.ShowsViewportOverlay;
        var structuredDiagnostics = _visibilityOverlayUx2NavJ.ShowsStructuredDiagnostics;
        var boundedRuntime = _visibilityOverlayUx2NavJ.ShowsBoundedRuntimeClaim;
        var humanSurface = _visibilityOverlayUx2NavJ.ShowsHumanSurface;
        var isolatedEvidence = _visibilityOverlayUx2NavJ.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _visibilityOverlayUx2NavJ.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _visibilityOverlayUx2NavJ.ChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && structuredDiagnostics && boundedRuntime &&
                      humanSurface && isolatedEvidence && evidenceSourceUntouched;
        _visibilityOverlayUx2NavJ.Close();
        var evidenceReleased = _visibilityOverlayUx2NavJ.EvidenceResourcesReleased;
        var sourceSessionRestored = _visibilityOverlayUx2NavJ.SourceSessionRestored;

        var result = _session.RunUx2NavJContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _visibilityOverlayUx2NavJ.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _visibilityOverlayUx2NavJ.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureVisibilityApprovalUx2NavJ() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_J_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} bounded={boundedRuntime.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _visibilityOverlayUx2NavJ.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_J_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"visibility_authoring={result.VisibilityAuthoring.ToString().ToLowerInvariant()} " +
            $"stable_order={result.StableOrder.ToString().ToLowerInvariant()} " +
            $"unknown_neighbors={result.UnknownNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"lod_reference={result.LodPreservedByReference.ToString().ToLowerInvariant()} " +
            $"other_authoring={result.OtherAuthoringPreserved.ToString().ToLowerInvariant()} " +
            $"validation={result.Validation.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
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
