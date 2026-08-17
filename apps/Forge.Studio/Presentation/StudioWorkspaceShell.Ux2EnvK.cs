using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioWaterGraphicsQualityOverlayUx2EnvK _waterGraphicsQualityOverlayUx2EnvK = null!;
    private Button _waterGraphicsQualityButtonUx2EnvK = null!;

    private void BuildUx2EnvKActions(HBoxContainer headingRow)
    {
        _waterGraphicsQualityButtonUx2EnvK = StudioStyle.Button("Qualité eau & image", compact: true);
        _waterGraphicsQualityButtonUx2EnvK.TooltipText =
            "Éditer la politique eau, MSAA, anisotropie et repli sans prétendre modifier le renderer en direct.";
        _waterGraphicsQualityButtonUx2EnvK.Pressed += OpenWaterGraphicsQualityUx2EnvK;
        headingRow.AddChild(_waterGraphicsQualityButtonUx2EnvK);
    }

    private void InitializeUx2EnvKOverlay()
    {
        _waterGraphicsQualityOverlayUx2EnvK = new StudioWaterGraphicsQualityOverlayUx2EnvK(_session);
        _waterGraphicsQualityOverlayUx2EnvK.StatusChanged += SetStatus;
        _waterGraphicsQualityOverlayUx2EnvK.WaterGraphicsChanged += RefreshAfterUx2EnvKMutation;
        AddChild(_waterGraphicsQualityOverlayUx2EnvK);
    }

    private void OpenWaterGraphicsQualityUx2EnvK()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant la qualité eau & image.", error: true);
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
        CloseUx2EnvCOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("environment", notify: false);
        _waterGraphicsQualityOverlayUx2EnvK.Open();
        SetStatus("Qualité eau & image ouverte · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2EnvKMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2EnvKActionState();
    }

    private void UpdateUx2EnvKActionState()
    {
        if (_waterGraphicsQualityButtonUx2EnvK is null) return;
        _waterGraphicsQualityButtonUx2EnvK.Visible = PageId == "environment" && _session.Project is not null;
    }

    private void CloseUx2EnvKOverlay()
    {
        if (_waterGraphicsQualityOverlayUx2EnvK is not null) _waterGraphicsQualityOverlayUx2EnvK.Close();
    }

    internal bool PrepareUx2EnvKEvidence(string mode)
    {
        SwitchPage("environment", notify: false);
        CloseUx2EnvBOverlay();
        CloseUx2EnvCOverlay();
        CloseUx2EnvDOverlay();
        CloseUx2EnvEOverlay();
        CloseUx2EnvFOverlay();
        CloseUx2EnvGOverlay();
        CloseUx2EnvHOverlay();
        CloseUx2EnvIOverlay();
        CloseUx2EnvJOverlay();
        return _waterGraphicsQualityOverlayUx2EnvK.PrepareEvidence(mode);
    }

    internal bool RunUx2EnvKSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _waterGraphicsQualityOverlayUx2EnvK.IsOpen;
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
        var originalApproval = _session.CaptureWaterGraphicsApprovalUx2EnvK();

        var uiTransaction = _waterGraphicsQualityOverlayUx2EnvK.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2EnvKEvidence("ux2-env-k-water-graphics");
        var overlayOpen = _waterGraphicsQualityOverlayUx2EnvK.IsOpen;
        var choiceCount = _waterGraphicsQualityOverlayUx2EnvK.ChoiceCount;
        var explicitSelection = _waterGraphicsQualityOverlayUx2EnvK.HasExplicitSelection;
        var canonicalPreview = _waterGraphicsQualityOverlayUx2EnvK.CanonicalPreviewReady;
        var waterPlan = _waterGraphicsQualityOverlayUx2EnvK.ShowsWaterPlan;
        var graphicsPlan = _waterGraphicsQualityOverlayUx2EnvK.ShowsGraphicsPlan;
        var structuredDiagnostics = _waterGraphicsQualityOverlayUx2EnvK.ShowsStructuredDiagnostics;
        var boundedRuntime = _waterGraphicsQualityOverlayUx2EnvK.ShowsBoundedRuntimeClaim;
        var humanSurface = _waterGraphicsQualityOverlayUx2EnvK.ShowsHumanSurface;
        var isolatedEvidence = _waterGraphicsQualityOverlayUx2EnvK.UsesIsolatedEvidence;
        var realModuleSource = _waterGraphicsQualityOverlayUx2EnvK.UsesRealModuleSource;
        var evidenceSourceUntouched = _waterGraphicsQualityOverlayUx2EnvK.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "environment" && overlayOpen && choiceCount == 1 &&
                      explicitSelection && canonicalPreview && waterPlan && graphicsPlan && structuredDiagnostics &&
                      boundedRuntime && humanSurface && isolatedEvidence && realModuleSource &&
                      evidenceSourceUntouched;
        _waterGraphicsQualityOverlayUx2EnvK.Close();
        var evidenceResourcesReleased = _waterGraphicsQualityOverlayUx2EnvK.EvidenceResourcesReleased;
        var sourceSessionRestored = _waterGraphicsQualityOverlayUx2EnvK.SourceSessionRestored;

        var result = _session.RunUx2EnvKContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _waterGraphicsQualityOverlayUx2EnvK.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreWaterGraphicsApprovalUx2EnvK(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _waterGraphicsQualityOverlayUx2EnvK.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureWaterGraphicsApprovalUx2EnvK() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_K_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"water_plan={waterPlan.ToString().ToLowerInvariant()} " +
                $"graphics_plan={graphicsPlan.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"bounded_runtime={boundedRuntime.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_ENV_K_{(valid ? "PASS" : "FAILED")} " +
            $"routes=EE-009,EE-030 " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_module_profile={result.RealModuleProfileDetected.ToString().ToLowerInvariant()} " +
            $"real_module_water={result.RealModuleWaterDetected.ToString().ToLowerInvariant()} " +
            $"invalid_water={result.InvalidWaterQualityBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_aa={result.InvalidAntiAliasingBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_aniso={result.InvalidAnisotropyBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_upscaling={result.InvalidUpscalingBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_dry_run={result.CanonicalDryRun.ToString().ToLowerInvariant()} " +
            $"water_plan={result.WaterPlanBounded.ToString().ToLowerInvariant()} " +
            $"graphics_plan={result.GraphicsPlanBounded.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.ApprovalFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"apply_undo_redo={result.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"creator_tails={result.CreatorTailsPreserved.ToString().ToLowerInvariant()} " +
            $"post_launch_tails={result.PostLaunchTailsPreserved.ToString().ToLowerInvariant()} " +
            $"extension_tails={result.ExtensionTailsPreserved.ToString().ToLowerInvariant()} " +
            $"runtime_policy_tails={result.RuntimePolicyTailsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"bounded_runtime={result.BoundedRuntimeClaim.ToString().ToLowerInvariant()} " +
            $"cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"real_module_source={realModuleSource.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
