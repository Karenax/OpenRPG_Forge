using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRepositorySyncWorkbenchUx8L : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx8L? _sourceSnapshot;
    private StudioUx8LSyncDraft? _draft;
    private StudioUx8LSyncPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private CheckBox _resumable = null!;
    private CheckBox _deduplicate = null!;
    private CheckBox _verifyContent = null!;
    private CheckBox _requireTrustedSigner = null!;
    private LineEdit _storage = null!;
    private LineEdit _maximumCache = null!;
    private LineEdit _licenses = null!;
    private TextEdit _signers = null!;
    private LineEdit _channel = null!;
    private LineEdit _compatibility = null!;
    private CheckBox _requireSignature = null!;
    private CheckBox _requireChangeNotes = null!;
    private LineEdit _repository = null!;
    private LineEdit _glob = null!;
    private CheckBox _consent = null!;
    private Label _summary = null!;
    private Label _resources = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _verifyButton = null!;
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
    { Succeeded: true, CanonicalDryRun: true, ManifestValid: true, SyncPlanValid: true, ScopeConfined: true };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx8L();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8L(this));
            return Visible && text.Contains("ORF-CONT-009", StringComparison.Ordinal) &&
                   text.Contains("ORF-EE-025", StringComparison.Ordinal) &&
                   text.Contains("Synchronisation automatique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Métadonnées NWSync", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun réseau", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8L(text);
        }
    }

    internal StudioRepositorySyncWorkbenchUx8L(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8L();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        _session.CloseUx8L();
        OpenCurrentSessionUx8L();
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
        _session.CloseUx8L();
        ResetUx8L();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx8L(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-l-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded)
        {
            DeleteEvidenceUx8L();
            RestoreSourceSessionUx8L();
            return false;
        }
        var repository = Path.Combine(_evidenceRoot, "repository");
        Directory.CreateDirectory(repository);
        File.WriteAllBytes(Path.Combine(repository, "core.bin"), Encoding.UTF8.GetBytes("ux8-l-ui-core"));
        File.WriteAllBytes(Path.Combine(repository, "localization.bin"), Encoding.UTF8.GetBytes("ux8-l-ui-localization"));
        if (!controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx8L();
            RestoreSourceSessionUx8L();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentSessionUx8L();
        _moduleChoice.Select(1);
        SelectModuleUx8L(1);
        if (_draft is null) return false;
        _draft = _draft with
        {
            Resumable = true,
            DeduplicateByHash = true,
            VerifyContent = true,
            RequireTrustedSigner = true,
            StorageDirectory = ".forge/content-cache-ux8-l",
            MaximumCacheBytes = 64L * 1024 * 1024,
            AllowedLicenses = ["CC0-1.0"],
            TrustedSignerFingerprints = [new string('a', 64)],
            Channel = "preview",
            Compatibility = "1.x",
            RequireSignature = true,
            RequireChangeNotes = true,
            RepositoryRelativePath = "repository",
            ResourceGlob = "*.bin",
            Consent = true,
        };
        PopulateUx8L(_draft);
        PreviewUx8L();
        var ready = UsesIsolatedEvidence && PreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_L_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _draft is null || _evidenceRoot is null) return false;
        var initial = ProjectJsonSerializer.Serialize(_session.Project!);
        var draft = ReadDraftUx8L();
        if (draft is null) return false;
        var preview = _session.PreviewSyncUx8L(draft);
        var sync = preview.Succeeded && _session.VerifySyncInSandboxUx8L(draft).Succeeded;
        var applied = preview.CanApply && _session.ApplyApprovedSyncUx8L(draft).Succeeded;
        var changed = applied && initial != ProjectJsonSerializer.Serialize(_session.Project!);
        var undone = applied && _session.UndoSyncUx8L().Succeeded;
        var redone = undone && _session.RedoSyncUx8L().Succeeded;
        var saved = redone && _session.SaveSyncUx8L().Succeeded;
        var reopened = saved ? ProjectSession.Open(_evidenceRoot) : null;
        var persisted = reopened is { Succeeded: true, Session: not null } &&
                        ProjectJsonSerializer.Serialize(reopened.Session.Project) ==
                        ProjectJsonSerializer.Serialize(_session.Project!);
        ReloadUx8L();
        return preview.Succeeded && sync && applied && changed && undone && redone && saved && persisted &&
               EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        DeleteEvidenceUx8L();
        RestoreSourceSessionUx8L();
        Visible = false;
        ResetUx8L();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Visible = true;
        UpdateBoundsUx8L();
    }

    internal void HideSourceSurfaceAfterSmoke() => Visible = false;

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8L();
    }

    private void BuildUiUx8L()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.01f, 0.016f, 0.027f, 0.97f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8LRepositorySyncWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(16, 13, 16, 13);
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PUBLICATION · DÉPÔT & NWSYNC · UX8-L", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Configurer une synchronisation vérifiable avant la connexion", 19, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Aperçu sec canonique, cache adressé par SHA-256, confiance, licences, consentement et sauvegarde",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8L("Fermer", Close, 84));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module", 10, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8L((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);
        root.AddChild(StudioStyle.Label(
            "ORF-CONT-009 · Synchronisation automatique  |  ORF-EE-025 · Métadonnées NWSync",
            9, StudioStyle.Warning, wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 470),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var split = new HSplitContainer { SplitOffsets = [760] };
        split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(split);
        BuildFormUx8L(split);
        BuildReportUx8L(split);

        var footer = StudioStyle.Panel("Ux8LStickyFooter", StudioStyle.SurfaceRaised, 8, 1);
        root.AddChild(footer);
        var footerMargin = StudioStyle.Margin(9, 6, 9, 6);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 5);
        footerMargin.AddChild(footerColumn);
        footerColumn.AddChild(StudioStyle.Label(
            "Aucun réseau lancé par Studio · la vérification réelle utilise uniquement un cache local jetable",
            9, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _previewButton = ActionButtonUx8L("Prévisualiser", PreviewUx8L, 116, primary: true);
        _verifyButton = ActionButtonUx8L("Vérifier le cache", VerifyUx8L, 126);
        _applyButton = ActionButtonUx8L("Appliquer", ApplyUx8L, 96);
        _undoButton = ActionButtonUx8L("Annuler", UndoUx8L, 90);
        _redoButton = ActionButtonUx8L("Rétablir", RedoUx8L, 90);
        _saveButton = ActionButtonUx8L("Enregistrer", SaveUx8L, 105);
        foreach (var button in new[] { _previewButton, _verifyButton, _applyButton, _undoButton, _redoButton, _saveButton })
            actions.AddChild(button);
        footerColumn.AddChild(actions);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 9, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 22);
        footerColumn.AddChild(_status);
        UpdateButtonsUx8L();
    }

    private void BuildFormUx8L(HSplitContainer split)
    {
        var panel = StudioStyle.Panel("Ux8LRepositoryForm", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(650, 0);
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

        column.AddChild(StudioStyle.Label("POLITIQUE DE SYNCHRONISATION", 10, StudioStyle.Accent));
        var guarantees = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        guarantees.AddThemeConstantOverride("h_separation", 12);
        guarantees.AddThemeConstantOverride("v_separation", 5);
        column.AddChild(guarantees);
        _resumable = ToggleUx8L("Reprise partielle", guarantees);
        _deduplicate = ToggleUx8L("Déduplication SHA-256", guarantees);
        _verifyContent = ToggleUx8L("Vérifier chaque contenu", guarantees);
        _requireTrustedSigner = ToggleUx8L("Exiger un signataire approuvé", guarantees);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(grid);
        AddLineUx8L(grid, "Dossier cache relatif", out _storage);
        AddLineUx8L(grid, "Quota cache · Mio", out _maximumCache);
        AddLineUx8L(grid, "Licences SPDX · virgules", out _licenses);
        AddTextUx8L(grid, "Empreintes signataires SHA-256 · une par ligne", out _signers, 72);

        column.AddChild(StudioStyle.Label("MÉTADONNÉES NWSYNC", 10, StudioStyle.Accent));
        var metadata = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        metadata.AddThemeConstantOverride("h_separation", 10);
        metadata.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(metadata);
        AddLineUx8L(metadata, "Canal", out _channel);
        AddLineUx8L(metadata, "Compatibilité", out _compatibility);
        _requireSignature = ToggleUx8L("Signature obligatoire", column);
        _requireChangeNotes = ToggleUx8L("Notes de changement obligatoires", column);

        column.AddChild(StudioStyle.Label("DÉPÔT LOCAL DE VALIDATION · NON PERSISTÉ", 10, StudioStyle.Accent));
        var local = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        local.AddThemeConstantOverride("h_separation", 10);
        local.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(local);
        AddLineUx8L(local, "Dossier dépôt relatif", out _repository);
        AddLineUx8L(local, "Filtre ressources", out _glob);
        _consent = new CheckBox { Text = "Je donne mon consentement explicite pour appliquer cette configuration." };
        _consent.Toggled += _ => InvalidateUx8L();
        column.AddChild(_consent);

        foreach (var line in new[] { _storage, _maximumCache, _licenses, _channel, _compatibility, _repository, _glob })
            line.TextChanged += _ => InvalidateUx8L();
        _signers.TextChanged += InvalidateUx8L;
    }

    private void BuildReportUx8L(HSplitContainer split)
    {
        var panel = StudioStyle.Panel("Ux8LRepositoryReport", StudioStyle.SurfaceRaised, 8, 1);
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
        _summary = ReportUx8L("Aucun aperçu sec.", 150);
        column.AddChild(_summary);
        column.AddChild(StudioStyle.Label("MANIFESTE & CACHE", 10, StudioStyle.Accent));
        _resources = ReportUx8L("Aucune ressource planifiée.", 190);
        column.AddChild(_resources);
        column.AddChild(StudioStyle.Label("DIAGNOSTICS", 10, StudioStyle.Accent));
        _diagnostics = ReportUx8L("Aucun diagnostic.", 100);
        column.AddChild(_diagnostics);
    }

    private void OpenCurrentSessionUx8L()
    {
        ResetUx8L();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionnez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem($"{StudioSessionController.HumanTextUx8L(_session.Project.Module.DisplayName)} · workspace ouvert");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBoundsUx8L();
        SetStatusUx8L("Sélectionnez explicitement le module.", false);
    }

    private void SelectModuleUx8L(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1 && _session.Project is not null;
        _draft = _session.CreateSyncDraftUx8L(_explicitSelection);
        _preview = null;
        if (_draft is not null) PopulateUx8L(_draft);
        SetStatusUx8L(_explicitSelection
            ? "Profil chargé · configurez le dépôt puis lancez l’aperçu sec."
            : "Sélectionnez explicitement le module.", false);
        UpdateButtonsUx8L();
    }

    private void PopulateUx8L(StudioUx8LSyncDraft draft)
    {
        _updating = true;
        _resumable.ButtonPressed = draft.Resumable;
        _deduplicate.ButtonPressed = draft.DeduplicateByHash;
        _verifyContent.ButtonPressed = draft.VerifyContent;
        _requireTrustedSigner.ButtonPressed = draft.RequireTrustedSigner;
        _storage.Text = draft.StorageDirectory;
        _maximumCache.Text = (draft.MaximumCacheBytes / (1024L * 1024)).ToString(CultureInfo.InvariantCulture);
        _licenses.Text = string.Join(", ", draft.AllowedLicenses);
        _signers.Text = string.Join('\n', draft.TrustedSignerFingerprints);
        _channel.Text = draft.Channel;
        _compatibility.Text = draft.Compatibility;
        _requireSignature.ButtonPressed = draft.RequireSignature;
        _requireChangeNotes.ButtonPressed = draft.RequireChangeNotes;
        _repository.Text = draft.RepositoryRelativePath;
        _glob.Text = draft.ResourceGlob;
        _consent.ButtonPressed = draft.Consent;
        _summary.Text = "Brouillon chargé · aucune écriture effectuée.";
        _resources.Text = "Aucune ressource planifiée.";
        _diagnostics.Text = "Aucun diagnostic.";
        _updating = false;
    }

    private StudioUx8LSyncDraft? ReadDraftUx8L()
    {
        if (_draft is null) return null;
        if (!long.TryParse(_maximumCache.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mebibytes) ||
            mebibytes <= 0 || mebibytes > 524_288)
        {
            SetStatusUx8L("Le quota doit être un nombre entier entre 1 et 524 288 Mio.", true);
            return null;
        }
        return _draft = _draft with
        {
            Resumable = _resumable.ButtonPressed,
            DeduplicateByHash = _deduplicate.ButtonPressed,
            VerifyContent = _verifyContent.ButtonPressed,
            RequireTrustedSigner = _requireTrustedSigner.ButtonPressed,
            StorageDirectory = _storage.Text,
            MaximumCacheBytes = checked(mebibytes * 1024L * 1024),
            AllowedLicenses = SplitValuesUx8L(_licenses.Text),
            TrustedSignerFingerprints = SplitLinesUx8L(_signers.Text),
            Channel = _channel.Text,
            Compatibility = _compatibility.Text,
            RequireSignature = _requireSignature.ButtonPressed,
            RequireChangeNotes = _requireChangeNotes.ButtonPressed,
            RepositoryRelativePath = _repository.Text,
            ResourceGlob = _glob.Text,
            Consent = _consent.ButtonPressed,
        };
    }

    private void PreviewUx8L()
    {
        var draft = ReadDraftUx8L();
        if (draft is null) return;
        _preview = _session.PreviewSyncUx8L(draft);
        _summary.Text = _preview.SemanticDiff.Count == 0
            ? _preview.Message
            : string.Join('\n', _preview.SemanticDiff.Select(item => "• " + ScrubHumanUx8L(item))) +
              $"\n\nManifeste · {ShortFingerprintUx8L(_preview.ManifestFingerprint)} · " +
              $"{StudioSessionController.HumanBytesUx8L(_preview.TotalBytes)}";
        _resources.Text = _preview.Resources.Count == 0
            ? "Aucune ressource planifiée."
            : string.Join('\n', _preview.Resources.Select(item => "• " + ScrubHumanUx8L(item)));
        _diagnostics.Text = _preview.Diagnostics.Count == 0
            ? "Aucun diagnostic bloquant · manifeste canonique et cache confinés."
            : string.Join('\n', _preview.Diagnostics.Select(item => "• " + ScrubHumanUx8L(item)));
        SetStatusUx8L(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx8L();
    }

    private void VerifyUx8L()
    {
        var draft = ReadDraftUx8L();
        if (draft is null || !PreviewReady) return;
        var result = _session.VerifySyncInSandboxUx8L(draft);
        SetStatusUx8L(result.Message, !result.Succeeded);
        if (result.Succeeded) _resources.Text += "\n• Vérification réelle : téléchargement local puis réutilisation intégrale du cache.";
    }

    private void ApplyUx8L()
    {
        var draft = ReadDraftUx8L();
        if (draft is null) return;
        var result = _session.ApplyApprovedSyncUx8L(draft);
        SetStatusUx8L(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadUx8L(); WorkspaceChanged?.Invoke(); }
        UpdateButtonsUx8L();
    }

    private void UndoUx8L()
    {
        var result = _session.UndoSyncUx8L();
        SetStatusUx8L(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadUx8L(); WorkspaceChanged?.Invoke(); }
        UpdateButtonsUx8L();
    }

    private void RedoUx8L()
    {
        var result = _session.RedoSyncUx8L();
        SetStatusUx8L(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadUx8L(); WorkspaceChanged?.Invoke(); }
        UpdateButtonsUx8L();
    }

    private void SaveUx8L()
    {
        var result = _session.SaveSyncUx8L();
        SetStatusUx8L(result.Message, !result.Succeeded);
        UpdateButtonsUx8L();
    }

    private void ReloadUx8L()
    {
        _draft = _session.CreateSyncDraftUx8L(explicitlySelected: true);
        _preview = null;
        if (_draft is not null) PopulateUx8L(_draft);
    }

    private void InvalidateUx8L()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelSyncApprovalUx8L();
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        _resources.Text = "Aucune ressource approuvée.";
        _diagnostics.Text = "Aucun diagnostic calculé.";
        UpdateButtonsUx8L();
    }

    private void ResetUx8L()
    {
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        if (_summary is not null) _summary.Text = "Aucun aperçu sec.";
        if (_resources is not null) _resources.Text = "Aucune ressource planifiée.";
        if (_diagnostics is not null) _diagnostics.Text = "Aucun diagnostic.";
        UpdateButtonsUx8L();
    }

    private void UpdateButtonsUx8L()
    {
        if (_previewButton is null) return;
        _previewButton.Disabled = _draft is null;
        _verifyButton.Disabled = !PreviewReady;
        _applyButton.Disabled = _preview?.CanApply != true;
        _undoButton.Disabled = _session.History?.CanUndo != true;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = _draft is null;
    }

    private SourceSnapshotUx8L CaptureSourceUx8L(StudioSessionController session) => new(
        session.Project,
        session.History,
        session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [],
        session.IsDirty,
        session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(),
        session.SelectedEntityId,
        session.CaptureSyncApprovalUx8L());

    private bool SourceUnchangedUx8L()
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
               source.CaptureSyncApprovalUx8L() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx8L()
    {
        if (_sourceSession is null) return;
        _session = _sourceSession;
        _sourceSession = null;
        _sourceSessionRestored = true;
        _sourceSnapshot = null;
    }

    private void DeleteEvidenceUx8L()
    {
        try
        {
            if (_evidenceRoot is not null && Directory.Exists(_evidenceRoot)) Directory.Delete(_evidenceRoot, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8L_EVIDENCE_CLEANUP_FAILED " + exception.Message);
        }
        _evidenceResourcesReleased = _evidenceRoot is null || !Directory.Exists(_evidenceRoot);
        _evidenceRoot = null;
    }

    private void UpdateBoundsUx8L()
    {
        var viewport = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1280, viewport.X - 48), Math.Max(720, viewport.Y - 36));
    }

    private void SetStatusUx8L(string message, bool error)
    {
        var text = ScrubHumanUx8L(message);
        if (_status is not null)
        {
            _status.Text = text;
            _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        }
        StatusChanged?.Invoke(text, error);
    }

    private static void AddLineUx8L(GridContainer grid, string label, out LineEdit edit)
    {
        var field = StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true);
        field.CustomMinimumSize = new Vector2(215, 0);
        grid.AddChild(field);
        edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(edit);
        grid.AddChild(edit);
    }

    private static void AddTextUx8L(GridContainer grid, string label, out TextEdit edit, float height)
    {
        var field = StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true);
        field.CustomMinimumSize = new Vector2(215, 0);
        grid.AddChild(field);
        edit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.None,
            ScrollFitContentHeight = false,
        };
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        grid.AddChild(edit);
    }

    private CheckBox ToggleUx8L(string text, Container parent)
    {
        var toggle = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toggle.Toggled += _ => InvalidateUx8L();
        parent.AddChild(toggle);
        return toggle;
    }

    private static Label ReportUx8L(string text, float height)
    {
        var label = StudioStyle.Label(text, 9, StudioStyle.Text, wrap: true);
        label.CustomMinimumSize = new Vector2(0, height);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static Button ActionButtonUx8L(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static IReadOnlyList<string> SplitValuesUx8L(string value) => value
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> SplitLinesUx8L(string value) => value
        .Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string ShortFingerprintUx8L(string value) => value.Length < 12 ? "non calculée" : value[..12] + "…";

    private static string ScrubHumanUx8L(string? value) => StudioSessionController.HumanTextUx8L(value);

    private static bool ContainsUuidUx8L(string value) => Regex.IsMatch(value,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static IEnumerable<string> DescendantTextUx8L(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit line) yield return line.Text;
            foreach (var nested in DescendantTextUx8L(child)) yield return nested;
        }
    }

    private sealed record SourceSnapshotUx8L(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? SelectedEntity,
        StudioUx8LApprovalSnapshot Approval);
}
