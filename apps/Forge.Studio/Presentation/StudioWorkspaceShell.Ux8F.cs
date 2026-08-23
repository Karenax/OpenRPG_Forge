using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioSupplyChainEvidenceWorkbenchUx8F _supplyChainEvidenceUx8F = null!;
    private Button _supplyChainEvidenceButtonUx8F = null!;

    private void BuildUx8FActions(HBoxContainer headingRow)
    {
        _supplyChainEvidenceButtonUx8F = StudioStyle.Button("Dépendances & notices", compact: true);
        _supplyChainEvidenceButtonUx8F.TooltipText =
            "Approbations transactionnelles, notices tierces, crédits et frontières IP.";
        _supplyChainEvidenceButtonUx8F.Pressed += OpenSupplyChainEvidenceUx8F;
        headingRow.AddChild(_supplyChainEvidenceButtonUx8F);
    }

    private void InitializeUx8FOverlay()
    {
        _supplyChainEvidenceUx8F = new StudioSupplyChainEvidenceWorkbenchUx8F(_session);
        _supplyChainEvidenceUx8F.StatusChanged += SetStatus;
        AddChild(_supplyChainEvidenceUx8F);
    }

    private void OpenSupplyChainEvidenceUx8F()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant l’inspection de la chaîne de contenu.", error: true);
            return;
        }
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8IOverlay();
        _supplyChainEvidenceUx8F.Open();
        SetStatus("UX8-F · dépendances prévisualisables, notices, crédits et frontières IP sans publication.");
    }

    private void UpdateUx8FActionState()
    {
        if (_supplyChainEvidenceButtonUx8F is not null)
            _supplyChainEvidenceButtonUx8F.Visible = PageId == "production" && _session.Project is not null;
    }

    private void CloseUx8FOverlay()
    {
        if (_supplyChainEvidenceUx8F is not null) _supplyChainEvidenceUx8F.Close();
    }

    internal bool PrepareUx8FEvidence(string mode)
    {
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8IOverlay();
        return _supplyChainEvidenceUx8F.PrepareEvidence(mode);
    }

    internal bool RunUx8FSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _supplyChainEvidenceUx8F.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureDependencyApprovalUx8F();

        var prepared = PrepareUx8FEvidence("ux8-f-supply-chain-evidence");
        var uiEvidence = prepared && _supplyChainEvidenceUx8F.RunUiEvidence();
        var selectionReady = _supplyChainEvidenceUx8F.HasExplicitSelection;
        var human = _supplyChainEvidenceUx8F.ShowsHumanSurface;
        var navigation = _supplyChainEvidenceUx8F.NavigationReady;
        var cancellation = _supplyChainEvidenceUx8F.CancellationReady;
        var released = _supplyChainEvidenceUx8F.EvidenceResourcesReleased;
        var uiReady = prepared && uiEvidence && _supplyChainEvidenceUx8F.IsOpen && selectionReady &&
                      _supplyChainEvidenceUx8F.FourResultsReady && navigation && cancellation && released && human;
        var contract = _session.RunUx8FContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _supplyChainEvidenceUx8F.RestoreSourceSurfaceAfterSmoke();
        else _supplyChainEvidenceUx8F.HideSourceSurfaceAfterSmoke();
        _session.RestoreDependencyApprovalUx8F(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureDependencyApprovalUx8F() == originalApproval;
        var surfaceRestored = PageId == originalPage && _supplyChainEvidenceUx8F.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_F_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selectionReady.ToString().ToLowerInvariant()} " +
                 $"dependency_approval={contract.DependencyApproval.ToString().ToLowerInvariant()} " +
                 $"third_party_notices={contract.ThirdPartyNotices.ToString().ToLowerInvariant()} " +
                 $"credits={contract.CreditsGeneration.ToString().ToLowerInvariant()} " +
                 $"ip_boundary={contract.IpBoundary.ToString().ToLowerInvariant()} " +
                 $"cancel={cancellation.ToString().ToLowerInvariant()} " +
                 $"logs={contract.LogsOnDemand.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"navigation={navigation.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.ResourcesReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8FAutomatedEvidence()
    {
        if (_supplyChainEvidenceUx8F is not null) _supplyChainEvidenceUx8F.Close();
    }
}
