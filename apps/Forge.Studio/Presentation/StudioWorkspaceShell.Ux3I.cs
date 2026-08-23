using Forge.ModuleFormat.Serialization;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssetGovernanceWorkbenchUx3I _assetGovernanceUx3I = null!;
    private Button _assetGovernanceButtonUx3I = null!;

    private void BuildUx3IActions(HBoxContainer headingRow)
    {
        _assetGovernanceButtonUx3I = StudioStyle.Button("Provenance & quarantaine", compact: true);
        _assetGovernanceButtonUx3I.TooltipText =
            "Gouverner provenance, quarantaine d’import et plans média sans exécuter de fournisseur.";
        _assetGovernanceButtonUx3I.Pressed += OpenAssetGovernanceUx3I;
        headingRow.AddChild(_assetGovernanceButtonUx3I);
    }

    private void InitializeUx3IOverlay()
    {
        _assetGovernanceUx3I = new StudioAssetGovernanceWorkbenchUx3I(_session);
        _assetGovernanceUx3I.StatusChanged += SetStatus;
        _assetGovernanceUx3I.WorkspaceChanged += RefreshAfterUx3IMutation;
        AddChild(_assetGovernanceUx3I);
    }

    private void OpenAssetGovernanceUx3I()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant sa gouvernance d’assets.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        _assetGovernanceUx3I.Open();
        SetStatus("UX3-I · provenance, quarantaine et génération média gouvernée.");
    }

    private void RefreshAfterUx3IMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx3IActionState();
    }

    private void UpdateUx3IActionState()
    {
        if (_assetGovernanceButtonUx3I is not null)
            _assetGovernanceButtonUx3I.Visible = PageId is "blueprints" or "assets" && _session.Project is not null;
    }

    private void CloseUx3IOverlay()
    {
        if (_assetGovernanceUx3I is not null) _assetGovernanceUx3I.Close();
    }

    internal bool PrepareUx3IEvidence(string mode)
    {
        SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        _assetGovernanceUx3I.Open();
        return _assetGovernanceUx3I.PrepareEvidence(mode);
    }

    internal void ReleaseUx3IAutomatedEvidence() => _assetGovernanceUx3I.CloseEvidence();

    internal bool RunUx3ISmoke()
    {
        var originalPage = PageId;
        var originalOpen = _assetGovernanceUx3I.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalWorkspace = EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile);
        var governanceApproval = _session.CaptureGovernanceApprovalUx3I();
        var mediaApproval = _session.CaptureMediaApprovalUx3I();

        var uiTransaction = _assetGovernanceUx3I.RunUiTransactionEvidence();
        var prepared = PrepareUx3IEvidence("ux3-i-governed-assets");
        var uiReady = prepared && _assetGovernanceUx3I.IsOpen &&
                      _assetGovernanceUx3I.UsesIsolatedEvidence &&
                      (_assetGovernanceUx3I.UsesRealModuleEvidence ^
                       _assetGovernanceUx3I.UsesCanonicalFallbackEvidence) &&
                      _assetGovernanceUx3I.HasExplicitSelections &&
                      _assetGovernanceUx3I.GovernancePreviewReady &&
                      _assetGovernanceUx3I.MediaPreviewReady &&
                      _assetGovernanceUx3I.ShowsHumanSurface &&
                      _assetGovernanceUx3I.EvidenceSourceUntouched;
        _assetGovernanceUx3I.CloseEvidence();
        var released = _assetGovernanceUx3I.EvidenceResourcesReleased;
        var sourceRestored = _assetGovernanceUx3I.SourceSessionRestored;
        var contract = _session.RunUx3IContractSmoke();
        var contractReleased = _session.LastUx3ITemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _assetGovernanceUx3I.RestoreSourceSurfaceAfterSmoke();
        else _assetGovernanceUx3I.HideSourceSurfaceAfterSmoke();
        _session.RestoreGovernanceApprovalUx3I(governanceApproval);
        _session.RestoreMediaApprovalUx3I(mediaApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile) == originalWorkspace &&
                              _session.CaptureGovernanceApprovalUx3I() == governanceApproval &&
                              _session.CaptureMediaApprovalUx3I() == mediaApproval;
        var surfaceRestored = PageId == originalPage && _assetGovernanceUx3I.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && contractReleased &&
                    sourceRestored && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_I_{(valid ? "PASS" : "FAILED")} " +
                 "routes=CONT-023,CONT-024,AI-024 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"provenance={contract.Provenance.ToString().ToLowerInvariant()} " +
                 $"quarantine={contract.Quarantine.ToString().ToLowerInvariant()} " +
                 $"human_decision={contract.HumanDecision.ToString().ToLowerInvariant()} " +
                 $"media_policy={contract.MediaPolicy.ToString().ToLowerInvariant()} " +
                 $"media_plan={contract.MediaPlan.ToString().ToLowerInvariant()} " +
                 $"canonical_governance={contract.CanonicalGovernanceCommand.ToString().ToLowerInvariant()} " +
                 $"canonical_project={contract.CanonicalProjectCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbor={contract.NeighborPreserved.ToString().ToLowerInvariant()} " +
                 $"project_source={contract.ProjectPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
