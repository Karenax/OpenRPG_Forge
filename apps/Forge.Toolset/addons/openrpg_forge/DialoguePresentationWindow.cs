#if TOOLS
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Sequences;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class DialoguePresentationWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<DialogueId, DialoguePresentationProfile?, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private OptionButton? _dialoguePicker;
    private CheckBox? _required;
    private TextEdit? _animations;
    private TextEdit? _voices;
    private TextEdit? _cameras;
    private TextEdit? _bookmarks;
    private TextEdit? _interjections;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private readonly List<DialogueId> _dialogueIds = [];
    private DialogueId? _dialogueId;
    private Guid _profileId;
    private bool _lastCommandSucceeded;

    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<DialogueId, DialoguePresentationProfile?, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Mise en scène des dialogues · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1420, 880);
        CloseRequested += Hide;
        Build();
        Refresh();
    }

    public void Open(DialogueId? dialogueId = null)
    {
        Refresh(dialogueId);
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public void Refresh(DialogueId? preferred = null)
    {
        var state = _load?.Invoke();
        if (state is null || state.Project.Dialogues.Count == 0)
        {
            SetEmpty("Aucun dialogue disponible dans le projet.");
            return;
        }

        _dialogueIds.Clear();
        _dialoguePicker!.Clear();
        foreach (var dialogue in state.Project.Dialogues.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            _dialogueIds.Add(dialogue.Id);
            _dialoguePicker.AddItem(dialogue.DisplayName);
        }
        var requested = preferred ?? _dialogueId;
        var selected = requested is null ? 0 : Math.Max(0, _dialogueIds.FindIndex(item => item == requested));
        _dialoguePicker.Selected = selected;
        LoadDialogue(state.Project.Dialogues.Single(item => item.Id == _dialogueIds[selected]));
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_dialogueId is null) return false;
        if (BuildProfile().AnimationCues.Count == 0) CreateStarterProfile();
        RebuildSummary();
        return SummarySectionCount == 5 &&
               DialoguePresentationValidator.Validate(CurrentDialogue().WithPresentation(BuildProfile())).Count == 0;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastCommandSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastCommandSucceeded; }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "MISE EN SCÈNE DES DIALOGUES" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Animations, voix, caméra, bookmarks de revue et interjections — données canoniques, aperçu transactionnel."
        });
        var chooser = new HBoxContainer(); chooser.AddThemeConstantOverride("separation", 10); root.AddChild(chooser);
        chooser.AddChild(new Label { Text = "Dialogue" });
        _dialoguePicker = new OptionButton { CustomMinimumSize = new Vector2(360, 38) };
        _dialoguePicker.ItemSelected += index => SelectDialogue((int)index); chooser.AddChild(_dialoguePicker);
        _required = new CheckBox { Text = "Profil requis par le runtime" }; chooser.AddChild(_required);
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd"));
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; chooser.AddChild(_status);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [900]
        };
        root.AddChild(split);
        var editor = Panel("PROFIL CANONIQUE"); split.AddChild(editor);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editor.GetChild<VBoxContainer>(0).AddChild(tabs);
        _animations = Multi(Tab(tabs, "ANIMATIONS"),
            "nœud|participant|animation|début ms|durée ms|fondu ms|geste|regarde|interruption|fallback", 300);
        _voices = Multi(Tab(tabs, "VOIX & SONS"),
            "nœud|participant|locale|ressource|prise|clé sous-titre|sous-titre|lipsync|émotion|durée ms|licence|sha256|fallback", 300);
        _cameras = Multi(Tab(tabs, "CAMÉRA"),
            "nœud|mode|ancre|focus|cadrage|fov|durée ms|fondu ms|bandes|contrôle joueur|mouvement réduit|marge|priorité", 300);
        _bookmarks = Multi(Tab(tabs, "BOOKMARKS"),
            "nœud|nom|portée Personal/Shared|commentaire de revue|auteur", 300);
        _interjections = Multi(Tab(tabs, "INTERJECTIONS"),
            "déclencheur|réplique|compagnon|traits séparés par ,|présent|relation min|priorité|cooldown s|groupe|conflit", 300);

        var previewPanel = Panel("APERÇU & DIFF"); split.AddChild(previewPanel);
        var preview = previewPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(390, 460)
        }; preview.AddChild(_summary);
        preview.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        preview.AddChild(Button("Créer un profil de départ", CreateStarterProfile));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(390, 150) };
        preview.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser la transaction", () => Mutate(true)));
        actions.AddChild(Button("Appliquer au projet", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void SelectDialogue(int index)
    {
        var state = _load?.Invoke();
        if (state is null || index < 0 || index >= _dialogueIds.Count) return;
        LoadDialogue(state.Project.Dialogues.Single(item => item.Id == _dialogueIds[index]));
    }

    private void LoadDialogue(DialogueDefinition dialogue)
    {
        _dialogueId = dialogue.Id;
        var profile = dialogue.Presentation;
        _profileId = profile?.Id ?? StableId(dialogue.Id.Value, "profile", dialogue.Slug, 0);
        _required!.ButtonPressed = profile?.RequiredAtRuntime ?? false;
        _animations!.Text = string.Join('\n', (profile?.AnimationCues ?? []).Select(item =>
            $"{item.NodeId}|{item.ParticipantBlueprintId}|{item.AnimationId}|{item.StartMilliseconds}|" +
            $"{item.DurationMilliseconds}|{item.BlendMilliseconds}|{item.Gesture}|{item.LookAtBlueprintId}|" +
            $"{item.InterruptionPolicy}|{item.FallbackAnimationId}"));
        _voices!.Text = string.Join('\n', (profile?.VoiceCues ?? []).Select(item =>
            $"{item.NodeId}|{item.ParticipantBlueprintId}|{item.Locale}|{item.Resource}|{item.Take}|" +
            $"{item.Subtitle.Key}|{item.Subtitle.Fallback}|{item.LipSyncProfileId}|{item.Emotion}|" +
            $"{item.DurationMilliseconds}|{item.LicenseSpdx}|{item.ContentSha256}|{Lower(item.AllowSubtitleFallback)}"));
        _cameras!.Text = string.Join('\n', (profile?.CameraShots ?? []).Select(item =>
            $"{item.NodeId}|{item.Mode}|{item.CameraAnchorId}|{item.FocusBlueprintId}|{item.Framing}|" +
            $"{Number(item.FieldOfViewDegrees)}|{item.DurationMilliseconds}|{item.BlendMilliseconds}|" +
            $"{Lower(item.CinematicBars)}|{Lower(item.AllowPlayerOverride)}|{Lower(item.ReduceMotionFallback)}|" +
            $"{Number(item.SafeAreaMargin)}|{item.Priority}"));
        _bookmarks!.Text = string.Join('\n', (profile?.Bookmarks ?? []).Select(item =>
            $"{item.NodeId}|{item.Name}|{item.Scope}|{item.ReviewComment}|{item.AuthorId}"));
        _interjections!.Text = string.Join('\n', (profile?.InterjectionRules ?? []).Select(item =>
            $"{item.TriggerNodeId}|{item.InterjectionNodeId}|{item.CompanionBlueprintId}|" +
            $"{string.Join(',', item.PersonalityTags)}|{Lower(item.RequirePresent)}|{Number(item.MinimumRelation)}|" +
            $"{item.Priority}|{item.CooldownSeconds}|{item.ConflictGroup}|{item.ConflictPolicy}"));
        _status!.Text = $"{dialogue.DisplayName} · {(profile is null ? "profil non configuré" : $"UUID {profile.Id:D}")}";
        _feedback!.Text = profile is null
            ? "[color=#b8a8ce]État vide : créez un profil de départ ou renseignez les onglets.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private void CreateStarterProfile()
    {
        var dialogue = CurrentDialogue();
        var node = dialogue.Nodes.First();
        var participant = node.SpeakerBlueprintId ?? _load!.Invoke()!.Project.Blueprints
            .First(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn).Id;
        _animations!.Text = $"{node.Id}|{participant}|talk.explain|0|1800|180|Explain||BlendOut|idle";
        _voices!.Text = $"{node.Id}|{participant}|fr-FR||take-01|{node.Text.Key}|{node.Text.Fallback}|viseme-neutral|attentive|1800|||true";
        _cameras!.Text = $"{node.Id}|Automatic||{participant}|Medium|52|1800|250|true|true|true|0.08|10";
        _bookmarks!.Text = $"{node.Id}|Revue {dialogue.DisplayName}|Shared|Valider le cadrage, le ton et le sous-titre.|creator";
        _interjections!.Text = string.Empty;
        RebuildSummary();
    }

    private DialoguePresentationProfile BuildProfile()
    {
        var dialogue = CurrentDialogue();
        const string provenance = "toolset:dialogue-presentation";
        var animations = Rows(_animations!.Text, 10, "animation").Select((p, i) =>
            new DialogueAnimationCue(StableId(_profileId, "animation", p[0], i), Node(p[0]), Blueprint(p[1]),
                Required(p[2], "animation"), Integer(p[3]), Integer(p[4]), Integer(p[5]),
                EnumValue<DialogueGesture>(p[6]), OptionalBlueprint(p[7]),
                EnumValue<DialogueAnimationInterruptionPolicy>(p[8]), Required(p[9], "fallback"), provenance)).ToArray();
        var voices = Rows(_voices!.Text, 13, "voix").Select((p, i) =>
            new DialogueVoiceCue(StableId(_profileId, "voice", $"{p[0]}:{p[2]}:{p[4]}", i), Node(p[0]),
                Blueprint(p[1]), Required(p[2], "locale"), Optional(p[3]), Required(p[4], "prise"),
                new LocalizedText(p[5], p[6]), Required(p[7], "lipsync"), Required(p[8], "émotion"),
                Integer(p[9]), Optional(p[10]), Optional(p[11]), Boolean(p[12]), provenance)).ToArray();
        var cameras = Rows(_cameras!.Text, 13, "caméra").Select((p, i) =>
            new DialogueCameraShot(StableId(_profileId, "camera", p[0], i), Node(p[0]),
                EnumValue<DialogueCameraShotMode>(p[1]), OptionalGuid(p[2]), OptionalBlueprint(p[3]),
                EnumValue<CameraFraming>(p[4]), Float(p[5]), Integer(p[6]), Integer(p[7]), Boolean(p[8]),
                Boolean(p[9]), Boolean(p[10]), Float(p[11]), Integer(p[12]), provenance)).ToArray();
        var bookmarks = Rows(_bookmarks!.Text, 5, "bookmark").Select((p, i) =>
            new DialogueBookmark(StableId(_profileId, "bookmark", $"{p[0]}:{p[1]}", i), Node(p[0]),
                Required(p[1], "nom"), EnumValue<DialogueBookmarkScope>(p[2]), Optional(p[3]),
                Required(p[4], "auteur"), $"dialogue://{dialogue.Id}/{Node(p[0])}", provenance)).ToArray();
        var interjections = Rows(_interjections!.Text, 10, "interjection").Select((p, i) =>
            new DialogueInterjectionRule(StableId(_profileId, "interjection", $"{p[0]}:{p[1]}", i),
                Node(p[0]), Node(p[1]), Blueprint(p[2]), Csv(p[3]), Boolean(p[4]), Double(p[5]), Integer(p[6]),
                Integer(p[7]), Required(p[8], "groupe"), EnumValue<DialogueInterjectionConflictPolicy>(p[9]),
                provenance)).ToArray();
        return new(_profileId, 1, provenance, _required!.ButtonPressed, true,
            animations, voices, cameras, bookmarks, interjections);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _dialogueId is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = DialoguePresentationValidator.Validate(CurrentDialogue().WithPresentation(profile));
            SummarySectionCount = 5;
            _summary.Text = $"[font_size=22][color=#ff9858]{CurrentDialogue().DisplayName}[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID {profile.Id:D} · schéma v1 · interjections serveur[/color]\n\n" +
                $"[b]ANIMATIONS[/b]\n{profile.AnimationCues.Count} cue(s), gestes, regards, fondus et fallback\n\n" +
                $"[b]VOIX & SONS[/b]\n{profile.VoiceCues.Count} prise(s), locales, sous-titres et licences\n\n" +
                $"[b]CAMÉRA[/b]\n{profile.CameraShots.Count} plan(s), accessibilité et priorité joueur\n\n" +
                $"[b]BOOKMARKS[/b]\n{profile.Bookmarks.Count} repère(s) personnel(s)/partagé(s)\n\n" +
                $"[b]INTERJECTIONS[/b]\n{profile.InterjectionRules.Count} règle(s), cooldown et conflits\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Profil valide et prêt à prévisualiser[/color]" :
                    $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n" +
                    string.Join('\n', diagnostics.Select(item => $"• {item.Code} · {item.Path}")));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private void Mutate(bool dryRun)
    {
        try
        {
            var result = _mutate?.Invoke(_dialogueId!, BuildProfile(), dryRun)
                ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
            _lastCommandSucceeded = result.Succeeded;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
            _feedback!.Text = result.Succeeded
                ? $"[color=#8ce0bd]{(dryRun ? "Aperçu valide" : "Profil appliqué")} · {LastPatchSummary}[/color]"
                : $"[color=#ff7777]{LastDiagnostics}[/color]";
            if (!dryRun && result.Succeeded) Refresh(_dialogueId);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
        {
            _lastCommandSucceeded = false;
            LastDiagnostics = exception.Message;
            _feedback!.Text = $"[color=#ff7777]{exception.Message}[/color]";
        }
    }

    private DialogueDefinition CurrentDialogue() => _load!.Invoke()!.Project.Dialogues
        .Single(item => item.Id == _dialogueId);
    private void SetEmpty(string message) { _status!.Text = message; _feedback!.Text = $"[color=#b8a8ce]{message}[/color]"; SummarySectionCount = 0; }
    private static Guid StableId(Guid root, string group, string value, int index) { var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}|{group}|{value}|{index}")); return new Guid(bytes[..16]); }
    private static IEnumerable<string[]> Rows(string text, int count, string label) { foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { var parts = line.Split('|', StringSplitOptions.TrimEntries); if (parts.Length != count) throw new FormatException($"Ligne {label} invalide ({count} colonnes attendues) : '{line}'."); yield return parts; } }
    private static DialogueNodeId Node(string value) => DialogueNodeId.Parse(Required(value, "nœud"));
    private static BlueprintId Blueprint(string value) => BlueprintId.Parse(Required(value, "blueprint"));
    private static BlueprintId? OptionalBlueprint(string value) => string.IsNullOrWhiteSpace(value) ? null : Blueprint(value);
    private static Guid? OptionalGuid(string value) => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
    private static string Required(string value, string label) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new FormatException($"{label} requis.");
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int Integer(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static float Float(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static double Double(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool Boolean(string value) => bool.TryParse(value, out var result) ? result : throw new FormatException($"Booléen invalide : '{value}'.");
    private static T EnumValue<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var result) ? result : throw new FormatException($"Valeur {typeof(T).Name} invalide : '{value}'.");
    private static string[] Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Lower(bool value) => value.ToString().ToLowerInvariant();
    private static PanelContainer Panel(string title) { var panel = new PanelContainer(); var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad72")); column.AddChild(label); return panel; }
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; form.AddThemeConstantOverride("separation", 8); scroll.AddChild(form); tabs.AddChild(scroll); return form; }
    private static TextEdit Multi(VBoxContainer parent, string label, float height) { parent.AddChild(new Label { Text = label }); var field = new TextEdit { CustomMinimumSize = new Vector2(0, height), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; parent.AddChild(field); return field; }
    private static Button Button(string text, Action pressed, bool primary = false) { var button = new Button { Text = text }; if (primary) button.AddThemeColorOverride("font_color", new Color("fff1e8")); button.Pressed += pressed; return button; }
}
#endif
