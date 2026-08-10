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

    public void Initialize(
        Func<ProjectState?> getState,
        Func<DialogueDefinition, bool> applyDialogue,
        Action<DialogueId> openPresentation,
        Action undo,
        Action redo,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(applyDialogue);
        ArgumentNullException.ThrowIfNull(brandWindow);
        ArgumentNullException.ThrowIfNull(openPresentation);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);
        _getState = getState;
        _applyDialogue = applyDialogue;
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
            value.StartsWith("--dialogue-graph-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _capturePath = captureArgument["--dialogue-graph-ui-capture=".Length..];
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
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_DIALOGUE_GRAPH_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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

        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(620, 0),
        };
        editor.AddChild(detailScroll);
        var detailColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        detailColumn.AddThemeConstantOverride("separation", 6);
        detailScroll.AddChild(detailColumn);
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
        detailColumn.AddChild(new Label { Text = "VARIANTES · locale|genre|pluriel|texte" });
        _nodeVariants = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "fr|any|Other|Texte français\nen|any|One|English singular",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_nodeVariants);
        detailColumn.AddChild(new Label { Text = "ACTIONS À L’ENTRÉE · type|clé|valeur|required/optional" });
        _nodeActions = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "PlayAudio|elyra.greeting|play|required",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_nodeActions);
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
        _choiceActionLines = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 54),
            PlaceholderText = "StartQuest|crypt-ashes|active|required",
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        detailColumn.AddChild(_choiceActionLines);
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
    }

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
        _entryNodeId = dialogue.EntryNodeId;
        _dialogueName!.Text = dialogue.DisplayName;
        _dialogueSlug!.Text = dialogue.Slug;
        _draftNodes.Clear();
        _draftNodes.AddRange(dialogue.Nodes);
        _nodePage = 0;
        RefreshNodes(0);
        SetStatus($"Dialogue chargé · {_draftNodes.Count} nœud(s).", false);
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

    private IReadOnlyList<DialogueParticipant> MergeParticipants() => _draftParticipants
        .Concat(_draftNodes.Select(node => DialogueParticipant.FromSpeaker(node.SpeakerBlueprintId)))
        .DistinctBy(item => item.Id)
        .OrderBy(item => item.Role, StringComparer.Ordinal)
        .ToArray();

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

    private void Close()
    {
        Hide();
        Exclusive = false;
    }
}
#endif
