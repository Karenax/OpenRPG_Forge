using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerAdvancedOverlay _triggerAdvancedOverlay = null!;
    private Button _triggerAdvancedButton = null!;

    private void BuildUx1ACActions(HBoxContainer headingRow)
    {
        _triggerAdvancedButton = StudioStyle.Button("Piège & volumes", compact: true);
        _triggerAdvancedButton.TooltipText =
            "Configurer le trigger de piège, le filtre de cible, les dix volumes de gameplay et la visualisation d’occupation.";
        _triggerAdvancedButton.Pressed += OpenTriggerAdvancedUx1AC;
        headingRow.AddChild(_triggerAdvancedButton);
    }

    private void InitializeUx1ACOverlay()
    {
        _triggerAdvancedOverlay = new StudioTriggerAdvancedOverlay(_session);
        _triggerAdvancedOverlay.StatusChanged += SetStatus;
        _triggerAdvancedOverlay.TriggerChanged += RefreshAfterUx1ACMutation;
        AddChild(_triggerAdvancedOverlay);
    }

    private void OpenTriggerAdvancedUx1AC()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de configurer un trigger avancé.", error: true);
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
        SwitchPage("placement", notify: false);
        _triggerAdvancedOverlay.Open();
        SetStatus("Trigger avancé ouvert · sélection explicite · aperçu obligatoire avant application");
    }

    private void RefreshAfterUx1ACMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1ACActionState();
    }

    private void UpdateUx1ACActionState()
    {
        if (_triggerAdvancedButton is null)
        {
            return;
        }
        _triggerAdvancedButton.Visible = PageId == "placement" && _session.ActiveArea is not null;
    }

    private void CloseUx1ACOverlay()
    {
        if (_triggerAdvancedOverlay is not null)
        {
            _triggerAdvancedOverlay.Close();
        }
    }

    internal bool PrepareUx1ACEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _triggerAdvancedOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1ACSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var uiTransaction = _triggerAdvancedOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1ACEvidence("ux1-ac-trigger-advanced");
        var overlayOpen = _triggerAdvancedOverlay.IsOpen;
        var sectionCount = _triggerAdvancedOverlay.SectionCount;
        var choiceCount = _triggerAdvancedOverlay.ChoiceCount;
        var volumeCount = _triggerAdvancedOverlay.VolumeCount;
        var explicitSelection = _triggerAdvancedOverlay.HasExplicitSelection;
        var typedBindings = _triggerAdvancedOverlay.HasTypedBindings;
        var humanSurface = _triggerAdvancedOverlay.ShowsHumanSurface;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      overlayOpen &&
                      sectionCount == 3 &&
                      choiceCount >= 1 &&
                      volumeCount == 10 &&
                      explicitSelection &&
                      typedBindings &&
                      humanSurface;
        _triggerAdvancedOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1ACContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      _session.IsDirty == originalDirty;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AC_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} sections={sectionCount} choices={choiceCount} " +
                $"volumes={volumeCount} selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"typed={typedBindings.ToString().ToLowerInvariant()} human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1AC_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.SelectionExplicit.ToString().ToLowerInvariant()} " +
            $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
            $"advanced_creation={result.AdvancedCreation.ToString().ToLowerInvariant()} " +
            $"trap={result.Trap.ToString().ToLowerInvariant()} " +
            $"target_filter={result.TargetFilter.ToString().ToLowerInvariant()} " +
            $"gameplay_volumes={result.GameplayVolumes.ToString().ToLowerInvariant()} " +
            $"occupancy_debug={result.OccupancyDebug.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_trap={result.InvalidTrapBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_filter={result.InvalidFilterBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_volume={result.InvalidVolumeBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_debug={result.InvalidDebugBlocked.ToString().ToLowerInvariant()} " +
            $"stale_draft_blocked={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval_blocked={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"runtime_adapter={result.RuntimeAdapter.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"foundation_preserved={result.FoundationPreserved.ToString().ToLowerInvariant()} " +
            $"portal_preserved={result.PortalPreserved.ToString().ToLowerInvariant()} " +
            $"validation_preserved={result.ValidationPreserved.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
