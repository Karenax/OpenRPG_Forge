using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaLifecycleEventsOverlayUx2LwB _areaLifecycleOverlayUx2LwB = null!;
    private Button _areaLifecycleButtonUx2LwB = null!;

    private void BuildUx2LwBActions(HBoxContainer headingRow)
    {
        _areaLifecycleButtonUx2LwB = StudioStyle.Button("Entrée / sortie", compact: true);
        _areaLifecycleButtonUx2LwB.TooltipText =
            "Relier les événements typés d’entrée et de sortie par aperçu sec canonique.";
        _areaLifecycleButtonUx2LwB.Pressed += OpenAreaLifecycleUx2LwB;
        headingRow.AddChild(_areaLifecycleButtonUx2LwB);
    }

    private void InitializeUx2LwBOverlay()
    {
        _areaLifecycleOverlayUx2LwB = new StudioAreaLifecycleEventsOverlayUx2LwB(_session);
        _areaLifecycleOverlayUx2LwB.StatusChanged += SetStatus;
        _areaLifecycleOverlayUx2LwB.AreaLifecycleChanged += RefreshAfterUx2LwBMutation;
        AddChild(_areaLifecycleOverlayUx2LwB);
    }

    private void OpenAreaLifecycleUx2LwB()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son entrée et sa sortie.", error: true);
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
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwKOverlay();
        CloseUx4AOverlay();
        CloseUx5APage();
        SwitchPage("living-world", notify: false);
        _areaLifecycleOverlayUx2LwB.Open();
        SetStatus("Entrée / sortie ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwBMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwBActionState();
    }

    private void UpdateUx2LwBActionState()
    {
        if (_areaLifecycleButtonUx2LwB is null)
        {
            return;
        }
        _areaLifecycleButtonUx2LwB.Visible = PageId == "living-world" &&
                                             _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwBOverlay()
    {
        if (_areaLifecycleOverlayUx2LwB is not null)
        {
            _areaLifecycleOverlayUx2LwB.Close();
        }
    }

    internal bool PrepareUx2LwBEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx2LwKOverlay();
        return _areaLifecycleOverlayUx2LwB.PrepareEvidence(mode);
    }

    internal bool RunUx2LwBSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _areaLifecycleOverlayUx2LwB.IsOpen;
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
        var originalApproval = _session.CaptureAreaLifecycleApprovalUx2LwB();

        var uiTransaction = _areaLifecycleOverlayUx2LwB.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwBEvidence("ux2-lw-b-area-lifecycle");
        var overlayOpen = _areaLifecycleOverlayUx2LwB.IsOpen;
        var choiceCount = _areaLifecycleOverlayUx2LwB.ChoiceCount;
        var explicitSelection = _areaLifecycleOverlayUx2LwB.HasExplicitSelection;
        var canonicalPreview = _areaLifecycleOverlayUx2LwB.CanonicalPreviewReady;
        var bothEvents = _areaLifecycleOverlayUx2LwB.ShowsBothEvents;
        var honestBoundary = _areaLifecycleOverlayUx2LwB.ShowsNoExecutionBoundary;
        var structuredDiagnostics = _areaLifecycleOverlayUx2LwB.ShowsStructuredDiagnostics;
        var humanSurface = _areaLifecycleOverlayUx2LwB.ShowsHumanSurface;
        var realScripts = _areaLifecycleOverlayUx2LwB.UsesRealScriptCatalog;
        var isolatedEvidence = _areaLifecycleOverlayUx2LwB.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _areaLifecycleOverlayUx2LwB.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && bothEvents && honestBoundary &&
                      structuredDiagnostics && humanSurface && realScripts && isolatedEvidence &&
                      evidenceSourceUntouched;
        _areaLifecycleOverlayUx2LwB.Close();
        var evidenceResourcesReleased = _areaLifecycleOverlayUx2LwB.EvidenceResourcesReleased;
        var sourceSessionRestored = _areaLifecycleOverlayUx2LwB.SourceSessionRestored;

        var result = _session.RunUx2LwBContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _areaLifecycleOverlayUx2LwB.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _areaLifecycleOverlayUx2LwB.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureAreaLifecycleApprovalUx2LwB() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_B_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"events={bothEvents.ToString().ToLowerInvariant()} " +
                $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"real_scripts={realScripts.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }

        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_LW_B_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"real_scripts={result.RealScriptCatalog.ToString().ToLowerInvariant()} " +
            $"required_script={result.RequiredScriptBlocked.ToString().ToLowerInvariant()} " +
            $"missing_script={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"entered_observed={result.EnteredObserved.ToString().ToLowerInvariant()} " +
            $"exited_observed={result.ExitedObserved.ToString().ToLowerInvariant()} " +
            $"no_script_execution={result.NoScriptExecutionClaim.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"optional_removal={result.OptionalRemoval.ToString().ToLowerInvariant()} " +
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
