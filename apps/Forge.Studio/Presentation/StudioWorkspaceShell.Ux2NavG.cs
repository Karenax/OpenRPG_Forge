using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDoorPortalNavigationOverlayUx2NavG _doorPortalNavigationOverlayUx2NavG = null!;
    private Button _doorPortalNavigationButtonUx2NavG = null!;

    private void BuildUx2NavGActions(HBoxContainer headingRow)
    {
        _doorPortalNavigationButtonUx2NavG = StudioStyle.Button("Portails de porte", compact: true);
        _doorPortalNavigationButtonUx2NavG.TooltipText =
            "Configurer destination, retour, PortalUse et navigation dynamique par aperçu sec.";
        _doorPortalNavigationButtonUx2NavG.Pressed += OpenDoorPortalNavigationUx2NavG;
        headingRow.AddChild(_doorPortalNavigationButtonUx2NavG);
    }

    private void InitializeUx2NavGOverlay()
    {
        _doorPortalNavigationOverlayUx2NavG = new StudioDoorPortalNavigationOverlayUx2NavG(_session);
        _doorPortalNavigationOverlayUx2NavG.StatusChanged += SetStatus;
        _doorPortalNavigationOverlayUx2NavG.DoorNavigationChanged += RefreshAfterUx2NavGMutation;
        AddChild(_doorPortalNavigationOverlayUx2NavG);
    }

    private void OpenDoorPortalNavigationUx2NavG()
    {
        if (!_session.DoorNavigationChoicesUx2NavG.Any(item => item.CanEdit))
        {
            SetStatus("Aucune porte ne possède à la fois fondation, navigation et accès UX1 valides.", error: true);
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
        CloseUx1AJOverlay();
        CloseUx2AOverlay();
        CloseUx4AOverlay();
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _doorPortalNavigationOverlayUx2NavG.Open();
        SetStatus("Portails de porte ouverts · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavGMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavGActionState();
    }

    private void UpdateUx2NavGActionState()
    {
        if (_doorPortalNavigationButtonUx2NavG is null) return;
        _doorPortalNavigationButtonUx2NavG.Visible = PageId == "navigation" &&
                                                     _session.DoorNavigationChoicesUx2NavG.Any(item => item.CanEdit);
    }

    private void CloseUx2NavGOverlay()
    {
        if (_doorPortalNavigationOverlayUx2NavG is not null)
            _doorPortalNavigationOverlayUx2NavG.Close();
    }

    internal bool PrepareUx2NavGEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2EnvJOverlay();
        return _doorPortalNavigationOverlayUx2NavG.PrepareEvidence(mode);
    }

    internal bool RunUx2NavGSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _doorPortalNavigationOverlayUx2NavG.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalUndo = originalHistory?.CanUndo;
        var originalRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalNav = _session.CaptureNavigationApprovalUx2NavB();
        var originalWaypoint = _session.CaptureWaypointApprovalUx2NavC();
        var originalPatrol = _session.CapturePatrolApprovalUx2NavD();
        var originalLocator = _session.CaptureLocatorApprovalUx2NavE();
        var originalPortal = _session.CapturePortalApprovalUx2NavF();
        var originalDoorPortal = _session.CaptureDoorNavigationApprovalUx2NavG();

        var uiTransaction = _doorPortalNavigationOverlayUx2NavG.RunUiTransactionEvidence();
        var evidence = PrepareUx2NavGEvidence("ux2-nav-g");
        var uiReady = evidence && PageId == "navigation" &&
                      _doorPortalNavigationOverlayUx2NavG.IsOpen &&
                      _doorPortalNavigationOverlayUx2NavG.ChoiceCount >= 3 &&
                      _doorPortalNavigationOverlayUx2NavG.HasExplicitSelection &&
                      _doorPortalNavigationOverlayUx2NavG.CanonicalPreviewReady &&
                      _doorPortalNavigationOverlayUx2NavG.ShowsRuntimeResolution &&
                      _doorPortalNavigationOverlayUx2NavG.ShowsStructuredDiagnostics &&
                      _doorPortalNavigationOverlayUx2NavG.ShowsHumanSurface &&
                      _doorPortalNavigationOverlayUx2NavG.UsesIsolatedEvidence &&
                      _doorPortalNavigationOverlayUx2NavG.EvidenceSourceUntouched;
        var isolatedEvidence = _doorPortalNavigationOverlayUx2NavG.UsesIsolatedEvidence;
        _doorPortalNavigationOverlayUx2NavG.Close();
        var released = _doorPortalNavigationOverlayUx2NavG.EvidenceResourcesReleased;
        var restored = _doorPortalNavigationOverlayUx2NavG.SourceSessionRestored;
        var result = _session.RunUx2NavGContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _doorPortalNavigationOverlayUx2NavG.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreNavigationApprovalUx2NavB(originalNav);
        _session.RestoreWaypointApprovalUx2NavC(originalWaypoint);
        _session.RestorePatrolApprovalUx2NavD(originalPatrol);
        _session.RestoreLocatorApprovalUx2NavE(originalLocator);
        _session.RestorePortalApprovalUx2NavF(originalPortal);
        _session.RestoreDoorNavigationApprovalUx2NavG(originalDoorPortal);
        var surfaceRestored = PageId == originalPage &&
                              _doorPortalNavigationOverlayUx2NavG.IsOpen == originalOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, _session.History) &&
                              _session.History?.State.Revision == originalRevision &&
                              (_session.History?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.History?.CanUndo == originalUndo &&
                              _session.History?.CanRedo == originalRedo && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNavigationApprovalUx2NavB() == originalNav &&
                              _session.CaptureWaypointApprovalUx2NavC() == originalWaypoint &&
                              _session.CapturePatrolApprovalUx2NavD() == originalPatrol &&
                              _session.CaptureLocatorApprovalUx2NavE() == originalLocator &&
                              _session.CapturePortalApprovalUx2NavF() == originalPortal &&
                              _session.CaptureDoorNavigationApprovalUx2NavG() == originalDoorPortal;
        var valid = result.Succeeded && uiReady && uiTransaction && released && restored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_NAV_G_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
                 $"typed_destination={result.TypedDestination.ToString().ToLowerInvariant()} " +
                 $"atomic_direction={result.AtomicDirection.ToString().ToLowerInvariant()} " +
                 $"portal_use_in_place={result.PortalUseInPlace.ToString().ToLowerInvariant()} " +
                 $"portal_use_runtime={result.PortalUseRuntime.ToString().ToLowerInvariant()} " +
                 $"dynamic_navigation={result.DynamicNavigationRuntime.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"ux1_preserved={result.Ux1DataPreserved.ToString().ToLowerInvariant()} " +
                 $"neighbor_links={result.NeighborLinksPreserved.ToString().ToLowerInvariant()} " +
                 $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
                 $"cleanup={result.Cleanup.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"evidence_released={released.ToString().ToLowerInvariant()} " +
                 $"source_restored={restored.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
