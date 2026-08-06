#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class RpgKitAuthoringWindow : Window
{
    private static readonly (string Name, string Detail)[] Domains =
    [
        ("CRÉATURES", "identité, combat, perception, comportement, loot"),
        ("FACTIONS", "adhésions, relations, réputation, hostilité"),
        ("ITEMS", "propriétés, inventaire, équipement, charges"),
        ("PLACEABLES & PORTES", "états, interaction, verrouillage, transition"),
        ("TRIGGERS & RENCONTRES", "volumes, conditions, vagues, difficulté"),
        ("MARCHANDS", "catalogue, prix, stock et transactions"),
        ("COMPAGNONS", "recrutement, groupe, ordres, loyauté"),
        ("DIALOGUES & QUÊTES", "graphes, conditions, journal, objectifs"),
        ("FORGESCRIPT", "API, compilation, diagnostics, tests et bindings"),
    ];

    private Func<ProjectState?>? _load;
    private Func<BlueprintId, string, string, BlueprintKind, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action? _openDialogues;
    private Action? _openScripts;
    private Action? _openCreaturePresentation;
    private Action? _openCreatureProgression;
    private Action? _openCreatureLoot;
    private Action? _openCreatureRoleBehavior;
    private Action? _openCreatureEvents;
    private Action? _openFactions;
    private Action<Window>? _applyIcon;
    private BlueprintId _draftId = BlueprintId.From(Guid.NewGuid());
    private OptionButton? _kind;
    private LineEdit? _slug;
    private LineEdit? _name;
    private Label? _metrics;
    private ItemList? _catalog;
    private RichTextLabel? _feedback;

    public void Initialize(Func<ProjectState?> load, Func<BlueprintId, string, string, BlueprintKind, bool, CommandResult> mutate, Action undo, Action redo, Action openDialogues, Action openScripts, Action openCreaturePresentation, Action openCreatureProgression, Action openCreatureLoot, Action openCreatureRoleBehavior, Action openCreatureEvents, Action openFactions, Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _openDialogues = openDialogues; _openScripts = openScripts; _openCreaturePresentation = openCreaturePresentation; _openCreatureProgression = openCreatureProgression; _openCreatureLoot = openCreatureLoot; _openCreatureRoleBehavior = openCreatureRoleBehavior; _openCreatureEvents = openCreatureEvents; _openFactions = openFactions; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Atelier RPG Kit · OpenRPG Forge"; MinSize = new(980, 650); Size = new(1180, 760); CloseRequested += Hide; Build(); Refresh();
    }

    public void Open()
    {
        Refresh(); PopupCentered(); _applyIcon?.Invoke(this);
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 22); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "ATELIER RPG KIT", TooltipText = "Toutes les mutations passent par les commandes du projet." }; title.AddThemeFontSizeOverride("font_size", 25); title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Créez le contenu jouable sans éditer le JSON. Prévisualisez chaque transaction avant de l'appliquer." });
        _metrics = new Label(); _metrics.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_metrics);
        var split = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [360] }; root.AddChild(split);
        var left = Panel("CAPACITÉS DISPONIBLES"); split.AddChild(left); _catalog = new ItemList { CustomMinimumSize = new(360, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; left.GetChild<VBoxContainer>(0).AddChild(_catalog);
        var right = Panel("CRÉER UN BLUEPRINT RPG"); split.AddChild(right); var form = right.GetChild<VBoxContainer>(0);
        form.AddChild(new Label { Text = "Type canonique" }); _kind = new OptionButton(); foreach (var kind in Enum.GetValues<BlueprintKind>()) _kind.AddItem(KindName(kind)); form.AddChild(_kind);
        form.AddChild(new Label { Text = "Identifiant lisible (slug)" }); _slug = new LineEdit { PlaceholderText = "ex. sentinelle-prismatique" }; form.AddChild(_slug);
        form.AddChild(new Label { Text = "Nom affiché" }); _name = new LineEdit { PlaceholderText = "ex. Sentinelle prismatique" }; form.AddChild(_name);
        var actions = new HBoxContainer(); form.AddChild(actions); AddButton(actions, "Prévisualiser", () => Mutate(true)); AddButton(actions, "Appliquer", () => Mutate(false), true); AddButton(actions, "Annuler", () => { _undo?.Invoke(); Refresh(); }); AddButton(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(); });
        var shortcuts = new HBoxContainer(); form.AddChild(shortcuts); AddButton(shortcuts, "Ouvrir Dialogues", () => _openDialogues?.Invoke()); AddButton(shortcuts, "Ouvrir Scripts & build", () => _openScripts?.Invoke());
        AddButton(shortcuts, "Présenter une créature", () => _openCreaturePresentation?.Invoke(), true);
        AddButton(shortcuts, "Progression & pouvoirs", () => _openCreatureProgression?.Invoke(), true);
        AddButton(shortcuts, "Butin, vol & cadavres", () => _openCreatureLoot?.Invoke(), true);
        AddButton(shortcuts, "Rôle, activité & auto-level", () => _openCreatureRoleBehavior?.Invoke(), true);
        AddButton(shortcuts, "Événements avancés", () => _openCreatureEvents?.Invoke(), true);
        AddButton(shortcuts, "Factions avancées", () => _openFactions?.Invoke(), true);
        _feedback = new RichTextLabel { BbcodeEnabled = true, FitContent = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 180) }; form.AddChild(_feedback);
    }

    public void Refresh()
    {
        var state = _load?.Invoke(); _catalog?.Clear(); foreach (var domain in Domains) _catalog?.AddItem($"{domain.Name}  ·  {domain.Detail}"); if (_metrics is null) return; _metrics.Text = state is null ? "Aucun projet ouvert" : $"Révision {state.Revision} · {state.Project.Blueprints.Count} blueprints · {state.Project.Dialogues.Count} dialogues · {state.Project.Quests.Count} quêtes · {state.Project.Areas.Sum(area => area.Entities.Count)} entités";
    }

    private void Mutate(bool dryRun)
    {
        if (_mutate is null || _kind is null || _slug is null || _name is null || _feedback is null) return; var result = _mutate(_draftId, _slug.Text, _name.Text, (BlueprintKind)_kind.Selected, dryRun); if (!result.Succeeded) { _feedback.Text = $"[color=#ff7777]Transaction refusée[/color]\n{string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"))}"; return; }
        _feedback.Text = dryRun ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}" : $"[color=#8ce0bd]APPLIQUÉ[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}"; if (!dryRun) { _draftId = BlueprintId.From(Guid.NewGuid()); _slug.Text = string.Empty; _name.Text = string.Empty; Refresh(); }
    }

    private static PanelContainer Panel(string title) { var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 9); panel.AddChild(box); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad70")); box.AddChild(label); return panel; }
    private static void AddButton(Container owner, string text, Action action, bool primary = false) { var button = new Button { Text = text, CustomMinimumSize = new(120, 36) }; if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec")); button.Pressed += action; owner.AddChild(button); }
    private static string KindName(BlueprintKind kind) => kind switch { BlueprintKind.Creature => "Créature", BlueprintKind.Item => "Objet", BlueprintKind.Placeable => "Élément plaçable", BlueprintKind.Door => "Porte", BlueprintKind.Trigger => "Déclencheur", BlueprintKind.PlayerSpawn => "Point de départ", _ => kind.ToString() };
}
#endif
