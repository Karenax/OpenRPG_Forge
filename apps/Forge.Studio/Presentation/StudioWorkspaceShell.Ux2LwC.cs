using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaUserEventsOverlayUx2LwC _areaUserEventsOverlayUx2LwC = null!;
    private Button _areaUserEventsButtonUx2LwC = null!;

    private void BuildUx2LwCActions(HBoxContainer headingRow)
    {
        _areaUserEventsButtonUx2LwC = StudioStyle.Button("Événement de zone", compact: true);
        _areaUserEventsButtonUx2LwC.TooltipText =
            "Décrire un payload typé et relier un handler ForgeScript réel par aperçu sec canonique.";
        _areaUserEventsButtonUx2LwC.Pressed += OpenAreaUserEventsUx2LwC;
        headingRow.AddChild(_areaUserEventsButtonUx2LwC);
    }

    private void InitializeUx2LwCOverlay()
    {
        _areaUserEventsOverlayUx2LwC = new StudioAreaUserEventsOverlayUx2LwC(_session);
        _areaUserEventsOverlayUx2LwC.StatusChanged += SetStatus;
        _areaUserEventsOverlayUx2LwC.AreaUserEventsChanged += RefreshAfterUx2LwCMutation;
        AddChild(_areaUserEventsOverlayUx2LwC);
    }

    private void OpenAreaUserEventsUx2LwC()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer un événement utilisateur.", error: true);
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
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        CloseUx4AOverlay();
        CloseUx5APage();
        CloseUx2LwBOverlay();
        SwitchPage("living-world", notify: false);
        _areaUserEventsOverlayUx2LwC.Open();
        SetStatus("Événement de zone ouvert · zone et événement explicites · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwCMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwCActionState();
    }

    private void UpdateUx2LwCActionState()
    {
        if (_areaUserEventsButtonUx2LwC is null)
        {
            return;
        }
        _areaUserEventsButtonUx2LwC.Visible = PageId == "living-world" &&
                                             _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwCOverlay()
    {
        if (_areaUserEventsOverlayUx2LwC is not null)
        {
            _areaUserEventsOverlayUx2LwC.Close();
        }
    }

    internal bool PrepareUx2LwCEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwBOverlay();
        CloseUx2LwDOverlay();
        CloseUx2LwEOverlay();
        CloseUx2LwFOverlay();
        CloseUx2LwGOverlay();
        CloseUx2LwHOverlay();
        CloseUx2LwIOverlay();
        return _areaUserEventsOverlayUx2LwC.PrepareEvidence(mode);
    }

    internal bool RunUx2LwCSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _areaUserEventsOverlayUx2LwC.IsOpen;
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
        var originalApproval = _session.CaptureAreaUserEventApprovalUx2LwC();

        var uiTransaction = _areaUserEventsOverlayUx2LwC.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwCEvidence("ux2-lw-c-area-user-events");
        var overlayOpen = _areaUserEventsOverlayUx2LwC.IsOpen;
        var choiceCount = _areaUserEventsOverlayUx2LwC.ChoiceCount;
        var explicitArea = _areaUserEventsOverlayUx2LwC.HasExplicitAreaSelection;
        var explicitEvent = _areaUserEventsOverlayUx2LwC.HasExplicitEventSelection;
        var canonicalPreview = _areaUserEventsOverlayUx2LwC.CanonicalPreviewReady;
        var typedPayload = _areaUserEventsOverlayUx2LwC.ShowsTypedPayload;
        var honestBoundary = _areaUserEventsOverlayUx2LwC.ShowsNoExecutionBoundary;
        var structuredDiagnostics = _areaUserEventsOverlayUx2LwC.ShowsStructuredDiagnostics;
        var humanSurface = _areaUserEventsOverlayUx2LwC.ShowsHumanSurface;
        var realScripts = _areaUserEventsOverlayUx2LwC.UsesRealScriptCatalog;
        var isolatedEvidence = _areaUserEventsOverlayUx2LwC.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _areaUserEventsOverlayUx2LwC.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitArea && explicitEvent && canonicalPreview && typedPayload && honestBoundary &&
                      structuredDiagnostics && humanSurface && realScripts && isolatedEvidence &&
                      evidenceSourceUntouched;
        _areaUserEventsOverlayUx2LwC.Close();
        var evidenceResourcesReleased = _areaUserEventsOverlayUx2LwC.EvidenceResourcesReleased;
        var sourceSessionRestored = _areaUserEventsOverlayUx2LwC.SourceSessionRestored;

        var result = _session.RunUx2LwCContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _areaUserEventsOverlayUx2LwC.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _areaUserEventsOverlayUx2LwC.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureAreaUserEventApprovalUx2LwC() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_C_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"area={explicitArea.ToString().ToLowerInvariant()} " +
                $"event={explicitEvent.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"payload={typedPayload.ToString().ToLowerInvariant()} " +
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
            $"OPENRPG_FORGE_STUDIO_UX2_LW_C_{(valid ? "PASS" : "FAILED")} " +
            $"area_explicit={result.ExplicitAreaSelection.ToString().ToLowerInvariant()} " +
            $"event_explicit={result.ExplicitEventSelection.ToString().ToLowerInvariant()} " +
            $"real_scripts={result.RealScriptCatalog.ToString().ToLowerInvariant()} " +
            $"invalid_payload={result.InvalidPayloadBlocked.ToString().ToLowerInvariant()} " +
            $"missing_script={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"missing_handler={result.MissingHandlerBlocked.ToString().ToLowerInvariant()} " +
            $"legacy_blocked={result.LegacyDispatchBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"compiled_no_execution={result.CompiledWithoutExecution.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"collection_preserved={result.CollectionPreserved.ToString().ToLowerInvariant()} " +
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
