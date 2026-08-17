using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioClimateWaterReverbOverlayUx2EnvC _climateWaterReverbOverlayUx2EnvC = null!;
    private Button _climateWaterReverbButtonUx2EnvC = null!;

    private void BuildUx2EnvCActions(HBoxContainer headingRow)
    {
        _climateWaterReverbButtonUx2EnvC = StudioStyle.Button("Climat, eau et réverbération", compact: true);
        _climateWaterReverbButtonUx2EnvC.TooltipText =
            "Authorer vent, météo, eau et zones acoustiques avec aperçu sec déterministe.";
        _climateWaterReverbButtonUx2EnvC.Pressed += OpenClimateWaterReverbUx2EnvC;
        headingRow.AddChild(_climateWaterReverbButtonUx2EnvC);
    }

    private void InitializeUx2EnvCOverlay()
    {
        _climateWaterReverbOverlayUx2EnvC = new StudioClimateWaterReverbOverlayUx2EnvC(_session);
        _climateWaterReverbOverlayUx2EnvC.StatusChanged += SetStatus;
        _climateWaterReverbOverlayUx2EnvC.AreaEnvironmentChanged += RefreshAfterUx2EnvCMutation;
        AddChild(_climateWaterReverbOverlayUx2EnvC);
    }

    private void OpenClimateWaterReverbUx2EnvC()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son climat.", error: true);
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
        CloseUx2EnvBOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        SwitchPage("environment", notify: false);
        _climateWaterReverbOverlayUx2EnvC.Open();
        SetStatus("Climat, eau et réverbération ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvCMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvCActionState();
    }

    private void UpdateUx2EnvCActionState()
    {
        if (_climateWaterReverbButtonUx2EnvC is null) return;
        _climateWaterReverbButtonUx2EnvC.Visible =
            PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvCOverlay()
    {
        if (_climateWaterReverbOverlayUx2EnvC is not null)
            _climateWaterReverbOverlayUx2EnvC.Close();
    }

    internal bool PrepareUx2EnvCEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvBOverlay();
        CloseUx2EnvEOverlay();
        return _climateWaterReverbOverlayUx2EnvC.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvCSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _climateWaterReverbOverlayUx2EnvC.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureClimateApprovalUx2EnvC();

        var uiTransaction = _climateWaterReverbOverlayUx2EnvC.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvCEvidence("ux2-env-c-climate-water-reverb");
        var overlayOpen = _climateWaterReverbOverlayUx2EnvC.IsOpen;
        var choiceCount = _climateWaterReverbOverlayUx2EnvC.ChoiceCount;
        var explicitSelection = _climateWaterReverbOverlayUx2EnvC.HasExplicitSelection;
        var canonicalPreview = _climateWaterReverbOverlayUx2EnvC.CanonicalPreviewReady;
        var completeCollections = _climateWaterReverbOverlayUx2EnvC.ShowsCompleteCollections;
        var structuredDiagnostics = _climateWaterReverbOverlayUx2EnvC.ShowsStructuredDiagnostics;
        var humanSurface = _climateWaterReverbOverlayUx2EnvC.ShowsHumanSurface;
        var isolatedEvidence = _climateWaterReverbOverlayUx2EnvC.UsesIsolatedEvidence;
        var realModuleSource = _climateWaterReverbOverlayUx2EnvC.UsesRealModuleSource;
        var canonicalFallback = _climateWaterReverbOverlayUx2EnvC.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _climateWaterReverbOverlayUx2EnvC.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeCollections && structuredDiagnostics &&
                      humanSurface && isolatedEvidence && (realModuleSource ^ canonicalFallback) &&
                      evidenceSourceUntouched;
        _climateWaterReverbOverlayUx2EnvC.Close();
        var evidenceResourcesReleased = _climateWaterReverbOverlayUx2EnvC.EvidenceResourcesReleased;
        var sourceSessionRestored = _climateWaterReverbOverlayUx2EnvC.SourceSessionRestored;

        var result = _session.RunUx2EnvCContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _climateWaterReverbOverlayUx2EnvC.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _climateWaterReverbOverlayUx2EnvC.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureClimateApprovalUx2EnvC() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_C_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete_collections={completeCollections.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
                $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_C_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"complete_collections={result.CompleteCollections.ToString().ToLowerInvariant()} " +
            $"invalid_wind={result.InvalidWindBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_weather={result.InvalidWeatherBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_water={result.InvalidWaterBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_reverb={result.InvalidReverbBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"bounded_plans={result.BoundedPlans.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_fingerprint={result.StaleFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"lot_b={result.LotBPreserved.ToString().ToLowerInvariant()} " +
            $"environment_tails={result.EnvironmentTailsPreserved.ToString().ToLowerInvariant()} " +
            $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
            $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
