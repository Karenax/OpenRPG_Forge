#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class QuestOperationsWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<QuestId, QuestOperationsProfile?, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _quests;
    private QuestId? _selectedQuestId;
    private Guid _profileId;
    private SpinBox? _priority;
    private SpinBox? _importance;
    private CheckBox? _manualPin;
    private CheckBox? _defaultPinned;
    private CheckBox? _repeat;
    private SpinBox? _cooldown;
    private SpinBox? _maximumCompletions;
    private SpinBox? _rewardDecay;
    private OptionButton? _shareMode;
    private CheckBox? _synchronize;
    private OptionButton? _separation;
    private CheckBox? _exclusive;
    private LineEdit? _mutex;
    private LineEdit? _confirmation;
    private LineEdit? _consequence;
    private OptionButton? _notificationMode;
    private LineEdit? _notificationGroup;
    private LineEdit? _accessibleText;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private bool _lastSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public QuestId? SelectedQuestId => _selectedQuestId;
    public int SummarySectionCount { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<QuestId, QuestOperationsProfile?, bool, CommandResult> mutate, Action undo, Action redo,
        Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Quêtes avancées · OpenRPG Forge";
        MinSize = new(1100, 720); Size = new(1360, 850); CloseRequested += Hide; Build(); Refresh();
    }

    public void Open() { Refresh(); PopupCentered(); _applyIcon?.Invoke(this); }

    public bool ConfigureForSmokeTest()
    {
        Refresh(); if (_selectedQuestId is null) return false;
        _priority!.Value = 92; _importance!.Value = 96; _manualPin!.ButtonPressed = true;
        _defaultPinned!.ButtonPressed = true; _repeat!.ButtonPressed = true; _cooldown!.Value = 3600;
        _maximumCompletions!.Value = 3; _rewardDecay!.Value = .75;
        Select(_shareMode!, QuestShareMode.Unanimous); _synchronize!.ButtonPressed = true;
        Select(_separation!, QuestSeparationPolicy.SnapshotIndividual); _exclusive!.ButtonPressed = true;
        _mutex!.Text = "crypt-ending"; _confirmation!.Text = "Confirmer cette voie irréversible ?";
        _consequence!.Text = "Les autres voies seront verrouillées.";
        Select(_notificationMode!, QuestNotificationMode.Grouped); _notificationGroup!.Text = "crypt-main";
        _accessibleText!.Text = "Quête mise à jour : nouvel objectif disponible."; RebuildSummary(); return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    public void Refresh()
    {
        var state = _load?.Invoke(); var previous = _selectedQuestId; _quests?.Clear();
        if (state is null || state.Project.Quests.Count == 0) { SetEmpty("Aucune quête dans le projet."); return; }
        var candidates = state.Project.Quests.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        foreach (var quest in candidates)
        {
            var profile = quest.Operations;
            var index = _quests!.AddItem($"{quest.DisplayName}\n{quest.Slug} · " +
                (profile is null ? "non configurée" : $"priorité {profile.Journal.Priority} · {profile.Share.Mode}"));
            _quests.SetItemMetadata(index, quest.Id.ToString());
        }
        var selected = Array.FindIndex(candidates, item => item.Id == previous); if (selected < 0) selected = 0;
        _quests!.Select(selected); LoadQuest(candidates[selected]);
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "QUÊTES · JOURNAL, PARTAGE & CONSÉQUENCES" };
        title.AddThemeFontSizeOverride("font_size", 25); title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title); root.AddChild(new Label { Text = "Configurez les politiques de quête sans éditer le JSON ; chaque application est transactionnelle." });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [280] };
        root.AddChild(split); var catalog = Panel("QUÊTES"); split.AddChild(catalog);
        _quests = new ItemList { CustomMinimumSize = new(280, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _quests.ItemSelected += OnSelected; catalog.GetChild<VBoxContainer>(0).AddChild(_quests);
        var editor = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SplitOffsets = [650] };
        split.AddChild(editor); var formPanel = Panel("POLITIQUES"); editor.AddChild(formPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        var journal = Tab(tabs, "JOURNAL & RÉPÉTITION");
        _priority = Spin(journal, "Priorité", 0, 100, 50, 1); _importance = Spin(journal, "Importance narrative", 0, 100, 50, 1);
        _manualPin = Check(journal, "Autoriser l’épinglage manuel"); _defaultPinned = Check(journal, "Épinglée par défaut");
        _repeat = Check(journal, "Quête répétable"); _cooldown = Spin(journal, "Délai entre répétitions (s)", 0, 31_536_000, 0, 60);
        _maximumCompletions = Spin(journal, "Nombre maximal (0 = illimité)", 0, 1000, 0, 1);
        _rewardDecay = Spin(journal, "Multiplicateur de récompense", 0, 1, 1, .05);
        var party = Tab(tabs, "GROUPE & CHOIX"); _shareMode = EnumSelect<QuestShareMode>(); party.AddChild(Labeled("Mode de partage", _shareMode));
        _synchronize = Check(party, "Synchroniser la progression"); _separation = EnumSelect<QuestSeparationPolicy>(); party.AddChild(Labeled("Séparation du groupe", _separation));
        _exclusive = Check(party, "Premier choix irréversible et exclusif"); _mutex = Field(party, "Groupe d’exclusion", "ending");
        _confirmation = Field(party, "Confirmation", "Confirmer ce choix ?"); _consequence = Field(party, "Conséquence affichée", "Ce choix est irréversible.");
        var notices = Tab(tabs, "NOTIFICATIONS"); _notificationMode = EnumSelect<QuestNotificationMode>(); notices.AddChild(Labeled("Mode", _notificationMode));
        _notificationGroup = Field(notices, "Groupe", "quest"); _accessibleText = Field(notices, "Texte accessible", "Journal de quête mis à jour.");
        var summaryPanel = Panel("APERÇU & DIFF"); editor.AddChild(summaryPanel); var summaryBox = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(340, 390) };
        summaryBox.AddChild(_summary); summaryBox.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(340, 140) }; summaryBox.AddChild(_feedback);
        var actions = new HBoxContainer(); root.AddChild(actions); actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true)); actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); })); actions.AddChild(Button("Fermer", Hide));
    }

    private void LoadQuest(QuestDefinition quest)
    {
        _selectedQuestId = quest.Id; var profile = quest.Operations ?? QuestOperationsProfile.CreateDefault(Guid.NewGuid(), "toolset:g13-j");
        _profileId = profile.Id; _priority!.Value = profile.Journal.Priority; _importance!.Value = profile.Journal.NarrativeImportance;
        _manualPin!.ButtonPressed = profile.Journal.AllowManualPin; _defaultPinned!.ButtonPressed = profile.Journal.DefaultPinned;
        _repeat!.ButtonPressed = profile.Repeat.Enabled; _cooldown!.Value = profile.Repeat.CooldownSeconds;
        _maximumCompletions!.Value = profile.Repeat.MaximumCompletions; _rewardDecay!.Value = profile.Repeat.RewardDecay;
        Select(_shareMode!, profile.Share.Mode); _synchronize!.ButtonPressed = profile.Share.SynchronizeProgress;
        Select(_separation!, profile.Share.Separation); var choice = profile.ExclusiveChoices.FirstOrDefault();
        _exclusive!.ButtonPressed = choice is not null; _mutex!.Text = choice?.MutexGroup ?? "ending";
        _confirmation!.Text = choice?.Confirmation.Fallback ?? "Confirmer ce choix ?";
        _consequence!.Text = choice?.Consequence.Fallback ?? "Ce choix est irréversible.";
        Select(_notificationMode!, profile.Notification.Mode); _notificationGroup!.Text = profile.Notification.GroupKey;
        _accessibleText!.Text = profile.Notification.AccessibleText; _feedback!.Text = quest.Operations is null
            ? "[color=#b8a8ce]État vide : configurez les politiques de cette quête.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]"; RebuildSummary();
    }

    private QuestOperationsProfile BuildProfile()
    {
        var quest = SelectedQuest() ?? throw new InvalidOperationException("Aucune quête sélectionnée.");
        var choices = _exclusive!.ButtonPressed && quest.Transitions.Count > 0
            ? new[] { new QuestExclusiveChoicePolicy(Stable(_profileId, "exclusive"), quest.Transitions[0].Id,
                _mutex!.Text.Trim(), true, new("quest.choice.confirm", _confirmation!.Text.Trim()),
                new("quest.choice.consequence", _consequence!.Text.Trim()), [], "toolset:g13-j") }
            : [];
        return new(_profileId, 1, "toolset:g13-j", true,
            new(QuestJournalSortingPolicy.ImportanceThenUpdated, (int)_priority!.Value, _manualPin!.ButtonPressed,
                _defaultPinned!.ButtonPressed, (int)_importance!.Value, true),
            new(_repeat!.ButtonPressed, (long)_cooldown!.Value, (int)_maximumCompletions!.Value,
                QuestRepeatResetMode.Full, [], _rewardDecay!.Value, 2026),
            new((QuestShareMode)_shareMode!.Selected, _synchronize!.ButtonPressed,
                (QuestSeparationPolicy)_separation!.Selected, true), choices,
            new((QuestNotificationMode)_notificationMode!.Selected,
                new("quest.notification.title", "Journal mis à jour"),
                new("quest.notification.body", "Un objectif a été mis à jour."), _accessibleText!.Text.Trim(),
                _notificationGroup!.Text.Trim(), 3));
    }

    private void Mutate(bool dryRun)
    {
        _lastSucceeded = false; LastCommandId = Guid.Empty; LastPatchSummary = LastDiagnostics = string.Empty;
        if (_selectedQuestId is null || _mutate is null) return;
        try
        {
            var result = _mutate(_selectedQuestId, BuildProfile(), dryRun); LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback!.Text = $"[color=#ff7777]Transaction refusée[/color]\n{LastDiagnostics}"; return;
            }
            _lastSucceeded = true; _feedback!.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { LastDiagnostics = exception.Message; _feedback!.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}"; }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var quest = SelectedQuest()!; var profile = BuildProfile(); var candidate = quest.WithOperations(profile);
            var diagnostics = QuestOperationsValidator.Validate(candidate); var repeat = diagnostics.Count == 0
                ? QuestOperationsRuntimeResolver.ResolveRepeat(candidate, new(DateTimeOffset.UtcNow, null, 0, 1, 1, true, true, new HashSet<QuestTransitionId>())) : null;
            SummarySectionCount = 5; _summary.Text = $"[font_size=22][color=#ff9858]Priorité {profile.Journal.Priority}[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID {profile.Id:D} · schéma v{profile.SchemaVersion}[/color]\n\n" +
                $"[b]JOURNAL[/b]\nImportance {profile.Journal.NarrativeImportance} · épinglage {profile.Journal.AllowManualPin}\n\n" +
                $"[b]RÉPÉTITION[/b]\n{profile.Repeat.Enabled} · délai {profile.Repeat.CooldownSeconds}s · décision {repeat?.Code}\n\n" +
                $"[b]GROUPE[/b]\n{profile.Share.Mode} · synchro {profile.Share.SynchronizeProgress} · {profile.Share.Separation}\n\n" +
                $"[b]CHOIX & NOTIFICATION[/b]\n{profile.ExclusiveChoices.Count} exclusif(s) · {profile.Notification.Mode} · {profile.Notification.GroupKey}\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Profil valide et exécutable[/color]" :
                    $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n{string.Join('\n', diagnostics.Select(item => item.Code))}");
        }
        catch (Exception exception) { SummarySectionCount = 0; _summary.Text = $"[color=#ff7777]{exception.Message}[/color]"; }
    }

    private QuestDefinition? SelectedQuest() => _load?.Invoke()?.Project.Quests.SingleOrDefault(item => item.Id == _selectedQuestId);
    private void OnSelected(long index)
    {
        if (_quests is null || _load?.Invoke() is not { } state) return;
        var id = QuestId.Parse(_quests.GetItemMetadata((int)index).AsString()); LoadQuest(state.Project.Quests.Single(item => item.Id == id));
    }
    private void SetEmpty(string message)
    {
        _selectedQuestId = null; if (_feedback is not null) _feedback.Text = message;
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Aucune quête sélectionnée.[/color]";
    }
    private static Guid Stable(Guid root, string key) { var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{root:D}|{key}")); return new(hash.AsSpan(0, 16)); }
    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box); var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new("ffad70")); box.AddChild(label); return panel;
    }
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 8); scroll.AddChild(box); tabs.AddChild(scroll); return box;
    }
    private static Control Labeled(string label, Control control) { var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); box.AddChild(control); return box; }
    private static CheckBox Check(Container owner, string text) { var value = new CheckBox { Text = text }; owner.AddChild(value); return value; }
    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        var box = new VBoxContainer(); box.AddChild(new Label { Text = label });
        var value = new LineEdit { PlaceholderText = placeholder }; box.AddChild(value); owner.AddChild(box); return value;
    }
    private static SpinBox Spin(Container owner, string label, double min, double max, double value, double step)
    {
        var box = new VBoxContainer(); box.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step }; box.AddChild(spin); owner.AddChild(box); return spin;
    }
    private static OptionButton EnumSelect<T>() where T : struct, Enum { var result = new OptionButton(); foreach (var value in Enum.GetValues<T>()) result.AddItem(value.ToString()); return result; }
    private static void Select<T>(OptionButton option, T value) where T : struct, Enum => option.Select(Convert.ToInt32(value));
    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(140, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new("fff4ec")); button.Pressed += action; return button;
    }
}
#endif
