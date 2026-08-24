using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAreaLoadingOverlay _areaLoadingOverlay = null!;
    private Button _areaLoadingButton = null!;

    private void BuildUx1CActions(HBoxContainer headingRow)
    {
        _areaLoadingButton = StudioStyle.Button("Chargement", compact: true);
        _areaLoadingButton.TooltipText =
            "Configurer titre, média, astuces, progression et repli accessible de la zone active.";
        _areaLoadingButton.Pressed += OpenAreaLoading;
        headingRow.AddChild(_areaLoadingButton);
    }

    private void InitializeUx1COverlay()
    {
        _areaLoadingOverlay = new StudioAreaLoadingOverlay(_session);
        _areaLoadingOverlay.StatusChanged += SetStatus;
        _areaLoadingOverlay.AreaChanged += RefreshAfterUx1CMutation;
        AddChild(_areaLoadingOverlay);
    }

    private void OpenAreaLoading()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de configurer son écran de chargement.", error: true);
            return;
        }

        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaCreationOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _areaLayersOverlay.Close();
        _placeableBlueprintOverlay.Close();
        SwitchPage("zones", notify: false);
        _areaLoadingOverlay.Open();
        SetStatus("Écran de chargement ouvert · aperçu normal et mouvement réduit disponibles");
    }

    private void RefreshAfterUx1CMutation()
    {
        RefreshAfterUx1BMutation();
        UpdateUx1CActionState();
    }

    private void UpdateUx1CActionState()
    {
        if (_areaLoadingButton is null)
        {
            return;
        }
        _areaLoadingButton.Visible = PageId == "zones" && _session.ActiveArea is not null;
    }

    internal bool PrepareUx1CEvidence(string mode)
    {
        SwitchPage("zones", notify: false);
        return _areaLoadingOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1CSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var uiTransaction = _areaLoadingOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1CEvidence("ux1-c-loading") &&
                      PageId == "zones" &&
                      _areaLoadingOverlay.IsOpen &&
                      _areaLoadingOverlay.PreviewModeCount == 2 &&
                      _areaLoadingOverlay.ShowsHumanSurface;
        _areaLoadingOverlay.Close();
        var result = _session.RunUx1CContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1C_{(valid ? "PASS" : "FAILED")} " +
            $"preview={result.Preview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"image_video={result.ImageAndVideo.ToString().ToLowerInvariant()} " +
            $"reduced_motion={result.ReducedMotion.ToString().ToLowerInvariant()} " +
            $"accessible_fallback={result.AccessibleFallback.ToString().ToLowerInvariant()} " +
            $"real_streaming={result.RealStreaming.ToString().ToLowerInvariant()} " +
            $"minimum_duration={result.MinimumDuration.ToString().ToLowerInvariant()} " +
            $"other_experience_preserved={result.OtherExperiencePreserved.ToString().ToLowerInvariant()} " +
            $"world_profile_preserved={result.WorldProfilePreserved.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
