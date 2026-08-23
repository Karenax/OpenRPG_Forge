using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioQualityLabWorkbenchUx7B _qualityLabUx7B = null!;
    private Button _qualityLabButtonUx7B = null!;

    private void BuildUx7BActions(HBoxContainer headingRow)
    {
        _qualityLabButtonUx7B = StudioStyle.Button("Laboratoire qualité", compact: true);
        _qualityLabButtonUx7B.TooltipText =
            "Lancer quatre contrôles isolés, annulables et navigables puis gouverner leur profil.";
        _qualityLabButtonUx7B.Pressed += OpenQualityLabUx7B;
        headingRow.AddChild(_qualityLabButtonUx7B);
    }

    private void InitializeUx7BOverlay()
    {
        _qualityLabUx7B = new StudioQualityLabWorkbenchUx7B(_session);
        _qualityLabUx7B.StatusChanged += SetStatus;
        AddChild(_qualityLabUx7B);
    }

    private void OpenQualityLabUx7B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser le laboratoire qualité.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _qualityLabUx7B.Open();
        SetStatus("UX7-B · laboratoire qualité isolé.");
    }

    private void UpdateUx7BActionState()
    {
        if (_qualityLabButtonUx7B is not null)
            _qualityLabButtonUx7B.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7BOverlay()
    {
        if (_qualityLabUx7B is not null) _qualityLabUx7B.Close();
    }

    internal bool PrepareUx7BEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        CloseUx7IOverlay();
        _qualityLabUx7B.Open();
        return _qualityLabUx7B.PrepareEvidence(mode);
    }

    internal bool RunUx7BSmoke()
    {
        var originalPage = PageId;
        var originalAOpen = _canonicalValidationUx7A.IsOpen;
        var originalBOpen = _qualityLabUx7B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalAApproval = _session.CaptureValidationApprovalUx7A();
        var originalBApproval = _session.CaptureQualityFoundationApprovalUx7B();

        var uiTransaction = _qualityLabUx7B.RunUiTransactionEvidence();
        var prepared = PrepareUx7BEvidence("ux7-b-quality-lab");
        var uiReady = prepared && _qualityLabUx7B.IsOpen && _qualityLabUx7B.UsesIsolatedEvidence &&
                      _qualityLabUx7B.HasExplicitSelection && _qualityLabUx7B.CanonicalPreviewReady &&
                      _qualityLabUx7B.FourResultsReady && _qualityLabUx7B.ResultNavigationReady &&
                      _qualityLabUx7B.CancellationReady && _qualityLabUx7B.ShowsHumanSurface &&
                      _qualityLabUx7B.EvidenceSourceUntouched;
        _qualityLabUx7B.CloseEvidence();
        var released = _qualityLabUx7B.EvidenceResourcesReleased;
        var sourceRestored = _qualityLabUx7B.SourceSessionRestored;
        var contract = _session.RunUx7BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalAOpen) _canonicalValidationUx7A.RestoreSourceSurfaceAfterSmoke();
        if (originalBOpen) _qualityLabUx7B.RestoreSourceSurfaceAfterSmoke();
        else _qualityLabUx7B.HideSourceSurfaceAfterSmoke();
        _session.RestoreValidationApprovalUx7A(originalAApproval);
        _session.RestoreQualityFoundationApprovalUx7B(originalBApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureValidationApprovalUx7A() == originalAApproval &&
                              _session.CaptureQualityFoundationApprovalUx7B() == originalBApproval;
        var surfaceRestored = PageId == originalPage &&
                              _canonicalValidationUx7A.IsOpen == originalAOpen &&
                              _qualityLabUx7B.IsOpen == originalBOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_B_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.FourRoutes.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"domain={contract.DomainHarness.ToString().ToLowerInvariant()} " +
                 $"headless={contract.HeadlessRuntime.ToString().ToLowerInvariant()} " +
                 $"saves={contract.SaveCoverage.ToString().ToLowerInvariant()} " +
                 $"logs={contract.StructuredLogs.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={contract.Navigation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"invalid={contract.InvalidProfileBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"transaction={contract.ApplyUndoRedo.ToString().ToLowerInvariant()} " +
                 $"persistence={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"licenses={contract.LicenseTailPreserved.ToString().ToLowerInvariant()} " +
                 $"validation={contract.ValidationTailPreserved.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7BAutomatedEvidence()
    {
        if (_qualityLabUx7B is not null) _qualityLabUx7B.CloseEvidence();
    }
}
