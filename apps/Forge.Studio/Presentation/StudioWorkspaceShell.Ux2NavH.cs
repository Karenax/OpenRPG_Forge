using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioTriggerPortalNavigationOverlayUx2NavH _triggerPortalNavigationOverlayUx2NavH = null!;
    private Button _triggerPortalNavigationButtonUx2NavH = null!;

    private void BuildUx2NavHActions(HBoxContainer headingRow)
    {
        _triggerPortalNavigationButtonUx2NavH = StudioStyle.Button("Transitions de trigger", compact: true);
        _triggerPortalNavigationButtonUx2NavH.TooltipText =
            "Configurer destination, arrivée, orientation, préchargement et permissions par aperçu sec.";
        _triggerPortalNavigationButtonUx2NavH.Pressed += OpenTriggerPortalNavigationUx2NavH;
        headingRow.AddChild(_triggerPortalNavigationButtonUx2NavH);
    }

    private void InitializeUx2NavHOverlay()
    {
        _triggerPortalNavigationOverlayUx2NavH = new StudioTriggerPortalNavigationOverlayUx2NavH(_session);
        _triggerPortalNavigationOverlayUx2NavH.StatusChanged += SetStatus;
        _triggerPortalNavigationOverlayUx2NavH.TriggerPortalChanged += RefreshAfterUx2NavHMutation;
        AddChild(_triggerPortalNavigationOverlayUx2NavH);
    }

    private void OpenTriggerPortalNavigationUx2NavH()
    {
        if (!_session.TriggerPortalChoicesUx2NavH.Any(item => item.CanEdit))
        {
            SetStatus("Aucun déclencheur ne possède une fondation UX1-Y éditable dans cette zone.", error: true);
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
        CloseUx2NavGOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _triggerPortalNavigationOverlayUx2NavH.Open();
        SetStatus("Transitions de déclencheur ouvertes · sélection explicite · aperçu sec obligatoire");
    }

    private void RefreshAfterUx2NavHMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavHActionState();
    }

    private void UpdateUx2NavHActionState()
    {
        if (_triggerPortalNavigationButtonUx2NavH is null) return;
        _triggerPortalNavigationButtonUx2NavH.Visible = PageId == "navigation" &&
                                                        _session.TriggerPortalChoicesUx2NavH.Any(item => item.CanEdit);
    }

    private void CloseUx2NavHOverlay()
    {
        if (_triggerPortalNavigationOverlayUx2NavH is not null)
            _triggerPortalNavigationOverlayUx2NavH.Close();
    }

    internal bool PrepareUx2NavHEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavBOverlay();
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        return _triggerPortalNavigationOverlayUx2NavH.PrepareEvidence(mode);
    }

    internal bool RunUx2NavHSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _triggerPortalNavigationOverlayUx2NavH.IsOpen;
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
        var originalTriggerPortal = _session.CaptureTriggerPortalApprovalUx2NavH();

        var uiTransaction = _triggerPortalNavigationOverlayUx2NavH.RunUiTransactionEvidence();
        var evidence = PrepareUx2NavHEvidence("ux2-nav-h");
        var uiReady = evidence && PageId == "navigation" &&
                      _triggerPortalNavigationOverlayUx2NavH.IsOpen &&
                      _triggerPortalNavigationOverlayUx2NavH.ChoiceCount >= 2 &&
                      _triggerPortalNavigationOverlayUx2NavH.HasExplicitSelection &&
                      _triggerPortalNavigationOverlayUx2NavH.CanonicalPreviewReady &&
                      _triggerPortalNavigationOverlayUx2NavH.ShowsRuntimeResolution &&
                      _triggerPortalNavigationOverlayUx2NavH.ShowsStructuredDiagnostics &&
                      _triggerPortalNavigationOverlayUx2NavH.ShowsHumanSurface &&
                      _triggerPortalNavigationOverlayUx2NavH.UsesIsolatedEvidence &&
                      _triggerPortalNavigationOverlayUx2NavH.EvidenceSourceUntouched;
        var isolatedEvidence = _triggerPortalNavigationOverlayUx2NavH.UsesIsolatedEvidence;
        _triggerPortalNavigationOverlayUx2NavH.Close();
        var released = _triggerPortalNavigationOverlayUx2NavH.EvidenceResourcesReleased;
        var restored = _triggerPortalNavigationOverlayUx2NavH.SourceSessionRestored;
        var result = _session.RunUx2NavHContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _triggerPortalNavigationOverlayUx2NavH.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreNavigationApprovalUx2NavB(originalNav);
        _session.RestoreWaypointApprovalUx2NavC(originalWaypoint);
        _session.RestorePatrolApprovalUx2NavD(originalPatrol);
        _session.RestoreLocatorApprovalUx2NavE(originalLocator);
        _session.RestorePortalApprovalUx2NavF(originalPortal);
        _session.RestoreDoorNavigationApprovalUx2NavG(originalDoorPortal);
        _session.RestoreTriggerPortalApprovalUx2NavH(originalTriggerPortal);
        var surfaceRestored = PageId == originalPage &&
                              _triggerPortalNavigationOverlayUx2NavH.IsOpen == originalOpen;
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
                              _session.CaptureDoorNavigationApprovalUx2NavG() == originalDoorPortal &&
                              _session.CaptureTriggerPortalApprovalUx2NavH() == originalTriggerPortal;
        var valid = result.Succeeded && uiReady && uiTransaction && released && restored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX2_NAV_H_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"human_labels={result.HumanLabels.ToString().ToLowerInvariant()} " +
                 $"foundation_required={result.FoundationRequired.ToString().ToLowerInvariant()} " +
                 $"typed_destination={result.TypedDestination.ToString().ToLowerInvariant()} " +
                 $"runtime_transition={result.RuntimeTransition.ToString().ToLowerInvariant()} " +
                 $"permission_refusal={result.PermissionRefusal.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"ux1_preserved={result.Ux1DataPreserved.ToString().ToLowerInvariant()} " +
                 $"validation_neighbor={result.ValidationNeighborPreserved.ToString().ToLowerInvariant()} " +
                 $"world_neighbors={result.WorldNeighborsPreserved.ToString().ToLowerInvariant()} " +
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
