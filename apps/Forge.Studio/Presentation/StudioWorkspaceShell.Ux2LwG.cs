using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioEncounterSelectionActivationOverlayUx2LwG _encounterPolicyOverlayUx2LwG = null!;
    private Button _encounterPolicyButtonUx2LwG = null!;

    private void BuildUx2LwGActions(HBoxContainer headingRow)
    {
        _encounterPolicyButtonUx2LwG = StudioStyle.Button("Sélection et activation", compact: true);
        _encounterPolicyButtonUx2LwG.TooltipText =
            "Configurer l’évaluation de groupe et la politique unique ou continue par aperçu sec canonique.";
        _encounterPolicyButtonUx2LwG.Pressed += OpenEncounterPolicyUx2LwG;
        headingRow.AddChild(_encounterPolicyButtonUx2LwG);
    }

    private void InitializeUx2LwGOverlay()
    {
        _encounterPolicyOverlayUx2LwG = new StudioEncounterSelectionActivationOverlayUx2LwG(_session);
        _encounterPolicyOverlayUx2LwG.StatusChanged += SetStatus;
        _encounterPolicyOverlayUx2LwG.EncounterChanged += RefreshAfterUx2LwGMutation;
        AddChild(_encounterPolicyOverlayUx2LwG);
    }

    private void OpenEncounterPolicyUx2LwG()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les politiques de rencontre.", error: true);
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
        CloseUx1AIOverlay(); CloseUx1AJOverlay();
        CloseUx2AOverlay(); CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay();
        CloseUx2LwEOverlay(); CloseUx2LwFOverlay(); CloseUx2LwHOverlay(); CloseUx2LwIOverlay(); CloseUx2LwJOverlay(); CloseUx2LwKOverlay(); CloseUx2LwLOverlay(); CloseUx4AOverlay(); CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _encounterPolicyOverlayUx2LwG.Open();
        SetStatus("Politiques de rencontre ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwGMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwGActionState();
    }

    private void UpdateUx2LwGActionState()
    {
        if (_encounterPolicyButtonUx2LwG is null) return;
        _encounterPolicyButtonUx2LwG.Visible = PageId == "living-world" &&
                                               _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwGOverlay()
    {
        if (_encounterPolicyOverlayUx2LwG is not null)
            _encounterPolicyOverlayUx2LwG.Close();
    }

    internal bool PrepareUx2LwGEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2LwBOverlay(); CloseUx2LwCOverlay(); CloseUx2LwDOverlay(); CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwJOverlay();
        CloseUx2LwKOverlay();
        CloseUx2LwLOverlay();
        return _encounterPolicyOverlayUx2LwG.PrepareEvidence(mode);
    }

    internal bool RunUx2LwGSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _encounterPolicyOverlayUx2LwG.IsOpen;
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
        var originalApproval = _session.CaptureEncounterPolicyApprovalUx2LwG();

        var uiTransaction = _encounterPolicyOverlayUx2LwG.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwGEvidence("ux2-lw-g-encounter-selection-activation");
        var overlayOpen = _encounterPolicyOverlayUx2LwG.IsOpen;
        var choiceCount = _encounterPolicyOverlayUx2LwG.ChoiceCount;
        var explicitSelection = _encounterPolicyOverlayUx2LwG.HasExplicitSelection;
        var canonicalPreview = _encounterPolicyOverlayUx2LwG.CanonicalPreviewReady;
        var twoRoutes = _encounterPolicyOverlayUx2LwG.ShowsTwoRoutes;
        var honestBoundary = _encounterPolicyOverlayUx2LwG.ShowsHonestRuntimeBoundary;
        var structuredDiagnostics = _encounterPolicyOverlayUx2LwG.ShowsStructuredDiagnostics;
        var humanSurface = _encounterPolicyOverlayUx2LwG.ShowsHumanSurface;
        var realScripts = _encounterPolicyOverlayUx2LwG.UsesOnlyRealScripts;
        var isolatedEvidence = _encounterPolicyOverlayUx2LwG.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _encounterPolicyOverlayUx2LwG.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && twoRoutes && honestBoundary &&
                      structuredDiagnostics && humanSurface && realScripts && isolatedEvidence &&
                      evidenceSourceUntouched;
        _encounterPolicyOverlayUx2LwG.Close();
        var evidenceResourcesReleased = _encounterPolicyOverlayUx2LwG.EvidenceResourcesReleased;
        var sourceSessionRestored = _encounterPolicyOverlayUx2LwG.SourceSessionRestored;

        var result = _session.RunUx2LwGContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _encounterPolicyOverlayUx2LwG.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _encounterPolicyOverlayUx2LwG.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureEncounterPolicyApprovalUx2LwG() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_G_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                        $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                        $"routes={twoRoutes.ToString().ToLowerInvariant()} " +
                        $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                        $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"real_scripts={realScripts.ToString().ToLowerInvariant()} " +
                        $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_LW_G_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
                 $"strategy_validation={result.StrategyValidation.ToString().ToLowerInvariant()} " +
                 $"provider_guard={result.ProviderGuard.ToString().ToLowerInvariant()} " +
                 $"activation_validation={result.ActivationValidation.ToString().ToLowerInvariant()} " +
                 $"real_script={result.RealScriptValidation.ToString().ToLowerInvariant()} " +
                 $"deterministic_preview={result.DeterministicPreview.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"collections={result.CollectionsPreserved.ToString().ToLowerInvariant()} " +
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
