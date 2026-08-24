using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioProductionEvidenceWorkbenchUx8E _productionEvidenceUx8E = null!;
    private Button _productionEvidenceButtonUx8E = null!;

    private void BuildUx8EActions(HBoxContainer headingRow)
    {
        _productionEvidenceButtonUx8E = StudioStyle.Button("Docs, tâches & release", compact: true);
        _productionEvidenceButtonUx8E.TooltipText =
            "Documentation générée, manifests, canaux de release et archive de crash bornée.";
        _productionEvidenceButtonUx8E.Pressed += OpenProductionEvidenceUx8E;
        headingRow.AddChild(_productionEvidenceButtonUx8E);
    }

    private void InitializeUx8EOverlay()
    {
        _productionEvidenceUx8E = new StudioProductionEvidenceWorkbenchUx8E(_session);
        _productionEvidenceUx8E.StatusChanged += SetStatus;
        AddChild(_productionEvidenceUx8E);
    }

    private void OpenProductionEvidenceUx8E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant l’inspection de production.", error: true);
            return;
        }
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8FOverlay();
        CloseUx8IOverlay();
        _productionEvidenceUx8E.Open();
        SetStatus("UX8-E · documentation, tâches, canaux et diagnostics inspectés sans publication.");
    }

    private void UpdateUx8EActionState()
    {
        if (_productionEvidenceButtonUx8E is not null)
            _productionEvidenceButtonUx8E.Visible = PageId == "production" && _session.Project is not null;
    }

    private void CloseUx8EOverlay()
    {
        if (_productionEvidenceUx8E is not null) _productionEvidenceUx8E.Close();
    }

    internal bool PrepareUx8EEvidence(string mode)
    {
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8AOverlay();
        CloseUx8BOverlay();
        CloseUx8COverlay();
        CloseUx8DOverlay();
        CloseUx8FOverlay();
        CloseUx8IOverlay();
        return _productionEvidenceUx8E.PrepareEvidence(mode);
    }

    internal bool RunUx8ESmoke()
    {
        var originalPage = PageId;
        var originalOpen = _productionEvidenceUx8E.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;

        var prepared = PrepareUx8EEvidence("ux8-e-production-evidence");
        var uiEvidence = prepared && _productionEvidenceUx8E.RunUiEvidence();
        var selectionReady = _productionEvidenceUx8E.HasExplicitSelection;
        var human = _productionEvidenceUx8E.ShowsHumanSurface;
        var navigation = _productionEvidenceUx8E.NavigationReady;
        var cancellation = _productionEvidenceUx8E.CancellationReady;
        var released = _productionEvidenceUx8E.EvidenceResourcesReleased;
        var uiReady = prepared && uiEvidence && _productionEvidenceUx8E.IsOpen && selectionReady &&
                      _productionEvidenceUx8E.FourResultsReady && navigation && cancellation && released && human;
        var contract = _session.RunUx8EContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _productionEvidenceUx8E.RestoreSourceSurfaceAfterSmoke();
        else _productionEvidenceUx8E.HideSourceSurfaceAfterSmoke();
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected;
        var surfaceRestored = PageId == originalPage && _productionEvidenceUx8E.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_E_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selectionReady.ToString().ToLowerInvariant()} " +
                 $"documentation={contract.DocumentationCheck.ToString().ToLowerInvariant()} " +
                 $"tasks={contract.TaskManifests.ToString().ToLowerInvariant()} " +
                 $"release_channels={contract.ReleaseChannels.ToString().ToLowerInvariant()} " +
                 $"crash_artifacts={contract.CrashArtifacts.ToString().ToLowerInvariant()} " +
                 $"cancel={cancellation.ToString().ToLowerInvariant()} " +
                 $"logs={contract.LogsOnDemand.ToString().ToLowerInvariant()} " +
                 $"navigation={navigation.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx8EAutomatedEvidence()
    {
        if (_productionEvidenceUx8E is not null) _productionEvidenceUx8E.Close();
    }
}
