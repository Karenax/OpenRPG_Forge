using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureVoiceOverlayUx4L : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureVoiceEvidenceSourceSnapshotUx4L? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureVoiceChoiceUx4L> _choices = [];
    private StudioCreatureVoiceDraftUx4L? _draft;
    private StudioCreatureVoicePreviewUx4L? _previewResult;

    private readonly OptionButton _creature;
    private readonly CheckBox _enabled;
    private readonly LineEdit _profileId;
    private readonly LineEdit _language;
    private readonly LineEdit _banks;
    private readonly LineEdit _emotions;
    private readonly LineEdit _lipSync;
    private readonly LineEdit _variants;
    private readonly LineEdit _license;
    private readonly LineEdit _rights;
    private readonly Label _context;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsBanksAndEmotions => !string.IsNullOrWhiteSpace(_banks.Text) &&
                                           !string.IsNullOrWhiteSpace(_emotions.Text) &&
                                           _summary.Text.Contains("banque", StringComparison.OrdinalIgnoreCase) &&
                                           _summary.Text.Contains("émotion", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsLipSyncAndVariants => !string.IsNullOrWhiteSpace(_lipSync.Text) &&
                                             !string.IsNullOrWhiteSpace(_variants.Text) &&
                                             _summary.Text.Contains("lipsync", StringComparison.OrdinalIgnoreCase) &&
                                             _summary.Text.Contains("variante", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsGovernance => _summary.Text.Contains("Droits", StringComparison.OrdinalIgnoreCase) &&
                                     _summary.Text.Contains("provenance", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Voix et soundset", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Banques audio", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Émotions", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Licence SPDX", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun son, lipsync, TTS", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureVoiceOverlayUx4L(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureVoiceOverlayUx4L";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureVoiceDialogUx4L", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 68;
        dialog.OffsetTop = 36;
        dialog.OffsetRight = -68;
        dialog.OffsetBottom = -36;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("VOIX ET SOUNDSET · UX4-L", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Banques, émotions, langue, lipsync, variantes et droits · aucun UUID ni JSON affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer();
        selection.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(410, 30) };
        selection.AddChild(_creature);
        _enabled = new CheckBox { Text = "Profil voix actif" };
        selection.AddChild(_enabled);
        _context = StudioStyle.Label("Sélection explicite requise · présentation de créature obligatoire.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var host = new Control
        {
            CustomMinimumSize = new Vector2(0, 355),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(host);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        host.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        content.AddChild(StudioStyle.Label("Identité et prises du soundset", 12, StudioStyle.Accent));
        content.AddChild(StudioStyle.Label(
            "Les collections sont ordonnées canoniquement. Un doublon reste visible au validateur et bloque l’aperçu.",
            9, StudioStyle.Muted, wrap: true));
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _profileId = Field(identity, "Profil voix", "voice.warden");
        _language = Field(identity, "Langue", "fr-FR");
        _banks = Field(identity, "Banques audio (séparées par virgule)", "audio.warden.base, audio.warden.combat");
        _emotions = Field(identity, "Émotions (séparées par virgule)", "calm, hostile, fear");
        _lipSync = Field(identity, "Profil lipsync optionnel", "lipsync.fr-v2");
        _variants = Field(identity, "Variantes (séparées par virgule)", "default, armored");
        content.AddChild(identity);

        content.AddChild(StudioStyle.Label("Licence et provenance des droits", 12, StudioStyle.Accent));
        content.AddChild(StudioStyle.Label(
            "Le Studio conserve une licence SPDX et une provenance explicite ; il n’importe ni ne distribue de média.",
            9, StudioStyle.Muted, wrap: true));
        var governance = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _license = Field(governance, "Licence SPDX", "CC-BY-4.0");
        _rights = Field(governance, "Provenance des droits", "actor-consent:recording");
        content.AddChild(governance);

        content.AddChild(StudioStyle.Label("Aperçu canonique et diagnostics", 12, StudioStyle.Accent));
        content.AddChild(StudioStyle.Label(
            "Projection Domain/Application uniquement. Aucun son, lipsync, TTS, acteur ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        _diagnostics = new VBoxContainer();
        content.AddChild(_diagnostics);

        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 142),
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer crée une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34);
            footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature.");
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) &&
            _evidenceSession is null && _evidenceRoot is null) return;
        _session.CancelCreatureVoiceUx4L();
        _previewResult = null;
        _draft = null;
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureVoiceUx4L();
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreatureVoiceChoicesUx4L;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        ClearControls();
        Visible = true;
        SetEditingEnabled(false);
        _context.Text = _choices.Count == 0
            ? "État vide réel : aucune créature avec présentation éditable."
            : "Sélection explicite requise · présentation de créature obligatoire.";
        RenderEmpty(_choices.Count == 0
            ? "Créez d’abord une présentation via UX4-E ; aucune synthèse produit n’est effectuée ici."
            : "Choisissez une créature pour éditer sa voix et son soundset.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureVoiceUx4L();
        _previewResult = null;
        _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty("Sélection explicite requise.");
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreatureVoiceDraftUx4L(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly
                ? "Créature installée en lecture seule : créez une variante projet."
                : "La présentation de cette créature n’est pas éditable.");
            return;
        }
        Populate(_draft);
        SetEditingEnabled(true);
        _context.Text = $"{choice.Label} · soundset optionnel · gouvernance obligatoire si actif.";
        RenderEmpty("Modifiez le brouillon puis prévisualisez la commande canonique.");
    }

    private void Populate(StudioCreatureVoiceDraftUx4L draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.Enabled;
        _profileId.Text = draft.ProfileId;
        _language.Text = draft.Language;
        _banks.Text = draft.AudioBankIds;
        _emotions.Text = draft.EmotionIds;
        _lipSync.Text = draft.LipSyncProfileId;
        _variants.Text = draft.VariantIds;
        _license.Text = draft.LicenseSpdx;
        _rights.Text = draft.RightsProvenance;
        _updating = false;
        UpdateVoiceFields();
    }

    private StudioCreatureVoiceDraftUx4L? ReadDraft() => _draft is null ? null : _draft with
    {
        Enabled = _enabled.ButtonPressed,
        ProfileId = _profileId.Text,
        Language = _language.Text,
        AudioBankIds = _banks.Text,
        EmotionIds = _emotions.Text,
        LipSyncProfileId = _lipSync.Text,
        VariantIds = _variants.Text,
        LicenseSpdx = _license.Text,
        RightsProvenance = _rights.Text,
    };

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Sélectionnez une créature avant de prévisualiser.", true);
            return;
        }
        _draft = draft;
        _previewResult = _session.PreviewCreatureVoiceUx4L(draft);
        _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Brouillon incomplet.", true);
            return;
        }
        var result = _session.ApplyApprovedCreatureVoiceUx4L(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        _draft = _session.CreateCreatureVoiceDraftUx4L(draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void Undo()
    {
        var result = _session.UndoCreatureVoiceUx4L();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Redo()
    {
        var result = _session.RedoCreatureVoiceUx4L();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Reload()
    {
        if (_draft is null) return;
        _draft = _session.CreateCreatureVoiceDraftUx4L(_draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
        CreatureChanged?.Invoke();
    }

    private void RenderPreview(StudioCreatureVoicePreviewUx4L result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}",
                9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.VoiceSummary}\n{result.GovernanceSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : " +
                        $"{dryRun.ToString().ToLowerInvariant()}[/color]";
    }

    private void RenderEmpty(string message)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _summary.Text = message;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreatureVoiceUx4L();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateVoiceFields()
    {
        var enabled = _draft is not null && _enabled.ButtonPressed;
        foreach (var field in Fields()) field.Editable = enabled;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _enabled.Disabled = !enabled;
        foreach (var field in Fields()) field.Editable = enabled && _enabled.ButtonPressed;
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void ClearControls()
    {
        _updating = true;
        _enabled.ButtonPressed = false;
        foreach (var field in Fields()) field.Text = string.Empty;
        _updating = false;
        UpdateVoiceFields();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _enabled.Toggled += _ => { UpdateVoiceFields(); InvalidatePreview(); };
        foreach (var field in Fields()) field.TextChanged += _ => InvalidatePreview();
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none";
        _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous";
            return false;
        }
        if (!TryOpenEvidence())
        {
            _lastFailure = _lastFailure == "none" ? "open" : _lastFailure;
            return false;
        }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0)
        {
            _lastFailure = "fixture_creature_missing";
            Close();
            return false;
        }
        _creature.Select(index + 1);
        SelectCreature(index + 1);
        if (_draft is null)
        {
            _lastFailure = "draft";
            Close();
            return false;
        }
        _draft = StudioSessionController.ChangedCreatureVoiceDraftUx4L(_draft);
        Populate(_draft);
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] " +
                        $"· copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsBanksAndEmotions &&
                    ShowsLipSyncAndVariants && ShowsGovernance && ShowsHumanSurface && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"banks={ShowsBanksAndEmotions}:lip={ShowsLipSyncAndVariants}:" +
                           $"governance={ShowsGovernance}:human={ShowsHumanSurface}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4L_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-L isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-l-creature-voice") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            if (original is null || transaction.History is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original);
            Apply();
            var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null &&
                          ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo();
            var undone = applied && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == originalJson;
            if (undone) Redo();
            var redone = undone && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == appliedJson;
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4L_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureVoiceApprovalUx4L(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4LEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4l-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                !StudioSessionController.WriteEvidencePortraitUx4E(root, evidence.PortraitAssetPath,
                    evidence.PortraitBytes) || !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId;
            _session = session;
            _usesRealProfile = evidence.UsesRealProfile;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root);
            _session = _sourceSession;
            _sourceRestored = true;
            return false;
        }
    }

    private StudioCreatureVoiceEvidenceSourceSnapshotUx4L? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureVoiceApprovalUx4L());
    }

    private bool SourceMatches(StudioCreatureVoiceEvidenceSourceSnapshotUx4L snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureVoiceApprovalUx4L() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _evidenceBlueprintId = null;
        _session = _sourceSession;
        _usesRealProfile = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); }
        catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased;
        _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4l-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private IEnumerable<LineEdit> Fields()
    {
        yield return _profileId; yield return _language; yield return _banks; yield return _emotions;
        yield return _lipSync; yield return _variants; yield return _license; yield return _rights;
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private static LineEdit Field(Control parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }
    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            foreach (var value in VisibleText(child)) yield return value;
        }
    }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
