using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssetManifestWorkbenchUx3G _assetManifestUx3G = null!;
    private Button _assetManifestButtonUx3G = null!;

    private void BuildUx3GActions(HBoxContainer headingRow)
    {
        _assetManifestButtonUx3G = StudioStyle.Button("Paquets d’assets", compact: true);
        _assetManifestButtonUx3G.TooltipText =
            "Éditer manifests, collision/navigation, animations, VFX et LOD par transaction canonique.";
        _assetManifestButtonUx3G.Pressed += OpenAssetManifestUx3G;
        headingRow.AddChild(_assetManifestButtonUx3G);
    }

    private void InitializeUx3GOverlay()
    {
        _assetManifestUx3G = new StudioAssetManifestWorkbenchUx3G(_session);
        _assetManifestUx3G.StatusChanged += SetStatus;
        _assetManifestUx3G.WorkspaceChanged += RefreshAfterUx3GMutation;
        AddChild(_assetManifestUx3G);
    }

    private void OpenAssetManifestUx3G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses paquets d’assets.", error: true);
            return;
        }
        if (PageId is not ("blueprints" or "assets")) SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetManifestUx3G.Open();
        SetStatus("UX3-G · manifests, collision/navigation, animations, VFX et LOD.");
    }

    private void RefreshAfterUx3GMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx3GActionState();
    }

    private void UpdateUx3GActionState()
    {
        if (_assetManifestButtonUx3G is not null)
            _assetManifestButtonUx3G.Visible = PageId is "blueprints" or "assets" && _session.Project is not null;
    }

    private void CloseUx3GOverlay()
    {
        if (_assetManifestUx3G is not null) _assetManifestUx3G.Close();
    }

    internal bool PrepareUx3GEvidence(string mode)
    {
        SwitchPage("assets", notify: false);
        CloseUx3AOverlay();
        CloseUx3BOverlay();
        CloseUx3COverlay();
        CloseUx3DOverlay();
        CloseUx3EOverlay();
        CloseUx3FOverlay();
        CloseUx3HOverlay();
        CloseUx3IOverlay();
        _assetManifestUx3G.Open();
        return _assetManifestUx3G.PrepareEvidence(mode);
    }

    internal void ReleaseUx3GAutomatedEvidence() => _assetManifestUx3G.CloseEvidence();

    internal bool RunUx3GSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _assetManifestUx3G.IsOpen;
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
        var approval = _session.CaptureManifestApprovalUx3G();
        var root = _session.Session?.RootDirectory;
        var originalStore = root is null ? null : ReadStoreUx3G(root);

        var uiTransaction = _assetManifestUx3G.RunUiTransactionEvidence();
        var prepared = PrepareUx3GEvidence("ux3-g-asset-manifest");
        var uiReady = prepared && _assetManifestUx3G.IsOpen &&
                      _assetManifestUx3G.UsesIsolatedEvidence &&
                      (_assetManifestUx3G.UsesRealModuleEvidence ^
                       _assetManifestUx3G.UsesCanonicalFallbackEvidence) &&
                      _assetManifestUx3G.HasExplicitSelection &&
                      _assetManifestUx3G.ManifestPreviewReady &&
                      _assetManifestUx3G.ShowsHumanSurface &&
                      _assetManifestUx3G.EvidenceSourceUntouched;
        _assetManifestUx3G.CloseEvidence();
        var released = _assetManifestUx3G.EvidenceResourcesReleased;
        var sourceRestored = _assetManifestUx3G.SourceSessionRestored;
        var contract = _session.RunUx3GContractSmoke();
        var contractReleased = _session.LastUx3GTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _assetManifestUx3G.RestoreSourceSurfaceAfterSmoke();
        else _assetManifestUx3G.HideSourceSurfaceAfterSmoke();
        _session.RestoreManifestApprovalUx3G(approval);
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
                              _session.CaptureManifestApprovalUx3G() == approval &&
                              (root is null ? null : ReadStoreUx3G(root)) == originalStore;
        var surfaceRestored = PageId == originalPage && _assetManifestUx3G.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && contractReleased &&
                    sourceRestored && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_G_{(valid ? "PASS" : "FAILED")} " +
                 "routes=CONT-008,CONT-015,CONT-016,CONT-017 " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_manifest={contract.RealManifestLoaded.ToString().ToLowerInvariant()} " +
                 $"collision_navigation={contract.CollisionAndNavigation.ToString().ToLowerInvariant()} " +
                 $"animations={contract.AnimationLibrary.ToString().ToLowerInvariant()} " +
                 $"vfx_lod={contract.VfxAndLod.ToString().ToLowerInvariant()} " +
                 $"governance={contract.GovernanceValidated.ToString().ToLowerInvariant()} " +
                 $"platform_resolution={contract.PlatformResolution.ToString().ToLowerInvariant()} " +
                 $"vfx_plan={contract.VfxPlan.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"hidden_tails={contract.HiddenTailsPreserved.ToString().ToLowerInvariant()} " +
                 $"neighbor={contract.NeighborPreserved.ToString().ToLowerInvariant()} " +
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

    private static string? ReadStoreUx3G(string root)
    {
        var path = Path.Combine(root,
            AssetPackageManifestStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
