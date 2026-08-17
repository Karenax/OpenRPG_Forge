using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioLivingWorldPoliciesOverlay _livingWorldPoliciesOverlay = null!;
    private Button _livingWorldPoliciesButton = null!;

    private void BuildUx2AActions(HBoxContainer headingRow)
    {
        _livingWorldPoliciesButton = StudioStyle.Button("Politiques de zone", compact: true);
        _livingWorldPoliciesButton.TooltipText =
            "Configurer les rythmes, la perception et le repos de la zone par aperçu sec canonique.";
        _livingWorldPoliciesButton.Pressed += OpenLivingWorldPoliciesUx2A;
        headingRow.AddChild(_livingWorldPoliciesButton);
    }

    private void InitializeUx2AOverlay()
    {
        _livingWorldPoliciesOverlay = new StudioLivingWorldPoliciesOverlay(_session);
        _livingWorldPoliciesOverlay.StatusChanged += SetStatus;
        _livingWorldPoliciesOverlay.AreaPoliciesChanged += RefreshAfterUx2AMutation;
        AddChild(_livingWorldPoliciesOverlay);
    }

    private void OpenLivingWorldPoliciesUx2A()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer ses politiques.", error: true);
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
        CloseUx2LwBOverlay();
        SwitchPage("living-world", notify: false);
        _livingWorldPoliciesOverlay.Open();
        SetStatus("Politiques de zone ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2AMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2AActionState();
    }

    private void UpdateUx2AActionState()
    {
        if (_livingWorldPoliciesButton is null)
        {
            return;
        }
        _livingWorldPoliciesButton.Visible = PageId == "living-world" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2AOverlay()
    {
        if (_livingWorldPoliciesOverlay is not null)
        {
            _livingWorldPoliciesOverlay.Close();
        }
    }

    internal bool PrepareUx2AEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        return _livingWorldPoliciesOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx2ASmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _livingWorldPoliciesOverlay.IsOpen;
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
        var originalApproval = _session.CaptureAreaPolicyApprovalUx2A();

        var uiTransaction = _livingWorldPoliciesOverlay.RunUiTransactionEvidence();
        var uiNumericValues = _livingWorldPoliciesOverlay.LastUiNumericValuesPreserved;
        var evidencePrepared = PrepareUx2AEvidence("ux2-a-area-living-world");
        var overlayOpen = _livingWorldPoliciesOverlay.IsOpen;
        var choiceCount = _livingWorldPoliciesOverlay.ChoiceCount;
        var explicitSelection = _livingWorldPoliciesOverlay.HasExplicitSelection;
        var canonicalPreview = _livingWorldPoliciesOverlay.CanonicalPreviewReady;
        var completeCollections = _livingWorldPoliciesOverlay.ShowsCompleteCollections;
        var humanPerceptionDiff = _livingWorldPoliciesOverlay.ShowsHumanPerceptionDiff;
        var dormantRest = _livingWorldPoliciesOverlay.ShowsDormantRest;
        var structuredDiagnostics = _livingWorldPoliciesOverlay.ShowsStructuredDiagnostics;
        var humanSurface = _livingWorldPoliciesOverlay.ShowsHumanSurface;
        var isolatedEvidence = _livingWorldPoliciesOverlay.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _livingWorldPoliciesOverlay.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completeCollections && humanPerceptionDiff &&
                      dormantRest && structuredDiagnostics && humanSurface && isolatedEvidence &&
                      evidenceSourceUntouched;
        _livingWorldPoliciesOverlay.Close();
        var evidenceResourcesReleased = _livingWorldPoliciesOverlay.EvidenceResourcesReleased;
        var sourceSessionRestored = _livingWorldPoliciesOverlay.SourceSessionRestored;

        var result = _session.RunUx2AContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _livingWorldPoliciesOverlay.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _livingWorldPoliciesOverlay.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.IsDirty == originalDirty &&
                              _session.CaptureAreaPolicyApprovalUx2A() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2A_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete_collections={completeCollections.ToString().ToLowerInvariant()} " +
                $"perception_diff={humanPerceptionDiff.ToString().ToLowerInvariant()} " +
                $"dormant_rest={dormantRest.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && uiNumericValues && sourceUnchanged &&
                    surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2A_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_scripts={result.RealScriptCatalog.ToString().ToLowerInvariant()} " +
            $"complete_collections={result.CompleteCollections.ToString().ToLowerInvariant()} " +
            $"numeric_values={result.NumericValuesPreserved.ToString().ToLowerInvariant()} " +
            $"invalid_heartbeat={result.InvalidHeartbeatBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_perception={result.InvalidPerceptionBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_rest={result.InvalidRestBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"perception_diff={result.HumanPerceptionDiff.ToString().ToLowerInvariant()} " +
            $"dormant_rest={result.DormantRestHonest.ToString().ToLowerInvariant()} " +
            $"scheduler_policy={result.SchedulerPolicy.ToString().ToLowerInvariant()} " +
            $"perception_policy={result.PerceptionPolicy.ToString().ToLowerInvariant()} " +
            $"rest_policy={result.RestPolicy.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_fingerprint={result.StaleFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"ui_numeric_values={uiNumericValues.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
