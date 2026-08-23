using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCanonicalValidationWorkbenchUx7A _canonicalValidationUx7A = null!;
    private Button _canonicalValidationButtonUx7A = null!;

    private void BuildUx7AActions(HBoxContainer headingRow)
    {
        _canonicalValidationButtonUx7A = StudioStyle.Button("Validation canonique", compact: true);
        _canonicalValidationButtonUx7A.TooltipText =
            "Dix validations humaines, diagnostics cliquables et corrections avec aperçu obligatoire.";
        _canonicalValidationButtonUx7A.Pressed += OpenCanonicalValidationUx7A;
        headingRow.AddChild(_canonicalValidationButtonUx7A);
    }

    private void InitializeUx7AOverlay()
    {
        _canonicalValidationUx7A = new StudioCanonicalValidationWorkbenchUx7A(_session);
        _canonicalValidationUx7A.StatusChanged += SetStatus;
        _canonicalValidationUx7A.ProjectChanged += RefreshAfterUx7AMutation;
        _canonicalValidationUx7A.TargetRequested += selection =>
        {
            RouteSelection(selection, "Validation canonique UX7-A");
            SetStatus($"Cible ouverte · {selection.DisplayName}");
        };
        AddChild(_canonicalValidationUx7A);
    }

    private void OpenCanonicalValidationUx7A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de lancer sa validation.", error: true);
            return;
        }
        if (PageId != "validation") SwitchPage("validation", notify: false);
        _canonicalValidationUx7A.Open();
        SetStatus("UX7-A · dix autorités de validation en lecture seule, corrections prévisualisables.");
    }

    private void RefreshAfterUx7AMutation()
    {
        PopulateAreas();
        RebuildExplorer();
        RefreshInspector();
        WorldViewport.Rebuild(_session, frameArea: false);
        UpdateCommandState();
        if (PageId == "validation") _diagnosticsPage.Refresh();
        NavigationChanged?.Invoke();
        UpdateUx7AActionState();
    }

    private void UpdateUx7AActionState()
    {
        if (_canonicalValidationButtonUx7A is not null)
            _canonicalValidationButtonUx7A.Visible = PageId == "validation" && _session.Project is not null;
    }

    private void CloseUx7AOverlay()
    {
        if (_canonicalValidationUx7A is not null) _canonicalValidationUx7A.Close();
    }

    internal bool PrepareUx7AEvidence(string mode)
    {
        if (PageId != "validation") SwitchPage("validation", notify: false);
        return _canonicalValidationUx7A.PrepareEvidence(mode);
    }

    internal bool RunUx7ASmoke()
    {
        var originalPage = PageId;
        var originalOpen = _canonicalValidationUx7A.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var approval = _session.CaptureValidationApprovalUx7A();

        var uiTransaction = _canonicalValidationUx7A.RunUiTransactionEvidence();
        var prepared = PrepareUx7AEvidence("ux7-a-canonical-validation");
        var uiReady = prepared && _canonicalValidationUx7A.IsOpen &&
                      _canonicalValidationUx7A.UsesIsolatedEvidence &&
                      _canonicalValidationUx7A.HasExplicitSelection &&
                      _canonicalValidationUx7A.TenCategoriesVisible &&
                      _canonicalValidationUx7A.ReportReady &&
                      _canonicalValidationUx7A.ClickableDiagnosticReady &&
                      _canonicalValidationUx7A.DeepLinkReady &&
                      _canonicalValidationUx7A.QuickFixPreviewReady &&
                      _canonicalValidationUx7A.ShowsHumanSurface &&
                      _canonicalValidationUx7A.EvidenceSourceUntouched;
        _canonicalValidationUx7A.CloseEvidence();
        var released = _canonicalValidationUx7A.EvidenceResourcesReleased;
        var sourceRestored = _canonicalValidationUx7A.SourceSessionRestored;
        var contract = _session.RunUx7AContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _canonicalValidationUx7A.RestoreSourceSurfaceAfterSmoke();
        else _canonicalValidationUx7A.HideSourceSurfaceAfterSmoke();
        _session.RestoreValidationApprovalUx7A(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.CaptureValidationApprovalUx7A() == approval;
        var surfaceRestored = PageId == originalPage &&
                              _canonicalValidationUx7A.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX7_A_{(valid ? "PASS" : "FAILED")} " +
                 "routes=AREA-037,AUD-020,QA-001,QA-002,QA-003,QA-004,QA-005,QA-006,QA-007,QA-021 " +
                 $"ten_routes={contract.TenRoutes.ToString().ToLowerInvariant()} " +
                 $"canonical_categories={contract.CanonicalCategories.ToString().ToLowerInvariant()} " +
                 $"area_readiness={contract.AreaReadiness.ToString().ToLowerInvariant()} " +
                 $"audio={contract.AudioValidation.ToString().ToLowerInvariant()} " +
                 $"localization={contract.LocalizationValidation.ToString().ToLowerInvariant()} " +
                 $"licenses={contract.LicenseValidation.ToString().ToLowerInvariant()} " +
                 $"clickable={contract.ClickableDiagnostic.ToString().ToLowerInvariant()} " +
                 $"deep_link={contract.DeepLink.ToString().ToLowerInvariant()} " +
                 $"fix_preview={contract.QuickFixPreview.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx7AAutomatedEvidence()
    {
        if (_canonicalValidationUx7A is not null) _canonicalValidationUx7A.CloseEvidence();
    }
}
