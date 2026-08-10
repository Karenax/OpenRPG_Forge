#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class QuestJournalWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<QuestDefinition, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action? _openAdvanced;
    private Action<Window>? _applyIcon;
    private ItemList? _quests;
    private ItemList? _states;
    private ItemList? _transitions;
    private TabContainer? _tabs;
    private QuestDefinition? _draft;
    private QuestStateId? _selectedStateId;
    private QuestTransitionId? _selectedTransitionId;
    private LineEdit? _slug;
    private LineEdit? _titleKey;
    private LineEdit? _titleText;
    private LineEdit? _descriptionKey;
    private LineEdit? _descriptionText;
    private LineEdit? _category;
    private SpinBox? _priority;
    private LineEdit? _owner;
    private CheckBox? _required;
    private OptionButton? _defaultScope;
    private CheckBox? _allowPlayer;
    private CheckBox? _allowParty;
    private CheckBox? _allowWorld;
    private CheckBox? _authority;
    private LineEdit? _stateSlug;
    private LineEdit? _stateName;
    private SpinBox? _displayOrder;
    private SpinBox? _legacyIndex;
    private OptionButton? _terminalKind;
    private OptionButton? _reactivation;
    private LineEdit? _customTerminal;
    private LineEdit? _summaryKey;
    private LineEdit? _summaryText;
    private TextEdit? _summaryVariants;
    private LineEdit? _detailsKey;
    private TextEdit? _detailsText;
    private TextEdit? _objectives;
    private TextEdit? _links;
    private OptionButton? _transitionFrom;
    private OptionButton? _transitionTo;
    private SpinBox? _transitionOrder;
    private OptionButton? _eventKind;
    private LineEdit? _eventName;
    private LineEdit? _eventSource;
    private SpinBox? _transitionPriority;
    private OptionButton? _idempotence;
    private LineEdit? _idempotenceKey;
    private TextEdit? _transitionConditions;
    private TextEdit? _transitionActions;
    private SpinBox? _simulationMaximumSteps;
    private TextEdit? _simulationVariables;
    private TextEdit? _simulationEvents;
    private RichTextLabel? _preview;
    private RichTextLabel? _simulation;
    private RichTextLabel? _feedback;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;
    private bool _r008Capture;
    private bool _lastSucceeded;

    public QuestId? SelectedQuestId => _draft?.Id;
    public int StateCount => _draft?.States.Count ?? 0;
    public int LastValidationCount { get; private set; }
    public bool LastSimulationSucceeded { get; private set; }
    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;

    public void Initialize(
        Func<ProjectState?> load,
        Func<QuestDefinition, bool, CommandResult> mutate,
        Action undo,
        Action redo,
        Action openAdvanced,
        Action<Window> applyIcon)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _mutate = mutate ?? throw new ArgumentNullException(nameof(mutate));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _openAdvanced = openAdvanced ?? throw new ArgumentNullException(nameof(openAdvanced));
        _applyIcon = applyIcon ?? throw new ArgumentNullException(nameof(applyIcon));
    }

    public override void _Ready()
    {
        Title = "Quêtes · Journal & états · OpenRPG Forge";
        MinSize = new(1440, 900);
        Size = new(1440, 900);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        CloseRequested += Hide;
        Build();
        Refresh();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--quest-journal-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--quest-transition-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _r008Capture = capture.StartsWith("--quest-transition-ui-capture=", StringComparison.Ordinal);
            var prefix = _r008Capture ? "--quest-transition-ui-capture=" : "--quest-journal-ui-capture=";
            _capturePath = capture[prefix.Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1)
        {
            Open();
            PrepareCapture();
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_QUEST_JOURNAL_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        var previous = _draft?.Id;
        _quests?.Clear();
        if (state is null || state.Project.Quests.Count == 0)
        {
            SetEmpty("Aucune quête dans le projet. Créez une quête pour commencer.");
            return;
        }
        var candidates = state.Project.Quests.OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Title.Fallback, StringComparer.CurrentCultureIgnoreCase).ToArray();
        foreach (var quest in candidates)
        {
            var terminalCount = quest.States.Count(item => item.IsTerminal);
            var index = _quests!.AddItem($"{quest.Title.Fallback} — {quest.Category} · priorité {quest.Priority} · " +
                                         $"{quest.States.Count} états · {terminalCount} fin(s)");
            _quests.SetItemMetadata(index, quest.Id.ToString());
        }
        var selected = Array.FindIndex(candidates, item => item.Id == previous);
        if (selected < 0) selected = 0;
        _quests!.Select(selected);
        LoadQuest(candidates[selected]);
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_draft is null) return false;
        _category!.Text = "main";
        _priority!.Value = 91;
        _owner!.Text = "author:smoke-r007";
        _required!.ButtonPressed = true;
        Select(_defaultScope!, QuestProgressScope.Player);
        _allowPlayer!.ButtonPressed = true;
        _allowParty!.ButtonPressed = true;
        _allowWorld!.ButtonPressed = false;
        _authority!.ButtonPressed = false;
        var terminalIndex = _draft.States.Select((state, index) => (state, index))
            .FirstOrDefault(item => item.state.IsTerminal).index;
        _states!.Select(terminalIndex);
        LoadState(_draft.States[terminalIndex]);
        Select(_terminalKind!, QuestTerminalKind.Completed);
        Select(_reactivation!, QuestReactivationRule.Manual);
        _summaryText!.Text = "La Crypte des Cendres est libérée.";
        _detailsText!.Text = "Le Gardien est vaincu. La braise peut être rendue à Elyra.";
        _summaryVariants!.Text = "en|The Crypt of Ashes is free.";
        _objectives!.Text = "quest.smoke.return|Retourner auprès d’Elyra";
        _links!.Text = "Custom|lore:ember-key|quest.smoke.ember|Clé de braise";
        RebuildPreview();
        Simulate();
        return LastValidationCount == 0 && LastSimulationSucceeded;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    public bool ConfigureForR008SmokeTest()
    {
        Refresh();
        if (_draft is null || _draft.Transitions.Count == 0) return false;
        var ordered = _draft.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).ToArray();
        _transitions!.Select(0);
        LoadTransition(ordered[0]);
        Select(_eventKind!, QuestEventKind.Dialogue);
        _eventName!.Text = "dialogue.accept";
        _eventSource!.Text = "dialogue:smoke-r008";
        _transitionPriority!.Value = 90;
        Select(_idempotence!, QuestTransitionIdempotence.PerEvent);
        _idempotenceKey!.Text = "quest-smoke-r008";
        _simulationMaximumSteps!.Value = Math.Max(16, ordered.Length + 2);
        _simulationVariables!.Text = string.Empty;
        _simulationEvents!.Text = string.Join('\n', ordered.Select((transition, index) =>
        {
            var eventName = index == 0 ? "dialogue.accept" : transition.EventBinding.EventName;
            var variables = string.Join(';', transition.Conditions.Select(item =>
                $"{(item.Kind == QuestConditionKind.HasItem ? $"item:{item.Key}" : item.Key)}={item.ExpectedValue}"));
            return $"{eventName}|player:preview|{variables}";
        }));
        RebuildPreview();
        Simulate();
        return LastValidationCount == 0 && LastSimulationSucceeded;
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "QUÊTES · JOURNAL, ÉTATS & PROGRESSION" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Éditez le modèle canonique, validez chaque état et simulez une instance joueur, groupe ou monde.",
        });

        var body = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);
        var catalog = Panel("CATALOGUE");
        catalog.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        catalog.CustomMinimumSize = new(290, 0);
        body.AddChild(catalog);
        var catalogBox = catalog.GetChild<VBoxContainer>(0);
        _quests = new ItemList { CustomMinimumSize = new(285, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _quests.ItemSelected += SelectQuest;
        catalogBox.AddChild(_quests);
        catalogBox.AddChild(Button("+ Nouvelle quête", CreateNewQuest, true));

        var center = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        center.AddThemeConstantOverride("separation", 10);
        body.AddChild(center);
        var editorPanel = Panel("AUTEUR DE QUÊTE");
        editorPanel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        editorPanel.CustomMinimumSize = new(650, 0);
        center.AddChild(editorPanel);
        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editorPanel.GetChild<VBoxContainer>(0).AddChild(_tabs);

        var identity = Tab(_tabs, "IDENTITÉ & PORTÉE");
        _slug = Field(identity, "Slug stable", "ex. crypt-of-ashes");
        _titleKey = Field(identity, "Clé du titre localisé", "quest.crypt.title");
        _titleText = Field(identity, "Titre de repli", "Crypte des Cendres");
        _descriptionKey = Field(identity, "Clé de description", "quest.crypt.description");
        _descriptionText = Field(identity, "Description de repli", "Libérez la crypte.");
        var metadata = new GridContainer { Columns = 3 };
        identity.AddChild(metadata);
        _category = Field(metadata, "Catégorie du journal", "main");
        _priority = Spin(metadata, "Priorité", 1, 100, 50, 1);
        _owner = Field(metadata, "Propriétaire", "author:karen");
        _required = Check(identity, "Requise au runtime (diagnostic bloquant si invalide)");
        _defaultScope = EnumSelect<QuestProgressScope>();
        identity.AddChild(Labeled("Portée par défaut", _defaultScope));
        var scopes = new HBoxContainer();
        identity.AddChild(scopes);
        _allowPlayer = Check(scopes, "Joueur");
        _allowParty = Check(scopes, "Groupe");
        _allowWorld = Check(scopes, "Monde");
        _authority = Check(identity, "Autorité serveur requise");

        var stateTab = Tab(_tabs, "ÉTATS & JOURNAL");
        var stateSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, 610),
            SplitOffsets = [210],
        };
        stateTab.AddChild(stateSplit);
        _states = new ItemList { CustomMinimumSize = new(210, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _states.ItemSelected += SelectState;
        stateSplit.AddChild(_states);
        var stateScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        stateSplit.AddChild(stateScroll);
        var stateForm = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stateForm.AddThemeConstantOverride("separation", 7);
        stateScroll.AddChild(stateForm);
        _stateSlug = Field(stateForm, "Slug d’état", "active");
        _stateName = Field(stateForm, "Nom affiché", "Active");
        var ordering = new GridContainer { Columns = 2 };
        stateForm.AddChild(ordering);
        _displayOrder = Spin(ordering, "Ordre d’affichage", 1, 10_000, 1, 1);
        _legacyIndex = Spin(ordering, "Index historique (-1 = aucun)", -1, 10_000, -1, 1);
        _terminalKind = EnumSelect<QuestTerminalKind>();
        stateForm.AddChild(Labeled("Type final", _terminalKind));
        _reactivation = EnumSelect<QuestReactivationRule>();
        stateForm.AddChild(Labeled("Réactivation explicite", _reactivation));
        _customTerminal = Field(stateForm, "Code final personnalisé", "ending.secret");
        _summaryKey = Field(stateForm, "Clé du résumé", "quest.crypt.active.summary");
        _summaryText = Field(stateForm, "Résumé de repli", "Retrouvez la clé.");
        _summaryVariants = Multiline(stateForm, "Variantes du résumé (une par ligne : locale|texte)", 65);
        _detailsKey = Field(stateForm, "Clé des détails", "quest.crypt.active.details");
        _detailsText = Multiline(stateForm, "Détails de repli", 65);
        _objectives = Multiline(stateForm, "Objectifs localisés (clé|texte)", 65);
        _links = Multiline(stateForm, "Liens contextuels (type|référence|clé|libellé)", 65);

        var transitionTab = Tab(_tabs, "TRANSITIONS & TEST");
        var transitionSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, 610),
            SplitOffsets = [210],
        };
        transitionTab.AddChild(transitionSplit);
        var transitionCatalog = new VBoxContainer { CustomMinimumSize = new(210, 0) };
        transitionSplit.AddChild(transitionCatalog);
        _transitions = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _transitions.ItemSelected += SelectTransition;
        transitionCatalog.AddChild(_transitions);
        transitionCatalog.AddChild(Button("+ Nouvelle transition", CreateNewTransition));
        var transitionScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        transitionSplit.AddChild(transitionScroll);
        var transitionForm = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        transitionForm.AddThemeConstantOverride("separation", 7);
        transitionScroll.AddChild(transitionForm);
        _transitionFrom = new OptionButton();
        transitionForm.AddChild(Labeled("État source typé", _transitionFrom));
        _transitionTo = new OptionButton();
        transitionForm.AddChild(Labeled("État cible typé", _transitionTo));
        var transitionOrdering = new GridContainer { Columns = 2 };
        transitionForm.AddChild(transitionOrdering);
        _transitionOrder = Spin(transitionOrdering, "Ordre déterministe", 1, 10_000, 1, 1);
        _transitionPriority = Spin(transitionOrdering, "Priorité", 1, 100, 50, 1);
        _eventKind = EnumSelect<QuestEventKind>();
        transitionForm.AddChild(Labeled("Type d’événement", _eventKind));
        _eventName = Field(transitionForm, "Binding d’événement", "dialogue.accept");
        _eventSource = Field(transitionForm, "Référence source typée (optionnelle)", "dialogue:elyra");
        _idempotence = EnumSelect<QuestTransitionIdempotence>();
        transitionForm.AddChild(Labeled("Idempotence", _idempotence));
        _idempotenceKey = Field(transitionForm, "Clé d’idempotence", "quest.accept");
        _transitionConditions = Multiline(transitionForm, "Conditions typées (type|clé|valeur)", 65);
        _transitionActions = Multiline(transitionForm, "Actions typées (type|clé|valeur)", 65);
        transitionForm.AddChild(new HSeparator());
        var scenarioTitle = new Label { Text = "SCÉNARIO SANDBOX PERSISTÉ" };
        scenarioTitle.AddThemeColorOverride("font_color", new Color("ffad70"));
        transitionForm.AddChild(scenarioTitle);
        _simulationMaximumSteps = Spin(transitionForm, "Garde du nombre d’étapes", 1, 10_000, 100, 1);
        _simulationVariables = Multiline(transitionForm, "Variables initiales (clé|valeur)", 60);
        _simulationEvents = Multiline(transitionForm,
            "Événements (événement|acteur|clé=valeur;clé=valeur)", 95);

        var evidence = Panel("APERÇU, VALIDATION & SIMULATION");
        evidence.CustomMinimumSize = new(410, 0);
        center.AddChild(evidence);
        var evidenceBox = evidence.GetChild<VBoxContainer>(0);
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(385, 300),
        };
        evidenceBox.AddChild(_preview);
        evidenceBox.AddChild(Button("Valider & actualiser", RebuildPreview));
        _simulation = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new(385, 185),
        };
        evidenceBox.AddChild(_simulation);
        evidenceBox.AddChild(Button("Simuler l’instance", Simulate, true));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(385, 100) };
        evidenceBox.AddChild(_feedback);

        var actions = new HBoxContainer();
        root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Politiques avancées", () => _openAdvanced?.Invoke()));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void LoadQuest(QuestDefinition quest)
    {
        _draft = quest;
        _slug!.Text = quest.Slug;
        _titleKey!.Text = quest.Title.Key;
        _titleText!.Text = quest.Title.Fallback;
        _descriptionKey!.Text = quest.Description.Key;
        _descriptionText!.Text = quest.Description.Fallback;
        _category!.Text = quest.Category;
        _priority!.Value = quest.Priority;
        _owner!.Text = quest.Owner;
        _required!.ButtonPressed = quest.RequiredAtRuntime;
        Select(_defaultScope!, quest.InstancePolicy.DefaultScope);
        _allowPlayer!.ButtonPressed = quest.InstancePolicy.AllowedScopes.Contains(QuestProgressScope.Player);
        _allowParty!.ButtonPressed = quest.InstancePolicy.AllowedScopes.Contains(QuestProgressScope.Party);
        _allowWorld!.ButtonPressed = quest.InstancePolicy.AllowedScopes.Contains(QuestProgressScope.World);
        _authority!.ButtonPressed = quest.InstancePolicy.RequireAuthority;
        _states!.Clear();
        var states = quest.States.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).ToArray();
        foreach (var state in states)
        {
            var terminal = state.TerminalKind == QuestTerminalKind.None ? "en cours" : state.TerminalKind.ToString();
            var index = _states.AddItem($"{state.DisplayOrder:000} · {state.DisplayName} — {terminal} · " +
                                        $"ancien {state.LegacyJournalIndex?.ToString() ?? "—"}");
            _states.SetItemMetadata(index, state.Id.ToString());
        }
        _states.Select(0);
        LoadState(states[0]);
        LoadTransitionCatalog(quest);
        LoadSimulation(quest.Simulation);
        _feedback!.Text = "[color=#8ce0bd]Définition canonique chargée.[/color]";
        RebuildPreview();
    }

    private void LoadState(QuestStateDefinition state)
    {
        _selectedStateId = state.Id;
        _stateSlug!.Text = state.Slug;
        _stateName!.Text = state.DisplayName;
        _displayOrder!.Value = state.DisplayOrder;
        _legacyIndex!.Value = state.LegacyJournalIndex ?? -1;
        Select(_terminalKind!, state.TerminalKind);
        Select(_reactivation!, state.Reactivation);
        _customTerminal!.Text = state.CustomTerminalCode;
        _summaryKey!.Text = state.Journal.Summary.Key;
        _summaryText!.Text = state.Journal.Summary.Fallback;
        _summaryVariants!.Text = string.Join('\n', state.Journal.Summary.Variants.Select(item =>
            $"{item.Locale}|{item.Text}"));
        _detailsKey!.Text = state.Journal.Details.Key;
        _detailsText!.Text = state.Journal.Details.Fallback;
        _objectives!.Text = string.Join('\n', state.Journal.Objectives.Select(item => $"{item.Key}|{item.Fallback}"));
        _links!.Text = string.Join('\n', state.Journal.ContextLinks.Select(item =>
            $"{item.Kind}|{item.ReferenceId}|{item.Label?.Key ?? string.Empty}|{item.Label?.Fallback ?? string.Empty}"));
    }

    private void LoadTransitionCatalog(QuestDefinition quest)
    {
        _transitions!.Clear();
        _transitionFrom!.Clear();
        _transitionTo!.Clear();
        foreach (var state in quest.States.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value))
        {
            _transitionFrom.AddItem(state.Slug);
            _transitionFrom.SetItemMetadata(_transitionFrom.ItemCount - 1, state.Id.ToString());
            _transitionTo.AddItem(state.Slug);
            _transitionTo.SetItemMetadata(_transitionTo.ItemCount - 1, state.Id.ToString());
        }
        var transitions = quest.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).ToArray();
        foreach (var transition in transitions)
        {
            var from = quest.States.Single(item => item.Id == transition.FromStateId).Slug;
            var to = quest.States.Single(item => item.Id == transition.ToStateId).Slug;
            var index = _transitions.AddItem($"{transition.DisplayOrder:000} · {transition.EventBinding.EventName}\n{from} → {to} · P{transition.Priority}");
            _transitions.SetItemMetadata(index, transition.Id.ToString());
        }
        if (transitions.Length == 0)
        {
            _selectedTransitionId = null;
            return;
        }
        _transitions.Select(0);
        LoadTransition(transitions[0]);
    }

    private void LoadTransition(QuestTransition transition)
    {
        _selectedTransitionId = transition.Id;
        SelectStateOption(_transitionFrom!, transition.FromStateId);
        SelectStateOption(_transitionTo!, transition.ToStateId);
        _transitionOrder!.Value = transition.DisplayOrder;
        Select(_eventKind!, transition.EventBinding.Kind);
        _eventName!.Text = transition.EventBinding.EventName;
        _eventSource!.Text = transition.EventBinding.SourceReference ?? string.Empty;
        _transitionPriority!.Value = transition.Priority;
        Select(_idempotence!, transition.Idempotence);
        _idempotenceKey!.Text = transition.IdempotenceKey;
        _transitionConditions!.Text = string.Join('\n', transition.Conditions.Select(item =>
            $"{item.Kind}|{item.Key}|{item.ExpectedValue}"));
        _transitionActions!.Text = string.Join('\n', transition.Actions.Select(item =>
            $"{item.Kind}|{item.Key}|{item.Value}"));
    }

    private void LoadSimulation(QuestSimulationProfile simulation)
    {
        _simulationMaximumSteps!.Value = simulation.MaximumSteps;
        _simulationVariables!.Text = string.Join('\n', simulation.InitialVariables.Select(item =>
            $"{item.Key}|{item.Value}"));
        _simulationEvents!.Text = string.Join('\n', simulation.Events.Select(item =>
            $"{item.EventName}|{item.ActorId}|{string.Join(';', item.Variables.Select(variable => $"{variable.Key}={variable.Value}"))}"));
    }

    private QuestDefinition BuildDraft()
    {
        if (_draft is null || _selectedStateId is null)
            throw new InvalidOperationException("Aucune quête ou état sélectionné.");
        var state = BuildState(_draft.States.Single(item => item.Id == _selectedStateId));
        var states = _draft.States.Select(item => item.Id == state.Id ? state : item).ToArray();
        var transitions = _selectedTransitionId is null
            ? _draft.Transitions
            : _draft.Transitions.Select(item => item.Id == _selectedTransitionId ? BuildTransition(item) : item)
                .ToArray();
        var defaultScope = (QuestProgressScope)_defaultScope!.Selected;
        var scopes = new List<QuestProgressScope>();
        if (_allowPlayer!.ButtonPressed) scopes.Add(QuestProgressScope.Player);
        if (_allowParty!.ButtonPressed) scopes.Add(QuestProgressScope.Party);
        if (_allowWorld!.ButtonPressed) scopes.Add(QuestProgressScope.World);
        if (!scopes.Contains(defaultScope)) scopes.Add(defaultScope);
        return new QuestDefinition(
            _draft.Id, QuestDefinition.CurrentSchemaVersion, "toolset:r007", _slug!.Text.Trim(),
            _titleText!.Text.Trim(),
            new LocalizedText(_titleKey!.Text.Trim(), _titleText.Text.Trim(), provenance: "toolset:r007"),
            new LocalizedText(_descriptionKey!.Text.Trim(), _descriptionText!.Text.Trim(), provenance: "toolset:r007"),
            _category!.Text.Trim(), (int)_priority!.Value, _owner!.Text.Trim(), _required!.ButtonPressed,
            new QuestInstancePolicy(defaultScope, scopes, _authority!.ButtonPressed),
            _draft.InitialStateId, states, transitions, _draft.Operations, BuildSimulation(_draft.Simulation));
    }

    private QuestStateDefinition BuildState(QuestStateDefinition source)
    {
        var variants = ParseLines(_summaryVariants!.Text, 2, "variante").Select(parts =>
            LocalizedTextVariant.Create(_summaryKey!.Text.Trim(), parts[0], parts[1], provenance: "toolset:r007"))
            .ToArray();
        var summary = new LocalizedText(_summaryKey!.Text.Trim(), _summaryText!.Text.Trim(), variants,
            "toolset:r007");
        var details = new LocalizedText(_detailsKey!.Text.Trim(), _detailsText!.Text.Trim(),
            provenance: "toolset:r007");
        var objectives = ParseLines(_objectives!.Text, 2, "objectif")
            .Select(parts => new LocalizedText(parts[0], parts[1], provenance: "toolset:r007")).ToArray();
        var links = ParseLines(_links!.Text, 4, "lien").Select(parts =>
        {
            if (!Enum.TryParse<QuestJournalLinkKind>(parts[0], true, out var kind))
                throw new ArgumentException($"Type de lien inconnu : {parts[0]}.");
            var label = string.IsNullOrWhiteSpace(parts[2]) || string.IsNullOrWhiteSpace(parts[3])
                ? null
                : new LocalizedText(parts[2], parts[3], provenance: "toolset:r007");
            return new QuestJournalLink(kind, parts[1], label, "toolset:r007");
        }).ToArray();
        var journal = new QuestJournalEntry(summary, details, objectives, links, "toolset:r007");
        var terminal = (QuestTerminalKind)_terminalKind!.Selected;
        return new QuestStateDefinition(source.Id, QuestStateDefinition.CurrentSchemaVersion, "toolset:r007",
            _stateSlug!.Text.Trim(), _stateName!.Text.Trim(), journal, terminal != QuestTerminalKind.None,
            (int)_displayOrder!.Value, _legacyIndex!.Value < 0 ? null : (int)_legacyIndex.Value, terminal,
            _customTerminal!.Text.Trim(), (QuestReactivationRule)_reactivation!.Selected);
    }

    private QuestTransition BuildTransition(QuestTransition source)
    {
        var conditions = ParseLines(_transitionConditions!.Text, 3, "condition").Select(parts =>
        {
            if (!Enum.TryParse<QuestConditionKind>(parts[0], true, out var kind))
                throw new ArgumentException($"Type de condition inconnu : {parts[0]}.");
            return new QuestCondition(kind, parts[1], parts[2]);
        }).ToArray();
        var actions = ParseLines(_transitionActions!.Text, 3, "action").Select(parts =>
        {
            if (!Enum.TryParse<QuestActionKind>(parts[0], true, out var kind))
                throw new ArgumentException($"Type d’action inconnu : {parts[0]}.");
            return new QuestAction(kind, parts[1], parts[2]);
        }).ToArray();
        return new QuestTransition(source.Id, QuestTransition.CurrentSchemaVersion, "toolset:r008",
            SelectedState(_transitionFrom!), SelectedState(_transitionTo!), (int)_transitionOrder!.Value,
            new QuestEventBinding((QuestEventKind)_eventKind!.Selected, _eventName!.Text.Trim(),
                _eventSource!.Text.Trim(), "toolset:r008"), (int)_transitionPriority!.Value,
            (QuestTransitionIdempotence)_idempotence!.Selected, _idempotenceKey!.Text.Trim(), 1,
            conditions, actions);
    }

    private QuestSimulationProfile BuildSimulation(QuestSimulationProfile source)
    {
        var variables = ParseLines(_simulationVariables!.Text, 2, "variable")
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        var baseTime = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var events = ParseLines(_simulationEvents!.Text, 3, "événement").Select((parts, index) =>
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0) throw new ArgumentException($"Variable d’événement invalide : {pair}");
                values[pair[..separator].Trim()] = pair[(separator + 1)..].Trim();
            }
            return new QuestSimulationEvent(StableId($"{source.Id}:{index}:{parts[0]}:{parts[1]}"),
                parts[0], baseTime.AddSeconds(index + 1), parts[1], values, "toolset:r008");
        }).ToArray();
        return new QuestSimulationProfile(source.Id, QuestSimulationProfile.CurrentSchemaVersion, "toolset:r008",
            (int)_simulationMaximumSteps!.Value, variables, events);
    }

    private void RebuildPreview()
    {
        if (_preview is null) return;
        try
        {
            var candidate = BuildDraft();
            var diagnostics = QuestValidator.Validate(candidate);
            LastValidationCount = diagnostics.Count;
            var graph = string.Join('\n', candidate.States.OrderBy(item => item.DisplayOrder).Select(state =>
            {
                var targets = candidate.Transitions.Where(item => item.FromStateId == state.Id)
                    .OrderByDescending(item => item.Priority).ThenBy(item => item.DisplayOrder)
                    .Select(item => $"{item.EventBinding.EventName} → " +
                                    candidate.States.Single(target => target.Id == item.ToStateId).Slug +
                                    $" [P{item.Priority}]").ToArray();
                return $"  {state.DisplayOrder:000}  {state.Slug}  →  {(targets.Length == 0 ? "FIN" : string.Join(", ", targets))}";
            }));
            _preview.Text = $"[font_size=21][color=#ff9858]{candidate.Title.Fallback}[/color][/font_size]\n" +
                $"[color=#8ce0bd]{candidate.Category} · priorité {candidate.Priority} · {candidate.Owner}[/color]\n" +
                $"UUID {candidate.Id} · schéma v{candidate.SchemaVersion} · {candidate.Provenance}\n\n" +
                $"[b]PORTÉE[/b]\n{candidate.InstancePolicy.DefaultScope} · autorisées " +
                $"{string.Join(", ", candidate.InstancePolicy.AllowedScopes)} · autorité {candidate.InstancePolicy.RequireAuthority}\n\n" +
                $"[b]MINI-GRAPHE[/b]\n{graph}\n\n" +
                (diagnostics.Count == 0
                    ? "[color=#8ce0bd]✓ Définition valide et sérialisable[/color]"
                    : $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n" +
                      string.Join('\n', diagnostics.Select(item => $"{item.Code}\n  {item.Path}")));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            LastValidationCount = 1;
            _preview.Text = $"[color=#ff7777]Formulaire invalide[/color]\n{exception.Message}";
        }
    }

    private void Simulate()
    {
        LastSimulationSucceeded = false;
        try
        {
            var quest = BuildDraft();
            var diagnostics = QuestValidator.Validate(quest);
            if (diagnostics.Count > 0) throw new InvalidOperationException(diagnostics[0].Code);
            var result = QuestSimulationEngine.Run(quest, quest.InstancePolicy.DefaultScope,
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Player ? "player:preview" :
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Party ? "party:preview" : "world:preview",
                new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero), authorityGranted: true,
                instanceId: Guid.Parse("c7070000-0000-4000-8000-000000000001"));
            var final = quest.States.Single(item => item.Id == result.Instance.CurrentStateId);
            var trace = result.Steps.Select(step => step.TransitionId is null
                ? $"{step.Event.EventName} · refusé ({string.Join(", ", step.Diagnostics.Select(item => item.Code))})"
                : $"{step.Event.EventName} · {quest.States.Single(item => item.Id == step.BeforeStateId).Slug} → " +
                  $"{quest.States.Single(item => item.Id == step.AfterStateId).Slug} · {step.Effects.Count} effet(s)");
            LastSimulationSucceeded = result.Status is QuestSimulationStatus.Completed or QuestSimulationStatus.AwaitingEvent;
            var color = result.Status == QuestSimulationStatus.Completed ? "#8ce0bd" : "#ffbd7a";
            _simulation!.Text = $"[b]STATE EXPLORER · {result.Status}[/b]\n" +
                $"Scénario {result.ScenarioId} · {result.Steps.Count} événement(s) · {result.Audit.Count} transition(s)\n" +
                string.Join('\n', trace) + $"\n[color={color}]État final : {final.TerminalKind} · " +
                $"{final.Journal.Summary.Fallback}[/color]";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _simulation!.Text = $"[color=#ff7777]Simulation bloquée[/color]\n{exception.Message}";
        }
    }

    private void Mutate(bool dryRun)
    {
        _lastSucceeded = false;
        LastCommandId = Guid.Empty;
        LastPatchSummary = LastDiagnostics = string.Empty;
        try
        {
            var result = _mutate!(BuildDraft(), dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback!.Text = $"[color=#ff7777]Transaction refusée[/color]\n{LastDiagnostics}";
                return;
            }
            _lastSucceeded = true;
            _feedback!.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU · aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            LastDiagnostics = exception.Message;
            _feedback!.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private void SelectQuest(long index)
    {
        if (_quests is null || _load?.Invoke() is not { } state) return;
        var id = QuestId.Parse(_quests.GetItemMetadata((int)index).AsString());
        LoadQuest(state.Project.Quests.Single(item => item.Id == id));
    }

    private void SelectState(long index)
    {
        if (_states is null || _draft is null) return;
        var id = QuestStateId.Parse(_states.GetItemMetadata((int)index).AsString());
        LoadState(_draft.States.Single(item => item.Id == id));
        RebuildPreview();
    }

    private void SelectTransition(long index)
    {
        if (_transitions is null || _draft is null) return;
        var id = QuestTransitionId.Parse(_transitions.GetItemMetadata((int)index).AsString());
        LoadTransition(_draft.Transitions.Single(item => item.Id == id));
        RebuildPreview();
    }

    private void CreateNewTransition()
    {
        if (_draft is null || _draft.States.Count == 0) return;
        var orderedStates = _draft.States.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).ToArray();
        var source = orderedStates.FirstOrDefault(item => !item.IsTerminal) ?? orderedStates[0];
        var target = orderedStates.SkipWhile(item => item.Id != source.Id).Skip(1).FirstOrDefault() ?? orderedStates[^1];
        var transition = new QuestTransition(QuestTransitionId.From(Guid.NewGuid()), 1, "toolset:r008",
            source.Id, target.Id, _draft.Transitions.Count + 1,
            new QuestEventBinding(QuestEventKind.Manual, "manual", provenance: "toolset:r008"), 50,
            QuestTransitionIdempotence.None, string.Empty, 1);
        _draft = new QuestDefinition(_draft.Id, _draft.SchemaVersion, _draft.Provenance, _draft.Slug,
            _draft.DisplayName, _draft.Title, _draft.Description, _draft.Category, _draft.Priority, _draft.Owner,
            _draft.RequiredAtRuntime, _draft.InstancePolicy, _draft.InitialStateId, _draft.States,
            [.. _draft.Transitions, transition], _draft.Operations, _draft.Simulation);
        LoadTransitionCatalog(_draft);
        _transitions!.Select(_draft.Transitions.Count - 1);
        LoadTransition(transition);
        _feedback!.Text = "[color=#ffbd7a]Transition ajoutée au brouillon · appliquez pour enregistrer.[/color]";
    }

    private void CreateNewQuest()
    {
        var questId = QuestId.From(Guid.NewGuid());
        var initialId = QuestStateId.From(Guid.NewGuid());
        var completedId = QuestStateId.From(Guid.NewGuid());
        _draft = new QuestDefinition(questId, 1, "toolset:r007", $"quest-{questId.Value:N}"[..14],
            "Nouvelle quête", new("quest.new.title", "Nouvelle quête"),
            new("quest.new.description", "Décrivez la quête."), "side", 50, "author:local", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initialId,
            [
                new(initialId, 1, "toolset:r007", "initial", "Initial", new(
                    new LocalizedText("quest.new.initial.summary", "Commencer la quête"),
                    new LocalizedText("quest.new.initial.details", "Décrivez la première étape."),
                    provenance: "toolset:r007"), false, 1, 0, QuestTerminalKind.None, string.Empty,
                    QuestReactivationRule.Never),
                new(completedId, 1, "toolset:r007", "completed", "Terminée", new(
                    new LocalizedText("quest.new.completed.summary", "Quête terminée"),
                    new LocalizedText("quest.new.completed.details", "La quête est terminée."),
                    provenance: "toolset:r007"), true, 2, 1, QuestTerminalKind.Completed, string.Empty,
                    QuestReactivationRule.Never),
            ],
            [new QuestTransition(QuestTransitionId.From(Guid.NewGuid()), initialId, completedId)]);
        LoadQuest(_draft);
        _feedback!.Text = "[color=#ffbd7a]Brouillon local créé · appliquez pour l’ajouter au projet.[/color]";
    }

    private void PrepareCapture()
    {
        if (_r008Capture)
        {
            ConfigureForR008SmokeTest();
            _tabs!.CurrentTab = 2;
            _feedback!.Text = "[color=#8ce0bd]R008 prêt · événements, idempotence, linter et trace sandbox gouvernés[/color]";
        }
        else
        {
            ConfigureForSmokeTest();
            _tabs!.CurrentTab = 1;
            _feedback!.Text = "[color=#8ce0bd]R007 prêt · dry-run, undo/redo, persistance et API gouvernées[/color]";
        }
    }

    private void SetEmpty(string message)
    {
        _draft = null;
        _selectedStateId = null;
        if (_preview is not null) _preview.Text = "[color=#b8a8ce]État vide · aucune quête sélectionnée.[/color]";
        if (_simulation is not null) _simulation.Text = "Sélectionnez ou créez une quête pour simuler sa progression.";
        if (_feedback is not null) _feedback.Text = message;
    }

    private static IReadOnlyList<string[]> ParseLines(string text, int fields, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var result = new List<string[]>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|').Select(item => item.Trim()).ToArray();
            if (parts.Length != fields || parts.Take(2).Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException($"Ligne {label} invalide : {line}");
            result.Add(parts);
        }
        return result;
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new Color("ffad70"));
        box.AddChild(label);
        return panel;
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(box);
        tabs.AddChild(scroll);
        return box;
    }

    private static Control Labeled(string label, Control control)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddChild(new Label { Text = label });
        box.AddChild(control);
        return box;
    }

    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(Labeled(label, value));
        return value;
    }

    private static TextEdit Multiline(Container owner, string label, int height)
    {
        var value = new TextEdit { CustomMinimumSize = new(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(Labeled(label, value));
        return value;
    }

    private static SpinBox Spin(Container owner, string label, double min, double max, double value, double step)
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step };
        owner.AddChild(Labeled(label, spin));
        return spin;
    }

    private static CheckBox Check(Container owner, string text)
    {
        var value = new CheckBox { Text = text };
        owner.AddChild(value);
        return value;
    }

    private static OptionButton EnumSelect<T>() where T : struct, Enum
    {
        var result = new OptionButton();
        foreach (var value in Enum.GetValues<T>()) result.AddItem(value.ToString());
        return result;
    }

    private static void Select<T>(OptionButton option, T value) where T : struct, Enum =>
        option.Select(Convert.ToInt32(value));

    private static void SelectStateOption(OptionButton option, QuestStateId stateId)
    {
        for (var index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemMetadata(index).AsString() != stateId.ToString()) continue;
            option.Select(index);
            return;
        }
        throw new InvalidOperationException($"État de transition introuvable : {stateId}");
    }

    private static QuestStateId SelectedState(OptionButton option)
    {
        if (option.Selected < 0) throw new InvalidOperationException("Sélectionnez un état de transition.");
        return QuestStateId.Parse(option.GetItemMetadata(option.Selected).AsString());
    }

    private static Guid StableId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(125, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec"));
        button.Pressed += action;
        return button;
    }

}
#endif
