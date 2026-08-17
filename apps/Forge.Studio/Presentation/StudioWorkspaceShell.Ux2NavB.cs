using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNavigationFoundationOverlayUx2NavB _navigationFoundationOverlayUx2NavB = null!;
    private Button _navigationFoundationButtonUx2NavB = null!;

    private void BuildUx2NavBActions(HBoxContainer headingRow)
    {
        _navigationFoundationButtonUx2NavB = StudioStyle.Button("Surface & ancres", compact: true);
        _navigationFoundationButtonUx2NavB.TooltipText =
            "Éditer la surface, les profils, les liaisons et les ancres avec aperçu 3D sec.";
        _navigationFoundationButtonUx2NavB.Pressed += OpenNavigationFoundationUx2NavB;
        headingRow.AddChild(_navigationFoundationButtonUx2NavB);
    }

    private void InitializeUx2NavBOverlay()
    {
        _navigationFoundationOverlayUx2NavB = new StudioNavigationFoundationOverlayUx2NavB(_session, WorldViewport);
        _navigationFoundationOverlayUx2NavB.StatusChanged += SetStatus;
        _navigationFoundationOverlayUx2NavB.NavigationChanged += RefreshAfterUx2NavBMutation;
        AddChild(_navigationFoundationOverlayUx2NavB);
    }

    private void OpenNavigationFoundationUx2NavB()
    {
        if (_session.Project?.Areas.Count is not > 0)
        {
            SetStatus("Créez d’abord une zone avant d’éditer sa navigation.", error: true);
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
        CloseUx2NavCOverlay();
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        SwitchPage("navigation", notify: false);
        _navigationFoundationOverlayUx2NavB.Open();
        SetStatus("Navigation ouverte · sélection explicite · aperçu sec 3D obligatoire");
    }

    private void RefreshAfterUx2NavBMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx2NavBActionState();
    }

    private void UpdateUx2NavBActionState()
    {
        if (_navigationFoundationButtonUx2NavB is null)
        {
            return;
        }
        _navigationFoundationButtonUx2NavB.Visible = PageId == "navigation" && _session.Project?.Areas.Count > 0;
    }

    private void CloseUx2NavBOverlay()
    {
        if (_navigationFoundationOverlayUx2NavB is not null)
        {
            _navigationFoundationOverlayUx2NavB.Close();
        }
    }

    internal bool PrepareUx2NavBEvidence(string mode)
    {
        SwitchPage("navigation", notify: false);
        CloseUx2NavDOverlay();
        CloseUx2NavEOverlay();
        CloseUx2NavFOverlay();
        CloseUx2NavGOverlay();
        CloseUx2NavHOverlay();
        CloseUx2NavIOverlay();
        CloseUx2NavJOverlay();
        CloseUx2NavKOverlay();
        CloseUx2EnvJOverlay();
        return _navigationFoundationOverlayUx2NavB.PrepareEvidence(mode);
    }

    internal bool RunUx2NavBSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _navigationFoundationOverlayUx2NavB.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalDirty = _session.IsDirty;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureNavigationApprovalUx2NavB();

        var uiTransaction = _navigationFoundationOverlayUx2NavB.RunUiTransactionEvidence();
        var uiTransactionReleased = _navigationFoundationOverlayUx2NavB.EvidenceResourcesReleased;
        var uiTransactionRestored = _navigationFoundationOverlayUx2NavB.SourceSessionRestored;
        var evidencePrepared = PrepareUx2NavBEvidence("ux2-nav-b");
        var overlayOpen = _navigationFoundationOverlayUx2NavB.IsOpen;
        var explicitSelection = _navigationFoundationOverlayUx2NavB.HasExplicitSelection;
        var canonicalPreview = _navigationFoundationOverlayUx2NavB.CanonicalPreviewReady;
        var realSurface = _navigationFoundationOverlayUx2NavB.ShowsRealSurface;
        var humanDestinations = _navigationFoundationOverlayUx2NavB.ShowsHumanDestinations;
        var structuredDiagnostics = _navigationFoundationOverlayUx2NavB.ShowsStructuredDiagnostics;
        var humanSurface = _navigationFoundationOverlayUx2NavB.ShowsHumanSurface;
        var isolatedEvidence = _navigationFoundationOverlayUx2NavB.UsesIsolatedEvidence;
        var evidenceSourceUntouched = _navigationFoundationOverlayUx2NavB.EvidenceSourceUntouched;
        var uiReady = evidencePrepared && PageId == "navigation" && overlayOpen &&
                      _navigationFoundationOverlayUx2NavB.ChoiceCount >= 1 &&
                      explicitSelection && canonicalPreview && realSurface && humanDestinations &&
                      structuredDiagnostics && humanSurface &&
                      isolatedEvidence && evidenceSourceUntouched;
        _navigationFoundationOverlayUx2NavB.Close();
        var evidenceReleased = _navigationFoundationOverlayUx2NavB.EvidenceResourcesReleased;
        var sourceSessionRestored = _navigationFoundationOverlayUx2NavB.SourceSessionRestored;

        var result = _session.RunUx2NavBContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _navigationFoundationOverlayUx2NavB.RestoreSourceSurfaceAfterSmoke();
        }
        var surfaceRestored = PageId == originalPageId &&
                              _navigationFoundationOverlayUx2NavB.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureNavigationApprovalUx2NavB() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_B_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"surface={realSurface.ToString().ToLowerInvariant()} " +
                $"destinations={humanDestinations.ToString().ToLowerInvariant()} " +
                $"diagnostics={structuredDiagnostics.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && uiReady && uiTransaction &&
                    _navigationFoundationOverlayUx2NavB.LastUiTransaction && uiTransactionReleased &&
                    uiTransactionRestored && evidenceReleased && sourceSessionRestored && sourceUnchanged &&
                    surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX2_NAV_B_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"human_destinations={result.HumanDestinations.ToString().ToLowerInvariant()} " +
            $"surface={result.SurfaceBuilt.ToString().ToLowerInvariant()} " +
            $"partial_bake={result.PartialBakePlanned.ToString().ToLowerInvariant()} " +
            $"invalid_bounds={result.InvalidBoundsBlocked.ToString().ToLowerInvariant()} " +
            $"invalid_destination={result.InvalidDestinationBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_fingerprint={result.StaleFingerprintBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"navigation_neighbors={result.NavigationNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"project_neighbors={result.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"contract_source_untouched={result.SourceUntouched.ToString().ToLowerInvariant()} " +
            $"human_text={result.HumanText.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceReleased.ToString().ToLowerInvariant()} " +
            $"source_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
