using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioObservabilityWorkbenchUx7I _observabilityUx7I = null!;
    private Button _observabilityButtonUx7I = null!;

    private void BuildUx7IActions(HBoxContainer headingRow)
    {
        _observabilityButtonUx7I = StudioStyle.Button("Tracy et OpenTelemetry", compact: true);
        _observabilityButtonUx7I.TooltipText =
            "Configurer l’observabilité et lancer une capture OpenTelemetry isolée; Tracy natif exige un moteur instrumenté.";
        _observabilityButtonUx7I.Pressed += OpenObservabilityUx7I;
        headingRow.AddChild(_observabilityButtonUx7I);
    }

    private void InitializeUx7IOverlay()
    {
        _observabilityUx7I = new StudioObservabilityWorkbenchUx7I(_session);
        _observabilityUx7I.StatusChanged += SetStatus;
        AddChild(_observabilityUx7I);
    }

    private void OpenObservabilityUx7I()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son observabilité.", error: true);
            return;
        }
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        _observabilityUx7I.Open();
        SetStatus("UX7-I · Tracy natif explicite, fallback OpenTelemetry réellement capturable dans un isolat.");
    }

    private void UpdateUx7IActionState()
    {
        if (_observabilityButtonUx7I is not null)
            _observabilityButtonUx7I.Visible = PageId == "quality" && _session.Project is not null;
    }

    private void CloseUx7IOverlay()
    {
        if (_observabilityUx7I is not null) _observabilityUx7I.Close();
    }

    internal bool PrepareUx7IEvidence(string mode)
    {
        if (PageId != "quality") SwitchPage("quality", notify: false);
        CloseUx7BOverlay();
        CloseUx7COverlay();
        CloseUx7DOverlay();
        CloseUx7EOverlay();
        CloseUx7HOverlay();
        _observabilityUx7I.Open();
        return _observabilityUx7I.PrepareEvidence(mode);
    }

    internal bool RunUx7ISmoke()
    {
        var originalPage = PageId;
        var originalOpen = _observabilityUx7I.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureObservabilityApprovalUx7I();

        var uiTransaction = _observabilityUx7I.RunUiTransactionEvidence();
        var prepared = _observabilityUx7I.PrepareEvidence("ux7-i-observability");
        var uiReady = prepared && _observabilityUx7I.IsOpen &&
                      _observabilityUx7I.HasExplicitSelection &&
                      _observabilityUx7I.CanonicalPreviewReady &&
                      _observabilityUx7I.OpenTelemetryResultReady &&
                      _observabilityUx7I.NativeTracyDiagnosticReady &&
                      _observabilityUx7I.CancellationReady &&
                      _observabilityUx7I.ResultNavigationReady &&
                      _observabilityUx7I.UsesIsolatedEvidence &&
                      _observabilityUx7I.ShowsHumanSurface &&
                      _observabilityUx7I.EvidenceSourceUntouched;
        _observabilityUx7I.CloseEvidence();
        var released = _observabilityUx7I.EvidenceResourcesReleased;
        var sourceRestored = _observabilityUx7I.SourceSessionRestored;
        var contract = _session.RunUx7IContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _observabilityUx7I.RestoreSourceSurfaceAfterSmoke();
        else _observabilityUx7I.HideSourceSurfaceAfterSmoke();
        _session.RestoreObservabilityApprovalUx7I(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureObservabilityApprovalUx7I() == originalApproval;
        var surfaceRestored = PageId == originalPage && _observabilityUx7I.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_I_{(valid ? "PASS" : "FAILED")} " +
                 $"route={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={contract.InvalidBlocked.ToString().ToLowerInvariant()} " +
                 $"stale={contract.StaleBlocked.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"roundtrip={contract.RoundTrip.ToString().ToLowerInvariant()} " +
                 $"preservation={contract.Preservation.ToString().ToLowerInvariant()} " +
                 $"otel={contract.OpenTelemetryCapture.ToString().ToLowerInvariant()} " +
                 $"tracy_diagnostic={contract.NativeTracyDiagnostic.ToString().ToLowerInvariant()} " +
                 $"cancel={contract.Cancellation.ToString().ToLowerInvariant()} " +
                 $"navigation={contract.ResultNavigation.ToString().ToLowerInvariant()} " +
                 $"isolated={contract.Isolated.ToString().ToLowerInvariant()} " +
                 $"artifact={contract.ArtifactConfined.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(sourceUnchanged && contract.SourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7IAutomatedEvidence()
    {
        if (_observabilityUx7I is not null) _observabilityUx7I.CloseEvidence();
    }
}
