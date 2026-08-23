using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRepositorySyncWorkbenchUx8L _repositorySyncUx8L = null!;
    private Button _repositorySyncButtonUx8L = null!;

    private void BuildUx8LActions(HBoxContainer headingRow)
    {
        _repositorySyncButtonUx8L = StudioStyle.Button("Dépôt & NWSync", compact: true);
        _repositorySyncButtonUx8L.TooltipText =
            "Configurer la synchronisation, la confiance, les licences et les métadonnées NWSync.";
        _repositorySyncButtonUx8L.Pressed += OpenRepositorySyncUx8L;
        headingRow.AddChild(_repositorySyncButtonUx8L);
    }

    private void InitializeUx8LOverlay()
    {
        _repositorySyncUx8L = new StudioRepositorySyncWorkbenchUx8L(_session);
        _repositorySyncUx8L.StatusChanged += SetStatus;
        _repositorySyncUx8L.WorkspaceChanged += RefreshAfterUx8LMutation;
        AddChild(_repositorySyncUx8L);
    }

    private void OpenRepositorySyncUx8L()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son dépôt.", error: true);
            return;
        }
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8GOverlay();
        CloseUx8NOverlay();
        _repositorySyncUx8L.Open();
        SetStatus("UX8-L · synchronisation automatique et métadonnées NWSync.");
    }

    private void RefreshAfterUx8LMutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx8LActionState();
    }

    private void UpdateUx8LActionState()
    {
        if (_repositorySyncButtonUx8L is not null)
            _repositorySyncButtonUx8L.Visible = PageId == "packages" && _session.Project is not null;
    }

    private void CloseUx8LOverlay()
    {
        if (_repositorySyncUx8L is not null) _repositorySyncUx8L.Close();
    }

    internal bool PrepareUx8LEvidence(string mode)
    {
        if (PageId != "packages") SwitchPage("packages", notify: false);
        CloseUx8GOverlay();
        CloseUx8NOverlay();
        return _repositorySyncUx8L.PrepareEvidence(mode);
    }

    internal bool RunUx8LSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _repositorySyncUx8L.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var approval = _session.CaptureSyncApprovalUx8L();
        var packageApproval = _session.CapturePackageApprovalUx8G();

        var prepared = PrepareUx8LEvidence("ux8-l-repository-sync");
        var selection = prepared && _repositorySyncUx8L.HasExplicitSelection;
        var human = prepared && _repositorySyncUx8L.ShowsHumanSurface;
        var preview = prepared && _repositorySyncUx8L.PreviewReady;
        var sourceUntouched = prepared && _repositorySyncUx8L.EvidenceSourceUntouched;
        var uiEvidence = prepared && _repositorySyncUx8L.RunUiEvidence();
        _repositorySyncUx8L.CloseEvidence();
        var released = _repositorySyncUx8L.EvidenceResourcesReleased;
        var sourceRestored = _repositorySyncUx8L.SourceSessionRestored;
        var contract = _session.RunUx8LContractSmoke();
        var contractReleased = _session.LastUx8LTemporaryReleased;

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _repositorySyncUx8L.RestoreSourceSurfaceAfterSmoke();
        else _repositorySyncUx8L.HideSourceSurfaceAfterSmoke();
        _session.RestoreSyncApprovalUx8L(approval);
        _session.RestorePackageApprovalUx8G(packageApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureSyncApprovalUx8L() == approval &&
                              _session.CapturePackageApprovalUx8G() == packageApproval;
        var surfaceRestored = PageId == originalPage && _repositorySyncUx8L.IsOpen == originalOpen;
        var uiReady = prepared && selection && human && preview && sourceUntouched && uiEvidence && released && sourceRestored;
        var valid = contract.Succeeded && contractReleased && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_L_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.TwoRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"consent={contract.ConsentRequired.ToString().ToLowerInvariant()} " +
                 $"scope={contract.ScopeConfined.ToString().ToLowerInvariant()} " +
                 $"manifest={contract.ManifestValidated.ToString().ToLowerInvariant()} " +
                 $"trust_license={contract.TrustAndLicenseBlocked.ToString().ToLowerInvariant()} " +
                 $"tamper={contract.TamperBlocked.ToString().ToLowerInvariant()} " +
                 $"resume_reuse={contract.ResumeAndReuse.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contractReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows_count=1");
        return valid;
    }

    internal void ReleaseUx8LAutomatedEvidence()
    {
        if (_repositorySyncUx8L is not null) _repositorySyncUx8L.CloseEvidence();
    }
}
