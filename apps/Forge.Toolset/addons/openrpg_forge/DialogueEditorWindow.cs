#if TOOLS
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Godot;

namespace Forge.Toolset.Editor;

public partial class DialogueChoiceList : ItemList
{
    public const string PayloadKind = "openrpg_forge.dialogue-choice";
    public event Action<int, int>? ChoiceMoved;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var index = GetItemAtPosition(atPosition, true);
        if (index < 0) return default;
        Select(index);
        var preview = new Label { Text = $"  Déplacer {GetItemText(index)}  " };
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { ["kind"] = PayloadKind, ["sourceIndex"] = index };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        TryRead(data, out _) && GetItemAtPosition(atPosition, true) >= 0;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryRead(data, out var source)) return;
        var target = GetItemAtPosition(atPosition, true);
        if (target >= 0 && source != target) ChoiceMoved?.Invoke(source, target);
    }

    private static bool TryRead(Variant data, out int source)
    {
        source = -1;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") || !dictionary.ContainsKey("sourceIndex") ||
            !string.Equals(dictionary["kind"].AsString(), PayloadKind, StringComparison.Ordinal)) return false;
        source = dictionary["sourceIndex"].AsInt32();
        return source >= 0;
    }
}

public partial class DialogueEditorWindow : Window
{
    private const int NodePageSize = 25;
    private readonly List<DialogueId> _dialogueIds = [];
    private readonly List<BlueprintId?> _speakerIds = [];
    private readonly List<DialogueNode> _draftNodes = [];

    private Func<ProjectState?>? _getState;
    private Func<DialogueDefinition, bool>? _applyDialogue;
    private Func<DialogueId, bool>? _deleteDialogue;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _brandWindow;
    private Action<DialogueId>? _openPresentation;
    private DialogueId? _draftDialogueId;
    private DialoguePresentationProfile? _draftPresentation;
    private int _draftSchemaVersion = DialogueDefinition.CurrentSchemaVersion;
    private string _draftProvenance = "author:dialogue-editor";
    private IReadOnlyDictionary<string, string> _draftMetadata = new Dictionary<string, string>();
    private IReadOnlyList<DialogueParticipant> _draftParticipants = [];
    private IReadOnlyList<DialogueEntryPoint> _draftEntryPoints = [];
    private DialogueAuthoringProfile? _draftAuthoring;
    private DialogueExperienceProfile? _draftExperience;
    private DialogueProductionProfile? _draftProduction;
    private DialogueNodeId? _entryNodeId;
    private int _selectedNodeIndex = -1;
    private int _selectedChoiceIndex = -1;
    private int _nodePage;

    private OptionButton? _dialoguePicker;
    private LineEdit? _dialogueName;
    private LineEdit? _dialogueSlug;
    private ItemList? _nodeList;
    private Label? _nodePageLabel;
    private OptionButton? _speakerPicker;
    private OptionButton? _nodeRole;
    private LineEdit? _subgraphKey;
    private LineEdit? _nodeTextKey;
    private TextEdit? _nodeText;
    private TextEdit? _nodeVariants;
    private TextEdit? _nodeActions;
    private DialogueChoiceList? _choiceList;
    private LineEdit? _choiceTextKey;
    private LineEdit? _choiceText;
    private TextEdit? _choiceVariants;
    private OptionButton? _choiceTarget;
    private OptionButton? _choiceEdgeKind;
    private SpinBox? _choicePriority;
    private OptionButton? _conditionCombination;
    private TextEdit? _conditions;
    private TextEdit? _choiceActionLines;
    private ScrollContainer? _detailScroll;
    private CheckBox? _authoringEnabled;
    private CheckBox? _authoringRequired;
    private TextEdit? _authoringParticipants;
    private TextEdit? _authoringTokens;
    private OptionButton? _authoringEvaluation;
    private TextEdit? _authoringWeights;
    private OptionButton? _wizardConditionKind;
    private LineEdit? _wizardConditionKey;
    private LineEdit? _wizardConditionValue;
    private OptionButton? _wizardActionKind;
    private LineEdit? _wizardActionKey;
    private LineEdit? _wizardActionValue;
    private Label? _wizardSources;
    private LineEdit? _notesIntent;
    private LineEdit? _notesEmotion;
    private LineEdit? _notesSource;
    private OptionButton? _notesStatus;
    private LineEdit? _notesTodos;
    private LineEdit? _notesAuthor;
    private CheckBox? _experienceEnabled;
    private CheckBox? _experienceRequired;
    private CheckBox? _experienceRollback;
    private CheckBox? _experienceResume;
    private TextEdit? _experienceCompletedActions;
    private TextEdit? _experienceAbortedActions;
    private LineEdit? _experienceSearchPattern;
    private CheckBox? _experienceSearchRegex;
    private LineEdit? _experienceReplacement;
    private LineEdit? _experienceSearchFilters;
    private readonly Dictionary<DialogueReviewBadge, CheckBox> _experienceBadgeChecks = [];
    private CheckBox? _experienceReviewMode;
    private TextEdit? _experienceMemorySlots;
    private TextEdit? _experienceSkillChecks;
    private Label? _experienceSearchPreview;
    private CheckBox? _productionEnabled;
    private CheckBox? _productionRequired;
    private TextEdit? _productionSubgraphs;
    private TextEdit? _productionConsequences;
    private TextEdit? _productionCoverage;
    private TextEdit? _productionCollaboration;
    private TextEdit? _productionAiPlans;
    private Label? _productionSummary;
    private RichTextLabel? _preview;
    private RichTextLabel? _graphOverview;
    private TextEdit? _simulationState;
    private RichTextLabel? _simulationLog;
    private RichTextLabel? _validation;
    private Label? _status;
    private Button? _removeChoiceButton;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;
    private bool _captureR068;
    private bool _captureR069;
    private bool _captureR070;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<DialogueDefinition, bool> applyDialogue,
        Func<DialogueId, bool> deleteDialogue,
        Action<DialogueId> openPresentation,
        Action undo,
        Action redo,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(applyDialogue);
        ArgumentNullException.ThrowIfNull(deleteDialogue);
        ArgumentNullException.ThrowIfNull(brandWindow);
        ArgumentNullException.ThrowIfNull(openPresentation);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        _getState = getState;
        _applyDialogue = applyDialogue;
        _deleteDialogue = deleteDialogue;
        _openPresentation = openPresentation;
        _undo = undo;
        _redo = redo;
        _brandWindow = brandWindow;
        Title = "Dialogues · OpenRPG Forge";
        MinSize = new Vector2I(1440, 950);
        Size = new Vector2I(1440, 950);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--dialogue-graph-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r068-dialogue-authoring-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r069-dialogue-experience-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r070-dialogue-production-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _captureR068 = captureArgument.StartsWith("--r068-dialogue-authoring-ui-capture=",
                StringComparison.Ordinal);
            _captureR069 = captureArgument.StartsWith("--r069-dialogue-experience-ui-capture=",
                StringComparison.Ordinal);
            _captureR070 = captureArgument.StartsWith("--r070-dialogue-production-ui-capture=",
                StringComparison.Ordinal);
            _capturePath = _captureR070
                ? captureArgument["--r070-dialogue-production-ui-capture=".Length..]
                : _captureR069
                ? captureArgument["--r069-dialogue-experience-ui-capture=".Length..]
                : _captureR068
                    ? captureArgument["--r068-dialogue-authoring-ui-capture=".Length..]
                    : captureArgument["--dialogue-graph-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1.0)
        {
            Open();
            PrepareCaptureDialogue();
            if (_captureR068) PrepareR068Capture();
            if (_captureR069) PrepareR069Capture();
            if (_captureR070) PrepareR070Capture();
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.5) return;
        if (string.IsNullOrWhiteSpace(_capturePath))
        {
            GD.PrintErr($"OPENRPG_FORGE_{CaptureMarker()}_UI_CAPTURE_FAILED empty_path");
            _capturePath = null;
            SetProcess(false);
            GetTree().Quit(1);
            return;
        }
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_{CaptureMarker()}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open(bool createNew = false)
    {
        var state = _getState?.Invoke();
        if (state is null)
        {
            return;
        }

        PopulateBlueprints(state);
        PopulateDialogues(state);
        if (createNew || _dialogueIds.Count == 0)
        {
            CreateNewDialogue();
        }
        else
        {
            _dialoguePicker!.Selected = 0;
            LoadDialogue(state.Project.Dialogues.Single(dialogue => dialogue.Id == _dialogueIds[0]));
        }

        Exclusive = true;
        PopupCentered(new Vector2I(1440, 950));
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _getState?.Invoke();
        if (state is null) return;
        var previous = _draftDialogueId;
        PopulateBlueprints(state);
        PopulateDialogues(state);
        if (_dialogueIds.Count == 0)
        {
            CreateNewDialogue();
            return;
        }

        var selected = previous is null ? -1 : _dialogueIds.FindIndex(item => item == previous);
        if (selected < 0) selected = 0;
        _dialoguePicker!.Selected = selected;
        LoadDialogue(state.Project.Dialogues.Single(dialogue => dialogue.Id == _dialogueIds[selected]));
    }

    public bool ConfigureForR068SmokeTest(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var state = _getState?.Invoke();
        if (state is null) return false;
        PopulateBlueprints(state);
        LoadDialogue(dialogue);

        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, "toolset:r068");
        var kinds = new[]
        {
            DialogueParticipantBindingKind.Role,
            DialogueParticipantBindingKind.Query,
            DialogueParticipantBindingKind.Tag,
            DialogueParticipantBindingKind.Entity,
        };
        var roles = profile.Participants.Select(item => item.Role).ToArray();
        var narrator = roles.FirstOrDefault(item => string.Equals(item, "narrator", StringComparison.Ordinal)) ??
                       roles[0];
        var participants = profile.Participants.Select((item, index) => item with
        {
            Provenance = "toolset:r068",
            Kind = kinds[index % kinds.Length],
            Value = kinds[index % kinds.Length] switch
            {
                DialogueParticipantBindingKind.Query => $"role={item.Role}",
                DialogueParticipantBindingKind.Tag => $"dialogue-{item.Role}",
                DialogueParticipantBindingKind.Entity => $"entity:{item.Role}",
                _ => item.Role,
            },
            FallbackRole = item.Role == narrator ? null : narrator,
        }).ToArray();
        var modes = new[]
        {
            DialogueChoiceEvaluationMode.Weighted,
            DialogueChoiceEvaluationMode.FirstValid,
            DialogueChoiceEvaluationMode.AllValid,
            DialogueChoiceEvaluationMode.RandomDeterministic,
            DialogueChoiceEvaluationMode.Manual,
        };
        var entryNode = dialogue.Nodes.Single(item => item.Id == dialogue.EntryNodeId);
        var wizard = new List<DialogueWizardEntry>();
        if (entryNode.OnEnterActions.FirstOrDefault() is { } action)
            wizard.Add(new DialogueWizardEntry(Guid.Parse("d0685000-0000-4000-8000-000000000101"), 1,
                "toolset:r068", DialogueWizardEntryKind.Action, action.Id,
                $"builder:action:{action.Kind}:{action.Key}:{action.Value}:{action.Required.ToString().ToLowerInvariant()}"));
        if (entryNode.Choices.SelectMany(item => item.Conditions).FirstOrDefault() is { } condition)
            wizard.Add(new DialogueWizardEntry(Guid.Parse("d0685000-0000-4000-8000-000000000102"), 1,
                "toolset:r068", DialogueWizardEntryKind.Condition, condition.Id,
                $"builder:condition:{condition.Kind}:{condition.Key}:{condition.ExpectedValue}:{condition.Negated.ToString().ToLowerInvariant()}"));
        profile = profile with
        {
            Provenance = "toolset:r068",
            RequiredAtRuntime = true,
            Participants = participants,
            Nodes = profile.Nodes.Select((item, index) => item with
            {
                Provenance = "toolset:r068",
                EvaluationMode = modes[index % modes.Length],
                Tokens = item.NodeId == dialogue.EntryNodeId
                    ? [new DialogueTextToken(Guid.Parse("d0685000-0000-4000-8000-000000000103"), 1,
                        "toolset:r068", "player.name", DialogueTextTokenType.EntityName, true, "Hero")]
                    : [],
                ChoiceEvaluations = item.ChoiceEvaluations.Select((value, choiceIndex) => value with
                {
                    Provenance = "toolset:r068",
                    Weight = choiceIndex + 1,
                }).ToArray(),
                WizardEntries = item.NodeId == dialogue.EntryNodeId ? wizard : [],
                Notes = item.Notes with
                {
                    Provenance = "toolset:r068",
                    Intent = $"Valider le noeud {index + 1} dans le parcours auteur.",
                    Emotion = index == 0 ? "mysterious" : "neutral",
                    Source = "acceptance:r068",
                    Status = DialogueNodeEditorialStatus.Review,
                    Todos = ["Relire le ton"],
                    AuthorMention = "@karen",
                },
            }).ToArray(),
        };
        _draftAuthoring = profile;
        RefreshNodes(0);
        RefreshValidation();
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        var configured = _authoringEnabled?.ButtonPressed == true &&
                         _authoringRequired?.ButtonPressed == true &&
                         _authoringEvaluation?.ItemCount == modes.Length && diagnostics.Count == 0;
        if (!configured)
            GD.PrintErr("OPENRPG_FORGE_DIALOGUE_R068_UI_CONFIGURATION_FAILED " +
                        $"enabled={_authoringEnabled?.ButtonPressed} required={_authoringRequired?.ButtonPressed} " +
                        $"modes={_authoringEvaluation?.ItemCount} diagnostics={string.Join(',', diagnostics.Select(item => item.Code))}");
        return configured;
    }

    public bool ApplyForR068SmokeTest()
    {
        if (!SaveSelectedNode()) return false;
        try
        {
            return _applyDialogue?.Invoke(CreateDraftDefinition()) == true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
            return false;
        }
    }

    public bool ConfigureForR069SmokeTest(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var state = _getState?.Invoke();
        if (state is null) return false;
        PopulateBlueprints(state);
        LoadDialogue(dialogue);
        var success = dialogue.Nodes.First(item => item.Id != dialogue.EntryNodeId).Id;
        var failure = dialogue.Nodes.Last(item => item.Id != dialogue.EntryNodeId).Id;
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "toolset:r069");
        profile = profile with
        {
            RequiredAtRuntime = true,
            Lifecycle = profile.Lifecycle with
            {
                Provenance = "toolset:r069",
                CompletedActions = [new(DialogueActionKind.SetVariable, "dialogue.completed", "true")],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
                CreateResumeState = true,
            },
            Search = profile.Search with
            {
                Provenance = "toolset:r069",
                Pattern = "crypte|braise",
                UseRegex = true,
                SpeakerRoles = [],
                ReplacementPreview = "flamme",
            },
            Review = profile.Review with { Provenance = "toolset:r069", ReviewMode = true },
            MemorySlots =
            [new(Guid.Parse("d0695000-0000-4000-8000-000000000001"), 1, "toolset:r069",
                DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                DialogueMemoryDuration.Campaign, "0")],
            SkillChecks =
            [new(Guid.Parse("d0695000-0000-4000-8000-000000000002"), 1, "toolset:r069",
                dialogue.EntryNodeId, "persuasion", 12, "elyra", true,
                DialogueSkillCheckVisibility.FullExplanation, success, failure)],
        };
        _draftExperience = profile;
        RefreshNodes(0);
        RefreshValidation();
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        return _experienceEnabled?.ButtonPressed == true &&
               _experienceRequired?.ButtonPressed == true &&
               _experienceBadgeChecks.Count == 6 && diagnostics.Count == 0;
    }

    public bool ApplyForR069SmokeTest()
    {
        if (!SaveSelectedNode()) return false;
        try
        {
            return _applyDialogue?.Invoke(CreateDraftDefinition()) == true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
            return false;
        }
    }

    public bool ConfigureForR070SmokeTest(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var state = _getState?.Invoke();
        if (state is null) return false;
        PopulateBlueprints(state);
        LoadDialogue(dialogue);
        _draftProduction = CreateR070ProductionProfile(dialogue, "toolset:r070");
        RefreshNodes(0);
        RefreshValidation();
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        return _productionEnabled?.ButtonPressed == true && _productionRequired?.ButtonPressed == true &&
               _productionSummary?.Text.Contains("Couverture", StringComparison.Ordinal) == true &&
               diagnostics.Count == 0;
    }

    public bool ApplyForR070SmokeTest()
    {
        if (!SaveSelectedNode()) return false;
        try { return _applyDialogue?.Invoke(CreateDraftDefinition()) == true; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
            return false;
        }
    }

    private void BuildUi()
    {
        var background = new PanelContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("15111f"),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        background.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        var title = new Label
        {
            Text = "DIALOGUES",
            ThemeTypeVariation = "HeaderSmall",
        };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        content.AddChild(title);
        content.AddChild(new Label
        {
            Text = "Construisez les répliques, les choix et leurs conditions. Le graphe est validé avant application.",
        });

        var dialogueBar = new HBoxContainer();
        dialogueBar.AddThemeConstantOverride("separation", 8);
        content.AddChild(dialogueBar);
        dialogueBar.AddChild(new Label { Text = "Dialogue" });
        _dialoguePicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(240, 38),
        };
        dialogueBar.AddChild(_dialoguePicker);
        var newDialogueButton = new Button
        {
            CustomMinimumSize = new Vector2(150, 38),
            Text = "+ Nouveau",
        };
        dialogueBar.AddChild(newDialogueButton);
        newDialogueButton.Pressed += CreateNewDialogue;
        var deleteDialogueButton = new Button { Text = "Supprimer", TooltipText = "Supprimer le dialogue du projet" };
        deleteDialogueButton.Pressed += DeleteCurrentDialogue;
        dialogueBar.AddChild(deleteDialogueButton);
        var presentationButton = new Button { CustomMinimumSize = new Vector2(170, 38), Text = "Mise en scène…" };
        dialogueBar.AddChild(presentationButton);
        presentationButton.Pressed += OpenPresentation;
        _dialogueName = new LineEdit
        {
            CustomMinimumSize = new Vector2(220, 38),
            PlaceholderText = "Nom du dialogue",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        dialogueBar.AddChild(_dialogueName);
        _dialogueSlug = new LineEdit
        {
            CustomMinimumSize = new Vector2(190, 38),
            PlaceholderText = "slug-du-dialogue",
        };
        dialogueBar.AddChild(_dialogueSlug);

        var editor = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 14);
        content.AddChild(editor);

        var nodeColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(245, 0),
        };
        nodeColumn.AddThemeConstantOverride("separation", 6);
        editor.AddChild(nodeColumn);
        nodeColumn.AddChild(new Label { Text = "NŒUDS DU GRAPHE" });
        _nodeList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        nodeColumn.AddChild(_nodeList);
        var pageRow = new HBoxContainer();
        pageRow.AddThemeConstantOverride("separation", 6);
        nodeColumn.AddChild(pageRow);
        var previousPage = new Button { Text = "‹", TooltipText = "Page précédente" };
        previousPage.Pressed += () => ChangeNodePage(-1);
        pageRow.AddChild(previousPage);
        _nodePageLabel = new Label
        {
            Text = "Page 1 / 1",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        pageRow.AddChild(_nodePageLabel);
        var nextPage = new Button { Text = "›", TooltipText = "Page suivante" };
        nextPage.Pressed += () => ChangeNodePage(1);
        pageRow.AddChild(nextPage);
        var addNodeButton = new Button
        {
            CustomMinimumSize = new Vector2(0, 38),
            Text = "+ Ajouter un nœud",
        };
        nodeColumn.AddChild(addNodeButton);
        addNodeButton.Pressed += AddNode;
        var nodeActions = new HBoxContainer();
        nodeActions.AddThemeConstantOverride("separation", 6);
        nodeColumn.AddChild(nodeActions);
        var setEntryButton = new Button
        {
            Text = "Définir comme entrée",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        setEntryButton.Pressed += SetSelectedNodeAsEntry;
        nodeActions.AddChild(setEntryButton);
        var removeNodeButton = new Button { Text = "Supprimer", TooltipText = "Retire le nœud du brouillon" };
        removeNodeButton.Pressed += RemoveSelectedNode;
        nodeActions.AddChild(removeNodeButton);

        _detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(620, 0),
        };
        editor.AddChild(_detailScroll);
        var detailColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        detailColumn.AddThemeConstantOverride("separation", 6);
        _detailScroll.AddChild(detailColumn);
        detailColumn.AddChild(new Label { Text = "RÉPLIQUE SÉLECTIONNÉE" });
        var nodeMetadata = new HBoxContainer();
        nodeMetadata.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(nodeMetadata);
        _speakerPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(190, 36),
        };
        nodeMetadata.AddChild(_speakerPicker);
        _nodeRole = new OptionButton { CustomMinimumSize = new Vector2(155, 36), TooltipText = "Rôle explicite du nœud" };
        foreach (var role in Enum.GetValues<DialogueNodeRole>().Where(role => role != DialogueNodeRole.Unspecified))
            _nodeRole.AddItem(role.ToString(), (int)role);
        nodeMetadata.AddChild(_nodeRole);
        _subgraphKey = new LineEdit { CustomMinimumSize = new Vector2(150, 36), PlaceholderText = "Sous-graphe (optionnel)" };
        nodeMetadata.AddChild(_subgraphKey);
        _nodeTextKey = new LineEdit
        {
            PlaceholderText = "localisation.exemple.replique",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        nodeMetadata.AddChild(_nodeTextKey);
        _nodeText = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 62),
            PlaceholderText = "Texte de la réplique",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        detailColumn.AddChild(_nodeText);
        detailColumn.AddChild(new Label { Text = "VARIANTES LOCALISÉES" });
        _nodeVariants = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "fr|any|Other|Texte français\nen|any|One|English singular",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_nodeVariants);
        StructuredCollectionField.Attach(_nodeVariants, "variante",
            new("Langue", "fr", "fr"),
            new("Genre grammatical", "any", "any"),
            new("Pluriel", Options: ["Zero", "One", "Two", "Few", "Many", "Other"], DefaultValue: "Other"),
            new("Texte", "Texte de la variante"));
        detailColumn.AddChild(new Label { Text = "ACTIONS À L’ENTRÉE" });
        _nodeActions = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "PlayAudio|elyra.greeting|play|required",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_nodeActions);
        StructuredCollectionField.Attach(_nodeActions, "action",
            new("Type", "PlayAudio", "PlayAudio"),
            new("Cible ou clé", "elyra.greeting"),
            new("Valeur", "play"),
            new("Obligation", Options: ["required", "optional"], DefaultValue: "required"));
        var saveNodeButton = new Button { Text = "Enregistrer la réplique" };
        detailColumn.AddChild(saveNodeButton);
        saveNodeButton.Pressed += SaveSelectedNodeFromUi;

        detailColumn.AddChild(new Label { Text = "CHOIX DU JOUEUR" });
        _choiceList = new DialogueChoiceList
        {
            CustomMinimumSize = new Vector2(0, 96),
            TooltipText = "Glissez-déposez les réponses pour changer leur ordre stable.",
        };
        detailColumn.AddChild(_choiceList);
        var choiceTextRow = new HBoxContainer();
        choiceTextRow.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(choiceTextRow);
        _choiceTextKey = new LineEdit
        {
            PlaceholderText = "localisation.exemple.choix",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        choiceTextRow.AddChild(_choiceTextKey);
        _choiceText = new LineEdit
        {
            PlaceholderText = "Texte du choix",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        choiceTextRow.AddChild(_choiceText);
        _choiceVariants = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 48),
            PlaceholderText = "Variantes du choix · locale|genre|pluriel|texte",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_choiceVariants);
        StructuredCollectionField.Attach(_choiceVariants, "variante",
            new("Langue", "fr", "fr"),
            new("Genre grammatical", "any", "any"),
            new("Pluriel", Options: ["Zero", "One", "Two", "Few", "Many", "Other"], DefaultValue: "Other"),
            new("Texte", "Texte de la variante"));
        var choiceRouteRow = new HBoxContainer();
        choiceRouteRow.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(choiceRouteRow);
        _choiceTarget = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, 36),
        };
        choiceRouteRow.AddChild(_choiceTarget);
        _choiceEdgeKind = new OptionButton { CustomMinimumSize = new Vector2(120, 36) };
        _choiceEdgeKind.AddItem("Flux", (int)DialogueEdgeKind.Flow);
        _choiceEdgeKind.AddItem("Référence", (int)DialogueEdgeKind.Reference);
        choiceRouteRow.AddChild(_choiceEdgeKind);
        _choicePriority = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100000,
            Step = 1,
            CustomMinimumSize = new Vector2(95, 36),
            TooltipText = "Priorité stable"
        };
        choiceRouteRow.AddChild(_choicePriority);
        _conditionCombination = new OptionButton
        {
            CustomMinimumSize = new Vector2(105, 36),
            TooltipText = "ALL exige toutes les lignes ; ANY en exige au moins une.",
        };
        _conditionCombination.AddItem("ALL", (int)DialogueConditionCombination.All);
        _conditionCombination.AddItem("ANY", (int)DialogueConditionCombination.Any);
        choiceRouteRow.AddChild(_conditionCombination);
        _conditions = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "IS|HasItem|ash-key|true\nNOT|QuestStateEquals|crypt-ashes|completed",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_conditions);
        StructuredCollectionField.Attach(_conditions, "condition",
            new("Opérateur", Options: ["IS", "NOT"], DefaultValue: "IS"),
            new("Type", "HasItem", "HasItem"),
            new("Cible ou clé", "ash-key"),
            new("Valeur attendue", "true", "true"));
        _choiceActionLines = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "StartQuest|crypt-ashes|active|required",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_choiceActionLines);
        StructuredCollectionField.Attach(_choiceActionLines, "action",
            new("Type", "StartQuest", "StartQuest"),
            new("Cible ou clé", "crypt-ashes"),
            new("Valeur", "active"),
            new("Obligation", Options: ["required", "optional"], DefaultValue: "required"));
        var choiceButtons = new HBoxContainer();
        choiceButtons.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(choiceButtons);
        var saveChoiceButton = new Button
        {
            Text = "Ajouter ou remplacer le choix",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        choiceButtons.AddChild(saveChoiceButton);
        saveChoiceButton.Pressed += SaveChoice;
        _removeChoiceButton = new Button
        {
            Disabled = true,
            Text = "Retirer",
        };
        choiceButtons.AddChild(_removeChoiceButton);
        _removeChoiceButton.Pressed += RemoveChoice;
        var moveUp = new Button { Text = "↑", TooltipText = "Monter la réponse" };
        moveUp.Pressed += () => MoveChoice(_selectedChoiceIndex, _selectedChoiceIndex - 1);
        choiceButtons.AddChild(moveUp);
        var moveDown = new Button { Text = "↓", TooltipText = "Descendre la réponse" };
        moveDown.Pressed += () => MoveChoice(_selectedChoiceIndex, _selectedChoiceIndex + 1);
        choiceButtons.AddChild(moveDown);

        var authoringPanel = new PanelContainer();
        authoringPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1b1427"),
            BorderColor = new Color("6e4d91"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
        });
        detailColumn.AddChild(authoringPanel);
        var authoring = new VBoxContainer();
        authoring.AddThemeConstantOverride("separation", 6);
        authoringPanel.AddChild(authoring);
        var authoringHeader = new HBoxContainer();
        authoring.AddChild(authoringHeader);
        var authoringTitle = new Label
        {
            Text = "AUTEUR P1 · TOKENS · PARTICIPANTS · ÉVALUATION · WIZARD · NOTES",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        authoringTitle.AddThemeColorOverride("font_color", new Color("ffad72"));
        authoringHeader.AddChild(authoringTitle);
        _authoringEnabled = new CheckBox { Text = "Actif" };
        authoringHeader.AddChild(_authoringEnabled);
        _authoringRequired = new CheckBox { Text = "Requis runtime" };
        authoringHeader.AddChild(_authoringRequired);

        var authoringColumns = new HBoxContainer();
        authoringColumns.AddThemeConstantOverride("separation", 12);
        authoring.AddChild(authoringColumns);
        var contracts = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        contracts.AddThemeConstantOverride("separation", 5);
        authoringColumns.AddChild(contracts);
        var assistant = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        assistant.AddThemeConstantOverride("separation", 5);
        authoringColumns.AddChild(assistant);

        contracts.AddChild(new Label { Text = "PARTICIPANTS" });
        _authoringParticipants = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 52),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        contracts.AddChild(_authoringParticipants);
        var evaluationRow = new HBoxContainer();
        evaluationRow.AddChild(new Label { Text = "Mode d’évaluation", CustomMinimumSize = new Vector2(145, 0) });
        _authoringEvaluation = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var mode in Enum.GetValues<DialogueChoiceEvaluationMode>())
            _authoringEvaluation.AddItem(mode.ToString(), (int)mode);
        evaluationRow.AddChild(_authoringEvaluation);
        contracts.AddChild(evaluationRow);
        contracts.AddChild(new Label { Text = "VARIABLES DE TEXTE" });
        _authoringTokens = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 52),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        contracts.AddChild(_authoringTokens);
        contracts.AddChild(new Label { Text = "POIDS DES CHOIX" });
        _authoringWeights = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 44),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        contracts.AddChild(_authoringWeights);

        assistant.AddChild(new Label { Text = "WIZARD VISUEL · sources typées" });
        var conditionBuilder = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _wizardConditionKind = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var kind in Enum.GetValues<DialogueConditionKind>())
            _wizardConditionKind.AddItem(kind.ToString(), (int)kind);
        conditionBuilder.AddChild(_wizardConditionKind);
        _wizardConditionKey = new LineEdit { PlaceholderText = "clé condition", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        conditionBuilder.AddChild(_wizardConditionKey);
        _wizardConditionValue = new LineEdit { PlaceholderText = "valeur", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        conditionBuilder.AddChild(_wizardConditionValue);
        var addCondition = new Button { Text = "+ Condition typée" };
        addCondition.Pressed += AddWizardCondition;
        conditionBuilder.AddChild(addCondition);
        assistant.AddChild(conditionBuilder);

        var actionBuilder = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _wizardActionKind = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var kind in Enum.GetValues<DialogueActionKind>())
            _wizardActionKind.AddItem(kind.ToString(), (int)kind);
        actionBuilder.AddChild(_wizardActionKind);
        _wizardActionKey = new LineEdit { PlaceholderText = "cible action", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionBuilder.AddChild(_wizardActionKey);
        _wizardActionValue = new LineEdit { PlaceholderText = "valeur", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionBuilder.AddChild(_wizardActionValue);
        var addAction = new Button { Text = "+ Action typée" };
        addAction.Pressed += AddWizardAction;
        actionBuilder.AddChild(addAction);
        assistant.AddChild(actionBuilder);
        _wizardSources = new Label { Text = "Sources visuelles · aucune", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _wizardSources.AddThemeColorOverride("font_color", new Color("ab9acb"));
        assistant.AddChild(_wizardSources);

        var notes = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        notes.AddThemeConstantOverride("h_separation", 8);
        notes.AddThemeConstantOverride("v_separation", 5);
        assistant.AddChild(notes);
        _notesIntent = AddAuthoringField(notes, "Intention", "But narratif du nœud");
        _notesEmotion = AddAuthoringField(notes, "Émotion", "warm, tense…");
        _notesSource = AddAuthoringField(notes, "Source", "brief:chapitre-1");
        _notesAuthor = AddAuthoringField(notes, "Auteur", "@auteur");
        _notesTodos = AddAuthoringField(notes, "TODO", "voix; validation lore");
        notes.AddChild(new Label { Text = "Statut" });
        _notesStatus = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var status in Enum.GetValues<DialogueNodeEditorialStatus>())
            _notesStatus.AddItem(status.ToString(), (int)status);
        notes.AddChild(_notesStatus);

        var experiencePanel = new PanelContainer();
        experiencePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151e2b"),
            BorderColor = new Color("3c7391"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
        });
        detailColumn.AddChild(experiencePanel);
        var experience = new VBoxContainer();
        experience.AddThemeConstantOverride("separation", 6);
        experiencePanel.AddChild(experience);
        var experienceHeader = new HBoxContainer();
        experience.AddChild(experienceHeader);
        var experienceTitle = new Label
        {
            Text = "EXPÉRIENCE DE DIALOGUE P1",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        experienceTitle.AddThemeColorOverride("font_color", new Color("76cce6"));
        experienceHeader.AddChild(experienceTitle);
        _experienceEnabled = new CheckBox { Text = "Actif" };
        experienceHeader.AddChild(_experienceEnabled);
        _experienceRequired = new CheckBox { Text = "Requis runtime" };
        experienceHeader.AddChild(_experienceRequired);
        var experienceSubtitle = new Label
        {
            Text = "Fin/abandon · recherche & remplacement preview · revue · mémoire · checks",
        };
        experienceSubtitle.AddThemeColorOverride("font_color", new Color("9db8ca"));
        experience.AddChild(experienceSubtitle);

        var experienceColumns = new HBoxContainer();
        experienceColumns.AddThemeConstantOverride("separation", 12);
        experience.AddChild(experienceColumns);
        var lifecycleColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        lifecycleColumn.AddThemeConstantOverride("separation", 4);
        experienceColumns.AddChild(lifecycleColumn);
        var intelligenceColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        intelligenceColumn.AddThemeConstantOverride("separation", 4);
        experienceColumns.AddChild(intelligenceColumn);

        var lifecycleFlags = new HBoxContainer();
        _experienceRollback = new CheckBox { Text = "Rollback abandon" };
        lifecycleFlags.AddChild(_experienceRollback);
        _experienceResume = new CheckBox { Text = "État de reprise" };
        lifecycleFlags.AddChild(_experienceResume);
        lifecycleColumn.AddChild(lifecycleFlags);
        lifecycleColumn.AddChild(new Label { Text = "ACTIONS DE FIN" });
        _experienceCompletedActions = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 44),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        lifecycleColumn.AddChild(_experienceCompletedActions);
        lifecycleColumn.AddChild(new Label { Text = "Actions abandon · raison disponible au runtime" });
        _experienceAbortedActions = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 44),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        lifecycleColumn.AddChild(_experienceAbortedActions);
        lifecycleColumn.AddChild(new Label { Text = "MÉMOIRE DU DIALOGUE" });
        _experienceMemorySlots = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 56),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        lifecycleColumn.AddChild(_experienceMemorySlots);

        var searchRow = new HBoxContainer();
        searchRow.AddChild(new Label { Text = "Recherche", CustomMinimumSize = new Vector2(92, 0) });
        _experienceSearchPattern = new LineEdit
        {
            PlaceholderText = "Recherche multi-dialogues",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        searchRow.AddChild(_experienceSearchPattern);
        _experienceSearchRegex = new CheckBox { Text = "Regex sûre" };
        searchRow.AddChild(_experienceSearchRegex);
        intelligenceColumn.AddChild(searchRow);
        var replacementRow = new HBoxContainer();
        replacementRow.AddChild(new Label { Text = "Remplacement", CustomMinimumSize = new Vector2(92, 0) });
        _experienceReplacement = new LineEdit
        {
            PlaceholderText = "Prévisualisation uniquement",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        replacementRow.AddChild(_experienceReplacement);
        intelligenceColumn.AddChild(replacementRow);
        var filterRow = new HBoxContainer();
        filterRow.AddChild(new Label { Text = "Filtres", CustomMinimumSize = new Vector2(92, 0) });
        _experienceSearchFilters = new LineEdit
        {
            PlaceholderText = "Filtres · speaker:guide; condition:HasItem; action:RunScript",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        filterRow.AddChild(_experienceSearchFilters);
        intelligenceColumn.AddChild(filterRow);
        _experienceSearchPreview = new Label
        {
            Text = "Prévisualisation · aucun résultat",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _experienceSearchPreview.AddThemeColorOverride("font_color", new Color("9db8ca"));
        intelligenceColumn.AddChild(_experienceSearchPreview);
        intelligenceColumn.AddChild(new Label { Text = "Badges & légende" });
        var badgesRow = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        badgesRow.AddThemeConstantOverride("h_separation", 10);
        badgesRow.AddThemeConstantOverride("v_separation", 2);
        foreach (var badge in Enum.GetValues<DialogueReviewBadge>())
        {
            var check = new CheckBox { Text = badge.ToString() };
            _experienceBadgeChecks[badge] = check;
            badgesRow.AddChild(check);
        }
        intelligenceColumn.AddChild(badgesRow);
        _experienceReviewMode = new CheckBox { Text = "Mode review · masquer les nœuds approuvés" };
        intelligenceColumn.AddChild(_experienceReviewMode);
        intelligenceColumn.AddChild(new Label
        {
            Text = "TESTS DE COMPÉTENCE",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _experienceSkillChecks = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 72),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        intelligenceColumn.AddChild(_experienceSkillChecks);

        var productionPanel = new PanelContainer();
        productionPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("211a2e"),
            BorderColor = new Color("8756b5"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
        });
        detailColumn.AddChild(productionPanel);
        var production = new VBoxContainer();
        production.AddThemeConstantOverride("separation", 6);
        productionPanel.AddChild(production);
        var productionHeader = new HBoxContainer();
        production.AddChild(productionHeader);
        var productionTitle = new Label
        {
            Text = "PRODUCTION NARRATIVE P1",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        productionTitle.AddThemeColorOverride("font_color", new Color("c792ea"));
        productionHeader.AddChild(productionTitle);
        _productionEnabled = new CheckBox { Text = "Actif" };
        productionHeader.AddChild(_productionEnabled);
        _productionRequired = new CheckBox { Text = "Requis runtime" };
        productionHeader.AddChild(_productionRequired);
        var productionSubtitle = new Label
        {
            Text = "Sous-dialogues versionnés · impact · couverture · collaboration · IA contrôlée",
        };
        productionSubtitle.AddThemeColorOverride("font_color", new Color("b7a1cb"));
        production.AddChild(productionSubtitle);
        var productionColumns = new HBoxContainer();
        productionColumns.AddThemeConstantOverride("separation", 12);
        production.AddChild(productionColumns);
        var productionLeft = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var productionRight = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        productionLeft.AddThemeConstantOverride("separation", 4);
        productionRight.AddThemeConstantOverride("separation", 4);
        productionColumns.AddChild(productionLeft);
        productionColumns.AddChild(productionRight);
        productionLeft.AddChild(new Label
        {
            Text = "SOUS-DIALOGUES",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _productionSubgraphs = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 72),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        productionLeft.AddChild(_productionSubgraphs);
        productionLeft.AddChild(new Label
        {
            Text = "CONSÉQUENCES",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _productionConsequences = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 72),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        productionLeft.AddChild(_productionConsequences);
        productionLeft.AddChild(new Label { Text = "COUVERTURE DES PARCOURS" });
        _productionCoverage = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 72),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        productionLeft.AddChild(_productionCoverage);
        productionRight.AddChild(new Label
        {
            Text = "COLLABORATION PAR NŒUD",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _productionCollaboration = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 92),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        productionRight.AddChild(_productionCollaboration);
        productionRight.AddChild(new Label
        {
            Text = "PROPOSITIONS IA · APERÇU TRANSACTIONNEL",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _productionAiPlans = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 72),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        productionRight.AddChild(_productionAiPlans);
        _productionSummary = new Label
        {
            Text = "Aperçu production · aucun profil",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _productionSummary.AddThemeColorOverride("font_color", new Color("b7a1cb"));
        productionRight.AddChild(_productionSummary);

        detailColumn.AddChild(new Label { Text = "APERÇU" });
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 84),
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        detailColumn.AddChild(_preview);
        detailColumn.AddChild(new Label { Text = "MINICARTE DU GRAPHE" });
        _graphOverview = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 105),
            FitContent = false,
        };
        detailColumn.AddChild(_graphOverview);

        var labColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(350, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        labColumn.AddThemeConstantOverride("separation", 6);
        editor.AddChild(labColumn);
        labColumn.AddChild(new Label { Text = "SIMULATION SANDBOX" });
        labColumn.AddChild(new Label
        {
            Text = "État fictif · namespace:clé=valeur",
            Modulate = new Color("ab9acb"),
        });
        _simulationState = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 92),
            Text = "item:ash-key=true\nquest:crypt-ashes=inactive",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        labColumn.AddChild(_simulationState);
        var simulationActions = new HBoxContainer();
        simulationActions.AddThemeConstantOverride("separation", 6);
        labColumn.AddChild(simulationActions);
        var runSimulation = new Button { Text = "▶ Tester", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        runSimulation.Pressed += RunSimulation;
        simulationActions.AddChild(runSimulation);
        var pseudo = new Button { Text = "Pseudo-loc" };
        pseudo.Pressed += ShowPseudoLocalization;
        simulationActions.AddChild(pseudo);
        _simulationLog = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 245),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        labColumn.AddChild(_simulationLog);
        labColumn.AddChild(new Label { Text = "VALIDATION STRUCTURELLE" });
        _validation = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 220),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        labColumn.AddChild(_validation);

        _status = new Label
        {
            Text = "Prêt.",
        };
        _status.AddThemeColorOverride("font_color", new Color("ab9acb"));
        content.AddChild(_status);
        var bottom = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        bottom.AddThemeConstantOverride("separation", 8);
        content.AddChild(bottom);
        var cancelButton = new Button
        {
            CustomMinimumSize = new Vector2(120, 38),
            Text = "Fermer",
        };
        bottom.AddChild(cancelButton);
        cancelButton.Pressed += Close;
        var previewButton = new Button
        {
            CustomMinimumSize = new Vector2(140, 38),
            Text = "Actualiser l’aperçu",
        };
        bottom.AddChild(previewButton);
        previewButton.Pressed += RefreshPreview;
        var undoButton = new Button { CustomMinimumSize = new Vector2(105, 38), Text = "Annuler" };
        undoButton.Pressed += UndoDialogue;
        bottom.AddChild(undoButton);
        var redoButton = new Button { CustomMinimumSize = new Vector2(105, 38), Text = "Rétablir" };
        redoButton.Pressed += RedoDialogue;
        bottom.AddChild(redoButton);
        var applyButton = new Button
        {
            CustomMinimumSize = new Vector2(210, 38),
            Text = "Appliquer le dialogue",
        };
        applyButton.AddThemeColorOverride("font_color", Colors.White);
        applyButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("e24f2d"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        bottom.AddChild(applyButton);
        applyButton.Pressed += ApplyDialogue;
        BuildStructuredEditors();
    }

    private void BuildStructuredEditors()
    {
        StructuredCollectionField.Attach(_authoringParticipants!, "participant",
            new("Rôle", "guide"),
            new("Type de liaison", Options: Enum.GetNames<DialogueParticipantBindingKind>(),
                DefaultValue: DialogueParticipantBindingKind.Role.ToString()),
            new("Valeur", "guide"), new("Rôle de repli", "narrator"));
        StructuredCollectionField.Attach(_authoringTokens!, "variable de texte",
            new("Nom", "player.name"),
            new("Type", Options: Enum.GetNames<DialogueTextTokenType>(),
                DefaultValue: Enum.GetNames<DialogueTextTokenType>()[0]),
            new("Obligation", Options: ["required", "optional"], DefaultValue: "optional"),
            new("Valeur par défaut", "Hero"));
        StructuredCollectionField.Attach(_authoringWeights!, "poids de choix",
            new("Choix", "UUID du choix"), new("Poids", "1", "1"));
        AttachActionEditor(_experienceCompletedActions!, "action de fin");
        AttachActionEditor(_experienceAbortedActions!, "action d’abandon");
        StructuredCollectionField.Attach(_experienceMemorySlots!, "mémoire",
            new("Portée", Options: Enum.GetNames<DialogueMemoryScope>(),
                DefaultValue: Enum.GetNames<DialogueMemoryScope>()[0]),
            new("Cible", "player"), new("Nom", "trust"),
            new("Type", Options: Enum.GetNames<DialogueTextTokenType>(),
                DefaultValue: Enum.GetNames<DialogueTextTokenType>()[0]),
            new("Durée", "dialogue"), new("Valeur par défaut"));
        StructuredCollectionField.Attach(_experienceSkillChecks!, "test de compétence",
            new("Nœud", "UUID du nœud"), new("Compétence", "persuasion"),
            new("Difficulté", "12", "10"), new("Opposant", "optionnel"),
            new("Avantage", Options: ["true", "false"], DefaultValue: "false"),
            new("Visibilité", Options: Enum.GetNames<DialogueSkillCheckVisibility>(),
                DefaultValue: Enum.GetNames<DialogueSkillCheckVisibility>()[0]),
            new("Nœud si réussite", "UUID"), new("Nœud si échec", "UUID"));
        StructuredCollectionField.Attach(_productionSubgraphs!, "sous-dialogue",
            new("Nœud", "UUID"), new("Clé", "dialogue.key"), new("Version", "1", "1"),
            new("Entrées", "a,b"), new("Sorties", "success,failure"),
            new("Variables requises", "quest.state"), new("Test", "isolated"),
            new("Valeurs d’entrée", "key=value"), new("Résultats attendus", "key=value"));
        StructuredCollectionField.Attach(_productionConsequences!, "conséquence",
            new("Nœud", "UUID"),
            new("Cible", Options: Enum.GetNames<DialogueConsequenceTargetKind>(),
                DefaultValue: DialogueConsequenceTargetKind.World.ToString()),
            new("Identifiant cible", "quest-id"), new("Opération", "set"),
            new("Valeur", "active"), new("Explication", "Conséquence pour le joueur"));
        StructuredCollectionField.Attach(_productionCoverage!, "scénario de couverture",
            new("Type ou profondeur", "Nominal"), new("Nom ou états requis", "main"),
            new("Nœuds visités", "uuid,uuid"), new("État attendu", "key=value"));
        StructuredCollectionField.Attach(_productionCollaboration!, "entrée de collaboration",
            new("Type", Options: ["revision", "lock", "comment", "conflict"], DefaultValue: "comment"),
            new("Nœud", "UUID"), new("Valeur 1"), new("Valeur 2"), new("Valeur 3"),
            new("Valeur 4"), new("Valeur 5"), new("Valeur 6"), new("Valeur 7"), new("Valeur 8"));
        StructuredCollectionField.Attach(_productionAiPlans!, "proposition IA",
            new("Autorisation", Options: Enum.GetNames<DialogueAiAuthorizationLevel>(),
                DefaultValue: DialogueAiAuthorizationLevel.SuggestOnly.ToString()),
            new("Synopsis"), new("Étapes requises", "beat-a,beat-b"),
            new("Règles de style", "concise"), new("Sources de contexte", "lore:entry"),
            new("Différence proposée"));
    }

    private static void AttachActionEditor(TextEdit backing, string itemName) =>
        StructuredCollectionField.Attach(backing, itemName,
            new("Type", Options: Enum.GetNames<DialogueActionKind>(),
                DefaultValue: Enum.GetNames<DialogueActionKind>()[0]),
            new("Cible", "quest.state"), new("Valeur", "active"),
            new("Obligation", Options: ["required", "optional"], DefaultValue: "optional"));

    private void ConnectSignals()
    {
        CloseRequested += Close;
        _dialoguePicker!.ItemSelected += SelectDialogue;
        _nodeList!.ItemSelected += SelectNode;
        _choiceList!.ItemSelected += SelectChoice;
        _choiceList.ChoiceMoved += MoveChoice;
    }

    private void PopulateBlueprints(ProjectState state)
    {
        _speakerIds.Clear();
        _speakerPicker!.Clear();
        _speakerIds.Add(null);
        _speakerPicker.AddItem("Narrateur / système");
        foreach (var blueprint in state.Project.Blueprints
                     .Where(blueprint => blueprint.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
                     .OrderBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal))
        {
            _speakerIds.Add(blueprint.Id);
            _speakerPicker.AddItem(blueprint.DisplayName);
        }
    }

    private void PopulateDialogues(ProjectState state)
    {
        _dialogueIds.Clear();
        _dialoguePicker!.Clear();
        foreach (var dialogue in state.Project.Dialogues.OrderBy(dialogue => dialogue.DisplayName, StringComparer.Ordinal))
        {
            _dialogueIds.Add(dialogue.Id);
            _dialoguePicker.AddItem(dialogue.DisplayName);
        }
    }

    private void SelectDialogue(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _dialogueIds.Count)
        {
            return;
        }

        LoadDialogue(state.Project.Dialogues.Single(dialogue => dialogue.Id == _dialogueIds[(int)index]));
    }

    private void LoadDialogue(DialogueDefinition dialogue)
    {
        _draftDialogueId = dialogue.Id;
        _draftPresentation = dialogue.Presentation;
        _draftSchemaVersion = dialogue.SchemaVersion;
        _draftProvenance = dialogue.Provenance;
        _draftMetadata = dialogue.Metadata;
        _draftParticipants = dialogue.Participants;
        _draftEntryPoints = dialogue.EntryPoints;
        _draftAuthoring = dialogue.Authoring;
        _draftExperience = dialogue.Experience;
        _draftProduction = dialogue.Production;
        _entryNodeId = dialogue.EntryNodeId;
        _dialogueName!.Text = dialogue.DisplayName;
        _dialogueSlug!.Text = dialogue.Slug;
        _draftNodes.Clear();
        _draftNodes.AddRange(dialogue.Nodes);
        _nodePage = 0;
        RefreshNodes(0);
        SetStatus($"Dialogue chargé · {_draftNodes.Count} nœud(s).", false);
    }

    private void DeleteCurrentDialogue()
    {
        if (_draftDialogueId is null || _getState?.Invoke()?.Project.Dialogues
                .All(item => item.Id != _draftDialogueId) != false)
        {
            SetStatus("Le brouillon n’est pas encore enregistré.", false, true);
            return;
        }
        if (_deleteDialogue?.Invoke(_draftDialogueId) == true)
        {
            Refresh();
            SetStatus("Dialogue supprimé · utilisez Annuler pour le restaurer.", true);
        }
        else SetStatus("Suppression refusée · consultez les dépendances du projet.", false, true);
    }

    private void CreateNewDialogue()
    {
        _draftDialogueId = DialogueId.From(Guid.NewGuid());
        _draftPresentation = null;
        _draftSchemaVersion = DialogueDefinition.CurrentSchemaVersion;
        _draftProvenance = "author:dialogue-editor";
        _draftMetadata = new Dictionary<string, string> { ["kind"] = "conversation" };
        _draftParticipants = [];
        _draftEntryPoints = [];
        _draftAuthoring = null;
        _draftExperience = null;
        _draftProduction = null;
        _dialogueName!.Text = "Nouveau dialogue";
        _dialogueSlug!.Text = $"dialogue-{_dialogueIds.Count + 1}";
        _draftNodes.Clear();
        _nodePage = 0;
        var nodeId = DialogueNodeId.From(Guid.NewGuid());
        _entryNodeId = nodeId;
        _draftEntryPoints = [new DialogueEntryPoint(
            Guid.NewGuid(),
            DialogueEntryPoint.CurrentSchemaVersion,
            "default",
            nodeId,
            0,
            "author:dialogue-editor")];
        _draftNodes.Add(new DialogueNode(
            nodeId,
            null,
            new LocalizedText($"{_dialogueSlug.Text}.entry", "Nouvelle réplique")));
        RefreshNodes(0);
        SetStatus("Nouveau dialogue prêt. Ajoutez des choix ou d’autres nœuds.", false);
    }

    private void AddNode()
    {
        if (!SaveSelectedNode())
        {
            return;
        }

        var nodeId = DialogueNodeId.From(Guid.NewGuid());
        _draftNodes.Add(new DialogueNode(
            nodeId,
            null,
            new LocalizedText($"{_dialogueSlug!.Text}.node-{_draftNodes.Count + 1}", "Nouvelle réplique")));
        RefreshNodes(_draftNodes.Count - 1);
        SetStatus("Nœud ajouté. Reliez-le depuis un choix pour rendre le graphe valide.", false);
    }

    private void SetSelectedNodeAsEntry()
    {
        if (CurrentNode is null) return;
        _entryNodeId = CurrentNode.Id;
        _draftEntryPoints = NormalizeEntryPoints();
        RefreshNodes(_selectedNodeIndex);
        SetStatus("Nœud défini comme entrée principale dans le brouillon.", true);
    }

    private void RemoveSelectedNode()
    {
        if (CurrentNode is null || _selectedNodeIndex < 0) return;
        if (_draftNodes.Count <= 1)
        {
            SetStatus("Un dialogue doit conserver au moins un nœud.", false, true);
            return;
        }
        if (CurrentNode.Id == _entryNodeId)
        {
            SetStatus("Choisissez d’abord une autre entrée principale avant de supprimer ce nœud.", false, true);
            return;
        }
        var removedId = CurrentNode.Id;
        var removedReferences = 0;
        _draftNodes.RemoveAt(_selectedNodeIndex);
        for (var index = 0; index < _draftNodes.Count; index++)
        {
            var node = _draftNodes[index];
            var choices = node.Choices.Where(choice =>
            {
                if (choice.TargetNodeId != removedId) return true;
                removedReferences++;
                return false;
            }).ToArray();
            if (choices.Length == node.Choices.Count) continue;
            _draftNodes[index] = new DialogueNode(node.Id, node.SpeakerBlueprintId, node.Text,
                node.OnEnterActions, choices, node.Role, node.ParticipantId, node.SubgraphKey);
        }
        _draftEntryPoints = _draftEntryPoints.Where(item => item.NodeId != removedId).ToArray();
        _selectedNodeIndex = -1;
        _selectedChoiceIndex = -1;
        RefreshNodes(0);
        SetStatus($"Nœud retiré du brouillon · {removedReferences} choix dépendant(s) retiré(s). Appliquez pour enregistrer.", true);
    }

    private void SelectNode(long index)
    {
        var absoluteIndex = (_nodePage * NodePageSize) + (int)index;
        if (index < 0 || absoluteIndex >= _draftNodes.Count)
        {
            return;
        }

        _selectedNodeIndex = absoluteIndex;
        _selectedChoiceIndex = -1;
        var node = _draftNodes[_selectedNodeIndex];
        var speakerIndex = _speakerIds.FindIndex(id => id == node.SpeakerBlueprintId);
        _speakerPicker!.Selected = Math.Max(0, speakerIndex);
        _nodeRole!.Selected = Math.Max(0, _nodeRole.GetItemIndex((int)node.Role));
        _subgraphKey!.Text = node.SubgraphKey ?? string.Empty;
        _nodeTextKey!.Text = node.Text.Key;
        _nodeText!.Text = node.Text.Fallback;
        _nodeVariants!.Text = DialogueAuthoringCodec.FormatVariants(node.Text);
        _nodeActions!.Text = DialogueAuthoringCodec.FormatActions(node.OnEnterActions);
        PopulateAuthoringFields(node);
        PopulateExperienceFields();
        PopulateProductionFields();
        RefreshTargets();
        RefreshChoices();
        RefreshPreview();
    }

    private bool SaveSelectedNode()
    {
        if (_selectedNodeIndex < 0 || _selectedNodeIndex >= _draftNodes.Count)
        {
            return false;
        }

        try
        {
            var previous = _draftNodes[_selectedNodeIndex];
            var speakerIndex = _speakerPicker!.Selected;
            var speaker = speakerIndex >= 0 && speakerIndex < _speakerIds.Count
                ? _speakerIds[speakerIndex]
                : null;
            _draftNodes[_selectedNodeIndex] = new DialogueNode(
                previous.Id,
                speaker,
                BuildLocalizedText(previous.Text, _nodeTextKey!.Text, _nodeText!.Text, _nodeVariants!.Text),
                DialogueAuthoringCodec.ParseActions(_nodeActions!.Text, previous.OnEnterActions),
                previous.Choices,
                (DialogueNodeRole)_nodeRole!.GetItemId(_nodeRole.Selected),
                previous.ParticipantId,
                _subgraphKey!.Text);
            SaveAuthoringFields(previous.Id);
            SaveExperienceFields();
            SaveProductionFields();
            RefreshNodes(_selectedNodeIndex);
            SetStatus("Réplique enregistrée dans le brouillon.", true);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
            return false;
        }
    }

    private void SaveSelectedNodeFromUi() => SaveSelectedNode();

    private void SelectChoice(long index)
    {
        var choices = CurrentNode?.Choices;
        if (choices is null || index < 0 || index >= choices.Count)
        {
            return;
        }

        _selectedChoiceIndex = (int)index;
        var choice = choices[_selectedChoiceIndex];
        _choiceTextKey!.Text = choice.Text.Key;
        _choiceText!.Text = choice.Text.Fallback;
        _choiceVariants!.Text = DialogueAuthoringCodec.FormatVariants(choice.Text);
        _choiceTarget!.Selected = choice.TargetNodeId is null
            ? 0
            : _draftNodes.FindIndex(node => node.Id == choice.TargetNodeId) + 1;
        _choiceEdgeKind!.Selected = Math.Max(0, _choiceEdgeKind.GetItemIndex((int)choice.EdgeKind));
        _choicePriority!.Value = choice.Priority;
        _conditionCombination!.Selected = Math.Max(0,
            _conditionCombination.GetItemIndex((int)choice.ConditionCombination));
        _conditions!.Text = DialogueAuthoringCodec.FormatConditions(choice.Conditions);
        _choiceActionLines!.Text = DialogueAuthoringCodec.FormatActions(choice.Actions);
        _removeChoiceButton!.Disabled = false;
    }

    private void SaveChoice()
    {
        if (!SaveSelectedNode() || CurrentNode is null)
        {
            return;
        }

        try
        {
            var existing = _selectedChoiceIndex >= 0 && _selectedChoiceIndex < CurrentNode.Choices.Count
                ? CurrentNode.Choices[_selectedChoiceIndex]
                : null;
            var conditions = DialogueAuthoringCodec.ParseConditions(_conditions!.Text, existing?.Conditions);
            var actions = DialogueAuthoringCodec.ParseActions(_choiceActionLines!.Text, existing?.Actions);
            var targetIndex = _choiceTarget!.Selected - 1;
            var target = targetIndex >= 0 && targetIndex < _draftNodes.Count
                ? _draftNodes[targetIndex].Id
                : null;
            var choice = new DialogueChoice(
                existing?.Id ?? DialogueChoiceId.From(Guid.NewGuid()),
                BuildLocalizedText(existing?.Text, _choiceTextKey!.Text, _choiceText!.Text, _choiceVariants!.Text),
                conditions,
                actions,
                target,
                (DialogueEdgeKind)_choiceEdgeKind!.GetItemId(_choiceEdgeKind.Selected),
                (int)_choicePriority!.Value,
                (DialogueConditionCombination)_conditionCombination!.GetItemId(_conditionCombination.Selected));
            var choices = CurrentNode.Choices.ToList();
            if (existing is null)
            {
                choices.Add(choice);
                _selectedChoiceIndex = choices.Count - 1;
            }
            else
            {
                choices[_selectedChoiceIndex] = choice;
            }

            ReplaceCurrentNode(new DialogueNode(
                CurrentNode.Id,
                CurrentNode.SpeakerBlueprintId,
                CurrentNode.Text,
                CurrentNode.OnEnterActions,
                choices,
                CurrentNode.Role,
                CurrentNode.ParticipantId,
                CurrentNode.SubgraphKey));
            RefreshChoices(_selectedChoiceIndex);
            RefreshPreview();
            SetStatus("Choix enregistré dans le brouillon.", true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private void RemoveChoice()
    {
        if (CurrentNode is null || _selectedChoiceIndex < 0 ||
            _selectedChoiceIndex >= CurrentNode.Choices.Count)
        {
            return;
        }

        var choices = CurrentNode.Choices.Where((_, index) => index != _selectedChoiceIndex).ToArray();
        ReplaceCurrentNode(new DialogueNode(
            CurrentNode.Id,
            CurrentNode.SpeakerBlueprintId,
            CurrentNode.Text,
            CurrentNode.OnEnterActions,
            choices,
            CurrentNode.Role,
            CurrentNode.ParticipantId,
            CurrentNode.SubgraphKey));
        _selectedChoiceIndex = -1;
        RefreshChoices();
        RefreshPreview();
        SetStatus("Choix retiré du brouillon.", true);
    }

    private void ApplyDialogue()
    {
        if (!SaveSelectedNode() || _draftDialogueId is null || _entryNodeId is null)
        {
            return;
        }

        try
        {
            var dialogue = CreateDraftDefinition();
            if (_applyDialogue?.Invoke(dialogue) == true)
            {
                SetStatus("Dialogue appliqué au projet.", true);
                Close();
            }
            else
            {
                SetStatus("Le graphe est invalide. Consultez les diagnostics du projet.", false, true);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private void RefreshNodes(int selectedIndex)
    {
        _nodeList!.Clear();
        if (_draftNodes.Count > 0)
        {
            _nodePage = Math.Clamp(selectedIndex / NodePageSize, 0, (_draftNodes.Count - 1) / NodePageSize);
        }
        foreach (var node in _draftNodes.Skip(_nodePage * NodePageSize).Take(NodePageSize))
        {
            var prefix = node.Id == _entryNodeId ? "◆" : "◇";
            _nodeList.AddItem($"{prefix} [{node.Role}] {node.Text.Fallback}");
            _nodeList.SetItemTooltip(_nodeList.ItemCount - 1,
                $"UUID {node.Id} · participant {node.ParticipantId:D} · sous-graphe {node.SubgraphKey ?? "principal"}");
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(_draftNodes.Count / (double)NodePageSize));
        _nodePageLabel!.Text = $"Page {_nodePage + 1} / {pageCount} · {_draftNodes.Count} nœuds";

        if (_draftNodes.Count == 0)
        {
            _selectedNodeIndex = -1;
            return;
        }

        _selectedNodeIndex = Math.Clamp(selectedIndex, 0, _draftNodes.Count - 1);
        var visibleIndex = _selectedNodeIndex - (_nodePage * NodePageSize);
        _nodeList.Select(visibleIndex);
        SelectNode(visibleIndex);
    }

    private void ChangeNodePage(int direction)
    {
        if (_draftNodes.Count == 0) return;
        var pageCount = (int)Math.Ceiling(_draftNodes.Count / (double)NodePageSize);
        var targetPage = Math.Clamp(_nodePage + direction, 0, pageCount - 1);
        if (targetPage == _nodePage) return;
        _nodePage = targetPage;
        RefreshNodes(_nodePage * NodePageSize);
    }

    private void RefreshTargets()
    {
        _choiceTarget!.Clear();
        _choiceTarget.AddItem("Fin du dialogue");
        foreach (var node in _draftNodes)
        {
            _choiceTarget.AddItem(node.Text.Fallback);
        }
    }

    private void RefreshChoices(int selectedIndex = -1)
    {
        _choiceList!.Clear();
        foreach (var choice in CurrentNode?.Choices ?? [])
        {
            var conditionBadge = choice.Conditions.Count > 0
                ? $" · {choice.ConditionCombination} {choice.Conditions.Count} condition(s)"
                : string.Empty;
            var actionBadge = choice.Actions.Count > 0 ? $" · {choice.Actions.Count} action(s)" : string.Empty;
            var referenceBadge = choice.EdgeKind == DialogueEdgeKind.Reference ? " · référence" : string.Empty;
            _choiceList.AddItem($"P{choice.Priority:D2} · {choice.Text.Fallback}{referenceBadge}{conditionBadge}{actionBadge}");
        }

        _selectedChoiceIndex = selectedIndex;
        _removeChoiceButton!.Disabled = selectedIndex < 0;
        if (selectedIndex >= 0 && CurrentNode is not null && selectedIndex < CurrentNode.Choices.Count)
        {
            _choiceList.Select(selectedIndex);
        }
        else
        {
            _choiceTextKey!.Clear();
            _choiceText!.Clear();
            _choiceVariants!.Clear();
            _conditionCombination!.Selected = 0;
            _conditions!.Clear();
            _choiceActionLines!.Clear();
            _choiceTarget!.Selected = 0;
            _choiceEdgeKind!.Selected = 0;
            _choicePriority!.Value = 0;
        }
    }

    private void RefreshPreview()
    {
        if (_preview is null || CurrentNode is null)
        {
            return;
        }

        var speaker = CurrentNode.SpeakerBlueprintId is null
            ? "Narrateur"
            : _getState?.Invoke()?.Project.Blueprints
                .SingleOrDefault(blueprint => blueprint.Id == CurrentNode.SpeakerBlueprintId)?.DisplayName ?? "Intervenant";
        var lines = new List<string>
        {
            $"[color=#ff9858][b]{speaker}[/b][/color]",
            CurrentNode.Text.Fallback,
            string.Empty,
        };
        foreach (var choice in CurrentNode.Choices)
        {
            var suffix = choice.Conditions.Count == 0
                ? string.Empty
                : $"  [color=#ab9acb]si {choice.ConditionCombination} · {choice.Conditions.Count} prédicat(s)[/color]";
            lines.Add($"› {choice.Text.Fallback}{suffix}");
        }

        if (CurrentNode.Choices.Count == 0)
        {
            lines.Add("[color=#ab9acb]Fin de branche · ajoutez un choix pour continuer.[/color]");
        }

        _preview.Text = string.Join("\n", lines);
        RefreshGraphOverview();
        RefreshValidation();
    }

    private void MoveChoice(int sourceIndex, int targetIndex)
    {
        if (CurrentNode is null || sourceIndex < 0 || sourceIndex >= CurrentNode.Choices.Count ||
            targetIndex < 0 || targetIndex >= CurrentNode.Choices.Count || sourceIndex == targetIndex) return;
        var choices = CurrentNode.Choices.ToList();
        var moved = choices[sourceIndex];
        choices.RemoveAt(sourceIndex);
        choices.Insert(targetIndex, moved);
        ReplaceCurrentNode(new DialogueNode(CurrentNode.Id, CurrentNode.SpeakerBlueprintId, CurrentNode.Text,
            CurrentNode.OnEnterActions, choices, CurrentNode.Role, CurrentNode.ParticipantId, CurrentNode.SubgraphKey));
        RefreshChoices(targetIndex);
        RefreshPreview();
        SetStatus($"Ordre des réponses modifié · {sourceIndex + 1} → {targetIndex + 1}.", true);
    }

    private void RefreshGraphOverview()
    {
        if (_graphOverview is null) return;
        var lines = new List<string>();
        foreach (var node in _draftNodes)
        {
            var incoming = _draftNodes.Sum(candidate => candidate.Choices.Count(choice => choice.TargetNodeId == node.Id));
            var outgoing = node.Choices.Count(choice => choice.TargetNodeId is not null);
            var marker = node.Id == _entryNodeId ? "◆" : "◇";
            var subgroup = string.IsNullOrWhiteSpace(node.SubgraphKey) ? string.Empty : $" · {node.SubgraphKey}";
            lines.Add($"{marker} [b]{node.Role}[/b]{subgroup} · {incoming} entrant(s) / {outgoing} sortant(s) · {node.Text.Fallback}");
            foreach (var choice in node.Choices.Where(choice => choice.TargetNodeId is not null))
            {
                var target = _draftNodes.FindIndex(candidate => candidate.Id == choice.TargetNodeId) + 1;
                lines.Add($"    {(choice.EdgeKind == DialogueEdgeKind.Reference ? "↪ réf." : "→")} N{target} · P{choice.Priority} · {choice.Text.Fallback}");
            }
        }
        _graphOverview.Text = string.Join("\n", lines);
    }

    private void RefreshValidation()
    {
        if (_validation is null || _draftDialogueId is null || _entryNodeId is null || _draftNodes.Count == 0)
            return;
        try
        {
            var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
            _validation.Text = diagnostics.Count == 0
                ? "[color=#83d6ae]✓ Graphe valide · localisation, conditions, actions, fins et références cohérentes.[/color]"
                : string.Join("\n", diagnostics.Take(12).Select(item =>
                    $"[color=#ff8c8c]• {item.Code}[/color]\n  {item.Path}\n  {item.Message}"));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _validation.Text = $"[color=#ff8c8c]• dialogue.draft.invalid[/color]\n  {exception.Message}";
        }
    }

    private void RunSimulation()
    {
        if (_simulationLog is null || _simulationState is null || !SaveSelectedNode()) return;
        try
        {
            var state = DialogueAuthoringCodec.ParseState(_simulationState.Text);
            var result = DialogueSimulationService.Run(CreateDraftDefinition(), state);
            var lines = new List<string>
            {
                result.Succeeded
                    ? $"[color=#83d6ae]✓ {result.StopReason} · {result.VisitedNodes.Count} nœud(s)[/color]"
                    : $"[color=#ff8c8c]✗ {result.StopReason}[/color]",
            };
            lines.AddRange(result.Journal.Take(30).Select(item =>
                $"[color=#ab9acb]{item.Sequence:D2} · {item.Kind}[/color] {item.Message}"));
            if (result.FinalState.Values.Count > 0)
            {
                lines.Add("\n[b]État final sandbox[/b]");
                lines.AddRange(result.FinalState.Values.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => $"{item.Key}={item.Value}"));
            }
            lines.AddRange(result.Diagnostics.Select(item => $"[color=#ff8c8c]{item.Code}[/color] · {item.Message}"));
            _simulationLog.Text = string.Join("\n", lines);
            SetStatus("Simulation sandbox terminée · le projet n’a pas été modifié.", result.Succeeded,
                !result.Succeeded);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _simulationLog.Text = $"[color=#ff8c8c]État fictif invalide[/color]\n{exception.Message}";
            SetStatus(exception.Message, false, true);
        }
    }

    private void ShowPseudoLocalization()
    {
        if (_simulationLog is null || CurrentNode is null) return;
        _simulationLog.Text =
            $"[color=#ff9858][b]Pseudo-localisation[/b][/color]\n{DialogueAuthoringCodec.PseudoLocalize(CurrentNode.Text)}";
        SetStatus("Pseudo-localisation générée en mémoire · aucun fichier écrit.", true);
    }

    private DialogueDefinition CreateDraftDefinition()
    {
        var dialogue = CreateBaseDraftDefinition();
        if (_draftAuthoring is not null) dialogue = dialogue.WithAuthoring(_draftAuthoring);
        if (_draftExperience is not null) dialogue = dialogue.WithExperience(_draftExperience);
        return _draftProduction is null ? dialogue : dialogue.WithProduction(_draftProduction);
    }

    private DialogueDefinition CreateBaseDraftDefinition()
    {
        if (_draftDialogueId is null || _entryNodeId is null)
            throw new InvalidOperationException("Aucun dialogue n’est chargé.");
        return new DialogueDefinition(
            _draftDialogueId,
            _dialogueSlug!.Text,
            _dialogueName!.Text,
            _entryNodeId,
            _draftNodes,
            _draftPresentation,
            _draftSchemaVersion,
            _draftProvenance,
            _draftMetadata,
            MergeParticipants(),
            NormalizeEntryPoints(),
            edges: null);
    }

    private void PopulateAuthoringFields(DialogueNode node)
    {
        var baseline = DialogueAuthoringProfile.CreateDefault(CreateBaseDraftDefinition(), "author:r068");
        var profile = _draftAuthoring ?? baseline with { Enabled = false };
        var item = profile.Nodes.SingleOrDefault(value => value.NodeId == node.Id) ??
                   baseline.Nodes.Single(value => value.NodeId == node.Id);
        _authoringEnabled!.ButtonPressed = _draftAuthoring?.Enabled == true;
        _authoringRequired!.ButtonPressed = _draftAuthoring?.RequiredAtRuntime == true;
        _authoringParticipants!.Text = string.Join('\n', profile.Participants.Select(value =>
            $"{value.Role}|{value.Kind}|{value.Value}|{value.FallbackRole ?? string.Empty}"));
        _authoringEvaluation!.Selected = Math.Max(0,
            _authoringEvaluation.GetItemIndex((int)item.EvaluationMode));
        _authoringTokens!.Text = string.Join('\n', item.Tokens.Select(value =>
            $"{value.Name}|{value.Type}|{(value.Required ? "required" : "optional")}|{value.DefaultValue ?? string.Empty}"));
        _authoringWeights!.Text = string.Join('\n', item.ChoiceEvaluations.Select(value =>
            $"{value.ChoiceId}|{value.Weight:R}"));
        _notesIntent!.Text = item.Notes.Intent;
        _notesEmotion!.Text = item.Notes.Emotion;
        _notesSource!.Text = item.Notes.Source;
        _notesStatus!.Selected = Math.Max(0, _notesStatus.GetItemIndex((int)item.Notes.Status));
        _notesTodos!.Text = string.Join("; ", item.Notes.Todos);
        _notesAuthor!.Text = item.Notes.AuthorMention;
        _wizardSources!.Text = item.WizardEntries.Count == 0
            ? "Sources visuelles · aucune"
            : $"Sources visuelles · {item.WizardEntries.Count} · " +
              string.Join(" · ", item.WizardEntries.Take(2).Select(value => value.SourceDescription));
    }

    private void SaveAuthoringFields(DialogueNodeId nodeId)
    {
        if (_draftAuthoring is null && _authoringEnabled?.ButtonPressed != true) return;
        var dialogue = CreateBaseDraftDefinition();
        var baseline = DialogueAuthoringProfile.CreateDefault(dialogue, "author:r068");
        var source = _draftAuthoring ?? baseline;
        var participantIds = source.Participants.ToDictionary(item => item.Role, item => item.Id,
            StringComparer.Ordinal);
        var participants = ParseLines(_authoringParticipants!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || !Enum.TryParse<DialogueParticipantBindingKind>(parts[1], true, out var kind))
                throw new ArgumentException($"Participant ligne {index + 1} : rôle|kind|valeur|fallback attendu.");
            return new DialogueParticipantBinding(
                participantIds.GetValueOrDefault(parts[0], Guid.NewGuid()),
                DialogueParticipantBinding.CurrentSchemaVersion, "author:r068", parts[0], kind, parts[2],
                parts.Length == 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null);
        }).ToArray();
        if (participants.Length == 0) participants = baseline.Participants.ToArray();

        var normalizedNodes = baseline.Nodes.Select(defaultNode =>
            source.Nodes.SingleOrDefault(item => item.NodeId == defaultNode.NodeId) ?? defaultNode).ToArray();
        var sourceNode = normalizedNodes.Single(item => item.NodeId == nodeId);
        var tokenIds = sourceNode.Tokens.ToDictionary(item => item.Name, item => item.Id, StringComparer.Ordinal);
        var tokens = ParseLines(_authoringTokens!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || !Enum.TryParse<DialogueTextTokenType>(parts[1], true, out var type) ||
                parts[2] is not ("required" or "optional"))
                throw new ArgumentException($"Token ligne {index + 1} : nom|type|required/optional|défaut attendu.");
            return new DialogueTextToken(tokenIds.GetValueOrDefault(parts[0], Guid.NewGuid()),
                DialogueTextToken.CurrentSchemaVersion, "author:r068", parts[0], type,
                parts[2] == "required", parts.Length == 4 && parts[3].Length > 0 ? parts[3] : null);
        }).ToArray();
        var evaluationIds = sourceNode.ChoiceEvaluations.ToDictionary(item => item.ChoiceId, item => item.Id);
        var evaluations = ParseLines(_authoringWeights!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var weight))
                throw new ArgumentException($"Poids ligne {index + 1} : UUID|poids attendu.");
            var choiceId = DialogueChoiceId.Parse(parts[0]);
            return new DialogueChoiceEvaluation(evaluationIds.GetValueOrDefault(choiceId, Guid.NewGuid()),
                DialogueChoiceEvaluation.CurrentSchemaVersion, "author:r068", choiceId, weight);
        }).ToArray();
        if (evaluations.Length == 0)
            evaluations = baseline.Nodes.Single(item => item.NodeId == nodeId).ChoiceEvaluations.ToArray();
        var canonicalNode = dialogue.Nodes.Single(item => item.Id == nodeId);
        var wizardTargets = canonicalNode.OnEnterActions.Select(item => item.Id)
            .Concat(canonicalNode.Choices.SelectMany(item => item.Conditions).Select(item => item.Id))
            .Concat(canonicalNode.Choices.SelectMany(item => item.Actions).Select(item => item.Id)).ToHashSet();
        var notes = sourceNode.Notes with
        {
            SchemaVersion = DialogueNodeEditorialNotes.CurrentSchemaVersion,
            Provenance = "author:r068",
            Intent = _notesIntent!.Text.Trim(),
            Emotion = _notesEmotion!.Text.Trim(),
            Source = _notesSource!.Text.Trim(),
            Status = (DialogueNodeEditorialStatus)_notesStatus!.GetItemId(_notesStatus.Selected),
            Todos = _notesTodos!.Text.Split(';', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries),
            AuthorMention = _notesAuthor!.Text.Trim(),
        };
        var updatedNode = sourceNode with
        {
            Provenance = "author:r068",
            SpeakerRole = participants.Any(item => item.Role == sourceNode.SpeakerRole)
                ? sourceNode.SpeakerRole
                : participants[0].Role,
            EvaluationMode = (DialogueChoiceEvaluationMode)_authoringEvaluation!.GetItemId(
                _authoringEvaluation.Selected),
            Tokens = tokens,
            ChoiceEvaluations = evaluations,
            WizardEntries = sourceNode.WizardEntries.Where(item => wizardTargets.Contains(item.TargetId)).ToArray(),
            Notes = notes,
        };
        _draftAuthoring = source with
        {
            SchemaVersion = DialogueAuthoringProfile.CurrentSchemaVersion,
            Provenance = "author:r068",
            Enabled = _authoringEnabled!.ButtonPressed,
            RequiredAtRuntime = _authoringRequired!.ButtonPressed,
            Participants = participants,
            Nodes = normalizedNodes.Select(item => item.NodeId == nodeId ? updatedNode : item).ToArray(),
        };
    }

    private void PopulateExperienceFields()
    {
        var dialogue = CreateBaseDraftDefinition();
        var baseline = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069");
        var profile = _draftExperience ?? baseline with { Enabled = false };
        _experienceEnabled!.ButtonPressed = _draftExperience?.Enabled == true;
        _experienceRequired!.ButtonPressed = _draftExperience?.RequiredAtRuntime == true;
        _experienceRollback!.ButtonPressed = profile.Lifecycle.RollbackOnAbort;
        _experienceResume!.ButtonPressed = profile.Lifecycle.CreateResumeState;
        _experienceCompletedActions!.Text = DialogueAuthoringCodec.FormatActions(profile.Lifecycle.CompletedActions);
        _experienceAbortedActions!.Text = DialogueAuthoringCodec.FormatActions(profile.Lifecycle.AbortedActions);
        _experienceSearchPattern!.Text = profile.Search.Pattern;
        _experienceSearchRegex!.ButtonPressed = profile.Search.UseRegex;
        _experienceReplacement!.Text = profile.Search.ReplacementPreview;
        _experienceSearchFilters!.Text = string.Join("; ",
            profile.Search.SpeakerRoles.Select(item => $"speaker:{item}")
                .Concat(profile.Search.ConditionKinds.Select(item => $"condition:{item}"))
                .Concat(profile.Search.ActionKinds.Select(item => $"action:{item}")));
        foreach (var item in _experienceBadgeChecks)
            item.Value.ButtonPressed = profile.Review.Badges.Contains(item.Key);
        _experienceReviewMode!.ButtonPressed = profile.Review.ReviewMode;
        _experienceMemorySlots!.Text = string.Join('\n', profile.MemorySlots.Select(item =>
            $"{item.Scope}|{item.ScopeId}|{item.Name}|{item.Type}|{item.Duration}|{item.DefaultValue ?? string.Empty}"));
        _experienceSkillChecks!.Text = string.Join('\n', profile.SkillChecks.Select(item =>
            $"{item.NodeId}|{item.SkillId}|{item.Difficulty:R}|{item.OpponentId ?? string.Empty}|" +
            $"{item.Advantage.ToString().ToLowerInvariant()}|{item.Visibility}|{item.SuccessNodeId}|{item.FailureNodeId}"));
        try
        {
            var dialogues = (_getState?.Invoke()?.Project.Dialogues ?? [])
                .Where(item => item.Id != dialogue.Id).Append(dialogue).ToArray();
            var preview = DialogueExperienceRuntimeService.PreviewReplace(dialogues, profile.Search);
            _experienceSearchPreview!.Text =
                $"Prévisualisation · {preview.Hits.Count} résultat(s) · {preview.Replacements.Count} champ(s) · aucun écrit";
        }
        catch (ArgumentException exception)
        {
            _experienceSearchPreview!.Text = $"Erreur recherche · {exception.Message}";
        }
    }

    private void SaveExperienceFields()
    {
        if (_draftExperience is null && _experienceEnabled?.ButtonPressed != true) return;
        var dialogue = CreateBaseDraftDefinition();
        var baseline = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069");
        var source = _draftExperience ?? baseline;
        var lifecycle = source.Lifecycle with
        {
            SchemaVersion = DialogueLifecyclePolicy.CurrentSchemaVersion,
            Provenance = "author:r069",
            CompletedActions = DialogueAuthoringCodec.ParseActions(_experienceCompletedActions!.Text,
                source.Lifecycle.CompletedActions),
            AbortedActions = DialogueAuthoringCodec.ParseActions(_experienceAbortedActions!.Text,
                source.Lifecycle.AbortedActions),
            RollbackOnAbort = _experienceRollback!.ButtonPressed,
            CreateResumeState = _experienceResume!.ButtonPressed,
        };
        var speakers = new List<string>();
        var conditions = new List<DialogueConditionKind>();
        var actions = new List<DialogueActionKind>();
        foreach (var token in _experienceSearchFilters!.Text.Split(';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                throw new ArgumentException("Filtre recherche attendu : speaker|condition|action:valeur.");
            switch (parts[0].ToLowerInvariant())
            {
                case "speaker": speakers.Add(parts[1]); break;
                case "condition" when Enum.TryParse<DialogueConditionKind>(parts[1], true, out var kind):
                    conditions.Add(kind); break;
                case "action" when Enum.TryParse<DialogueActionKind>(parts[1], true, out var action):
                    actions.Add(action); break;
                default: throw new ArgumentException($"Filtre recherche inconnu : {token}.");
            }
        }
        var search = source.Search with
        {
            SchemaVersion = DialogueSearchProfile.CurrentSchemaVersion,
            Provenance = "author:r069",
            Pattern = _experienceSearchPattern!.Text.Trim(),
            UseRegex = _experienceSearchRegex!.ButtonPressed,
            SpeakerRoles = speakers.Distinct(StringComparer.Ordinal).ToArray(),
            ConditionKinds = conditions.Distinct().ToArray(),
            ActionKinds = actions.Distinct().ToArray(),
            ReplacementPreview = _experienceReplacement!.Text,
        };
        DialogueExperienceRuntimeService.ValidateSearch(search);
        var review = source.Review with
        {
            SchemaVersion = DialogueReviewProfile.CurrentSchemaVersion,
            Provenance = "author:r069",
            Badges = _experienceBadgeChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key)
                .Order().ToArray(),
            ReviewMode = _experienceReviewMode!.ButtonPressed,
        };
        var slotIds = source.MemorySlots.ToDictionary(item => (item.Scope, item.ScopeId, item.Name), item => item.Id);
        var memory = ParseLines(_experienceMemorySlots!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 6, StringSplitOptions.TrimEntries);
            if (parts.Length < 5 || !Enum.TryParse<DialogueMemoryScope>(parts[0], true, out var scope) ||
                !Enum.TryParse<DialogueTextTokenType>(parts[3], true, out var type) ||
                !Enum.TryParse<DialogueMemoryDuration>(parts[4], true, out var duration))
                throw new ArgumentException($"Mémoire ligne {index + 1} : scope|cible|nom|type|durée|défaut attendu.");
            return new DialogueMemorySlot(slotIds.GetValueOrDefault((scope, parts[1], parts[2]), Guid.NewGuid()),
                DialogueMemorySlot.CurrentSchemaVersion, "author:r069", scope, parts[1], parts[2], type, duration,
                parts.Length == 6 && parts[5].Length > 0 ? parts[5] : null);
        }).ToArray();
        var checkIds = source.SkillChecks.ToDictionary(item => item.NodeId, item => item.Id);
        var checks = ParseLines(_experienceSkillChecks!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 8, StringSplitOptions.TrimEntries);
            if (parts.Length != 8 || !double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var difficulty) ||
                !bool.TryParse(parts[4], out var advantage) ||
                !Enum.TryParse<DialogueSkillCheckVisibility>(parts[5], true, out var visibility))
                throw new ArgumentException($"Check ligne {index + 1} : node|skill|DD|opposant|advantage|visibilité|succès|échec attendu.");
            var nodeId = DialogueNodeId.Parse(parts[0]);
            return new DialogueSkillCheck(checkIds.GetValueOrDefault(nodeId, Guid.NewGuid()),
                DialogueSkillCheck.CurrentSchemaVersion, "author:r069", nodeId, parts[1], difficulty,
                parts[3].Length > 0 ? parts[3] : null, advantage, visibility,
                DialogueNodeId.Parse(parts[6]), DialogueNodeId.Parse(parts[7]));
        }).ToArray();
        _draftExperience = source with
        {
            SchemaVersion = DialogueExperienceProfile.CurrentSchemaVersion,
            Provenance = "author:r069",
            Enabled = _experienceEnabled!.ButtonPressed,
            RequiredAtRuntime = _experienceRequired!.ButtonPressed,
            Lifecycle = lifecycle,
            Search = search,
            Review = review,
            MemorySlots = memory,
            SkillChecks = checks,
        };
    }

    private void PopulateProductionFields()
    {
        var dialogue = CreateBaseDraftDefinition();
        var baseline = DialogueProductionProfile.CreateDefault(dialogue, "author:r070");
        var profile = _draftProduction ?? baseline with { Enabled = false };
        _productionEnabled!.ButtonPressed = _draftProduction?.Enabled == true;
        _productionRequired!.ButtonPressed = _draftProduction?.RequiredAtRuntime == true;
        _productionSubgraphs!.Text = string.Join('\n', profile.Invocations.Select(invocation =>
        {
            var contract = profile.Subgraphs.Single(item => item.Id == invocation.ContractId);
            var inputs = string.Join(',', contract.Parameters.Where(item =>
                item.Direction == DialogueSubgraphParameterDirection.Input).Select(item =>
                $"{item.Name}:{item.Type}:{(item.Required ? "required" : "optional")}:" +
                invocation.InputBindings.GetValueOrDefault(item.Name, string.Empty)));
            var outputs = string.Join(',', contract.Parameters.Where(item =>
                item.Direction == DialogueSubgraphParameterDirection.Output).Select(item =>
                $"{item.Name}:{item.Type}:{(item.Required ? "required" : "optional")}:" +
                invocation.OutputBindings.GetValueOrDefault(item.Name, string.Empty)));
            var test = contract.Tests.FirstOrDefault();
            return $"{invocation.NodeId}|{contract.Key}|{contract.Version}|{inputs}|{outputs}|" +
                   $"{string.Join(',', contract.RequiredVariables)}|{test?.Name ?? "isolated"}|" +
                   $"{FormatMap(test?.Inputs)}|{FormatMap(test?.ExpectedOutputs)}";
        }));
        _productionConsequences!.Text = string.Join('\n', profile.Consequences.Select(item =>
            $"{item.NodeId}|{item.Kind}|{item.TargetId}|{item.Operation}|{item.Value}|{item.Explanation}"));
        _productionCoverage!.Text = string.Join('\n',
            new[] { $"{profile.Coverage.MaximumDepth}|{FormatMap(profile.Coverage.RequiredStates)}" }
                .Concat(profile.Coverage.Evidence.Select(item =>
                    $"{item.Kind}|{item.Name}|{string.Join(',', item.VisitedNodes)}|{FormatMap(item.State)}")));
        var collaborationLines = new List<string>();
        collaborationLines.AddRange(profile.Collaboration.Revisions.Select(item =>
            $"revision|{item.NodeId}|{item.Revision}|{item.AuthorId}|{item.CanonicalJson}"));
        collaborationLines.AddRange(profile.Collaboration.Locks.Select(item =>
            $"lock|{item.NodeId}|{item.OwnerId}|{item.ExpiresAt:O}"));
        collaborationLines.AddRange(profile.Collaboration.Comments.Select(item =>
            $"comment|{item.NodeId}|{item.AuthorId}|{item.Resolved.ToString().ToLowerInvariant()}|{item.Text}"));
        collaborationLines.AddRange(profile.Collaboration.Conflicts.Select(item =>
            $"conflict|{item.NodeId}|{item.Field}|{item.BaseValue}|{item.OursValue}|{item.TheirsValue}|" +
            $"{item.Resolution}|{item.ManualValue ?? string.Empty}|{item.Resolved.ToString().ToLowerInvariant()}"));
        _productionCollaboration!.Text = string.Join('\n', collaborationLines);
        _productionAiPlans!.Text = string.Join('\n', profile.AiPlans.Select(item =>
            $"{item.Authorization}|{item.Synopsis}|{string.Join(',', item.RequiredBeats)}|" +
            $"{string.Join(',', item.StyleRules)}|{string.Join(',', item.ContextCitations)}|{item.SemanticDiff}"));
        try
        {
            var report = _draftProduction is null
                ? null
                : DialogueProductionRuntimeService.MeasureCoverage(dialogue.WithProduction(profile));
            _productionSummary!.Text = report is null
                ? "Aperçu production · profil optionnel absent"
                : $"Couverture {report.CoveredNodes.Count}/{profile.Coverage.Evidence.SelectMany(item => item.VisitedNodes).Concat(report.UncoveredNodes).Distinct().Count()} · " +
                  $"{report.Paths.Count} chemin(s), {report.ImpossiblePaths.Count} impossible(s) · " +
                  $"{profile.Consequences.Count} impact(s) · {profile.Collaboration.Conflicts.Count} conflit(s) · " +
                  $"{profile.AiPlans.Count(item => item.PreviewOnly)} preview IA";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _productionSummary!.Text = $"Erreur production · {exception.Message}";
        }
    }

    private void SaveProductionFields()
    {
        if (_draftProduction is null && _productionEnabled?.ButtonPressed != true) return;
        var dialogue = CreateBaseDraftDefinition();
        var source = _draftProduction ?? DialogueProductionProfile.CreateDefault(dialogue, "author:r070");
        var contractByKey = source.Subgraphs.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var invocationByNode = source.Invocations.ToDictionary(item => item.NodeId);
        var contracts = new List<DialogueSubgraphContract>();
        var invocations = new List<DialogueSubgraphInvocation>();
        foreach (var (line, index) in ParseLines(_productionSubgraphs!.Text).Select((value, i) => (value, i)))
        {
            var parts = line.Split('|', 9, StringSplitOptions.TrimEntries);
            if (parts.Length != 9 || !int.TryParse(parts[2], out var version) || version <= 0)
                throw new ArgumentException($"Sous-dialogue ligne {index + 1} : node|clé|version|entrées|sorties|variables|test|in|out attendu.");
            var nodeId = DialogueNodeId.Parse(parts[0]);
            contractByKey.TryGetValue(parts[1], out var previousContract);
            var parameterIds = (previousContract?.Parameters ?? []).ToDictionary(item =>
                (item.Direction, item.Name), item => item.Id);
            var inputBindings = new Dictionary<string, string>(StringComparer.Ordinal);
            var outputBindings = new Dictionary<string, string>(StringComparer.Ordinal);
            var parameters = ParseParameters(parts[3], DialogueSubgraphParameterDirection.Input, parameterIds,
                    inputBindings)
                .Concat(ParseParameters(parts[4], DialogueSubgraphParameterDirection.Output, parameterIds,
                    outputBindings)).ToArray();
            var previousTest = previousContract?.Tests.FirstOrDefault();
            var test = new DialogueSubgraphTestCase(previousTest?.Id ?? Guid.NewGuid(), 1, "author:r070", parts[6],
                ParseMap(parts[7]), ParseMap(parts[8]));
            var contract = new DialogueSubgraphContract(previousContract?.Id ?? Guid.NewGuid(), 1, "author:r070",
                parts[1], version, parameters, Csv(parts[5]), [test]);
            contracts.Add(contract);
            invocationByNode.TryGetValue(nodeId, out var previousInvocation);
            invocations.Add(new DialogueSubgraphInvocation(previousInvocation?.Id ?? Guid.NewGuid(), 1,
                "author:r070", nodeId, contract.Id, inputBindings, outputBindings));
        }
        var consequenceIds = source.Consequences.ToDictionary(item => (item.NodeId, item.Kind, item.TargetId),
            item => item.Id);
        var consequences = ParseLines(_productionConsequences!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 6, StringSplitOptions.TrimEntries);
            if (parts.Length != 6 || !Enum.TryParse<DialogueConsequenceTargetKind>(parts[1], true, out var kind))
                throw new ArgumentException($"Conséquence ligne {index + 1} : node|type|cible|opération|valeur|explication attendu.");
            var nodeId = DialogueNodeId.Parse(parts[0]);
            return new DialogueTypedConsequence(consequenceIds.GetValueOrDefault((nodeId, kind, parts[2]),
                Guid.NewGuid()), 1, "author:r070", nodeId, kind, parts[2], parts[3], parts[4], parts[5]);
        }).ToArray();
        var coverageLines = ParseLines(_productionCoverage!.Text).ToArray();
        if (coverageLines.Length == 0) throw new ArgumentException("La couverture requiert une ligne maxDepth|états.");
        var coverageHeader = coverageLines[0].Split('|', 2, StringSplitOptions.TrimEntries);
        if (coverageHeader.Length != 2 || !int.TryParse(coverageHeader[0], out var maximumDepth))
            throw new ArgumentException("Couverture : maxDepth|clé=valeur attendu.");
        var evidenceIds = source.Coverage.Evidence.ToDictionary(item => (item.Kind, item.Name), item => item.Id);
        var evidence = coverageLines.Skip(1).Select((line, index) =>
        {
            var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || !Enum.TryParse<DialogueCoverageEvidenceKind>(parts[0], true, out var kind))
                throw new ArgumentException($"Couverture ligne {index + 2} : Test/Session|nom|nodes|état attendu.");
            return new DialogueCoverageEvidence(evidenceIds.GetValueOrDefault((kind, parts[1]), Guid.NewGuid()), 1,
                "author:r070", parts[1], kind, Csv(parts[2]).Select(DialogueNodeId.Parse).ToArray(),
                ParseMap(parts[3]));
        }).ToArray();
        var coverage = source.Coverage with
        {
            SchemaVersion = 1,
            Provenance = "author:r070",
            MaximumDepth = maximumDepth,
            RequiredStates = ParseMap(coverageHeader[1]),
            Evidence = evidence,
        };
        var revisions = new List<DialogueNodeRevision>();
        var locks = new List<DialogueNodeLock>();
        var comments = new List<DialogueNodeComment>();
        var conflicts = new List<DialogueSemanticConflict>();
        foreach (var (line, index) in ParseLines(_productionCollaboration!.Text).Select((value, i) => (value, i)))
        {
            var kind = line.Split('|', 2)[0].ToLowerInvariant();
            var existingIndex = kind switch
            {
                "revision" => revisions.Count,
                "lock" => locks.Count,
                "comment" => comments.Count,
                "conflict" => conflicts.Count,
                _ => -1,
            };
            switch (kind)
            {
                case "revision":
                    {
                        var parts = line.Split('|', 5, StringSplitOptions.TrimEntries);
                        if (parts.Length != 5 || !long.TryParse(parts[2], out var revision))
                            throw new ArgumentException($"Collaboration ligne {index + 1} : revision invalide.");
                        var old = existingIndex < source.Collaboration.Revisions.Count
                            ? source.Collaboration.Revisions[existingIndex] : null;
                        revisions.Add(new(old?.Id ?? Guid.NewGuid(), 1, "author:r070",
                            DialogueNodeId.Parse(parts[1]), revision, parts[3], parts[4], old?.At ?? DateTimeOffset.UtcNow));
                        break;
                    }
                case "lock":
                    {
                        var parts = line.Split('|', 4, StringSplitOptions.TrimEntries);
                        if (parts.Length != 4 || !DateTimeOffset.TryParse(parts[3], out var expires))
                            throw new ArgumentException($"Collaboration ligne {index + 1} : lock invalide.");
                        locks.Add(new(existingIndex < source.Collaboration.Locks.Count
                                ? source.Collaboration.Locks[existingIndex].Id : Guid.NewGuid(), 1, "author:r070",
                            DialogueNodeId.Parse(parts[1]), parts[2], expires));
                        break;
                    }
                case "comment":
                    {
                        var parts = line.Split('|', 5, StringSplitOptions.TrimEntries);
                        if (parts.Length != 5 || !bool.TryParse(parts[3], out var resolved))
                            throw new ArgumentException($"Collaboration ligne {index + 1} : commentaire invalide.");
                        var old = existingIndex < source.Collaboration.Comments.Count
                            ? source.Collaboration.Comments[existingIndex] : null;
                        comments.Add(new(old?.Id ?? Guid.NewGuid(), 1, "author:r070",
                            DialogueNodeId.Parse(parts[1]), parts[2], parts[4], resolved, old?.At ?? DateTimeOffset.UtcNow));
                        break;
                    }
                case "conflict":
                    {
                        var parts = line.Split('|', 9, StringSplitOptions.TrimEntries);
                        if (parts.Length != 9 || !Enum.TryParse<DialogueConflictResolution>(parts[6], true,
                                out var resolution) || !bool.TryParse(parts[8], out var resolved))
                            throw new ArgumentException($"Collaboration ligne {index + 1} : conflit invalide.");
                        conflicts.Add(new(existingIndex < source.Collaboration.Conflicts.Count
                                ? source.Collaboration.Conflicts[existingIndex].Id : Guid.NewGuid(), 1, "author:r070",
                            DialogueNodeId.Parse(parts[1]), parts[2], parts[3], parts[4], parts[5], resolution,
                            parts[7].Length == 0 ? null : parts[7], resolved));
                        break;
                    }
                default: throw new ArgumentException($"Collaboration ligne {index + 1} : type inconnu {kind}.");
            }
        }
        var collaboration = source.Collaboration with
        {
            SchemaVersion = 1,
            Provenance = "author:r070",
            Revisions = revisions,
            Locks = locks,
            Comments = comments,
            Conflicts = conflicts,
        };
        var aiPlans = ParseLines(_productionAiPlans!.Text).Select((line, index) =>
        {
            var parts = line.Split('|', 6, StringSplitOptions.TrimEntries);
            if (parts.Length != 6 || !Enum.TryParse<DialogueAiAuthorizationLevel>(parts[0], true,
                    out var authorization))
                throw new ArgumentException($"IA ligne {index + 1} : autorisation|synopsis|beats|style|citations|diff attendu.");
            var old = index < source.AiPlans.Count ? source.AiPlans[index] : null;
            return new DialogueAiPlan(old?.Id ?? Guid.NewGuid(), 1, "author:r070",
                old?.TransactionId ?? Guid.NewGuid(), old?.PlanId ?? Guid.NewGuid(), authorization, parts[1],
                Csv(parts[2]), Csv(parts[3]), Csv(parts[4]), parts[5], [], true, false);
        }).ToArray();
        _draftProduction = source with
        {
            SchemaVersion = 1,
            Provenance = "author:r070",
            Enabled = _productionEnabled!.ButtonPressed,
            RequiredAtRuntime = _productionRequired!.ButtonPressed,
            Subgraphs = contracts,
            Invocations = invocations,
            Consequences = consequences,
            Coverage = coverage,
            Collaboration = collaboration,
            AiPlans = aiPlans,
        };
    }

    private static IReadOnlyList<DialogueSubgraphParameter> ParseParameters(
        string value,
        DialogueSubgraphParameterDirection direction,
        IReadOnlyDictionary<(DialogueSubgraphParameterDirection Direction, string Name), Guid> ids,
        IDictionary<string, string> bindings) => Csv(value).Select((token, index) =>
    {
        var parts = token.Split(':', 4, StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !Enum.TryParse<DialogueTextTokenType>(parts[1], true, out var type) ||
            parts[2] is not ("required" or "optional") || string.IsNullOrWhiteSpace(parts[3]))
            throw new ArgumentException($"Paramètre {index + 1} : nom:type:required|optional:binding attendu.");
        bindings[parts[0]] = parts[3];
        return new DialogueSubgraphParameter(ids.GetValueOrDefault((direction, parts[0]), Guid.NewGuid()), 1,
            "author:r070", parts[0], type, direction, parts[2] == "required");
    }).ToArray();

    private static IReadOnlyDictionary<string, string> ParseMap(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in Csv(value))
        {
            var separator = token.IndexOf('=');
            if (separator < 1 || separator == token.Length - 1 ||
                !result.TryAdd(token[..separator].Trim(), token[(separator + 1)..].Trim()))
                throw new ArgumentException($"Paire clé=valeur invalide : {token}.");
        }
        return result;
    }

    private static string FormatMap(IReadOnlyDictionary<string, string>? values) => values is null
        ? string.Empty
        : string.Join(',', values.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"));

    private static string[] Csv(string value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void AddWizardCondition()
    {
        if (CurrentNode is null || _selectedChoiceIndex < 0 || _selectedChoiceIndex >= CurrentNode.Choices.Count)
        {
            SetStatus("Sélectionnez un choix avant d’ajouter une condition typée.", false, true);
            return;
        }
        try
        {
            SaveAuthoringFields(CurrentNode.Id);
            var built = DialogueScriptWizard.BuildCondition(
                (DialogueConditionKind)_wizardConditionKind!.GetItemId(_wizardConditionKind.Selected),
                _wizardConditionKey!.Text, _wizardConditionValue!.Text, false, "author:r068");
            var choices = CurrentNode.Choices.ToArray();
            var previous = choices[_selectedChoiceIndex];
            choices[_selectedChoiceIndex] = new DialogueChoice(previous.Id, previous.Text,
                [.. previous.Conditions, built.Value], previous.Actions, previous.TargetNodeId, previous.EdgeKind,
                previous.Priority, previous.ConditionCombination);
            ReplaceCurrentNode(new DialogueNode(CurrentNode.Id, CurrentNode.SpeakerBlueprintId, CurrentNode.Text,
                CurrentNode.OnEnterActions, choices, CurrentNode.Role, CurrentNode.ParticipantId,
                CurrentNode.SubgraphKey));
            AddWizardSource(CurrentNode.Id, built.Source);
            var selected = _selectedChoiceIndex;
            RefreshNodes(_selectedNodeIndex);
            SelectChoice(selected);
            SetStatus("Condition typée générée avec sa source visible.", true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private void AddWizardAction()
    {
        if (CurrentNode is null) return;
        try
        {
            SaveAuthoringFields(CurrentNode.Id);
            var built = DialogueScriptWizard.BuildAction(
                (DialogueActionKind)_wizardActionKind!.GetItemId(_wizardActionKind.Selected),
                _wizardActionKey!.Text, _wizardActionValue!.Text, true, "author:r068");
            ReplaceCurrentNode(new DialogueNode(CurrentNode.Id, CurrentNode.SpeakerBlueprintId, CurrentNode.Text,
                [.. CurrentNode.OnEnterActions, built.Value], CurrentNode.Choices, CurrentNode.Role,
                CurrentNode.ParticipantId, CurrentNode.SubgraphKey));
            AddWizardSource(CurrentNode.Id, built.Source);
            RefreshNodes(_selectedNodeIndex);
            SetStatus("Action typée générée avec sa source visible.", true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private void AddWizardSource(DialogueNodeId nodeId, DialogueWizardEntry source)
    {
        if (_draftAuthoring is null) throw new InvalidOperationException("Activez le profil auteur P1.");
        _draftAuthoring = _draftAuthoring with
        {
            Nodes = _draftAuthoring.Nodes.Select(item => item.NodeId == nodeId
                ? item with { WizardEntries = [.. item.WizardEntries, source] }
                : item).ToArray(),
        };
    }

    private static IEnumerable<string> ParseLines(string value) => (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private IReadOnlyList<DialogueParticipant> MergeParticipants()
    {
        var participants = _draftParticipants.ToList();
        foreach (var derived in _draftNodes.Select(node => DialogueParticipant.FromSpeaker(node.SpeakerBlueprintId)))
        {
            if (participants.Any(item => item.Id == derived.Id ||
                                         string.Equals(item.Role, derived.Role, StringComparison.Ordinal)))
                continue;
            participants.Add(derived);
        }
        return participants.OrderBy(item => item.Role, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<DialogueEntryPoint> NormalizeEntryPoints()
    {
        if (_entryNodeId is null) return [];
        if (_draftEntryPoints.Count == 0)
            return [new DialogueEntryPoint(
                Guid.NewGuid(),
                DialogueEntryPoint.CurrentSchemaVersion,
                "default",
                _entryNodeId,
                0,
                "author:dialogue-editor")];
        return _draftEntryPoints.Select(item => item.Key == "default"
                ? item with { NodeId = _entryNodeId }
                : item)
            .ToArray();
    }

    private static LocalizedText BuildLocalizedText(
        LocalizedText? previous,
        string key,
        string fallback,
        string variantLines)
    {
        var variants = DialogueAuthoringCodec.ParseVariants(key, variantLines, previous?.Variants);
        return previous is null
            ? new LocalizedText(key, fallback, variants, "author:dialogue-editor")
            : new LocalizedText(previous.Id, LocalizedText.CurrentSchemaVersion, "author:dialogue-editor",
                key, fallback, variants);
    }

    private void UndoDialogue()
    {
        _undo?.Invoke();
        Open();
        SetStatus("Dialogue restauré depuis l’historique projet.", true);
    }

    private void RedoDialogue()
    {
        _redo?.Invoke();
        Open();
        SetStatus("Dialogue réappliqué depuis l’historique projet.", true);
    }

    private void PrepareCaptureDialogue()
    {
        var speaker = _speakerIds.Skip(1).FirstOrDefault();
        var entry = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000101"));
        var lore = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000102"));
        var quest = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000103"));
        var end = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000104"));
        DialogueChoice Choice(string suffix, string text, DialogueNodeId target, DialogueEdgeKind kind, int priority,
            IReadOnlyList<DialogueCondition>? conditions = null,
            IReadOnlyList<DialogueAction>? actions = null,
            DialogueConditionCombination combination = DialogueConditionCombination.All) =>
            new(DialogueChoiceId.From(Guid.Parse($"d0050000-0000-4000-8000-000000000{suffix}")),
                new LocalizedText($"dialogue.capture.{suffix}", text,
                    [LocalizedTextVariant.Create($"dialogue.capture.{suffix}", "en", $"English: {text}")],
                    "capture:r006"),
                conditions, actions, target, kind, priority, combination);
        _draftDialogueId = DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000100"));
        _entryNodeId = entry;
        _draftPresentation = null;
        _draftSchemaVersion = DialogueDefinition.CurrentSchemaVersion;
        _draftProvenance = "capture:r006";
        _draftMetadata = new Dictionary<string, string> { ["kind"] = "conversation", ["proof"] = "r006" };
        _draftParticipants = [];
        _draftEntryPoints = [new DialogueEntryPoint(
            Guid.Parse("d0050000-0000-4000-8000-000000000110"), 1, "default", entry, 0, "capture:r006")];
        _draftAuthoring = null;
        _draftExperience = null;
        _draftProduction = null;
        _dialogueName!.Text = "Le pacte des Cendres";
        _dialogueSlug!.Text = "pacte-des-cendres";
        _draftNodes.Clear();
        _draftNodes.AddRange(
        [
            new(entry, speaker, new LocalizedText("dialogue.capture.entry", "Que souhaitez-vous demander à Elyra ?",
                    [LocalizedTextVariant.Create("dialogue.capture.entry", "en", "What do you want to ask Elyra?")],
                    "capture:r006"),
                [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.greeting", "play")], choices:
                [Choice("105", "Parlez-moi de la crypte.", lore, DialogueEdgeKind.Flow, 20,
                    [new DialogueCondition(DialogueConditionKind.HasItem, "ash-key", "true")],
                    [new DialogueAction(DialogueActionKind.SetRelation, "elyra", "trusted")]),
                 Choice("106", "Rappelez-moi notre mission.", quest, DialogueEdgeKind.Flow, 10,
                    [new DialogueCondition(DialogueConditionKind.QuestStateEquals, "crypt-ashes", "inactive"),
                     new DialogueCondition(DialogueConditionKind.EventRaised, "warden-fallen", "true", negated: true)],
                    [new DialogueAction(DialogueActionKind.StartQuest, "crypt-ashes", "active")],
                    DialogueConditionCombination.Any),
                 Choice("107", "Revenir à la légende.", lore, DialogueEdgeKind.Reference, 5)],
                role: DialogueNodeRole.PlayerChoice),
            new(lore, speaker, new("dialogue.capture.lore", "La braise garde la mémoire des anciens."),
                choices: [Choice("108", "Continuer", end, DialogueEdgeKind.Flow, 10)],
                role: DialogueNodeRole.SpeakerLine, subgraphKey: "lore"),
            new(quest, null, new("dialogue.capture.quest", "Objectif mis à jour : ouvrir le sanctuaire."),
                choices: [Choice("109", "Terminer", end, DialogueEdgeKind.Flow, 10)], role: DialogueNodeRole.System,
                subgraphKey: "quest"),
            new(end, null, new("dialogue.capture.end", "Fin de la conversation."), role: DialogueNodeRole.End),
        ]);
        _nodePage = 0;
        RefreshNodes(0);
        RunSimulation();
        var captureGraph = CreateDraftDefinition();
        GD.Print("OPENRPG_FORGE_DIALOGUE_R006_CAPTURE_STATE " +
                 $"nodes={captureGraph.Nodes.Count} edges={captureGraph.Edges.Count} " +
                 $"targets={string.Join(',', captureGraph.Edges.Select(item => item.TargetNodeId))} " +
                 $"diagnostics={string.Join(',', DialogueValidator.Validate(captureGraph).Select(item => item.Code))}");
        SetStatus("R006 · variantes · ALL/ANY/NOT · actions transactionnelles · simulation et linter", true);
    }

    private void PrepareR068Capture()
    {
        var current = CurrentNode ?? throw new InvalidOperationException("R068 capture requires an entry node.");
        var text = new LocalizedText(current.Text.Id, LocalizedText.CurrentSchemaVersion, "capture:r068",
            current.Text.Key, "Bienvenue, {{player.name}}. Choisissez une réponse.", current.Text.Variants);
        ReplaceCurrentNode(new DialogueNode(current.Id, current.SpeakerBlueprintId, text,
            current.OnEnterActions, current.Choices, current.Role, current.ParticipantId, current.SubgraphKey));
        _draftMetadata = new Dictionary<string, string>(_draftMetadata, StringComparer.Ordinal)
        {
            ["proof"] = "r068",
            ["authoring.required"] = "true",
        };
        var dialogue = CreateBaseDraftDefinition();
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, "capture:r068");
        var entry = profile.Nodes.Single(item => item.NodeId == dialogue.EntryNodeId);
        var canonicalEntry = dialogue.Nodes.Single(item => item.Id == dialogue.EntryNodeId);
        var wizard = new List<DialogueWizardEntry>();
        if (canonicalEntry.OnEnterActions.FirstOrDefault() is { } action)
            wizard.Add(new DialogueWizardEntry(Guid.Parse("d0685000-0000-4000-8000-000000000001"), 1,
                "capture:r068", DialogueWizardEntryKind.Action, action.Id,
                $"builder:action:{action.Kind}:{action.Key}:{action.Value}:true"));
        if (canonicalEntry.Choices.SelectMany(item => item.Conditions).FirstOrDefault() is { } condition)
            wizard.Add(new DialogueWizardEntry(Guid.Parse("d0685000-0000-4000-8000-000000000002"), 1,
                "capture:r068", DialogueWizardEntryKind.Condition, condition.Id,
                $"builder:condition:{condition.Kind}:{condition.Key}:{condition.ExpectedValue}:false"));
        profile = profile with
        {
            RequiredAtRuntime = true,
            Participants = profile.Participants.Select(item => item.Role == entry.SpeakerRole
                ? item with { Kind = DialogueParticipantBindingKind.Tag, Value = "elyra" }
                : item).ToArray(),
            Nodes = profile.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                ? item with
                {
                    EvaluationMode = DialogueChoiceEvaluationMode.Weighted,
                    Tokens = [new DialogueTextToken(Guid.Parse("d0685000-0000-4000-8000-000000000003"), 1,
                        "capture:r068", "player.name", DialogueTextTokenType.EntityName, true, "Héroïne")],
                    ChoiceEvaluations = item.ChoiceEvaluations.Select((value, index) =>
                        value with { Weight = index + 1, Provenance = "capture:r068" }).ToArray(),
                    WizardEntries = wizard,
                    Notes = item.Notes with
                    {
                        Provenance = "capture:r068",
                        Intent = "Présenter trois embranchements lisibles.",
                        Emotion = "mysterious",
                        Source = "brief:crypt-of-ashes",
                        Status = DialogueNodeEditorialStatus.Review,
                        Todos = ["Valider la voix", "Relire la lore"],
                        AuthorMention = "@karen",
                    },
                }
                : item).ToArray(),
        };
        _draftAuthoring = profile;
        _wizardConditionKey!.Text = "ash-key";
        _wizardConditionValue!.Text = "true";
        _wizardActionKey!.Text = "crypt-ashes";
        _wizardActionValue!.Text = "active";
        RefreshNodes(_selectedNodeIndex);
        _detailScroll!.ScrollVertical = 430;
        RefreshValidation();
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        GD.Print("OPENRPG_FORGE_DIALOGUE_R068_CAPTURE_STATE " +
                 $"tokens={profile.Nodes.Sum(item => item.Tokens.Count)} participants={profile.Participants.Count} " +
                 $"mode={profile.Nodes.Single(item => item.NodeId == dialogue.EntryNodeId).EvaluationMode} " +
                 $"wizard={wizard.Count} notes={profile.Nodes.Count} diagnostics={string.Join(',', diagnostics.Select(item => item.Code))}");
        SetStatus("R068 · tokens typés · participants · cinq modes · wizard sourcé · notes éditoriales", true);
    }

    private void PrepareR069Capture()
    {
        var dialogue = CreateBaseDraftDefinition();
        var success = dialogue.Nodes[1].Id;
        var failure = dialogue.Nodes[2].Id;
        _draftMetadata = new Dictionary<string, string>(_draftMetadata, StringComparer.Ordinal)
        {
            ["proof"] = "r069",
            ["experience.required"] = "true",
        };
        dialogue = CreateBaseDraftDefinition();
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "capture:r069");
        profile = profile with
        {
            RequiredAtRuntime = true,
            Lifecycle = profile.Lifecycle with
            {
                Provenance = "capture:r069",
                CompletedActions = [new(DialogueActionKind.SetVariable, "dialogue.completed", "true")],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
                CreateResumeState = true,
            },
            Search = profile.Search with
            {
                Provenance = "capture:r069",
                Pattern = "crypte|braise",
                UseRegex = true,
                SpeakerRoles = [],
                ConditionKinds = [DialogueConditionKind.HasItem],
                ActionKinds = [DialogueActionKind.StartQuest],
                ReplacementPreview = "flamme",
            },
            Review = profile.Review with { Provenance = "capture:r069", ReviewMode = true },
            MemorySlots =
            [
                new(Guid.Parse("d0695000-0000-4000-8000-000000000011"), 1, "capture:r069",
                    DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                    DialogueMemoryDuration.Campaign, "0"),
                new(Guid.Parse("d0695000-0000-4000-8000-000000000012"), 1, "capture:r069",
                    DialogueMemoryScope.World, "world:crypt", "ashes-awakened", DialogueTextTokenType.Boolean,
                    DialogueMemoryDuration.Permanent, "false"),
            ],
            SkillChecks =
            [new(Guid.Parse("d0695000-0000-4000-8000-000000000013"), 1, "capture:r069",
                dialogue.EntryNodeId, "persuasion", 12, "elyra", true,
                DialogueSkillCheckVisibility.FullExplanation, success, failure)],
        };
        _draftExperience = profile;
        RefreshNodes(_selectedNodeIndex);
        _detailScroll!.ScrollVertical = 1050;
        RefreshValidation();
        var preview = DialogueExperienceRuntimeService.PreviewReplace([CreateDraftDefinition()], profile.Search);
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        GD.Print("OPENRPG_FORGE_DIALOGUE_R069_CAPTURE_STATE " +
                 $"completed={profile.Lifecycle.CompletedActions.Count} aborted={profile.Lifecycle.AbortedActions.Count} " +
                 $"search={preview.Hits.Count} badges={profile.Review.Badges.Count} memory={profile.MemorySlots.Count} " +
                 $"checks={profile.SkillChecks.Count} diagnostics={string.Join(',', diagnostics.Select(item => item.Code))}");
        SetStatus("R069 · fin/abandon · recherche preview · 6 badges · mémoire typée · check déterministe", true);
    }

    private void PrepareR070Capture()
    {
        var subgraphIndex = _draftNodes.FindIndex(item => item.SubgraphKey == "lore");
        if (subgraphIndex < 0) subgraphIndex = Math.Min(1, _draftNodes.Count - 1);
        var node = _draftNodes[subgraphIndex];
        _draftNodes[subgraphIndex] = new DialogueNode(node.Id, node.SpeakerBlueprintId, node.Text,
            node.OnEnterActions, node.Choices, DialogueNodeRole.Subgraph, node.ParticipantId, "embers");
        _draftMetadata = new Dictionary<string, string>(_draftMetadata, StringComparer.Ordinal)
        {
            ["proof"] = "r070",
            ["production.required"] = "true",
        };
        var dialogue = CreateBaseDraftDefinition();
        _draftProduction = CreateR070ProductionProfile(dialogue, "capture:r070");
        RefreshNodes(_selectedNodeIndex);
        _detailScroll!.ScrollVertical = 1580;
        RefreshValidation();
        var report = DialogueProductionRuntimeService.MeasureCoverage(CreateDraftDefinition());
        var diagnostics = DialogueValidator.Validate(CreateDraftDefinition());
        GD.Print("OPENRPG_FORGE_DIALOGUE_R070_CAPTURE_STATE " +
                 $"subgraphs={_draftProduction.Subgraphs.Count} tests={_draftProduction.Subgraphs.Sum(item => item.Tests.Count)} " +
                 $"consequences={_draftProduction.Consequences.Count} paths={report.Paths.Count} " +
                 $"covered={report.CoveredNodes.Count} impossible={report.ImpossiblePaths.Count} " +
                 $"revisions={_draftProduction.Collaboration.Revisions.Count} locks={_draftProduction.Collaboration.Locks.Count} " +
                 $"comments={_draftProduction.Collaboration.Comments.Count} conflicts={_draftProduction.Collaboration.Conflicts.Count} " +
                 $"ai={_draftProduction.AiPlans.Count} diagnostics={string.Join(',', diagnostics.Select(item => item.Code))}");
        SetStatus("R070 · sous-dialogue testé · impact typé · couverture · collaboration · IA en preview", true);
    }

    private static DialogueProductionProfile CreateR070ProductionProfile(
        DialogueDefinition dialogue,
        string provenance)
    {
        var subgraph = dialogue.Nodes.FirstOrDefault(item => item.Role == DialogueNodeRole.Subgraph &&
            !string.IsNullOrWhiteSpace(item.SubgraphKey))
            ?? throw new InvalidOperationException("R070 requires one visible subgraph node.");
        var contractId = Guid.Parse("d0705000-0000-4000-8000-000000000001");
        var input = new DialogueSubgraphParameter(Guid.Parse("d0705000-0000-4000-8000-000000000002"), 1,
            provenance, "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var output = new DialogueSubgraphParameter(Guid.Parse("d0705000-0000-4000-8000-000000000003"), 1,
            provenance, "accepted", DialogueTextTokenType.Boolean, DialogueSubgraphParameterDirection.Output, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0705000-0000-4000-8000-000000000004"), 1,
            provenance, "trust accepted", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string> { ["accepted"] = "true" });
        var contract = new DialogueSubgraphContract(contractId, 1, provenance, subgraph.SubgraphKey!, 2,
            [input, output], ["party:trust"], [test]);
        var invocation = new DialogueSubgraphInvocation(Guid.Parse("d0705000-0000-4000-8000-000000000005"),
            1, provenance, subgraph.Id, contractId,
            new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string> { ["accepted"] = "memory:accepted" });
        var consequences = Enum.GetValues<DialogueConsequenceTargetKind>().Select((kind, index) =>
            new DialogueTypedConsequence(Guid.Parse($"d0705000-0000-4000-8000-0000000001{index:00}"), 1,
                provenance, dialogue.EntryNodeId, kind, kind switch
                {
                    DialogueConsequenceTargetKind.Quest => "quest:crypt-of-ashes",
                    DialogueConsequenceTargetKind.Faction => "faction:ash-wardens",
                    DialogueConsequenceTargetKind.Item => "item:ember-key",
                    _ => "world:crypt-awakened",
                }, kind == DialogueConsequenceTargetKind.Faction ? "reputation" : "set", "+1",
                $"Impact {kind} visible dans le graphe.")).ToArray();
        var evidence = new DialogueCoverageEvidence(Guid.Parse("d0705000-0000-4000-8000-000000000201"), 1,
            provenance, "test:embers", DialogueCoverageEvidenceKind.Test,
            [dialogue.EntryNodeId, subgraph.Id], new Dictionary<string, string>
            {
                ["quest:crypt-of-ashes"] = "active",
            });
        var coverage = new DialogueCoveragePolicy(Guid.Parse("d0705000-0000-4000-8000-000000000202"), 1,
            provenance, 64, new Dictionary<string, string> { ["quest:crypt-of-ashes"] = "active" },
            [evidence]);
        var now = DateTimeOffset.UnixEpoch;
        var revision = new DialogueNodeRevision(Guid.Parse("d0705000-0000-4000-8000-000000000301"), 1,
            provenance, subgraph.Id, 2, "author", "{\"text\":\"ours\"}", now);
        var nodeLock = new DialogueNodeLock(Guid.Parse("d0705000-0000-4000-8000-000000000302"), 1,
            provenance, subgraph.Id, "author", now.AddMinutes(5));
        var comment = new DialogueNodeComment(Guid.Parse("d0705000-0000-4000-8000-000000000303"), 1,
            provenance, subgraph.Id, "reviewer", "Clarifier l'impact de la promesse.", false, now);
        var conflict = new DialogueSemanticConflict(Guid.Parse("d0705000-0000-4000-8000-000000000304"), 1,
            provenance, subgraph.Id, "text", "{\"text\":\"base\"}", "{\"text\":\"ours\"}",
            "{\"text\":\"theirs\"}", DialogueConflictResolution.Manual, "{\"text\":\"merged\"}", true);
        var collaboration = new DialogueCollaborationProfile(
            Guid.Parse("d0705000-0000-4000-8000-000000000305"), 1, provenance, [revision], [nodeLock],
            [comment], [conflict]);
        var ai = new DialogueAiPlan(Guid.Parse("d0705000-0000-4000-8000-000000000401"), 1, provenance,
            Guid.Parse("d0705000-0000-4000-8000-000000000402"),
            Guid.Parse("d0705000-0000-4000-8000-000000000403"), DialogueAiAuthorizationLevel.AuthorApply,
            "Une promesse sous la crypte.", ["offre", "choix"], ["style:embers"],
            ["PROJECT_CONTEXT.md"], "add-subgraph:embers|nodes:1", [], true, false);
        return new DialogueProductionProfile(Guid.Parse("d0705000-0000-4000-8000-000000000010"), 1,
            provenance, true, true, [contract], [invocation], consequences, coverage, collaboration, [ai]);
    }

    private string CaptureMarker() => _captureR070
        ? "R070_DIALOGUE_PRODUCTION"
        : _captureR069
            ? "R069_DIALOGUE_EXPERIENCE"
            : _captureR068
                ? "R068_DIALOGUE_AUTHORING"
                : "DIALOGUE_GRAPH";

    private void ReplaceCurrentNode(DialogueNode node) => _draftNodes[_selectedNodeIndex] = node;

    private DialogueNode? CurrentNode =>
        _selectedNodeIndex >= 0 && _selectedNodeIndex < _draftNodes.Count
            ? _draftNodes[_selectedNodeIndex]
            : null;

    private void OpenPresentation()
    {
        if (_draftDialogueId is null || _getState?.Invoke()?.Project.Dialogues.All(item => item.Id != _draftDialogueId) != false)
        {
            SetStatus("Appliquez d’abord le dialogue avant de configurer sa mise en scène.", false, true);
            return;
        }
        _openPresentation?.Invoke(_draftDialogueId);
    }

    private void SetStatus(string message, bool success, bool error = false)
    {
        _status!.Text = message;
        _status.AddThemeColorOverride(
            "font_color",
            error ? new Color("ff8c8c") : success ? new Color("83d6ae") : new Color("ab9acb"));
    }

    private static LineEdit AddAuthoringField(GridContainer grid, string label, string placeholder)
    {
        grid.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddChild(field);
        return field;
    }

    private void Close()
    {
        Hide();
        Exclusive = false;
    }
}
#endif
