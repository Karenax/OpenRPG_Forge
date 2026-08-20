using Godot;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaVoiceOverlayUx4Q : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioAreaVoiceEvidenceSourceSnapshotUx4Q? _sourceSnapshot;
    private AreaId? _evidenceAreaId;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealModule;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _applied;
    private bool _updating;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioAreaVoiceAreaChoiceUx4Q> _areas = [];
    private IReadOnlyList<StudioAreaVoiceCreatureChoiceUx4Q> _creatures = [];
    private StudioAreaVoiceDraftUx4Q? _draft;
    private StudioAreaVoicePreviewUx4Q? _previewResult;
    private readonly List<StudioLocalizedVoiceTakeUx4Q> _takes = [];

    private readonly OptionButton _area;
    private readonly OptionButton _creature;
    private readonly LineEdit _defaultLocale;
    private readonly ItemList _takeList;
    private readonly LineEdit _locale;
    private readonly LineEdit _resource;
    private readonly LineEdit _subtitle;
    private readonly LineEdit _phonemes;
    private readonly LineEdit _license;
    private readonly LineEdit _sha;
    private readonly LineEdit _ttsPlugin;
    private readonly CheckBox _synthetic;
    private readonly CheckBox _ttsAllowed;
    private readonly LineEdit _consent;
    private readonly LineEdit _speaker;
    private readonly LineEdit _provenance;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? VoiceChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int AreaChoiceCount => _areas.Count;
    internal int CreatureChoiceCount => _creatures.Count;
    internal bool HasExplicitSelection => _area.Selected > 0 && _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsLocalizedTakes => _takes.Count >= 2 && _summary.Text.Contains("2 prise", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsSubtitlePhoneme => _takes.All(item => !string.IsNullOrWhiteSpace(item.Subtitle)) &&
        _takes.Any(item => !string.IsNullOrWhiteSpace(item.PhonemeResource));
    internal bool ShowsTtsConsent => _ttsAllowed.ButtonPressed && !string.IsNullOrWhiteSpace(_consent.Text) &&
        _summary.Text.Contains("TTS", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Voix localisée de créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Prises, sous-titres et phonèmes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Consentement TTS", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModule;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioAreaVoiceOverlayUx4Q(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioAreaVoiceOverlayUx4Q";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false; MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = new Color(0, 0, 0, .72f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);
        var dialog = StudioStyle.Panel("AreaVoiceDialogUx4Q", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 30); AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); dialog.AddChild(root);
        var header = new HBoxContainer(); var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("VOIX LOCALISÉE DE CRÉATURE · UX4-Q", 14, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label(
            "Prises, sous-titres et phonèmes · fallback TTS gouverné · aucune écoute live.",
            9, StudioStyle.Muted, wrap: true)); header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close); root.AddChild(header);
        var selectors = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _area = Choice(selectors, "Zone", []); _creature = Choice(selectors, "Créature", []); root.AddChild(selectors);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true };
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form); root.AddChild(scroll);
        _defaultLocale = Field(form, "Locale par défaut", "fr-FR");
        form.AddChild(StudioStyle.Label("Prises, sous-titres et phonèmes", 11, StudioStyle.Accent));
        _takeList = new ItemList { CustomMinimumSize = new Vector2(0, 100), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddChild(_takeList);
        var takeGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _locale = Field(takeGrid, "Locale", "fr-FR"); _resource = Field(takeGrid, "Ressource audio (vide = TTS)", "builtin://voice/line");
        _subtitle = Field(takeGrid, "Sous-titre", "La route est sûre.");
        _phonemes = Field(takeGrid, "Ressource phonèmes", "phonemes://line");
        _license = Field(takeGrid, "Licence SPDX", "CC0-1.0"); _sha = Field(takeGrid, "SHA-256 contenu", "64 caractères hexadécimaux");
        form.AddChild(takeGrid);
        var takeButtons = new HBoxContainer();
        var add = StudioStyle.Button("Nouvelle prise", compact: true); var update = StudioStyle.Button("Mettre à jour", compact: true);
        var remove = StudioStyle.Button("Retirer", compact: true); add.Pressed += ClearTake; update.Pressed += () => SaveTake();
        remove.Pressed += RemoveTake; takeButtons.AddChild(add); takeButtons.AddChild(update); takeButtons.AddChild(remove); form.AddChild(takeButtons);
        form.AddChild(StudioStyle.Label("Consentement TTS", 11, StudioStyle.Accent));
        var consentGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _ttsPlugin = Field(consentGrid, "Plugin TTS", "tts.local"); _consent = Field(consentGrid, "Référence de consentement", "consent:author");
        _speaker = Field(consentGrid, "Attribution locuteur", "voix auteur"); form.AddChild(consentGrid);
        _synthetic = new CheckBox { Text = "Voix synthétique" }; _ttsAllowed = new CheckBox { Text = "Usage TTS autorisé" };
        form.AddChild(_synthetic); form.AddChild(_ttsAllowed);
        _provenance = Field(form, "Provenance", "studio:ux4-q");
        form.AddChild(StudioStyle.Label(
            "Studio résout la locale et le fallback en mémoire ; il ne charge aucun média et ne lance aucun moteur TTS.",
            9, StudioStyle.Muted, wrap: true));
        _diagnostics = new VBoxContainer(); form.AddChild(_diagnostics);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 128),
            FitContent = false, SelectionEnabled = true }; root.AddChild(_summary);
        var note = StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true); root.AddChild(note);
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); RenderEmpty("Choisissez explicitement une zone et une créature."); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBound(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBound(); }
    internal void HideSourceSurfaceAfterSmoke() { Visible = false; }
    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession)) return;
        _session.CancelAreaVoiceUx4Q(); _draft = null; _previewResult = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBound()
    {
        _session.CancelAreaVoiceUx4Q(); _areas = _session.AreaVoiceAreaChoicesUx4Q;
        _creatures = _session.AreaVoiceCreatureChoicesUx4Q; _updating = true; _area.Clear(); _creature.Clear();
        _area.AddItem("Choisir une zone…"); foreach (var choice in _areas) _area.AddItem(choice.Label); _area.Select(0);
        _creature.AddItem("Choisir une créature…"); foreach (var choice in _creatures) _creature.AddItem(choice.Label); _creature.Select(0);
        _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
    }
    private void SelectionChanged(long _)
    {
        if (_updating) return; _session.CancelAreaVoiceUx4Q(); _previewResult = null; _apply.Disabled = true;
        if (_area.Selected <= 0 || _creature.Selected <= 0)
        { _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise."); return; }
        _draft = _session.CreateAreaVoiceDraftUx4Q(_areas[_area.Selected - 1].AreaId,
            _creatures[_creature.Selected - 1].BlueprintId);
        if (_draft is null) { SetEditingEnabled(false); RenderEmpty("Cette combinaison zone/créature est indisponible."); return; }
        Populate(_draft); SetEditingEnabled(true); RenderEmpty("Modifiez les prises puis prévisualisez la commande canonique.");
    }
    private void Populate(StudioAreaVoiceDraftUx4Q draft)
    {
        _updating = true; _defaultLocale.Text = draft.DefaultLocale; _takes.Clear(); _takes.AddRange(draft.Takes);
        _ttsPlugin.Text = draft.TtsPluginId; _synthetic.ButtonPressed = draft.SyntheticVoice;
        _ttsAllowed.ButtonPressed = draft.TtsUseAllowed; _consent.Text = draft.ConsentReference;
        _speaker.Text = draft.SpeakerAttribution; _provenance.Text = draft.Provenance; _updating = false;
        RebuildTakeList(0);
    }
    private void RebuildTakeList(int select)
    {
        _updating = true; _takeList.Clear(); foreach (var item in _takes)
            _takeList.AddItem($"{item.Locale} · {(string.IsNullOrWhiteSpace(item.Resource) ? "TTS" : "audio")} · {item.Subtitle}");
        if (_takes.Count > 0) { select = Math.Clamp(select, 0, _takes.Count - 1); _takeList.Select(select); SelectTake(select); }
        else ClearTake(); _updating = false;
    }
    private void SelectTake(long index)
    {
        if (index < 0 || index >= _takes.Count) return; var item = _takes[(int)index]; _updating = true;
        _locale.Text = item.Locale; _resource.Text = item.Resource; _subtitle.Text = item.Subtitle;
        _phonemes.Text = item.PhonemeResource; _license.Text = item.LicenseSpdx; _sha.Text = item.ContentSha256; _updating = false;
    }
    private void ClearTake()
    { _updating = true; _takeList.DeselectAll(); _locale.Text = string.Empty; _resource.Text = string.Empty;
      _subtitle.Text = string.Empty; _phonemes.Text = string.Empty; _license.Text = string.Empty; _sha.Text = string.Empty; _updating = false; }
    private bool SaveTake(bool invalidate = true)
    {
        var locale = _locale.Text.Trim(); var subtitle = _subtitle.Text.Trim();
        if (string.IsNullOrWhiteSpace(locale) || string.IsNullOrWhiteSpace(subtitle)) return false;
        var item = new StudioLocalizedVoiceTakeUx4Q(locale, _resource.Text.Trim(), subtitle, _phonemes.Text.Trim(),
            _license.Text.Trim(), _sha.Text.Trim()); var selected = Selected(_takeList);
        if (selected >= 0 && selected < _takes.Count) _takes[selected] = item; else _takes.Add(item);
        RebuildTakeList(selected >= 0 ? selected : _takes.Count - 1); if (invalidate) InvalidatePreview(); return true;
    }
    private void RemoveTake()
    { var selected = Selected(_takeList); if (selected < 0 || selected >= _takes.Count) return;
      _takes.RemoveAt(selected); RebuildTakeList(Math.Min(selected, _takes.Count - 1)); InvalidatePreview(); }
    private StudioAreaVoiceDraftUx4Q? ReadDraft()
    {
        if (_draft is null) return null; var selected = Selected(_takeList);
        if (selected >= 0 && !SaveTake(false)) return null;
        return _draft with
        {
            DefaultLocale = _defaultLocale.Text.Trim(), Takes = _takes.ToArray(), TtsPluginId = _ttsPlugin.Text.Trim(),
            SyntheticVoice = _synthetic.ButtonPressed, TtsUseAllowed = _ttsAllowed.ButtonPressed,
            ConsentReference = _consent.Text.Trim(), SpeakerAttribution = _speaker.Text.Trim(),
            Provenance = _provenance.Text.Trim(),
        };
    }
    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { SetStatus("Brouillon voix incomplet.", true); return; }
        _draft = draft; _previewResult = _session.PreviewAreaVoiceUx4Q(draft);
        _previewDryRun = _previewResult.Succeeded; RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply; SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(); if (draft is null) return; var result = _session.ApplyApprovedAreaVoiceUx4Q(draft);
        _applied = result.Succeeded; SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        VoiceChanged?.Invoke(); _draft = _session.CreateAreaVoiceDraftUx4Q(draft.AreaId, draft.CreatureBlueprintId);
        if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoAreaVoiceUx4Q(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoAreaVoiceUx4Q(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    { if (_draft is null) return; _draft = _session.CreateAreaVoiceDraftUx4Q(_draft.AreaId, _draft.CreatureBlueprintId);
      if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true; VoiceChanged?.Invoke(); }
    private void RenderPreview(StudioAreaVoicePreviewUx4Q result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}", 9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.VoiceSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string message)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = message; }
    private void ClearControls()
    { _updating = true; _takes.Clear(); _takeList.Clear(); foreach (var field in new[] { _defaultLocale, _locale,
        _resource, _subtitle, _phonemes, _license, _sha, _ttsPlugin, _consent, _speaker, _provenance }) field.Text = string.Empty;
      _synthetic.ButtonPressed = false; _ttsAllowed.ButtonPressed = false; _updating = false; }
    private void SetEditingEnabled(bool enabled)
    { _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo;
      _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null; }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelAreaVoiceUx4Q(); _previewResult = null; _apply.Disabled = true; }
    private void ConnectEvents()
    {
        _area.ItemSelected += SelectionChanged; _creature.ItemSelected += SelectionChanged;
        _takeList.ItemSelected += SelectTake; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
        foreach (var field in new[] { _defaultLocale, _locale, _resource, _subtitle, _phonemes, _license, _sha,
                     _ttsPlugin, _consent, _speaker, _provenance }) field.TextChanged += _ => InvalidatePreview();
        _synthetic.Toggled += _ => InvalidatePreview(); _ttsAllowed.Toggled += _ => InvalidatePreview();
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) return false;
        if (!TryOpenEvidence()) return false;
        var areaIndex = _areas.ToList().FindIndex(item => item.AreaId == _evidenceAreaId);
        var creatureIndex = _creatures.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (areaIndex < 0 || creatureIndex < 0) { _lastFailure = "selection_missing"; Close(); return false; }
        _updating = true; _area.Select(areaIndex + 1); _creature.Select(creatureIndex + 1); _updating = false;
        SelectionChanged(0); if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        _takes.Clear(); _takes.Add(new("fr-FR", string.Empty, "La route est sûre · preuve transactionnelle.", "phonemes://ux4q/fr", string.Empty, string.Empty));
        _takes.Add(new("en-US", "builtin://voice/ux4q/en", "The road is safe.", "phonemes://ux4q/en",
            "CC0-1.0", new string('a', 64))); RebuildTakeList(0);
        _defaultLocale.Text = "fr-FR"; _ttsPlugin.Text = "tts.local"; _synthetic.ButtonPressed = true;
        _ttsAllowed.ButtonPressed = true; _consent.Text = "consent:ux4-q:witness";
        _speaker.Text = "voix témoin modifiée"; _provenance.Text = "studio:ux4-q:changed";
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealModule ? "PROFIL RÉEL COPIÉ" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"session isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsLocalizedTakes &&
                    ShowsSubtitlePhoneme && ShowsTtsConsent && ShowsHumanSurface && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealModule ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:takes={ShowsLocalizedTakes}:" +
                           $"phoneme={ShowsSubtitlePhoneme}:tts={ShowsTtsConsent}:human={ShowsHumanSurface}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealModule ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4Q_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-Q isolée prête · aucune donnée source modifiée.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-q-area-voice") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; if (original is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original); Apply(); var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null && ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && ProjectJsonSerializer.Serialize(transaction.Project!) == originalJson;
            if (undone) Redo(); var redone = undone && ProjectJsonSerializer.Serialize(transaction.Project!) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            { reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson; }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4Q_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        { reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
          _sourceSession.RestoreAreaVoiceApprovalUx4Q(snapshot.Approval); }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4QEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4q-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root;
            _evidenceAreaId = evidence.AreaId; _evidenceBlueprintId = evidence.BlueprintId; _session = session;
            _usesRealModule = evidence.UsesRealModule; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBound();
            return SourceMatches(snapshot) && (_usesRealModule ^ _usesFallback);
        }
        catch { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioAreaVoiceEvidenceSourceSnapshotUx4Q? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAreaVoiceApprovalUx4Q());
    }
    private bool SourceMatches(StudioAreaVoiceEvidenceSourceSnapshotUx4Q snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveEntity && _sourceSession.CaptureAreaVoiceApprovalUx4Q() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceAreaId = null; _evidenceBlueprintId = null; _session = _sourceSession;
        _usesRealModule = false; _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root); if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased; _sourceRestored = true; return _resourcesReleased;
    }
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4q-evidence-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, true); return !Directory.Exists(full);
        }
        catch { return false; }
    }
    private static OptionButton Choice(Control parent, string label, IEnumerable<string> values)
    { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
      var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; foreach (var value in values) choice.AddItem(value);
      box.AddChild(choice); parent.AddChild(box); return choice; }
    private static LineEdit Field(Control parent, string label, string placeholder)
    { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
      var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
      box.AddChild(field); parent.AddChild(box); return field; }
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private static int Selected(ItemList list) => list.GetSelectedItems().FirstOrDefault(-1);
    private static IEnumerable<string> VisibleText(Node node)
    { foreach (var child in node.GetChildren()) { if (child is Label { Visible: true } label) yield return label.Text;
      if (child is Button { Visible: true } button) yield return button.Text; foreach (var value in VisibleText(child)) yield return value; } }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
