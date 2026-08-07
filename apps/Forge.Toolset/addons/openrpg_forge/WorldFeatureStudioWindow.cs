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
        PopulateCreatures(_choice["companion.target"], state);
        PopulateAreas(_choice["trigger.area"], state);
        PopulateAreas(_choice["encounter.area"], state);
        PopulateAreas(_choice["vehicle.area"], state);
        var dialogue = state.Project.Dialogues.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        var quest = state.Project.Quests.FirstOrDefault()?.Id.Value.ToString("D") ?? string.Empty;
        foreach (var key in new[] { "placeable.dialogue", "door.dialogue", "trigger.dialogue", "companion.dialogue" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = dialogue;
        foreach (var key in new[] { "trigger.quest", "companion.quest" })
            if (string.IsNullOrWhiteSpace(_text[key].Text)) _text[key].Text = quest;
        RebuildSummary();
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_choice["placeable.target"].ItemCount == 0 || _choice["door.target"].ItemCount == 0 ||
            _choice["trigger.target"].ItemCount == 0 || _choice["merchant.target"].ItemCount == 0 ||
            _choice["companion.target"].ItemCount == 0 || _choice["encounter.area"].ItemCount == 0)
            return false;
        _text["placeable.faction"].Text = "ash-watch";
        _text["placeable.key"].Text = "key:ember";
        _check["placeable.physics"].ButtonPressed = true;
        _number["placeable.mass"].Value = 12;
        _text["door.loading"].Text = "assets/loading/crypt-of-ashes.png";
        _number["door.health"].Value = 40;
        _text["trigger.key"].Text = "key:ember";
        _check["trigger.rest"].ButtonPressed = true;
        _check["trigger.director"].ButtonPressed = true;
        _text["encounter.faction"].Text = "ash-hostile";
        _number["encounter.alert"].Value = 2;
        _text["merchant.currency"].Text = "ember-crown";
        _check["merchant.ledger"].ButtonPressed = true;
        _text["companion.formation"].Text = "wedge";
        _check["companion.direct"].ButtonPressed = true;
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
        Target(form, "placeable.target", "Blueprint placeable");
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
        Target(form, "merchant.target", "Blueprint du marchand");
        Text(form, "merchant.currency", "Monnaie principale", "ember-crown");
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
        Target(form, "companion.target", "Blueprint du compagnon");
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
                    _number["placeable.mass"].Value, ["lock-y"], true, 4, true, "server") : null);
            placeables = Upsert(placeables, profile, item => item.BlueprintId == placeableId);
        }

        var doors = current.Doors;
        if (SelectedBlueprint("door.target") is { } doorId)
        {
            var assemblyKind = (DoorWorldProfile.AssemblyKind)_choice["door.assembly"].Selected;
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
                    new(Stable(profileId, "panel-b"), "secondary", 1, .1, true)], "open-safely", assemblyKind == DoorWorldProfile.AssemblyKind.Airlock, "close-and-restore"));
            doors = Upsert(doors, profile, item => item.BlueprintId == doorId);
        }

        var triggerArea = SelectedArea("trigger.area");
        var triggers = current.Triggers;
        if (SelectedBlueprint("trigger.target") is { } triggerId && triggerArea is not null)
        {
            var profileId = Stable(moduleId, $"trigger:{triggerId}");
            var dialogueId = GuidOrNull(_text["trigger.dialogue"].Text);
            var questId = GuidOrNull(_text["trigger.quest"].Text);
            var profile = new TriggerWorldProfile(profileId, 1, "toolset:g13-r-s", triggerId, triggerArea,
                new(["sight", "search"], (int)_number["trigger.discovery"].Value, Stable(profileId, "reveal"), "feedback.secret", true,
                    TriggerWorldProfile.DiscoveryScope.Party),
                new("inspect", "cursor.secret", "outline.gold", "trigger.inspect", _text["trigger.click"].Text.Trim(), true, "player"),
                new(_number["trigger.interval"].Value, 30, true, true), _text["trigger.custom"].Text.Trim(),
                string.IsNullOrWhiteSpace(_text["trigger.key"].Text) ? null : new(_text["trigger.key"].Text.Trim(), false, "trigger.denied"),
                new(_check["trigger.rest"].ButtonPressed, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
                new("tracking", 10, "authored-tracks", 45, 2, "false-trail", questId),
                dialogueId is null ? null : new([], 4, true, 60, "party-vote", dialogueId.Value),
                new(true, true, _check["trigger.director"].ButtonPressed, true, true));
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
        if (encounterArea is not null)
        {
            var encounterId = Stable(moduleId, $"encounter:{encounterArea}");
            var routeId = route?.Id ?? Stable(moduleId, $"reinforcement:{encounterArea}");
            var profile = new EncounterWorldProfile(encounterId, 1, "toolset:g13-t-u", encounterArea,
                new(_number["encounter.respawn"].Value, (int)_number["encounter.cycles"].Value, "area.occupied", true, true, .5),
                Null(_text["encounter.faction"].Text), new(true, _number["encounter.interval"].Value, 12, "encounter.authored"),
                new((int)_number["encounter.alert"].Value, _text["encounter.faction"].Text.Trim(), routeId,
                    (int)_number["encounter.cap"].Value, 3, "boss.dead"),
                new((EncounterWorldProfile.VisibilityMode)_choice["encounter.visibility"].Selected, 10, true, "vfx.spawn"),
                new(30, 30, _number["encounter.cleanup"].Value, true, true));
            encounters = Upsert(encounters, profile, item => item.AreaId == encounterArea);
        }

        var merchants = current.Merchants;
        if (SelectedBlueprint("merchant.target") is { } merchantId)
        {
            var currency = _text["merchant.currency"].Text.Trim();
            var balance = (decimal)_number["merchant.balance"].Value;
            var cap = (decimal)_number["merchant.cap"].Value;
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
                new(_check["merchant.ledger"].ButtonPressed, 90, Math.Max(100, cap), true, true, true));
            merchants = Upsert(merchants, profile, item => item.BlueprintId == merchantId);
        }

        var companions = current.Companions;
        if (SelectedBlueprint("companion.target") is { } companionId && GuidOrNull(_text["companion.dialogue"].Text) is { } companionDialogue)
        {
            var questId = GuidOrNull(_text["companion.quest"].Text);
            var profile = new CompanionWorldProfile(Stable(moduleId, $"companion:{companionId}"), 1, "toolset:g13-y-z", companionId,
                new(companionDialogue, "full", "limited", "greeting", "full", "deny"),
                new(_text["companion.formation"].Text.Trim(), (int)_number["companion.slot"].Value, 1.5, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.PlayerDirected, (int)_number["companion.level"].Value, 40, true, true, [5, 10, 20]),
                new(.5, .5, 0, _number["companion.loyalty"].Value, ["authored"], ["joined-party"]),
                new([], 2, 90, [], true, companionDialogue),
                questId is null ? null : new(questId.Value, "loyalty>=0", "departed", 0, "party-reacts"),
                new(_check["companion.traps"].ButtonPressed, _check["companion.traps"].ButtonPressed, 5, "sight", "traps", true),
                new(Csv(_text["companion.abilities"].Text), [], .35, "lowest-health", true),
                new(_check["companion.direct"].ButtonPressed, true, true, true, "owner-or-gm"),
                new(true, _check["companion.split"].ButtonPressed, "owner-instance", _check["companion.split"].ButtonPressed, 120));
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
                $"[b]Placeables[/b] · {catalog.Placeables.Count} profil(s) · accès, dialogues, {catalog.Placeables.Count(item => item.Physics is not null)} physique(s)\n" +
                $"[b]Portes[/b] · {catalog.Doors.Count} profil(s) · {catalog.Doors.Count(item => item.Assembly is not null)} assemblage(s)\n" +
                $"[b]Triggers[/b] · {catalog.Triggers.Count} profil(s) · secrets, repos, pistes, interjections et Live Director\n" +
                $"[b]Rencontres[/b] · {catalog.Encounters.Count} profil(s) · respawn, renforts, visibilité et nettoyage\n" +
                $"[b]Routes[/b] · {catalog.VehicleRoutes.Count} · {catalog.VehicleRoutes.Sum(item => item.Lanes.Count)} lane(s)\n" +
                $"[b]Marchands[/b] · {catalog.Merchants.Count} · monnaies, trésorerie, services et audit\n" +
                $"[b]Compagnons[/b] · {catalog.Companions.Count} · formation, relation, progression et contrôle\n" +
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
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step };
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
