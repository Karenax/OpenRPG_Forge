using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerLivingWorldOverlayUx2LwE _triggerLivingWorldOverlayUx2LwE = null!;
    private Button _triggerLivingWorldButtonUx2LwE = null!;

    private void BuildUx2LwEActions(HBoxContainer headingRow)
    {
        _triggerLivingWorldButtonUx2LwE = StudioStyle.Button(
            "Repos, pistes, interjections", compact: true);
        _triggerLivingWorldButtonUx2LwE.TooltipText =
            "Configurer les politiques vivantes d’un déclencheur existant par aperçu sec canonique.";
        _triggerLivingWorldButtonUx2LwE.Pressed += OpenLivingWorldTriggerUx2LwE;
        headingRow.AddChild(_triggerLivingWorldButtonUx2LwE);
    }

    private void InitializeUx2LwEOverlay()
    {
        _triggerLivingWorldOverlayUx2LwE = new StudioTriggerLivingWorldOverlayUx2LwE(_session);
        _triggerLivingWorldOverlayUx2LwE.StatusChanged += SetStatus;
        _triggerLivingWorldOverlayUx2LwE.LivingWorldTriggerChanged += RefreshAfterUx2LwEMutation;
        AddChild(_triggerLivingWorldOverlayUx2LwE);
    }

    private void OpenLivingWorldTriggerUx2LwE()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer ses déclencheurs vivants.", error: true);
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
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        CloseUx4AOverlay();
        CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _triggerLivingWorldOverlayUx2LwE.Open();
        SetStatus("Repos, pistes et interjections ouverts · déclencheur explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwEMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwEActionState();
    }

    private void UpdateUx2LwEActionState()
    {
        if (_triggerLivingWorldButtonUx2LwE is null) return;
        _triggerLivingWorldButtonUx2LwE.Visible = PageId == "living-world" &&
                                                  _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwEOverlay()
    {
        if (_triggerLivingWorldOverlayUx2LwE is not null)
            _triggerLivingWorldOverlayUx2LwE.Close();
    }

    internal bool PrepareUx2LwEEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        return _triggerLivingWorldOverlayUx2LwE.PrepareEvidence(mode);
    }

    internal bool RunUx2LwESmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _triggerLivingWorldOverlayUx2LwE.IsOpen;
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
        var originalApproval = _session.CaptureLivingWorldTriggerApprovalUx2LwE();

        var uiTransaction = _triggerLivingWorldOverlayUx2LwE.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwEEvidence("ux2-lw-e-trigger-living-world");
        var overlayOpen = _triggerLivingWorldOverlayUx2LwE.IsOpen;
        var choiceCount = _triggerLivingWorldOverlayUx2LwE.ChoiceCount;
        var explicitSelection = _triggerLivingWorldOverlayUx2LwE.HasExplicitSelection;
        var canonicalPreview = _triggerLivingWorldOverlayUx2LwE.CanonicalPreviewReady;
        var threePolicies = _triggerLivingWorldOverlayUx2LwE.ShowsThreePolicies;
        var honestBoundary = _triggerLivingWorldOverlayUx2LwE.ShowsNoRuntimeExecutionBoundary;
        var structuredDiagnostics = _triggerLivingWorldOverlayUx2LwE.ShowsStructuredDiagnostics;
        var humanSurface = _triggerLivingWorldOverlayUx2LwE.ShowsHumanSurface;
        var realReferences = _triggerLivingWorldOverlayUx2LwE.UsesRealReferences;
        var isolatedEvidence = _triggerLivingWorldOverlayUx2LwE.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _triggerLivingWorldOverlayUx2LwE.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && threePolicies && honestBoundary &&
                      structuredDiagnostics && humanSurface && realReferences && isolatedEvidence &&
                      evidenceSourceUntouched;
        _triggerLivingWorldOverlayUx2LwE.Close();
        var evidenceResourcesReleased = _triggerLivingWorldOverlayUx2LwE.EvidenceResourcesReleased;
        var sourceSessionRestored = _triggerLivingWorldOverlayUx2LwE.SourceSessionRestored;

        var result = _session.RunUx2LwEContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
            _triggerLivingWorldOverlayUx2LwE.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _triggerLivingWorldOverlayUx2LwE.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureLivingWorldTriggerApprovalUx2LwE() == originalApproval;

        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_E_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"policies={threePolicies.ToString().ToLowerInvariant()} " +
                $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"real_references={realReferences.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }

        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_LW_E_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"rest_validation={result.RestValidation.ToString().ToLowerInvariant()} " +
            $"clue_validation={result.ClueValidation.ToString().ToLowerInvariant()} " +
            $"narrative_validation={result.NarrativeValidation.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"optional_removal={result.OptionalRemoval.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"collections={result.CollectionPreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
