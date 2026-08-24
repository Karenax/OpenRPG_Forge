using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioSecurityWorkersWorkbenchUx8J _securityWorkersUx8J = null!;
    private Button _securityWorkersButtonUx8J = null!;

    private void BuildUx8JActions(HBoxContainer headingRow)
    {
        _securityWorkersButtonUx8J = StudioStyle.Button("Sécurité workers & IA", compact: true);
        _securityWorkersButtonUx8J.TooltipText =
            "Confiner les workers externes et tester la protection contre l’injection de prompt.";
        _securityWorkersButtonUx8J.Pressed += OpenSecurityWorkersUx8J;
        headingRow.AddChild(_securityWorkersButtonUx8J);
    }

    private void InitializeUx8JOverlay()
    {
        _securityWorkersUx8J = new StudioSecurityWorkersWorkbenchUx8J(_session);
        _securityWorkersUx8J.StatusChanged += SetStatus;
        AddChild(_securityWorkersUx8J);
    }

    private void OpenSecurityWorkersUx8J()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer sa sécurité.", error: true);
            return;
        }
        if (PageId != "security") SwitchPage("security", notify: false);
        CloseUx8JOverlay();
        _securityWorkersUx8J.Open();
        SetStatus("UX8-J · workers confinés et sonde anti-injection.");
    }

    private void UpdateUx8JActionState()
    {
        if (_securityWorkersButtonUx8J is not null)
            _securityWorkersButtonUx8J.Visible = PageId == "security" && _session.Project is not null;
    }

    private void CloseUx8JOverlay()
    {
        if (_securityWorkersUx8J is not null) _securityWorkersUx8J.Close();
    }

    internal bool PrepareUx8JEvidence(string mode)
    {
        if (PageId != "security") SwitchPage("security", notify: false);
        return _securityWorkersUx8J.PrepareEvidence(mode);
    }

    internal bool RunUx8JSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _securityWorkersUx8J.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureSecurityApprovalUx8J();

        var prepared = PrepareUx8JEvidence("ux8-j-security-workers");
        var uiEvidence = prepared && _securityWorkersUx8J.RunUiEvidence();
        var selection = _securityWorkersUx8J.HasExplicitSelection;
        var human = _securityWorkersUx8J.ShowsHumanSurface;
        var uiReady = prepared && uiEvidence && _securityWorkersUx8J.IsOpen && selection && human &&
                      _securityWorkersUx8J.WorkerPreviewReady && _securityWorkersUx8J.InjectionPreviewReady;
        var contract = _session.RunUx8JContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _securityWorkersUx8J.RestoreSourceSurfaceAfterSmoke();
        else _securityWorkersUx8J.HideSourceSurfaceAfterSmoke();
        _session.RestoreSecurityApprovalUx8J(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureSecurityApprovalUx8J() == originalApproval;
        var surfaceRestored = PageId == originalPage && _securityWorkersUx8J.IsOpen == originalOpen;
        var resourcesReleased = _session.LastUx8JTemporaryReleased;
        var valid = contract.Succeeded && resourcesReleased && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_J_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.TwoRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={selection.ToString().ToLowerInvariant()} " +
                 $"scope={contract.ScopeBounded.ToString().ToLowerInvariant()} " +
                 $"consent={contract.ConsentRequired.ToString().ToLowerInvariant()} " +
                 $"python_denied={contract.PythonShellEvalNetworkDenied.ToString().ToLowerInvariant()} " +
                 $"injection={contract.PromptInjectionDetected.ToString().ToLowerInvariant()} " +
                 $"escalation={contract.ToolAndEscalationDenied.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"audit={contract.AuditRecorded.ToString().ToLowerInvariant()} " +
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

    internal void ReleaseUx8JAutomatedEvidence()
    {
        if (_securityWorkersUx8J is not null) _securityWorkersUx8J.Close();
    }
}
