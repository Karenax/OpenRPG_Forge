using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerFoundationOverlay _triggerFoundationOverlay = null!;
    private Button _triggerFoundationButton = null!;

    private void BuildUx1YActions(HBoxContainer headingRow)
    {
        _triggerFoundationButton = StudioStyle.Button("Déclencheur", compact: true);
        _triggerFoundationButton.TooltipText =
            "Configurer un volume, ses filtres, ses actions et ses événements avec aperçu obligatoire.";
        _triggerFoundationButton.Pressed += OpenTriggerFoundationUx1Y;
        headingRow.AddChild(_triggerFoundationButton);
    }

    private void InitializeUx1YOverlay()
    {
        _triggerFoundationOverlay = new StudioTriggerFoundationOverlay(_session);
        _triggerFoundationOverlay.StatusChanged += SetStatus;
        _triggerFoundationOverlay.TriggerChanged += RefreshAfterUx1YMutation;
        AddChild(_triggerFoundationOverlay);
    }

    private void OpenTriggerFoundationUx1Y()
    {
        if (_session.ActiveArea is null)
        {
            SetStatus("Créez d’abord une zone avant de configurer un déclencheur.", error: true);
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
        SwitchPage("placement", notify: false);
        _triggerFoundationOverlay.Open();
        SetStatus("Déclencheur ouvert · sélection explicite · aperçu obligatoire avant application");
    }

    private void RefreshAfterUx1YMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx1YActionState();
    }

    private void UpdateUx1YActionState()
    {
        if (_triggerFoundationButton is null)
        {
            return;
        }
        _triggerFoundationButton.Visible = PageId == "placement" && _session.ActiveArea is not null;
    }

    private void CloseUx1YOverlay()
    {
        if (_triggerFoundationOverlay is not null)
        {
            _triggerFoundationOverlay.Close();
        }
    }

    internal bool PrepareUx1YEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _triggerFoundationOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx1YSmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var uiTransaction = _triggerFoundationOverlay.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx1YEvidence("ux1-y-foundation");
        var overlayOpen = _triggerFoundationOverlay.IsOpen;
        var sectionCount = _triggerFoundationOverlay.SectionCount;
        var vertexCount = _triggerFoundationOverlay.VertexCount;
        var holeCount = _triggerFoundationOverlay.HoleCount;
        var uniformProjection = _triggerFoundationOverlay.UsesUniformProjection;
        var polygonSurface = _triggerFoundationOverlay.IsExtrudedPolygonSurface;
        var humanSurface = _triggerFoundationOverlay.ShowsHumanSurface;
        var uiReady = evidencePrepared &&
                      PageId == "placement" &&
                      overlayOpen &&
                      sectionCount == 3 &&
                      vertexCount >= 3 &&
                      holeCount >= 1 &&
                      uniformProjection &&
                      polygonSurface &&
                      humanSurface;
        _triggerFoundationOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1YContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      _session.IsDirty == originalDirty;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Y_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} sections={sectionCount} vertices={vertexCount} " +
                $"holes={holeCount} uniform={uniformProjection.ToString().ToLowerInvariant()} " +
                $"polygon={polygonSurface.ToString().ToLowerInvariant()} human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1Y_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.SelectionExplicit.ToString().ToLowerInvariant()} " +
            $"geometry={result.Geometry.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"invalid_geometry={result.InvalidGeometryBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_filter={result.InvalidFilterBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_binding={result.InvalidBindingBlocked.ToString().ToLowerInvariant()} " +
            $"stale_draft_blocked={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_revision_blocked={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"non_polygon_blocked={result.NonPolygonBlocked.ToString().ToLowerInvariant()} " +
            $"typed_events={result.TypedEvents.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"portal_preserved={result.PortalPreserved.ToString().ToLowerInvariant()} " +
            $"validation_preserved={result.ValidationPreserved.ToString().ToLowerInvariant()} " +
            $"advanced_preserved={result.AdvancedPreserved.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"uniform_projection={uniformProjection.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
