using System.Globalization;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.ModuleFormat.Serialization;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDialogueChoiceListUx5A : ItemList
{
    private const string PayloadKind = "openrpg-forge.studio-dialogue-choice-ux5a";

    internal event Action<int, int>? ChoiceMoved;

    internal bool KeyboardAlternativeEnabled => true;

    internal bool ExerciseKeyboardMoveForEvidence(int sourceIndex, Key keycode)
    {
        if (sourceIndex < 0 || sourceIndex >= ItemCount || keycode is not (Key.Up or Key.Down))
        {
            return false;
        }

        var observed = false;
        void Observe(int source, int target) =>
            observed |= source == sourceIndex && target == sourceIndex + (keycode == Key.Up ? -1 : 1);
        ChoiceMoved += Observe;
        try
        {
            Select(sourceIndex);
            _GuiInput(new InputEventKey
            {
                Pressed = true,
                Echo = false,
                AltPressed = true,
                Keycode = keycode,
            });
            return observed;
        }
        finally
        {
            ChoiceMoved -= Observe;
        }
    }

    internal bool ExerciseDragDropMoveForEvidence(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= ItemCount || targetIndex < 0 || targetIndex >= ItemCount ||
            sourceIndex == targetIndex)
        {
            return false;
        }

        var observed = false;
        void Observe(int source, int target) =>
            observed |= source == sourceIndex && target == targetIndex;
        ChoiceMoved += Observe;
        try
        {
            Select(sourceIndex);
            Variant payload = CreateChoicePayload(sourceIndex);
            var targetRect = GetItemRect(targetIndex);
            var targetPosition = targetRect.Position + targetRect.Size / 2f;
            if (!_CanDropData(targetPosition, payload))
            {
                return false;
            }
            _DropData(targetPosition, payload);
            return observed;
        }
        finally
        {
            ChoiceMoved -= Observe;
        }
    }

    internal StudioDialogueChoiceListUx5A()
    {
        SelectMode = SelectModeEnum.Single;
        SameColumnWidth = true;
        MaxColumns = 1;
        AllowRmbSelect = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.All;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var index = GetItemAtPosition(atPosition, true);
        if (index < 0)
        {
            return default;
        }

        Select(index);
        var preview = StudioStyle.Label($"  Déplacer {GetItemText(index)}  ", 11, StudioStyle.Text);
        preview.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(StudioStyle.Selection, StudioStyle.Success, 6, 1));
        SetDragPreview(preview);
        return CreateChoicePayload(index);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        TryReadChoice(data, out _) && GetItemAtPosition(atPosition, true) >= 0;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadChoice(data, out var sourceIndex))
        {
            return;
        }
        var targetIndex = GetItemAtPosition(atPosition, true);
        if (targetIndex >= 0 && targetIndex != sourceIndex)
        {
            ChoiceMoved?.Invoke(sourceIndex, targetIndex);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, AltPressed: true } key)
        {
            return;
        }
        var selected = GetSelectedItems();
        if (selected.Length != 1)
        {
            return;
        }
        var sourceIndex = selected[0];
        var targetIndex = key.Keycode switch
        {
            Key.Up => sourceIndex - 1,
            Key.Down => sourceIndex + 1,
            _ => sourceIndex,
        };
        if (targetIndex < 0 || targetIndex >= ItemCount || targetIndex == sourceIndex)
        {
            return;
        }
        ChoiceMoved?.Invoke(sourceIndex, targetIndex);
        AcceptEvent();
    }

    private static bool TryReadChoice(Variant data, out int sourceIndex)
    {
        sourceIndex = -1;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") || !dictionary.ContainsKey("sourceIndex") ||
            !string.Equals(dictionary["kind"].AsString(), PayloadKind, StringComparison.Ordinal))
        {
            return false;
        }
        sourceIndex = dictionary["sourceIndex"].AsInt32();
        return sourceIndex >= 0;
    }

    private static Godot.Collections.Dictionary CreateChoicePayload(int sourceIndex) => new()
    {
        ["kind"] = PayloadKind,
        ["sourceIndex"] = sourceIndex,
    };
}

internal sealed partial class StudioDialogueFoundationPage : Control
{
    private const int LinePageSize = 25;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private readonly OptionButton _dialoguePicker;
    private readonly Label _inventory;
    private readonly LineEdit _displayName;
    private readonly LineEdit _slug;
    private readonly StudioDialogueChoiceListUx5A _lines;
    private readonly Label _linePage;
    private readonly Button _previousPage;
    private readonly Button _nextPage;
    private readonly Button _addLine;
    private readonly Button _removeLine;
    private readonly Button _setEntry;
    private readonly OptionButton _role;
    private readonly OptionButton _speaker;
    private readonly TextEdit _lineText;
    private readonly Label _lineContext;
    private readonly StudioDialogueChoiceListUx5A _choices;
    private readonly TextEdit _choiceText;
    private readonly OptionButton _choiceTarget;
    private readonly OptionButton _edgeKind;
    private readonly LineEdit _priority;
    private readonly Button _addChoice;
    private readonly Button _removeChoice;
    private readonly Button _moveChoiceUp;
    private readonly Button _moveChoiceDown;
    private readonly TextEdit _miniMap;
    private readonly Label _previewSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Label _status;
    private readonly List<DialogueId> _dialogueIds = [];
    private readonly List<BlueprintId?> _speakerIds = [];
    private readonly List<bool> _speakerProtected = [];
    private readonly List<DialogueNodeRole> _roles = [];
    private readonly List<DialogueNodeId?> _targetNodeIds = [];
    private readonly List<int> _visibleLineIndexes = [];
    private StudioDialogueDraftUx5A? _draft;
    private StudioDialoguePreviewUx5A? _lastPreview;
    private StudioDialogueEvidenceWorkspaceUx5A? _evidenceWorkspace;
    private StudioDialogueEvidenceSourceSnapshotUx5A? _evidenceSourceSnapshot;
    private int _linePageIndex;
    private int _selectedLineIndex = -1;
    private int _selectedChoiceIndex = -1;
    private bool _loading;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _emptyStateVerified;
    private bool _lastKeyboardReorderExercised;
    private bool _lastDragDropReorderExercised;
    private bool _lastProtectedSpeakerPreserved;
    private bool _lastSubgraphProtected;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? DocumentChanged;

    internal bool IsOpen => Visible;

    internal bool HasExplicitSelection => _dialoguePicker.Selected > 0 && _draft is not null;

    internal bool PreviewReady => _lastPreview is { Succeeded: true, CanApply: true };

    internal bool MiniMapReadOnly => !_miniMap.Editable;

    internal bool KeyboardReorderAvailable => _choices.KeyboardAlternativeEnabled;

    internal bool KeyboardReorderExercised => _lastKeyboardReorderExercised;

    internal bool DragDropReorderExercised => _lastDragDropReorderExercised;

    internal bool ProtectedSpeakerPreserved => _lastProtectedSpeakerPreserved;

    internal bool SubgraphRoleProtected => _lastSubgraphProtected;

    internal bool EmptyStateVerified => _emptyStateVerified;

    internal bool PaginationIsInventoryOnly =>
        _draft is { Lines.Count: > LinePageSize } && _visibleLineIndexes.Count <= LinePageSize &&
        _draft.Lines.Count > _visibleLineIndexes.Count;

    internal bool UsesIsolatedEvidence => _evidenceWorkspace is not null;

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceResourcesReleased => _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal bool ShowsHumanSurface =>
        _inventory.Text.Contains("ligne", StringComparison.OrdinalIgnoreCase) &&
        _lineContext.Text.Contains("Ligne", StringComparison.Ordinal) &&
        _miniMap.Text.Contains("entrant", StringComparison.OrdinalIgnoreCase) &&
        _previewSummary.Text.Contains("Dry-run", StringComparison.OrdinalIgnoreCase) &&
        !_previewSummary.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_miniMap.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase);

    internal StudioDialogueFoundationPage(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioDialogueFoundationPageUx5A";
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = StudioStyle.Background,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = StudioStyle.Margin(12, 10, 12, 10);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var banner = new HBoxContainer();
        banner.AddThemeConstantOverride("separation", 10);
        root.AddChild(banner);
        var bannerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerText.AddChild(StudioStyle.Label("UX5-A · DLG-001 À DLG-005", 10, StudioStyle.Accent));
        bannerText.AddChild(StudioStyle.Label("Dialogues · lignes et choix", 20));
        bannerText.AddChild(StudioStyle.Label(
            "Édition humaine directe. Le graphe reste une mini-carte avancée en lecture seule ; chaque écriture passe par un dry-run canonique.",
            10,
            StudioStyle.Muted,
            wrap: true));
        banner.AddChild(bannerText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        banner.AddChild(close);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = [280, 830],
        };
        root.AddChild(split);

        var inventoryCard = Card(split, "DIALOGUES ET LIGNES",
            "Choisissez d’abord un dialogue. Les pages de 25 lignes ne filtrent jamais le graphe ni ses branches.");
        inventoryCard.CustomMinimumSize = new Vector2(270, 0);
        _dialoguePicker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_dialoguePicker);
        inventoryCard.AddChild(_dialoguePicker);
        _inventory = StudioStyle.Label("Aucune sélection · édition désactivée.", 10, StudioStyle.Muted, wrap: true);
        inventoryCard.AddChild(_inventory);
        _displayName = Field(inventoryCard, "Nom humain", "Nom du dialogue");
        _slug = Field(inventoryCard, "Slug", "dialogue-principal");
        var lineToolbar = new HBoxContainer();
        lineToolbar.AddThemeConstantOverride("separation", 5);
        inventoryCard.AddChild(lineToolbar);
        _previousPage = StudioStyle.Button("‹ 25", compact: true);
        _nextPage = StudioStyle.Button("25 ›", compact: true);
        _linePage = StudioStyle.Label("Page 1 / 1", 10, StudioStyle.Muted);
        _linePage.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _linePage.HorizontalAlignment = HorizontalAlignment.Center;
        lineToolbar.AddChild(_previousPage);
        lineToolbar.AddChild(_linePage);
        lineToolbar.AddChild(_nextPage);
        _lines = new StudioDialogueChoiceListUx5A
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 220),
        };
        inventoryCard.AddChild(_lines);
        var lineActions = new HBoxContainer();
        lineActions.AddThemeConstantOverride("separation", 5);
        inventoryCard.AddChild(lineActions);
        _addLine = StudioStyle.Button("＋ Ligne", compact: true);
        _removeLine = StudioStyle.Button("Retirer", compact: true);
        _setEntry = StudioStyle.Button("Définir entrée", compact: true);
        lineActions.AddChild(_addLine);
        lineActions.AddChild(_removeLine);
        lineActions.AddChild(_setEntry);

        var editScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(500, 0),
        };
        split.AddChild(editScroll);
        var editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editor.AddThemeConstantOverride("separation", 8);
        editScroll.AddChild(editor);
        var lineCard = Card(editor, "LIGNE SÉLECTIONNÉE",
            "Le rôle et l’intervenant sont explicites. Conditions, actions, variantes et profils avancés restent préservés hors surface.");
        _lineContext = StudioStyle.Label("Ligne non sélectionnée.", 10, StudioStyle.Muted, wrap: true);
        lineCard.AddChild(_lineContext);
        var lineIdentity = new HBoxContainer();
        lineIdentity.AddThemeConstantOverride("separation", 8);
        lineCard.AddChild(lineIdentity);
        _role = OptionField(lineIdentity, "Rôle");
        _speaker = OptionField(lineIdentity, "Intervenant");
        _lineText = TextField(lineCard, "Texte de la ligne", 92);

        var choiceCard = Card(editor, "RÉPONSES ORDONNÉES",
            "Glissez une réponse ou utilisez Monter/Descendre. Alt+↑ et Alt+↓ offrent la même action au clavier.");
        _choices = new StudioDialogueChoiceListUx5A
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 150),
        };
        choiceCard.AddChild(_choices);
        var choiceOrder = new HBoxContainer();
        choiceOrder.AddThemeConstantOverride("separation", 5);
        choiceCard.AddChild(choiceOrder);
        _addChoice = StudioStyle.Button("＋ Réponse", compact: true);
        _removeChoice = StudioStyle.Button("Retirer", compact: true);
        _moveChoiceUp = StudioStyle.Button("Monter · Alt+↑", compact: true);
        _moveChoiceDown = StudioStyle.Button("Descendre · Alt+↓", compact: true);
        choiceOrder.AddChild(_addChoice);
        choiceOrder.AddChild(_removeChoice);
        choiceOrder.AddChild(_moveChoiceUp);
        choiceOrder.AddChild(_moveChoiceDown);
        _choiceText = TextField(choiceCard, "Texte de la réponse", 70);
        var choiceProperties = new HBoxContainer();
        choiceProperties.AddThemeConstantOverride("separation", 8);
        choiceCard.AddChild(choiceProperties);
        _choiceTarget = OptionField(choiceProperties, "Cible humaine");
        _edgeKind = OptionField(choiceProperties, "Type de lien");
        var priorityColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(110, 0),
        };
        priorityColumn.AddChild(StudioStyle.SectionLabel("PRIORITÉ"));
        _priority = new LineEdit { PlaceholderText = "0", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(_priority);
        priorityColumn.AddChild(_priority);
        choiceProperties.AddChild(priorityColumn);

        var historyCard = Card(editor, "TRANSACTION",
            "Prévisualiser ne modifie rien. Appliquer utilise la commande canonique et l’historique global du projet.");
        var transactionRow = new HBoxContainer();
        transactionRow.AddThemeConstantOverride("separation", 6);
        historyCard.AddChild(transactionRow);
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer l’aperçu", primary: true, compact: true);
        var undo = StudioStyle.Button("Annuler", compact: true);
        var redo = StudioStyle.Button("Rétablir", compact: true);
        var save = StudioStyle.Button("Enregistrer", compact: true);
        transactionRow.AddChild(_preview);
        transactionRow.AddChild(_apply);
        transactionRow.AddChild(undo);
        transactionRow.AddChild(redo);
        transactionRow.AddChild(save);

        var advanced = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0),
        };
        advanced.AddThemeConstantOverride("separation", 8);
        split.AddChild(advanced);
        var mapCard = Card(
            advanced,
            "MINI-CARTE AVANCÉE · LECTURE SEULE",
            "Toutes les lignes et liaisons sont inventoriées, y compris hors de la page courante.",
            expandVertical: true);
        mapCard.SizeFlagsVertical = SizeFlags.ExpandFill;
        _miniMap = new TextEdit
        {
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 260),
        };
        ApplyTextStyle(_miniMap);
        mapCard.AddChild(_miniMap);
        var previewCard = Card(
            advanced,
            "APERÇU ET DIAGNOSTICS",
            "Le diff reste humain ; les références internes ne sont jamais affichées.",
            expandVertical: true);
        previewCard.SizeFlagsVertical = SizeFlags.ExpandFill;
        _previewSummary = StudioStyle.Label(
            "Aucun aperçu · Dry-run requis avant toute écriture.",
            10,
            StudioStyle.Muted,
            wrap: true);
        _previewSummary.SizeFlagsVertical = SizeFlags.ExpandFill;
        previewCard.AddChild(_previewSummary);

        _status = StudioStyle.Label("Choisissez explicitement un dialogue pour commencer.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        foreach (var role in Enum.GetValues<DialogueNodeRole>().Where(item => item != DialogueNodeRole.Unspecified))
        {
            _roles.Add(role);
            _role.AddItem(HumanRole(role));
            if (role == DialogueNodeRole.Subgraph)
            {
                var roleIndex = _role.ItemCount - 1;
                _role.SetItemDisabled(roleIndex, true);
                _role.SetItemTooltip(
                    roleIndex,
                    "Rôle hérité protégé : la clé de sous-dialogue reste préservée hors de cette surface.");
            }
        }
        _edgeKind.AddItem("Flow · progression");
        _edgeKind.AddItem("Reference · retour intentionnel");

        _dialoguePicker.ItemSelected += SelectDialogue;
        _displayName.TextChanged += _ => UpdateDialogueIdentity();
        _slug.TextChanged += _ => UpdateDialogueIdentity();
        _previousPage.Pressed += () => ChangeLinePage(-1);
        _nextPage.Pressed += () => ChangeLinePage(1);
        _lines.ItemSelected += SelectLine;
        _addLine.Pressed += AddLine;
        _removeLine.Pressed += RemoveLine;
        _setEntry.Pressed += SetEntryLine;
        _role.ItemSelected += _ => UpdateSelectedLine();
        _speaker.ItemSelected += _ => UpdateSelectedLine();
        _lineText.TextChanged += UpdateSelectedLine;
        _choices.ItemSelected += SelectChoice;
        _choices.ChoiceMoved += MoveChoice;
        _addChoice.Pressed += AddChoice;
        _removeChoice.Pressed += RemoveChoice;
        _moveChoiceUp.Pressed += () => MoveSelectedChoice(-1);
        _moveChoiceDown.Pressed += () => MoveSelectedChoice(1);
        _choiceText.TextChanged += UpdateSelectedChoice;
        _choiceTarget.ItemSelected += _ => UpdateSelectedChoice();
        _edgeKind.ItemSelected += _ => UpdateSelectedChoice();
        _priority.TextChanged += _ => UpdateSelectedChoice();
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        undo.Pressed += Undo;
        redo.Pressed += Redo;
        save.Pressed += Save;
        SetEditingState();
    }

    public override void _ExitTree()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        _session = _sourceSession;
        _session.CancelDialogueUx5A();
        OpenActive();
    }

    internal void Close()
    {
        if (ReferenceEquals(_session, _sourceSession))
        {
            _sourceSession.CloseUx5A();
        }
        else
        {
            _session.CloseUx5A();
        }
        Visible = false;
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal void ReloadAfterMutation(DialogueId? dialogueId)
    {
        if (!Visible || !ReferenceEquals(_session, _sourceSession))
        {
            return;
        }
        PopulateDialogues();
        if (dialogueId is null)
        {
            ClearSelection("Choisissez explicitement un dialogue après l’actualisation.");
            return;
        }
        var index = _dialogueIds.FindIndex(item => item == dialogueId);
        if (index < 0)
        {
            ClearSelection("Le dialogue précédemment sélectionné n’existe plus.");
            return;
        }
        _dialoguePicker.Select(index + 1);
        LoadExistingDialogue(index);
    }

    internal bool PrepareEvidence(string mode)
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        if (!ActivateEvidenceWorkspace())
        {
            Visible = false;
            ReleaseEvidenceWorkspace();
            CompleteEvidenceSourceAudit();
            return false;
        }
        if (!SelectEvidenceDialogueAndLine())
        {
            Close();
            return false;
        }

        var reorderControls = ExerciseReorderControlsForEvidence();
        var previewed = PreviewInternal();
        var ready = previewed && mode.StartsWith("ux5-a", StringComparison.OrdinalIgnoreCase) &&
                     _emptyStateVerified && HasExplicitSelection && PreviewReady && MiniMapReadOnly &&
                     KeyboardReorderAvailable && reorderControls && _lastProtectedSpeakerPreserved &&
                     _lastSubgraphProtected && PaginationIsInventoryOnly && ShowsHumanSurface &&
                     EvidenceSourceUntouched;
        if (!ready)
        {
            Close();
        }
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        var visible = false;
        var dryRunPure = false;
        var applied = false;
        var undoRedo = false;
        var persisted = false;
        var cleanupSucceeded = true;
        var controlledFailure = "none";
        StudioSessionController? reopened = null;
        try
        {
            if (!ActivateEvidenceWorkspace() || !SelectEvidenceDialogueAndLine())
            {
                throw new InvalidOperationException("The isolated UX5-A dialogue fixture is unavailable.");
            }
            var dialogueId = _draft!.DialogueId;
            var originalFirstChoice = _draft.Lines[0].Choices[0].Original.Id;
            var reorderControls = ExerciseReorderControlsForEvidence();
            var reorderedFirstChoice = _draft.Lines[0].Choices[0].Original.Id;
            var history = _session.History!;
            var beforeProject = _session.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var previewed = PreviewInternal();
            dryRunPure = previewed && ReferenceEquals(beforeProject, _session.Project) &&
                         history.State.Revision == beforeRevision &&
                         history.Journal.SequenceEqual(beforeJournal);
            applied = ApplyInternal();
            var orderApplied = _session.Project!.Dialogues.Single(item => item.Id == dialogueId)
                .Nodes[0].Choices[0].Id == reorderedFirstChoice;
            var undo = _session.Undo();
            var orderUndone = _session.Project!.Dialogues.Single(item => item.Id == dialogueId)
                .Nodes[0].Choices[0].Id == originalFirstChoice;
            var redo = _session.Redo();
            var orderRedone = _session.Project!.Dialogues.Single(item => item.Id == dialogueId)
                .Nodes[0].Choices[0].Id == reorderedFirstChoice;
            undoRedo = undo.Succeeded && orderUndone && redo.Succeeded && orderRedone;
            var serialized = ProjectJsonSerializer.Serialize(_session.Project!);
            var saved = _session.Save().Succeeded;
            reopened = new StudioSessionController();
            var opened = reopened.Open(_evidenceWorkspace!.RootPath);
            persisted = saved && opened.Succeeded && string.Equals(
                ProjectJsonSerializer.Serialize(reopened.Project!),
                serialized,
                StringComparison.Ordinal);
            var appliedEntry = _session.Project!.Dialogues.Single(item => item.Id == dialogueId).Nodes[0];
            visible = _emptyStateVerified && HasExplicitSelection && MiniMapReadOnly && KeyboardReorderAvailable &&
                      reorderControls && _lastProtectedSpeakerPreserved && _lastSubgraphProtected &&
                      PaginationIsInventoryOnly && ShowsHumanSurface && orderApplied &&
                      appliedEntry.SpeakerBlueprintId == _draft.Lines[0].SpeakerBlueprintId;
        }
        catch (Exception exception)
        {
            controlledFailure = exception.GetType().Name;
        }
        finally
        {
            if (reopened is not null)
            {
                try
                {
                    reopened.CloseUx5A();
                    reopened.Close();
                }
                catch (Exception exception)
                {
                    cleanupSucceeded = false;
                    controlledFailure += $"|reopened:{exception.GetType().Name}";
                }
            }
            if (!ReferenceEquals(_session, _sourceSession))
            {
                try
                {
                    _session.CloseUx5A();
                }
                catch (Exception exception)
                {
                    cleanupSucceeded = false;
                    controlledFailure += $"|editing:{exception.GetType().Name}";
                }
            }
            Visible = false;
            try
            {
                ReleaseEvidenceWorkspace();
            }
            catch (Exception exception)
            {
                cleanupSucceeded = false;
                controlledFailure += $"|release:{exception.GetType().Name}";
                _session = _sourceSession;
            }
            try
            {
                CompleteEvidenceSourceAudit();
            }
            catch (Exception exception)
            {
                cleanupSucceeded = false;
                controlledFailure += $"|audit:{exception.GetType().Name}";
            }
        }

        var valid = visible && dryRunPure && applied && undoRedo && persisted && cleanupSucceeded &&
                    EvidenceSourceUntouched && EvidenceResourcesReleased && SourceSessionRestored;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX5A_UI_TRANSACTION_FAILED visible={visible.ToString().ToLowerInvariant()} " +
                $"dry_run={dryRunPure.ToString().ToLowerInvariant()} apply={applied.ToString().ToLowerInvariant()} " +
                $"undo_redo={undoRedo.ToString().ToLowerInvariant()} persisted={persisted.ToString().ToLowerInvariant()} " +
                $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                $"released={EvidenceResourcesReleased.ToString().ToLowerInvariant()} " +
                $"cleanup={cleanupSucceeded.ToString().ToLowerInvariant()} failure={controlledFailure}");
        }
        return valid;
    }

    private void OpenActive()
    {
        _session.CancelDialogueUx5A();
        Visible = true;
        _lastKeyboardReorderExercised = false;
        _lastDragDropReorderExercised = false;
        _lastProtectedSpeakerPreserved = false;
        _lastSubgraphProtected = false;
        PopulateSpeakers();
        PopulateDialogues();
        ClearSelection("Choisissez explicitement un dialogue ; aucune sélection implicite n’est effectuée.");
        _emptyStateVerified = _dialoguePicker.Selected == 0 && _draft is null &&
                              !_displayName.Editable && !_slug.Editable &&
                              _preview.Disabled && _apply.Disabled;
    }

    private void PopulateDialogues()
    {
        _loading = true;
        _dialoguePicker.Clear();
        _dialogueIds.Clear();
        _dialoguePicker.AddItem("Choisir un dialogue…");
        foreach (var choice in _session.DialogueChoicesUx5A)
        {
            _dialoguePicker.AddItem(choice.Label);
            _dialoguePicker.SetItemTooltip(
                _dialoguePicker.ItemCount - 1,
                $"{choice.LineCount} ligne(s) · {choice.ChoiceCount} réponse(s)");
            _dialogueIds.Add(choice.Id);
        }
        _dialoguePicker.AddItem("＋ Nouveau dialogue…");
        _dialoguePicker.Select(0);
        _loading = false;
    }

    private void PopulateSpeakers()
    {
        _speaker.Clear();
        _speakerIds.Clear();
        _speakerProtected.Clear();
        foreach (var choice in _session.DialogueSpeakerChoicesUx5A)
        {
            _speaker.AddItem(choice.Label);
            _speakerIds.Add(choice.Id);
            _speakerProtected.Add(choice.Protected);
            if (choice.Protected)
            {
                _speaker.SetItemTooltip(
                    _speaker.ItemCount - 1,
                    "Intervenant hérité : sélection humaine conservée, type avancé non modifié.");
            }
        }
        if (_speakerIds.Count == 0)
        {
            _speaker.AddItem("Narration / système");
            _speakerIds.Add(null);
            _speakerProtected.Add(false);
        }
    }

    private void SelectDialogue(long index)
    {
        if (_loading)
        {
            return;
        }
        _session.CancelDialogueUx5A();
        if (index <= 0)
        {
            ClearSelection("Choisissez explicitement un dialogue pour activer l’édition.");
            return;
        }
        if (index <= _dialogueIds.Count)
        {
            LoadExistingDialogue((int)index - 1);
            return;
        }
        if (index == _dialogueIds.Count + 1)
        {
            LoadDraft(_session.CreateNewDialogueDraftUx5A(), "Nouveau dialogue créé en mémoire · dry-run requis.");
            return;
        }
        ClearSelection("Sélection de dialogue invalide.");
    }

    private void LoadExistingDialogue(int dialogueIndex)
    {
        if (dialogueIndex < 0 || dialogueIndex >= _dialogueIds.Count)
        {
            ClearSelection("Le dialogue sélectionné n’existe plus.");
            return;
        }
        LoadDraft(
            _session.CreateDialogueDraftUx5A(_dialogueIds[dialogueIndex]),
            "Dialogue chargé · sélectionnez explicitement une ligne.");
    }

    private void LoadDraft(StudioDialogueDraftUx5A? draft, string message)
    {
        if (draft is null)
        {
            ClearSelection("Le dialogue ne peut pas être chargé sans ambiguïté.");
            return;
        }
        _draft = draft;
        _linePageIndex = 0;
        _selectedLineIndex = -1;
        _selectedChoiceIndex = -1;
        _loading = true;
        _displayName.Text = draft.DisplayName;
        _slug.Text = draft.Slug;
        _loading = false;
        _inventory.Text =
            $"{draft.Lines.Count} ligne(s) · {draft.Lines.Sum(item => item.Choices.Count)} réponse(s) · " +
            "inventaire complet conservé en mémoire";
        PopulateLinePage();
        ClearLineEditor();
        RefreshMiniMap();
        ClearPreview("Aucun aperçu · Dry-run requis après sélection et édition.");
        SetStatus(message);
        SetEditingState();
    }

    private void ClearSelection(string message)
    {
        _draft = null;
        _lastPreview = null;
        _linePageIndex = 0;
        _selectedLineIndex = -1;
        _selectedChoiceIndex = -1;
        _loading = true;
        _displayName.Clear();
        _slug.Clear();
        _loading = false;
        _inventory.Text = "Aucune sélection · édition désactivée.";
        _lines.Clear();
        _visibleLineIndexes.Clear();
        _linePage.Text = "Page 1 / 1 · 0 ligne";
        ClearLineEditor();
        _miniMap.Text = "Mini-carte indisponible · choisissez un dialogue.";
        ClearPreview("Aucun aperçu · Dry-run requis avant toute écriture.");
        SetStatus(message);
        SetEditingState();
    }

    private void PopulateLinePage(int selectedAbsoluteIndex = -1)
    {
        _lines.Clear();
        _visibleLineIndexes.Clear();
        if (_draft is null)
        {
            return;
        }
        var pageCount = Math.Max(1, (int)Math.Ceiling(_draft.Lines.Count / (double)LinePageSize));
        _linePageIndex = Math.Clamp(_linePageIndex, 0, pageCount - 1);
        var start = _linePageIndex * LinePageSize;
        var end = Math.Min(start + LinePageSize, _draft.Lines.Count);
        for (var index = start; index < end; index++)
        {
            var line = _draft.Lines[index];
            _visibleLineIndexes.Add(index);
            var entry = line.Original.Id == _draft.EntryNodeId ? "◆" : "◇";
            _lines.AddItem(
                $"{entry} L{index + 1:00} · {HumanRole(line.Role)} · {Snippet(line.Text)}");
            _lines.SetItemTooltip(
                _lines.ItemCount - 1,
                $"{line.Choices.Count} réponse(s) · intervenant {HumanSpeaker(line.SpeakerBlueprintId)}");
        }
        _linePage.Text =
            $"Page {_linePageIndex + 1} / {pageCount} · {_draft.Lines.Count} ligne(s) au total";
        _previousPage.Disabled = _linePageIndex == 0;
        _nextPage.Disabled = _linePageIndex >= pageCount - 1;
        if (selectedAbsoluteIndex >= start && selectedAbsoluteIndex < end)
        {
            var local = selectedAbsoluteIndex - start;
            _lines.Select(local);
        }
    }

    private void ChangeLinePage(int direction)
    {
        if (_draft is null)
        {
            return;
        }
        var pageCount = Math.Max(1, (int)Math.Ceiling(_draft.Lines.Count / (double)LinePageSize));
        var next = Math.Clamp(_linePageIndex + direction, 0, pageCount - 1);
        if (next == _linePageIndex)
        {
            return;
        }
        _linePageIndex = next;
        _selectedLineIndex = -1;
        _selectedChoiceIndex = -1;
        PopulateLinePage();
        ClearLineEditor();
        SetEditingState();
        SetStatus("Page d’inventaire changée · le graphe complet reste chargé.");
    }

    private void SelectLine(long visibleIndex)
    {
        if (_draft is null || visibleIndex < 0 || visibleIndex >= _visibleLineIndexes.Count)
        {
            return;
        }
        SelectLineAbsolute(_visibleLineIndexes[(int)visibleIndex]);
    }

    private void SelectLineAbsolute(int absoluteIndex)
    {
        if (_draft is null || absoluteIndex < 0 || absoluteIndex >= _draft.Lines.Count)
        {
            return;
        }
        _selectedLineIndex = absoluteIndex;
        _selectedChoiceIndex = -1;
        var line = _draft.Lines[absoluteIndex];
        _loading = true;
        _lineText.Text = line.Text;
        var roleIndex = _roles.FindIndex(item => item == line.Role);
        _role.Select(Math.Max(0, roleIndex));
        var speakerIndex = EnsureSpeakerOption(line.SpeakerBlueprintId);
        _speaker.Select(speakerIndex);
        _loading = false;
        _lineContext.Text =
            $"Ligne {absoluteIndex + 1:00} · {(line.Original.Id == _draft.EntryNodeId ? "entrée principale" : "ligne liée")} · " +
            $"{line.Choices.Count} réponse(s)";
        PopulateChoices();
        RefreshTargets();
        ClearChoiceEditor();
        SetEditingState();
    }

    private void ClearLineEditor()
    {
        _loading = true;
        _lineText.Clear();
        _role.Select(0);
        _speaker.Select(0);
        _loading = false;
        _lineContext.Text = "Ligne non sélectionnée · choisissez une ligne dans l’inventaire.";
        _choices.Clear();
        _targetNodeIds.Clear();
        _choiceTarget.Clear();
        ClearChoiceEditor();
    }

    private void AddLine()
    {
        if (_draft is null)
        {
            return;
        }
        var index = _draft.Lines.Count;
        var nodeId = DialogueNodeId.From(Guid.NewGuid());
        var narrator = DialogueParticipant.FromSpeaker(null);
        var key = $"{_draft.Slug}.line-{index + 1}-{Guid.NewGuid():N}";
        var original = new DialogueNode(
            nodeId,
            null,
            new LocalizedText(key, "Nouvelle ligne", provenance: "author:studio-ux5-a"),
            role: DialogueNodeRole.Narration);
        var line = new StudioDialogueLineDraftUx5A(
            original,
            original.Text.Fallback,
            original.Role,
            null,
            narrator.Id,
            []);
        _draft = _draft with { Lines = [.. _draft.Lines, line] };
        _linePageIndex = index / LinePageSize;
        PopulateLinePage(index);
        SelectLineAbsolute(index);
        InvalidatePreview("Ligne ajoutée en mémoire · reliez-la avant le dry-run.");
        RefreshInventoryAndMap();
    }

    private void RemoveLine()
    {
        if (_draft is null || _selectedLineIndex < 0 || _selectedLineIndex >= _draft.Lines.Count)
        {
            return;
        }
        if (_draft.Lines.Count == 1)
        {
            SetStatus("Un dialogue doit conserver au moins une ligne.", true);
            return;
        }
        var selectedId = _draft.Lines[_selectedLineIndex].Original.Id;
        if (selectedId == _draft.EntryNodeId)
        {
            SetStatus("Définissez d’abord une autre entrée principale avant de retirer cette ligne.", true);
            return;
        }
        if (_draft.Source.EntryPoints.Any(entry => entry.NodeId == selectedId))
        {
            SetStatus(
                "Retrait refusé · cette ligne appartient aux entrées conservées du dialogue.",
                true);
            return;
        }
        var incoming = _draft.Lines.Sum(line => line.Choices.Count(choice => choice.TargetNodeId == selectedId));
        if (incoming > 0)
        {
            SetStatus(
                $"Retrait refusé · {incoming} réponse(s) ciblent encore cette ligne. Modifiez-les explicitement.",
                true);
            return;
        }
        var lines = _draft.Lines.Where((_, index) => index != _selectedLineIndex).ToArray();
        _draft = _draft with { Lines = lines };
        _selectedLineIndex = -1;
        _selectedChoiceIndex = -1;
        _linePageIndex = Math.Min(_linePageIndex, Math.Max(0, (lines.Length - 1) / LinePageSize));
        PopulateLinePage();
        ClearLineEditor();
        InvalidatePreview("Ligne retirée du brouillon · dry-run requis.");
        RefreshInventoryAndMap();
        SetEditingState();
    }

    private void SetEntryLine()
    {
        if (_draft is null || _selectedLineIndex < 0 || _selectedLineIndex >= _draft.Lines.Count)
        {
            return;
        }
        _draft = _draft with { EntryNodeId = _draft.Lines[_selectedLineIndex].Original.Id };
        PopulateLinePage(_selectedLineIndex);
        _lineContext.Text = $"Ligne {_selectedLineIndex + 1:00} · entrée principale · " +
                            $"{_draft.Lines[_selectedLineIndex].Choices.Count} réponse(s)";
        InvalidatePreview("Entrée principale modifiée en mémoire · dry-run requis.");
        RefreshMiniMap();
    }

    private void UpdateDialogueIdentity()
    {
        if (_loading || _draft is null)
        {
            return;
        }
        _draft = _draft with { DisplayName = _displayName.Text, Slug = _slug.Text };
        InvalidatePreview("Identité humaine modifiée · dry-run requis.");
    }

    private void UpdateSelectedLine()
    {
        if (_loading || _draft is null || _selectedLineIndex < 0 ||
            _selectedLineIndex >= _draft.Lines.Count)
        {
            return;
        }
        var lines = _draft.Lines.ToArray();
        var line = lines[_selectedLineIndex];
        var roleIndex = Math.Clamp(_role.Selected, 0, _roles.Count - 1);
        var speakerIndex = Math.Clamp(_speaker.Selected, 0, _speakerIds.Count - 1);
        var speaker = _speakerIds[speakerIndex];
        var selectedRole = _roles[roleIndex];
        if (line.Original.Role == DialogueNodeRole.Subgraph)
        {
            selectedRole = DialogueNodeRole.Subgraph;
        }
        else if (selectedRole == DialogueNodeRole.Subgraph)
        {
            selectedRole = line.Role;
        }
        var participantId = speaker == line.Original.SpeakerBlueprintId
            ? line.Original.ParticipantId
            : DialogueParticipant.FromSpeaker(speaker).Id;
        lines[_selectedLineIndex] = line with
        {
            Text = _lineText.Text,
            Role = selectedRole,
            SpeakerBlueprintId = speaker,
            ParticipantId = participantId,
        };
        _draft = _draft with { Lines = lines };
        PopulateLinePage(_selectedLineIndex);
        InvalidatePreview("Ligne modifiée · dry-run requis.");
        RefreshMiniMap();
    }

    private void PopulateChoices(int selectedIndex = -1)
    {
        _choices.Clear();
        var line = CurrentLine;
        if (line is null)
        {
            return;
        }
        for (var index = 0; index < line.Choices.Count; index++)
        {
            var choice = line.Choices[index];
            var target = ResolveTargetLabel(choice.TargetNodeId);
            var reference = choice.EdgeKind == DialogueEdgeKind.Reference ? " · référence" : string.Empty;
            _choices.AddItem(
                $"{index + 1:00} · P{choice.PriorityText} · {Snippet(choice.Text)} → {target}{reference}");
        }
        if (selectedIndex >= 0 && selectedIndex < line.Choices.Count)
        {
            _choices.Select(selectedIndex);
        }
    }

    private void RefreshTargets()
    {
        _choiceTarget.Clear();
        _targetNodeIds.Clear();
        _choiceTarget.AddItem("Fin du dialogue · aucune cible");
        _targetNodeIds.Add(null);
        if (_draft is null)
        {
            return;
        }
        for (var index = 0; index < _draft.Lines.Count; index++)
        {
            var line = _draft.Lines[index];
            _choiceTarget.AddItem($"Ligne {index + 1:00} · {HumanRole(line.Role)} · {Snippet(line.Text)}");
            _targetNodeIds.Add(line.Original.Id);
        }
    }

    private void SelectChoice(long index)
    {
        var line = CurrentLine;
        if (line is null || index < 0 || index >= line.Choices.Count)
        {
            return;
        }
        _selectedChoiceIndex = (int)index;
        var choice = line.Choices[_selectedChoiceIndex];
        _loading = true;
        _choiceText.Text = choice.Text;
        var targetIndex = _targetNodeIds.FindIndex(item => item == choice.TargetNodeId);
        _choiceTarget.Select(Math.Max(0, targetIndex));
        _edgeKind.Select(choice.EdgeKind == DialogueEdgeKind.Reference ? 1 : 0);
        _priority.Text = choice.PriorityText;
        _loading = false;
        SetEditingState();
    }

    private void ClearChoiceEditor()
    {
        _selectedChoiceIndex = -1;
        _loading = true;
        _choiceText.Clear();
        if (_choiceTarget.ItemCount > 0)
        {
            _choiceTarget.Select(0);
        }
        _edgeKind.Select(0);
        _priority.Text = "0";
        _loading = false;
        SetEditingState();
    }

    private void AddChoice()
    {
        var line = CurrentLine;
        if (_draft is null || line is null || _selectedLineIndex < 0)
        {
            return;
        }
        if (line.Role == DialogueNodeRole.End)
        {
            SetStatus("Changez d’abord le rôle Fin avant d’ajouter une réponse.", true);
            return;
        }
        var key = $"{_draft.Slug}.choice-{Guid.NewGuid():N}";
        var original = new DialogueChoice(
            DialogueChoiceId.From(Guid.NewGuid()),
            new LocalizedText(key, "Nouvelle réponse", provenance: "author:studio-ux5-a"));
        var choice = new StudioDialogueChoiceDraftUx5A(
            original,
            original.Text.Fallback,
            null,
            DialogueEdgeKind.Flow,
            "0");
        var choices = line.Choices.Append(choice).ToArray();
        ReplaceCurrentLine(line with { Choices = choices });
        _selectedChoiceIndex = choices.Length - 1;
        PopulateChoices(_selectedChoiceIndex);
        RefreshTargets();
        SelectChoice(_selectedChoiceIndex);
        InvalidatePreview("Réponse ajoutée en mémoire · dry-run requis.");
        RefreshInventoryAndMap();
    }

    private void RemoveChoice()
    {
        var line = CurrentLine;
        if (line is null || _selectedChoiceIndex < 0 || _selectedChoiceIndex >= line.Choices.Count)
        {
            return;
        }
        var choices = line.Choices.Where((_, index) => index != _selectedChoiceIndex).ToArray();
        ReplaceCurrentLine(line with { Choices = choices });
        PopulateChoices();
        ClearChoiceEditor();
        InvalidatePreview("Réponse retirée du brouillon · dry-run requis.");
        RefreshInventoryAndMap();
    }

    private void UpdateSelectedChoice()
    {
        var line = CurrentLine;
        if (_loading || _draft is null || line is null || _selectedChoiceIndex < 0 ||
            _selectedChoiceIndex >= line.Choices.Count)
        {
            return;
        }
        var targetIndex = Math.Clamp(_choiceTarget.Selected, 0, Math.Max(0, _targetNodeIds.Count - 1));
        var choices = line.Choices.ToArray();
        choices[_selectedChoiceIndex] = choices[_selectedChoiceIndex] with
        {
            Text = _choiceText.Text,
            TargetNodeId = _targetNodeIds.Count == 0 ? null : _targetNodeIds[targetIndex],
            EdgeKind = _edgeKind.Selected == 1 ? DialogueEdgeKind.Reference : DialogueEdgeKind.Flow,
            PriorityText = _priority.Text,
        };
        ReplaceCurrentLine(line with { Choices = choices });
        PopulateChoices(_selectedChoiceIndex);
        InvalidatePreview("Réponse modifiée · dry-run requis.");
        RefreshMiniMap();
    }

    private void MoveSelectedChoice(int direction)
    {
        var line = CurrentLine;
        if (line is null || _selectedChoiceIndex < 0)
        {
            return;
        }
        MoveChoice(_selectedChoiceIndex, _selectedChoiceIndex + direction);
    }

    private void MoveChoice(int sourceIndex, int targetIndex)
    {
        var line = CurrentLine;
        if (_draft is null || line is null || _selectedLineIndex < 0 ||
            sourceIndex < 0 || sourceIndex >= line.Choices.Count ||
            targetIndex < 0 || targetIndex >= line.Choices.Count || sourceIndex == targetIndex)
        {
            return;
        }
        _draft = StudioSessionController.MoveChoiceDraftUx5A(
            _draft,
            _selectedLineIndex,
            sourceIndex,
            targetIndex);
        _selectedChoiceIndex = targetIndex;
        PopulateChoices(targetIndex);
        SelectChoice(targetIndex);
        InvalidatePreview(
            $"Réponses réordonnées · position {sourceIndex + 1} vers {targetIndex + 1} · dry-run requis.");
        RefreshMiniMap();
    }

    private void ReplaceCurrentLine(StudioDialogueLineDraftUx5A replacement)
    {
        if (_draft is null || _selectedLineIndex < 0 || _selectedLineIndex >= _draft.Lines.Count)
        {
            return;
        }
        var lines = _draft.Lines.ToArray();
        lines[_selectedLineIndex] = replacement;
        _draft = _draft with { Lines = lines };
    }

    private StudioDialogueLineDraftUx5A? CurrentLine =>
        _draft is not null && _selectedLineIndex >= 0 && _selectedLineIndex < _draft.Lines.Count
            ? _draft.Lines[_selectedLineIndex]
            : null;

    private void Preview() => PreviewInternal();

    private bool PreviewInternal()
    {
        if (_draft is null || !HasExplicitSelection)
        {
            SetStatus("Choisissez explicitement un dialogue avant le dry-run.", true);
            return false;
        }
        var preview = _session.PreviewDialogueUx5A(_draft);
        _lastPreview = preview;
        _previewSummary.Text = RenderPreview(preview);
        _previewSummary.AddThemeColorOverride(
            "font_color",
            preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
        _apply.Disabled = !preview.CanApply;
        SetStatus(preview.Message, !preview.Succeeded);
        return preview.Succeeded;
    }

    private void Apply() => ApplyInternal();

    private bool ApplyInternal()
    {
        var dialogueId = _draft?.DialogueId;
        var result = _session.ApplyApprovedDialogueUx5A();
        SetStatus(result.Message, !result.Succeeded);
        _apply.Disabled = true;
        if (!result.Succeeded)
        {
            return false;
        }
        _lastPreview = null;
        if (ReferenceEquals(_session, _sourceSession))
        {
            ReloadAfterMutation(dialogueId);
            DocumentChanged?.Invoke();
        }
        return true;
    }

    private void Undo()
    {
        var dialogueId = _draft?.DialogueId;
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _session.CancelDialogueUx5A();
        if (ReferenceEquals(_session, _sourceSession))
        {
            ReloadAfterMutation(dialogueId);
            DocumentChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var dialogueId = _draft?.DialogueId;
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _session.CancelDialogueUx5A();
        if (ReferenceEquals(_session, _sourceSession))
        {
            ReloadAfterMutation(dialogueId);
            DocumentChanged?.Invoke();
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void InvalidatePreview(string message)
    {
        _session.CancelDialogueUx5A();
        ClearPreview(message);
    }

    private void ClearPreview(string message)
    {
        _lastPreview = null;
        _previewSummary.Text = message;
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _apply.Disabled = true;
    }

    private void RefreshInventoryAndMap()
    {
        if (_draft is null)
        {
            return;
        }
        _inventory.Text =
            $"{_draft.Lines.Count} ligne(s) · {_draft.Lines.Sum(item => item.Choices.Count)} réponse(s) · " +
            "inventaire complet conservé en mémoire";
        PopulateLinePage(_selectedLineIndex);
        RefreshMiniMap();
        SetEditingState();
    }

    private void RefreshMiniMap()
    {
        if (_draft is null)
        {
            _miniMap.Text = "Mini-carte indisponible · choisissez un dialogue.";
            return;
        }
        var builder = new StringBuilder()
            .AppendLine($"Graphe complet · {_draft.Lines.Count} ligne(s) · " +
                        $"{_draft.Lines.Sum(item => item.Choices.Count)} réponse(s)")
            .AppendLine("◆ entrée principale · ◇ ligne · → Flow · ↪ Reference")
            .AppendLine();
        for (var index = 0; index < _draft.Lines.Count; index++)
        {
            var line = _draft.Lines[index];
            var incoming = _draft.Lines.Sum(candidate =>
                candidate.Choices.Count(choice => choice.TargetNodeId == line.Original.Id));
            var outgoing = line.Choices.Count(choice => choice.TargetNodeId is not null);
            var entry = line.Original.Id == _draft.EntryNodeId ? "◆" : "◇";
            builder.AppendLine(
                $"{entry} Ligne {index + 1:00} · {HumanRole(line.Role)} · {incoming} entrant(s) / {outgoing} sortant(s)")
                .AppendLine($"   {Snippet(line.Text, 72)}");
            foreach (var choice in line.Choices)
            {
                var target = choice.TargetNodeId is null
                    ? "fin"
                    : ResolveTargetLabel(choice.TargetNodeId);
                builder.AppendLine(
                    $"   {(choice.EdgeKind == DialogueEdgeKind.Reference ? "↪ Reference" : "→ Flow")} · " +
                    $"P{choice.PriorityText} · {Snippet(choice.Text, 48)} · {target}");
            }
        }
        _miniMap.Text = builder.ToString().TrimEnd();
    }

    private string ResolveTargetLabel(DialogueNodeId? targetId)
    {
        if (targetId is null)
        {
            return "fin";
        }
        var index = _draft?.Lines.ToList().FindIndex(item => item.Original.Id == targetId) ?? -1;
        return index >= 0 ? $"ligne {index + 1:00}" : "cible indisponible";
    }

    private string HumanSpeaker(BlueprintId? speakerId)
    {
        if (speakerId is null)
        {
            return "narration";
        }
        var index = _speakerIds.FindIndex(item => item == speakerId);
        return index >= 0 ? _speaker.GetItemText(index) : "intervenant indisponible";
    }

    private int EnsureSpeakerOption(BlueprintId? speakerId)
    {
        var index = _speakerIds.FindIndex(item => item == speakerId);
        if (index >= 0)
        {
            return index;
        }

        _speaker.AddItem(
            speakerId is null
                ? "Narration / système"
                : "Intervenant référencé indisponible · hérité protégé");
        _speakerIds.Add(speakerId);
        _speakerProtected.Add(speakerId is not null);
        index = _speakerIds.Count - 1;
        if (speakerId is not null)
        {
            _speaker.SetItemTooltip(
                index,
                "Référence préservée sans identifiant technique visible ; le validateur canonique contrôlera sa résolution.");
        }
        return index;
    }

    private void SetEditingState()
    {
        var hasDraft = _draft is not null;
        var hasLine = CurrentLine is not null;
        var hasChoice = hasLine && _selectedChoiceIndex >= 0 &&
                        _selectedChoiceIndex < CurrentLine!.Choices.Count;
        _displayName.Editable = hasDraft;
        _slug.Editable = hasDraft;
        _addLine.Disabled = !hasDraft;
        _removeLine.Disabled = !hasLine;
        _setEntry.Disabled = !hasLine;
        _role.Disabled = !hasLine || CurrentLine?.Original.Role == DialogueNodeRole.Subgraph;
        _speaker.Disabled = !hasLine;
        _lineText.Editable = hasLine;
        _addChoice.Disabled = !hasLine;
        _removeChoice.Disabled = !hasChoice;
        _moveChoiceUp.Disabled = !hasChoice || _selectedChoiceIndex == 0;
        _moveChoiceDown.Disabled = !hasChoice ||
                                   _selectedChoiceIndex >= (CurrentLine?.Choices.Count ?? 0) - 1;
        _choiceText.Editable = hasChoice;
        _choiceTarget.Disabled = !hasChoice;
        _edgeKind.Disabled = !hasChoice;
        _priority.Editable = hasChoice;
        _preview.Disabled = !hasDraft;
        _apply.Disabled = _lastPreview is not { CanApply: true };
    }

    private bool SelectEvidenceDialogueAndLine()
    {
        var index = _session.DialogueChoicesUx5A.ToList().FindIndex(item =>
            item.Label.Contains("Branche témoin", StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }
        _dialoguePicker.Select(index + 1);
        LoadExistingDialogue(index);
        if (_draft is null || _draft.Lines.Count <= LinePageSize || _draft.Lines[0].Choices.Count < 2)
        {
            return false;
        }
        _lines.Select(0);
        SelectLineAbsolute(0);
        _choices.Select(0);
        SelectChoice(0);
        var selectedSpeakerIndex = _speaker.Selected;
        var selectedSpeaker = selectedSpeakerIndex >= 0 && selectedSpeakerIndex < _speakerIds.Count
            ? _speakerIds[selectedSpeakerIndex]
            : null;
        var entrySpeaker = _draft.Lines[0].SpeakerBlueprintId;
        _lineText.Text = _lineText.Text + " · édition UI";
        UpdateSelectedLine();
        _lastProtectedSpeakerPreserved = entrySpeaker is not null && selectedSpeaker == entrySpeaker &&
                                         _speakerProtected[selectedSpeakerIndex] &&
                                         _draft.Lines[0].SpeakerBlueprintId == entrySpeaker &&
                                         _speaker.GetItemText(selectedSpeakerIndex).Contains(
                                             "protégé",
                                             StringComparison.OrdinalIgnoreCase);

        var subgraphIndex = _draft.Lines.ToList().FindIndex(line =>
            line.Original.Role == DialogueNodeRole.Subgraph);
        var subgraphRoleIndex = _roles.FindIndex(role => role == DialogueNodeRole.Subgraph);
        if (subgraphIndex >= 0 && subgraphRoleIndex >= 0)
        {
            SelectLineAbsolute(subgraphIndex);
            _lastSubgraphProtected = _role.Selected == subgraphRoleIndex && _role.Disabled &&
                                     _role.IsItemDisabled(subgraphRoleIndex) &&
                                     _draft.Lines[subgraphIndex].Role == DialogueNodeRole.Subgraph &&
                                     string.Equals(
                                         _draft.Lines[subgraphIndex].Original.SubgraphKey,
                                         "branche-secondaire-protegee",
                                         StringComparison.Ordinal);
        }

        SelectLineAbsolute(0);
        _choices.Select(0);
        SelectChoice(0);
        return HasExplicitSelection && _lastProtectedSpeakerPreserved && _lastSubgraphProtected;
    }

    private bool ExerciseReorderControlsForEvidence()
    {
        var line = CurrentLine;
        if (line is null || line.Choices.Count < 3)
        {
            return false;
        }

        _lastKeyboardReorderExercised = _choices.ExerciseKeyboardMoveForEvidence(0, Key.Down);
        _lastDragDropReorderExercised = _lastKeyboardReorderExercised &&
                                        _choices.ExerciseDragDropMoveForEvidence(1, 2);
        return _lastKeyboardReorderExercised && _lastDragDropReorderExercised;
    }

    private bool ActivateEvidenceWorkspace()
    {
        ReleaseEvidenceWorkspace();
        var workspace = _sourceSession.CreateUx5AEvidenceWorkspace();
        if (workspace is null)
        {
            return false;
        }
        _evidenceWorkspace = workspace;
        _lastEvidenceResourcesReleased = false;
        _session = workspace.Session;
        OpenActive();
        return _session.DialogueChoicesUx5A.Any(item =>
            item.Label.Contains("Branche témoin", StringComparison.Ordinal));
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceWorkspace is null)
        {
            _session = _sourceSession;
            return;
        }
        var workspace = _evidenceWorkspace;
        _evidenceWorkspace = null;
        _session = _sourceSession;
        workspace.Dispose();
        _lastEvidenceResourcesReleased = workspace.ResourcesReleased;
        if (!_lastEvidenceResourcesReleased)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_RELEASE_FAILED resources_released=false");
        }
    }

    private StudioDialogueEvidenceSourceSnapshotUx5A? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureDialogueApprovalUx5A());
    }

    private bool SourceMatchesSnapshot(StudioDialogueEvidenceSourceSnapshotUx5A snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               Equals(_sourceSession.CaptureDialogueApprovalUx5A(), snapshot.Approval);
    }

    private void CompleteEvidenceSourceAudit()
    {
        if (_evidenceSourceSnapshot is not { } snapshot)
        {
            return;
        }
        _lastEvidenceSourceUntouched = SourceMatchesSnapshot(snapshot);
        _evidenceSourceSnapshot = null;
        if (!_lastEvidenceSourceUntouched)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX5A_EVIDENCE_SOURCE_CHANGED source_untouched=false");
        }
    }

    private static string RenderPreview(StudioDialoguePreviewUx5A preview)
    {
        var builder = new StringBuilder()
            .AppendLine(preview.Succeeded
                ? "Dry-run canonique accepté · aucune écriture."
                : "Dry-run refusé · aucune écriture.")
            .AppendLine()
            .AppendLine(preview.Summary);
        if (preview.Diagnostics.Count > 0)
        {
            builder.AppendLine().AppendLine("Corrections requises :");
            foreach (var group in preview.Diagnostics.GroupBy(item => item.Section))
            {
                builder.AppendLine($"• {group.Key}");
                foreach (var diagnostic in group)
                {
                    builder.AppendLine($"  {diagnostic.Code} · {diagnostic.Message}");
                }
            }
        }
        return builder.ToString().TrimEnd();
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static VBoxContainer Card(
        Container parent,
        string title,
        string description,
        bool expandVertical = false)
    {
        var panel = StudioStyle.Panel(title.Replace(' ', '_'), StudioStyle.Surface, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (expandVertical)
        {
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        }
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(9, 8, 9, 8);
        panel.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);
        content.AddChild(StudioStyle.SectionLabel(title));
        content.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
        return content;
    }

    private static LineEdit Field(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.SectionLabel(label));
        var field = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(field);
        parent.AddChild(field);
        return field;
    }

    private static OptionButton OptionField(Container parent, string label)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150, 0),
        };
        column.AddChild(StudioStyle.SectionLabel(label));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        column.AddChild(option);
        parent.AddChild(column);
        return option;
    }

    private static TextEdit TextField(Container parent, string label, float height)
    {
        parent.AddChild(StudioStyle.SectionLabel(label));
        var field = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        ApplyTextStyle(field);
        parent.AddChild(field);
        return field;
    }

    private static void ApplyTextStyle(TextEdit field)
    {
        field.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        field.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        field.AddThemeColorOverride("font_color", StudioStyle.Text);
        field.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        field.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
    }

    private static string HumanRole(DialogueNodeRole role) => role switch
    {
        DialogueNodeRole.SpeakerLine => "Réplique PNJ",
        DialogueNodeRole.PlayerChoice => "Choix joueur",
        DialogueNodeRole.Narration => "Narration",
        DialogueNodeRole.System => "Système",
        DialogueNodeRole.ActionOnly => "Action seule",
        DialogueNodeRole.Subgraph => "Sous-dialogue · hérité protégé",
        DialogueNodeRole.End => "Fin",
        _ => "Rôle hérité",
    };

    private static string Snippet(string value, int maximum = 42)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0)
        {
            return "texte vide";
        }
        return normalized.Length <= maximum ? normalized : normalized[..(maximum - 1)] + "…";
    }
}
