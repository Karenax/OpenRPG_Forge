using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _assistedAreaPlanButton = null!;
    private StudioAssistedAreaPlanWorkspace _assistedAreaPlanWorkspace = null!;

    private void BuildUx1AFActions(HBoxContainer headingRow)
    {
        _assistedAreaPlanButton = StudioStyle.Button("Plan assisté", compact: true);
        _assistedAreaPlanButton.TooltipText =
            "Créer un plan de zone gouverné, l’approuver explicitement puis prévisualiser sa construction atomique.";
        _assistedAreaPlanButton.Pressed += OpenUx1AFAssistedAreaPlan;
        headingRow.AddChild(_assistedAreaPlanButton);
    }

    private void InitializeUx1AFWorkspace()
    {
        _assistedAreaPlanWorkspace = new StudioAssistedAreaPlanWorkspace(_session);
        _assistedAreaPlanWorkspace.StatusChanged += SetStatus;
        _assistedAreaPlanWorkspace.WorkspaceChanged += RefreshAfterUx1AFMutation;
        AddChild(_assistedAreaPlanWorkspace);
    }

    private void OpenUx1AFAssistedAreaPlan()
    {
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
        _assistedAreaPlanWorkspace.Open();
        SetStatus(
            "Plans assistés ouverts · sélection explicite · confidentialité visible · dry-run obligatoire");
    }

    private void RefreshAfterUx1AFMutation()
    {
        PopulateAreas();
        if (_session.ActiveArea is null)
        {
            WorldViewport.ShowNoArea(_session);
        }
        else
        {
            WorldViewport.Rebuild(_session, frameArea: false);
        }
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        UpdateUx1AFActionState();
        NavigationChanged?.Invoke();
    }

    private void UpdateUx1AFActionState()
    {
        if (_assistedAreaPlanButton is null)
        {
            return;
        }

        var project = _session.Project;
        _assistedAreaPlanButton.Visible = PageId is "zones" or "plans" &&
                                          project is not null && project.Areas.Count > 0 &&
                                          project.Blueprints.Any(blueprint =>
                                              blueprint.Kind == BlueprintKind.Placeable);
    }

    private void CloseUx1AFWorkspace()
    {
        if (_assistedAreaPlanWorkspace is not null)
        {
            _assistedAreaPlanWorkspace.Close();
        }
    }

    internal bool PrepareUx1AFEvidence(string mode)
    {
        SwitchPage("plans", notify: false);
        return _assistedAreaPlanWorkspace.PrepareEvidence(mode);
    }

    internal bool RunUx1AFSmoke()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalDirty = _session.IsDirty;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var uiTransaction = _assistedAreaPlanWorkspace.RunUiTransactionEvidence();
        var uiTransactionSourceUntouched = _assistedAreaPlanWorkspace.EvidenceSourceUntouched;
        var uiTransactionResourcesReleased = _assistedAreaPlanWorkspace.EvidenceResourcesReleased;
        var uiTransactionSessionRestored = _assistedAreaPlanWorkspace.SourceSessionRestored;

        var evidencePrepared = PrepareUx1AFEvidence("ux1-af-assisted-area-plan");
        var uiReady = evidencePrepared && PageId == "plans" &&
                      _assistedAreaPlanWorkspace.IsOpen &&
                      _assistedAreaPlanWorkspace.UsesIsolatedEvidence &&
                      _assistedAreaPlanWorkspace.HasExplicitSelection &&
                      _assistedAreaPlanWorkspace.PlanPreviewReady &&
                      _assistedAreaPlanWorkspace.BuildPreviewReady &&
                      _assistedAreaPlanWorkspace.SavedPlanCount >= 1 &&
                      _assistedAreaPlanWorkspace.ShowsHumanSurface &&
                      _assistedAreaPlanWorkspace.EvidenceSourceUntouched;
        _assistedAreaPlanWorkspace.Close();
        var evidenceResourcesReleased = _assistedAreaPlanWorkspace.EvidenceResourcesReleased;
        var evidenceSourceUntouched = _assistedAreaPlanWorkspace.EvidenceSourceUntouched;
        var sourceSessionRestored = _assistedAreaPlanWorkspace.SourceSessionRestored;

        var result = _session.RunUx1AFContractSmoke();
        var currentHistory = _session.History;
        var sourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                              originalHistory is not null && currentHistory is not null &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory.State.Revision == originalRevision &&
                              currentHistory.Journal.SequenceEqual(originalJournal) &&
                              currentHistory.CanUndo == originalCanUndo &&
                              currentHistory.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var valid = result.Succeeded && result.ResourcesReleased && uiTransaction && uiReady &&
                    uiTransactionSourceUntouched && uiTransactionResourcesReleased &&
                    uiTransactionSessionRestored && evidenceSourceUntouched && evidenceResourcesReleased &&
                    sourceSessionRestored && sourceUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AF_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"privacy={result.PrivacyVisible.ToString().ToLowerInvariant()} " +
            $"provenance={result.ProvenanceVisible.ToString().ToLowerInvariant()} " +
            $"plan_dry_run={result.PlanDryRunPure.ToString().ToLowerInvariant()} " +
            $"plan_persisted={result.PlanApprovalPersisted.ToString().ToLowerInvariant()} " +
            $"plan_history_separate={result.PlanHistorySeparated.ToString().ToLowerInvariant()} " +
            $"plan_undo_redo={result.PlanUndoRedo.ToString().ToLowerInvariant()} " +
            $"plan_revision_guard={result.PlanRevisionGuard.ToString().ToLowerInvariant()} " +
            $"project_revision_guard={result.ProjectRevisionGuard.ToString().ToLowerInvariant()} " +
            $"build_dry_run={result.BuildDryRunPure.ToString().ToLowerInvariant()} " +
            $"geometry_applied={result.BuildAppliedGeometry.ToString().ToLowerInvariant()} " +
            $"build_history_separate={result.BuildHistorySeparated.ToString().ToLowerInvariant()} " +
            $"build_undo_redo={result.BuildUndoRedo.ToString().ToLowerInvariant()} " +
            $"build_revision_guard={result.BuildRevisionGuard.ToString().ToLowerInvariant()} " +
            $"save_reopen={result.SaveReopen.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"source_untouched={sourceUntouched.ToString().ToLowerInvariant()} " +
            $"resources_released={(result.ResourcesReleased && evidenceResourcesReleased).ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
