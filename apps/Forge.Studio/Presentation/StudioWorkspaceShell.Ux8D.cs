using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRepositoryReadinessWorkbenchUx8D _repositoryReadinessUx8D = null!;
    private Button _repositoryReadinessButtonUx8D = null!;

    private void BuildUx8DActions(HBoxContainer headingRow)
    {
        _repositoryReadinessButtonUx8D = StudioStyle.Button("État dépôt & versions", compact: true);
        _repositoryReadinessButtonUx8D.TooltipText =
            "Dépôt Git, huit dossiers, versions sémantiques et ADR en lecture seule.";
        _repositoryReadinessButtonUx8D.Pressed += OpenRepositoryReadinessUx8D;
        headingRow.AddChild(_repositoryReadinessButtonUx8D);
    }

    private void InitializeUx8DOverlay()
    {
        _repositoryReadinessUx8D = new StudioRepositoryReadinessWorkbenchUx8D(_session);
        _repositoryReadinessUx8D.StatusChanged += SetStatus;
        AddChild(_repositoryReadinessUx8D);
    }

    private void OpenRepositoryReadinessUx8D()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant l’inspection du dépôt.", error: true);
            return;
        }
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8IOverlay();
        _repositoryReadinessUx8D.Open();
        SetStatus("UX8-D · dépôt, structure, versions et ADR inspectés sans commande Git ni écriture.");
    }

    private void UpdateUx8DActionState()
    {
        if (_repositoryReadinessButtonUx8D is not null)
            _repositoryReadinessButtonUx8D.Visible = PageId == "production" && _session.Project is not null;
    }

    private void CloseUx8DOverlay()
    {
        if (_repositoryReadinessUx8D is not null) _repositoryReadinessUx8D.Close();
    }

    internal bool PrepareUx8DEvidence(string mode)
    {
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8IOverlay();
        return _repositoryReadinessUx8D.PrepareEvidence(mode);
    }

    internal bool RunUx8DSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _repositoryReadinessUx8D.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var prepared = PrepareUx8DEvidence("ux8-d-repository-readiness");
        var uiEvidence = prepared && _repositoryReadinessUx8D.RunUiEvidence();
        var selectionReady = _repositoryReadinessUx8D.HasExplicitSelection;
        var human = _repositoryReadinessUx8D.ShowsHumanSurface;
        var navigation = _repositoryReadinessUx8D.NavigationReady;
        var cancellation = _repositoryReadinessUx8D.CancellationReady;
        var uiReady = prepared && uiEvidence && _repositoryReadinessUx8D.IsOpen && selectionReady &&
                      _repositoryReadinessUx8D.FourResultsReady && navigation && cancellation && human;
        var contract = _session.RunUx8DContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _repositoryReadinessUx8D.RestoreSourceSurfaceAfterSmoke();
        else _repositoryReadinessUx8D.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var surfaceRestored = PageId == originalPage && _repositoryReadinessUx8D.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_D_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selectionReady.ToString().ToLowerInvariant()} " +
                 $"git={contract.CanonicalRepository.ToString().ToLowerInvariant()} " +
                 $"layout={contract.StandardLayout.ToString().ToLowerInvariant()} " +
                 $"semver={contract.SemanticVersioning.ToString().ToLowerInvariant()} " +
                 $"adrs={contract.ArchitectureDecisions.ToString().ToLowerInvariant()} " +
                 $"cancel={cancellation.ToString().ToLowerInvariant()} " +
                 $"logs={contract.LogsOnDemand.ToString().ToLowerInvariant()} " +
                 $"navigation={navigation.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8DAutomatedEvidence()
    {
        if (_repositoryReadinessUx8D is not null) _repositoryReadinessUx8D.Close();
    }
}
