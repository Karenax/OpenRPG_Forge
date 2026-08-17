using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorValidationOverlay _doorValidationOverlay = null!;
    private Button _doorValidationButton = null!;

    private void BuildUx1AJActions(HBoxContainer headingRow)
    {
        _doorValidationButton = StudioStyle.Button("Validation porte", compact: true);
        _doorValidationButton.TooltipText =
            "Qualifier les destinations, animations, collisions, accès et scripts d’une porte avec le validateur canonique.";
        _doorValidationButton.Pressed += OpenDoorValidationUx1AJ;
        headingRow.AddChild(_doorValidationButton);
    }

    private void InitializeUx1AJOverlay()
    {
        _doorValidationOverlay = new StudioDoorValidationOverlay(_session);
        _doorValidationOverlay.StatusChanged += SetStatus;
        _doorValidationOverlay.DoorChanged += RefreshAfterUx1AJMutation;
        AddChild(_doorValidationOverlay);
    }

    private void OpenDoorValidationUx1AJ()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de qualifier une porte.", error: true);
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
        SwitchPage("placement", notify: false);
        _doorValidationOverlay.Open();
        SetStatus("Validation porte ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx1AJMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1AJActionState();
    }

    private void UpdateUx1AJActionState()
    {
        if (_doorValidationButton is null)
        {
            return;
        }
        _doorValidationButton.Visible = PageId == "placement" &&
                                        _session.Project?.Blueprints.Any(blueprint =>
                                            blueprint.Kind ==
                                            Forge.Domain.Blueprints.BlueprintKind.Door) == true;
    }

    private void CloseUx1AJOverlay()
    {
        if (_doorValidationOverlay is not null)
        {
            _doorValidationOverlay.Close();
        }
    }

    internal bool PrepareUx1AJEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _doorValidationOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1AJSmoke()
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
        var originalApproval = _session.CaptureDoorValidationApprovalUx1AJ();

        var uiTransaction = _doorValidationOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1AJEvidence("ux1-aj-door-validation");
        var overlayOpen = _doorValidationOverlay.IsOpen;
        var choiceCount = _doorValidationOverlay.ChoiceCount;
        var explicitSelection = _doorValidationOverlay.HasExplicitSelection;
        var canonicalPreview = _doorValidationOverlay.CanonicalPreviewReady;
        var groupedDiagnostics = _doorValidationOverlay.ShowsGroupedDiagnostics;
        var humanSurface = _doorValidationOverlay.ShowsHumanSurface;
        var suggestions = _doorValidationOverlay.SuggestionsReady;
        var protectedDestinations = _doorValidationOverlay.ProtectedDestinations;
        var isolatedEvidence = _doorValidationOverlay.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _doorValidationOverlay.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "placement" && overlayOpen && choiceCount >= 1 &&
                      explicitSelection && canonicalPreview && groupedDiagnostics && humanSurface && suggestions &&
                      protectedDestinations && isolatedEvidence && evidenceSourceUntouched;
        _doorValidationOverlay.Close();
        var evidenceResourcesReleased = _doorValidationOverlay.EvidenceResourcesReleased;
        var sourceSessionRestored = _doorValidationOverlay.SourceSessionRestored;
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);

        var result = _session.RunUx1AJContractSmoke();
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
                              _session.CaptureDoorValidationApprovalUx1AJ() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AJ_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"groups={groupedDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"suggestions={suggestions.ToString().ToLowerInvariant()} " +
                $"protected_destinations={protectedDestinations.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AJ_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"prerequisites={result.PrerequisitesRequired.ToString().ToLowerInvariant()} " +
            $"human_suggestions={result.HumanSuggestions.ToString().ToLowerInvariant()} " +
            $"protected_destinations={result.ProtectedDestinations.ToString().ToLowerInvariant()} " +
            $"optional_not_evaluated={result.OptionalNotEvaluated.ToString().ToLowerInvariant()} " +
            $"required_absent={result.RequiredAbsentBlocked.ToString().ToLowerInvariant()} " +
            $"missing_destination={result.DestinationMissingBlocked.ToString().ToLowerInvariant()} " +
            $"missing_animation={result.AnimationMissingBlocked.ToString().ToLowerInvariant()} " +
            $"missing_rejection_animation={result.RejectionAnimationBlocked.ToString().ToLowerInvariant()} " +
            $"collision_authored_missing={result.CollisionAuthoredMissingBlocked.ToString().ToLowerInvariant()} " +
            $"collision_mismatch={result.CollisionMismatchBlocked.ToString().ToLowerInvariant()} " +
            $"missing_collision={result.CollisionMissingBlocked.ToString().ToLowerInvariant()} " +
            $"missing_credential={result.CredentialMissingBlocked.ToString().ToLowerInvariant()} " +
            $"missing_script={result.ScriptMissingBlocked.ToString().ToLowerInvariant()} " +
            $"orphan_link={result.OrphanLinkBlocked.ToString().ToLowerInvariant()} " +
            $"reverse_link={result.ReverseLinkBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"optional_suppression={result.OptionalSuppression.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"door_neighbors={result.DoorNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"catalog_neighbors={result.CatalogNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"structured_human={result.StructuredHumanDiagnostics.ToString().ToLowerInvariant()} " +
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
