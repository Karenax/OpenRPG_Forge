using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaCombatPoliciesOverlayUx2LwD _areaCombatOverlayUx2LwD = null!;
    private Button _areaCombatButtonUx2LwD = null!;

    private void BuildUx2LwDActions(HBoxContainer headingRow)
    {
        _areaCombatButtonUx2LwD = StudioStyle.Button("Combat de zone", compact: true);
        _areaCombatButtonUx2LwD.TooltipText =
            "Configurer la politique Combat et projeter honnêtement l’autorité runtime sans simuler de dégât.";
        _areaCombatButtonUx2LwD.Pressed += OpenAreaCombatUx2LwD;
        headingRow.AddChild(_areaCombatButtonUx2LwD);
    }

    private void InitializeUx2LwDOverlay()
    {
        _areaCombatOverlayUx2LwD = new StudioAreaCombatPoliciesOverlayUx2LwD(_session);
        _areaCombatOverlayUx2LwD.StatusChanged += SetStatus;
        _areaCombatOverlayUx2LwD.AreaCombatChanged += RefreshAfterUx2LwDMutation;
        AddChild(_areaCombatOverlayUx2LwD);
    }

    private void OpenAreaCombatUx2LwD()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant de configurer son combat.", error: true);
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
        CloseUx4AOverlay();
        CloseUx5APage();
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwEOverlay();
        SwitchPage("living-world", notify: false);
        _areaCombatOverlayUx2LwD.Open();
        SetStatus("Combat de zone ouvert · zone explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2LwDMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2LwDActionState();
    }

    private void UpdateUx2LwDActionState()
    {
        if (_areaCombatButtonUx2LwD is null)
            return;
        _areaCombatButtonUx2LwD.Visible = PageId == "living-world" &&
                                         _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2LwDOverlay()
    {
        if (_areaCombatOverlayUx2LwD is not null)
            _areaCombatOverlayUx2LwD.Close();
    }

    internal bool PrepareUx2LwDEvidence(string mode)
    {
        SwitchPage("living-world", notify: false);
        CloseUx2AOverlay();
        CloseUx2LwBOverlay();
        CloseUx2LwCOverlay();
        CloseUx2LwEOverlay();
        return _areaCombatOverlayUx2LwD.PrepareEvidence(mode);
    }

    internal bool RunUx2LwDSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _areaCombatOverlayUx2LwD.IsOpen;
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
        var originalApproval = _session.CaptureAreaCombatApprovalUx2LwD();

        var uiTransaction = _areaCombatOverlayUx2LwD.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx2LwDEvidence("ux2-lw-d-area-combat-authority");
        var overlayOpen = _areaCombatOverlayUx2LwD.IsOpen;
        var choiceCount = _areaCombatOverlayUx2LwD.ChoiceCount;
        var explicitSelection = _areaCombatOverlayUx2LwD.HasExplicitSelection;
        var canonicalPreview = _areaCombatOverlayUx2LwD.CanonicalPreviewReady;
        var completePolicy = _areaCombatOverlayUx2LwD.ShowsCompleteCombatPolicy;
        var honestBoundary = _areaCombatOverlayUx2LwD.ShowsNoSimulationBoundary;
        var serverAuthority = _areaCombatOverlayUx2LwD.ShowsServerAuthority;
        var diagnostics = _areaCombatOverlayUx2LwD.ShowsStructuredDiagnostics;
        var human = _areaCombatOverlayUx2LwD.ShowsHumanSurface;
        var isolated = _areaCombatOverlayUx2LwD.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _areaCombatOverlayUx2LwD.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "living-world" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && completePolicy && honestBoundary &&
                      serverAuthority && diagnostics && human && isolated && evidenceSourceUntouched;
        _areaCombatOverlayUx2LwD.Close();
        var evidenceReleased = _areaCombatOverlayUx2LwD.EvidenceResourcesReleased;
        var sourceSessionRestored = _areaCombatOverlayUx2LwD.SourceSessionRestored;

        var result = _session.RunUx2LwDContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
            _areaCombatOverlayUx2LwD.RestoreSourceSurfaceAfterSmoke();
        var surfaceRestored = PageId == originalPageId &&
                              _areaCombatOverlayUx2LwD.IsOpen == originalSurfaceOpen;
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
                              _session.CaptureAreaCombatApprovalUx2LwD() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_LW_D_UI_SURFACE_FAILED " +
                $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"complete_policy={completePolicy.ToString().ToLowerInvariant()} " +
                $"honest_boundary={honestBoundary.ToString().ToLowerInvariant()} " +
                $"server_authority={serverAuthority.ToString().ToLowerInvariant()} " +
                $"diagnostics={diagnostics.ToString().ToLowerInvariant()} " +
                $"human={human.ToString().ToLowerInvariant()} " +
                $"isolated={isolated.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }

        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged && surfaceRestored &&
                    evidenceReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_LW_D_{(valid ? "PASS" : "FAILED")} " +
            $"selection={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
            $"server_diagnostic={result.ServerAuthorityDiagnostic.ToString().ToLowerInvariant()} " +
            $"aggro_policy={result.AggroPolicyApplied.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"target_neighbors={result.TargetNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"heartbeats={result.HeartbeatsPreserved.ToString().ToLowerInvariant()} " +
            $"rest={result.RestPreserved.ToString().ToLowerInvariant()} " +
            $"cinematics={result.CinematicsPreserved.ToString().ToLowerInvariant()} " +
            $"perception={result.PerceptionPreserved.ToString().ToLowerInvariant()} " +
            $"experience={result.ExperiencePreserved.ToString().ToLowerInvariant()} " +
            $"structured_diagnostics={result.StructuredDiagnostics.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolated.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
