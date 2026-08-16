using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _assemblyValidationButton = null!;

    private void BuildUx1AAActions(HBoxContainer headingRow)
    {
        _assemblyValidationButton = StudioStyle.Button("Assemblage & validation", compact: true);
        _assemblyValidationButton.TooltipText =
            "Configurer sockets, assemblages, actions contextuelles et validation d’un placeable.";
        _assemblyValidationButton.Pressed += OpenAssemblyValidation;
        headingRow.AddChild(_assemblyValidationButton);
    }

    private void OpenAssemblyValidation()
    {
        OpenPlaceableBlueprint(staticFocus: false);
        _placeableBlueprintOverlay.OpenAssemblyValidation();
        SetStatus(
            "Assemblage et validation ouverts · sélection explicite et aperçu obligatoire avant application");
    }

    private void UpdateUx1AAActionState()
    {
        if (_assemblyValidationButton is null)
        {
            return;
        }

        _assemblyValidationButton.Visible = PageId == "placement" &&
                                            _session.Project?.Blueprints.Any(blueprint =>
                                                blueprint.Kind == BlueprintKind.Placeable) == true;
    }

    private void CloseUx1AAOverlay()
    {
        if (_placeableBlueprintOverlay is not null && _placeableBlueprintOverlay.AssemblyValidationOpen)
        {
            _placeableBlueprintOverlay.CloseAssemblyValidation();
        }
    }

    internal bool PrepareUx1AAEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareAssemblyValidationEvidence(mode);
    }

    internal bool RunUx1AASmoke()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalDirty = _session.IsDirty;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var fallbackExpected = !_session.CreateUx1AACatalog().Placeables.Any(option => option.Ready);

        var uiTransaction = _placeableBlueprintOverlay.RunAssemblyValidationUiTransactionEvidence();
        var uiTransactionSourceUntouched =
            _placeableBlueprintOverlay.AssemblyValidationEvidenceSourceUntouched;
        var uiTransactionResourcesReleased =
            _placeableBlueprintOverlay.AssemblyValidationEvidenceResourcesReleased;

        var evidencePrepared = PrepareUx1AAEvidence("ux1-aa-validation");
        var isolatedFallback = _placeableBlueprintOverlay.AssemblyValidationUsesIsolatedEvidence;
        var evidenceSourceUntouched =
            _placeableBlueprintOverlay.AssemblyValidationEvidenceSourceUntouched;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.AssemblyValidationOpen &&
                      _placeableBlueprintOverlay.AssemblyValidationPlaceableCount >= 1 &&
                      _placeableBlueprintOverlay.AssemblyValidationSectionCount == 3 &&
                      _placeableBlueprintOverlay.AssemblyValidationHasExplicitSelection &&
                      _placeableBlueprintOverlay.AssemblyValidationPreviewReady &&
                      _placeableBlueprintOverlay.AssemblyValidationShowsHumanSurface &&
                      isolatedFallback == fallbackExpected &&
                      evidenceSourceUntouched;
        _placeableBlueprintOverlay.Close();
        var evidenceResourcesReleased =
            _placeableBlueprintOverlay.AssemblyValidationEvidenceResourcesReleased;

        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1AAContractSmoke();
        var currentHistory = _session.History;
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      _session.IsDirty == originalDirty &&
                                      originalHistory is not null &&
                                      currentHistory is not null &&
                                      ReferenceEquals(originalHistory, currentHistory) &&
                                      currentHistory.State.Revision == originalRevision &&
                                      currentHistory.CanUndo == originalCanUndo &&
                                      currentHistory.CanRedo == originalCanRedo &&
                                      currentHistory.Journal.SequenceEqual(originalJournal);
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    uiTransactionSourceUntouched && uiTransactionResourcesReleased &&
                    evidenceResourcesReleased && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AA_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"prerequisites_refused={result.PrerequisitesRefused.ToString().ToLowerInvariant()} " +
            $"assembly={result.AssemblyPreview.ToString().ToLowerInvariant()} " +
            $"multi_verb={result.MultiVerbPreview.ToString().ToLowerInvariant()} " +
            $"validation={result.ValidationPreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"command_parity={result.CommandParity.ToString().ToLowerInvariant()} " +
            $"invalid_socket={result.InvalidSocketBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_attachment={result.InvalidAttachmentBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_verb={result.InvalidVerbBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_validation={result.InvalidValidationBlocked.ToString().ToLowerInvariant()} " +
            $"stale_revision_blocked={result.StalePreviewBlocked.ToString().ToLowerInvariant()} " +
            $"damage_authority_preserved={result.DamageAuthorityPreserved.ToString().ToLowerInvariant()} " +
            $"capabilities_preserved={result.NeighborCapabilitiesPreserved.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPlaceablePreserved.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"fallback_expected={fallbackExpected.ToString().ToLowerInvariant()} " +
            $"isolated_fallback={isolatedFallback.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"ui_transaction_isolated={uiTransactionSourceUntouched.ToString().ToLowerInvariant()} " +
            $"ui_transaction_released={uiTransactionResourcesReleased.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
