using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCompatibilityMigrationWorkbenchUx8I _compatibilityMigrationUx8I = null!;
    private Button _compatibilityMigrationButtonUx8I = null!;

    private void BuildUx8IActions(HBoxContainer headingRow)
    {
        _compatibilityMigrationButtonUx8I = StudioStyle.Button("Compatibilité", compact: true);
        _compatibilityMigrationButtonUx8I.TooltipText =
            "Versions projet, migrations automatiques et compatibilité réelle des assets par plateforme.";
        _compatibilityMigrationButtonUx8I.Pressed += OpenCompatibilityMigrationUx8I;
        headingRow.AddChild(_compatibilityMigrationButtonUx8I);
    }

    private void InitializeUx8IOverlay()
    {
        _compatibilityMigrationUx8I = new StudioCompatibilityMigrationWorkbenchUx8I(_session);
        _compatibilityMigrationUx8I.StatusChanged += SetStatus;
        AddChild(_compatibilityMigrationUx8I);
    }

    private void OpenCompatibilityMigrationUx8I()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de vérifier sa compatibilité.", error: true);
            return;
        }
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        CloseUx8IOverlay();
        _compatibilityMigrationUx8I.Open();
        SetStatus("UX8-I · compatibilité projet, migrations et variantes d’assets par plateforme.");
    }

    private void UpdateUx8IActionState()
    {
        if (_compatibilityMigrationButtonUx8I is not null)
            _compatibilityMigrationButtonUx8I.Visible = PageId == "production" && _session.Project is not null;
    }

    private void CloseUx8IOverlay()
    {
        if (_compatibilityMigrationUx8I is not null) _compatibilityMigrationUx8I.Close();
    }

    internal bool PrepareUx8IEvidence(string mode)
    {
        if (PageId != "production") SwitchPage("production", notify: false);
        CloseUx8DOverlay();
        CloseUx8EOverlay();
        CloseUx8FOverlay();
        return _compatibilityMigrationUx8I.PrepareEvidence(mode);
    }

    internal bool RunUx8ISmoke()
    {
        var originalPage = PageId;
        var originalOpen = _compatibilityMigrationUx8I.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureCompatibilityApprovalUx8I();

        var prepared = PrepareUx8IEvidence("ux8-i-compatibility-migrations");
        var uiEvidence = prepared && _compatibilityMigrationUx8I.RunUiEvidence();
        var selection = _compatibilityMigrationUx8I.HasExplicitSelection;
        var human = _compatibilityMigrationUx8I.ShowsHumanSurface;
        var uiReady = prepared && uiEvidence && _compatibilityMigrationUx8I.IsOpen && selection && human &&
                      _compatibilityMigrationUx8I.PreviewReady && _compatibilityMigrationUx8I.PlatformReady;
        var contract = _session.RunUx8IContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _compatibilityMigrationUx8I.RestoreSourceSurfaceAfterSmoke();
        else _compatibilityMigrationUx8I.HideSourceSurfaceAfterSmoke();
        _session.RestoreCompatibilityApprovalUx8I(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureCompatibilityApprovalUx8I() == originalApproval;
        var surfaceRestored = PageId == originalPage && _compatibilityMigrationUx8I.IsOpen == originalOpen;
        var resourcesReleased = _session.LastUx8ITemporaryReleased;
        var valid = contract.Succeeded && resourcesReleased && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_I_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.TwoRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"compatibility={contract.CompatibilityValidated.ToString().ToLowerInvariant()} " +
                 $"runtime_gate={contract.RuntimeRequiredGate.ToString().ToLowerInvariant()} " +
                 $"migration={contract.MigrationDryRun.ToString().ToLowerInvariant()} " +
                 $"platform={contract.PlatformExactOrFallback.ToString().ToLowerInvariant()} " +
                 $"hashes={contract.PlatformHashes.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"resources_released={resourcesReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} human={human.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows_count=1");
        return valid;
    }

    internal void ReleaseUx8IAutomatedEvidence()
    {
        if (_compatibilityMigrationUx8I is not null) _compatibilityMigrationUx8I.Close();
    }
}
