#if TOOLS
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class WorldFeatureStudioWindow : Window
{
    private readonly Dictionary<string, LineEdit> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpinBox> _number = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _check = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OptionButton> _choice = new(StringComparer.Ordinal);
    private Func<ProjectState?>? _load;
    private Func<WorldFeatureCatalog, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private TabContainer? _tabs;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private int _captureTab = 6;
    private string _captureMarker = "R037_COMPANION";
    private bool _lastSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SectionCount => 8;

    public void Initialize(Func<ProjectState?> load, Func<WorldFeatureCatalog, bool, CommandResult> mutate,
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
        Title = "Studio du monde vivant · OpenRPG Forge";
        MinSize = new(1180, 760);
        Size = new(1540, 940);
        CloseRequested += Hide;
        Build();
        Refresh();
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r037-companion-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r040-door-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r041-door-nav-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r042-encounter-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r045-merchant-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r046-placeable-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r047-placeable-events-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r048-trigger-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r049-trigger-validation-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            var triggerValidationCapture = captureArgument.StartsWith("--r049-trigger-validation-ui-capture=", StringComparison.Ordinal);
            var triggerCapture = captureArgument.StartsWith("--r048-trigger-ui-capture=", StringComparison.Ordinal);
            var placeableEventsCapture = captureArgument.StartsWith("--r047-placeable-events-ui-capture=", StringComparison.Ordinal);
            var placeableCapture = captureArgument.StartsWith("--r046-placeable-ui-capture=", StringComparison.Ordinal);
            var merchantCapture = captureArgument.StartsWith("--r045-merchant-ui-capture=", StringComparison.Ordinal);
            var encounterCapture = captureArgument.StartsWith("--r042-encounter-ui-capture=", StringComparison.Ordinal);
            var doorNavigationCapture = captureArgument.StartsWith("--r041-door-nav-ui-capture=", StringComparison.Ordinal);
            var doorCapture = doorNavigationCapture || captureArgument.StartsWith("--r040-door-ui-capture=", StringComparison.Ordinal);
            var prefix = triggerValidationCapture ? "--r049-trigger-validation-ui-capture=" :
                triggerCapture ? "--r048-trigger-ui-capture=" :
                placeableEventsCapture ? "--r047-placeable-events-ui-capture=" :
                placeableCapture ? "--r046-placeable-ui-capture=" :
                merchantCapture ? "--r045-merchant-ui-capture=" :
                encounterCapture ? "--r042-encounter-ui-capture=" :
                doorNavigationCapture ? "--r041-door-nav-ui-capture=" :
                doorCapture ? "--r040-door-ui-capture=" : "--r037-companion-ui-capture=";
            _capturePath = captureArgument[prefix.Length..];
            _captureTab = triggerValidationCapture || triggerCapture ? 2 : placeableEventsCapture || placeableCapture ? 0 : merchantCapture ? 5 : encounterCapture ? 3 : doorCapture ? 1 : 6;
            _captureMarker = triggerValidationCapture ? "R049_TRIGGER_VALIDATION" : triggerCapture ? "R048_TRIGGER" : placeableEventsCapture ? "R047_PLACEABLE_EVENTS" : placeableCapture ? "R046_PLACEABLE" : merchantCapture ? "R045_MERCHANT" : encounterCapture ? "R042_ENCOUNTER" : doorNavigationCapture ? "R041_DOOR_NAV" :
                doorCapture ? "R040_DOOR" : "R037_COMPANION";
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            ConfigureForSmokeTest();
            if (_tabs is not null) _tabs.CurrentTab = _captureTab;
            _captureOpened = true;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = GetViewport().GetTexture().GetImage().SavePng(destination);
        GD.Print($"OPENRPG_FORGE_{_captureMarker}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
        _capturePath = null;
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
        if (state is null || _choice.Count == 0) return;
        PopulateBlueprints(_choice["placeable.target"], state, BlueprintKind.Placeable);
        PopulateBlueprints(_choice["door.target"], state, BlueprintKind.Door);
        PopulateBlueprints(_choice["trigger.target"], state, BlueprintKind.Trigger);
        PopulateCreatures(_choice["merchant.target"], state);
        PopulateBlueprints(_choice["merchant.stockItem"], state, BlueprintKind.Item);
        PopulateCreatures(_choice["companion.target"], state);
        PopulateCreatures(_choice["encounter.candidate"], state);
        PopulateAreas(_choice["trigger.area"], state);
        PopulateAreas(_choice["encounter.area"], state);
        PopulateAreas(_choice["vehicle.area"], state);
        var dialogue = state.Project.Dialogues.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        var quest = state.Project.Quests.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        foreach (var key in new[] { "placeable.dialogue", "door.dialogue", "trigger.dialogue", "companion.dialogue" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = dialogue;
        foreach (var key in new[] { "trigger.quest", "companion.quest" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = quest;
        if (string.IsNullOrWhiteSpace(_text["companion.areas"].Text))
            _text["companion.areas"].Text = string.Join(",", state.Project.Areas.Select(item => item.Id.ToString()));
        RebuildSummary();
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_choice["placeable.target"].ItemCount == 0 || _choice["door.target"].ItemCount == 0 ||
            _choice["trigger.target"].ItemCount == 0 || _choice["merchant.target"].ItemCount == 0 ||
            _choice["companion.target"].ItemCount == 0 || _choice["encounter.area"].ItemCount == 0 ||
            _choice["encounter.candidate"].ItemCount == 0)
            return false;
        _text["placeable.faction"].Text = "ash-watch";
        _text["placeable.key"].Text = "key:ember";
        _check["placeable.foundationRequired"].ButtonPressed = true;
        _check["placeable.onUsedRequired"].ButtonPressed = true;
        _text["placeable.apiVersion"].Text = "1.0.0";
        _text["placeable.eventBinding"].Text = "InteractionExecuted";
        _text["placeable.locks"].Text = "lock:crypt-pillar";
        _text["placeable.traps"].Text = "trap:ember";
        _text["placeable.inventories"].Text = "inventory:crypt-pillar";
        _text["placeable.physicsProfiles"].Text = "physics:rigid-placeable";
        _text["placeable.slug"].Text = "crypt-pillar";
        _text["placeable.name"].Text = "Pilier des cendres";
        _text["placeable.asset"].Text = "assets/placeables/crypt-pillar.glb";
        _choice["placeable.role"].Select(0);
        _number["placeable.positionX"].Value = 1.25;
        _number["placeable.positionY"].Value = 0;
        _number["placeable.positionZ"].Value = -2.5;
        _check["placeable.physics"].ButtonPressed = true;
        _number["placeable.mass"].Value = 12;
        _text["door.loading"].Text = "assets/loading/crypt-of-ashes.png";
        _number["door.health"].Value = 40;
        _text["trigger.key"].Text = "key:ember";
        _check["trigger.foundationRequired"].ButtonPressed = true;
        _text["trigger.slug"].Text = "crypt-threshold";
        _text["trigger.conditions"].Text = "crypt.open";
        _text["trigger.portalAnchor"].Text = "anchor:crypt-exit";
        _text["trigger.onEnterScript"].Text = "scripts/trigger_enter";
        _text["trigger.onExitScript"].Text = "scripts/trigger_exit";
        _check["trigger.validationRequired"].ButtonPressed = true;
        _text["trigger.knownTargets"].Text = "encounter:crypt-warden";
        _text["trigger.knownScripts"].Text = "scripts/trigger_click,scripts/trigger_enter,scripts/trigger_exit";
        _text["trigger.accessibleLayers"].Text = "characters";
        _check["trigger.rest"].ButtonPressed = true;
        _check["trigger.director"].ButtonPressed = true;
        _text["encounter.faction"].Text = "ash-hostile";
        _number["encounter.alert"].Value = 2;
        _text["merchant.currency"].Text = "ember-crown";
        _check["merchant.ledger"].ButtonPressed = true;
        _check["merchant.transactionsRequired"].ButtonPressed = true;
        _text["merchant.vendorInteraction"].Text = "interaction:trade";
        _text["merchant.dialogueChoice"].Text = "choice:trade";
        _text["merchant.variant"].Text = "standard";
        _number["merchant.stockQuantity"].Value = 12;
        _number["merchant.unitPrice"].Value = 5;
        _text["merchant.locales"].Text = "fr-FR,en-US";
        _text["merchant.restrictions"].Text = "quest:merchant-open";
        _text["merchant.onOpen"].Text = "scripts/merchant_open";
        _text["companion.formation"].Text = "wedge";
        _check["companion.direct"].ButtonPressed = true;
        _check["companion.recruitDialogue"].ButtonPressed = true;
        _check["companion.recruitScript"].ButtonPressed = true;
        _text["companion.recruitScriptId"].Text = "scripts/companion_recruit";
        _text["companion.commands"].Text = "follow,hold,attack,interact";
        _text["companion.contexts"].Text = "recruit,owner";
        _check["companion.crossZone"].ButtonPressed = (_load?.Invoke()?.Project.Areas.Count ?? 0) > 1;
        _text["script.tag"].Text = "door.ash";
        _text["script.event"].Text = "opened";
        _text["script.route"].Text = "scripts/door_ash_opened";
        RebuildSummary();
        return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "STUDIO DU MONDE VIVANT" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Placeables, portes, triggers, rencontres, routes, marchands, compagnons et scripts — un seul document canonique, prévisualisable et annulable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [990] };
        root.AddChild(split);
        var tabsPanel = Panel("CONFIGURATION SANS JSON");
        split.AddChild(tabsPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _tabs = tabs;
        tabsPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildPlaceables(Tab(tabs, "PLACEABLES"));
        BuildDoors(Tab(tabs, "PORTES"));
        BuildTriggers(Tab(tabs, "TRIGGERS"));
        BuildEncounters(Tab(tabs, "RENCONTRES"));
        BuildVehicles(Tab(tabs, "ROUTES"));
        BuildMerchants(Tab(tabs, "MARCHANDS"));
        BuildCompanions(Tab(tabs, "COMPAGNONS"));
        BuildScripts(Tab(tabs, "SCRIPTS"));
        var previewPanel = Panel("APERÇU SÉMANTIQUE");
        split.AddChild(previewPanel);
        var preview = previewPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(360, 480),
        };
        preview.AddChild(_summary);
        preview.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(360, 150) };
        preview.AddChild(_feedback);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildPlaceables(VBoxContainer form)
    {
        Section(form, "FONDATION PLACEABLE", "Blueprint composable, role exclusif, apparence et transform independant du moteur.");
        Section(form, "ONUSED ET VALIDATION", "Evenement type et controles collision, navigation, animation, locks, traps, inventaire, scripts, physique et budgets.");
        Toggle(form, "placeable.onUsedRequired", "OnUsed requis au runtime", true);
        Text(form, "placeable.apiVersion", "API ForgeScript", "1.0.0");
        Text(form, "placeable.eventBinding", "Binding evenement", "InteractionExecuted");
        Text(form, "placeable.beforeScript", "Script avant", "scripts/placeable_before_used");
        Text(form, "placeable.afterScript", "Script apres", "scripts/placeable_after_used");
        Toggle(form, "placeable.hasLock", "Composant lock", true);
        Text(form, "placeable.locks", "References locks", "lock:crypt-pillar");
        Toggle(form, "placeable.hasTrap", "Composant trap", true);
        Text(form, "placeable.traps", "References traps", "trap:ember");
        Toggle(form, "placeable.hasInventory", "Composant inventaire", true);
        Text(form, "placeable.inventories", "References inventaires", "inventory:crypt-pillar");
        Text(form, "placeable.physicsProfiles", "Profils physiques", "physics:rigid-placeable");
        Number(form, "placeable.drawBudget", "Budget draw calls", 1, 100000, 8, 1);
        Number(form, "placeable.scriptBudget", "Budget scripts", 0, 100000, 8, 1);
        Target(form, "placeable.target", "Blueprint placeable");
        Toggle(form, "placeable.foundationRequired", "Fondation requise au runtime", true);
        Text(form, "placeable.slug", "Slug stable", "crypt-pillar");
        Text(form, "placeable.locale", "Locale", "fr-FR");
        Text(form, "placeable.name", "Nom localise", "Pilier des cendres");
        Text(form, "placeable.prompt", "Prompt localise", "Examiner");
        Text(form, "placeable.asset", "Asset visuel", "assets/placeables/crypt-pillar.glb");
        Text(form, "placeable.mesh", "Mesh", "mesh:crypt-pillar");
        Text(form, "placeable.icon", "Icone", "assets/icons/crypt-pillar.png");
        Text(form, "placeable.portrait", "Portrait", "assets/portraits/crypt-pillar.png");
        Text(form, "placeable.tags", "Tags", "crypt,ash,architecture");
        Text(form, "placeable.notes", "Notes auteur", "Pilier interactif de la crypte");
        Choice(form, "placeable.role", "Role", ["Objet utilisable", "Decor statique optimise"]);
        Section(form, "INTERACTION, COLLISION ET COMPOSANTS", "Verbe, focus, autorite, collision et scripts sont valides avant le build.");
        Text(form, "placeable.verb", "Label du verbe", "placeable.examine");
        Text(form, "placeable.event", "Evenement OnUsed", "placeable-used");
        Number(form, "placeable.distance", "Distance", .1, 100, 2.5, .1);
        Number(form, "placeable.angle", "Angle", 1, 360, 120, 1);
        Toggle(form, "placeable.focus", "Focus requis", true);
        Number(form, "placeable.duration", "Duree", 0, 3600, 1, .1);
        Text(form, "placeable.authority", "Autorite", "server");
        Text(form, "placeable.conditions", "Conditions", "quest:crypt-open");
        Toggle(form, "placeable.collision", "Collision active", true);
        Toggle(form, "placeable.navObstacle", "Obstacle navigation", true);
        Text(form, "placeable.script", "Script OnUsed", "scripts/placeable_used");
        Section(form, "TRANSFORM PRECIS", "Coordonnees numeriques, pivot, snap, parentage, surface et animation partagent le document canonique.");
        Number(form, "placeable.positionX", "Position X", -1000000, 1000000, 0, .05);
        Number(form, "placeable.positionY", "Position Y", -1000000, 1000000, 0, .05);
        Number(form, "placeable.positionZ", "Position Z", -1000000, 1000000, 0, .05);
        Number(form, "placeable.rotationX", "Rotation X", -36000, 36000, 0, 1);
        Number(form, "placeable.rotationY", "Rotation Y", -36000, 36000, 45, 1);
        Number(form, "placeable.rotationZ", "Rotation Z", -36000, 36000, 0, 1);
        Number(form, "placeable.scaleX", "Echelle X", .01, 100, 1, .05);
        Number(form, "placeable.scaleY", "Echelle Y", .01, 100, 1, .05);
        Number(form, "placeable.scaleZ", "Echelle Z", .01, 100, 1, .05);
        Number(form, "placeable.pivotX", "Pivot X", -1000000, 1000000, 0, .05);
        Number(form, "placeable.pivotY", "Pivot Y", -1000000, 1000000, .5, .05);
        Number(form, "placeable.pivotZ", "Pivot Z", -1000000, 1000000, 0, .05);
        Number(form, "placeable.snap", "Snap", 0, 10000, .25, .05);
        Text(form, "placeable.parent", "Parent UUID optionnel", string.Empty);
        Toggle(form, "placeable.alignSurface", "Aligner sur la surface", true);
        Text(form, "placeable.transformAnimation", "Animation de transform", "animation:crypt-pillar-rise");
        Text(form, "placeable.overrides", "Overrides explicites", "material=ash-stone,variant=lit");
        Info(form, "Le viewport affiche pivot et snap ; Previsualiser n'ecrit rien, Appliquer conserve undo/redo.");
        Section(form, "SOCIAL, EVENEMENTS ET PHYSIQUE", "Les comportements monde vivant restent dans le meme profil canonique.");
        Text(form, "placeable.faction", "Faction / ownership", "ash-watch");
        Text(form, "placeable.dialogue", "Dialogue lié (UUID)", string.Empty);
        Text(form, "placeable.key", "Credential / clé", "key:ember");
        Choice(form, "placeable.consume", "Politique de clé", ["Conserver", "Consommer", "Charges", "Partagée", "Permission virtuelle"]);
        Text(form, "placeable.attack", "Script attaque physique", "scripts/placeable_attack");
        Text(form, "placeable.ability", "Script capacité reçue", "scripts/placeable_ability");
        Text(form, "placeable.custom", "Namespace événement utilisateur", "placeable.ash");
        Number(form, "placeable.interval", "Heartbeat (secondes)", 0.1, 3600, 5, .5);
        Toggle(form, "placeable.physics", "Physique dynamique et réplication serveur", true);
        Number(form, "placeable.mass", "Masse", .1, 100000, 12, .5);
    }

    private void BuildDoors(VBoxContainer form)
    {
        Target(form, "door.target", "Blueprint porte");
        Choice(form, "door.placement", "Placement", ["Socket architecture", "Placement libre"]);
        Text(form, "door.socket", "Socket architecture", "socket:crypt-arch");
        Section(form, "NAVIGATION DYNAMIQUE", "Obstacle et lien suivent l'état et la progression de l'animation.");
        Text(form, "door.navObstacle", "Obstacle navigation", "nav-obstacle:crypt-door");
        Text(form, "door.navLink", "Lien navigation", "nav-link:crypt-door");
        Number(form, "door.openingPassable", "Ouverture praticable à", 0, 1, .75, .05);
        Number(form, "door.closingBlocked", "Fermeture bloquante à", 0, 1, .25, .05);
        Toggle(form, "door.navVisualize", "Visualiser obstacle et lien dans le viewport", true);
        Choice(form, "door.portalKind", "Destination typée", ["Porte", "Trigger", "Waypoint", "Point de départ"]);
        Text(form, "door.spawnAnchor", "Ancre de destination", "anchor:crypt-exit");
        Text(form, "door.permission", "Permission de transition", "portal.travel");
        Toggle(form, "door.oneWay", "Transition unidirectionnelle", false);
        Number(form, "door.health", "Points de structure", 1, 100000, 40, 1);
        Number(form, "door.hardness", "Dureté", 0, 10000, 4, .5);
        Text(form, "door.loading", "Image de chargement gouvernée", "assets/loading/crypt-of-ashes.png");
        Choice(form, "door.assembly", "Assemblage", ["Simple", "Double", "Herse", "Ascenseur", "Sas"]);
        Text(form, "door.faction", "Faction / ownership", "ash-watch");
        Text(form, "door.dialogue", "Dialogue lié (UUID)", string.Empty);
        Text(form, "door.damageScript", "Script dégâts / destruction", "scripts/door_damage");
        Text(form, "door.attackScript", "Script attaque physique", "scripts/door_attack");
        Text(form, "door.abilityScript", "Script capacité reçue", "scripts/door_ability");
        Text(form, "door.custom", "Namespace événement utilisateur", "door.ash");
    }

    private void BuildTriggers(VBoxContainer form)
    {
        Target(form, "trigger.target", "Blueprint trigger");
        Target(form, "trigger.area", "Zone propriétaire");
        Section(form, "VOLUME CANONIQUE", "Polygone 2D extrude ou forme 3D, sommets, trous, snap, hauteur, transform et overlay runtime.");
        Toggle(form, "trigger.foundationRequired", "Fondation requise au runtime", true);
        Text(form, "trigger.slug", "Identifiant lisible", "crypt-threshold");
        Choice(form, "trigger.shape", "Forme", ["Polygone 2D extrude", "Boite 3D", "Sphere 3D", "Capsule 3D", "Enveloppe convexe 3D"]);
        Number(form, "trigger.height", "Hauteur", .1, 10000, 3, .25);
        Number(form, "trigger.snap", "Pas de grille", 0, 1000, .25, .05);
        Toggle(form, "trigger.visualize", "Afficher l'overlay dans le viewport", true);
        Section(form, "VALIDATION PRE-BUILD", "Auto-intersections, references, scripts, portail et accessibilite sont controles sur le document canonique.");
        Toggle(form, "trigger.validationRequired", "Contexte de validation requis au runtime", true);
        Text(form, "trigger.knownTargets", "Cibles resolues", "encounter:crypt-warden");
        Text(form, "trigger.knownScripts", "Scripts resolus", "scripts/trigger_click,scripts/trigger_enter,scripts/trigger_exit");
        Text(form, "trigger.accessibleLayers", "Layers accessibles", "characters");
        Section(form, "FILTRES, ACTIONS ET TRANSITION", "Entites, tags, layers, conditions, cooldown, fire policy et actions typees.");
        Text(form, "trigger.entityKinds", "Types d'entite", "creature");
        Text(form, "trigger.requiredTags", "Tags requis", "player");
        Text(form, "trigger.excludedTags", "Tags exclus", "undead");
        Text(form, "trigger.layers", "Layers", "characters");
        Text(form, "trigger.conditions", "Conditions", "crypt.open");
        Number(form, "trigger.cooldown", "Cooldown (secondes)", 0, 86400, 5, .5);
        Choice(form, "trigger.fire", "Politique de tir", ["Une fois", "Une fois par entite", "Repeter", "Pendant la presence"]);
        Text(form, "trigger.actionKind", "Action typee", "start-encounter");
        Text(form, "trigger.actionTarget", "Cible de l'action", "encounter:crypt-warden");
        Toggle(form, "trigger.portal", "Transition inter-zone", true);
        Choice(form, "trigger.portalKind", "Destination typee", ["Porte", "Trigger", "Waypoint", "Point de depart"]);
        Text(form, "trigger.portalAnchor", "Ancre d'arrivee", "anchor:crypt-exit");
        Section(form, "EVENEMENTS FORGESCRIPT", "VolumeEntered transporte entite, point, direction, layers et state ; VolumeExited ajoute duree et raison.");
        Text(form, "trigger.apiVersion", "Version API ForgeScript", "1.0.0");
        Text(form, "trigger.onEnterScript", "Script apres OnEnter", "scripts/trigger_enter");
        Text(form, "trigger.onExitScript", "Script apres OnExit", "scripts/trigger_exit");
        Number(form, "trigger.discovery", "Difficulté de découverte", 0, 100, 10, 1);
        Text(form, "trigger.key", "Credential requis", "key:ember");
        Text(form, "trigger.click", "Script OnClick", "scripts/trigger_click");
        Number(form, "trigger.interval", "Heartbeat occupé (secondes)", .1, 3600, 2, .5);
        Text(form, "trigger.custom", "Namespace événement utilisateur", "trigger.ash");
        Toggle(form, "trigger.rest", "Volume de repos sûr", true);
        Text(form, "trigger.quest", "Quête de piste (UUID)", string.Empty);
        Text(form, "trigger.dialogue", "Dialogue d’interjection (UUID)", string.Empty);
        Toggle(form, "trigger.director", "Autoriser simulation manuelle auditée", true);
    }

    private void BuildEncounters(VBoxContainer form)
    {
        Target(form, "encounter.area", "Zone de rencontre");
        Section(form, "VOLUME D'ACTIVATION", "Géométrie 2D/3D, filtres, ligne de vue et script partagent un contrat canonique.");
        Choice(form, "encounter.shape", "Forme", ["Polygone 2D extrudé", "Boîte 3D", "Sphère 3D", "Capsule 3D"]);
        Number(form, "encounter.height", "Hauteur", .5, 1000, 4, .5);
        Number(form, "encounter.radius", "Rayon", .5, 1000, 8, .5);
        Text(form, "encounter.tags", "Tags d'entités requis", "player");
        Text(form, "encounter.allowedFactions", "Factions autorisées", "party");
        Toggle(form, "encounter.lineOfSight", "Exiger une ligne de vue", true);
        Number(form, "encounter.threat", "Rayon de menace", 0, 10000, 18, .5);
        Text(form, "encounter.activationScript", "Script d'activation", "scripts/encounter_activate");
        Toggle(form, "encounter.visualize", "Visualiser le volume dans le viewport", true);
        Section(form, "POOL DE CRÉATURES", "Blueprints pondérés, rôles, coûts, conditions, variantes et limites réseau.");
        Target(form, "encounter.candidate", "Blueprint créature candidate");
        Choice(form, "encounter.role", "Rôle", ["Brute", "Escarmouche", "Contrôle", "Soutien", "Boss", "Ambiant"]);
        Number(form, "encounter.weight", "Poids", .5, 10000, 3, .5);
        Number(form, "encounter.cost", "Coût de budget", .5, 10000, 8, .5);
        Number(form, "encounter.maximumAlive", "Maximum vivant", 1, 1000, 4, 1);
        Text(form, "encounter.conditions", "Conditions", "area.occupied");
        Text(form, "encounter.variants", "Variantes", "ember");
        Number(form, "encounter.budget", "Budget de spawn", 1, 100000, 36, 1);
        Text(form, "encounter.anchors", "Ancres de spawn", "anchor:crypt-north");
        Text(form, "encounter.rewards", "Récompenses", "reward:ember-shard");
        Number(form, "encounter.networkCap", "Cap d'acteurs réseau", 1, 10000, 12, 1);
        Number(form, "encounter.respawn", "Délai de respawn (secondes)", 0, 86400, 30, 1);
        Number(form, "encounter.cycles", "Cycles maximum", 0, 100, 3, 1);
        Text(form, "encounter.faction", "Faction de spawn", "ash-hostile");
        Number(form, "encounter.interval", "Heartbeat (secondes)", .1, 3600, 5, .5);
        Number(form, "encounter.alert", "Niveau d’alerte des renforts", 0, 100, 2, 1);
        Number(form, "encounter.cap", "Cap de renforts", 1, 1000, 4, 1);
        Choice(form, "encounter.visibility", "Spawn hors vue", ["Caché", "Derrière couverture", "Portail", "Effet", "Visible autorisé"]);
        Number(form, "encounter.cleanup", "Nettoyage abandon (secondes)", 0, 86400, 120, 1);
    }

    private void BuildVehicles(VBoxContainer form)
    {
        Target(form, "vehicle.area", "Zone de route");
        Number(form, "vehicle.speed", "Limite de vitesse", .1, 500, 8, .5);
        Number(form, "vehicle.width", "Largeur de voie", .5, 100, 3, .5);
        Number(form, "vehicle.stop", "Arrêt terminal (secondes)", 0, 3600, 5, .5);
        Toggle(form, "vehicle.overtake", "Autoriser le dépassement", false);
        Choice(form, "vehicle.sync", "Synchronisation", ["Snapshots serveur", "Prédiction bornée", "Locale"]);
        Info(form, "La route initiale relie deux points éditables dans le viewport ; lanes, vitesse, arrêt et politique réseau sont persistés.");
    }

    private void BuildMerchants(VBoxContainer form)
    {
        Section(form, "TRANSACTIONS ATOMIQUES", "Valider fonds, propriete, stock et capacite avant une mutation unique avec rollback exact.");
        Target(form, "merchant.target", "Blueprint du marchand");
        Text(form, "merchant.currency", "Monnaie principale", "ember-crown");
        Section(form, "STOCK, BINDINGS ET VALIDATION", "Identite stable, localisation, restrictions, scripts et autorite serveur sont verifies avant le build.");
        Toggle(form, "merchant.transactionsRequired", "Transactions requises au runtime", true);
        Text(form, "merchant.vendorInteraction", "Interaction marchand", "interaction:trade");
        Text(form, "merchant.dialogueChoice", "Choix de dialogue", "choice:trade");
        Target(form, "merchant.stockItem", "Blueprint item en stock");
        Text(form, "merchant.variant", "Variante", "standard");
        Number(form, "merchant.stockQuantity", "Quantite", 0, 1000000, 12, 1);
        Number(form, "merchant.unitPrice", "Prix unitaire", 0, 100000000, 5, 1);
        Text(form, "merchant.locales", "Locales", "fr-FR,en-US");
        Text(form, "merchant.restrictions", "Restrictions", "quest:merchant-open");
        Text(form, "merchant.onOpen", "Script onOpen", "scripts/merchant_open");
        Number(form, "merchant.buyerCapacity", "Capacite acheteur", 1, 1000000, 100, 1);
        Number(form, "merchant.sellerCapacity", "Capacite vendeur", 1, 1000000, 1000, 1);
        Info(form, "Previsualiser n'ecrit rien ; Appliquer passe par la commande canonique et conserve undo/redo.");
        Number(form, "merchant.balance", "Trésorerie", 0, 100000000, 1000, 10);
        Number(form, "merchant.cap", "Prix maximal d’achat", 0, 100000000, 500, 10);
        Toggle(form, "merchant.stolen", "Accepter les biens volés hors juridiction", false);
        Number(form, "merchant.restock", "Réapprovisionnement (heures monde)", .1, 10000, 24, 1);
        Number(form, "merchant.discount", "Remise de négociation maximale (%)", 0, 100, 20, 1);
        Toggle(form, "merchant.market", "Marché dynamique régional", true);
        Text(form, "merchant.service", "Service vendu", "repair");
        Toggle(form, "merchant.ledger", "Journal économique et anomalies", true);
    }

    private void BuildCompanions(VBoxContainer form)
    {
        Section(form, "VALIDATION DU COMPAGNON", "Un contrat canonique unique, inspectable et testable avant le build.");
        Target(form, "companion.target", "Blueprint du compagnon");
        var preview = new PanelContainer { CustomMinimumSize = new(0, 86) };
        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 14);
        preview.AddChild(previewRow);
        previewRow.AddChild(new ColorRect { Color = new("ff9858"), CustomMinimumSize = new(58, 58) });
        var previewText = new Label
        {
            Text = "APERÇU DE RÈGLES\nRecrutement gouverné · ordres bornés · survie · suivi inter-zones",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        previewText.AddThemeColorOverride("font_color", new("dccbef"));
        previewRow.AddChild(previewText);
        form.AddChild(preview);

        Section(form, "RECRUTEMENT & PROPRIÉTÉ", "Les chemins de recrutement et l’autorité sont validés ensemble.");
        Toggle(form, "companion.recruitDialogue", "Autoriser le recrutement par dialogue", true);
        Toggle(form, "companion.recruitScript", "Autoriser le recrutement par ForgeScript", true);
        Text(form, "companion.recruitScriptId", "Script de recrutement", "scripts/companion_recruit");
        Text(form, "companion.currency", "Monnaie", "ember-crown");
        Number(form, "companion.cost", "Coût", 0, 100000000, 25, 1);
        Number(form, "companion.reputation", "Réputation minimale", -1, 1, .25, .05);
        Choice(form, "companion.ownership", "Propriété", ["Groupe", "Individuelle", "Partagée", "IA seule", "Maître de jeu"]);
        Text(form, "companion.authority", "Autorité", "server");
        Toggle(form, "companion.transfer", "Autoriser le transfert de propriété", true);

        Section(form, "COMMANDES, ÉQUIPEMENT & CYCLE DE VIE", "Les bindings sont stables, uniques et validés avant exécution.");
        Text(form, "companion.commands", "Commandes (virgules)", "follow,hold,attack,interact");
        Text(form, "companion.inventory", "Inventaire", "companion.inventory");
        Text(form, "companion.equipment", "Politique d’équipement", "companion.standard");
        Text(form, "companion.slots", "Slots d’équipement", "main-hand,off-hand,armor");
        Choice(form, "companion.deathMode", "Politique de mort", ["Corps récupérable", "À terre", "Mort permanente", "Résurrection", "Remplacement", "Mémorial"]);
        Text(form, "companion.deathPolicy", "Binding de mort", "companion.recoverable");
        Number(form, "companion.recovery", "Récupération (secondes)", 0, 86400, 30, 1);
        Text(form, "companion.contexts", "Contextes de dialogue", "recruit,owner");

        Section(form, "NAVIGATION INTER-ZONES", "Les destinations doivent appartenir au graphe de portails ou disposer d’un repli sûr.");
        Toggle(form, "companion.crossZone", "Activer le suivi inter-zones", false);
        Text(form, "companion.areas", "Zones autorisées (UUID, virgules)", string.Empty);
        Toggle(form, "companion.followPortals", "Suivre les portails authored", true);
        Toggle(form, "companion.safeFallback", "Autoriser un repli sûr", true);
        Text(form, "companion.fallbackAnchor", "Ancre de repli", "party.safe");
        Number(form, "companion.timeout", "Timeout de navigation (secondes)", .1, 3600, 15, .5);
        Toggle(form, "companion.required", "Validation obligatoire au runtime", true);

        Section(form, "SOCIAL, FORMATION & PROGRESSION", "Les comportements déjà livrés restent dans le même profil canonique.");
        Text(form, "companion.dialogue", "Dialogue social / banter (UUID)", string.Empty);
        Text(form, "companion.formation", "Formation", "wedge");
        Number(form, "companion.slot", "Slot de formation", 0, 32, 1, 1);
        Number(form, "companion.level", "Niveau actuel", 1, 100, 4, 1);
        Number(form, "companion.loyalty", "Loyauté", -1, 1, .5, .05);
        Text(form, "companion.quest", "Quête personnelle (UUID)", string.Empty);
        Toggle(form, "companion.traps", "Détecter et désamorcer les pièges", true);
        Text(form, "companion.abilities", "Capacités autorisées (virgules)", "heal,shield");
        Toggle(form, "companion.direct", "Contrôle direct / possession", true);
        Toggle(form, "companion.split", "Réunion du groupe et synchro des quêtes", true);
    }

    private void BuildScripts(VBoxContainer form)
    {
        Text(form, "script.rig", "Rig caméra", "rig.dialogue");
        Text(form, "script.shot", "Plan de cutscene", "shot.door");
        Number(form, "script.duration", "Durée du plan", .1, 3600, 3, .1);
        Toggle(form, "script.lock", "Verrouiller le contrôle joueur pendant le plan", true);
        Text(form, "script.tag", "Tag explicite de blueprint", "door.ash");
        Text(form, "script.event", "Événement routé", "opened");
        Text(form, "script.route", "Script appelé", "scripts/door_ash_opened");
        Text(form, "script.flag", "Feature flag nommé", "rest.enabled");
        Toggle(form, "script.flagDefault", "Valeur par défaut du flag", true);
        Text(form, "script.ai", "Réglage IA nommé", "combat.aggression");
        Choice(form, "script.aiDefault", "Valeur IA par défaut", ["Passive", "Équilibrée", "Agressive"]);
    }

    private WorldFeatureCatalog BuildCatalog()
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var current = state.Project.Module.WorldFeatures;
        var moduleId = state.Project.Module.Id.Value;
        var placeables = current.Placeables;
        if (SelectedBlueprint("placeable.target") is { } placeableId)
        {
            var consumption = (PlaceableWorldProfile.CredentialConsumption)_choice["placeable.consume"].Selected;
            var staticRole = _choice["placeable.role"].Selected == 1;
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Stable(moduleId, $"placeable-foundation:{placeableId}"), 1, "toolset:r046",
                _text["placeable.slug"].Text.Trim(),
                [new(_text["placeable.locale"].Text.Trim(), _text["placeable.name"].Text.Trim(),
                    _text["placeable.prompt"].Text.Trim())], Csv(_text["placeable.tags"].Text),
                _text["placeable.notes"].Text.Trim(),
                new(_text["placeable.asset"].Text.Trim(), [_text["placeable.mesh"].Text.Trim()], [],
                    _text["placeable.icon"].Text.Trim(), _text["placeable.portrait"].Text.Trim(), []),
                new("box", new(1, 1.5f, .5f), _check["placeable.collision"].ButtonPressed,
                    _check["placeable.navObstacle"].ButtonPressed, "world"),
                staticRole ? null : new(
                    [new(Stable(moduleId, $"placeable-verb:{placeableId}"), _text["placeable.verb"].Text.Trim(),
                        _text["placeable.event"].Text.Trim(), string.Join("&&", Csv(_text["placeable.conditions"].Text)))],
                    _number["placeable.distance"].Value, _number["placeable.angle"].Value,
                    _check["placeable.focus"].ButtonPressed, _text["placeable.prompt"].Text.Trim(),
                    _number["placeable.duration"].Value, _text["placeable.authority"].Text.Trim(),
                    Csv(_text["placeable.conditions"].Text)),
                staticRole ? new(true, true, true, true, false) : null,
                new(true, _check["placeable.hasInventory"].ButtonPressed, _check["placeable.hasLock"].ButtonPressed,
                    _check["placeable.hasTrap"].ButtonPressed, _check["placeable.physics"].ButtonPressed,
                    new Dictionary<string, string> { ["onUsed"] = _text["placeable.script"].Text.Trim() }),
                new(new((float)_number["placeable.positionX"].Value, (float)_number["placeable.positionY"].Value,
                        (float)_number["placeable.positionZ"].Value),
                    new((float)_number["placeable.rotationX"].Value, (float)_number["placeable.rotationY"].Value,
                        (float)_number["placeable.rotationZ"].Value),
                    new((float)_number["placeable.scaleX"].Value, (float)_number["placeable.scaleY"].Value,
                        (float)_number["placeable.scaleZ"].Value),
                    new((float)_number["placeable.pivotX"].Value, (float)_number["placeable.pivotY"].Value,
                        (float)_number["placeable.pivotZ"].Value), _number["placeable.snap"].Value,
                    GuidOrNull(_text["placeable.parent"].Text), _check["placeable.alignSurface"].ButtonPressed,
                    Null(_text["placeable.transformAnimation"].Text)), KeyValues(_text["placeable.overrides"].Text),
                staticRole ? null : new(Stable(moduleId, $"placeable-on-used:{placeableId}"), 1, "toolset:r047",
                    _text["placeable.apiVersion"].Text.Trim(), _text["placeable.eventBinding"].Text.Trim(),
                    Null(_text["placeable.beforeScript"].Text), Null(_text["placeable.afterScript"].Text)),
                new(Csv(_text["placeable.transformAnimation"].Text), Csv(_text["placeable.locks"].Text),
                    Csv(_text["placeable.traps"].Text), Csv(_text["placeable.inventories"].Text),
                    Csv(_text["placeable.physicsProfiles"].Text), (int)_number["placeable.drawBudget"].Value,
                    (int)_number["placeable.scriptBudget"].Value),
                !staticRole && _check["placeable.onUsedRequired"].ButtonPressed);
            var profile = new PlaceableWorldProfile(Stable(moduleId, $"placeable:{placeableId}"), 1, "toolset:g13-n-o", placeableId,
                Null(_text["placeable.faction"].Text), GuidOrNull(_text["placeable.dialogue"].Text),
                string.IsNullOrWhiteSpace(_text["placeable.key"].Text) ? null : new(_text["placeable.key"].Text.Trim(), consumption,
                    consumption == PlaceableWorldProfile.CredentialConsumption.Charge ? 3 : 1,
                    consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission ? _text["placeable.key"].Text.Trim() : null, true),
                new([
                    new("physical-attack-resolved", _text["placeable.attack"].Text.Trim(), false, true),
                    new("ability-impacted", _text["placeable.ability"].Text.Trim(), false, true),
                    new("heartbeat", "scripts/placeable_heartbeat", false, true),
                    new($"custom:{_text["placeable.custom"].Text.Trim()}:signal", "scripts/placeable_custom", false, true),
                ], new(true, _number["placeable.interval"].Value, 12, true, _text["placeable.custom"].Text.Trim())),
                _check["placeable.physics"].ButtonPressed ? new(PlaceableWorldProfile.BodyMode.Rigid,
                    _number["placeable.mass"].Value, ["lock-y"], true, 4, true, "server") : null,
                foundation, _check["placeable.foundationRequired"].ButtonPressed);
            placeables = Upsert(placeables, profile, item => item.BlueprintId == placeableId);
        }

        var doors = current.Doors;
        if (SelectedBlueprint("door.target") is { } doorId)
        {
            var assemblyKind = (DoorWorldProfile.AssemblyKind)_choice["door.assembly"].Selected;
            var placement = (DoorWorldProfile.PlacementMode)_choice["door.placement"].Selected;
            var destinationKind = (DoorWorldProfile.DestinationKind)_choice["door.portalKind"].Selected;
            var profileId = Stable(moduleId, $"door:{doorId}");
            var profile = new DoorWorldProfile(profileId, 1, "toolset:g13-p-q", doorId,
                new(_number["door.health"].Value, _number["door.hardness"].Value, false, "door.damaged", "door.breached", true,
                    "assets/debris/door.glb", "service.repair-door"),
                new(_text["door.loading"].Text.Trim(), "loading.area.title", "loading.area.tip", true, .5),
                [new("damage-applied", _text["door.damageScript"].Text.Trim(), false, true),
                 new("entity-destroyed", _text["door.damageScript"].Text.Trim(), false, true),
                 new("physical-attack-resolved", _text["door.attackScript"].Text.Trim(), false, true),
                 new("ability-impacted", _text["door.abilityScript"].Text.Trim(), false, true),
                 new("heartbeat", "scripts/door_heartbeat", false, true),
                 new($"custom:{_text["door.custom"].Text.Trim()}:signal", "scripts/door_custom", false, true)],
                GuidOrNull(_text["door.dialogue"].Text), Null(_text["door.faction"].Text),
                new(assemblyKind, [new(Stable(profileId, "panel-a"), "primary", -1, 0, false),
                    new(Stable(profileId, "panel-b"), "secondary", 1, .1, true)], "open-safely", assemblyKind == DoorWorldProfile.AssemblyKind.Airlock, "close-and-restore"),
                new(placement,
                    placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
                        ? _text["door.socket"].Text.Trim() : null,
                    placement == DoorWorldProfile.PlacementMode.ArchitectureSocket
                        ? new("kit.crypt", "arched", new Dictionary<string, string> { ["door"] = "material.ash" }, 0, 0, 0)
                        : null,
                    "closed",
                    [
                        new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                        new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                        new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                        new("closing", DoorWorldProfile.StateKind.Closing, "animation:closing", true, false),
                        new("locked", DoorWorldProfile.StateKind.Locked, "animation:locked", true, true),
                        new("jammed", DoorWorldProfile.StateKind.Jammed, "animation:jammed", true, true),
                        new("broken", DoorWorldProfile.StateKind.Broken, "animation:broken", false, false),
                        new("disabled", DoorWorldProfile.StateKind.Disabled, "animation:disabled", true, false),
                        new("custom", DoorWorldProfile.StateKind.Custom, "animation:custom", true, true),
                    ],
                    [
                        new("closed", "opening", "interact", 1, null),
                        new("opening", "open", "animation-complete", 0, null),
                        new("open", "closing", "interact", 1, null),
                        new("closing", "closed", "animation-complete", 0, null),
                    ],
                    new(2.5, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                    new(destinationKind, Stable(profileId, "destination"), _text["door.spawnAnchor"].Text.Trim(),
                        180, true, [_text["door.permission"].Text.Trim()]),
                    [new(Stable(profileId, "link"), Stable(profileId, "destination"),
                        _check["door.oneWay"].ButtonPressed,
                        _check["door.oneWay"].ButtonPressed ? string.Empty : "Retour vers la crypte")],
                    new Dictionary<string, string> { ["interaction.prompt"] = "door.open" },
                    new(_text["door.navObstacle"].Text.Trim(), _text["door.navLink"].Text.Trim(),
                        ["closed", "locked", "jammed", "disabled", "custom"], ["open", "broken"],
                        _number["door.openingPassable"].Value, _number["door.closingBlocked"].Value,
                        _check["door.navVisualize"].ButtonPressed)));
            doors = Upsert(doors, profile, item => item.BlueprintId == doorId);
        }

        var triggerArea = SelectedArea("trigger.area");
        var triggers = current.Triggers;
        if (SelectedBlueprint("trigger.target") is { } triggerId && triggerArea is not null)
        {
            var profileId = Stable(moduleId, $"trigger:{triggerId}");
            var dialogueId = GuidOrNull(_text["trigger.dialogue"].Text);
            var questId = GuidOrNull(_text["trigger.quest"].Text);
            var foundation = new TriggerWorldProfile.FoundationPolicy(
                Stable(profileId, "foundation"), 1, "toolset:r048", _text["trigger.slug"].Text.Trim(),
                new((TriggerWorldProfile.ShapeKind)_choice["trigger.shape"].Selected,
                    [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)],
                    [[new(-.5f, 0, -.5f), new(.5f, 0, -.5f), new(0, 0, .5f)]],
                    new(4, (float)_number["trigger.height"].Value, 4), _number["trigger.height"].Value,
                    _number["trigger.snap"].Value, _check["trigger.visualize"].ButtonPressed),
                new(Csv(_text["trigger.entityKinds"].Text), Csv(_text["trigger.requiredTags"].Text),
                    Csv(_text["trigger.excludedTags"].Text), Csv(_text["trigger.layers"].Text)),
                Csv(_text["trigger.conditions"].Text), _number["trigger.cooldown"].Value,
                (TriggerWorldProfile.FirePolicy)_choice["trigger.fire"].Selected,
                [new(Stable(profileId, "action"), _text["trigger.actionKind"].Text.Trim(),
                    _text["trigger.actionTarget"].Text.Trim(), new Dictionary<string, string> { ["source"] = "toolset" })],
                _check["trigger.portal"].ButtonPressed
                    ? new((TriggerWorldProfile.DestinationKind)_choice["trigger.portalKind"].Selected,
                        Stable(profileId, "destination"), _text["trigger.portalAnchor"].Text.Trim(),
                        new(0, 180, 0), true, ["portal.travel"])
                    : null,
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
                new Dictionary<string, string> { ["editor.overlay"] = "volume" },
                new(Stable(profileId, "on-enter"), 1, "toolset:r048", _text["trigger.apiVersion"].Text.Trim(),
                    "VolumeEntered", null, Null(_text["trigger.onEnterScript"].Text)),
                new(Stable(profileId, "on-exit"), 1, "toolset:r048", _text["trigger.apiVersion"].Text.Trim(),
                    "VolumeExited", null, Null(_text["trigger.onExitScript"].Text)),
                new([Stable(profileId, "destination")], Csv(_text["trigger.knownTargets"].Text),
                    Csv(_text["trigger.knownScripts"].Text), Csv(_text["trigger.accessibleLayers"].Text)),
                _check["trigger.validationRequired"].ButtonPressed);
            var profile = new TriggerWorldProfile(profileId, 1, "toolset:g13-r-s", triggerId, triggerArea,
                new(["sight", "search"], (int)_number["trigger.discovery"].Value, Stable(profileId, "reveal"), "feedback.secret", true,
                    TriggerWorldProfile.DiscoveryScope.Party),
                new("inspect", "cursor.secret", "outline.gold", "trigger.inspect", _text["trigger.click"].Text.Trim(), true, "player"),
                new(_number["trigger.interval"].Value, 30, true, true), _text["trigger.custom"].Text.Trim(),
                string.IsNullOrWhiteSpace(_text["trigger.key"].Text) ? null : new(_text["trigger.key"].Text.Trim(), false, "trigger.denied"),
                new(_check["trigger.rest"].ButtonPressed, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
                new("tracking", 10, "authored-tracks", 45, 2, "false-trail", questId),
                dialogueId is null ? null : new([], 4, true, 60, "party-vote", dialogueId.Value),
                new(true, true, _check["trigger.director"].ButtonPressed, true, true), foundation,
                _check["trigger.foundationRequired"].ButtonPressed);
            triggers = Upsert(triggers, profile, item => item.BlueprintId == triggerId);
        }

        var routeArea = SelectedArea("vehicle.area");
        var routes = current.VehicleRoutes;
        VehicleRouteProfile? route = null;
        if (routeArea is not null)
        {
            var routeId = Stable(moduleId, $"vehicle:{routeArea}");
            var pointA = Stable(routeId, "start");
            var pointB = Stable(routeId, "end");
            route = new(routeId, 1, "toolset:g13-v", routeArea,
                [new(pointA, Vector3Value.Zero, 0, _number["vehicle.width"].Value),
                 new(pointB, new(20, 0, 0), 0, _number["vehicle.width"].Value)],
                [new("primary", 0, _number["vehicle.speed"].Value, "forward")],
                [new(Stable(routeId, "stop"), pointB, _number["vehicle.stop"].Value, "authored-stop")],
                _number["vehicle.speed"].Value, _check["vehicle.overtake"].ButtonPressed,
                _choice["vehicle.sync"].Selected switch { 1 => "bounded-prediction", 2 => "local", _ => "server-snapshots" });
            routes = Upsert(routes, route, item => item.Id == routeId);
        }

        var encounterArea = SelectedArea("encounter.area");
        var encounters = current.Encounters;
        if (encounterArea is not null && SelectedBlueprint("encounter.candidate") is { } encounterCandidateId)
        {
            var encounterId = Stable(moduleId, $"encounter:{encounterArea}");
            var routeId = route?.Id ?? Stable(moduleId, $"reinforcement:{encounterArea}");
            var profile = new EncounterWorldProfile(encounterId, 1, "toolset:g13-t-u", encounterArea,
                new(_number["encounter.respawn"].Value, (int)_number["encounter.cycles"].Value, "area.occupied", true, true, .5),
                Null(_text["encounter.faction"].Text), new(true, _number["encounter.interval"].Value, 12, "encounter.authored"),
                new((int)_number["encounter.alert"].Value, _text["encounter.faction"].Text.Trim(), routeId,
                    (int)_number["encounter.cap"].Value, 3, "boss.dead"),
                new((EncounterWorldProfile.VisibilityMode)_choice["encounter.visibility"].Selected, 10, true, "vfx.spawn"),
                new(30, 30, _number["encounter.cleanup"].Value, true, true),
                new(
                    new((EncounterWorldProfile.VolumeShape)_choice["encounter.shape"].Selected,
                        [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                        _number["encounter.height"].Value, _number["encounter.radius"].Value,
                        Csv(_text["encounter.tags"].Text), Csv(_text["encounter.allowedFactions"].Text),
                        _check["encounter.lineOfSight"].ButtonPressed, _number["encounter.threat"].Value,
                        Null(_text["encounter.activationScript"].Text), _check["encounter.visualize"].ButtonPressed),
                    [new(Stable(encounterId, $"candidate:{encounterCandidateId}"), encounterCandidateId,
                        (EncounterWorldProfile.CandidateRole)_choice["encounter.role"].Selected,
                        _number["encounter.weight"].Value, _number["encounter.cost"].Value,
                        Csv(_text["encounter.conditions"].Text), Csv(_text["encounter.variants"].Text),
                        (int)_number["encounter.maximumAlive"].Value)],
                    _number["encounter.budget"].Value, Csv(_text["encounter.anchors"].Text),
                    Csv(_text["encounter.rewards"].Text), (int)_number["encounter.networkCap"].Value));
            encounters = Upsert(encounters, profile, item => item.AreaId == encounterArea);
        }

        var merchants = current.Merchants;
        if (SelectedBlueprint("merchant.target") is { } merchantId)
        {
            var currency = _text["merchant.currency"].Text.Trim();
            var balance = (decimal)_number["merchant.balance"].Value;
            var cap = (decimal)_number["merchant.cap"].Value;
            var stockItem = SelectedBlueprint("merchant.stockItem");
            var restrictions = Csv(_text["merchant.restrictions"].Text);
            var transactions = stockItem is null ? null : new MerchantWorldProfile.TransactionPolicy(
                Stable(moduleId, $"merchant-transactions:{merchantId}"), 1, "toolset:r045",
                _text["merchant.vendorInteraction"].Text.Trim(), _text["merchant.dialogueChoice"].Text.Trim(),
                [new(Stable(moduleId, $"merchant-stock:{merchantId}:{stockItem}"), stockItem,
                    _text["merchant.variant"].Text.Trim(), (int)_number["merchant.stockQuantity"].Value,
                    (decimal)_number["merchant.unitPrice"].Value, currency, restrictions)],
                Csv(_text["merchant.locales"].Text), restrictions,
                new Dictionary<string, string> { ["onOpen"] = _text["merchant.onOpen"].Text.Trim() },
                (int)_number["merchant.buyerCapacity"].Value, (int)_number["merchant.sellerCapacity"].Value, true);
            var profile = new MerchantWorldProfile(Stable(moduleId, $"merchant:{merchantId}"), 1, "toolset:g13-w-x", merchantId,
                [new(currency, $"currency.{currency}", 1, true, false)], [new(currency, balance, 0, 0, balance / 10, Math.Max(balance, 1) * 2)],
                new(cap, cap * 2, new Dictionary<string, decimal>(), -1, true),
                new(_check["merchant.stolen"].ButtonPressed, ["authored-jurisdiction"], false, false, .25, "crime.stolen-goods"),
                new(_number["merchant.restock"].Value, "market.restock", route?.Id, true, true),
                new("bargain", 10, _number["merchant.discount"].Value / 100, .2, 60, true),
                _check["merchant.market"].ButtonPressed ? new("authored-region", .25, .5, new Dictionary<string, double>(), true) : null,
                [new(Stable(moduleId, $"merchant-service:{merchantId}"), _text["merchant.service"].Text.Trim(),
                    $"service.{_text["merchant.service"].Text.Trim()}", currency, Math.Min(cap, 25),
                    $"service.{_text["merchant.service"].Text.Trim()}", "true")],
                new(_check["merchant.ledger"].ButtonPressed, 90, Math.Max(100, cap), true, true, true),
                transactions, _check["merchant.transactionsRequired"].ButtonPressed);
            merchants = Upsert(merchants, profile, item => item.BlueprintId == merchantId);
        }

        var companions = current.Companions;
        if (SelectedBlueprint("companion.target") is { } companionId && GuidOrNull(_text["companion.dialogue"].Text) is { } companionDialogue)
        {
            var questId = GuidOrNull(_text["companion.quest"].Text);
            var profileId = Stable(moduleId, $"companion:{companionId}");
            var areaIds = Csv(_text["companion.areas"].Text).Select(AreaId.Parse).ToArray();
            var contexts = Csv(_text["companion.contexts"].Text)
                .Select(context => new CompanionWorldProfile.DialogueContextValidation(
                    context, companionDialogue, $"dialogue.{context}"))
                .ToArray();
            var ownership = _choice["companion.ownership"].Selected switch
            {
                1 => "individual",
                2 => "shared",
                3 => "ai-only",
                4 => "game-master",
                _ => "party",
            };
            var deathMode = _choice["companion.deathMode"].Selected switch
            {
                1 => "downed",
                2 => "permanent-death",
                3 => "resurrection",
                4 => "replacement",
                5 => "memorial",
                _ => "recoverable-corpse",
            };
            var profile = new CompanionWorldProfile(profileId, 1, "toolset:r037", companionId,
                new(companionDialogue, "full", "limited", "greeting", "full", "deny"),
                new(_text["companion.formation"].Text.Trim(), (int)_number["companion.slot"].Value, 1.5, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.PlayerDirected, (int)_number["companion.level"].Value, 40, true, true, [5, 10, 20]),
                new(.5, .5, 0, _number["companion.loyalty"].Value, ["authored"], ["joined-party"]),
                new([], 2, 90, [], true, companionDialogue),
                questId is null ? null : new(questId.Value, "loyalty>=0", "departed", 0, "party-reacts"),
                new(_check["companion.traps"].ButtonPressed, _check["companion.traps"].ButtonPressed, 5, "sight", "traps", true),
                new(Csv(_text["companion.abilities"].Text), [], .35, "lowest-health", true),
                new(_check["companion.direct"].ButtonPressed, true, true, true, "owner-or-gm"),
                new(true, _check["companion.split"].ButtonPressed, "owner-instance", _check["companion.split"].ButtonPressed, 120),
                new(Stable(profileId, "validation"), 1, "toolset:r037",
                    new(_check["companion.recruitDialogue"].ButtonPressed, _check["companion.recruitScript"].ButtonPressed,
                        _text["companion.recruitScriptId"].Text.Trim(), _text["companion.currency"].Text.Trim(),
                        (decimal)_number["companion.cost"].Value, _number["companion.reputation"].Value),
                    new(ownership, _text["companion.authority"].Text.Trim(), _check["companion.transfer"].ButtonPressed),
                    Csv(_text["companion.commands"].Text),
                    new(_text["companion.inventory"].Text.Trim(), _text["companion.equipment"].Text.Trim(),
                        Csv(_text["companion.slots"].Text)),
                    new(_text["companion.deathPolicy"].Text.Trim(), deathMode, _number["companion.recovery"].Value),
                    contexts,
                    new(_check["companion.crossZone"].ButtonPressed, areaIds,
                        _check["companion.followPortals"].ButtonPressed, _check["companion.safeFallback"].ButtonPressed,
                        _text["companion.fallbackAnchor"].Text.Trim(), _number["companion.timeout"].Value)),
                _check["companion.required"].ButtonPressed);
            companions = Upsert(companions, profile, item => item.BlueprintId == companionId);
        }

        var scripts = new ScriptAutomationProfile(moduleId, 1, "toolset:g13-aa-ab",
            new([new(Stable(moduleId, "camera-shot"), _text["script.rig"].Text.Trim(), _text["script.shot"].Text.Trim(),
                _number["script.duration"].Value, .4, _check["script.lock"].ButtonPressed, true)],
                "restore-camera-and-control", true, true),
            new(true, true, [new(_text["script.tag"].Text.Trim(), _text["script.event"].Text.Trim(), _text["script.route"].Text.Trim(), true)]),
            [new(_text["script.flag"].Text.Trim(), ScriptAutomationProfile.SettingKind.Boolean,
                _check["script.flagDefault"].ButtonPressed ? "true" : "false", ScriptAutomationProfile.FlagScope.Module,
                "Authored module feature flag.", 1)],
            [new(_text["script.ai"].Text.Trim(), ScriptAutomationProfile.SettingKind.Choice,
                _choice["script.aiDefault"].Selected switch { 0 => "passive", 2 => "aggressive", _ => "balanced" },
                ["passive", "balanced", "aggressive"], "Named AI behavior preview.", true)]);

        return new(moduleId, 1, "toolset:g13-n-ab", placeables, doors, triggers, encounters, routes, merchants, companions, scripts);
    }

    private void Mutate(bool dryRun)
    {
        _lastSucceeded = false;
        LastCommandId = Guid.Empty;
        LastPatchSummary = LastDiagnostics = string.Empty;
        if (_mutate is null || _feedback is null) return;
        try
        {
            var result = _mutate(BuildCatalog(), dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
            _lastSucceeded = result.Succeeded;
            _feedback.Text = result.Succeeded
                ? dryRun
                    ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                    : $"[color=#8ce0bd]MONDE VIVANT ENREGISTRÉ[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}"
                : $"[color=#ff7777]TRANSACTION REFUSÉE[/color]\n{LastDiagnostics}";
            if (!dryRun && result.Succeeded) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]CONFIGURATION INVALIDE[/color]\n{exception.Message}";
        }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var catalog = BuildCatalog();
            var events = catalog.Placeables.Sum(item => item.Reactive.Events.Count) + catalog.Doors.Sum(item => item.Events.Count);
            _summary.Text = "[color=#ff9858][b]COUVERTURE DU MODULE[/b][/color]\n\n" +
                $"[b]Placeables[/b] · {catalog.Placeables.Count} profil(s) · {catalog.Placeables.Count(item => item.Foundation is not null)} fondation(s) · " +
                $"{catalog.Placeables.Count(item => item.Foundation?.Interaction is not null)} utilisable(s) · " +
                $"{catalog.Placeables.Count(item => item.Foundation?.StaticProp is not null)} décor(s) · {catalog.Placeables.Count(item => item.Physics is not null)} physique(s)\n" +
                $"[b]Portes[/b] · {catalog.Doors.Count} profil(s) · {catalog.Doors.Count(item => item.Assembly is not null)} assemblage(s)\n" +
                $"[b]Triggers[/b] · {catalog.Triggers.Count} profil(s) · {catalog.Triggers.Count(item => item.Foundation is not null)} volume(s) canonique(s) · " +
                $"{catalog.Triggers.Sum(item => item.Foundation?.Actions.Count ?? 0)} action(s) · {catalog.Triggers.Count(item => item.Foundation?.Portal is not null)} transition(s)\n" +
                $"[b]Rencontres[/b] · {catalog.Encounters.Count} profil(s) · respawn, renforts, visibilité et nettoyage\n" +
                $"[b]Routes[/b] · {catalog.VehicleRoutes.Count} · {catalog.VehicleRoutes.Sum(item => item.Lanes.Count)} lane(s)\n" +
                $"[b]Marchands[/b] · {catalog.Merchants.Count} · monnaies, trésorerie, services et audit\n" +
                $"[b]Compagnons[/b] · {catalog.Companions.Count} · {catalog.Companions.Count(item => item.Validation is not null)} validé(s) · " +
                $"{catalog.Companions.Sum(item => item.Validation?.CommandIds.Count ?? 0)} commande(s) · " +
                $"mort {catalog.Companions.FirstOrDefault()?.Validation?.Death.Mode ?? "legacy"} · " +
                $"inter-zones {(catalog.Companions.Any(item => item.Validation?.CrossZoneNavigation.Enabled == true) ? "actif" : "borné")}\n" +
                $"[b]ForgeScript[/b] · {events} binding(s) monde · {catalog.Scripts.Camera.Shots.Count} plan(s) · " +
                $"{catalog.Scripts.TagRouting.Routes.Count} route(s) · {catalog.Scripts.ModuleFlags.Count} flag(s) · {catalog.Scripts.AiSettings.Count} réglage(s) IA\n\n" +
                "[color=#8ce0bd]Prêt pour prévisualisation transactionnelle.[/color]";
        }
        catch (Exception exception)
        {
            _summary.Text = $"[color=#ff7777]Configuration incomplète[/color]\n{exception.Message}";
        }
    }

    private static IReadOnlyList<T> Upsert<T>(IReadOnlyList<T> source, T value, Func<T, bool> match) =>
        [.. source.Where(item => !match(item)), value];

    private BlueprintId? SelectedBlueprint(string key)
    {
        var option = _choice[key];
        return option.ItemCount == 0 || option.Selected < 0 ? null : BlueprintId.Parse(option.GetItemMetadata(option.Selected).AsString());
    }

    private AreaId? SelectedArea(string key)
    {
        var option = _choice[key];
        return option.ItemCount == 0 || option.Selected < 0 ? null : AreaId.Parse(option.GetItemMetadata(option.Selected).AsString());
    }

    private static Guid Stable(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}:{key}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static Guid Stable(Guid owner, Guid key) => Stable(owner, key.ToString("D"));
    private static Guid? GuidOrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value.Trim());
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static IReadOnlyDictionary<string, string> KeyValues(string value) => Csv(value)
        .Select(item => item.Split('=', 2, StringSplitOptions.TrimEntries))
        .Where(item => item.Length == 2 && item.All(part => !string.IsNullOrWhiteSpace(part)))
        .GroupBy(item => item[0], StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.Ordinal);

    private static void PopulateBlueprints(OptionButton option, ProjectState state, BlueprintKind kind) =>
        Populate(option, state.Project.Blueprints.Where(item => item.Kind == kind).Select(item => (item.DisplayName, item.Id.ToString())));

    private static void PopulateCreatures(OptionButton option, ProjectState state) =>
        Populate(option, state.Project.Blueprints.Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .Select(item => (item.DisplayName, item.Id.ToString())));

    private static void PopulateAreas(OptionButton option, ProjectState state) =>
        Populate(option, state.Project.Areas.Select(item => (item.DisplayName, item.Id.ToString())));

    private static void Populate(OptionButton option, IEnumerable<(string Name, string Id)> values)
    {
        var selected = option.ItemCount > 0 && option.Selected >= 0 ? option.GetItemMetadata(option.Selected).AsString() : null;
        option.Clear();
        foreach (var value in values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            option.AddItem(value.Name);
            option.SetItemMetadata(option.ItemCount - 1, value.Id);
        }
        if (selected is not null)
            for (var index = 0; index < option.ItemCount; index++)
                if (string.Equals(option.GetItemMetadata(index).AsString(), selected, StringComparison.OrdinalIgnoreCase))
                    option.Select(index);
    }

    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabs.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(form);
        return form;
    }

    private void Target(VBoxContainer form, string key, string label)
    {
        var option = new OptionButton();
        _choice.Add(key, option);
        form.AddChild(Labeled(label, option));
    }

    private void Text(VBoxContainer form, string key, string label, string value)
    {
        var field = new LineEdit { Text = value };
        field.TextChanged += _ => RebuildSummary();
        _text.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private void Number(VBoxContainer form, string key, string label, double min, double max, double value, double step)
    {
        var field = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
        field.ValueChanged += _ => RebuildSummary();
        _number.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private void Toggle(VBoxContainer form, string key, string label, bool value)
    {
        var field = new CheckBox { Text = label, ButtonPressed = value };
        field.Toggled += _ => RebuildSummary();
        _check.Add(key, field);
        form.AddChild(field);
    }

    private void Choice(VBoxContainer form, string key, string label, IReadOnlyList<string> values)
    {
        var field = new OptionButton();
        foreach (var value in values) field.AddItem(value);
        field.ItemSelected += _ => RebuildSummary();
        _choice.Add(key, field);
        form.AddChild(Labeled(label, field));
    }

    private static void Info(VBoxContainer form, string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new("a99bc1"));
        form.AddChild(label);
    }

    private static void Section(VBoxContainer form, string title, string subtitle)
    {
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 16);
        heading.AddThemeColorOverride("font_color", new("ffad70"));
        form.AddChild(heading);
        Info(form, subtitle);
    }

    private static Control Labeled(string label, Control field)
    {
        var row = new VBoxContainer();
        var title = new Label { Text = label };
        title.AddThemeColorOverride("font_color", new("cdbbe9"));
        row.AddChild(title);
        row.AddChild(field);
        return row;
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new("ffad70"));
        box.AddChild(label);
        return panel;
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(130, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new("fff4ec"));
        button.Pressed += action;
        return button;
    }
}
#endif
