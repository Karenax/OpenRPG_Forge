using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioZoneDynamicOperationsOverlay _zoneDynamicOperationsOverlay = null!;
    private Button _zoneDynamicOperationsButton = null!;

    private void BuildUx1WActions(HBoxContainer headingRow)
    {
        _zoneDynamicOperationsButton = StudioStyle.Button("Échelle & runtime", compact: true);
        _zoneDynamicOperationsButton.TooltipText =
            "Mettre la sélection à l’échelle, configurer les zones dynamiques ou simuler une copie runtime.";
        _zoneDynamicOperationsButton.Pressed += OpenZoneDynamicOperations;
        headingRow.AddChild(_zoneDynamicOperationsButton);
    }

    private void InitializeUx1WOverlay()
    {
        _zoneDynamicOperationsOverlay = new StudioZoneDynamicOperationsOverlay(_session);
        _zoneDynamicOperationsOverlay.StatusChanged += SetStatus;
        _zoneDynamicOperationsOverlay.ZoneChanged += RefreshAfterUx1WMutation;
        AddChild(_zoneDynamicOperationsOverlay);
    }

    private void OpenZoneDynamicOperations()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant d’ouvrir les outils dynamiques.", error: true);
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
        SwitchPage("zones", notify: false);
        _zoneDynamicOperationsOverlay.OpenScale();
        SetStatus("Outils de zone ouverts · aperçu obligatoire avant chaque application");
    }

    private void RefreshAfterUx1WMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1WActionState();
    }

    private void UpdateUx1WActionState()
    {
        if (_zoneDynamicOperationsButton is null)
        {
            return;
        }

        _zoneDynamicOperationsButton.Visible = PageId == "zones" && _session.ActiveArea is not null;
    }

    private void CloseUx1WOverlay()
    {
        if (_zoneDynamicOperationsOverlay is not null)
        {
            _zoneDynamicOperationsOverlay.Close();
        }
    }

    internal bool PrepareUx1WEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        return _zoneDynamicOperationsOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1WSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _zoneDynamicOperationsOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1WEvidence("ux1-w-dynamic") &&
                      PageId == "zones" &&
                      _zoneDynamicOperationsOverlay.IsOpen &&
                      _zoneDynamicOperationsOverlay.SectionCount == 3 &&
                      _zoneDynamicOperationsOverlay.ShowsHumanSurface;
        _zoneDynamicOperationsOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1WContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1W_{(valid ? "PASS" : "FAILED")} " +
            $"scale_multi={result.ScaleMultiEdit.ToString().ToLowerInvariant()} " +
            $"scale_constraints={result.ScaleConstraints.ToString().ToLowerInvariant()} " +
            $"scale_dry_run={result.ScaleDryRun.ToString().ToLowerInvariant()} " +
            $"dynamic_preview={result.DynamicAreaPreview.ToString().ToLowerInvariant()} " +
            $"dynamic_quota={result.DynamicQuota.ToString().ToLowerInvariant()} " +
            $"runtime_copy={result.RuntimeCopyPreview.ToString().ToLowerInvariant()} " +
            $"fresh_ids={result.FreshIdentities.ToString().ToLowerInvariant()} " +
            $"ownership_refs_state={result.OwnershipReferencesState.ToString().ToLowerInvariant()} " +
            $"canonical_commands={result.CanonicalCommands.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"preservation={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
