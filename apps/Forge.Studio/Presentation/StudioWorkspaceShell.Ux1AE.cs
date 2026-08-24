using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerDiscoveryAccessOverlay _triggerDiscoveryAccessOverlay = null!;
    private Button _triggerDiscoveryAccessButton = null!;

    private void BuildUx1AEActions(HBoxContainer headingRow)
    {
        _triggerDiscoveryAccessButton = StudioStyle.Button("Découverte & accès", compact: true);
        _triggerDiscoveryAccessButton.TooltipText =
            "Configurer secret, cible révélée, clic, présentation, permission et justificatif sans modifier le volume de base.";
        _triggerDiscoveryAccessButton.Pressed += OpenTriggerDiscoveryAccessUx1AE;
        headingRow.AddChild(_triggerDiscoveryAccessButton);
    }

    private void InitializeUx1AEOverlay()
    {
        _triggerDiscoveryAccessOverlay = new StudioTriggerDiscoveryAccessOverlay(_session);
        _triggerDiscoveryAccessOverlay.StatusChanged += SetStatus;
        _triggerDiscoveryAccessOverlay.TriggerChanged += RefreshAfterUx1AEMutation;
        AddChild(_triggerDiscoveryAccessOverlay);

        _triggerFoundationButton.Pressed += CloseUx1AEOverlay;
        _triggerAdvancedButton.Pressed += CloseUx1AEOverlay;
    }

    private void OpenTriggerDiscoveryAccessUx1AE()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de configurer découverte et accès.", error: true);
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
        SwitchPage("placement", notify: false);
        _triggerDiscoveryAccessOverlay.Open();
        SetStatus("Découverte & accès ouvert · sélection explicite · aperçu obligatoire avant application");
    }

    private void RefreshAfterUx1AEMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1AEActionState();
    }

    private void UpdateUx1AEActionState()
    {
        if (_triggerDiscoveryAccessButton is null)
        {
            return;
        }
        _triggerDiscoveryAccessButton.Visible = PageId == "placement" && _session.ActiveArea is not null;
    }

    private void CloseUx1AEOverlay()
    {
        if (_triggerDiscoveryAccessOverlay is not null)
        {
            _triggerDiscoveryAccessOverlay.Close();
        }
    }

    internal bool PrepareUx1AEEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _triggerDiscoveryAccessOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1AESmoke()
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

        var uiTransaction = _triggerDiscoveryAccessOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1AEEvidence("ux1-ae-discovery-access");
        var overlayOpen = _triggerDiscoveryAccessOverlay.IsOpen;
        var sectionCount = _triggerDiscoveryAccessOverlay.SectionCount;
        var choiceCount = _triggerDiscoveryAccessOverlay.ChoiceCount;
        var explicitSelection = _triggerDiscoveryAccessOverlay.HasExplicitSelection;
        var runtimePreview = _triggerDiscoveryAccessOverlay.RuntimePreviewReady;
        var humanSurface = _triggerDiscoveryAccessOverlay.ShowsHumanSurface;
        var isolatedEvidence = _triggerDiscoveryAccessOverlay.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _triggerDiscoveryAccessOverlay.EvidenceSourceUntouched;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      overlayOpen &&
                      sectionCount == 3 &&
                      choiceCount >= 1 &&
                      explicitSelection &&
                      runtimePreview &&
                      humanSurface &&
                      isolatedEvidence &&
                      evidenceSourceUntouched;
        _triggerDiscoveryAccessOverlay.Close();
        var evidenceResourcesReleased = _triggerDiscoveryAccessOverlay.EvidenceResourcesReleased;
        var sourceSessionRestored = _triggerDiscoveryAccessOverlay.SourceSessionRestored;
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);

        var result = _session.RunUx1AEContractSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.IsDirty == originalDirty;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AE_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} sections={sectionCount} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"runtime_preview={runtimePreview.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && sourceUnchanged &&
                    evidenceResourcesReleased && sourceSessionRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AE_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.SelectionExplicit.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"discovery={result.Discovery.ToString().ToLowerInvariant()} " +
            $"interaction={result.Interaction.ToString().ToLowerInvariant()} " +
            $"access={result.Access.ToString().ToLowerInvariant()} " +
            $"runtime_preview={result.RuntimePreview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_discovery={result.InvalidDiscoveryBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_interaction={result.InvalidInteractionBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_access={result.InvalidAccessBlocked.ToString().ToLowerInvariant()} " +
            $"validation_script_blocked={result.ValidationScriptBlocked.ToString().ToLowerInvariant()} " +
            $"stale_draft_blocked={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval_blocked={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"foundation_preserved={result.FoundationPreserved.ToString().ToLowerInvariant()} " +
            $"portal_preserved={result.PortalPreserved.ToString().ToLowerInvariant()} " +
            $"validation_preserved={result.ValidationPreserved.ToString().ToLowerInvariant()} " +
            $"advanced_preserved={result.AdvancedPreserved.ToString().ToLowerInvariant()} " +
            $"ux2_neighbors_preserved={result.Ux2NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
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
