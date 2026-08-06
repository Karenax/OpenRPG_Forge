#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Godot;

namespace Forge.Toolset.Editor;

public partial class DialogueEditorWindow : Window
{
    private readonly List<DialogueId> _dialogueIds = [];
    private readonly List<BlueprintId?> _speakerIds = [];
    private readonly List<DialogueNode> _draftNodes = [];

    private Func<ProjectState?>? _getState;
    private Func<DialogueDefinition, bool>? _applyDialogue;
    private Action<Window>? _brandWindow;
    private Action<DialogueId>? _openPresentation;
    private DialogueId? _draftDialogueId;
    private DialoguePresentationProfile? _draftPresentation;
    private DialogueNodeId? _entryNodeId;
    private int _selectedNodeIndex = -1;
    private int _selectedChoiceIndex = -1;

    private OptionButton? _dialoguePicker;
    private LineEdit? _dialogueName;
    private LineEdit? _dialogueSlug;
    private ItemList? _nodeList;
    private OptionButton? _speakerPicker;
    private LineEdit? _nodeTextKey;
    private TextEdit? _nodeText;
    private ItemList? _choiceList;
    private LineEdit? _choiceTextKey;
    private LineEdit? _choiceText;
    private OptionButton? _choiceTarget;
    private OptionButton? _conditionKind;
    private LineEdit? _conditionKey;
    private LineEdit? _conditionValue;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _removeChoiceButton;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<DialogueDefinition, bool> applyDialogue,
        Action<DialogueId> openPresentation,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(applyDialogue);
        ArgumentNullException.ThrowIfNull(brandWindow);
        ArgumentNullException.ThrowIfNull(openPresentation);
        _getState = getState;
        _applyDialogue = applyDialogue;
        _openPresentation = openPresentation;
        _brandWindow = brandWindow;
        Title = "Dialogues · OpenRPG Forge";
        MinSize = new Vector2I(940, 790);
        Size = new Vector2I(940, 790);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
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
        PopupCentered(new Vector2I(940, 790));
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
        var addNodeButton = new Button
        {
            CustomMinimumSize = new Vector2(0, 38),
            Text = "+ Ajouter un nœud",
        };
        nodeColumn.AddChild(addNodeButton);
        addNodeButton.Pressed += AddNode;

        var detailColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        detailColumn.AddThemeConstantOverride("separation", 6);
        editor.AddChild(detailColumn);
        detailColumn.AddChild(new Label { Text = "RÉPLIQUE SÉLECTIONNÉE" });
        var nodeMetadata = new HBoxContainer();
        nodeMetadata.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(nodeMetadata);
        _speakerPicker = new OptionButton
        {
            CustomMinimumSize = new Vector2(220, 36),
        };
        nodeMetadata.AddChild(_speakerPicker);
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
        var saveNodeButton = new Button { Text = "Enregistrer la réplique" };
        detailColumn.AddChild(saveNodeButton);
        saveNodeButton.Pressed += SaveSelectedNodeFromUi;

        detailColumn.AddChild(new Label { Text = "CHOIX DU JOUEUR" });
        _choiceList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 76),
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
        var choiceRouteRow = new HBoxContainer();
        choiceRouteRow.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(choiceRouteRow);
        _choiceTarget = new OptionButton
        {
            CustomMinimumSize = new Vector2(220, 36),
        };
        choiceRouteRow.AddChild(_choiceTarget);
        _conditionKind = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, 36),
        };
        _conditionKind.AddItem("Sans condition");
        _conditionKind.AddItem("Variable égale");
        _conditionKind.AddItem("Possède l’objet");
        _conditionKind.AddItem("État de quête");
        choiceRouteRow.AddChild(_conditionKind);
        _conditionKey = new LineEdit
        {
            PlaceholderText = "clé",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        choiceRouteRow.AddChild(_conditionKey);
        _conditionValue = new LineEdit
        {
            CustomMinimumSize = new Vector2(110, 36),
            PlaceholderText = "valeur",
        };
        choiceRouteRow.AddChild(_conditionValue);
        var choiceActions = new HBoxContainer();
        choiceActions.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(choiceActions);
        var saveChoiceButton = new Button
        {
            Text = "Ajouter ou remplacer le choix",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        choiceActions.AddChild(saveChoiceButton);
        saveChoiceButton.Pressed += SaveChoice;
        _removeChoiceButton = new Button
        {
            Disabled = true,
            Text = "Retirer",
        };
        choiceActions.AddChild(_removeChoiceButton);
        _removeChoiceButton.Pressed += RemoveChoice;

        detailColumn.AddChild(new Label { Text = "APERÇU" });
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 84),
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        detailColumn.AddChild(_preview);

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
        _entryNodeId = dialogue.EntryNodeId;
        _dialogueName!.Text = dialogue.DisplayName;
        _dialogueSlug!.Text = dialogue.Slug;
        _draftNodes.Clear();
        _draftNodes.AddRange(dialogue.Nodes);
        RefreshNodes(0);
        SetStatus($"Dialogue chargé · {_draftNodes.Count} nœud(s).", false);
    }

    private void CreateNewDialogue()
    {
        _draftDialogueId = DialogueId.From(Guid.NewGuid());
        _draftPresentation = null;
        _dialogueName!.Text = "Nouveau dialogue";
        _dialogueSlug!.Text = $"dialogue-{_dialogueIds.Count + 1}";
        _draftNodes.Clear();
        var nodeId = DialogueNodeId.From(Guid.NewGuid());
        _entryNodeId = nodeId;
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
        if (index < 0 || index >= _draftNodes.Count)
        {
            return;
        }

        _selectedNodeIndex = (int)index;
        _selectedChoiceIndex = -1;
        var node = _draftNodes[_selectedNodeIndex];
        var speakerIndex = _speakerIds.FindIndex(id => id == node.SpeakerBlueprintId);
        _speakerPicker!.Selected = Math.Max(0, speakerIndex);
        _nodeTextKey!.Text = node.Text.Key;
        _nodeText!.Text = node.Text.Fallback;
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
                new LocalizedText(_nodeTextKey!.Text, _nodeText!.Text),
                previous.OnEnterActions,
                previous.Choices);
            RefreshNodes(_selectedNodeIndex);
            SetStatus("Réplique enregistrée dans le brouillon.", true);
            return true;
        }
        catch (ArgumentException exception)
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
        _choiceTarget!.Selected = choice.TargetNodeId is null
            ? 0
            : _draftNodes.FindIndex(node => node.Id == choice.TargetNodeId) + 1;
        var condition = choice.Conditions.FirstOrDefault();
        _conditionKind!.Selected = condition is null ? 0 : (int)condition.Kind + 1;
        _conditionKey!.Text = condition?.Key ?? string.Empty;
        _conditionValue!.Text = condition?.ExpectedValue ?? string.Empty;
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
            IReadOnlyList<DialogueCondition> conditions = _conditionKind!.Selected == 0
                ? []
                : [new DialogueCondition(
                    (DialogueConditionKind)(_conditionKind.Selected - 1),
                    _conditionKey!.Text,
                    _conditionKind.Selected == 2 && string.IsNullOrWhiteSpace(_conditionValue!.Text)
                        ? "true"
                        : _conditionValue!.Text)];
            var targetIndex = _choiceTarget!.Selected - 1;
            var target = targetIndex >= 0 && targetIndex < _draftNodes.Count
                ? _draftNodes[targetIndex].Id
                : null;
            var choice = new DialogueChoice(
                existing?.Id ?? DialogueChoiceId.From(Guid.NewGuid()),
                new LocalizedText(_choiceTextKey!.Text, _choiceText!.Text),
                conditions,
                existing?.Actions ?? [],
                target);
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
                choices));
            RefreshChoices(_selectedChoiceIndex);
            RefreshPreview();
            SetStatus("Choix enregistré dans le brouillon.", true);
        }
        catch (ArgumentException exception)
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
            choices));
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
            var dialogue = new DialogueDefinition(
                _draftDialogueId,
                _dialogueSlug!.Text,
                _dialogueName!.Text,
                _entryNodeId,
                _draftNodes,
                _draftPresentation);
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
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private void RefreshNodes(int selectedIndex)
    {
        _nodeList!.Clear();
        foreach (var node in _draftNodes)
        {
            var prefix = node.Id == _entryNodeId ? "◆" : "◇";
            _nodeList.AddItem($"{prefix} {node.Text.Fallback}");
        }

        if (_draftNodes.Count == 0)
        {
            _selectedNodeIndex = -1;
            return;
        }

        _selectedNodeIndex = Math.Clamp(selectedIndex, 0, _draftNodes.Count - 1);
        _nodeList.Select(_selectedNodeIndex);
        SelectNode(_selectedNodeIndex);
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
            var conditionBadge = choice.Conditions.Count > 0 ? " · condition" : string.Empty;
            _choiceList.AddItem($"{choice.Text.Fallback}{conditionBadge}");
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
            _conditionKind!.Selected = 0;
            _conditionKey!.Clear();
            _conditionValue!.Clear();
            _choiceTarget!.Selected = 0;
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
            var condition = choice.Conditions.FirstOrDefault();
            var suffix = condition is null ? string.Empty : $"  [color=#ab9acb]si {condition.Key}[/color]";
            lines.Add($"› {choice.Text.Fallback}{suffix}");
        }

        if (CurrentNode.Choices.Count == 0)
        {
            lines.Add("[color=#ab9acb]Fin de branche · ajoutez un choix pour continuer.[/color]");
        }

        _preview.Text = string.Join("\n", lines);
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
