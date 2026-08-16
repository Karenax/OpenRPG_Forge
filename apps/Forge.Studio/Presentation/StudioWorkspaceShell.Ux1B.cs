using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaDocumentOperationsOverlay _areaDocumentOperationsOverlay = null!;
    private Button _manageAreaButton = null!;

    private void BuildUx1BActions(HBoxContainer headingRow)
    {
        _manageAreaButton = StudioStyle.Button("Dimensions & copie", compact: true);
        _manageAreaButton.TooltipText =
            "Redimensionner ou copier la zone active · aperçu obligatoire · opération annulable.";
        _manageAreaButton.Pressed += OpenAreaDocumentResize;
        headingRow.AddChild(_manageAreaButton);
    }

    private void InitializeUx1BOverlay()
    {
        _areaDocumentOperationsOverlay = new StudioAreaDocumentOperationsOverlay(_session);
        _areaDocumentOperationsOverlay.StatusChanged += SetStatus;
        _areaDocumentOperationsOverlay.AreaChanged += RefreshAfterUx1BMutation;
        AddChild(_areaDocumentOperationsOverlay);
    }

    private void OpenAreaDocumentResize() => OpenAreaDocumentOperation(copy: false);

    private void OpenAreaDocumentClone() => OpenAreaDocumentOperation(copy: true);

    private void OpenAreaDocumentOperation(bool copy)
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de modifier son document.", error: true);
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
        _placeableBlueprintOverlay.Close();
        SwitchPage("zones", notify: false);
        if (copy)
        {
            _areaDocumentOperationsOverlay.OpenClone();
            SetStatus("Copie de zone ouverte · aperçu sans écriture · nouvelles identités internes");
        }
        else
        {
            _areaDocumentOperationsOverlay.OpenResize();
            SetStatus("Dimensions de zone ouvertes · objets hors limites contrôlés avant application");
        }
    }

    private void RefreshAfterUx1BMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1BActionState();
    }

    private void UpdateUx1BActionState()
    {
        if (_manageAreaButton is null)
        {
            return;
        }
        _manageAreaButton.Visible = PageId == "zones" && _session.ActiveArea is not null;
    }

    internal bool PrepareUx1BEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        return _areaDocumentOperationsOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1BSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var uiTransaction = _areaDocumentOperationsOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1BEvidence("ux1-b-resize") &&
                      PageId == "zones" &&
                      _areaDocumentOperationsOverlay.IsOpen &&
                      _areaDocumentOperationsOverlay.SectionCount == 2 &&
                      _areaDocumentOperationsOverlay.ShowsHumanSurface;
        _areaDocumentOperationsOverlay.Close();
        var result = _session.RunUx1BContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1B_{(valid ? "PASS" : "FAILED")} " +
            $"resize_preview={result.ResizePreview.ToString().ToLowerInvariant()} " +
            $"reposition_losses={result.RepositionLosses.ToString().ToLowerInvariant()} " +
            $"removal_preview={result.RemovalPreview.ToString().ToLowerInvariant()} " +
            $"resize_dry_run={result.ResizeDryRun.ToString().ToLowerInvariant()} " +
            $"resize_undo_redo={result.ResizeUndoRedo.ToString().ToLowerInvariant()} " +
            $"resize_persistence={result.ResizePersistence.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"clone_preview={result.ClonePreview.ToString().ToLowerInvariant()} " +
            $"full_clone={result.FullClone.ToString().ToLowerInvariant()} " +
            $"template_clone={result.TemplateClone.ToString().ToLowerInvariant()} " +
            $"portal_remap={result.PortalRemap.ToString().ToLowerInvariant()} " +
            $"new_identities={result.NewIdentities.ToString().ToLowerInvariant()} " +
            $"clone_undo_redo={result.CloneUndoRedo.ToString().ToLowerInvariant()} " +
            $"independent_document={result.IndependentDocument.ToString().ToLowerInvariant()} " +
            $"clone_persistence={result.ClonePersistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
