using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Domain.Content;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBulkTransferWorkbenchUx8K : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx8K? _sourceSnapshot;
    private StudioUx8KBulkDraft? _draft;
    private StudioUx8KBulkPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _operation = null!;
    private LineEdit _source = null!;
    private LineEdit _destination = null!;
    private LineEdit _glob = null!;
    private LineEdit _extensions = null!;
    private OptionButton _collision = null!;
    private LineEdit _provenance = null!;
    private LineEdit _license = null!;
    private CheckBox _consent = null!;
    private Label _summary = null!;
    private Label _entries = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    { Succeeded: true, CanonicalDryRun: true, ConfigurationValid: true, TransferValid: true, ScopeConfined: true };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx8K();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8K(this));
            return Visible &&
                   text.Contains("ORF-BUILD-007", StringComparison.Ordinal) &&
                   text.Contains("Import de masse", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Export de masse", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("consentement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("workspace", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8K(text);
        }
    }

    internal StudioBulkTransferWorkbenchUx8K(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8K();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        _session.CloseUx8K();
        OpenCurrentSessionUx8K();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        Visible = false;
        _session.CloseUx8K();
        ResetUx8K();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        CloseEvidence();
        var evidence = _session.CreateContentEvidencePlanUx3H();
        if (evidence is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx8K(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-k-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, evidence.Project).Succeeded ||
            !StudioSessionController.MaterializeContentEvidenceUx3H(_evidenceRoot, evidence) ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx8K();
            RestoreSourceSessionUx8K();
            return false;
        }

        _evidenceSession = controller;
        _session = controller;
        OpenCurrentSessionUx8K();
        _moduleChoice.Select(1);
        SelectModuleUx8K(1);
        if (_draft is null) return false;
        _draft = _draft with
        {
            Operation = ContentBatchOperation.Import,
            SourceRelativePath = evidence.BatchSourceRelativePath,
            DestinationRelativePath = evidence.BatchDestinationRelativePath,
            Glob = "*.json",
            Extensions = [".json"],
            CollisionPolicy = ContentBatchCollisionPolicy.PreserveBoth,
            Provenance = "studio-ux8-k-evidence",
            LicenseSpdx = "CC0-1.0",
            Consent = true,
        };
        PopulateUx8K(_draft);
        PreviewUx8K();
        var ready = UsesIsolatedEvidence && PreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_K_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _draft is null) return false;
        var initialProject = ProjectJsonSerializer.Serialize(_session.Project!);
        var draft = ReadDraftUx8K();
        if (draft is null) return false;
        var preview = _session.PreviewBulkTransferUx8K(draft with { Consent = true });
        var applied = preview.CanApply && _session.ApplyBulkTransferUx8K(draft with { Consent = true }).Succeeded;
        var undone = applied && _session.UndoBulkTransferUx8K().Succeeded;
        var redone = undone && _session.RedoBulkTransferUx8K().Succeeded;
        var saved = redone && _session.SaveBulkTransferUx8K().Succeeded;
        var reopened = saved && _evidenceRoot is not null ? ProjectSession.Open(_evidenceRoot) : null;
        var persisted = reopened is { Succeeded: true, Session: not null } &&
                        JsonSerializer.Serialize(reopened.Session.Project.Module.Content) ==
                        JsonSerializer.Serialize(_session.Project!.Module.Content);
        var changed = initialProject != ProjectJsonSerializer.Serialize(_session.Project!);
        ReloadUx8K();
        if (_draft is not null)
        {
            _draft = _draft with
            {
                Operation = ContentBatchOperation.Export,
                SourceRelativePath = "content/imported",
                DestinationRelativePath = "exports/ux8-k-ui",
                Glob = "*.json",
                Extensions = [".json"],
                CollisionPolicy = ContentBatchCollisionPolicy.Replace,
                Consent = true,
            };
            PopulateUx8K(_draft);
            PreviewUx8K();
        }
        return preview.Succeeded && applied && undone && redone && saved && persisted && changed &&
               PreviewReady && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null)
        {
            _evidenceResourcesReleased = _evidenceRoot is null || !Directory.Exists(_evidenceRoot);
            RestoreSourceSessionUx8K();
            Visible = false;
            return;
        }
        _evidenceSession.Close();
        _evidenceSession = null;
        DeleteEvidenceUx8K();
        RestoreSourceSessionUx8K();
        Visible = false;
        ResetUx8K();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Visible = true;
        UpdateBoundsUx8K();
    }

    internal void HideSourceSurfaceAfterSmoke() => Visible = false;

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8K();
    }

    private void OpenCurrentSessionUx8K()
    {
        ResetUx8K();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionnez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem($"{StudioSessionController.ScrubHumanUx3H(_session.Project.Module.DisplayName)} · workspace ouvert");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBoundsUx8K();
        SetStatusUx8K("Sélectionnez explicitement le module et le sens du transfert.", false);
    }

    private void BuildUiUx8K()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.01f, 0.016f, 0.027f, 0.97f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8KBulkTransferWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(16, 13, 16, 13);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PUBLICATION · TRANSFERTS DE MASSE · UX8-K", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Importer ou exporter un lot gouverné sans quitter Forge Studio", 19, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Aperçu sec canonique, consentement humain, staging atomique, empreintes SHA-256, annulation et sauvegarde",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8K("Fermer", Close, 84));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module", 10, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8K((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);
        root.AddChild(StudioStyle.Label(
            "ORF-BUILD-007 · Import/export en masse · les deux dossiers restent confinés au workspace sélectionné",
            9, StudioStyle.Warning, wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 480),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var split = new HSplitContainer { SplitOffsets = [690] };
        split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(split);
        BuildFormUx8K(split);
        BuildReportUx8K(split);

        var footer = StudioStyle.Panel("Ux8KStickyFooter", StudioStyle.SurfaceRaised, 8, 1);
        root.AddChild(footer);
        var footerMargin = StudioStyle.Margin(9, 6, 9, 6);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 5);
        footerMargin.AddChild(footerColumn);
        footerColumn.AddChild(StudioStyle.Label(
            "Aucun transfert distribué · aucun dossier hors workspace · aucun fichier ouvert silencieusement",
            9, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _previewButton = ActionButtonUx8K("Prévisualiser", PreviewUx8K, 122, primary: true);
        _applyButton = ActionButtonUx8K("Appliquer", ApplyUx8K, 102);
        _undoButton = ActionButtonUx8K("Annuler", UndoUx8K, 96);
        _redoButton = ActionButtonUx8K("Rétablir", RedoUx8K, 96);
        _saveButton = ActionButtonUx8K("Enregistrer", SaveUx8K, 112);
        foreach (var button in new[] { _previewButton, _applyButton, _undoButton, _redoButton, _saveButton })
            actions.AddChild(button);
        footerColumn.AddChild(actions);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 9, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 24);
        footerColumn.AddChild(_status);
        UpdateButtonsUx8K();
    }

    private void BuildFormUx8K(HSplitContainer split)
    {
        var panel = StudioStyle.Panel("Ux8KTransferForm", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(600, 0);
        split.AddChild(panel);
        var margin = StudioStyle.Margin(11, 9, 11, 9);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(column);
        column.AddChild(StudioStyle.Label("CONFIGURATION DU LOT", 10, StudioStyle.Accent));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 7);
        column.AddChild(grid);

        AddFieldUx8K(grid, "Sens du transfert", out _operation, ["Import de masse", "Export de masse"]);
        _operation.ItemSelected += _ => InvalidateUx8K();
        AddLineUx8K(grid, "Dossier source relatif", out _source);
        AddLineUx8K(grid, "Dossier destination relatif", out _destination);
        AddLineUx8K(grid, "Filtre glob", out _glob);
        AddLineUx8K(grid, "Extensions · séparées par virgule", out _extensions);
        AddFieldUx8K(grid, "Collision", out _collision,
            ["Refuser", "Ignorer l’entrant", "Remplacer", "Conserver les deux"]);
        _collision.ItemSelected += _ => InvalidateUx8K();
        AddLineUx8K(grid, "Provenance", out _provenance);
        AddLineUx8K(grid, "Licence SPDX", out _license);
        foreach (var line in new[] { _source, _destination, _glob, _extensions, _provenance, _license })
            line.TextChanged += _ => InvalidateUx8K();

        column.AddChild(StudioStyle.Label("GARANTIES VERROUILLÉES", 10, StudioStyle.Accent));
        foreach (var label in new[]
                 {
                     "✓ Chemins relatifs et non imbriqués",
                     "✓ Reparse points refusés",
                     "✓ Budget interactif borné à 32 Mio",
                     "✓ SHA-256 vérifié après staging",
                     "✓ Projet et fichiers annulés ensemble",
                 })
        {
            column.AddChild(new CheckBox { Text = label, ButtonPressed = true, Disabled = true });
        }
        _consent = new CheckBox { Text = "Je donne mon consentement explicite pour ce transfert de masse." };
        _consent.Toggled += _ => InvalidateUx8K();
        column.AddChild(_consent);
    }

    private void BuildReportUx8K(HSplitContainer split)
    {
        var panel = StudioStyle.Panel("Ux8KTransferReport", StudioStyle.SurfaceRaised, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        split.AddChild(panel);
        var margin = StudioStyle.Margin(11, 9, 11, 9);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        column.AddChild(StudioStyle.Label("APERÇU SEC & DIFF", 10, StudioStyle.Accent));
        _summary = ReportUx8K("Aucun aperçu sec.", 130);
        column.AddChild(_summary);
        column.AddChild(StudioStyle.Label("FICHIERS PLANIFIÉS", 10, StudioStyle.Accent));
        _entries = ReportUx8K("Aucun fichier sélectionné.", 180);
        column.AddChild(_entries);
        column.AddChild(StudioStyle.Label("DIAGNOSTICS", 10, StudioStyle.Accent));
        _diagnostics = ReportUx8K("Aucun diagnostic.", 105);
        column.AddChild(_diagnostics);
    }

    private void SelectModuleUx8K(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1 && _session.Project is not null;
        _draft = _explicitSelection ? _session.CreateBulkDraftUx8K() : null;
        _preview = null;
        if (_draft is not null) PopulateUx8K(_draft);
        SetStatusUx8K(_explicitSelection
            ? "Configuration chargée · choisissez le sens puis lancez l’aperçu sec."
            : "Sélectionnez explicitement le module.", false);
        UpdateButtonsUx8K();
    }

    private void PopulateUx8K(StudioUx8KBulkDraft draft)
    {
        _updating = true;
        _operation.Select(draft.Operation == ContentBatchOperation.Export ? 1 : 0);
        _source.Text = draft.SourceRelativePath;
        _destination.Text = draft.DestinationRelativePath;
        _glob.Text = draft.Glob;
        _extensions.Text = string.Join(", ", draft.Extensions);
        _collision.Select(draft.CollisionPolicy switch
        {
            ContentBatchCollisionPolicy.Skip => 1,
            ContentBatchCollisionPolicy.Replace => 2,
            ContentBatchCollisionPolicy.PreserveBoth => 3,
            _ => 0,
        });
        _provenance.Text = draft.Provenance;
        _license.Text = draft.LicenseSpdx;
        _consent.ButtonPressed = draft.Consent;
        _summary.Text = "Brouillon chargé · aucune écriture effectuée.";
        _entries.Text = "Aucun fichier sélectionné.";
        _diagnostics.Text = "Aucun diagnostic.";
        _updating = false;
    }

    private StudioUx8KBulkDraft? ReadDraftUx8K()
    {
        if (_draft is null) return null;
        var extensions = _extensions.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return _draft = _draft with
        {
            Operation = _operation.Selected == 1 ? ContentBatchOperation.Export : ContentBatchOperation.Import,
            SourceRelativePath = _source.Text,
            DestinationRelativePath = _destination.Text,
            Glob = _glob.Text,
            Extensions = extensions,
            CollisionPolicy = _collision.Selected switch
            {
                1 => ContentBatchCollisionPolicy.Skip,
                2 => ContentBatchCollisionPolicy.Replace,
                3 => ContentBatchCollisionPolicy.PreserveBoth,
                _ => ContentBatchCollisionPolicy.Fail,
            },
            Provenance = _provenance.Text,
            LicenseSpdx = _license.Text,
            Consent = _consent.ButtonPressed,
        };
    }

    private void PreviewUx8K()
    {
        var draft = ReadDraftUx8K();
        if (draft is null) return;
        _preview = _session.PreviewBulkTransferUx8K(draft);
        _summary.Text = string.Join('\n', _preview.SemanticDiff.Select(item => "• " + ScrubHumanUx8K(item)));
        if (_preview.SemanticDiff.Count == 0) _summary.Text = _preview.Message;
        _entries.Text = _preview.Entries.Count == 0
            ? "Aucun fichier planifié."
            : string.Join('\n', _preview.Entries.Take(80).Select(item => "• " + ScrubHumanUx8K(item)));
        _diagnostics.Text = _preview.Diagnostics.Count == 0
            ? "Aucun diagnostic bloquant · staging atomique prêt."
            : string.Join('\n', _preview.Diagnostics.Select(item => "• " + ScrubHumanUx8K(item)));
        SetStatusUx8K(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx8K();
    }

    private void ApplyUx8K()
    {
        var draft = ReadDraftUx8K();
        if (draft is null) return;
        var result = _session.ApplyBulkTransferUx8K(draft);
        SetStatusUx8K(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ReloadUx8K();
            WorkspaceChanged?.Invoke();
        }
        UpdateButtonsUx8K();
    }

    private void UndoUx8K()
    {
        var result = _session.UndoBulkTransferUx8K();
        SetStatusUx8K(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadUx8K(); WorkspaceChanged?.Invoke(); }
        UpdateButtonsUx8K();
    }

    private void RedoUx8K()
    {
        var result = _session.RedoBulkTransferUx8K();
        SetStatusUx8K(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadUx8K(); WorkspaceChanged?.Invoke(); }
        UpdateButtonsUx8K();
    }

    private void SaveUx8K()
    {
        var result = _session.SaveBulkTransferUx8K();
        SetStatusUx8K(result.Message, !result.Succeeded);
        UpdateButtonsUx8K();
    }

    private void ReloadUx8K()
    {
        _draft = _session.CreateBulkDraftUx8K();
        _preview = null;
        if (_draft is not null) PopulateUx8K(_draft);
    }

    private void InvalidateUx8K()
    {
        if (_updating) return;
        _preview = null;
        _session.CloseUx8K();
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        _entries.Text = "Aucun fichier approuvé.";
        _diagnostics.Text = "Aucun diagnostic calculé.";
        UpdateButtonsUx8K();
    }

    private void ResetUx8K()
    {
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        if (_summary is not null) _summary.Text = "Aucun aperçu sec.";
        if (_entries is not null) _entries.Text = "Aucun fichier sélectionné.";
        if (_diagnostics is not null) _diagnostics.Text = "Aucun diagnostic.";
        UpdateButtonsUx8K();
    }

    private void UpdateButtonsUx8K()
    {
        if (_previewButton is null) return;
        _previewButton.Disabled = _draft is null;
        _applyButton.Disabled = _preview?.CanApply != true;
        _undoButton.Disabled = !_session.CanUndoBulkUx8K;
        _redoButton.Disabled = !_session.CanRedoBulkUx8K;
        _saveButton.Disabled = _draft is null;
    }

    private SourceSnapshotUx8K CaptureSourceUx8K(StudioSessionController session) => new(
        session.Project,
        session.History,
        session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [],
        session.IsDirty,
        session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(),
        session.SelectedEntityId,
        session.CaptureBulkApprovalUx8K(),
        session.CaptureContentApprovalUx3H());

    private bool SourceUnchangedUx8K()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        return source is null || snapshot is null ||
               ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.IsDirty && source.ActiveAreaId == snapshot.AreaId &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.SelectedEntity &&
               source.CaptureBulkApprovalUx8K() == snapshot.Approval &&
               source.CaptureContentApprovalUx3H() == snapshot.ContentApproval;
    }

    private void RestoreSourceSessionUx8K()
    {
        if (_sourceSession is null) return;
        _session = _sourceSession;
        _sourceSession = null;
        _sourceSessionRestored = true;
        _sourceSnapshot = null;
    }

    private void DeleteEvidenceUx8K()
    {
        try
        {
            if (_evidenceRoot is not null && Directory.Exists(_evidenceRoot))
                Directory.Delete(_evidenceRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        _evidenceResourcesReleased = _evidenceRoot is null || !Directory.Exists(_evidenceRoot);
        _evidenceRoot = null;
    }

    private void SetStatusUx8K(string message, bool error)
    {
        _status.Text = ScrubHumanUx8K(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private void UpdateBoundsUx8K()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1040, size.X - 48), Math.Max(750, size.Y - 36));
    }

    private static void AddLineUx8K(GridContainer grid, string label, out LineEdit line)
    {
        var field = StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true);
        field.CustomMinimumSize = new Vector2(220, 0);
        grid.AddChild(field);
        line = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        grid.AddChild(line);
    }

    private static void AddFieldUx8K(
        GridContainer grid,
        string label,
        out OptionButton option,
        IReadOnlyList<string> choices)
    {
        var field = StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true);
        field.CustomMinimumSize = new Vector2(220, 0);
        grid.AddChild(field);
        option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        foreach (var choice in choices) option.AddItem(choice);
        grid.AddChild(option);
    }

    private static Label ReportUx8K(string text, float height)
    {
        var label = StudioStyle.Label(text, 9, StudioStyle.Text, wrap: true);
        label.CustomMinimumSize = new Vector2(0, height);
        return label;
    }

    private static Button ActionButtonUx8K(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 32);
        button.Pressed += action;
        return button;
    }

    private static IEnumerable<string> DescendantTextUx8K(Node node)
    {
        switch (node)
        {
            case OptionButton option:
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                break;
            case Label label: yield return label.Text; break;
            case Button button: yield return button.Text; break;
            case LineEdit line: yield return line.Text; break;
        }
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx8K(child))
                yield return value;
    }

    private static bool ContainsUuidUx8K(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);

    private static string ScrubHumanUx8K(string value) => Regex.Replace(value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé", RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx8K(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? SelectedEntity,
        StudioUx8KApprovalSnapshot Approval,
        StudioUx3HApprovalSnapshot ContentApproval);
}
