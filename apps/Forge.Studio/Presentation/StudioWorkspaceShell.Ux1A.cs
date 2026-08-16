using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaCreationOverlay _areaCreationOverlay = null!;
    private Button _newAreaButton = null!;

    private void BuildUx1AActions(HBoxContainer headingRow)
    {
        _newAreaButton = StudioStyle.Button("Nouvelle zone", compact: true);
        _newAreaButton.TooltipText =
            "Ouvrir l’assistant intégré · aperçu obligatoire · création annulable avant enregistrement.";
        _newAreaButton.Pressed += OpenAreaCreation;
        headingRow.AddChild(_newAreaButton);
    }

    private void InitializeUx1AOverlay()
    {
        _areaCreationOverlay = new StudioAreaCreationOverlay(_session);
        _areaCreationOverlay.StatusChanged += SetStatus;
        _areaCreationOverlay.AreaChanged += RefreshAfterUx1AMutation;
        AddChild(_areaCreationOverlay);
    }

    private void OpenAreaCreation()
    {
        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _areaLoadingOverlay.Close();
        _areaLayersOverlay.Close();
        _placeableBlueprintOverlay.Close();
        SwitchPage("zones", notify: false);
        _areaCreationOverlay.Open();
        SetStatus("Assistant de zone ouvert · trois étapes · aperçu sans écriture");
    }

    private void RefreshAfterUx1AMutation()
    {
        PopulateAreas();
        RefreshAreaTabs();
        if (_session.ActiveArea is null)
        {
            WorldViewport.ShowNoArea(_session);
            RefreshComparisonViewport();
            RebuildExplorer();
            RefreshInspector();
        }
        else
        {
            RebuildActiveArea(
                $"{_session.ActiveArea.DisplayName} · document de zone actif · non enregistré",
                notify: false);
        }
        UpdateUx1AActionState();
        UpdateCommandState();
        NavigationChanged?.Invoke();
    }

    private void UpdateUx1AActionState()
    {
        if (_newAreaButton is null)
        {
            return;
        }
        _newAreaButton.Visible = PageId == "zones" && _session.ActiveArea is not null;
    }

    internal bool PrepareUx1AEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        return _areaCreationOverlay.PrepareEvidence();
    }

    internal bool RunUx1ASmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var uiTransaction = _areaCreationOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1AEvidence("ux1-a-wizard") &&
                      PageId == "zones" &&
                      _areaCreationOverlay.IsOpen &&
                      _areaCreationOverlay.StepCount == 3 &&
                      _areaCreationOverlay.ShowsHumanSurface;
        _areaCreationOverlay.Close();
        var result = _session.RunUx1AContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1A_{(valid ? "PASS" : "FAILED")} " +
            $"wizard_preview={result.WizardPreview.ToString().ToLowerInvariant()} " +
            $"empty_template={result.EmptyTemplate.ToString().ToLowerInvariant()} " +
            $"playable_template={result.PlayableTemplate.ToString().ToLowerInvariant()} " +
            $"foundation={result.FoundationProfile.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"independent_document={result.IndependentDocument.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"stable_identity={result.StableIdentity.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
