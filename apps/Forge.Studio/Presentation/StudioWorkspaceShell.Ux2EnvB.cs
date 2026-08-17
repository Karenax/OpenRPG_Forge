using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaEnvironmentFoundationOverlayUx2EnvB _areaEnvironmentFoundationOverlayUx2EnvB = null!;
    private Button _areaEnvironmentFoundationButtonUx2EnvB = null!;

    private void BuildUx2EnvBActions(HBoxContainer headingRow)
    {
        _areaEnvironmentFoundationButtonUx2EnvB = StudioStyle.Button("Fondations d’environnement", compact: true);
        _areaEnvironmentFoundationButtonUx2EnvB.TooltipText =
            "Configurer classification, cycle, lumière physique et brouillard par aperçu sec canonique.";
        _areaEnvironmentFoundationButtonUx2EnvB.Pressed += OpenAreaEnvironmentFoundationUx2EnvB;
        headingRow.AddChild(_areaEnvironmentFoundationButtonUx2EnvB);
    }

    private void InitializeUx2EnvBOverlay()
    {
        _areaEnvironmentFoundationOverlayUx2EnvB = new StudioAreaEnvironmentFoundationOverlayUx2EnvB(_session);
        _areaEnvironmentFoundationOverlayUx2EnvB.StatusChanged += SetStatus;
        _areaEnvironmentFoundationOverlayUx2EnvB.AreaEnvironmentChanged += RefreshAfterUx2EnvBMutation;
        AddChild(_areaEnvironmentFoundationOverlayUx2EnvB);
    }

    private void OpenAreaEnvironmentFoundationUx2EnvB()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son environnement.", error: true);
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
        CloseUx2EnvCOverlay();
        SwitchPage("environment", notify: false);
        _areaEnvironmentFoundationOverlayUx2EnvB.Open();
        SetStatus("Fondations d’environnement ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvBMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvBActionState();
    }

    private void UpdateUx2EnvBActionState()
    {
        if (_areaEnvironmentFoundationButtonUx2EnvB is null) return;
        _areaEnvironmentFoundationButtonUx2EnvB.Visible =
            PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvBOverlay()
    {
        if (_areaEnvironmentFoundationOverlayUx2EnvB is not null)
            _areaEnvironmentFoundationOverlayUx2EnvB.Close();
    }

    internal bool PrepareUx2EnvBEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvCOverlay();
        return _areaEnvironmentFoundationOverlayUx2EnvB.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvBSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _areaEnvironmentFoundationOverlayUx2EnvB.IsOpen;
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
        var originalApproval = _session.CaptureAreaEnvironmentApprovalUx2EnvB();

        var uiTransaction = _areaEnvironmentFoundationOverlayUx2EnvB.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvBEvidence("ux2-env-b-area-environment");
        var overlayOpen = _areaEnvironmentFoundationOverlayUx2EnvB.IsOpen;
        var choiceCount = _areaEnvironmentFoundationOverlayUx2EnvB.ChoiceCount;
        var explicitSelection = _areaEnvironmentFoundationOverlayUx2EnvB.HasExplicitSelection;
        var canonicalPreview = _areaEnvironmentFoundationOverlayUx2EnvB.CanonicalPreviewReady;
        var completeCollections = _areaEnvironmentFoundationOverlayUx2EnvB.ShowsCompleteCollections;
        var structuredDiagnostics = _areaEnvironmentFoundationOverlayUx2EnvB.ShowsStructuredDiagnostics;
        var humanSurface = _areaEnvironmentFoundationOverlayUx2EnvB.ShowsHumanSurface;
        var isolatedEvidence = _areaEnvironmentFoundationOverlayUx2EnvB.UsesIsolatedEvidence;
        var realModuleSource = _areaEnvironmentFoundationOverlayUx2EnvB.UsesRealModuleSource;
        var canonicalFallback = _areaEnvironmentFoundationOverlayUx2EnvB.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _areaEnvironmentFoundationOverlayUx2EnvB.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeCollections && structuredDiagnostics &&
                      humanSurface && isolatedEvidence && (realModuleSource ^ canonicalFallback) &&
                      evidenceSourceUntouched;
        _areaEnvironmentFoundationOverlayUx2EnvB.Close();
        var evidenceResourcesReleased = _areaEnvironmentFoundationOverlayUx2EnvB.EvidenceResourcesReleased;
        var sourceSessionRestored = _areaEnvironmentFoundationOverlayUx2EnvB.SourceSessionRestored;

        var result = _session.RunUx2EnvBContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
            _areaEnvironmentFoundationOverlayUx2EnvB.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _areaEnvironmentFoundationOverlayUx2EnvB.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureAreaEnvironmentApprovalUx2EnvB() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_B_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_B_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"complete_collections={result.CompleteCollections.ToString().ToLowerInvariant()} " +
            $"invalid_classification={result.InvalidClassificationBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_cycle={result.InvalidCycleBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_lighting={result.InvalidLightingBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_fog={result.InvalidFogBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_fingerprint={result.StaleFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
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
