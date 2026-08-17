using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRenderingSkyOverlayUx2EnvD _renderingSkyOverlayUx2EnvD = null!;
    private Button _renderingSkyButtonUx2EnvD = null!;

    private void BuildUx2EnvDActions(HBoxContainer headingRow)
    {
        _renderingSkyButtonUx2EnvD = StudioStyle.Button("Rendu & ciel", compact: true);
        _renderingSkyButtonUx2EnvD.TooltipText =
            "Authorer PBR, HDR, Forward+, budgets et ciel avec simulation déterministe bornée.";
        _renderingSkyButtonUx2EnvD.Pressed += OpenRenderingSkyUx2EnvD;
        headingRow.AddChild(_renderingSkyButtonUx2EnvD);
    }

    private void InitializeUx2EnvDOverlay()
    {
        _renderingSkyOverlayUx2EnvD = new StudioRenderingSkyOverlayUx2EnvD(_session);
        _renderingSkyOverlayUx2EnvD.StatusChanged += SetStatus;
        _renderingSkyOverlayUx2EnvD.AreaEnvironmentChanged += RefreshAfterUx2EnvDMutation;
        AddChild(_renderingSkyOverlayUx2EnvD);
    }

    private void OpenRenderingSkyUx2EnvD()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son rendu.", error: true);
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
        CloseUx1AIOverlay(); CloseUx1AJOverlay(); CloseUx2AOverlay();
        CloseUx2EnvBOverlay(); CloseUx2EnvCOverlay(); CloseUx2EnvEOverlay(); CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        SwitchPage("environment", notify: false);
        _renderingSkyOverlayUx2EnvD.Open();
        SetStatus("Rendu et ciel ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvDMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvDActionState();
    }

    private void UpdateUx2EnvDActionState()
    {
        if (_renderingSkyButtonUx2EnvD is null) return;
        _renderingSkyButtonUx2EnvD.Visible = PageId == "environment" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2EnvDOverlay()
    {
        if (_renderingSkyOverlayUx2EnvD is not null) _renderingSkyOverlayUx2EnvD.Close();
    }

    internal bool PrepareUx2EnvDEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        return _renderingSkyOverlayUx2EnvD.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvDSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _renderingSkyOverlayUx2EnvD.IsOpen;
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
        var originalApproval = _session.CaptureRenderingApprovalUx2EnvD();

        var uiTransaction = _renderingSkyOverlayUx2EnvD.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvDEvidence("ux2-env-d-rendering-sky");
        var overlayOpen = _renderingSkyOverlayUx2EnvD.IsOpen;
        var choiceCount = _renderingSkyOverlayUx2EnvD.ChoiceCount;
        var explicitSelection = _renderingSkyOverlayUx2EnvD.HasExplicitSelection;
        var canonicalPreview = _renderingSkyOverlayUx2EnvD.CanonicalPreviewReady;
        var completeBudgets = _renderingSkyOverlayUx2EnvD.ShowsCompleteBudgets;
        var planWarnings = _renderingSkyOverlayUx2EnvD.ShowsPlanWarnings;
        var structuredDiagnostics = _renderingSkyOverlayUx2EnvD.ShowsStructuredDiagnostics;
        var humanSurface = _renderingSkyOverlayUx2EnvD.ShowsHumanSurface;
        var isolatedEvidence = _renderingSkyOverlayUx2EnvD.UsesIsolatedEvidence;
        var realModuleSource = _renderingSkyOverlayUx2EnvD.UsesRealModuleSource;
        var canonicalFallback = _renderingSkyOverlayUx2EnvD.UsesCanonicalFallbackEvidence;
        var evidenceSourceUntouched = _renderingSkyOverlayUx2EnvD.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeBudgets && planWarnings &&
                      structuredDiagnostics && humanSurface && isolatedEvidence &&
                      (realModuleSource ^ canonicalFallback) && evidenceSourceUntouched;
        _renderingSkyOverlayUx2EnvD.Close();
        var evidenceResourcesReleased = _renderingSkyOverlayUx2EnvD.EvidenceResourcesReleased;
        var sourceSessionRestored = _renderingSkyOverlayUx2EnvD.SourceSessionRestored;

        var result = _session.RunUx2EnvDContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _renderingSkyOverlayUx2EnvD.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _renderingSkyOverlayUx2EnvD.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureRenderingApprovalUx2EnvD() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_D_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete_budgets={completeBudgets.ToString().ToLowerInvariant()} " +
                $"plan_warnings={planWarnings.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
                $"canonical_fallback={canonicalFallback.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_D_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"complete_budgets={result.CompleteBudgets.ToString().ToLowerInvariant()} " +
            $"invalid_rendering={result.InvalidRenderingBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_budget={result.InvalidBudgetBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_sky={result.InvalidSkyBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"render_plans={result.RenderPlans.ToString().ToLowerInvariant()} " +
            $"plan_warnings={result.PlanWarnings.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_fingerprint={result.StaleFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"lot_b={result.LotBPreserved.ToString().ToLowerInvariant()} " +
            $"lot_c={result.LotCPreserved.ToString().ToLowerInvariant()} " +
            $"environment_tails={result.EnvironmentTailsPreserved.ToString().ToLowerInvariant()} " +
            $"area_neighbors={result.AreaNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
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
