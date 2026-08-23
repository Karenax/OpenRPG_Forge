using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDistributionReleaseWorkbenchUx8H _distributionReleaseUx8H = null!;
    private Button _distributionReleaseButtonUx8H = null!;

    private void BuildUx8HActions(HBoxContainer headingRow)
    {
        _distributionReleaseButtonUx8H = StudioStyle.Button("Cibles & livraison", compact: true);
        _distributionReleaseButtonUx8H.TooltipText =
            "Cibles de build, plateformes, packaging Windows, signatures et mises à jour delta.";
        _distributionReleaseButtonUx8H.Pressed += OpenDistributionReleaseUx8H;
        headingRow.AddChild(_distributionReleaseButtonUx8H);
    }

    private void InitializeUx8HOverlay()
    {
        _distributionReleaseUx8H = new StudioDistributionReleaseWorkbenchUx8H(_session);
        _distributionReleaseUx8H.StatusChanged += SetStatus;
        AddChild(_distributionReleaseUx8H);
    }

    private void OpenDistributionReleaseUx8H()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa livraison.", error: true);
            return;
        }
        if (PageId != "export") SwitchPage("export", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8GOverlay();
        CloseUx8KOverlay();
        _distributionReleaseUx8H.Open();
        SetStatus("UX8-H · cibles, plateformes, packaging, signatures et deltas sans publication.");
    }

    private void UpdateUx8HActionState()
    {
        if (_distributionReleaseButtonUx8H is not null)
            _distributionReleaseButtonUx8H.Visible = PageId == "export" && _session.Project is not null;
    }

    private void CloseUx8HOverlay()
    {
        if (_distributionReleaseUx8H is not null) _distributionReleaseUx8H.Close();
    }

    internal bool PrepareUx8HEvidence(string mode)
    {
        if (PageId != "export") SwitchPage("export", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8GOverlay();
        CloseUx8KOverlay();
        return _distributionReleaseUx8H.PrepareEvidence(mode);
    }

    internal bool RunUx8HSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _distributionReleaseUx8H.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureDistributionApprovalUx8H();

        var prepared = PrepareUx8HEvidence("ux8-h-distribution-release");
        var uiEvidence = prepared && _distributionReleaseUx8H.RunUiEvidence();
        var selection = _distributionReleaseUx8H.HasExplicitSelection;
        var human = _distributionReleaseUx8H.ShowsHumanSurface;
        var navigation = _distributionReleaseUx8H.NavigationReady;
        var uiReady = prepared && uiEvidence && _distributionReleaseUx8H.IsOpen && selection && human &&
                      navigation && _distributionReleaseUx8H.PreviewReady &&
                      _distributionReleaseUx8H.EvidenceResourcesReleased;
        var contract = _session.RunUx8HContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _distributionReleaseUx8H.RestoreSourceSurfaceAfterSmoke();
        else _distributionReleaseUx8H.HideSourceSurfaceAfterSmoke();
        _session.RestoreDistributionApprovalUx8H(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureDistributionApprovalUx8H() == originalApproval;
        var surfaceRestored = PageId == originalPage && _distributionReleaseUx8H.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_H_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FiveRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"targets={contract.TargetProfiles.ToString().ToLowerInvariant()} " +
                 $"platforms={contract.MultiPlatform.ToString().ToLowerInvariant()} " +
                 $"windows={contract.WindowsPackaging.ToString().ToLowerInvariant()} " +
                 $"signature={contract.SignatureRoundTrip.ToString().ToLowerInvariant()} " +
                 $"delta={contract.DeltaRoundTrip.ToString().ToLowerInvariant()} " +
                 $"bundles={contract.DeterministicBundles.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stable_blocked={contract.StablePolicyBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={contract.ResourcesReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows_count=1");
        return valid;
    }

    internal void ReleaseUx8HAutomatedEvidence()
    {
        if (_distributionReleaseUx8H is not null) _distributionReleaseUx8H.Close();
    }
}
