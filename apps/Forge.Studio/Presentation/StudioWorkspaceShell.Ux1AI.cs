using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerValidationOverlay _triggerValidationOverlay = null!;
    private Button _triggerValidationButton = null!;

    private void BuildUx1AIActions(HBoxContainer headingRow)
    {
        _triggerValidationButton = StudioStyle.Button("Validation", compact: true);
        _triggerValidationButton.TooltipText =
            "Contrôler les références, scripts, destinations et couches d’un déclencheur avec le validateur canonique.";
        _triggerValidationButton.Pressed += OpenTriggerValidationUx1AI;
        headingRow.AddChild(_triggerValidationButton);
    }

    private void InitializeUx1AIOverlay()
    {
        _triggerValidationOverlay = new StudioTriggerValidationOverlay(_session);
        _triggerValidationOverlay.StatusChanged += SetStatus;
        _triggerValidationOverlay.TriggerChanged += RefreshAfterUx1AIMutation;
        AddChild(_triggerValidationOverlay);

        _triggerFoundationButton.Pressed += CloseUx1AIOverlay;
        _triggerAdvancedButton.Pressed += CloseUx1AIOverlay;
        _triggerDiscoveryAccessButton.Pressed += CloseUx1AIOverlay;
    }

    private void OpenTriggerValidationUx1AI()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de valider un déclencheur.", error: true);
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
        CloseUx1YOverlay();
        CloseUx1ACOverlay();
        CloseUx1AEOverlay();
        SwitchPage("placement", notify: false);
        _triggerValidationOverlay.Open();
        SetStatus("Validation ouverte · sélection explicite · aperçu canonique obligatoire");
    }

    private void RefreshAfterUx1AIMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1AIActionState();
    }

    private void UpdateUx1AIActionState()
    {
        if (_triggerValidationButton is null)
        {
            return;
        }
        _triggerValidationButton.Visible = PageId == "placement" && _session.ActiveArea is not null;
    }

    private void CloseUx1AIOverlay()
    {
        if (_triggerValidationOverlay is not null)
        {
            _triggerValidationOverlay.Close();
        }
    }

    internal bool PrepareUx1AIEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _triggerValidationOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1AISmoke()
    {
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

        var uiTransaction = _triggerValidationOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1AIEvidence("ux1-ai-trigger-validation");
        var overlayOpen = _triggerValidationOverlay.IsOpen;
        var choiceCount = _triggerValidationOverlay.ChoiceCount;
        var explicitSelection = _triggerValidationOverlay.HasExplicitSelection;
        var canonicalPreview = _triggerValidationOverlay.CanonicalPreviewReady;
        var groupedDiagnostics = _triggerValidationOverlay.ShowsGroupedDiagnostics;
        var humanSurface = _triggerValidationOverlay.ShowsHumanSurface;
        var isolatedEvidence = _triggerValidationOverlay.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _triggerValidationOverlay.EvidenceSourceUntouched;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      overlayOpen &&
                      choiceCount >= 1 &&
                      explicitSelection &&
                      canonicalPreview &&
                      groupedDiagnostics &&
                      humanSurface &&
                      isolatedEvidence &&
                      evidenceSourceUntouched;
        _triggerValidationOverlay.Close();
        var evidenceResourcesReleased = _triggerValidationOverlay.EvidenceResourcesReleased;
        var sourceSessionRestored = _triggerValidationOverlay.SourceSessionRestored;
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);

        var result = _session.RunUx1AIContractSmoke();
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
                              _session.IsDirty == originalDirty;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AI_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"groups={groupedDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AI_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.SelectionExplicit.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"context_creation={result.ContextCreation.ToString().ToLowerInvariant()} " +
            $"required_flag={result.RequiredFlag.ToString().ToLowerInvariant()} " +
            $"runtime_resolution={result.RuntimeResolution.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_context={result.InvalidContextBlocked.ToString().ToLowerInvariant()} " +
            $"missing_target={result.MissingTargetBlocked.ToString().ToLowerInvariant()} " +
            $"missing_script={result.MissingScriptBlocked.ToString().ToLowerInvariant()} " +
            $"missing_portal={result.MissingPortalBlocked.ToString().ToLowerInvariant()} " +
            $"inaccessible={result.InaccessibleBlocked.ToString().ToLowerInvariant()} " +
            $"optional_suppression={result.OptionalSuppression.ToString().ToLowerInvariant()} " +
            $"grouped_diagnostics={result.GroupedDiagnostics.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"foundation_neighbors_preserved={result.FoundationNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"trigger_neighbors_preserved={result.TriggerNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
