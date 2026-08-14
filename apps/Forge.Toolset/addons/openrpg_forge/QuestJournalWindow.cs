#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class QuestJournalWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<QuestDefinition, bool, CommandResult>? _mutate;
    private Func<QuestId, bool>? _deleteQuest;
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
    private CheckBox? _authoringEnabled;
    private CheckBox? _authoringRequired;
    private Label? _authoringSummary;
    private TextEdit? _authoringObjectives;
    private SpinBox? _rewardProgression;
    private TextEdit? _rewardItems;
    private TextEdit? _rewardCurrencies;
    private TextEdit? _rewardReputation;
    private TextEdit? _rewardUnlocks;
    private TextEdit? _rewardChoices;
    private TextEdit? _notesDesign;
    private TextEdit? _notesCanon;
    private LineEdit? _notesOwner;
    private OptionButton? _notesStatus;
    private TextEdit? _notesRisks;
    private TextEdit? _notesTodos;
    private TextEdit? _notesSources;
    private TextEdit? _notesReviews;
    private TextEdit? _authoringParticipants;
    private TextEdit? _wizardZones;
    private TextEdit? _wizardProps;
    private TextEdit? _wizardDialogues;
    private TextEdit? _wizardTests;
    private CheckBox? _wizardOpaqueCode;
    private CheckBox? _experienceEnabled;
    private CheckBox? _experienceRequired;
    private Label? _experienceSummary;
    private TextEdit? _experienceResources;
    private TextEdit? _experienceSteps;
    private CheckBox? _experienceManualTracking;
    private TextEdit? _experienceRevealConditions;
    private TextEdit? _experienceHistory;
    private TextEdit? _experienceMarkers;
    private CheckBox? _qualityEnabled;
    private CheckBox? _qualityRequired;
    private Label? _qualitySummary;
    private TextEdit? _qualityScenarioSeeds;
    private LineEdit? _qualitySynopsisKey;
    private LineEdit? _qualitySynopsisText;
    private TextEdit? _qualityDependencies;
    private TextEdit? _qualityCitations;
    private TextEdit? _qualityStyleRules;
    private LineEdit? _qualityRequestedBy;
    private CheckBox? _qualityApproved;
    private LineEdit? _qualityReviewer;
    private RichTextLabel? _preview;
    private RichTextLabel? _simulation;
    private RichTextLabel? _feedback;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;
    private bool _r008Capture;
    private bool _r071Capture;
    private bool _r072Capture;
    private bool _r073Capture;
    private bool _lastSucceeded;

    public QuestId? SelectedQuestId => _draft?.Id;
    public int StateCount => _draft?.States.Count ?? 0;
    public int LastValidationCount { get; private set; }
    public bool LastSimulationSucceeded { get; private set; }
    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int LastAuthoringObjectiveCount => _draft?.Authoring?.Objectives.Count ?? 0;
    public int LastAuthoringParticipantCount => _draft?.Authoring?.Participants.Count ?? 0;
    public int LastExperienceResourceCount => _draft?.Experience?.Resources.Count ?? 0;
    public int LastExperienceMarkerCount => _draft?.Experience?.Markers.Count ?? 0;
    public int LastQualityScenarioCount => _draft?.Quality?.TestScenarios.Count ?? 0;
    public bool LastQualityPlanApproved => _draft?.Quality?.AiPlan.ApprovedAt is not null;

    public void Initialize(
        Func<ProjectState?> load,
        Func<QuestDefinition, bool, CommandResult> mutate,
        Func<QuestId, bool> deleteQuest,
        Action undo,
        Action redo,
        Action openAdvanced,
        Action<Window> applyIcon)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _mutate = mutate ?? throw new ArgumentNullException(nameof(mutate));
        _deleteQuest = deleteQuest ?? throw new ArgumentNullException(nameof(deleteQuest));
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
            value.StartsWith("--quest-transition-ui-capture=", StringComparison.Ordinal) ||
             value.StartsWith("--r071-quest-authoring-ui-capture=", StringComparison.Ordinal) ||
             value.StartsWith("--r072-quest-experience-ui-capture=", StringComparison.Ordinal) ||
             value.StartsWith("--r073-quest-quality-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _r008Capture = capture.StartsWith("--quest-transition-ui-capture=", StringComparison.Ordinal);
            _r071Capture = capture.StartsWith("--r071-quest-authoring-ui-capture=", StringComparison.Ordinal);
            _r072Capture = capture.StartsWith("--r072-quest-experience-ui-capture=", StringComparison.Ordinal);
            _r073Capture = capture.StartsWith("--r073-quest-quality-ui-capture=", StringComparison.Ordinal);
            var prefix = _r073Capture ? "--r073-quest-quality-ui-capture=" :
                _r072Capture ? "--r072-quest-experience-ui-capture=" :
                _r071Capture ? "--r071-quest-authoring-ui-capture=" :
                _r008Capture ? "--quest-transition-ui-capture=" : "--quest-journal-ui-capture=";
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
        var marker = _r073Capture ? "OPENRPG_FORGE_R073_QUEST_QUALITY_UI_CAPTURE" :
            _r072Capture ? "OPENRPG_FORGE_R072_QUEST_EXPERIENCE_UI_CAPTURE" :
            _r071Capture ? "OPENRPG_FORGE_R071_QUEST_AUTHORING_UI_CAPTURE" :
            "OPENRPG_FORGE_QUEST_JOURNAL_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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

    public bool ConfigureForR071SmokeTest()
    {
        Refresh();
        if (_draft is null || _draft.Transitions.Count == 0 || _load?.Invoke() is not { } state ||
            state.Project.Dialogues.Count == 0)
            return false;
        _authoringEnabled!.ButtonPressed = true;
        _authoringRequired!.ButtonPressed = true;
        _authoringObjectives!.Text = string.Join('\n', new string[]
        {
            "Counter|quest.r071.counter|Vaincre les sentinelles|2||||||false",
            "Boolean|quest.r071.boolean|Allumer le brasier|1||||||false",
            "Collection|quest.r071.collection|Réunir la clé|1|ash-key|||||false",
            "Destination|quest.r071.destination|Atteindre la crypte|0||crypt-of-ashes||||false",
            "Escort|quest.r071.escort|Escorter Elyra|0|elyra-npc|crypt-of-ashes||||false",
            "Survival|quest.r071.survival|Survivre au rituel|0|||60|||false",
            "Choice|quest.r071.choice|Choisir le destin|0||||choice:dawn;choice:dusk||false",
            "Custom|quest.r071.custom|Sceller le pacte|0|||||rule:ember|true",
        });
        _rewardProgression!.Value = 250;
        _rewardItems!.Text = "ash-key=1";
        _rewardCurrencies!.Text = "ember=5";
        _rewardReputation!.Text = "ash-watch=2";
        _rewardUnlocks!.Text = "unlock:crypt";
        _rewardChoices!.Text = "choice:dawn";
        _notesDesign!.Text = "Restaurer la veillée et exposer trois voies de résolution.";
        _notesCanon!.Text = "Le pacte des braises lie Elyra à la Crypte des Cendres.";
        _notesOwner!.Text = "author:karen";
        Select(_notesStatus!, QuestEditorialStatus.Review);
        _notesRisks!.Text = "complexité des branches";
        _notesTodos!.Text = "enregistrer les voix";
        _notesSources!.Text = "08_QUEST.md\nPROJECT_CONTEXT.md";
        _notesReviews!.Text = "reviewer:elyra|Review|Structure et canon vérifiés";
        _authoringParticipants!.Text = "guide|Blueprint|elyra-npc|quest:active|true";
        _wizardZones!.Text = "crypt-of-ashes";
        _wizardProps!.Text = "crypt-pillar";
        _wizardDialogues!.Text = state.Project.Dialogues[0].Id.ToString();
        _wizardTests!.Text = "nominal-path\nobjective-failure\nrepeated-transition\nmultiplayer-authority\nsave-resume";
        _wizardOpaqueCode!.ButtonPressed = false;
        RebuildPreview();
        Simulate();
        return LastValidationCount == 0 && LastSimulationSucceeded;
    }

    public bool ConfigureForR072SmokeTest()
    {
        if (!ConfigureForR071SmokeTest() || _draft is null || _load?.Invoke() is not { } state) return false;
        var area = state.Project.Areas[0];
        var entity = area.Entities.FirstOrDefault()?.Id.ToString() ?? "entity:crypt-guardian";
        _experienceEnabled!.ButtonPressed = true;
        _experienceRequired!.ButtonPressed = true;
        _experienceResources!.Text = string.Join('\n', new[]
        {
            "Item|ash-key|owner:module|false|quest:active",
            "Placeable|crypt-pillar|owner:module|false|quest:active",
            "Door|crypt-gate|owner:module|false|quest:active",
            $"Zone|{area.Slug}|owner:module|true|quest:active",
            "Asset|asset:ember-prism|owner:module|true|quest:active",
        });
        _experienceSteps!.Text =
            $"investigation|2|quest.r072.step|Inspecter le prisme|0|0|true";
        _experienceManualTracking!.ButtonPressed = true;
        _experienceRevealConditions!.Text = string.Join('\n', new[]
        {
            "Rumor|heard",
            "Discovered|heard;seen",
            "Tracked|accepted",
            "SecretCompleted|secret:complete",
        });
        _experienceHistory!.Text = string.Join('\n', new[]
        {
            "none|hidden|module-load|server|quest.r072.history.hidden|Un murmure se lève.|seed=72",
            "hidden|discovered|dialogue|player:smoke|quest.r072.history.discovered|La Crypte est révélée.|speaker=elyra",
        });
        _experienceMarkers!.Text = string.Join('\n', new[]
        {
            $"Zone|{area.Slug}|0|Discovered|8|true|quest.r072.marker.zone|Crypte des Cendres|",
            $"Entity|{entity}|0|Discovered|4|true|quest.r072.marker.entity|Gardien des Cendres|",
            "Query|query:nearest-ember|0|Rumor|12|true|quest.r072.marker.query|Écho des braises|",
            "Waypoint|waypoint:crypt-entry|0|Tracked|2|true|quest.r072.marker.waypoint|Entrée de la crypte|1,0,2",
        });
        RebuildPreview();
        Simulate();
        return LastValidationCount == 0 && LastSimulationSucceeded;
    }

    public bool ConfigureForR073SmokeTest()
    {
        if (!ConfigureForR072SmokeTest() || _draft is null) return false;
        _qualityEnabled!.ButtonPressed = true;
        _qualityRequired!.ButtonPressed = true;
        _qualityScenarioSeeds!.Text = string.Join('\n', new[]
        {
            "Nominal|7300", "Failure|7301", "Repetition|7302", "Multiplayer|7303", "SaveResume|7304",
        });
        _qualitySynopsisKey!.Text = "quest.r073.ai.synopsis";
        _qualitySynopsisText!.Text = "Restaurer la veillée avant l'effondrement de la Crypte des Cendres.";
        var candidate = BuildDraft();
        var participant = candidate.Authoring!.Participants[0].Id;
        _qualityDependencies!.Text = string.Join('\n', new[]
        {
            $"State|{candidate.InitialStateId}",
            $"Transition|{candidate.Transitions[0].Id}",
            $"Participant|{participant:D}",
            $"Zone|{candidate.Authoring.Wizard.ZoneIds[0]}",
            "Asset|asset:quest-generation-model",
        });
        _qualityCitations!.Text = "08_QUEST.md#orf-qst-025\nPROJECT_CONTEXT.md";
        _qualityStyleRules!.Text = "style:crypt\nstyle:human-review";
        _qualityRequestedBy!.Text = "agent:forge";
        _qualityApproved!.ButtonPressed = true;
        _qualityReviewer!.Text = "author:karen";
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
        catalogBox.AddChild(Button("Supprimer la quête", DeleteCurrentQuest));

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
        var stateCatalog = new VBoxContainer { CustomMinimumSize = new(210, 0) };
        stateSplit.AddChild(stateCatalog);
        _states = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _states.ItemSelected += SelectState;
        stateCatalog.AddChild(_states);
        stateCatalog.AddChild(Button("+ Nouvel état", CreateNewState));
        stateCatalog.AddChild(Button("Supprimer l’état", RemoveSelectedState));
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
        _summaryVariants = Multiline(stateForm, "Variantes du résumé", 65);
        _detailsKey = Field(stateForm, "Clé des détails", "quest.crypt.active.details");
        _detailsText = Multiline(stateForm, "Détails de repli", 65);
        _objectives = Multiline(stateForm, "Objectifs localisés", 65);
        _links = Multiline(stateForm, "Liens contextuels", 65);

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
        transitionCatalog.AddChild(Button("Supprimer la transition", RemoveSelectedTransition));
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
        _transitionConditions = Multiline(transitionForm, "Conditions typées", 65);
        _transitionActions = Multiline(transitionForm, "Actions typées", 65);
        transitionForm.AddChild(new HSeparator());
        var scenarioTitle = new Label { Text = "SCÉNARIO SANDBOX PERSISTÉ" };
        scenarioTitle.AddThemeColorOverride("font_color", new Color("ffad70"));
        transitionForm.AddChild(scenarioTitle);
        _simulationMaximumSteps = Spin(transitionForm, "Garde du nombre d’étapes", 1, 10_000, 100, 1);
        _simulationVariables = Multiline(transitionForm, "Variables initiales", 60);
        _simulationEvents = Multiline(transitionForm,
            "Événements de simulation", 95);

        var authoringTab = Tab(_tabs, "OBJECTIFS, CAST & WIZARD");
        var authoringFlags = new HBoxContainer();
        authoringTab.AddChild(authoringFlags);
        _authoringEnabled = Check(authoringFlags, "Profil auteur actif");
        _authoringRequired = Check(authoringFlags, "Requis au runtime");
        _authoringSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _authoringSummary.AddThemeColorOverride("font_color", new Color("8ce0bd"));
        authoringTab.AddChild(_authoringSummary);
        _authoringObjectives = Multiline(authoringTab,
            "Objectifs structurés", 125);
        var rewardGrid = new GridContainer { Columns = 2 };
        authoringTab.AddChild(rewardGrid);
        _rewardProgression = Spin(rewardGrid, "Progression", 0, 1_000_000, 0, 1);
        _rewardItems = Multiline(rewardGrid, "Objets (référence=quantité)", 58);
        _rewardCurrencies = Multiline(rewardGrid, "Monnaies (id=montant)", 58);
        _rewardReputation = Multiline(rewardGrid, "Réputation (faction=delta)", 58);
        _rewardUnlocks = Multiline(rewardGrid, "Déverrouillages", 58);
        _rewardChoices = Multiline(rewardGrid, "Choix de récompense", 58);
        _notesDesign = Multiline(authoringTab, "Notes de design", 58);
        _notesCanon = Multiline(authoringTab, "Canon", 58);
        var notesMeta = new GridContainer { Columns = 2 };
        authoringTab.AddChild(notesMeta);
        _notesOwner = Field(notesMeta, "Propriétaire", "author:karen");
        _notesStatus = EnumSelect<QuestEditorialStatus>();
        notesMeta.AddChild(Labeled("Statut éditorial", _notesStatus));
        var notesGrid = new GridContainer { Columns = 3 };
        authoringTab.AddChild(notesGrid);
        _notesRisks = Multiline(notesGrid, "Risques", 58);
        _notesTodos = Multiline(notesGrid, "TODO", 58);
        _notesSources = Multiline(notesGrid, "Sources", 58);
        _notesReviews = Multiline(authoringTab, "Historique de revue", 68);
        _authoringParticipants = Multiline(authoringTab,
            "Participants et rôles", 80);
        var wizardGrid = new GridContainer { Columns = 2 };
        authoringTab.AddChild(wizardGrid);
        _wizardZones = Multiline(wizardGrid, "Wizard · zones", 58);
        _wizardProps = Multiline(wizardGrid, "Wizard · props", 58);
        _wizardDialogues = Multiline(wizardGrid, "Wizard · dialogues UUID", 58);
        _wizardTests = Multiline(wizardGrid, "Tests générés", 82);
        _wizardOpaqueCode = Check(authoringTab, "Générer du code opaque (doit rester désactivé)");

        var experienceTab = Tab(_tabs, "RESSOURCES, HISTORIQUE & PINS");
        var experienceFlags = new HBoxContainer();
        experienceTab.AddChild(experienceFlags);
        _experienceEnabled = Check(experienceFlags, "Expérience active");
        _experienceRequired = Check(experienceFlags, "Requise au runtime");
        _experienceManualTracking = Check(experienceFlags, "Suivi manuel autorisé");
        _experienceSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _experienceSummary.AddThemeColorOverride("font_color", new Color("8ce0bd"));
        experienceTab.AddChild(_experienceSummary);
        _experienceResources = Multiline(experienceTab,
            "Ressources de quête", 84);
        _experienceSteps = Multiline(experienceTab,
            "Étapes de quête", 58);
        _experienceRevealConditions = Multiline(experienceTab,
            "Règles de révélation", 70);
        _experienceHistory = Multiline(experienceTab,
            "Chronologie", 72);
        _experienceMarkers = Multiline(experienceTab,
            "Repères sur la carte", 90);

        var qualityTab = Tab(_tabs, "TESTS & GÉNÉRATION IA");
        var qualityFlags = new HBoxContainer();
        qualityTab.AddChild(qualityFlags);
        _qualityEnabled = Check(qualityFlags, "Qualité active");
        _qualityRequired = Check(qualityFlags, "Requise au runtime");
        _qualityApproved = Check(qualityFlags, "Plan approuvé par un humain");
        _qualitySummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _qualitySummary.AddThemeColorOverride("font_color", new Color("8ce0bd"));
        qualityTab.AddChild(_qualitySummary);
        _qualityScenarioSeeds = Multiline(qualityTab,
            "Scénarios de test", 125);
        var aiGrid = new GridContainer { Columns = 2 };
        qualityTab.AddChild(aiGrid);
        _qualitySynopsisKey = Field(aiGrid, "Clé du synopsis IA", "quest.crypt.ai.synopsis");
        _qualitySynopsisText = Field(aiGrid, "Synopsis proposé", "Restaurer la veillée.");
        _qualityRequestedBy = Field(aiGrid, "Agent demandeur", "agent:forge");
        _qualityReviewer = Field(aiGrid, "Validateur humain", "author:karen");
        _qualityDependencies = Multiline(qualityTab,
            "Dépendances typées", 100);
        var qualityGovernance = new GridContainer { Columns = 2 };
        qualityTab.AddChild(qualityGovernance);
        _qualityCitations = Multiline(qualityGovernance, "Citations de contexte", 60);
        _qualityStyleRules = Multiline(qualityGovernance, "Règles de style", 60);
        BuildStructuredEditors();

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
        LoadAuthoring(quest.Authoring);
        LoadExperience(quest.Experience);
        LoadQuality(quest.Quality);
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

    private void LoadAuthoring(QuestAuthoringProfile? profile)
    {
        _authoringEnabled!.ButtonPressed = profile?.Enabled ?? false;
        _authoringRequired!.ButtonPressed = profile?.RequiredAtRuntime ?? false;
        _authoringObjectives!.Text = profile is null ? string.Empty : string.Join('\n', profile.Objectives
            .OrderBy(item => item.DisplayOrder).Select(item =>
                $"{item.Kind}|{item.Label.Key}|{item.Label.Fallback}|{item.Target}|" +
                $"{string.Join(';', item.TargetIds)}|{item.DestinationId}|{item.DurationSeconds}|" +
                $"{string.Join(';', item.ChoiceIds)}|{item.CustomRuleId}|{item.Optional}"));
        _rewardProgression!.Value = profile?.Rewards.Progression ?? 0;
        _rewardItems!.Text = profile is null ? string.Empty : FormatMap(profile.Rewards.Items);
        _rewardCurrencies!.Text = profile is null ? string.Empty : FormatMap(profile.Rewards.Currencies);
        _rewardReputation!.Text = profile is null ? string.Empty : FormatMap(profile.Rewards.Reputation);
        _rewardUnlocks!.Text = profile is null ? string.Empty : string.Join('\n', profile.Rewards.Unlocks);
        _rewardChoices!.Text = profile is null ? string.Empty : string.Join('\n', profile.Rewards.ChoiceIds);
        _notesDesign!.Text = profile?.Notes.Design ?? string.Empty;
        _notesCanon!.Text = profile?.Notes.Canon ?? string.Empty;
        _notesOwner!.Text = profile?.Notes.OwnerId ?? string.Empty;
        Select(_notesStatus!, profile?.Notes.Status ?? QuestEditorialStatus.Draft);
        _notesRisks!.Text = profile is null ? string.Empty : string.Join('\n', profile.Notes.Risks);
        _notesTodos!.Text = profile is null ? string.Empty : string.Join('\n', profile.Notes.Todos);
        _notesSources!.Text = profile is null ? string.Empty : string.Join('\n', profile.Notes.Sources);
        _notesReviews!.Text = profile is null ? string.Empty : string.Join('\n', profile.Notes.ReviewHistory
            .OrderBy(item => item.At).ThenBy(item => item.Id)
            .Select(item => $"{item.AuthorId}|{item.Status}|{item.Message}"));
        _authoringParticipants!.Text = profile is null ? string.Empty : string.Join('\n', profile.Participants
            .OrderBy(item => item.DisplayOrder).Select(item =>
                $"{item.Role}|{item.BindingKind}|{item.BindingValue}|" +
                $"{string.Join(';', item.PresenceConditions)}|{item.Required}"));
        _wizardZones!.Text = profile is null ? string.Empty : string.Join('\n', profile.Wizard.ZoneIds);
        _wizardProps!.Text = profile is null ? string.Empty : string.Join('\n', profile.Wizard.PropIds);
        _wizardDialogues!.Text = profile is null ? string.Empty : string.Join('\n', profile.Wizard.DialogueIds);
        _wizardTests!.Text = profile is null ? string.Empty : string.Join('\n', profile.Wizard.GeneratedTestScenarios);
        _wizardOpaqueCode!.ButtonPressed = profile?.Wizard.GeneratesOpaqueCode ?? false;
        _authoringSummary!.Text = profile is null
            ? "État vide · ajoutez des objectifs, un cast et un plan Wizard."
            : $"{profile.Objectives.Count} objectifs · {profile.Participants.Count} participant(s) · " +
              $"{profile.Wizard.ZoneIds.Count} zone(s) · {profile.Wizard.PropIds.Count} prop(s) · " +
              $"{profile.Wizard.DialogueIds.Count} dialogue(s) · {profile.Wizard.GeneratedTestScenarios.Count} tests · " +
              $"code opaque {(profile.Wizard.GeneratesOpaqueCode ? "OUI" : "non")} · {profile.Notes.Status}";
    }

    private void LoadExperience(QuestExperienceProfile? profile)
    {
        _experienceEnabled!.ButtonPressed = profile?.Enabled ?? false;
        _experienceRequired!.ButtonPressed = profile?.RequiredAtRuntime ?? false;
        _experienceManualTracking!.ButtonPressed = profile?.Visibility.AllowManualTracking ?? false;
        _experienceResources!.Text = profile is null ? string.Empty : string.Join('\n', profile.Resources
            .OrderBy(item => item.DisplayOrder).Select(item =>
                $"{item.Kind}|{item.ReferenceId}|{item.OwnerId}|{item.Required}|{string.Join(';', item.Conditions)}"));
        _experienceSteps!.Text = profile is null || _draft?.Authoring is null ? string.Empty : string.Join('\n',
            profile.Steps.OrderBy(item => item.DisplayOrder).Select(item =>
                $"{item.TemplateId}|{item.TemplateVersion}|{item.Label.Key}|{item.Label.Fallback}|" +
                $"{string.Join(';', item.ObjectiveIds.Select(id => IndexOf(_draft.Authoring.Objectives.Select(objective => objective.Id), id)))}|" +
                $"{string.Join(';', item.TransitionIds.Select(id => IndexOf(_draft.Transitions.Select(transition => transition.Id), id)))}|{item.Editable}"));
        _experienceRevealConditions!.Text = profile is null ? string.Empty : string.Join('\n',
            profile.Visibility.RevealConditions.OrderBy(item => item.Key).Select(item =>
                $"{item.Key}|{string.Join(';', item.Value)}"));
        _experienceHistory!.Text = profile is null ? string.Empty : string.Join('\n', profile.History
            .OrderBy(item => item.At).ThenBy(item => item.Id).Select(item =>
                $"{item.FromState}|{item.ToState}|{item.Cause}|{item.ActorId}|{item.PlayerSummary.Key}|" +
                $"{item.PlayerSummary.Fallback}|{string.Join(';', item.DebugContext.Select(pair => $"{pair.Key}={pair.Value}"))}"));
        _experienceMarkers!.Text = profile is null || _draft?.Authoring is null ? string.Empty : string.Join('\n',
            profile.Markers.OrderBy(item => item.DisplayOrder).Select(item =>
                $"{item.BindingKind}|{item.BindingValue}|" +
                $"{IndexOf(_draft.Authoring.Objectives.Select(objective => objective.Id), item.ObjectiveId)}|" +
                $"{item.Visibility}|{item.PrecisionRadius.ToString(System.Globalization.CultureInfo.InvariantCulture)}|" +
                $"{item.Tracked}|{item.Label.Key}|{item.Label.Fallback}|" +
                (item.FallbackPosition is { } position
                    ? $"{position.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{position.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      position.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty)));
        _experienceSummary!.Text = profile is null
            ? "État vide · ajoutez ressources, étapes, visibilité, chronologie et pins."
            : $"{profile.Resources.Count} ressources · {profile.Steps.Count} étape(s) · " +
              $"{profile.Visibility.RevealConditions.Count} révélations · {profile.History.Count} événements · " +
              $"{profile.Markers.Count} pins · suivi manuel {(profile.Visibility.AllowManualTracking ? "oui" : "non")}";
    }

    private void LoadQuality(QuestQualityProfile? profile)
    {
        _qualityEnabled!.ButtonPressed = profile?.Enabled ?? false;
        _qualityRequired!.ButtonPressed = profile?.RequiredAtRuntime ?? false;
        _qualityApproved!.ButtonPressed = profile?.AiPlan.ApprovedAt is not null;
        _qualityReviewer!.Text = profile?.AiPlan.ApprovedBy ?? string.Empty;
        _qualityScenarioSeeds!.Text = profile is null ? string.Empty : string.Join('\n',
            profile.TestScenarios.OrderBy(item => item.DisplayOrder).Select(item => $"{item.Kind}|{item.Seed}"));
        _qualitySynopsisKey!.Text = profile?.AiPlan.Synopsis.Key ?? string.Empty;
        _qualitySynopsisText!.Text = profile?.AiPlan.Synopsis.Fallback ?? string.Empty;
        _qualityRequestedBy!.Text = profile?.AiPlan.RequestedBy ?? string.Empty;
        _qualityDependencies!.Text = profile is null ? string.Empty : string.Join('\n',
            profile.AiPlan.Dependencies.OrderBy(item => item.DisplayOrder)
                .Select(item => $"{item.Kind}|{item.ReferenceId}"));
        _qualityCitations!.Text = profile is null ? string.Empty : string.Join('\n', profile.AiPlan.ContextCitations);
        _qualityStyleRules!.Text = profile is null ? string.Empty : string.Join('\n', profile.AiPlan.StyleRules);
        _qualitySummary!.Text = profile is null
            ? "État vide · générez les cinq scénarios et préparez une proposition IA gouvernée."
            : $"{profile.TestScenarios.Count} scénarios · {profile.TestScenarios.Sum(item => item.Steps.Count)} nœuds · " +
              $"{profile.TestScenarios.Sum(item => item.Edges.Count)} arêtes · plan {profile.AiPlan.Id:D} · " +
              $"autorisation {profile.AiPlan.Authorization} · lint {profile.AiPlan.Lint.Count}";
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
        var authoring = BuildAuthoring(_draft, transitions);
        var candidate = new QuestDefinition(
            _draft.Id, QuestDefinition.CurrentSchemaVersion, "toolset:r007", _slug!.Text.Trim(),
            _titleText!.Text.Trim(),
            new LocalizedText(_titleKey!.Text.Trim(), _titleText.Text.Trim(), provenance: "toolset:r007"),
            new LocalizedText(_descriptionKey!.Text.Trim(), _descriptionText!.Text.Trim(), provenance: "toolset:r007"),
            _category!.Text.Trim(), (int)_priority!.Value, _owner!.Text.Trim(), _required!.ButtonPressed,
            new QuestInstancePolicy(defaultScope, scopes, _authority!.ButtonPressed),
            _draft.InitialStateId, states, transitions, _draft.Operations, BuildSimulation(_draft.Simulation),
            authoring);
        candidate = candidate.WithExperience(BuildExperience(candidate));
        return candidate.WithQuality(BuildQuality(candidate));
    }

    private QuestQualityProfile? BuildQuality(QuestDefinition source)
    {
        if (!_qualityEnabled!.ButtonPressed && !_qualityRequired!.ButtonPressed) return null;
        if (source.Authoring is null)
            throw new ArgumentException("Le profil qualité exige le profil auteur canonique et ses références typées.");
        var seeds = ParseLines(_qualityScenarioSeeds!.Text, 2, "scénario généré").ToDictionary(parts =>
        {
            if (!Enum.TryParse<QuestQualityTestKind>(parts[0], true, out var kind))
                throw new ArgumentException($"Type de scénario inconnu : {parts[0]}.");
            return kind;
        }, parts =>
        {
            if (!int.TryParse(parts[1], out var seed) || seed <= 0)
                throw new ArgumentException($"Seed de scénario invalide : {parts[1]}.");
            return seed;
        });
        var scenarios = QuestQualityRuntimeService.GenerateTests(source).Select(item => item with
        {
            Seed = seeds.TryGetValue(item.Kind, out var seed) ? seed : item.Seed,
            Provenance = "toolset:r073",
        }).ToArray();
        var dependencies = ParseLines(_qualityDependencies!.Text, 2, "dépendance IA")
            .Select((parts, index) =>
            {
                if (!Enum.TryParse<QuestAiDependencyKind>(parts[0], true, out var kind))
                    throw new ArgumentException($"Type de dépendance IA inconnu : {parts[0]}.");
                return new QuestAiDependency(StableId($"{source.Id}:r073:dependency:{index}"), 1,
                    "toolset:r073", index + 1, kind, parts[1]);
            }).ToArray();
        var plan = QuestQualityRuntimeService.CreateAiPlan(source,
            new LocalizedText(_qualitySynopsisKey!.Text.Trim(), _qualitySynopsisText!.Text.Trim(),
                provenance: "toolset:r073"), dependencies, TextLines(_qualityCitations!.Text),
            TextLines(_qualityStyleRules!.Text), _qualityRequestedBy!.Text.Trim(),
            StableId($"{source.Id}:r073:transaction"),
            $"preview:quest:{source.Id}|states:{source.States.Count}|participants:{source.Authoring.Participants.Count}|tests:{scenarios.Length}");
        if (_qualityApproved!.ButtonPressed)
            plan = QuestQualityRuntimeService.Approve(plan, _qualityReviewer!.Text.Trim(),
                new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero));
        return new QuestQualityProfile(StableId($"{source.Id}:r073:quality"), 1, "toolset:r073",
            _qualityEnabled.ButtonPressed, _qualityRequired!.ButtonPressed, scenarios, plan);
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

    private QuestAuthoringProfile? BuildAuthoring(
        QuestDefinition source,
        IReadOnlyList<QuestTransition> transitions)
    {
        if (!_authoringEnabled!.ButtonPressed && !_authoringRequired!.ButtonPressed) return null;
        var objectives = ParseLines(_authoringObjectives!.Text, 10, "objectif auteur")
            .Select((parts, index) =>
            {
                if (!Enum.TryParse<QuestObjectiveKind>(parts[0], true, out var kind))
                    throw new ArgumentException($"Type d’objectif inconnu : {parts[0]}.");
                if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var target))
                    throw new ArgumentException($"Cible d’objectif invalide : {parts[3]}.");
                if (!double.TryParse(string.IsNullOrWhiteSpace(parts[6]) ? "0" : parts[6],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                        out var duration))
                    throw new ArgumentException($"Durée d’objectif invalide : {parts[6]}.");
                if (!bool.TryParse(parts[9], out var optional))
                    throw new ArgumentException($"Optionnel d’objectif invalide : {parts[9]}.");
                return new QuestAuthoringObjective(StableId($"{source.Id}:r071:objective:{index}"), 1,
                    "toolset:r071", index + 1, kind,
                    new LocalizedText(parts[1], parts[2], provenance: "toolset:r071"), target,
                    SplitValues(parts[4]), EmptyToNull(parts[5]), duration, SplitValues(parts[7]),
                    EmptyToNull(parts[8]), optional);
            }).ToArray();
        var participants = ParseLines(_authoringParticipants!.Text, 5, "participant")
            .Select((parts, index) =>
            {
                if (!Enum.TryParse<QuestParticipantBindingKind>(parts[1], true, out var bindingKind))
                    throw new ArgumentException($"Type de binding inconnu : {parts[1]}.");
                if (!bool.TryParse(parts[4], out var required))
                    throw new ArgumentException($"Requis participant invalide : {parts[4]}.");
                return new QuestAuthoringParticipant(StableId($"{source.Id}:r071:participant:{index}"), 1,
                    "toolset:r071", index + 1, parts[0], bindingKind, parts[2], SplitValues(parts[3]), required);
            }).ToArray();
        var reviews = ParseLines(_notesReviews!.Text, 3, "review").Select((parts, index) =>
        {
            if (!Enum.TryParse<QuestEditorialStatus>(parts[1], true, out var status))
                throw new ArgumentException($"Statut de review inconnu : {parts[1]}.");
            return new QuestAuthorReview(StableId($"{source.Id}:r071:review:{index}"), 1, "toolset:r071",
                parts[0], status, parts[2], new DateTimeOffset(2026, 8, 11, 0, 0, index, TimeSpan.Zero));
        }).ToArray();
        var rewards = new QuestAuthoringRewardBundle(StableId($"{source.Id}:r071:rewards"), 1,
            "toolset:r071", _rewardProgression!.Value, ParseMap(_rewardItems!.Text, int.Parse),
            ParseMap(_rewardCurrencies!.Text, value => decimal.Parse(value,
                System.Globalization.CultureInfo.InvariantCulture)),
            ParseMap(_rewardReputation!.Text, value => double.Parse(value,
                System.Globalization.CultureInfo.InvariantCulture)),
            TextLines(_rewardUnlocks!.Text), TextLines(_rewardChoices!.Text));
        var notes = new QuestAuthorNotes(_notesDesign!.Text.Trim(), _notesCanon!.Text.Trim(),
            _notesOwner!.Text.Trim(), (QuestEditorialStatus)_notesStatus!.Selected,
            TextLines(_notesRisks!.Text), TextLines(_notesTodos!.Text), TextLines(_notesSources!.Text), reviews);
        var wizard = new QuestPlotWizardPlan(StableId($"{source.Id}:r071:wizard"), 1, "toolset:r071",
            TextLines(_wizardZones!.Text), participants.Select(item => item.Id).ToArray(),
            TextLines(_wizardProps!.Text), TextLines(_wizardDialogues!.Text).Select(DialogueId.Parse).ToArray(),
            objectives.Select(item => item.Id).ToArray(), transitions.Select(item => item.Id).ToArray(),
            TextLines(_wizardTests!.Text), _wizardOpaqueCode!.ButtonPressed);
        return new QuestAuthoringProfile(StableId($"{source.Id}:r071:authoring"), 1, "toolset:r071",
            _authoringEnabled!.ButtonPressed, _authoringRequired!.ButtonPressed, objectives, rewards, notes,
            participants, wizard);
    }

    private QuestExperienceProfile? BuildExperience(QuestDefinition source)
    {
        if (!_experienceEnabled!.ButtonPressed && !_experienceRequired!.ButtonPressed) return null;
        if (source.Authoring is null)
            throw new ArgumentException("Le profil d’expérience exige au moins un objectif auteur canonique.");
        var objectives = source.Authoring.Objectives.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToArray();
        var transitions = source.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).ToArray();
        var resources = ParseLines(_experienceResources!.Text, 5, "ressource de quête")
            .Select((parts, index) =>
            {
                if (!Enum.TryParse<QuestExperienceResourceKind>(parts[0], true, out var kind))
                    throw new ArgumentException($"Type de ressource inconnu : {parts[0]}.");
                if (!bool.TryParse(parts[3], out var required))
                    throw new ArgumentException($"Indicateur requis invalide : {parts[3]}.");
                return new QuestExperienceResource(StableId($"{source.Id}:r072:resource:{index}"), 1,
                    "toolset:r072", index + 1, kind, parts[1], parts[2], required, SplitValues(parts[4]));
            }).ToArray();
        var steps = ParseLines(_experienceSteps!.Text, 7, "étape de quête").Select((parts, index) =>
        {
            if (!int.TryParse(parts[1], out var templateVersion) || templateVersion < 1)
                throw new ArgumentException($"Version de template invalide : {parts[1]}.");
            if (!bool.TryParse(parts[6], out var editable))
                throw new ArgumentException($"Indicateur modifiable invalide : {parts[6]}.");
            var objectiveIds = ParseIndices(parts[4], objectives.Length, "objectif")
                .Select(position => objectives[position].Id).ToArray();
            var transitionIds = ParseIndices(parts[5], transitions.Length, "transition")
                .Select(position => transitions[position].Id).ToArray();
            return new QuestExperienceStep(StableId($"{source.Id}:r072:step:{index}"), 1,
                $"toolset:r072:template:{parts[0]}:v{templateVersion}", index + 1,
                new LocalizedText(parts[2], parts[3], provenance: "toolset:r072"), objectiveIds, transitionIds,
                parts[0], templateVersion, editable);
        }).ToArray();
        var revealConditions = ParseLines(_experienceRevealConditions!.Text, 2, "condition de révélation")
            .ToDictionary(parts =>
            {
                if (!Enum.TryParse<QuestExperienceVisibility>(parts[0], true, out var visibility) ||
                    visibility == QuestExperienceVisibility.Hidden)
                    throw new ArgumentException($"Visibilité de révélation invalide : {parts[0]}.");
                return visibility;
            }, parts => SplitValues(parts[1]));
        var visibilityPolicy = new QuestExperienceVisibilityPolicy(
            StableId($"{source.Id}:r072:visibility"), 1, "toolset:r072", QuestExperienceVisibility.Hidden,
            revealConditions, _experienceManualTracking!.ButtonPressed);
        var at = new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero);
        var history = ParseLines(_experienceHistory!.Text, 7, "événement historique")
            .Select((parts, index) => new QuestExperienceHistoryEvent(
                StableId($"{source.Id}:r072:history:{index}"), 1, "toolset:r072", source.Id,
                parts[0], parts[1], parts[2], parts[3], at.AddMinutes(index), ParsePairs(parts[6]),
                new LocalizedText(parts[4], parts[5], provenance: "toolset:r072"))).ToArray();
        var markers = ParseLines(_experienceMarkers!.Text, 9, "pin de carte").Select((parts, index) =>
        {
            if (!Enum.TryParse<QuestExperienceMarkerBindingKind>(parts[0], true, out var bindingKind))
                throw new ArgumentException($"Binding de pin inconnu : {parts[0]}.");
            var objectiveIndex = ParseIndices(parts[2], objectives.Length, "objectif").Single();
            if (!Enum.TryParse<QuestExperienceVisibility>(parts[3], true, out var markerVisibility))
                throw new ArgumentException($"Visibilité de pin invalide : {parts[3]}.");
            if (!double.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var precision))
                throw new ArgumentException($"Précision de pin invalide : {parts[4]}.");
            if (!bool.TryParse(parts[5], out var tracked))
                throw new ArgumentException($"Suivi de pin invalide : {parts[5]}.");
            return new QuestExperienceMarker(StableId($"{source.Id}:r072:marker:{index}"), 1,
                "toolset:r072", index + 1, objectives[objectiveIndex].Id, bindingKind, parts[1], markerVisibility,
                precision, tracked, new LocalizedText(parts[6], parts[7], provenance: "toolset:r072"),
                ParseVector(parts[8]));
        }).ToArray();
        return new QuestExperienceProfile(StableId($"{source.Id}:r072:experience"), 1, "toolset:r072",
            _experienceEnabled.ButtonPressed, _experienceRequired!.ButtonPressed, resources, steps,
            visibilityPolicy, history, markers);
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
            var authoring = candidate.Authoring is { Enabled: true } profile
                ? $"[b]OBJECTIFS, CAST & WIZARD[/b]\n{profile.Objectives.Count} objectifs · " +
                  $"{profile.Participants.Count} participant(s) · progression {profile.Rewards.Progression:0.##} · " +
                  $"{profile.Notes.Status} · {profile.Wizard.GeneratedTestScenarios.Count} tests · " +
                  $"code opaque {(profile.Wizard.GeneratesOpaqueCode ? "OUI" : "non")}\n\n"
                : "[b]OBJECTIFS, CAST & WIZARD[/b]\nÉtat vide · extension optionnelle absente.\n\n";
            var experience = candidate.Experience is { Enabled: true } experienceProfile
                ? $"[b]RESSOURCES, HISTORIQUE & PINS[/b]\n{experienceProfile.Resources.Count} ressources · " +
                  $"{experienceProfile.Steps.Count} étape(s) · visibilité {experienceProfile.Visibility.Initial} · " +
                  $"{experienceProfile.History.Count} événements · {experienceProfile.Markers.Count} pins\n\n"
                : "[b]RESSOURCES, HISTORIQUE & PINS[/b]\nÉtat vide · extension optionnelle absente.\n\n";
            var quality = candidate.Quality is { Enabled: true } qualityProfile
                ? $"[b]TESTS & GÉNÉRATION IA[/b]\n{qualityProfile.TestScenarios.Count} scénarios · " +
                  $"{qualityProfile.TestScenarios.Sum(item => item.Steps.Count)} nœuds · " +
                  $"{qualityProfile.TestScenarios.Sum(item => item.Edges.Count)} arêtes · " +
                  $"plan {qualityProfile.AiPlan.Authorization} · lint {qualityProfile.AiPlan.Lint.Count}\n\n"
                : "[b]TESTS & GÉNÉRATION IA[/b]\nÉtat vide · extension optionnelle absente.\n\n";
            _authoringSummary!.Text = candidate.Authoring is { } summary
                ? $"{summary.Objectives.Count} objectifs · {summary.Participants.Count} participant(s) · " +
                  $"{summary.Wizard.ZoneIds.Count} zone(s) · {summary.Wizard.PropIds.Count} prop(s) · " +
                  $"{summary.Wizard.DialogueIds.Count} dialogue(s) · {summary.Wizard.GeneratedTestScenarios.Count} tests · " +
                  $"code opaque {(summary.Wizard.GeneratesOpaqueCode ? "OUI" : "non")} · {summary.Notes.Status}"
                : "État vide · ajoutez des objectifs, un cast et un plan Wizard.";
            _experienceSummary!.Text = candidate.Experience is { } experienceSummary
                ? $"{experienceSummary.Resources.Count} ressources · {experienceSummary.Steps.Count} étape(s) · " +
                  $"{experienceSummary.Visibility.RevealConditions.Count} révélations · " +
                  $"{experienceSummary.History.Count} événements · {experienceSummary.Markers.Count} pins · " +
                  $"suivi manuel {(experienceSummary.Visibility.AllowManualTracking ? "oui" : "non")}" :
                "État vide · ajoutez ressources, étapes, visibilité, chronologie et pins.";
            _qualitySummary!.Text = candidate.Quality is { } qualitySummary
                ? $"{qualitySummary.TestScenarios.Count} scénarios · " +
                  $"{qualitySummary.TestScenarios.Sum(item => item.Steps.Count)} nœuds · " +
                  $"{qualitySummary.TestScenarios.Sum(item => item.Edges.Count)} arêtes · " +
                  $"plan {qualitySummary.AiPlan.Id:D} · autorisation {qualitySummary.AiPlan.Authorization} · " +
                  $"lint {qualitySummary.AiPlan.Lint.Count}"
                : "État vide · générez les cinq scénarios et préparez une proposition IA gouvernée.";
            _preview.Text = $"[font_size=21][color=#ff9858]{candidate.Title.Fallback}[/color][/font_size]\n" +
                $"[color=#8ce0bd]{candidate.Category} · priorité {candidate.Priority} · {candidate.Owner}[/color]\n" +
                $"UUID {candidate.Id} · schéma v{candidate.SchemaVersion} · {candidate.Provenance}\n\n" +
                $"[b]PORTÉE[/b]\n{candidate.InstancePolicy.DefaultScope} · autorisées " +
                $"{string.Join(", ", candidate.InstancePolicy.AllowedScopes)} · autorité {candidate.InstancePolicy.RequireAuthority}\n\n" +
                $"[b]MINI-GRAPHE[/b]\n{graph}\n\n{authoring}{experience}{quality}" +
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

    private void CreateNewState()
    {
        try
        {
            var draft = BuildDraft();
            var id = QuestStateId.From(Guid.NewGuid());
            var order = draft.States.Count == 0 ? 1 : draft.States.Max(item => item.DisplayOrder) + 1;
            var state = new QuestStateDefinition(id, QuestStateDefinition.CurrentSchemaVersion, "author:quest-editor",
                $"state-{order}", $"Nouvel état {order}", new QuestJournalEntry(
                    new LocalizedText($"{draft.Slug}.state-{order}.summary", "Nouvelle étape"),
                    new LocalizedText($"{draft.Slug}.state-{order}.details", "Décrivez cette étape."),
                    provenance: "author:quest-editor"), false, order, null, QuestTerminalKind.None, string.Empty,
                QuestReactivationRule.Never);
            _draft = WithGraph(draft, draft.InitialStateId, [.. draft.States, state], draft.Transitions);
            LoadQuest(_draft);
            var index = _draft.States.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value)
                .ToList().FindIndex(item => item.Id == id);
            _states!.Select(index);
            LoadState(state);
            _feedback!.Text = "[color=#ffbd7a]État ajouté au brouillon · appliquez pour enregistrer.[/color]";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _feedback!.Text = $"[color=#ff7777]Impossible d’ajouter l’état[/color]\n{exception.Message}";
        }
    }

    private void RemoveSelectedState()
    {
        if (_draft is null || _selectedStateId is null) return;
        try
        {
            var draft = BuildDraft();
            if (draft.States.Count <= 1)
            {
                _feedback!.Text = "[color=#ff7777]Une quête doit conserver au moins un état.[/color]";
                return;
            }
            var removed = _selectedStateId;
            var states = draft.States.Where(item => item.Id != removed).ToArray();
            var removedTransitions = draft.Transitions.Count(item =>
                item.FromStateId == removed || item.ToStateId == removed);
            var transitions = draft.Transitions.Where(item =>
                item.FromStateId != removed && item.ToStateId != removed).ToArray();
            var initial = draft.InitialStateId == removed
                ? states.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value).First().Id
                : draft.InitialStateId;
            _draft = WithGraph(draft, initial, states, transitions);
            LoadQuest(_draft);
            _feedback!.Text = $"[color=#ffbd7a]État retiré du brouillon · {removedTransitions} transition(s) dépendante(s) retirée(s). Appliquez pour enregistrer.[/color]";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _feedback!.Text = $"[color=#ff7777]Impossible de supprimer l’état[/color]\n{exception.Message}";
        }
    }

    private void RemoveSelectedTransition()
    {
        if (_draft is null || _selectedTransitionId is null) return;
        try
        {
            var draft = BuildDraft();
            var transitions = draft.Transitions.Where(item => item.Id != _selectedTransitionId).ToArray();
            _draft = WithGraph(draft, draft.InitialStateId, draft.States, transitions);
            LoadQuest(_draft);
            _feedback!.Text = "[color=#ffbd7a]Transition retirée du brouillon · appliquez pour enregistrer.[/color]";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _feedback!.Text = $"[color=#ff7777]Impossible de supprimer la transition[/color]\n{exception.Message}";
        }
    }

    private static QuestDefinition WithGraph(QuestDefinition quest, QuestStateId initialStateId,
        IReadOnlyList<QuestStateDefinition> states, IReadOnlyList<QuestTransition> transitions) => new(
        quest.Id, quest.SchemaVersion, quest.Provenance, quest.Slug, quest.DisplayName, quest.Title,
        quest.Description, quest.Category, quest.Priority, quest.Owner, quest.RequiredAtRuntime,
        quest.InstancePolicy, initialStateId, states, transitions, quest.Operations, quest.Simulation,
        quest.Authoring, quest.Experience, quest.Quality);

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
            [.. _draft.Transitions, transition], _draft.Operations, _draft.Simulation, _draft.Authoring,
            _draft.Experience, _draft.Quality);
        LoadTransitionCatalog(_draft);
        _transitions!.Select(_draft.Transitions.Count - 1);
        LoadTransition(transition);
        _feedback!.Text = "[color=#ffbd7a]Transition ajoutée au brouillon · appliquez pour enregistrer.[/color]";
    }

    private void DeleteCurrentQuest()
    {
        if (_draft is null || _load?.Invoke()?.Project.Quests.All(item => item.Id != _draft.Id) != false)
        {
            _feedback!.Text = "[color=#ff7777]Le brouillon n’est pas encore enregistré.[/color]";
            return;
        }
        if (_deleteQuest?.Invoke(_draft.Id) == true)
        {
            Refresh();
            _feedback!.Text = "[color=#8ce0bd]Quête supprimée · utilisez Annuler pour la restaurer.[/color]";
        }
        else _feedback!.Text = "[color=#ff7777]Suppression refusée · consultez les dépendances du projet.[/color]";
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
        if (_r073Capture)
        {
            ConfigureForR073SmokeTest();
            _tabs!.CurrentTab = 5;
            _feedback!.Text = "[color=#8ce0bd]R073 prêt · tests générés et plan IA traçable, linté et approuvé[/color]";
        }
        else if (_r072Capture)
        {
            ConfigureForR072SmokeTest();
            _tabs!.CurrentTab = 4;
            _feedback!.Text = "[color=#8ce0bd]R072 prêt · ressources, étapes, visibilité, chronologie et pins gouvernés[/color]";
        }
        else if (_r071Capture)
        {
            ConfigureForR071SmokeTest();
            _tabs!.CurrentTab = 3;
            _feedback!.Text = "[color=#8ce0bd]R071 prêt · objectifs, récompenses, notes, cast et Wizard transparents[/color]";
        }
        else if (_r008Capture)
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

    private static IReadOnlyList<string> SplitValues(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> TextLines(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, string> ParsePairs(string value)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in SplitValues(value))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0 || separator == pair.Length - 1)
                throw new ArgumentException($"Paire debug invalide : {pair}");
            result.Add(pair[..separator].Trim(), pair[(separator + 1)..].Trim());
        }
        return result;
    }

    private static IReadOnlyList<int> ParseIndices(string value, int count, string label)
    {
        var result = new List<int>();
        foreach (var token in SplitValues(value))
        {
            if (!int.TryParse(token, out var index) || index < 0 || index >= count)
                throw new ArgumentException($"Indice {label} invalide : {token}");
            result.Add(index);
        }
        return result;
    }

    private static Vector3Value? ParseVector(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var z))
            throw new ArgumentException($"Fallback spatial invalide : {value}");
        return new Vector3Value(x, y, z);
    }

    private static int IndexOf<T>(IEnumerable<T> values, T expected) where T : notnull
    {
        var index = 0;
        foreach (var value in values)
        {
            if (EqualityComparer<T>.Default.Equals(value, expected)) return index;
            index++;
        }
        return -1;
    }

    private static IReadOnlyDictionary<string, T> ParseMap<T>(string text, Func<string, T> parse)
    {
        var result = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var line in TextLines(text))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
                throw new ArgumentException($"Entrée de récompense invalide : {line}");
            result.Add(line[..separator].Trim(), parse(line[(separator + 1)..].Trim()));
        }
        return result;
    }

    private static string FormatMap<T>(IReadOnlyDictionary<string, T> values) =>
        string.Join('\n', values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"));

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

    private void BuildStructuredEditors()
    {
        StructuredCollectionField.Attach(_summaryVariants!, "variante",
            new("Langue", "fr", "fr"), new("Texte", "Résumé traduit"));
        StructuredCollectionField.Attach(_objectives!, "objectif de journal",
            new("Clé", "quest.objective"), new("Libellé", "Objectif visible"));
        StructuredCollectionField.Attach(_links!, "lien contextuel",
            new("Type", "Custom", "Custom"), new("Référence", "lore:entry"),
            new("Clé", "quest.link"), new("Libellé", "En savoir plus"));
        StructuredCollectionField.Attach(_transitionConditions!, "condition",
            new("Type", "VariableEquals", "VariableEquals"), new("Clé", "quest.flag"),
            new("Valeur", "true", "true"));
        StructuredCollectionField.Attach(_transitionActions!, "action",
            new("Type", "SetVariable", "SetVariable"), new("Clé", "quest.flag"),
            new("Valeur", "true", "true"));
        StructuredCollectionField.Attach(_simulationVariables!, "variable initiale",
            new("Clé", "quest.flag"), new("Valeur", "true"));
        StructuredCollectionField.Attach(_simulationEvents!, "événement de simulation",
            new("Événement", "dialogue.accept"), new("Acteur", "player"),
            new("Données", "clé=valeur"));
        StructuredCollectionField.Attach(_authoringObjectives!, "objectif auteur",
            new("Type", "Custom", "Custom"), new("Clé", "objective.key"),
            new("Libellé", "Objectif"), new("Cible", "1", "1"),
            new("Références", "référence;référence"), new("Destination", "destination"),
            new("Durée", "0", "0"), new("Choix", "choix;choix"),
            new("Règle", "required", "required"));
        StructuredCollectionField.Attach(_notesReviews!, "revue",
            new("Auteur", "author:karen"),
            new("Statut", Options: ["Draft", "Review", "Approved", "Rejected"], DefaultValue: "Review"),
            new("Message", "Commentaire de revue"));
        StructuredCollectionField.Attach(_authoringParticipants!, "participant",
            new("Rôle", "guide"),
            new("Type", Options: ["Blueprint", "Instance", "Query"], DefaultValue: "Blueprint"),
            new("Binding", "creature/guide"), new("Conditions", "quest:active"),
            new("Requis", Options: ["true", "false"], DefaultValue: "true"));
        StructuredCollectionField.Attach(_experienceResources!, "ressource de quête",
            new("Type", Options: ["Item", "Placeable", "Door", "Zone", "Asset"], DefaultValue: "Item"),
            new("Référence", "item:key"), new("Propriétaire", "player"),
            new("Requise", Options: ["true", "false"], DefaultValue: "true"),
            new("Conditions", "condition;condition"));
        StructuredCollectionField.Attach(_experienceSteps!, "étape",
            new("Template", "step.default"), new("Version", "1", "1"),
            new("Clé de libellé", "quest.step"), new("Libellé", "Étape"),
            new("Objectifs", "0;1"), new("Transitions", "0;1"),
            new("Modifiable", Options: ["true", "false"], DefaultValue: "true"));
        StructuredCollectionField.Attach(_experienceRevealConditions!, "règle de révélation",
            new("État révélé", Options: ["Rumor", "Discovered", "Tracked", "SecretCompleted"],
                DefaultValue: "Discovered"),
            new("Conditions", "condition;condition"));
        StructuredCollectionField.Attach(_experienceHistory!, "entrée de chronologie",
            new("Depuis", "Hidden"), new("Vers", "Discovered"), new("Cause", "event"),
            new("Acteur", "player"), new("Clé du résumé", "quest.history"),
            new("Résumé joueur", "La quête progresse."), new("Détails", "clé=valeur"));
        StructuredCollectionField.Attach(_experienceMarkers!, "repère",
            new("Type", Options: ["Zone", "Entity", "Query", "Waypoint"], DefaultValue: "Zone"),
            new("Binding", "zone:crypt"), new("Objectif", "0", "0"),
            new("Visibilité", "Tracked", "Tracked"), new("Précision", "Exact", "Exact"),
            new("Suivi", Options: ["true", "false"], DefaultValue: "true"),
            new("Clé", "quest.marker"), new("Libellé", "Destination"),
            new("Position de repli", "0,0,0"));
        StructuredCollectionField.Attach(_qualityScenarioSeeds!, "scénario de test",
            new("Scénario", Options: ["Nominal", "Failure", "Repetition", "Multiplayer", "SaveResume"],
                DefaultValue: "Nominal"), new("Seed", "2026", "2026"));
        StructuredCollectionField.Attach(_qualityDependencies!, "dépendance",
            new("Type", Options: ["State", "Transition", "Participant", "Zone", "Prop", "Asset"],
                DefaultValue: "State"),
            new("Référence", "quest:state"));
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
