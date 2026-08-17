using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCameraAnchorsOverlayUx2NavM _cameraAnchorsOverlayUx2NavM = null!;
    private Button _cameraAnchorsButtonUx2NavM = null!;

    private void BuildUx2NavMActions(HBoxContainer headingRow)
    {
        _cameraAnchorsButtonUx2NavM = StudioStyle.Button("Ancres caméra", compact: true);
        _cameraAnchorsButtonUx2NavM.TooltipText =
            "Éditer transform, regard, cadrage, champ de vision, durée et cadre sûr avec overlay sec.";
        _cameraAnchorsButtonUx2NavM.Pressed += OpenCameraAnchorsUx2NavM;
        headingRow.AddChild(_cameraAnchorsButtonUx2NavM);
    }

    private void InitializeUx2NavMOverlay()
    {
        _cameraAnchorsOverlayUx2NavM = new(_session, WorldViewport);
        _cameraAnchorsOverlayUx2NavM.StatusChanged += SetStatus;
        _cameraAnchorsOverlayUx2NavM.CameraAnchorChanged += RefreshAfterUx2NavMMutation;
        AddChild(_cameraAnchorsOverlayUx2NavM);
    }

    private void OpenCameraAnchorsUx2NavM()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer ses ancres caméra.", error: true);
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
        CloseUx1WOverlay(); CloseUx1XOverlay(); CloseUx1YOverlay(); CloseUx1ZOverlay();
        CloseUx1AAOverlay(); CloseUx1ABOverlay(); CloseUx1ACOverlay(); CloseUx1ADOverlay();
        CloseUx1AEOverlay(); CloseUx1AFWorkspace(); CloseUx1AGOverlay(); CloseUx1AHOverlay();
        CloseUx1AIOverlay(); CloseUx1AJOverlay(); CloseUx2AOverlay(); CloseUx4AOverlay();
        CloseUx2NavBOverlay(); CloseUx2NavCOverlay(); CloseUx2NavDOverlay(); CloseUx2NavEOverlay();
        CloseUx2NavFOverlay(); CloseUx2NavGOverlay(); CloseUx2NavHOverlay(); CloseUx2NavIOverlay();
        CloseUx2NavJOverlay(); CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _cameraAnchorsOverlayUx2NavM.Open();
        SetStatus("Ancres caméra ouvertes · sélection zone/ancre explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavMMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavMActionState();
    }

    private void UpdateUx2NavMActionState()
    {
        if (_cameraAnchorsButtonUx2NavM is null) return;
        _cameraAnchorsButtonUx2NavM.Visible = PageId == "navigation" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavMOverlay()
    {
        if (_cameraAnchorsOverlayUx2NavM is not null) _cameraAnchorsOverlayUx2NavM.Close();
    }

    internal bool PrepareUx2NavMEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay(); CloseUx2NavCOverlay(); CloseUx2NavDOverlay(); CloseUx2NavEOverlay();
        CloseUx2NavFOverlay(); CloseUx2NavGOverlay(); CloseUx2NavHOverlay(); CloseUx2NavIOverlay();
        CloseUx2NavJOverlay(); CloseUx2EnvJOverlay();
        return _cameraAnchorsOverlayUx2NavM.PrepareEvidence(mode);
    }

    internal bool RunUx2NavMSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _cameraAnchorsOverlayUx2NavM.IsOpen;
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
        var originalApproval = _session.CaptureCameraAnchorApprovalUx2NavM();

        var uiTransaction = _cameraAnchorsOverlayUx2NavM.RunUiTransactionEvidence();
        var transactionReleased = _cameraAnchorsOverlayUx2NavM.EvidenceResourcesReleased;
        var transactionRestored = _cameraAnchorsOverlayUx2NavM.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavMEvidence("ux2-nav-m-camera-anchors");
        var overlayOpen = _cameraAnchorsOverlayUx2NavM.IsOpen;
        var explicitSelection = _cameraAnchorsOverlayUx2NavM.HasExplicitSelection;
        var canonicalPreview = _cameraAnchorsOverlayUx2NavM.CanonicalPreviewReady;
        var viewportOverlay = _cameraAnchorsOverlayUx2NavM.ShowsViewportOverlay;
        var framingPreview = _cameraAnchorsOverlayUx2NavM.ShowsFramingPreview;
        var runtimeBoundary = _cameraAnchorsOverlayUx2NavM.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _cameraAnchorsOverlayUx2NavM.ShowsStructuredDiagnostics;
        var humanSurface = _cameraAnchorsOverlayUx2NavM.ShowsHumanSurface;
        var canonicalFallback = _cameraAnchorsOverlayUx2NavM.UsesCanonicalFallback;
        var realModuleAnchor = _cameraAnchorsOverlayUx2NavM.UsesRealModuleAnchor;
        var isolatedEvidence = _cameraAnchorsOverlayUx2NavM.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _cameraAnchorsOverlayUx2NavM.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _cameraAnchorsOverlayUx2NavM.AreaChoiceCount >= 1 &&
                      _cameraAnchorsOverlayUx2NavM.AnchorChoiceCount >= 1 && explicitSelection &&
                      canonicalPreview && viewportOverlay && framingPreview && runtimeBoundary &&
                      structuredDiagnostics && humanSurface && (canonicalFallback || realModuleAnchor) &&
                      isolatedEvidence && evidenceSourceUntouched;
        _cameraAnchorsOverlayUx2NavM.Close();
        var evidenceReleased = _cameraAnchorsOverlayUx2NavM.EvidenceResourcesReleased;
        var sourceSessionRestored = _cameraAnchorsOverlayUx2NavM.SourceSessionRestored;

        var result = _session.RunUx2NavMContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _cameraAnchorsOverlayUx2NavM.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _cameraAnchorsOverlayUx2NavM.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureCameraAnchorApprovalUx2NavM() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_M_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} viewport={viewportOverlay.ToString().ToLowerInvariant()} " +
                $"framing={framingPreview.ToString().ToLowerInvariant()} boundary={runtimeBoundary.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} human={humanSurface.ToString().ToLowerInvariant()} " +
                $"fallback={canonicalFallback.ToString().ToLowerInvariant()} real={realModuleAnchor.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && transactionReleased && transactionRestored &&
                    evidenceReleased && sourceSessionRestored && sourceUnchanged && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_M_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitAreaAndAnchorSelection.ToString().ToLowerInvariant()} " +
            $"creation={result.CreationSurface.ToString().ToLowerInvariant()} " +
            $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
            $"transform_validation={result.TransformValidation.ToString().ToLowerInvariant()} " +
            $"bounds_validation={result.BoundsValidation.ToString().ToLowerInvariant()} " +
            $"lens_timing_safe={result.LensTimingSafeAreaValidation.ToString().ToLowerInvariant()} " +
            $"duplicate_name={result.DuplicateNameValidation.ToString().ToLowerInvariant()} " +
            $"overlay={result.OverlayPreview.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"module_cinematics={result.ModuleCinematicsPreserved.ToString().ToLowerInvariant()} " +
            $"target_area_neighbors={result.TargetAreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"other_anchors={result.OtherAnchorsPreserved.ToString().ToLowerInvariant()} " +
            $"other_areas={result.OtherAreasPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"fallback={canonicalFallback.ToString().ToLowerInvariant()} real_module={realModuleAnchor.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
