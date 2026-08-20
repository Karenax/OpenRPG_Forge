using System.Text.RegularExpressions;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNarrativeWorkbenchUx5B : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioNarrativeEvidenceSourceSnapshotUx5B? _sourceSnapshot;
    private readonly TabContainer _tabs;
    private readonly RichTextLabel _review;
    private readonly Label _context;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private readonly Button _close;
    private readonly Dictionary<StudioNarrativeSectionUx5B, OptionButton> _selectors = [];
    private readonly Dictionary<StudioNarrativeSectionUx5B, object> _drafts = [];
    private readonly Dictionary<string, LineEdit> _lines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextEdit> _texts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);
    private OptionButton _loreKind = null!;
    private StudioNarrativePreviewUx5B? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _evidenceSourceUntouched;
    private bool _resourcesReleased = true;
    private bool _sourceSessionRestored = true;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NarrativeChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool PreviewReady => _lastPreview is { Succeeded: true, CanApply: true };
    internal bool HasExplicitSelection => _selectors.Values.All(item => item.Selected > 0);
    internal bool ShowsFiveWorkflows => _tabs.GetTabCount() == 5;
    internal bool ShowsHumanSurface => HumanTextUx5B().Contains("Dialogue de production", StringComparison.Ordinal) &&
        HumanTextUx5B().Contains("Quête complète", StringComparison.Ordinal) &&
        HumanTextUx5B().Contains("Localisation et typographie", StringComparison.Ordinal) &&
        HumanTextUx5B().Contains("Lore et références", StringComparison.Ordinal) &&
        HumanTextUx5B().Contains("Cinématiques et contrôles", StringComparison.Ordinal) &&
        !Regex.IsMatch(HumanTextUx5B(), "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    internal bool EvidenceSourceUntouched => _evidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;

    internal StudioNarrativeWorkbenchUx5B(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioNarrativeWorkbenchUx5B";
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var veil = new ColorRect { Color = new Color(0.025f, 0.03f, 0.04f, .96f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        var margin = StudioStyle.Margin(24, 18, 24, 18);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var panel = StudioStyle.Panel("NarrativeWorkbenchUx5B", StudioStyle.Chrome, 1, 10);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(panel);
        var inner = StudioStyle.Margin(16, 14, 16, 14);
        panel.AddChild(inner);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        inner.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label("PRODUCTION NARRATIVE UX5", 22, StudioStyle.Text));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _context = StudioStyle.Label("Projet courant · aucune simulation live", 11, StudioStyle.Muted);
        heading.AddChild(_context);
        root.AddChild(heading);
        root.AddChild(StudioStyle.Label(
            "Cinq workflows spécialisés · chaque agrégat garde son propre dry-run, son approbation et son historique.",
            12, StudioStyle.Muted, wrap: true));

        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _tabs.CustomMinimumSize = new Vector2(0, 420);
        _tabs.TabChanged += index => ActivateSection((StudioNarrativeSectionUx5B)index);
        root.AddChild(_tabs);
        BuildDialogueTab();
        BuildQuestTab();
        BuildLocalizationTab();
        BuildLoreTab();
        BuildCinematicTab();

        _review = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            CustomMinimumSize = new Vector2(0, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ScrollActive = true,
        };
        _review.AddThemeColorOverride("default_color", StudioStyle.Text);
        _review.AddThemeFontSizeOverride("normal_font_size", 12);
        root.AddChild(_review);

        var boundary = StudioStyle.Label(
            "Aperçu sec : aucun dialogue, son, caméra, acteur, réseau ou modèle IA n’est exécuté dans Studio.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(boundary);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _preview = StudioStyle.Button("Prévisualiser", primary: true);
        _apply = StudioStyle.Button("Appliquer");
        _undo = StudioStyle.Button("Annuler");
        _redo = StudioStyle.Button("Rétablir");
        _save = StudioStyle.Button("Enregistrer");
        _close = StudioStyle.Button("Fermer");
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(116, 36);
            actions.AddChild(button);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _close.CustomMinimumSize = new Vector2(96, 36);
        actions.AddChild(_close);
        root.AddChild(actions);

        _preview.Pressed += PreviewActive;
        _apply.Pressed += ApplyActive;
        _undo.Pressed += UndoActive;
        _redo.Pressed += RedoActive;
        _save.Pressed += SaveActive;
        _close.Pressed += Close;
        ConnectInvalidation(this);
    }

    private void BuildDialogueTab()
    {
        var grid = BeginTab(StudioNarrativeSectionUx5B.Dialogue, "Dialogue de production",
            "Auteur, recherche, mémoire, compétences, voix, caméra, choix chronométré, variantes, sous-dialogue, couverture, collaboration et plan IA gouverné.");
        AddSelector(grid, StudioNarrativeSectionUx5B.Dialogue, "Dialogue");
        AddLine(grid, "dialogue.search", "Recherche humaine", "texte ou motif");
        AddLine(grid, "dialogue.memory", "Mémoire narrative", "promesse");
        AddLine(grid, "dialogue.skill", "Compétence", "insight");
        AddLine(grid, "dialogue.difficulty", "Difficulté", "12");
        AddText(grid, "dialogue.note", "Intention / commentaire", "Valider le ton et les conséquences.");
        AddText(grid, "dialogue.subtitle", "Sous-titre accessible", "La décision est prise.");
        AddLine(grid, "dialogue.fov", "Champ de vision", "52");
        AddLine(grid, "dialogue.timeout", "Choix chronométré (ms)", "8000");
        AddText(grid, "dialogue.ai", "Synopsis IA en aperçu", "Proposition narrative contrôlée par l’auteur.");
    }

    private void BuildQuestTab()
    {
        var grid = BeginTab(StudioNarrativeSectionUx5B.Quest, "Quête complète",
            "Journal, objectifs, participants, récompenses, assistant de trame, ressources, pins, visibilité, tests générés et plan IA avec approbation humaine.");
        AddSelector(grid, StudioNarrativeSectionUx5B.Quest, "Quête");
        AddLine(grid, "quest.category", "Catégorie", "journal");
        AddLine(grid, "quest.priority", "Priorité", "50");
        AddText(grid, "quest.objective", "Objectif", "Achever l’objectif narratif");
        AddLine(grid, "quest.reward", "Progression", "10");
        AddText(grid, "quest.note", "Notes auteur", "Respecter le canon et tester les cinq parcours.");
        AddText(grid, "quest.notification", "Notification accessible", "Journal de quête mis à jour.");
        AddText(grid, "quest.ai", "Synopsis IA en aperçu", "Proposition de quête contrôlée par l’auteur.");
        AddLine(grid, "quest.seed", "Seed des tests", "505");
    }

    private void BuildLocalizationTab()
    {
        var grid = BeginTab(StudioNarrativeSectionUx5B.Localization, "Localisation et typographie",
            "Extraction exhaustive, traduction, placeholders, sous-titres importants et lisibilité. Le catalogue et le profil typographique sont deux transactions explicites.");
        AddSelector(grid, StudioNarrativeSectionUx5B.Localization, "Catalogue");
        AddLine(grid, "loc.locale", "Locale", "fr-FR");
        AddLine(grid, "loc.key", "Clé", "audio.important.subtitle");
        AddText(grid, "loc.fallback", "Texte source", "Son important");
        AddText(grid, "loc.translation", "Traduction", "Son important");
        AddLine(grid, "loc.font", "Police principale", "Noto Sans");
        AddLine(grid, "loc.scale", "Échelle du texte", "1.15");
    }

    private void BuildLoreTab()
    {
        var grid = BeginTab(StudioNarrativeSectionUx5B.Lore, "Lore et références",
            "Index gouverné, sources projet réelles, relations résolues, recherche et contexte borné ; aucun contenu externe n’est inventé.");
        AddSelector(grid, StudioNarrativeSectionUx5B.Lore, "Entrée de lore");
        grid.AddChild(FieldLabel("Type"));
        _loreKind = new OptionButton();
        StudioStyle.ApplyOptionButton(_loreKind);
        foreach (var kind in Enum.GetValues<LoreEntryKind>()) _loreKind.AddItem(Humanize(kind.ToString()), (int)kind);
        grid.AddChild(_loreKind);
        AddLine(grid, "lore.key", "Clé stable", "module-context");
        AddLine(grid, "lore.title", "Titre", "Canon du module");
        AddText(grid, "lore.content", "Contenu", "Contexte narratif gouverné et sourcé.");
        AddLine(grid, "lore.tags", "Tags", "module, canon, dialogue");
        AddLine(grid, "lore.query", "Recherche", "module");
    }

    private void BuildCinematicTab()
    {
        var grid = BeginTab(StudioNarrativeSectionUx5B.Cinematic, "Cinématiques et contrôles",
            "Timeline, dialogue, démarrage, skip et verrouillage/déverrouillage explicite des contrôles ; aucun déplacement de caméra live dans Studio.");
        AddSelector(grid, StudioNarrativeSectionUx5B.Cinematic, "Zone de référence");
        AddLine(grid, "cinematic.name", "Nom de la séquence", "Ouverture narrative UX5");
        AddLine(grid, "cinematic.duration", "Durée (ms)", "5000");
        AddCheck(grid, "cinematic.skippable", "Passable", true);
        AddCheck(grid, "cinematic.sync", "Synchronisée multijoueur", false);
        AddCheck(grid, "cinematic.startup", "Séquence au démarrage", true);
        AddLine(grid, "cinematic.lock", "État au début", "controls.locked=true");
    }

    private GridContainer BeginTab(StudioNarrativeSectionUx5B section, string title, string description)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        _tabs.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 17, StudioStyle.Text));
        column.AddChild(StudioStyle.Label(description, 11, StudioStyle.Muted, wrap: true));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 7);
        column.AddChild(grid);
        return grid;
    }

    private void AddSelector(GridContainer grid, StudioNarrativeSectionUx5B section, string label)
    {
        grid.AddChild(FieldLabel(label));
        var selector = new OptionButton();
        StudioStyle.ApplyOptionButton(selector);
        selector.AddItem($"Sélectionner · {label.ToLowerInvariant()}…");
        selector.ItemSelected += _ => SelectionChanged(section);
        _selectors[section] = selector;
        grid.AddChild(selector);
    }

    private void AddLine(GridContainer grid, string key, string label, string value)
    {
        grid.AddChild(FieldLabel(label));
        var line = new LineEdit { Text = value, ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        _lines[key] = line;
        grid.AddChild(line);
    }

    private void AddText(GridContainer grid, string key, string label, string value)
    {
        grid.AddChild(FieldLabel(label));
        var text = new TextEdit
        {
            Text = value,
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        text.AddThemeColorOverride("font_color", StudioStyle.Text);
        text.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        _texts[key] = text;
        grid.AddChild(text);
    }

    private void AddCheck(GridContainer grid, string key, string label, bool value)
    {
        grid.AddChild(FieldLabel(label));
        var check = new CheckBox { Text = value ? "Oui" : "Non", ButtonPressed = value };
        check.Toggled += pressed => check.Text = pressed ? "Oui" : "Non";
        _checks[key] = check;
        grid.AddChild(check);
    }

    private static Label FieldLabel(string value) => StudioStyle.Label(value, 11, StudioStyle.Muted);

    internal void Open(StudioNarrativeSectionUx5B section = StudioNarrativeSectionUx5B.Dialogue)
    {
        if (_evidenceSession is null) _session = _sourceSession;
        PopulateSelectors();
        _tabs.CurrentTab = (int)section;
        Visible = true;
        MoveToFront();
        _context.Text = UsesIsolatedEvidence
            ? "COPIE TEMPORAIRE ISOLÉE · source strictement inchangée"
            : "PROJET COURANT · sélectionner explicitement avant tout aperçu";
        ResetReview("Sélectionnez un agrégat, ajustez les champs puis prévisualisez.");
        UpdateButtons();
    }

    internal void Close()
    {
        if (UsesIsolatedEvidence)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        _session.CancelNarrativeApprovalUx5B();
        Visible = false;
        _drafts.Clear();
        _lastPreview = null;
    }

    internal void RestoreSourceSurfaceAfterSmoke(StudioNarrativeSectionUx5B section)
    {
        _session = _sourceSession;
        Open(section);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = false;
    }

    private void PopulateSelectors()
    {
        _updating = true;
        Populate(_selectors[StudioNarrativeSectionUx5B.Dialogue], "Sélectionner · dialogue…",
            _session.DialogueProductionChoicesUx5B);
        Populate(_selectors[StudioNarrativeSectionUx5B.Quest], "Sélectionner · quête…",
            _session.QuestProductionChoicesUx5B);
        Populate(_selectors[StudioNarrativeSectionUx5B.Localization], "Catalogue du module",
            [new StudioNarrativeChoiceUx5B(Guid.Empty, "Catalogue extrait · toutes les chaînes du projet", true)]);
        Populate(_selectors[StudioNarrativeSectionUx5B.Lore], "Sélectionner · entrée…",
            _session.LoreEntryChoicesUx5B);
        Populate(_selectors[StudioNarrativeSectionUx5B.Cinematic], "Sélectionner · zone…",
            _session.CinematicAreaChoicesUx5B);
        _updating = false;
    }

    private static void Populate(OptionButton selector, string placeholder,
        IReadOnlyList<StudioNarrativeChoiceUx5B> choices)
    {
        selector.Clear();
        selector.AddItem(placeholder);
        selector.SetItemMetadata(0, string.Empty);
        foreach (var choice in choices)
        {
            selector.AddItem(choice.Label);
            selector.SetItemMetadata(selector.ItemCount - 1, choice.Id.ToString("D"));
        }
        selector.Select(0);
    }

    private void SelectionChanged(StudioNarrativeSectionUx5B section)
    {
        if (_updating) return;
        _session.CancelNarrativeApprovalUx5B(section);
        _drafts.Remove(section);
        _lastPreview = null;
        if (_selectors[section].Selected <= 0)
        {
            ResetReview("Sélection explicite requise.");
            UpdateButtons();
            return;
        }
        try
        {
            object? draft = section switch
            {
                StudioNarrativeSectionUx5B.Dialogue => _session.CreateDialogueProductionDraftUx5B(SelectedId(section)),
                StudioNarrativeSectionUx5B.Quest => _session.CreateQuestProductionDraftUx5B(SelectedId(section)),
                StudioNarrativeSectionUx5B.Localization => _session.CreateLocalizationProductionDraftUx5B(),
                StudioNarrativeSectionUx5B.Lore => _session.CreateLoreProductionDraftUx5B(SelectedNullableId(section)),
                StudioNarrativeSectionUx5B.Cinematic => _session.CreateCinematicProductionDraftUx5B(SelectedId(section)),
                _ => null,
            };
            if (draft is null)
            {
                ResetReview("Cet agrégat ne possède pas les prérequis canoniques de production.");
                UpdateButtons();
                return;
            }
            _drafts[section] = draft;
            LoadDraft(section, draft);
            ResetReview("Brouillon chargé sans mutation · prévisualisation requise.");
        }
        catch (Exception exception)
        {
            ResetReview($"Brouillon refusé : {exception.Message}");
        }
        UpdateButtons();
    }

    private void LoadDraft(StudioNarrativeSectionUx5B section, object draft)
    {
        _updating = true;
        if (section == StudioNarrativeSectionUx5B.Dialogue && draft is StudioDialogueProductionDraftUx5B dialogue)
        {
            _lines["dialogue.search"].Text = dialogue.Candidate.Experience!.Search.Pattern;
            _lines["dialogue.memory"].Text = dialogue.Candidate.Experience.MemorySlots[0].Name;
            _lines["dialogue.skill"].Text = dialogue.Candidate.Experience.SkillChecks[0].SkillId;
            _lines["dialogue.difficulty"].Text = dialogue.Candidate.Experience.SkillChecks[0].Difficulty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _texts["dialogue.note"].Text = dialogue.Candidate.Authoring!.Nodes[0].Notes.Intent;
            _texts["dialogue.subtitle"].Text = dialogue.Candidate.Presentation!.VoiceCues[0].Subtitle.Fallback;
            _lines["dialogue.fov"].Text = dialogue.Candidate.Presentation.CameraShots[0].FieldOfViewDegrees.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _lines["dialogue.timeout"].Text = dialogue.Candidate.Presentation.TimedChoices.FirstOrDefault()?.DurationMilliseconds.ToString() ?? "8000";
            _texts["dialogue.ai"].Text = dialogue.Candidate.Production!.AiPlans[0].Synopsis;
        }
        else if (section == StudioNarrativeSectionUx5B.Quest && draft is StudioQuestProductionDraftUx5B quest)
        {
            _lines["quest.category"].Text = quest.Candidate.Category;
            _lines["quest.priority"].Text = quest.Candidate.Priority.ToString();
            _texts["quest.objective"].Text = quest.Candidate.Authoring!.Objectives[0].Label.Fallback;
            _lines["quest.reward"].Text = quest.Candidate.Authoring.Rewards.Progression.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _texts["quest.note"].Text = quest.Candidate.Authoring.Notes.Design;
            _texts["quest.notification"].Text = quest.Candidate.Operations!.Notification.AccessibleText;
            _texts["quest.ai"].Text = quest.Candidate.Quality!.AiPlan.Synopsis.Fallback;
        }
        else if (section == StudioNarrativeSectionUx5B.Localization && draft is StudioLocalizationProductionDraftUx5B localization)
        {
            var message = localization.Candidate.Messages.First(item => item.Key == "audio.important.subtitle");
            _lines["loc.locale"].Text = "fr-FR";
            _lines["loc.key"].Text = message.Key;
            _texts["loc.fallback"].Text = message.Fallback;
            _texts["loc.translation"].Text = message.Translations.GetValueOrDefault("fr-FR", message.Fallback);
            _lines["loc.font"].Text = localization.WorkspaceCandidate.PrimaryFontFamily;
            _lines["loc.scale"].Text = localization.WorkspaceCandidate.TextScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (section == StudioNarrativeSectionUx5B.Lore && draft is StudioLoreProductionDraftUx5B lore)
        {
            var entry = lore.Candidate.Entries[0];
            _loreKind.Select((int)entry.Kind);
            _lines["lore.key"].Text = entry.Key;
            _lines["lore.title"].Text = entry.Title;
            _texts["lore.content"].Text = entry.Content;
            _lines["lore.tags"].Text = string.Join(", ", entry.Tags);
            _lines["lore.query"].Text = entry.Title;
        }
        else if (section == StudioNarrativeSectionUx5B.Cinematic && draft is StudioCinematicProductionDraftUx5B cinematic)
        {
            var sequence = cinematic.ModuleCandidate.Sequences.Single(item => item.Slug == "ux5-narrative-opening");
            _lines["cinematic.name"].Text = sequence.DisplayName;
            _lines["cinematic.duration"].Text = sequence.DurationMilliseconds.ToString();
            _checks["cinematic.skippable"].ButtonPressed = sequence.Skippable;
            _checks["cinematic.sync"].ButtonPressed = sequence.MultiplayerSynchronized;
            _checks["cinematic.startup"].ButtonPressed = cinematic.ModuleCandidate.Startup.Kind == Forge.Domain.Sequences.StartupFlowKind.Sequence;
            _lines["cinematic.lock"].Text = sequence.Tracks.First(item => item.Kind == Forge.Domain.Sequences.SequenceTrackKind.State).Keyframes[0].Value;
        }
        _updating = false;
    }

    private object? ReadDraft(StudioNarrativeSectionUx5B section)
    {
        if (!_drafts.TryGetValue(section, out var value)) return null;
        try
        {
            return section switch
            {
                StudioNarrativeSectionUx5B.Dialogue when value is StudioDialogueProductionDraftUx5B draft =>
                    _session.ConfigureDialogueProductionDraftUx5B(draft, L("dialogue.search"), L("dialogue.memory"),
                        L("dialogue.skill"), Double("dialogue.difficulty"), T("dialogue.note"),
                        T("dialogue.subtitle"), (float)Double("dialogue.fov"), Int("dialogue.timeout"), T("dialogue.ai")),
                StudioNarrativeSectionUx5B.Quest when value is StudioQuestProductionDraftUx5B draft =>
                    _session.ConfigureQuestProductionDraftUx5B(draft, L("quest.category"), Int("quest.priority"),
                        T("quest.objective"), Double("quest.reward"), T("quest.note"),
                        T("quest.notification"), T("quest.ai"), Int("quest.seed")),
                StudioNarrativeSectionUx5B.Localization when value is StudioLocalizationProductionDraftUx5B draft =>
                    _session.ConfigureLocalizationProductionDraftUx5B(draft, L("loc.locale"), L("loc.key"),
                        T("loc.fallback"), T("loc.translation"), L("loc.font"), Double("loc.scale")),
                StudioNarrativeSectionUx5B.Lore when value is StudioLoreProductionDraftUx5B draft =>
                    _session.ConfigureLoreProductionDraftUx5B(draft, (LoreEntryKind)_loreKind.Selected,
                        L("lore.key"), L("lore.title"), T("lore.content"), L("lore.tags"), L("lore.query")),
                StudioNarrativeSectionUx5B.Cinematic when value is StudioCinematicProductionDraftUx5B draft =>
                    _session.ConfigureCinematicProductionDraftUx5B(draft, L("cinematic.name"),
                        Int("cinematic.duration"), C("cinematic.skippable"), C("cinematic.sync"),
                        C("cinematic.startup"), L("cinematic.lock")),
                _ => null,
            };
        }
        catch (Exception exception)
        {
            ResetReview($"Valeur invalide : {exception.Message}");
            return null;
        }
    }

    private void PreviewActive()
    {
        var section = ActiveSection;
        var draft = ReadDraft(section);
        if (draft is null) return;
        _drafts[section] = draft;
        _lastPreview = section switch
        {
            StudioNarrativeSectionUx5B.Dialogue => _session.PreviewDialogueProductionUx5B((StudioDialogueProductionDraftUx5B)draft),
            StudioNarrativeSectionUx5B.Quest => _session.PreviewQuestProductionUx5B((StudioQuestProductionDraftUx5B)draft),
            StudioNarrativeSectionUx5B.Localization => _session.PreviewLocalizationProductionUx5B((StudioLocalizationProductionDraftUx5B)draft),
            StudioNarrativeSectionUx5B.Lore => _session.PreviewLoreProductionUx5B((StudioLoreProductionDraftUx5B)draft, L("lore.query")),
            StudioNarrativeSectionUx5B.Cinematic => _session.PreviewCinematicProductionUx5B((StudioCinematicProductionDraftUx5B)draft),
            _ => null,
        };
        RenderPreview();
        UpdateButtons();
    }

    private void ApplyActive()
    {
        var section = ActiveSection;
        var draft = ReadDraft(section);
        if (draft is null || _lastPreview is not { Succeeded: true, CanApply: true }) return;
        var result = _session.ApplyApprovedNarrativeUx5B(section, draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        NarrativeChanged?.Invoke();
        PopulateSelectors();
        ResetReview(result.Message + " Rechargez explicitement pour poursuivre.");
        UpdateButtons();
    }

    private void UndoActive()
    {
        var result = _session.UndoNarrativeUx5B(ActiveSection);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) NarrativeChanged?.Invoke();
        UpdateButtons();
    }

    private void RedoActive()
    {
        var result = _session.RedoNarrativeUx5B(ActiveSection);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) NarrativeChanged?.Invoke();
        UpdateButtons();
    }

    private void SaveActive()
    {
        var result = _session.SaveNarrativeUx5B(ActiveSection);
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void RenderPreview()
    {
        if (_lastPreview is null) { ResetReview("Aucun aperçu."); return; }
        var color = _lastPreview.Succeeded && _lastPreview.CanApply ? "#83d6ae" : "#ff8c8c";
        var builder = new System.Text.StringBuilder()
            .Append("[color=").Append(color).Append("][b]").Append(_lastPreview.Message).Append("[/b][/color]\n")
            .Append(_lastPreview.PatchSummary).Append("\n\n");
        foreach (var metric in _lastPreview.Metrics) builder.Append("• ").Append(metric).Append('\n');
        foreach (var diagnostic in _lastPreview.Diagnostics)
            builder.Append("[color=#ff8c8c]• ").Append(diagnostic.Code).Append(" · ")
                .Append(diagnostic.Message).Append("[/color]\n");
        builder.Append("\n[color=#d8b46a]").Append(_lastPreview.RuntimeBoundary).Append("[/color]");
        _review.Text = builder.ToString();
    }

    private void ResetReview(string message)
    {
        _review.Text = $"[color=#ab9acb]{message}[/color]";
        _lastPreview = null;
        _apply.Disabled = true;
    }

    private void ActivateSection(StudioNarrativeSectionUx5B section)
    {
        if (_updating) return;
        _lastPreview = null;
        ResetReview("Cet onglet possède sa propre approbation et son propre historique.");
        UpdateButtons();
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelNarrativeApprovalUx5B(ActiveSection);
        _lastPreview = null;
        _apply.Disabled = true;
    }

    private void ConnectInvalidation(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is LineEdit line) line.TextChanged += _ => InvalidatePreview();
            else if (child is TextEdit text) text.TextChanged += InvalidatePreview;
            else if (child is CheckBox check) check.Toggled += _ => InvalidatePreview();
            else if (child is OptionButton option && !_selectors.Values.Contains(option))
                option.ItemSelected += _ => InvalidatePreview();
            ConnectInvalidation(child);
        }
    }

    private void UpdateButtons()
    {
        _preview.Disabled = !_drafts.ContainsKey(ActiveSection);
        _apply.Disabled = _lastPreview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = ActiveSection == StudioNarrativeSectionUx5B.Lore
            ? false : !_session.CanUndo;
        _redo.Disabled = ActiveSection == StudioNarrativeSectionUx5B.Lore
            ? false : !_session.CanRedo;
        _save.Disabled = !_session.IsOpen;
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!ReleaseEvidence()) return false;
        var evidence = _sourceSession.CreateUx5BEvidenceProject();
        var snapshot = CaptureSourceSnapshot();
        if (evidence is null || snapshot is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux5b-" + Guid.NewGuid().ToString("N"));
        var controller = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(root, evidence.Project);
            if (!created.Succeeded || !controller.Open(root).Succeeded)
            {
                controller.Close();
                DeleteRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = controller;
            _evidenceRoot = root;
            _session = controller;
            _resourcesReleased = false;
            _sourceSessionRestored = false;
            Open(SectionFromMode(mode));
            _updating = true;
            foreach (var selector in _selectors.Values) if (selector.ItemCount > 1) selector.Select(1);
            _updating = false;
            foreach (var section in Enum.GetValues<StudioNarrativeSectionUx5B>())
                SelectionChanged(section);
            var allPreviewed = true;
            foreach (var section in Enum.GetValues<StudioNarrativeSectionUx5B>())
            {
                _tabs.CurrentTab = (int)section;
                PreviewActive();
                allPreviewed &= _lastPreview is { Succeeded: true, CanApply: true };
            }
            _tabs.CurrentTab = (int)SectionFromMode(mode);
            PreviewActive();
            _evidenceSourceUntouched = SourceMatchesSnapshot();
            _context.Text = evidence.UsesRealModule
                ? "COPIE TEMPORAIRE DU MODULE RÉEL · source strictement inchangée"
                : "FALLBACK CANONIQUE TEMPORAIRE · source strictement inchangée";
            return allPreviewed && HasExplicitSelection && ShowsFiveWorkflows && ShowsHumanSurface &&
                   UsesIsolatedEvidence && _evidenceSourceUntouched;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX5B_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            controller.Close();
            DeleteRoot(root);
            _session = _sourceSession;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceSnapshot();
        StudioSessionController? reopened = null;
        try
        {
            if (source is null || !PrepareEvidence("ux5-b-dialogue-production") || _evidenceSession is null)
                return false;
            var editing = _evidenceSession;
            foreach (var section in Enum.GetValues<StudioNarrativeSectionUx5B>())
            {
                _tabs.CurrentTab = (int)section;
                _updating = true;
                _selectors[section].Select(1);
                _updating = false;
                SelectionChanged(section);
                PreviewActive();
                if (_lastPreview is not { Succeeded: true, CanApply: true }) return false;
                ApplyActive();
                if (!_applied) return false;
                UndoActive();
                RedoActive();
            }
            var saved = editing.Save().Succeeded && editing.SaveWorkspaceProfile().Succeeded;
            var finalJson = editing.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(editing.Project);
            if (!saved || editing.Session is null) return false;
            reopened = new StudioSessionController();
            var persisted = reopened.Open(editing.Session.RootDirectory).Succeeded &&
                            reopened.Project is not null &&
                            ProjectJsonSerializer.Serialize(reopened.Project) == finalJson;
            reopened.Close(); reopened = null;
            var sourceUntouched = SourceMatchesSnapshot();
            CloseEvidence();
            return persisted && sourceUntouched && _resourcesReleased && _sourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX5B_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) CloseEvidence();
            if (source is not null) _sourceSession.RestoreNarrativeApprovalUx5B(source.Approval);
        }
    }

    internal void CloseEvidence()
    {
        _session.CancelNarrativeApprovalUx5B();
        _evidenceSession?.Close();
        _evidenceSession = null;
        var root = _evidenceRoot;
        _evidenceRoot = null;
        _resourcesReleased = root is null || DeleteRoot(root);
        _session = _sourceSession;
        _sourceSessionRestored = true;
        Visible = false;
        _drafts.Clear();
        _lastPreview = null;
        _evidenceSourceUntouched = SourceMatchesSnapshot();
    }

    private bool ReleaseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return true;
        CloseEvidence();
        return _resourcesReleased;
    }

    private StudioNarrativeEvidenceSourceSnapshotUx5B? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.CaptureNarrativeApprovalUx5B());
    }

    private bool SourceMatchesSnapshot()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return snapshot is not null && ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.Dirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               Equals(_sourceSession.CaptureNarrativeApprovalUx5B(), snapshot.Approval);
    }

    private static bool DeleteRoot(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch { return false; }
    }

    private static StudioNarrativeSectionUx5B SectionFromMode(string mode) => mode switch
    {
        var value when value.Contains("quest", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Quest,
        var value when value.Contains("local", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Localization,
        var value when value.Contains("lore", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Lore,
        var value when value.Contains("cinematic", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Cinematic,
        _ => StudioNarrativeSectionUx5B.Dialogue,
    };

    private StudioNarrativeSectionUx5B ActiveSection => (StudioNarrativeSectionUx5B)_tabs.CurrentTab;
    private Guid SelectedId(StudioNarrativeSectionUx5B section) => Guid.Parse(
        _selectors[section].GetItemMetadata(_selectors[section].Selected).AsString());
    private Guid? SelectedNullableId(StudioNarrativeSectionUx5B section)
    {
        var value = _selectors[section].GetItemMetadata(_selectors[section].Selected).AsString();
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }
    private string L(string key) => _lines[key].Text.Trim();
    private string T(string key) => _texts[key].Text.Trim();
    private bool C(string key) => _checks[key].ButtonPressed;
    private int Int(string key) => int.Parse(L(key), System.Globalization.CultureInfo.InvariantCulture);
    private double Double(string key) => double.Parse(L(key), System.Globalization.CultureInfo.InvariantCulture);
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private string HumanTextUx5B() => string.Join(' ', FindChildren("*", "Label", true, false)
        .OfType<Label>().Where(item => item.Visible).Select(item => item.Text)) + " " +
        string.Join(' ', FindChildren("*", "Button", true, false).OfType<Button>()
            .Where(item => item.Visible).Select(item => item.Text));
    private static string Humanize(string value) => Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}
