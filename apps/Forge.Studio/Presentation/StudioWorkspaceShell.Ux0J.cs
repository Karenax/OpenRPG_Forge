using Forge.Application.Creator;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioSafeRenameOverlay _safeRenameOverlay = null!;
    private StudioDiagnosticsPage _diagnosticsPage = null!;
    private Button _messagesButton = null!;

    private void InitializeUx0JPage(Control pageStack)
    {
        _diagnosticsPage = new StudioDiagnosticsPage(_session);
        _diagnosticsPage.TargetRequested += selection =>
        {
            RouteSelection(selection, "Messages et validation");
            _diagnosticsPage.Deactivate();
        };
        _diagnosticsPage.StatusChanged += SetStatus;
        _diagnosticsPage.ProjectChanged += RefreshAfterUx0JMutation;
        pageStack.AddChild(_diagnosticsPage);
    }

    private void InitializeUx0JOverlay()
    {
        _safeRenameOverlay = new StudioSafeRenameOverlay(_session);
        _safeRenameOverlay.StatusChanged += SetStatus;
        _safeRenameOverlay.ContentChanged += RefreshAfterUx0JMutation;
        AddChild(_safeRenameOverlay);
    }

    private void BuildUx0JStatusAccess(HBoxContainer statusRow)
    {
        _messagesButton = StudioStyle.Button("Messages et état", compact: true);
        _messagesButton.TooltipText = "Ouvrir les erreurs localisées, l’activité et les corrections prévisualisables.";
        _messagesButton.Pressed += () => SwitchPage("validation");
        statusRow.AddChild(_messagesButton);
    }

    private void OpenSafeRenameFromSearch(int index)
    {
        var entry = _globalSearchOverlay.EntryAt(index);
        if (entry?.Document is null || !_safeRenameOverlay.Open(entry.Document))
        {
            SetStatus("Ce résultat ne peut pas être renommé directement.", error: true);
            return;
        }
        _globalSearchOverlay.Close();
        SetStatus("Renommage sûr ouvert · aperçu obligatoire avant application");
    }

    private void RefreshAfterUx0JMutation()
    {
        PopulateAreas();
        RebuildExplorer();
        RefreshInspector();
        WorldViewport.Rebuild(_session, frameArea: false);
        UpdateCommandState();
        if (PageId == "validation")
        {
            _diagnosticsPage.Refresh();
        }
        NavigationChanged?.Invoke();
    }

    internal bool PrepareUx0JEvidence(string mode)
    {
        if (string.Equals(mode, "ux0-j-rename", StringComparison.OrdinalIgnoreCase))
        {
            var project = _session.Project!;
            var area = project.Areas.First();
            var document = new CreatorSearchDocument(
                CreatorContentKind.Area,
                area.Id.ToString(),
                area.DisplayName,
                $"areas/{area.Id}",
                area.DisplayName,
                provenance: "studio:ux0-j-evidence");
            return _safeRenameOverlay.PrepareEvidence(document);
        }

        SwitchPage("validation", notify: false);
        return _diagnosticsPage.PrepareEvidence(mode);
    }

    internal bool RunUx0JSmoke()
    {
        var originalProject = _session.Project;
        var uiTransaction = _diagnosticsPage.RunUiTransactionEvidence();
        var uiReady = PrepareUx0JEvidence("ux0-j-diagnostics") &&
                      PageId == "validation" &&
                      _pageTitle.Text == "Validation & corrections" &&
                      _diagnosticsPage.VisibleDiagnosticCount > 0 &&
                      _diagnosticsPage.ShowsHumanSurface;
        var result = _session.RunUx0JContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0J_{(valid ? "PASS" : "FAILED")} " +
            $"content_rename={result.ContentRename.ToString().ToLowerInvariant()} " +
            $"resource_rename={result.ResourceRename.ToString().ToLowerInvariant()} " +
            $"stable_identity={result.StableIdentity.ToString().ToLowerInvariant()} " +
            $"references_migrated={result.ReferencesMigrated.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"atomic_undo_redo={result.AtomicUndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence_roundtrip={result.PersistenceRoundTrip.ToString().ToLowerInvariant()} " +
            $"invalid_rename_blocked={result.InvalidRenameBlocked.ToString().ToLowerInvariant()} " +
            $"diagnostics={result.Diagnostics.ToString().ToLowerInvariant()} " +
            $"deep_link={result.DeepLink.ToString().ToLowerInvariant()} " +
            $"quick_fix_preview={result.QuickFixPreview.ToString().ToLowerInvariant()} " +
            $"quick_fix_undo={result.QuickFixUndo.ToString().ToLowerInvariant()} " +
            $"diagnostic_export={result.DiagnosticExport.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
