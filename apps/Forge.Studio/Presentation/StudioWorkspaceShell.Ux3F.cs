using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssetAuthoringWorkbenchUx3F _assetAuthoringUx3F = null!;
    private Button _assetAuthoringButtonUx3F = null!;

    private void BuildUx3FActions(HBoxContainer headingRow)
    {
        _assetAuthoringButtonUx3F = StudioStyle.Button("Contenu & Blender", compact: true);
        _assetAuthoringButtonUx3F.TooltipText =
            "Explorer textures et modèles, gouverner leur profil d’import et préparer un aller-retour Blender typé.";
        _assetAuthoringButtonUx3F.Pressed += OpenAssetAuthoringUx3F;
        headingRow.AddChild(_assetAuthoringButtonUx3F);
    }

    private void InitializeUx3FOverlay()
    {
        _assetAuthoringUx3F = new StudioAssetAuthoringWorkbenchUx3F(_session);
        _assetAuthoringUx3F.StatusChanged += SetStatus;
        _assetAuthoringUx3F.WorkspaceChanged += RefreshAfterUx3FMutation;
        AddChild(_assetAuthoringUx3F);
    }

    private void OpenAssetAuthoringUx3F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’explorer ses assets.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetAuthoringUx3F.Open();
        SetStatus("UX3-F · navigateur de contenu, profils d’import et pont Blender typé.");
    }

    private void RefreshAfterUx3FMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx3FActionState();
    }

    private void UpdateUx3FActionState()
    {
        if (_assetAuthoringButtonUx3F is not null)
            _assetAuthoringButtonUx3F.Visible = PageId is "blueprints" or "assets" && _session.Project is not null;
    }

    private void CloseUx3FOverlay()
    {
        if (_assetAuthoringUx3F is not null) _assetAuthoringUx3F.Close();
    }

    internal bool PrepareUx3FEvidence(string mode)
    {
        SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3GOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetAuthoringUx3F.Open();
        return _assetAuthoringUx3F.PrepareEvidence(mode);
    }

    internal void ReleaseUx3FAutomatedEvidence() => _assetAuthoringUx3F.CloseEvidence();

    internal bool RunUx3FSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _assetAuthoringUx3F.IsOpen;
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
        var approval = _session.CaptureAssetAuthoringApprovalUx3F();
        var root = _session.Session?.RootDirectory;
        var originalProfiles = root is null ? null : ReadStoreUx3F(root, AssetProcessingProfileStore.RelativePath);
        var originalExternal = root is null ? null : ReadStoreUx3F(root, AssetExternalEditStore.RelativePath);

        var uiTransaction = _assetAuthoringUx3F.RunUiTransactionEvidence();
        var prepared = PrepareUx3FEvidence("ux3-f-content-browser");
        var uiReady = prepared && _assetAuthoringUx3F.IsOpen &&
                      _assetAuthoringUx3F.UsesIsolatedEvidence &&
                      (_assetAuthoringUx3F.UsesRealModuleEvidence ^
                       _assetAuthoringUx3F.UsesCanonicalFallbackEvidence) &&
                      _assetAuthoringUx3F.HasExplicitSelection &&
                      _assetAuthoringUx3F.ProcessingPreviewReady &&
                      _assetAuthoringUx3F.ExternalPreviewReady &&
                      _assetAuthoringUx3F.ShowsHumanSurface &&
                      _assetAuthoringUx3F.EvidenceSourceUntouched;
        _assetAuthoringUx3F.CloseEvidence();
        var released = _assetAuthoringUx3F.EvidenceResourcesReleased;
        var sourceRestored = _assetAuthoringUx3F.SourceSessionRestored;
        var contract = _session.RunUx3FContractSmoke();
        var contractReleased = _session.LastUx3FTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _assetAuthoringUx3F.RestoreSourceSurfaceAfterSmoke();
        else _assetAuthoringUx3F.HideSourceSurfaceAfterSmoke();
        _session.RestoreAssetAuthoringApprovalUx3F(approval);
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
                              _session.CaptureAssetAuthoringApprovalUx3F() == approval &&
                              (root is null ? null : ReadStoreUx3F(root, AssetProcessingProfileStore.RelativePath)) ==
                              originalProfiles &&
                              (root is null ? null : ReadStoreUx3F(root, AssetExternalEditStore.RelativePath)) ==
                              originalExternal;
        var surfaceRestored = PageId == originalPage && _assetAuthoringUx3F.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && contractReleased &&
                    sourceRestored && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_F_{(valid ? "PASS" : "FAILED")} " +
                 "routes=CONT-013,CONT-014,CONT-020,CONT-021,CONT-022 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"catalog={contract.CatalogBrowsed.ToString().ToLowerInvariant()} " +
                 $"texture_model={contract.TextureAndModelFacets.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"profile_dry_run={contract.ProcessingDryRun.ToString().ToLowerInvariant()} " +
                 $"import_plan={contract.ImportPlan.ToString().ToLowerInvariant()} " +
                 $"external_dry_run={contract.ExternalDryRun.ToString().ToLowerInvariant()} " +
                 $"worker_request={contract.WorkerRequest.ToString().ToLowerInvariant()} " +
                 $"watch_plan={contract.WatchPlan.ToString().ToLowerInvariant()} " +
                 $"stale_profile={contract.StaleProcessingBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_external={contract.StaleExternalBlocked.ToString().ToLowerInvariant()} " +
                 $"separate_approvals={contract.SeparateApprovals.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"profile_neighbors={contract.ProfileNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"external_neighbors={contract.ExternalNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_source={contract.ProjectSourcePreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private static string? ReadStoreUx3F(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
