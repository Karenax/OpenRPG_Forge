using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioContentPackagesWorkbenchUx8G _contentPackagesUx8G = null!;
    private Button _contentPackagesButtonUx8G = null!;

    private void BuildUx8GActions(HBoxContainer headingRow)
    {
        _contentPackagesButtonUx8G = StudioStyle.Button("Packages & dépendances", compact: true);
        _contentPackagesButtonUx8G.TooltipText =
            "Catalogue versionné, priorités, dépendances et compilation en mémoire.";
        _contentPackagesButtonUx8G.Pressed += OpenContentPackagesUx8G;
        headingRow.AddChild(_contentPackagesButtonUx8G);
    }

    private void InitializeUx8GOverlay()
    {
        _contentPackagesUx8G = new StudioContentPackagesWorkbenchUx8G(_session);
        _contentPackagesUx8G.StatusChanged += SetStatus;
        AddChild(_contentPackagesUx8G);
    }

    private void OpenContentPackagesUx8G()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de gérer ses packages.", error: true);
            return;
        }
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8LOverlay();
        CloseUx8NOverlay();
        _contentPackagesUx8G.Open();
        SetStatus("UX8-G · packages versionnés, ordre, dépendances et compilation sans installation.");
    }

    private void UpdateUx8GActionState()
    {
        if (_contentPackagesButtonUx8G is not null)
            _contentPackagesButtonUx8G.Visible = PageId == "packages" && _session.Project is not null;
    }

    private void CloseUx8GOverlay()
    {
        if (_contentPackagesUx8G is not null) _contentPackagesUx8G.Close();
    }

    internal bool PrepareUx8GEvidence(string mode)
    {
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8LOverlay();
        CloseUx8NOverlay();
        return _contentPackagesUx8G.PrepareEvidence(mode);
    }

    internal bool RunUx8GSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _contentPackagesUx8G.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CapturePackageApprovalUx8G();

        var prepared = PrepareUx8GEvidence("ux8-g-content-packages");
        var uiEvidence = prepared && _contentPackagesUx8G.RunUiEvidence();
        var selection = _contentPackagesUx8G.HasExplicitSelection;
        var human = _contentPackagesUx8G.ShowsHumanSurface;
        var navigation = _contentPackagesUx8G.NavigationReady;
        var uiReady = prepared && uiEvidence && _contentPackagesUx8G.IsOpen && selection && human && navigation &&
                      _contentPackagesUx8G.PreviewReady && _contentPackagesUx8G.EvidenceResourcesReleased;
        var contract = _session.RunUx8GContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _contentPackagesUx8G.RestoreSourceSurfaceAfterSmoke();
        else _contentPackagesUx8G.HideSourceSurfaceAfterSmoke();
        _session.RestorePackageApprovalUx8G(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CapturePackageApprovalUx8G() == originalApproval;
        var surfaceRestored = PageId == originalPage && _contentPackagesUx8G.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_G_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"versioned={contract.VersionedPackages.ToString().ToLowerInvariant()} " +
                 $"priority={contract.PriorityResolution.ToString().ToLowerInvariant()} " +
                 $"dependencies={contract.Dependencies.ToString().ToLowerInvariant()} " +
                 $"compilation={contract.Compilation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={contract.ResourcesReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8GAutomatedEvidence()
    {
        if (_contentPackagesUx8G is not null) _contentPackagesUx8G.Close();
    }
}
