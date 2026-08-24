using System.Globalization;
using Forge.Domain.Factions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Factions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioFactionMatrixOverlayUx4C : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioFactionMatrixEvidenceSourceSnapshotUx4C? _sourceSnapshot;
    private bool _usesRealCrypt;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private string _lastFailure = "none";
    private StudioFactionMatrixDraftUx4C? _draft;
    private StudioFactionMatrixPreviewUx4C? _previewResult;
    private IReadOnlyList<StudioFactionChoiceUx4A> _choices = [];
    private readonly Dictionary<Guid, List<string>> _tags = [];
    private readonly List<FactionRelationship> _relationships = [];
    private readonly List<FactionHeatmapSnapshot> _heatmaps = [];
    private bool _updating;
    private bool _applied;

    private readonly OptionButton _focusFaction;
    private readonly Label _context;
    private readonly ItemList _tagList;
    private readonly LineEdit _tagValue;
    private readonly ItemList _matrixList;
    private readonly OptionButton _matrixSource;
    private readonly OptionButton _matrixTarget;
    private readonly LineEdit _matrixScore;
    private readonly LineEdit _matrixExplanation;
    private readonly LineEdit _matrixPreset;
    private readonly ItemList _heatmapList;
    private readonly LineEdit _heatmapLabel;
    private readonly TextEdit _snapshotJson;
    private readonly CheckBox _reputationConfigured;
    private readonly CheckBox _reputationRequired;
    private readonly OptionButton _reactionScope;
    private readonly LineEdit _reactionScopeId;
    private readonly CheckBox _applyDisguise;
    private readonly CheckBox _applyCrimes;
    private readonly CheckBox _applyDialogue;
    private readonly CheckBox _applyZone;
    private readonly CheckBox _allowGm;
    private readonly LineEdit _subjectId;
    private readonly OptionButton _reactionSource;
    private readonly OptionButton _reactionTarget;
    private readonly LineEdit _disguise;
    private readonly LineEdit _crime;
    private readonly LineEdit _dialogue;
    private readonly LineEdit _zoneOverride;
    private readonly LineEdit _gmOverride;
    private readonly RichTextLabel _validationDetail;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? FactionChanged;
    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _focusFaction.Selected > 0;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsSemanticTags => _tagList.ItemCount > 0;
    internal bool ShowsMatrixInterface => _matrixList.ItemCount > 0 && _heatmapList.ItemCount >= 2 &&
                                          !string.IsNullOrWhiteSpace(_snapshotJson.Text);
    internal bool ShowsPlayerReaction => _summary.Text.Contains("disposition", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsFactionValidation => _validationDetail.Text.Contains(
        "aucun diagnostic", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Tags sémantiques", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Matrice et historique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Réaction au joueur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Validation factionnelle", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealCryptEvidence => _usesRealCrypt;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioFactionMatrixOverlayUx4C(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioFactionMatrixOverlayUx4C";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.015f, 0.02f, 0.035f, 0.93f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("FactionMatrixDialogUx4C", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 64;
        dialog.OffsetTop = 34;
        dialog.OffsetRight = -64;
        dialog.OffsetBottom = -34;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("MATRICE ET VALIDATION · UX4-C", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Tags, heatmap, import/export et réaction : aperçu canonique obligatoire, aucun UUID affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var contextRow = new HBoxContainer();
        contextRow.AddChild(StudioStyle.Label("Faction", 9, StudioStyle.Muted));
        _focusFaction = new OptionButton { CustomMinimumSize = new Vector2(300, 30) };
        contextRow.AddChild(_focusFaction);
        _context = StudioStyle.Label("Sélection explicite requise.", 9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context);
        root.AddChild(contextRow);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        var tabs = new TabContainer();
        tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(tabs);

        var tagsTab = Tab(tabs, "1 · Tags sémantiques");
        tagsTab.AddChild(StudioStyle.Label(
            "Tags directs de la faction sélectionnée · les tags hérités restent calculés par le runtime.",
            10, StudioStyle.Muted, wrap: true));
        _tagList = List(tagsTab, 118);
        _tagValue = Field("Tag normalisé", tagsTab, "guard, civilian, undead, merchant…");
        var tagActions = new HBoxContainer();
        var addTag = StudioStyle.Button("Ajouter le tag", compact: true);
        var removeTag = StudioStyle.Button("Retirer le tag", compact: true);
        addTag.Pressed += AddTag;
        removeTag.Pressed += RemoveTag;
        tagActions.AddChild(addTag);
        tagActions.AddChild(removeTag);
        tagsTab.AddChild(tagActions);

        var matrixTab = Tab(tabs, "2 · Matrice et historique");
        matrixTab.AddChild(StudioStyle.Label(
            "Chaque direction reste indépendante. L’historique est versionné et importable/exportable.",
            10, StudioStyle.Muted, wrap: true));
        _matrixList = List(matrixTab, 102);
        var matrixOptions = new HBoxContainer();
        _matrixSource = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _matrixTarget = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        matrixOptions.AddChild(_matrixSource);
        matrixOptions.AddChild(_matrixTarget);
        matrixTab.AddChild(matrixOptions);
        _matrixScore = Field("Score -100..100", matrixTab, "0");
        _matrixExplanation = Field("Explication humaine", matrixTab, "raison de la relation");
        _matrixPreset = Field("Preset", matrixTab, "hostile, neutral, allied, custom");
        var saveRelation = StudioStyle.Button("Mettre à jour la direction", compact: true);
        saveRelation.Pressed += SaveRelationship;
        matrixTab.AddChild(saveRelation);
        matrixTab.AddChild(StudioStyle.Label("Historique heatmap", 10, StudioStyle.Accent));
        _heatmapList = List(matrixTab, 76);
        _heatmapLabel = Field("Libellé du nouvel instantané", matrixTab, "Avant négociation");
        var historyActions = new HBoxContainer();
        var capture = StudioStyle.Button("Capturer la matrice", compact: true);
        var removeSnapshot = StudioStyle.Button("Retirer l’instantané", compact: true);
        capture.Pressed += CaptureHeatmap;
        removeSnapshot.Pressed += RemoveHeatmap;
        historyActions.AddChild(capture);
        historyActions.AddChild(removeSnapshot);
        matrixTab.AddChild(historyActions);
        _snapshotJson = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 82),
            PlaceholderText = "JSON canonique de l’instantané sélectionné",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        matrixTab.AddChild(_snapshotJson);
        var importExport = new HBoxContainer();
        var export = StudioStyle.Button("Exporter la sélection", compact: true);
        var import = StudioStyle.Button("Importer le JSON", compact: true);
        export.Pressed += ExportHeatmap;
        import.Pressed += ImportHeatmap;
        importExport.AddChild(export);
        importExport.AddChild(import);
        matrixTab.AddChild(importExport);

        var reactionTab = Tab(tabs, "3 · Réaction au joueur");
        _reputationConfigured = new CheckBox { Text = "Profil de réputation configuré" };
        _reputationRequired = new CheckBox { Text = "Réputation requise au runtime" };
        reactionTab.AddChild(_reputationConfigured);
        reactionTab.AddChild(_reputationRequired);
        _reactionScope = EnumOption<ReputationScope>();
        reactionTab.AddChild(StudioStyle.Label("Scope par défaut", 9, StudioStyle.Muted));
        reactionTab.AddChild(_reactionScope);
        _reactionScopeId = Field("Clé de scope", reactionTab, "world, campaign, player…");
        var toggles = new HBoxContainer();
        _applyDisguise = new CheckBox { Text = "Déguisement" };
        _applyCrimes = new CheckBox { Text = "Crimes" };
        _applyDialogue = new CheckBox { Text = "Dialogue" };
        _applyZone = new CheckBox { Text = "Règles zone" };
        _allowGm = new CheckBox { Text = "Override MJ" };
        foreach (var toggle in new[] { _applyDisguise, _applyCrimes, _applyDialogue, _applyZone, _allowGm })
            toggles.AddChild(toggle);
        reactionTab.AddChild(toggles);
        reactionTab.AddChild(StudioStyle.Label(
            "Simulation copiée : réputation → déguisement → crimes → dialogue → zone → MJ.",
            10, StudioStyle.Muted, wrap: true));
        _subjectId = Field("Sujet humain", reactionTab, "player");
        var reactionFactions = new HBoxContainer();
        _reactionSource = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _reactionTarget = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reactionFactions.AddChild(_reactionSource);
        reactionFactions.AddChild(_reactionTarget);
        reactionTab.AddChild(reactionFactions);
        _disguise = Field("Modificateur déguisement", reactionTab, "0");
        _crime = Field("Modificateur crimes", reactionTab, "0");
        _dialogue = Field("Modificateur dialogue", reactionTab, "0");
        _zoneOverride = Field("Override zone facultatif", reactionTab, "vide");
        _gmOverride = Field("Override MJ facultatif", reactionTab, "vide");

        var validationTab = Tab(tabs, "4 · Validation factionnelle");
        validationTab.AddChild(StudioStyle.Label(
            "Contrôles canoniques : cycles, parents orphelins, bandes chevauchantes, membres sans faction, " +
            "tags, matrice, snapshots et réaction.", 11, StudioStyle.Accent, wrap: true));
        _validationDetail = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 190),
            Text = "Prévisualisez pour lancer la validation canonique.",
        };
        validationTab.AddChild(_validationDetail);

        var review = StudioStyle.Panel("FactionMatrixReviewUx4C", StudioStyle.Surface, 9, 1);
        review.CustomMinimumSize = new Vector2(0, 142);
        var reviewRow = new HBoxContainer();
        review.AddChild(reviewRow);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 122),
        };
        reviewRow.AddChild(_summary);
        _diagnostics = new VBoxContainer { CustomMinimumSize = new Vector2(360, 122) };
        reviewRow.AddChild(_diagnostics);
        root.AddChild(review);

        root.AddChild(StudioStyle.Label(
            "Aperçu sec : calculs purs uniquement ; aucun acteur, dialogue, règle de zone, MJ ou réseau n’est muté.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            footer.AddChild(button);
        }
        root.AddChild(footer);
        ConnectEvents();
        SetEditingEnabled(false);
        RenderEmpty("Sélectionnez une faction pour ouvrir la matrice canonique.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelFactionMatrixUx4C();
        _draft = _session.CreateFactionMatrixDraftUx4C();
        _choices = _session.FactionChoicesUx4A;
        RebuildFactionOptions();
        Visible = true;
        _applied = false;
        _previewResult = null;
        if (_draft is null)
        {
            SetEditingEnabled(false);
            RenderEmpty("Deux factions au minimum sont requises pour construire une matrice.");
            return;
        }
        LoadDraft(_draft);
        _focusFaction.Select(0);
        _context.Text = "Sélectionnez une faction : aucun tag n’est chargé automatiquement.";
        SetEditingEnabled(false);
        RenderEmpty("Contexte prêt · sélection explicite requise avant l’aperçu.");
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession))
            return;
        _session.CancelFactionMatrixUx4C();
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        else { _session = _sourceSession; _sourceRestored = true; }
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void LoadDraft(StudioFactionMatrixDraftUx4C draft)
    {
        _updating = true;
        _tags.Clear();
        foreach (var item in draft.TagsByFaction) _tags[item.Key] = item.Value.ToList();
        _relationships.Clear(); _relationships.AddRange(draft.Relationships);
        _heatmaps.Clear(); _heatmaps.AddRange(draft.HeatmapHistory.Select(item => item with
        {
            Relationships = item.Relationships.ToArray(),
        }));
        _reputationConfigured.ButtonPressed = draft.ReputationConfigured;
        _reputationRequired.ButtonPressed = draft.RequireReputationRuntime;
        _reactionScope.Select((int)draft.Reaction.DefaultScope);
        _reactionScopeId.Text = draft.Reaction.DefaultScopeId;
        _applyDisguise.ButtonPressed = draft.Reaction.ApplyDisguise;
        _applyCrimes.ButtonPressed = draft.Reaction.ApplyCrimes;
        _applyDialogue.ButtonPressed = draft.Reaction.ApplyDialogue;
        _applyZone.ButtonPressed = draft.Reaction.ApplyZoneRules;
        _allowGm.ButtonPressed = draft.Reaction.AllowGameMasterOverride;
        _subjectId.Text = draft.SubjectId;
        _disguise.Text = Number(draft.DisguiseModifier);
        _crime.Text = Number(draft.CrimeModifier);
        _dialogue.Text = Number(draft.DialogueModifier);
        _zoneOverride.Text = OptionalNumber(draft.ZoneOverride);
        _gmOverride.Text = OptionalNumber(draft.GameMasterOverride);
        SelectFaction(_reactionSource, draft.ReactionSourceId);
        SelectFaction(_reactionTarget, draft.ReactionTargetId);
        RebuildMatrix();
        RebuildHeatmaps();
        _updating = false;
    }

    private void SelectFocus(long index)
    {
        if (_updating || index <= 0 || index > _choices.Count)
        {
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[(int)index - 1];
        if (_draft is null) return;
        _draft = _draft with { FocusFactionId = choice.FactionId };
        RebuildTags(choice.FactionId);
        _context.Text = $"{choice.Label} · tags directs et effectifs · identité technique protégée";
        SetEditingEnabled(true);
        InvalidatePreview();
    }

    private StudioFactionMatrixDraftUx4C? ReadDraft()
    {
        if (_draft is null || !HasExplicitSelection || !TryDouble(_matrixScore.Text, out _) ||
            !TryDouble(_disguise.Text, out var disguise) || !TryDouble(_crime.Text, out var crime) ||
            !TryDouble(_dialogue.Text, out var dialogue) || !TryOptionalDouble(_zoneOverride.Text, out var zone) ||
            !TryOptionalDouble(_gmOverride.Text, out var gm))
        {
            SetStatus("Valeur numérique invalide : utilisez le format invariant, par exemple -12.5.", true);
            return null;
        }
        var source = SelectedFaction(_reactionSource);
        var target = SelectedFaction(_reactionTarget);
        if (source == Guid.Empty || target == Guid.Empty || source == target)
        {
            SetStatus("Choisissez deux factions différentes pour la simulation.", true);
            return null;
        }
        var reaction = _draft.Reaction with
        {
            DefaultScope = (ReputationScope)_reactionScope.Selected,
            DefaultScopeId = _reactionScopeId.Text.Trim(),
            ApplyDisguise = _applyDisguise.ButtonPressed,
            ApplyCrimes = _applyCrimes.ButtonPressed,
            ApplyDialogue = _applyDialogue.ButtonPressed,
            ApplyZoneRules = _applyZone.ButtonPressed,
            AllowGameMasterOverride = _allowGm.ButtonPressed,
        };
        return _draft with
        {
            FocusFactionId = _choices[_focusFaction.Selected - 1].FactionId,
            TagsByFaction = _tags.ToDictionary(item => item.Key,
                item => (IReadOnlyList<string>)item.Value.ToArray()),
            Relationships = _relationships.ToArray(),
            ReputationConfigured = _reputationConfigured.ButtonPressed,
            RequireReputationRuntime = _reputationRequired.ButtonPressed,
            HeatmapHistory = _heatmaps.Select(item => item with { Relationships = item.Relationships.ToArray() }).ToArray(),
            Reaction = reaction,
            SubjectId = _subjectId.Text.Trim(),
            ReactionSourceId = source,
            ReactionTargetId = target,
            DisguiseModifier = disguise,
            CrimeModifier = crime,
            DialogueModifier = dialogue,
            ZoneOverride = zone,
            GameMasterOverride = gm,
        };
    }

    private void AddTag()
    {
        if (!TryFocus(out var id)) return;
        var value = _tagValue.Text.Trim();
        if (value.Length == 0) return;
        if (!_tags[id].Contains(value, StringComparer.Ordinal)) _tags[id].Add(value);
        _tagValue.Text = string.Empty;
        RebuildTags(id);
        InvalidatePreview();
    }

    private void RemoveTag()
    {
        if (!TryFocus(out var id)) return;
        var selected = SelectedIndex(_tagList, _tags[id].Count);
        if (selected >= 0) _tags[id].RemoveAt(selected);
        RebuildTags(id);
        InvalidatePreview();
    }

    private void SelectRelationship(long index)
    {
        if (_updating || index < 0 || index >= _relationships.Count) return;
        var item = _relationships[(int)index];
        SelectFaction(_matrixSource, item.SourceId);
        SelectFaction(_matrixTarget, item.TargetId);
        _matrixScore.Text = Number(item.Score);
        _matrixExplanation.Text = item.Explanation;
        _matrixPreset.Text = item.Preset;
    }

    private void SaveRelationship()
    {
        var index = SelectedIndex(_matrixList, _relationships.Count);
        if (index < 0 || !TryDouble(_matrixScore.Text, out var score)) return;
        var source = SelectedFaction(_matrixSource);
        var target = SelectedFaction(_matrixTarget);
        _relationships[index] = new(source, target, score, _matrixExplanation.Text.Trim(), _matrixPreset.Text.Trim());
        RebuildMatrix(index);
        InvalidatePreview();
    }

    private void CaptureHeatmap()
    {
        var label = _heatmapLabel.Text.Trim();
        if (label.Length == 0) label = $"Capture {_heatmaps.Count + 1}";
        _heatmaps.Add(new(Guid.NewGuid(), 1, "studio:ux4-c", DateTimeOffset.UtcNow, label,
            _relationships.Select(item => item with { }).ToArray()));
        RebuildHeatmaps(_heatmaps.Count - 1);
        InvalidatePreview();
    }

    private void RemoveHeatmap()
    {
        var index = SelectedIndex(_heatmapList, _heatmaps.Count);
        if (index < 0) return;
        _heatmaps.RemoveAt(index);
        _snapshotJson.Text = string.Empty;
        RebuildHeatmaps();
        InvalidatePreview();
    }

    private void SelectHeatmap(long index)
    {
        if (index < 0 || index >= _heatmaps.Count) return;
        _snapshotJson.Text = FactionReputationService.ExportSnapshot(_heatmaps[(int)index]);
    }

    private void ExportHeatmap()
    {
        var index = SelectedIndex(_heatmapList, _heatmaps.Count);
        if (index >= 0) SelectHeatmap(index);
    }

    private void ImportHeatmap()
    {
        try
        {
            var imported = FactionReputationService.ImportSnapshot(_snapshotJson.Text);
            var index = _heatmaps.FindIndex(item => item.Id == imported.Id);
            if (index >= 0) _heatmaps[index] = imported; else _heatmaps.Add(imported);
            RebuildHeatmaps(index >= 0 ? index : _heatmaps.Count - 1);
            InvalidatePreview();
        }
        catch
        {
            SetStatus("Le JSON ne décrit pas un instantané heatmap canonique.", true);
        }
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var project = _session.Project;
        var history = _session.History;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var dirty = _session.IsDirty;
        var result = _session.PreviewFactionMatrixUx4C(draft);
        _previewResult = result;
        var dryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                     ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                     history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(result, dryRun);
        SetEditingEnabled(true);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var result = _session.ApplyApprovedFactionMatrixUx4C(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { InvalidatePreview(); return; }
        FactionChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoFactionMatrixUx4C();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { FactionChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Redo()
    {
        var result = _session.RedoFactionMatrixUx4C();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { FactionChanged?.Invoke(); ReloadAfterMutation(); }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        var focusId = HasExplicitSelection ? _choices[_focusFaction.Selected - 1].FactionId : Guid.Empty;
        _draft = _session.CreateFactionMatrixDraftUx4C(focusId);
        if (_draft is not null)
        {
            LoadDraft(_draft);
            if (focusId != Guid.Empty)
            {
                var index = _choices.ToList().FindIndex(item => item.FactionId == focusId);
                if (index >= 0)
                {
                    _focusFaction.Select(index + 1);
                    RebuildTags(focusId);
                    _context.Text = $"{_choices[index].Label} · projet rechargé après transaction";
                    SetEditingEnabled(true);
                }
            }
        }
        _previewResult = null;
        RenderEmpty("Projet rechargé · prévisualisez avant toute nouvelle application.");
    }

    private void RenderPreview(StudioFactionMatrixPreviewUx4C result, bool dryRun)
    {
        _summary.Text =
            $"[b]{(result.CanApply ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
            $"{result.Message}\n[color=#b8d9ff]Tags[/color] · {result.TagsSummary}\n" +
            $"[color=#b8d9ff]Matrice[/color] · {result.MatrixSummary}\n" +
            $"[color=#b8d9ff]Réaction[/color] · {result.ReactionSummary}\n" +
            $"{result.PatchSummary}\nInverse : {result.InverseSummary}";
        _validationDetail.Text = result.ValidationSummary;
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success, wrap: true));
        else
            foreach (var item in result.Diagnostics.Take(5))
                _diagnostics.AddChild(StudioStyle.Label(item.HumanMessage, 9, StudioStyle.Danger, wrap: true));
        _apply.Disabled = !result.CanApply || !dryRun;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void RenderEmpty(string message)
    {
        _summary.Text = message;
        _validationDetail.Text = "Prévisualisez pour lancer la validation canonique.";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _apply.Disabled = true;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelFactionMatrixUx4C();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
        if (!enabled) _apply.Disabled = true;
    }

    private void ConnectEvents()
    {
        _focusFaction.ItemSelected += SelectFocus;
        _matrixList.ItemSelected += SelectRelationship;
        _heatmapList.ItemSelected += SelectHeatmap;
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
        foreach (var check in new[] { _reputationConfigured, _reputationRequired, _applyDisguise, _applyCrimes,
                     _applyDialogue, _applyZone, _allowGm })
            check.Toggled += _ => InvalidatePreview();
        _reactionScope.ItemSelected += _ => InvalidatePreview();
    }

    private void RebuildFactionOptions()
    {
        _updating = true;
        _focusFaction.Clear();
        _focusFaction.AddItem("Choisir explicitement…");
        foreach (var choice in _choices) _focusFaction.AddItem(choice.Label);
        foreach (var option in new[] { _matrixSource, _matrixTarget, _reactionSource, _reactionTarget })
        {
            option.Clear();
            foreach (var choice in _choices) option.AddItem(choice.Label);
        }
        _updating = false;
    }

    private void RebuildTags(Guid id) => Rebuild(_tagList, _tags.TryGetValue(id, out var tags) ? tags : []);
    private void RebuildMatrix(int select = -1)
    {
        Rebuild(_matrixList, _relationships.Select(item =>
            $"{FactionName(item.SourceId)} → {FactionName(item.TargetId)} · {Number(item.Score)} · {item.Preset}"));
        if (select >= 0 && select < _matrixList.ItemCount) _matrixList.Select(select);
    }
    private void RebuildHeatmaps(int select = -1)
    {
        Rebuild(_heatmapList, _heatmaps.Select(item =>
            $"{item.Label} · {item.CapturedAt:yyyy-MM-dd HH:mm} · {item.Relationships.Count} direction(s)"));
        if (select >= 0 && select < _heatmapList.ItemCount)
        {
            _heatmapList.Select(select);
            SelectHeatmap(select);
        }
    }

    private bool TryFocus(out Guid id)
    {
        id = HasExplicitSelection ? _choices[_focusFaction.Selected - 1].FactionId : Guid.Empty;
        return id != Guid.Empty && _tags.ContainsKey(id);
    }
    private string FactionName(Guid id) => _choices.FirstOrDefault(item => item.FactionId == id)?.Label ?? "Faction héritée protégée";
    private Guid SelectedFaction(OptionButton option) => option.Selected >= 0 && option.Selected < _choices.Count
        ? _choices[option.Selected].FactionId : Guid.Empty;
    private void SelectFaction(OptionButton option, Guid id)
    {
        var index = _choices.ToList().FindIndex(item => item.FactionId == id);
        if (index >= 0) option.Select(index);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
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
        var index = _choices.ToList().FindIndex(item => item.Slug == "ash-watch");
        if (index < 0) index = 0;
        _focusFaction.Select(index + 1);
        SelectFocus(index + 1);
        if (_draft is null || !TryFocus(out var focus) || _relationships.Count == 0 || _heatmaps.Count < 2)
        {
            _lastFailure = "fixture_incomplete";
            Close();
            return false;
        }
        if (!_tags[focus].Contains("ux4-preview", StringComparer.Ordinal)) _tags[focus].Add("ux4-preview");
        _relationships[0] = _relationships[0] with
        {
            Score = Math.Clamp(_relationships[0].Score + 1, -100, 100),
            Explanation = "aperçu UX4-C isolé",
        };
        _matrixList.Select(0);
        SelectRelationship(0);
        _heatmapList.Select(_heatmaps.Count - 1);
        SelectHeatmap(_heatmaps.Count - 1);
        _disguise.Text = "7";
        _crime.Text = "-3";
        _dialogue.Text = "2";
        _zoneOverride.Text = "-12";
        _gmOverride.Text = "44";
        RebuildTags(focus);
        RebuildMatrix(0);
        var history = _session.History!;
        var project = _session.Project!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealCrypt ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsSemanticTags &&
                    ShowsMatrixInterface && ShowsPlayerReaction && ShowsFactionValidation && ShowsHumanSurface &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealCrypt ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:tags={ShowsSemanticTags}:matrix={ShowsMatrixInterface}:" +
                           $"reaction={ShowsPlayerReaction}:validation={ShowsFactionValidation}:human={ShowsHumanSurface}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4C_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-C isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-c-faction-matrix") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            if (original is null) return false;
            Apply();
            var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo();
            var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo();
            var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new StudioSessionController();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(redoneProject!);
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4C_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreFactionMatrixApprovalUx4C(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4CEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4c-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _session = session;
            _usesRealCrypt = evidence.UsesRealCryptSource;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return _draft is not null && SourceMatches(snapshot) && (_usesRealCrypt ^ _usesFallback);
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

    private StudioFactionMatrixEvidenceSourceSnapshotUx4C? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureFactionMatrixApprovalUx4C());
    }

    private bool SourceMatches(StudioFactionMatrixEvidenceSourceSnapshotUx4C snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureFactionMatrixApprovalUx4C() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _session = _sourceSession;
        _usesRealCrypt = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); } catch { released = false; }
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
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4c-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(body);
        tabs.AddChild(scroll);
        return body;
    }
    private static ItemList List(Node parent, float height)
    {
        var list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, height),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        parent.AddChild(list);
        return list;
    }
    private static LineEdit Field(string label, Node parent, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, 30) };
        parent.AddChild(field);
        return field;
    }
    private static OptionButton EnumOption<T>() where T : struct, Enum
    {
        var option = new OptionButton();
        foreach (var value in Enum.GetValues<T>()) option.AddItem(value.ToString());
        return option;
    }
    private static void Rebuild(ItemList list, IEnumerable<string> labels)
    {
        list.Clear();
        foreach (var label in labels) list.AddItem(label);
    }
    private static int SelectedIndex(ItemList list, int count)
    {
        var selected = list.GetSelectedItems();
        return selected.Length == 1 && selected[0] < count ? selected[0] : -1;
    }
    private static bool TryDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    private static bool TryOptionalDouble(string value, out double? result)
    {
        if (string.IsNullOrWhiteSpace(value)) { result = null; return true; }
        var ok = TryDouble(value, out var parsed);
        result = ok ? parsed : null;
        return ok;
    }
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string OptionalNumber(double? value) => value is null ? string.Empty : Number(value.Value);
    private static bool ContainsGuid(string value) =>
        value.Split([' ', '/', ':', '\'', '"', '(', ')', '[', ']', ',', '.', ';', '='],
            StringSplitOptions.RemoveEmptyEntries).Any(item => Guid.TryParse(item, out _));
    private static IEnumerable<string> VisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case Button button when button.Visible: yield return button.Text; break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
                case TabContainer tabContainer when tabContainer.Visible:
                    for (var index = 0; index < tabContainer.GetTabCount(); index++) yield return tabContainer.GetTabTitle(index);
                    break;
            }
            foreach (var text in VisibleText(child)) yield return text;
        }
    }
}
