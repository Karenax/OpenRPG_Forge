using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioFactionFoundationOverlayUx4A _factionFoundationOverlayUx4A = null!;
    private Button _factionFoundationButtonUx4A = null!;

    private void BuildUx4AActions(HBoxContainer headingRow)
    {
        _factionFoundationButtonUx4A = StudioStyle.Button("Factions", compact: true);
        _factionFoundationButtonUx4A.TooltipText =
            "Éditer définitions, parents, relations directionnelles et bandes avec aperçu canonique.";
        _factionFoundationButtonUx4A.Pressed += OpenFactionFoundationUx4A;
        headingRow.AddChild(_factionFoundationButtonUx4A);
    }

    private void InitializeUx4AOverlay()
    {
        _factionFoundationOverlayUx4A = new StudioFactionFoundationOverlayUx4A(_session);
        _factionFoundationOverlayUx4A.StatusChanged += SetStatus;
        _factionFoundationOverlayUx4A.FactionChanged += RefreshAfterUx4AMutation;
        AddChild(_factionFoundationOverlayUx4A);
    }

    private void OpenFactionFoundationUx4A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses factions.", error: true);
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
        CloseUx4BOverlay();
        CloseUx4COverlay();
        CloseUx4TOverlay();
        CloseUx4UOverlay();
        SwitchPage("social-content", notify: false);
        _factionFoundationOverlayUx4A.Open();
        SetStatus("Factions ouvertes · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4AMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4AActionState();
    }

    private void UpdateUx4AActionState()
    {
        if (_factionFoundationButtonUx4A is null)
        {
            return;
        }
        _factionFoundationButtonUx4A.Visible = PageId == "social-content" && _session.Project is not null;
    }

    private void CloseUx4AOverlay()
    {
        if (_factionFoundationOverlayUx4A is not null)
        {
            _factionFoundationOverlayUx4A.Close();
        }
    }

    internal bool PrepareUx4AEvidence(string mode)
    {
        SwitchPage("social-content", notify: false);
        return _factionFoundationOverlayUx4A.PrepareEvidence(mode);
    }

    internal bool RunUx4ASmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _factionFoundationOverlayUx4A.IsOpen;
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
        var originalApproval = _session.CaptureFactionApprovalUx4A();

        var uiTransaction = _factionFoundationOverlayUx4A.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4AEvidence("ux4-a-faction-foundation");
        var overlayOpen = _factionFoundationOverlayUx4A.IsOpen;
        var choiceCount = _factionFoundationOverlayUx4A.ChoiceCount;
        var explicitSelection = _factionFoundationOverlayUx4A.HasExplicitSelection;
        var canonicalPreview = _factionFoundationOverlayUx4A.CanonicalPreviewReady;
        var directionalMatrix = _factionFoundationOverlayUx4A.ShowsDirectionalMatrix;
        var hierarchy = _factionFoundationOverlayUx4A.ShowsHierarchy;
        var bands = _factionFoundationOverlayUx4A.ShowsBands;
        var humanSurface = _factionFoundationOverlayUx4A.ShowsHumanSurface;
        var isolatedEvidence = _factionFoundationOverlayUx4A.UsesIsolatedEvidence;
        var realCryptEvidence = _factionFoundationOverlayUx4A.UsesRealCryptEvidence;
        var canonicalFallbackEvidence = _factionFoundationOverlayUx4A.UsesCanonicalFallbackEvidence;
        var evidencePreviewDryRun = _factionFoundationOverlayUx4A.EvidencePreviewDryRun;
        var evidenceSourceUntouched = _factionFoundationOverlayUx4A.EvidenceSourceUntouched;
        var evidenceFailure = _factionFoundationOverlayUx4A.LastEvidenceFailure;
        var uiReady = evidencePrepared && PageId == "social-content" && overlayOpen && choiceCount >= 3 &&
                      explicitSelection && canonicalPreview && directionalMatrix && hierarchy && bands &&
                      humanSurface && isolatedEvidence && evidencePreviewDryRun && evidenceSourceUntouched &&
                      (realCryptEvidence ^ canonicalFallbackEvidence);
        _factionFoundationOverlayUx4A.CloseEvidence();
        var evidenceResourcesReleased = _factionFoundationOverlayUx4A.EvidenceResourcesReleased;
        var sourceSessionRestored = _factionFoundationOverlayUx4A.SourceSessionRestored;

        var result = _session.RunUx4AContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _factionFoundationOverlayUx4A.RestoreSourceSurfaceAfterSmoke();
        }
        _session.RestoreFactionApprovalUx4A(originalApproval);
        var surfaceRestored = PageId == originalPageId &&
                              _factionFoundationOverlayUx4A.IsOpen == originalSurfaceOpen;
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.IsDirty == originalDirty &&
                              _session.CaptureFactionApprovalUx4A() == originalApproval;
        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4A_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} choices={choiceCount} " +
                $"selected={explicitSelection.ToString().ToLowerInvariant()} " +
                $"canonical_preview={canonicalPreview.ToString().ToLowerInvariant()} " +
                $"directional={directionalMatrix.ToString().ToLowerInvariant()} " +
                $"hierarchy={hierarchy.ToString().ToLowerInvariant()} bands={bands.ToString().ToLowerInvariant()} " +
                $"human={humanSurface.ToString().ToLowerInvariant()} " +
                $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                $"real_crypt={realCryptEvidence.ToString().ToLowerInvariant()} " +
                $"canonical_fallback={canonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                $"preview_dry_run={evidencePreviewDryRun.ToString().ToLowerInvariant()} " +
                $"failure={evidenceFailure} " +
                $"source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()}");
        }
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceResourcesReleased && sourceSessionRestored && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX4A_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"creation={result.Creation.ToString().ToLowerInvariant()} " +
            $"hierarchy={result.Hierarchy.ToString().ToLowerInvariant()} " +
            $"directional_matrix={result.DirectionalMatrix.ToString().ToLowerInvariant()} " +
            $"bands={result.Bands.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"typed_fingerprint={result.TypedDraftFingerprint.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"personal_null_preserved={result.NullPersonalPreserved.ToString().ToLowerInvariant()} " +
            $"archive={result.Archive.ToString().ToLowerInvariant()} " +
            $"safe_removal={result.SafeRemoval.ToString().ToLowerInvariant()} " +
            $"external_reference_blocked={result.ExternalReferenceBlocked.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
            $"isolated_evidence={isolatedEvidence.ToString().ToLowerInvariant()} " +
            $"real_crypt_evidence={realCryptEvidence.ToString().ToLowerInvariant()} " +
            $"canonical_fallback_evidence={canonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
            $"evidence_preview_dry_run={evidencePreviewDryRun.ToString().ToLowerInvariant()} " +
            $"evidence_source_untouched={evidenceSourceUntouched.ToString().ToLowerInvariant()} " +
            $"evidence_released={evidenceResourcesReleased.ToString().ToLowerInvariant()} " +
            $"source_session_restored={sourceSessionRestored.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
