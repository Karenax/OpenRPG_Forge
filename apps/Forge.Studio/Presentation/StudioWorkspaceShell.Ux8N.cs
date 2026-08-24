using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioPackageLifecycleWorkbenchUx8N _packageLifecycleUx8N = null!;
    private Button _packageLifecycleButtonUx8N = null!;

    private void BuildUx8NActions(HBoxContainer headingRow)
    {
        _packageLifecycleButtonUx8N = StudioStyle.Button("Registre & cycle de vie", compact: true);
        _packageLifecycleButtonUx8N.TooltipText =
            "Packs curatés, espaces de noms, migration déclarative et désinstallation sûre.";
        _packageLifecycleButtonUx8N.Pressed += OpenPackageLifecycleUx8N;
        headingRow.AddChild(_packageLifecycleButtonUx8N);
    }

    private void InitializeUx8NOverlay()
    {
        _packageLifecycleUx8N = new StudioPackageLifecycleWorkbenchUx8N(_session);
        _packageLifecycleUx8N.StatusChanged += SetStatus;
        _packageLifecycleUx8N.WorkspaceChanged += RefreshAfterUx8NMutation;
        AddChild(_packageLifecycleUx8N);
    }

    private void OpenPackageLifecycleUx8N()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de gérer son registre et son cycle de vie.", error: true);
            return;
        }
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8GOverlay();
        CloseUx8LOverlay();
        _packageLifecycleUx8N.Open();
        SetStatus("UX8-N · registre de ressources, migration déclarative et désinstallation sûre.");
    }

    private void RefreshAfterUx8NMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx8NActionState();
    }

    private void UpdateUx8NActionState()
    {
        if (_packageLifecycleButtonUx8N is not null)
            _packageLifecycleButtonUx8N.Visible = PageId == "packages" && _session.Project is not null;
    }

    private void CloseUx8NOverlay()
    {
        if (_packageLifecycleUx8N is not null) _packageLifecycleUx8N.Close();
    }

    internal bool PrepareUx8NEvidence(string mode)
    {
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8GOverlay();
        CloseUx8LOverlay();
        return _packageLifecycleUx8N.PrepareEvidence(mode);
    }

    internal bool RunUx8NSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _packageLifecycleUx8N.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CapturePackageLifecycleApprovalUx8N();

        var prepared = PrepareUx8NEvidence("ux8-n-package-lifecycle");
        var selection = prepared && _packageLifecycleUx8N.HasExplicitSelection;
        var human = prepared && _packageLifecycleUx8N.ShowsHumanSurface;
        var preview = prepared && _packageLifecycleUx8N.PreviewReady;
        var sourceUntouched = prepared && _packageLifecycleUx8N.EvidenceSourceUntouched;
        var uiEvidence = prepared && _packageLifecycleUx8N.RunUiEvidence();
        _packageLifecycleUx8N.CloseEvidence();
        var released = _packageLifecycleUx8N.EvidenceResourcesReleased;
        var sourceRestored = _packageLifecycleUx8N.SourceSessionRestored;
        var contract = _session.RunUx8NContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _packageLifecycleUx8N.RestoreSourceSurfaceAfterSmoke();
        else _packageLifecycleUx8N.HideSourceSurfaceAfterSmoke();
        _session.RestorePackageLifecycleApprovalUx8N(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CapturePackageLifecycleApprovalUx8N() == originalApproval;
        var surfaceRestored = PageId == originalPage && _packageLifecycleUx8N.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released &&
                      sourceRestored;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_N_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"curated={contract.CuratedGovernance.ToString().ToLowerInvariant()} " +
                 $"namespaces={contract.ResourceNamespaces.ToString().ToLowerInvariant()} " +
                 $"migration={contract.MigrationLifecycle.ToString().ToLowerInvariant()} " +
                 $"uninstall={contract.SafeUninstall.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRuns.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.ResourcesReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8NAutomatedEvidence()
    {
        if (_packageLifecycleUx8N is not null) _packageLifecycleUx8N.CloseEvidence();
    }
}
