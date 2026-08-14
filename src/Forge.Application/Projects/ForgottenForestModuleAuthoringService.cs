using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Application.Projects;

public sealed record ForgottenForestAuthoredModule(
    ProjectState State,
    IReadOnlyDictionary<string, string> Scripts);

public static class ForgottenForestModuleAuthoringService
{
    private const string AssetPrefix = "kenney:mini-forest:";
    private const string NatureAssetPrefix = "kenney:nature-kit:";
    private const string PolyHavenAssetPrefix = "polyhaven:";
    private const string TownAssetPrefix = "kenney:fantasy-town-kit:";
    private const string CastleAssetPrefix = "kenney:castle-kit:";
    private const string DungeonAssetPrefix = "kaykit:dungeon-remastered-1.0:";
    private const string AnimatedCharacterExternalIdPrefix =
        "kaykit:character-pack-adventures-1.0:";
    private const string AnimatedMonsterExternalIdPrefix =
        "quaternius:lowpoly-animated-monsters:";
    private static readonly StableIds Ids = new("f5d1ca15-4a39-46ba-a8e2-74dde7188258");

    public static ForgottenForestAuthoredModule Create()
    {
        var areaId = Ids.Area("forgotten-forest");
        var dimensions = new AreaDimensions(512, 512);
        var world = CreateWorld(areaId, dimensions);
        var navigation = CreateNavigation(areaId, world);
        var environment = CreateEnvironment(world);
        var runtimePolicies = CreateRuntimePolicies(areaId);
        var strike = new AbilityDefinition(
            Ids.Ability("forest-strike"),
            "forest-strike",
            "Frappe de la clairière",
            0,
            0,
            AbilityTargeting.SingleEnemy,
            [new AbilityEffect(AbilityEffectKind.Damage, 8)],
            new AbilityVisualCue("cue.forest-strike"));
        var claw = new AbilityDefinition(
            Ids.Ability("thorn-claw"),
            "thorn-claw",
            "Griffe d’épine",
            0,
            0.8,
            AbilityTargeting.SingleEnemy,
            [new AbilityEffect(AbilityEffectKind.Damage, 3)],
            new AbilityVisualCue("cue.thorn-claw"));

        var blueprints = new[]
        {
            Blueprint("spawn", "Éclaireuse de l’Aube", BlueprintKind.PlayerSpawn,
                new CombatProfile(new CombatStats(42, 10, 8, 4, "dawn-watch", "heroes"), [strike])),
            Blueprint("guide", "Maëlys, garde forestière", BlueprintKind.Creature),
            Blueprint("dragon-mount", "Brumaille, jeune dragon des bois", BlueprintKind.Creature),
            Blueprint("scout", "Rôdeur corrompu", BlueprintKind.Creature,
                new CombatProfile(new CombatStats(14, 4, 4, 2, "rootbound", "hostiles"), [claw])),
            Blueprint("boss", "Gardien des Racines", BlueprintKind.Creature,
                new CombatProfile(new CombatStats(34, 8, 6, 4, "rootbound", "hostiles"), [claw], [new LootDrop("heartwood-sigil", 1)])),
            Blueprint("moonseed", "Graine de lune", BlueprintKind.Item),
            Blueprint("gate", "Passage des Ronces", BlueprintKind.Door),
            Blueprint("exit", "Sanctuaire oublié", BlueprintKind.Trigger),
            Blueprint("prop", "Décor de la forêt", BlueprintKind.Placeable),
        };

        var entities = new List<EntityInstance>
        {
            Entity("spawn", "spawn", -43, -43, world, 1, ["player"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
            Entity("guide", "guide", -37, -38, world, 0.08f, ["npc", "guide"],
                AnimatedCharacterVisual("mage", ("runtime.role", "guide"), ("runtime.displayName", "Maëlys")), Vector3Value.One),
            Entity("mist-dragon", "dragon-mount", -29, -47, world, .18f,
                ["friendly", "interactive", "mount", "flying-mount"],
                AnimatedDragonVisual(("runtime.displayName", "Brumaille")), Vector3Value.One, -32),
            Entity("moonseed", "moonseed", -14, 13, world, 0.12f, ["quest", "item"],
                Visual("flag", ("runtime.item", "moonseed"), ("runtime.displayName", "Graine de lune")), new(1.7f, 1.7f, 1.7f)),
            Entity("gate", "gate", 14, 18, world, 0.35f, ["gate", "solid"],
                Visual("fence", ("runtime.displayName", "Passage des Ronces")), new(3.2f, 2.2f, 1.2f), 32),
            Entity("scout-a", "scout", -2, 5, world, 0.08f, ["hostile"], AnimatedCharacterVisual("rogue"), Vector3Value.One),
            Entity("scout-b", "scout", 25, 25, world, 0.08f, ["hostile"], AnimatedCharacterVisual("rogue-hooded"), Vector3Value.One),
            Entity("boss", "boss", 42, 42, world, 0.08f, ["boss", "hostile"],
                AnimatedCharacterVisual("barbarian", ("runtime.role", "boss"),
                    ("phaseTwoScript", "scripts/root_guardian_phase_two.forge"),
                    ("deathScript", "scripts/root_guardian_death.forge")), new(1.2f, 1.2f, 1.2f)),
            Entity("exit", "exit", 51, 50, world, 0.12f, ["exit"], Visual("building-structure"), new(2.2f, 2.2f, 2.2f)),
        };

        AddProp(entities, world, "camp-tent", "tent", -39, -31, new(2.2f, 2.2f, 2.2f), -20);
        AddProp(entities, world, "camp-target", "target", -32, -28, new(1.5f, 1.5f, 1.5f), 15);
        AddProp(entities, world, "watch-platform", "platform", 30, 34, new(2.2f, 2.2f, 2.2f), 10);
        AddProp(entities, world, "watch-building", "building-structure", 37, 35, new(2.1f, 2.1f, 2.1f), -10);
        AddProp(entities, world, "watch-roof", "building-roof", 37, 35, new(2.1f, 2.1f, 2.1f), -10, 2.4f);
        AddProp(entities, world, "watch-ladder", "ladder", 32, 35, new(1.8f, 1.8f, 1.8f), -10);
        AddProp(entities, world, "landmark-flag", "flag", 30, 30, new(1.7f, 1.7f, 1.7f), 0);
        AddProp(entities, world, "rock-landmark-a", "rocks-high", -27, 19, new(2.1f, 2.1f, 2.1f), 23);
        AddProp(entities, world, "rock-landmark-b", "rocks-ramp", 17, -23, new(2.4f, 2.4f, 2.4f), -32);

        // The production map is composed as four readable outer sectors around the playable
        // central quest slice. Stable entity IDs keep every landmark editable in Creator.
        AddProp(entities, world, "grove-platform", "building-platform", -178, 162, new(2.8f, 2.8f, 2.8f), 18);
        AddProp(entities, world, "grove-stones", "stones", -166, 151, new(3.2f, 3.2f, 3.2f), -12);
        AddProp(entities, world, "grove-flag", "flag", -184, 147, new(2.2f, 2.2f, 2.2f), 8);
        AddProp(entities, world, "grove-rocks", "rocks-high", -151, 177, new(3.4f, 3.4f, 3.4f), 31);
        AddProp(entities, world, "grove-arch", "building-structure", -171, 169, new(2.4f, 2.4f, 2.4f), 18);

        // This lakeside landmark is seen at close range from the flying-mount proof. Keep it
        // to one architectural bay and one roof: the former 2x2 layout multiplied the gables,
        // while the Mini Forest "building-structure" asset is an open watchtower, not a house.
        AddTownHouse(entities, world, "lake-warden-hut", 113, 161, 12, 1, 1, false, compactCorners: true);
        var lakeHutEntrance = TransformLocal(113, 161, 0, 3.1f, 12);
        var lakeHutBanner = TransformLocal(113, 161, -3.4f, 2.4f, 12);
        var lakeHutCrates = TransformLocal(113, 161, 3.2f, -2.6f, 12);
        AddTownProp(entities, world, "lake-warden-steps", "stairs-wide-wood",
            lakeHutEntrance.X, lakeHutEntrance.Z, new(1.8f, 1.05f, 1.7f), 102, .08f,
            "lake-shore", "nonblocking");
        AddTownProp(entities, world, "lake-warden-banner", "banner-green",
            lakeHutBanner.X, lakeHutBanner.Z, new(1.25f, 1.25f, 1.25f), 102, 2.2f,
            "lake-shore", "nonblocking");
        AddDungeonProp(entities, world, "lake-warden-crates", "crates-stacked",
            lakeHutCrates.X, lakeHutCrates.Z, new(1.15f, 1.15f, 1.15f), 8, .08f,
            "lake-shore", "nonblocking");

        AddProp(entities, world, "southwest-tent", "tent", -174, -165, new(3.1f, 3.1f, 3.1f), 22);
        AddProp(entities, world, "southwest-target", "target", -157, -174, new(2.1f, 2.1f, 2.1f), -18);
        AddProp(entities, world, "southwest-ramp", "rocks-ramp", -188, -147, new(3.7f, 3.7f, 3.7f), 35);
        AddProp(entities, world, "southwest-stones", "stones", -149, -151, new(3.2f, 3.2f, 3.2f), 0);
        AddProp(entities, world, "southwest-flag", "flag", -181, -180, new(2.3f, 2.3f, 2.3f), 0);

        AddProp(entities, world, "sunken-structure", "building-structure", 168, -168, new(3.3f, 3.3f, 3.3f), 26);
        AddProp(entities, world, "sunken-roof", "building-roof", 168, -168, new(3.3f, 3.3f, 3.3f), 26, 3.8f);
        AddProp(entities, world, "sunken-platform", "building-platform", 151, -157, new(3.0f, 3.0f, 3.0f), -8);
        AddProp(entities, world, "sunken-rocks", "rocks-high", 184, -146, new(3.8f, 3.8f, 3.8f), -17);
        AddProp(entities, world, "sunken-flag", "flag", 178, -181, new(2.4f, 2.4f, 2.4f), 0);
        AddPolyHavenProp(entities, world, "central-ship", "dutch_ship_large_01", 2, 1,
            Vector3Value.One, 42, 0.12f, ["landmark", "ship"]);
        AddPolyHavenProp(entities, world, "northwest-pier-bridge", "modular_wooden_pier", -128, 88,
            new(1.2f, 1.2f, 1.2f), 48, 0.32f, ["bridge"]);
        AddPolyHavenProp(entities, world, "watcher-pier-bridge", "modular_wooden_pier", 71, -33,
            new(1.2f, 1.2f, 1.2f), 42, 0.32f, ["bridge"]);
        AddPolyHavenProp(entities, world, "southeast-pier-bridge", "modular_wooden_pier", 118, -70,
            new(1.2f, 1.2f, 1.2f), 51, 0.32f, ["bridge"]);
        AddPolyHavenProp(entities, world, "camp-axe-a", "wooden_axe_02", -33, -32,
            Vector3Value.One, -18, 0.18f, ["camp", "tool"]);
        AddPolyHavenProp(entities, world, "camp-axe-b", "wooden_axe_03", -34, -29,
            Vector3Value.One, 24, 0.18f, ["camp", "tool"]);
        AddPolyHavenProp(entities, world, "lake-pier", "modular_wooden_pier", 142, 184,
            new(1.35f, 1.35f, 1.35f), 8, 0.28f, ["bridge", "lake-shore"]);
        AddPolyHavenProp(entities, world, "lake-rocks-a", "rock_moss_set_01", 125, 177,
            new(1.7f, 1.7f, 1.7f), -18, 0.18f, ["lake-shore"]);
        AddPolyHavenProp(entities, world, "lake-rocks-b", "rock_moss_set_02", 123, 198,
            new(1.8f, 1.8f, 1.8f), 24, 0.18f, ["lake-shore"]);
        AddNatureProp(entities, world, "northeast-boundary-rock", "tall-rock-a", 246, 246,
            new(4.5f, 4.5f, 4.5f), 18, .08f, "forest", "landmark");
        AddForestBorderDressing(entities, world);

        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("fern-a", "detailed-bush", -52f, 26f, 12f, 3.2f),
                     ("fern-b", "large-grass", -48f, 31f, -18f, 2.8f),
                     ("flowers-a", "purple-flowers", -35f, -29f, 0f, 2.8f),
                     ("mushrooms-a", "red-mushrooms", 21f, -19f, 24f, 2.6f),
                     ("fallen-log-a", "fallen-log", -73f, 70f, 35f, 3.8f),
                     ("fallen-log-b", "fallen-log", 72f, 36f, -28f, 3.4f),
                 })
        {
            AddNatureProp(entities, world, $"forest-detail-{key}", asset, x, z,
                new(scale, scale, scale), yaw, .06f, "forest", "ground-detail");
        }

        AddCreature(entities, world, "goblin-scout-a", "scout", -92, 54, "Gobelin éclaireur", "goblin", false);
        AddCreature(entities, world, "goblin-scout-b", "scout", -83, 62, "Gobelin frondeur", "goblin", false);
        AddCreature(entities, world, "goblin-raider-a", "scout", 84, -18, "Gobelin pillard", "goblin", false);
        AddCreature(entities, world, "goblin-raider-b", "scout", 92, -24, "Gobelin coupe-bourse", "goblin", false);

        AddPortalEntity(entities, world, "portal-forest-village", -214, -214, "Vers Clairval");
        AddPortalEntity(entities, world, "portal-forest-fortress", 219, 209, "Vers la forteresse gnoll");
        AddPortalEntity(entities, world, "portal-forest-desert", 224, -190, "Vers les dunes d'Ambre");
        AddPortalEntity(entities, world, "portal-forest-snow", -218, 202, "Vers le col enneigé");

        var dialogueEntry = Ids.DialogueNode("guide-entry");
        var dialogueBrief = Ids.DialogueNode("guide-brief");
        var dialogue = new DialogueDefinition(
            Ids.Dialogue("forest-guide"),
            "maelys-forest-guide",
            "L’appel de Maëlys",
            dialogueEntry,
            [
                new DialogueNode(
                    dialogueEntry,
                    Ids.Blueprint("guide"),
                    new LocalizedText("dialogue.forest.entry", "La forêt a refermé le chemin. Une graine de lune peut apaiser les ronces."),
                    choices:
                    [
                        new DialogueChoice(
                            Ids.DialogueChoice("accept"),
                            new LocalizedText("dialogue.forest.accept", "Je rouvrirai le passage."),
                            actions: [new DialogueAction(DialogueActionKind.StartQuest, "wake-forgotten-forest", "active")],
                            targetNodeId: dialogueBrief),
                    ]),
                new DialogueNode(
                    dialogueBrief,
                    Ids.Blueprint("guide"),
                    new LocalizedText("dialogue.forest.brief", "Suis les fanions jusqu’au gué. Le Gardien veille au-delà de la tour."),
                    choices: [new DialogueChoice(Ids.DialogueChoice("leave"), new LocalizedText("dialogue.forest.leave", "Je reviendrai avec la lumière."))]),
            ]);

        var dormant = Ids.QuestState("dormant");
        var seed = Ids.QuestState("seed");
        var guardian = Ids.QuestState("guardian");
        var completed = Ids.QuestState("completed");
        var quest = new QuestDefinition(
            Ids.Quest("wake-forest"),
            "wake-forgotten-forest",
            "Réveiller la forêt oubliée",
            dormant,
            [
                new QuestStateDefinition(dormant, "L’appel de la garde", new QuestJournalEntry("quest.forest.dormant", "Parler à Maëlys près du camp [E].")),
                new QuestStateDefinition(seed, "La Graine de lune", new QuestJournalEntry("quest.forest.seed", "Trouver la Graine de lune puis ouvrir le Passage des Ronces [E].")),
                new QuestStateDefinition(guardian, "Le Gardien des Racines", new QuestJournalEntry("quest.forest.guardian", "Vaincre le Gardien des Racines [ESPACE].")),
                new QuestStateDefinition(completed, "Le sanctuaire réveillé", new QuestJournalEntry("quest.forest.completed", "Rejoindre le sanctuaire lumineux."), true),
            ],
            [
                new QuestTransition(Ids.QuestTransition("begin"), dormant, seed),
                new QuestTransition(
                    Ids.QuestTransition("open-gate"),
                    seed,
                    guardian,
                    [new QuestCondition(QuestConditionKind.HasItem, "moonseed", "true")],
                    [new QuestAction(QuestActionKind.UnlockEntity, Ids.Entity("gate").ToString(), "true")]),
                new QuestTransition(Ids.QuestTransition("guardian-falls"), guardian, completed),
            ]);

        var area = new AreaDefinition(
            areaId,
            "forgotten-forest",
            "La Forêt Oubliée",
            entities,
            AreaKind.Exterior,
            dimensions,
            environment: environment,
            navigation: navigation,
            runtimePolicies: runtimePolicies,
            world: world);
        var village = CreateDetailedVillageArea();
        var inn = CreateDetailedInnArea();
        var fortress = CreateDetailedGnollFortressArea();
        var desert = CreateDetailedDesertArea();
        var mountain = CreateDetailedSnowMountainArea();

        area = area.WithPortals([
            Portal("forest-village", "Route de Clairval", "portal-forest-village", "clairval", "clairval-spawn"),
            Portal("forest-fortress", "Piste de la forteresse", "portal-forest-fortress", "gnoll-fortress", "gnoll-fortress-spawn"),
            Portal("forest-desert", "Piste des dunes", "portal-forest-desert", "amber-desert", "amber-desert-spawn"),
            Portal("forest-snow", "Sentier du col", "portal-forest-snow", "snow-pass", "snow-pass-spawn"),
        ]);
        village = village.WithPortals([
            Portal("village-forest", "Chemin de la forêt", "portal-village-forest", "forgotten-forest", "spawn"),
            Portal("village-inn", "Porte de l'auberge", "portal-village-inn", "ember-inn", "ember-inn-spawn"),
        ]);
        inn = inn.WithPortals([
            Portal("inn-village", "Sortie vers Clairval", "portal-inn-village", "clairval", "clairval-spawn"),
        ]);
        fortress = fortress.WithPortals([
            Portal("fortress-forest", "Retour vers la forêt", "portal-fortress-forest", "forgotten-forest", "spawn"),
        ]);
        desert = desert.WithPortals([
            Portal("desert-forest", "Retour vers la forêt", "portal-desert-forest", "forgotten-forest", "spawn"),
        ]);
        mountain = mountain.WithPortals([
            Portal("snow-forest", "Retour vers la forêt", "portal-snow-forest", "forgotten-forest", "spawn"),
        ]);
        var module = new ModuleDefinition(
            Ids.Module("forgotten-forest"),
            "forgotten-forest",
            "Forgotten Forest",
            "Une tranche verticale jouable : explorer, dialoguer, récupérer la Graine de lune, ouvrir le passage et vaincre le Gardien.",
            inn.Id);
        var project = new ProjectDefinition(
            Ids.Project("forgotten-forest"),
            "Forgotten Forest",
            module,
            blueprints,
            [area, village, inn, fortress, desert, mountain],
            [dialogue],
            [quest]);
        var scripts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scripts/root_guardian_phase_two.forge"] =
                "void main()\n{\n    say(\"Le Gardien fait jaillir deux rôdeurs des racines !\");\n    spawn(\"rootbound-scout\");\n    spawn(\"rootbound-scout\");\n    AreaRef area = current_area();\n    EntityRef guardian = find_entity(area, \"boss\");\n    cast_ability(guardian, \"thorn-claw\");\n}\n",
            ["scripts/root_guardian_death.forge"] =
                "void main()\n{\n    say(\"Le cœur de la forêt bat de nouveau. Rejoignez le sanctuaire.\");\n    quest(\"wake-forgotten-forest\");\n}\n",
        };
        return new ForgottenForestAuthoredModule(new ProjectState(project, new ProjectRevision(0)), scripts);
    }

    private static AreaDefinition CreateDetailedVillageArea()
    {
        var areaId = Ids.Area("clairval");
        var dimensions = new AreaDimensions(176, 176);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Village);
        var entities = new List<EntityInstance>
        {
            Entity("clairval-spawn", "spawn", 0, -56, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
            Entity("clairval-mayor", "guide", -7, 8, world, .08f, ["npc", "villager"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Maire Ysilde")), Vector3Value.One),
            Entity("clairval-smith", "guide", 29, 28, world, .08f, ["npc", "blacksmith"],
                AnimatedCharacterVisual("barbarian", ("runtime.displayName", "Borin, forgeron")), Vector3Value.One),
            Entity("clairval-healer", "guide", -29, 24, world, .08f, ["npc", "healer"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Sœur Althea")), Vector3Value.One),
        };

        // Roads are authored once as terrain features. The former second layer of bright
        // modular tiles fought the terrain material and made the square read as a web of
        // white strips instead of a lived-in medieval street.

        AddTownHouse(entities, world, "village-inn", 24, -12, 90, 4, 3, false);
        AddTownHouse(entities, world, "village-smithy", 38, 28, 90, 3, 2, false);
        AddTownHouse(entities, world, "village-temple", -38, 24, -90, 3, 3, true);
        AddTownHouse(entities, world, "village-house-southwest", -38, -34, -90, 3, 2, false);
        AddTownHouse(entities, world, "village-house-southeast", 40, -48, 90, 3, 2, false);
        AddTownHouse(entities, world, "village-house-northwest", -43, 53, -90, 3, 2, false);
        AddTownHouse(entities, world, "village-house-northeast", 42, 56, 90, 3, 2, false);
        AddTownHouse(entities, world, "village-hall", 0, 64, 180, 4, 2, true);

        AddTownProp(entities, world, "village-fountain", "fountain-round", 0, 6,
            new(3, 3, 3), 0, .05f, "landmark", "nonblocking", "service");
        AddTownProp(entities, world, "village-market-red", "stall-red", -12, 17,
            new(4, 4, 4), 180, .08f, "market", "service");
        AddTownProp(entities, world, "village-market-green", "stall-green", 12, 17,
            new(4, 4, 4), 180, .08f, "market", "service");
        AddTownProp(entities, world, "village-market-bench-a", "stall-bench", -9, 12,
            new(3.2f, 3.2f, 3.2f), 90, .08f, "market");
        AddTownProp(entities, world, "village-market-bench-b", "stall-bench", 9, 12,
            new(3.2f, 3.2f, 3.2f), -90, .08f, "market");
        AddTownProp(entities, world, "village-cart", "cart", -15, -5,
            new(2.8f, 2.8f, 2.8f), 22, .08f, "market");
        AddTownProp(entities, world, "village-inn-banner", "banner-red", 17.6f, -12,
            new(4, 4, 4), 90, 2.2f, "inn", "landmark", "nonblocking");
        AddTownProp(entities, world, "village-temple-banner", "banner-green", -31.6f, 24,
            new(4.5f, 4.5f, 4.5f), -90, 2.2f, "temple", "healing", "landmark", "nonblocking");

        AddPrimitive(entities, world, "smithy-anvil", 29, 23, .55f, new(1.4f, .75f, .9f),
            "#454b52", ["decor", "solid", "blacksmith"], "box", "Enclume de Borin");
        AddPrimitive(entities, world, "smithy-forge", 28, 38, 1f, new(2.8f, 2f, 2.2f),
            "#ff6d2d", ["decor", "solid", "light", "blacksmith", "service"], "box", "Forge ardente", true);
        AddDungeonProp(entities, world, "smithy-crates", "crates-stacked", 47, 25,
            new(1.2f, 1.2f, 1.2f), -20, .08f, "blacksmith", "storage");
        AddDungeonProp(entities, world, "smithy-barrels", "barrel-small-stack", 47, 32,
            Vector3Value.One, 16, .08f, "blacksmith", "storage");
        AddPrimitive(entities, world, "temple-beacon", -29, 19, 1.6f, new(.9f, 3.2f, .9f),
            "#bde9ff", ["decor", "solid", "light", "healing", "temple", "service"], "cylinder", "Flamme de soin", true);

        AddTownProp(entities, world, "village-inn-steps", "stairs-wide-wood", 16, -12,
            new(1.45f, 1.45f, 1.45f), 90, .04f, "inn", "entrance", "nonblocking");
        AddDungeonProp(entities, world, "village-inn-barrels", "barrel-small-stack", 31, -22,
            new(1.1f, 1.1f, 1.1f), 18, .06f, "inn", "storage");
        AddDungeonProp(entities, world, "village-inn-crates", "crates-stacked", 33, -17,
            new(1.1f, 1.1f, 1.1f), -12, .06f, "inn", "storage");
        AddTownProp(entities, world, "village-smithy-cart", "cart", 48, 18,
            new(1.6f, 1.6f, 1.6f), -22, .06f, "blacksmith", "storage", "nonblocking");
        AddDungeonProp(entities, world, "village-smithy-table", "table-medium", 30, 36,
            Vector3Value.One, 90, .06f, "blacksmith", "workbench");
        AddTownProp(entities, world, "village-temple-bench-a", "stall-bench", -27, 13,
            new(2.3f, 2.3f, 2.3f), 0, .05f, "temple", "healing", "nonblocking");
        AddTownProp(entities, world, "village-temple-bench-b", "stall-bench", -40, 10,
            new(2.3f, 2.3f, 2.3f), 0, .05f, "temple", "healing", "nonblocking");
        foreach (var (key, x, z) in new[]
                 {
                     ("temple-a", -47f, 15f), ("temple-b", -49f, 31f),
                     ("square-a", -19f, 1f), ("square-b", 19f, 1f),
                     ("inn-a", 13f, -25f), ("inn-b", 35f, -5f),
                 })
        {
            AddNatureProp(entities, world, $"village-flower-{key}", "purple-flowers", x, z,
                new(2.8f, 2.8f, 2.8f), 0, .04f, "village", "garden");
        }
        foreach (var (key, x, z) in new[]
                 {
                     ("square-west", -22f, 7f), ("square-east", 22f, 7f),
                     ("inn", 15f, -19f), ("smithy", 29f, 35f), ("temple", -29f, 30f),
                 })
        {
            AddDungeonProp(entities, world, $"village-lantern-{key}", "torch-lit", x, z,
                new(1.15f, 1.15f, 1.15f), 0, 2.15f, "village", "light", "nonblocking");
        }

        for (var z = -58; z <= 58; z += 8)
        {
            AddTownProp(entities, world, $"village-fence-west-{z + 58}", "fence", -68, z,
                new(5, 5, 5), 0, .05f, "boundary");
            AddTownProp(entities, world, $"village-fence-east-{z + 58}", "fence", 68, z,
                new(5, 5, 5), 0, .05f, "boundary");
        }
        for (var x = -60; x <= 60; x += 8)
        {
            if (MathF.Abs(x) < 10) continue;
            AddTownProp(entities, world, $"village-fence-north-{x + 60}", "fence", x, 68,
                new(5, 5, 5), 90, .05f, "boundary");
            AddTownProp(entities, world, $"village-fence-south-{x + 60}", "fence", x, -68,
                new(5, 5, 5), 90, .05f, "boundary");
        }
        AddTownProp(entities, world, "village-fence-south-gate", "fence-gate", 0, -68,
            new(5, 5, 5), 90, .05f, "boundary", "gate", "nonblocking");
        AddConnectedAreaBorderDressing(entities, world, dimensions, ConnectedAreaTheme.Village);

        AddPortalEntity(entities, world, "portal-village-forest", 0, -72, "Vers la Forêt Oubliée");
        AddPortalEntity(entities, world, "portal-village-inn", 15, -12, "Entrer dans l'auberge");
        return CreateConnectedArea(areaId, "clairval", "Clairval", AreaKind.Exterior, dimensions, entities,
            world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.Village));
    }

    private static AreaDefinition CreateVillageArea()
    {
        var areaId = Ids.Area("clairval");
        var dimensions = new AreaDimensions(176, 176);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Village);
        var entities = new List<EntityInstance>
        {
            Entity("clairval-spawn", "spawn", 0, -56, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
            Entity("clairval-mayor", "guide", -6, -8, world, .08f, ["npc", "villager"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Maire Ysilde")), Vector3Value.One),
            Entity("clairval-smith", "guide", 34, 8, world, .08f, ["npc", "blacksmith"],
                AnimatedCharacterVisual("barbarian", ("runtime.displayName", "Borin, forgeron")), Vector3Value.One),
            Entity("clairval-healer", "guide", -34, 16, world, .08f, ["npc", "healer"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Sœur Althea")), Vector3Value.One),
        };
        AddProp(entities, world, "village-inn-shell", "building-structure", 15, 2,
            new(4.8f, 4.2f, 4.8f), -8, .2f);
        AddProp(entities, world, "village-inn-roof", "building-roof", 15, 2,
            new(4.9f, 4.9f, 4.9f), -8, 5.4f);
        AddPrimitive(entities, world, "village-inn-sign", 8, -1, 1.8f, new(.35f, 2.8f, .25f),
            "#6f3f24", ["decor", "solid"], "box", "Auberge du Cerf d'Ambre");
        AddProp(entities, world, "smithy-shell", "building-structure", 37, 11,
            new(3.4f, 3.2f, 3.4f), 18, .2f);
        AddPrimitive(entities, world, "smithy-anvil", 30, 5, .55f, new(1.3f, .8f, .8f),
            "#51555c", ["decor", "solid", "blacksmith"], "box", "Enclume de Borin");
        AddPrimitive(entities, world, "smithy-forge", 42, 3, .8f, new(2.2f, 1.6f, 1.4f),
            "#ff7a2f", ["decor", "solid", "light", "blacksmith"], "box", "Forge ardente", true);
        AddProp(entities, world, "temple-shell", "building-structure", -37, 18,
            new(3.8f, 3.8f, 3.8f), -14, .2f);
        AddPrimitive(entities, world, "temple-beacon", -29, 11, 1.6f, new(.9f, 3.2f, .9f),
            "#d9efff", ["decor", "solid", "light", "healing", "temple"], "cylinder", "Flamme de soin", true);
        AddProp(entities, world, "village-wagon", "platform", -7, 28, new(1.8f, 1.8f, 1.8f), 33);
        AddProp(entities, world, "village-well", "stones", 2, 12, new(2.2f, 2.2f, 2.2f), 0);
        AddProp(entities, world, "village-flag", "flag", 0, -18, new(2.1f, 2.1f, 2.1f), 0);
        foreach (var (key, x, z, yaw, scale) in new[]
                 {
                     ("northwest", -58f, -24f, 14f, 3.2f), ("north", -24f, 57f, -8f, 3f),
                     ("northeast", 46f, 49f, 20f, 3.25f), ("southeast", 57f, -31f, -18f, 3.1f),
                     ("southwest", -51f, -49f, 9f, 2.9f),
                 })
        {
            AddProp(entities, world, $"village-house-{key}", "building-structure", x, z,
                new(scale, scale, scale), yaw, .2f);
            AddProp(entities, world, $"village-roof-{key}", "building-roof", x, z,
                new(scale + .08f, scale + .08f, scale + .08f), yaw, 4.1f);
        }
        AddProp(entities, world, "village-market-tent-a", "tent", -14, 22, new(2.2f, 2.2f, 2.2f), 12);
        AddProp(entities, world, "village-market-tent-b", "tent", 17, 25, new(2.1f, 2.1f, 2.1f), -16);
        AddPrimitive(entities, world, "village-market-fire", 1, 31, .75f, new(1.1f, 1.6f, 1.1f),
            "#ff9a46", ["decor", "light", "market"], "cylinder", "Feu du marché", true);
        AddPortalEntity(entities, world, "portal-village-forest", 0, -72, "Vers la Forêt Oubliée");
        AddPortalEntity(entities, world, "portal-village-inn", 10, -2, "Entrer dans l'auberge");
        return CreateConnectedArea(areaId, "clairval", "Clairval", AreaKind.Exterior, dimensions, entities,
            world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.Village));
    }

    private static AreaDefinition CreateDetailedInnArea()
    {
        var areaId = Ids.Area("ember-inn");
        var dimensions = new AreaDimensions(54, 42);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Inn);
        var entities = new List<EntityInstance>
        {
            Entity("ember-inn-spawn", "spawn", 0, 14, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
            Entity("innkeeper", "guide", -15, -4, world, .08f, ["npc", "innkeeper"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Mara, aubergiste")), Vector3Value.One),
        };

        // A complete wood floor and modular timber shell replace the former single brown box.
        for (var x = -22; x <= 22; x += 4)
            for (var z = -16; z <= 16; z += 4)
                AddDungeonProp(entities, world, $"inn-floor-{x + 22}-{z + 16}", "floor-wood-large", x, z,
                    Vector3Value.One, 0, .015f, "floor", "nonblocking");

        for (var x = -22; x <= 22; x += 4)
        {
            var northKey = ((x + 22) / 4) % 2 == 0 ? "wall-wood-window-shutters" : "wall-wood";
            var southKey = x == -2 ? "wall-wood-door" :
                ((x + 22) / 4) % 3 == 0 ? "wall-wood-window-shutters" : "wall-wood";
            AddTownProp(entities, world, $"inn-wall-north-{x + 22}", northKey, x, -18,
                new(4, 4, 4), 90, .03f, "architecture", "wall", "nonblocking");
            AddTownProp(entities, world, $"inn-wall-south-{x + 22}", southKey, x, 18,
                new(4, 4, 4), 90, .03f, "architecture", "wall", "nonblocking");
        }
        for (var z = -14; z <= 14; z += 4)
        {
            var key = ((z + 14) / 4) % 2 == 0 ? "wall-wood-window-shutters" : "wall-wood";
            AddTownProp(entities, world, $"inn-wall-west-{z + 14}", key, -24, z,
                new(4, 4, 4), 0, .03f, "architecture", "wall", "nonblocking");
            AddTownProp(entities, world, $"inn-wall-east-{z + 14}", key, 24, z,
                new(4, 4, 4), 0, .03f, "architecture", "wall", "nonblocking");
        }

        // Dedicated L-shaped corner modules close the small diagonal gaps left by two
        // perpendicular flat wall meshes. Their stable authored placement also makes the
        // visual shell agree with the consolidated runtime collision shell.
        foreach (var (key, x, z, yaw) in new[]
                 {
                     ("northwest", -24f, -18f, 0f),
                     ("northeast", 24f, -18f, 90f),
                     ("southeast", 24f, 18f, 180f),
                     ("southwest", -24f, 18f, 270f),
                 })
        {
            AddTownProp(entities, world, $"inn-corner-{key}", "wall-wood-corner", x, z,
                new(4, 4, 4), yaw, .03f, "architecture", "wall", "corner", "nonblocking");
        }

        foreach (var (key, x, z) in new[]
                 {
                     ("a", -3f, -5f), ("b", 7f, -5f), ("c", -3f, 4f),
                     ("d", 7f, 4f), ("e", 17f, 4f),
                 })
        {
            AddDungeonProp(entities, world, $"inn-table-{key}", "table-long-decorated", x, z,
                Vector3Value.One, 90, .03f, "furniture", "table");
            AddDungeonProp(entities, world, $"inn-chair-{key}-left", "chair", x - 3, z,
                Vector3Value.One, 90, .03f, "furniture", "chair");
            AddDungeonProp(entities, world, $"inn-chair-{key}-right", "chair", x + 3, z,
                Vector3Value.One, -90, .03f, "furniture", "chair");
            AddDungeonProp(entities, world, $"inn-chair-{key}-north", "chair", x, z - 2,
                Vector3Value.One, 0, .03f, "furniture", "chair");
            AddDungeonProp(entities, world, $"inn-chair-{key}-south", "chair", x, z + 2,
                Vector3Value.One, 180, .03f, "furniture", "chair");
            AddDungeonProp(entities, world, $"inn-food-{key}", key is "a" or "d" ? "plate-food-a" : "plate-food-b",
                x, z, Vector3Value.One, 0, 1.05f, "food", "tabletop", "nonblocking");
        }

        for (var index = 0; index < 3; index++)
            AddDungeonProp(entities, world, $"inn-bar-{index}", "table-medium", -17, -7 + (index * 4),
                new(1.15f, 1.15f, 1.15f), 0, .03f, "bar", "furniture", "service");
        AddDungeonProp(entities, world, "inn-bar-shelf-a", "shelf-large", -23, -7,
            new(2.2f, 2.2f, 2.2f), 90, 1.15f, "bar", "storage", "nonblocking");
        AddDungeonProp(entities, world, "inn-bar-shelf-b", "shelf-small-candles", -23, 1,
            new(2.2f, 2.2f, 2.2f), 90, 1.15f, "bar", "storage", "nonblocking");
        AddDungeonProp(entities, world, "inn-barrels-a", "barrel-large-decorated", -20, 9,
            new(.85f, .85f, .85f), 12, .03f, "bar", "storage");
        AddDungeonProp(entities, world, "inn-barrels-b", "barrel-small-stack", -15, 10,
            Vector3Value.One, -18, .03f, "bar", "storage");
        AddDungeonProp(entities, world, "inn-storage-crates", "crates-stacked", -20, 14,
            Vector3Value.One, 10, .03f, "storage");
        AddDungeonProp(entities, world, "inn-storage-chest", "chest", -14, 14,
            Vector3Value.One, 180, .03f, "storage");

        for (var index = 0; index < 4; index++)
        {
            var x = -8 + (index * 8);
            AddDungeonProp(entities, world, $"inn-bed-{index}", "bed-decorated", x, -14,
                new(.92f, .92f, .92f), 90, .03f, "bed", "furniture", "rest");
            AddDungeonProp(entities, world, $"inn-bed-candle-{index}", "candle-triple", x + 2.2f, -15.5f,
                new(.75f, .75f, .75f), 0, .7f, "light", "nonblocking");
        }

        AddPrimitive(entities, world, "inn-hearth", 18, -11, 1.2f, new(4.4f, 2.5f, 2.2f),
            "#f06c2f", ["decor", "solid", "light", "hearth", "service"], "box", "Grand âtre", true);
        AddDungeonProp(entities, world, "inn-hearth-column-a", "column", 15.5f, -12.2f,
            new(.8f, .8f, .8f), 0, .03f, "hearth", "nonblocking");
        AddDungeonProp(entities, world, "inn-hearth-column-b", "column", 20.5f, -12.2f,
            new(.8f, .8f, .8f), 0, .03f, "hearth", "nonblocking");
        AddDungeonProp(entities, world, "inn-banner", "banner-red", 0, -17.8f,
            new(1.4f, 1.4f, 1.4f), 0, 1.8f, "decor", "nonblocking");

        AddDungeonProp(entities, world, "inn-bottle-cluster-a", "bottle-green", -17, -3,
            new(.8f, .8f, .8f), 0, 1.2f, "bar", "tabletop", "nonblocking");
        AddDungeonProp(entities, world, "inn-bottle-cluster-b", "bottle-green", -18, 1,
            new(.8f, .8f, .8f), 0, 1.2f, "bar", "tabletop", "nonblocking");
        AddDungeonProp(entities, world, "inn-candle-center", "candle-lit", 2, 0,
            new(.9f, .9f, .9f), 0, 1.15f, "light", "tabletop", "nonblocking");

        foreach (var (key, x, z, yaw) in new[]
                 {
                     ("nw", -22.8f, -9f, 90f), ("sw", -22.8f, 7f, 90f),
                     ("ne", 22.8f, -3f, -90f), ("se", 22.8f, 11f, -90f),
                 })
            AddDungeonProp(entities, world, $"inn-torch-{key}", "torch-lit", x, z,
                new(1.2f, 1.2f, 1.2f), yaw, 1.9f, "light", "nonblocking");

        AddCollisionProxy(entities, world, "inn-collision-north", 0, -18, 2, new(48, 4, .5f), 0, "wall");
        AddCollisionProxy(entities, world, "inn-collision-south-left", -13, 18, 2, new(22, 4, .5f), 0, "wall");
        AddCollisionProxy(entities, world, "inn-collision-south-right", 13, 18, 2, new(22, 4, .5f), 0, "wall");
        AddCollisionProxy(entities, world, "inn-collision-west", -24, 0, 2, new(.5f, 4, 36), 0, "wall");
        AddCollisionProxy(entities, world, "inn-collision-east", 24, 0, 2, new(.5f, 4, 36), 0, "wall");
        AddPortalEntity(entities, world, "portal-inn-village", 0, 16, "Sortir vers Clairval");
        return CreateConnectedArea(areaId, "ember-inn", "Auberge du Cerf d'Ambre", AreaKind.Interior,
            dimensions, entities, world, CreateConnectedEnvironment(areaId, AreaKind.Interior, ConnectedAreaTheme.Inn));
    }

    private static AreaDefinition CreateInnArea()
    {
        var areaId = Ids.Area("ember-inn");
        var dimensions = new AreaDimensions(54, 42);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Inn);
        var entities = new List<EntityInstance>
        {
            Entity("ember-inn-spawn", "spawn", 0, 14, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
            Entity("innkeeper", "guide", -9, -5, world, .08f, ["npc", "innkeeper"],
                AnimatedCharacterVisual("mage", ("runtime.displayName", "Mara, aubergiste")), Vector3Value.One),
        };
        AddPrimitive(entities, world, "inn-floor", 0, 0, -.2f, new(48, .4f, 36), "#68422c",
            ["floor", "solid"], "box", "Plancher de l'auberge");
        AddPrimitive(entities, world, "inn-wall-n", 0, -18, 2.5f, new(48, 5, .6f), "#9a6748",
            ["wall", "solid"], "box", "Mur nord");
        AddPrimitive(entities, world, "inn-wall-s", 0, 18, 2.5f, new(48, 5, .6f), "#9a6748",
            ["wall", "solid"], "box", "Mur sud");
        AddPrimitive(entities, world, "inn-wall-e", 24, 0, 2.5f, new(.6f, 5, 36), "#9a6748",
            ["wall", "solid"], "box", "Mur est");
        AddPrimitive(entities, world, "inn-wall-w", -24, 0, 2.5f, new(.6f, 5, 36), "#9a6748",
            ["wall", "solid"], "box", "Mur ouest");
        AddPrimitive(entities, world, "inn-bar", -11, -2, .8f, new(9, 1.5f, 1.4f), "#7b492b",
            ["decor", "solid", "service"], "box", "Comptoir");
        foreach (var (key, x, z) in new[] { ("a", 5f, -5f), ("b", 13f, -5f), ("c", 5f, 5f), ("d", 13f, 5f) })
        {
            AddPrimitive(entities, world, $"inn-table-{key}", x, z, .65f, new(3.4f, .3f, 2.4f), "#825436",
                ["decor", "solid", "table"], "box", $"Table {key.ToUpperInvariant()}");
        }
        AddPrimitive(entities, world, "inn-hearth", -17, -12, 1f, new(3.6f, 2.2f, 1.6f), "#ff6a2a",
            ["decor", "solid", "light", "hearth"], "box", "Grand âtre", true);
        for (var index = 0; index < 4; index++)
        {
            AddPrimitive(entities, world, $"inn-bed-{index}", -14 + (index * 8), 11, .45f,
                new(4.2f, .7f, 2.2f), "#7e2f36", ["decor", "solid", "rest", "bed"], "box",
                $"Lit {index + 1}");
        }
        AddPrimitive(entities, world, "inn-lantern-a", -4, -10, 2.5f, new(.3f, .6f, .3f), "#ffd08a",
            ["decor", "light"], "sphere", "Lanterne ouest", true);
        AddPrimitive(entities, world, "inn-lantern-b", 11, -10, 2.5f, new(.3f, .6f, .3f), "#ffd08a",
            ["decor", "light"], "sphere", "Lanterne est", true);
        AddPrimitive(entities, world, "inn-chandelier", 1, 1, 3.4f, new(.65f, .65f, .65f), "#ffe0a0",
            ["decor", "light"], "sphere", "Lustre central", true);
        AddPrimitive(entities, world, "inn-keg-a", -17, 6, .65f, new(1.2f, 1.4f, 1.2f), "#7c4b2a",
            ["decor", "solid"], "cylinder", "Tonneau de bière");
        AddPrimitive(entities, world, "inn-keg-b", -19, 9, .65f, new(1.2f, 1.4f, 1.2f), "#7c4b2a",
            ["decor", "solid"], "cylinder", "Tonneau de cidre");
        AddPortalEntity(entities, world, "portal-inn-village", 0, 16, "Sortir vers Clairval");
        return CreateConnectedArea(areaId, "ember-inn", "Auberge du Cerf d'Ambre", AreaKind.Interior,
            dimensions, entities, world, CreateConnectedEnvironment(areaId, AreaKind.Interior, ConnectedAreaTheme.Inn));
    }

    private static AreaDefinition CreateDetailedGnollFortressArea()
    {
        var areaId = Ids.Area("gnoll-fortress");
        var dimensions = new AreaDimensions(192, 192);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Fortress);
        var entities = new List<EntityInstance>
        {
            Entity("gnoll-fortress-spawn", "spawn", 0, -76, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
        };

        for (var index = 0; index < 8; index++)
            AddCreature(entities, world, $"gnoll-warrior-{index}", "scout", -31 + ((index % 4) * 20),
                -18 + ((index / 4) * 34), $"Gnoll maraudeur {index + 1}", "gnoll", false);
        AddCreature(entities, world, "gnoll-warlord", "boss", 0, 40, "Kharzug, chef de meute", "gnoll", true);

        for (var z = -38; z <= 62; z += 6)
        {
            AddCastleProp(entities, world, $"fortress-wall-west-{z + 38}", "wall", -49, z,
                new(6, 6, 6), 0, .05f, "fortress", "wall");
            AddCastleProp(entities, world, $"fortress-wall-east-{z + 38}", "wall", 49, z,
                new(6, 6, 6), 0, .05f, "fortress", "wall");
        }
        for (var x = -43; x <= 43; x += 6)
            AddCastleProp(entities, world, $"fortress-wall-north-{x + 43}", "wall", x, 68,
                new(6, 6, 6), 90, .05f, "fortress", "wall");
        for (var x = -43; x <= 43; x += 6)
        {
            if (MathF.Abs(x) < 9) continue;
            AddCastleProp(entities, world, $"fortress-wall-south-{x + 43}", "wall", x, -44,
                new(6, 6, 6), 90, .05f, "fortress", "wall");
        }
        AddCastleProp(entities, world, "fortress-gate-arch", "wall-narrow-gate", 0, -44,
            new(12, 12, 12), 90, .05f, "fortress", "gate", "landmark");
        AddCastleProp(entities, world, "fortress-gate", "gate", 0, -43.2f,
            new(8, 8, 8), 90, .05f, "fortress", "gate");

        foreach (var (key, x, z) in new[]
                 {
                     ("sw", -49f, -44f), ("se", 49f, -44f),
                     ("nw", -49f, 68f), ("ne", 49f, 68f),
                 })
        {
            AddCastleProp(entities, world, $"fortress-tower-{key}-base", "tower-square-base", x, z,
                new(9, 9, 9), 0, .04f, "fortress", "tower");
            AddCastleProp(entities, world, $"fortress-tower-{key}-mid", "tower-square-mid", x, z,
                new(9, 9, 9), 0, 8.6f, "fortress", "tower", "nonblocking");
            AddCastleProp(entities, world, $"fortress-tower-{key}-roof", "tower-square-top-roof", x, z,
                new(9.5f, 9.5f, 9.5f), 0, 17.1f, "fortress", "tower", "roof", "nonblocking");
            AddCastleProp(entities, world, $"fortress-tower-{key}-banner", "flag-banner-long", x, z,
                new(4, 4, 4), key is "sw" or "nw" ? 0 : 180, 18.5f,
                "fortress", "banner", "nonblocking");
        }

        AddCastleProp(entities, world, "fortress-keep-base", "tower-square-base", 0, 49,
            new(10, 10, 10), 0, .04f, "fortress", "keep", "landmark");
        AddCastleProp(entities, world, "fortress-keep-mid", "tower-square-mid", 0, 49,
            new(10, 10, 10), 0, 9.6f, "fortress", "keep", "nonblocking");
        AddCastleProp(entities, world, "fortress-keep-roof", "tower-square-top-roof", 0, 49,
            new(10.8f, 10.8f, 10.8f), 0, 19.1f, "fortress", "keep", "roof", "nonblocking");
        AddCastleProp(entities, world, "fortress-siege-tower", "siege-tower", -30, 35,
            new(5.5f, 5.5f, 5.5f), 15, .04f, "fortress", "siege", "landmark");

        for (var index = 0; index < 6; index++)
        {
            var x = -35 + (index * 14);
            AddCastleProp(entities, world, $"fortress-palisade-{index}", "wall-narrow-wood-fence", x, -30,
                new(6, 6, 6), 90, .04f, "fortress", "palisade");
        }

        AddProp(entities, world, "fortress-tent-a", "tent", -26, 18, new(3.4f, 3.4f, 3.4f), 14);
        AddProp(entities, world, "fortress-tent-b", "tent", 27, 21, new(3.4f, 3.4f, 3.4f), -18);
        AddDungeonProp(entities, world, "fortress-table", "table-long-decorated", 0, 12,
            new(1.2f, 1.2f, 1.2f), 90, .04f, "fortress", "war-table");
        AddDungeonProp(entities, world, "fortress-storage-a", "crates-stacked", -36, -5,
            new(1.5f, 1.5f, 1.5f), 12, .04f, "fortress", "storage");
        AddDungeonProp(entities, world, "fortress-storage-b", "barrel-small-stack", 35, -4,
            new(1.4f, 1.4f, 1.4f), -14, .04f, "fortress", "storage");
        AddDungeonProp(entities, world, "fortress-chest", "chest", 14, 38,
            new(1.3f, 1.3f, 1.3f), 180, .04f, "fortress", "loot");

        AddNatureProp(entities, world, "fortress-brazier-ring-a", "campfire-stones", -17, 30,
            new(2.6f, 2.6f, 2.6f), 0, .05f, "fortress", "brazier");
        AddNatureProp(entities, world, "fortress-brazier-ring-b", "campfire-stones", 17, 30,
            new(2.6f, 2.6f, 2.6f), 0, .05f, "fortress", "brazier");
        AddPrimitive(entities, world, "fortress-brazier-a", -17, 30, .62f, new(.65f, 1.1f, .65f),
            "#ff512d", ["decor", "light", "fortress", "nonblocking"], "cone", "Brasero ouest", true);
        AddPrimitive(entities, world, "fortress-brazier-b", 17, 30, .62f, new(.65f, 1.1f, .65f),
            "#ff512d", ["decor", "light", "fortress", "nonblocking"], "cone", "Brasero est", true);
        AddPrimitive(entities, world, "fortress-bone-standard", 0, 34, 3f, new(.9f, 6f, .9f),
            "#d7c4a0", ["decor", "solid", "landmark", "fortress"], "cylinder", "Totem de la Meute");

        AddCollisionProxy(entities, world, "fortress-collision-west", -49, 12, 4,
            new(4, 8, 112), 0, "wall", "fortress");
        AddCollisionProxy(entities, world, "fortress-collision-east", 49, 12, 4,
            new(4, 8, 112), 0, "wall", "fortress");
        AddCollisionProxy(entities, world, "fortress-collision-north", 0, 68, 4,
            new(94, 8, 4), 0, "wall", "fortress");
        AddCollisionProxy(entities, world, "fortress-collision-south-left", -29, -44, 4,
            new(40, 8, 4), 0, "wall", "fortress");
        AddCollisionProxy(entities, world, "fortress-collision-south-right", 29, -44, 4,
            new(40, 8, 4), 0, "wall", "fortress");
        AddCollisionProxy(entities, world, "fortress-collision-keep", 0, 49, 7,
            new(10, 14, 10), 0, "fortress", "keep");
        AddCastleProp(entities, world, "fortress-training-stairs", "wall-narrow-stairs", -28, -17,
            new(4.5f, 4.5f, 4.5f), 90, .05f, "fortress", "training");
        AddCastleProp(entities, world, "fortress-training-banner", "flag-banner-long", -28, -11,
            new(3.5f, 3.5f, 3.5f), 0, 7.5f, "fortress", "training", "nonblocking");
        AddDungeonProp(entities, world, "fortress-feast-barrels", "barrel-large-decorated", 31, 27,
            new(1.25f, 1.25f, 1.25f), 8, .05f, "fortress", "storage");
        AddDungeonProp(entities, world, "fortress-feast-crates", "crates-stacked", 36, 31,
            new(1.3f, 1.3f, 1.3f), -14, .05f, "fortress", "storage");
        foreach (var (key, x, z, yaw) in new[]
                 {
                     ("west", -18f, 8f, 0f), ("east", 18f, 8f, 180f),
                 })
        {
            AddDungeonProp(entities, world, $"fortress-feast-table-{key}", "table-long-decorated", x, z,
                new(1.05f, 1.05f, 1.05f), yaw, .05f, "fortress", "feast");
            AddDungeonProp(entities, world, $"fortress-feast-chair-{key}-a", "chair", x - 3, z,
                Vector3Value.One, yaw + 90, .05f, "fortress", "feast");
            AddDungeonProp(entities, world, $"fortress-feast-chair-{key}-b", "chair", x + 3, z,
                Vector3Value.One, yaw - 90, .05f, "fortress", "feast");
        }
        AddTownProp(entities, world, "fortress-supply-cart", "cart", 30, -18,
            new(1.35f, 1.35f, 1.35f), -24, .05f, "fortress", "storage", "nonblocking");
        AddConnectedAreaBorderDressing(entities, world, dimensions, ConnectedAreaTheme.Fortress);
        AddPortalEntity(entities, world, "portal-fortress-forest", 0, -82, "Retour vers la forêt");
        return CreateConnectedArea(areaId, "gnoll-fortress", "Forteresse de la Meute Grise", AreaKind.Exterior,
            dimensions, entities, world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.Fortress));
    }

    private static AreaDefinition CreateGnollFortressArea()
    {
        var areaId = Ids.Area("gnoll-fortress");
        var dimensions = new AreaDimensions(192, 192);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Fortress);
        var entities = new List<EntityInstance>
        {
            Entity("gnoll-fortress-spawn", "spawn", 0, -76, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
        };
        for (var index = 0; index < 5; index++)
        {
            AddCreature(entities, world, $"gnoll-warrior-{index}", "scout", -28 + (index * 14),
                -18 + ((index % 2) * 22), $"Gnoll maraudeur {index + 1}", "gnoll", false);
        }
        AddCreature(entities, world, "gnoll-warlord", "boss", 0, 48, "Kharzug, chef de meute", "gnoll", true);
        AddPrimitive(entities, world, "fortress-wall-west", -49, 14, 3f, new(5, 7, 96), "#4c443d",
            ["wall", "solid", "fortress"], "box", "Mur ouest");
        AddPrimitive(entities, world, "fortress-wall-east", 49, 14, 3f, new(5, 7, 96), "#4c443d",
            ["wall", "solid", "fortress"], "box", "Mur est");
        AddPrimitive(entities, world, "fortress-wall-north", 0, 69, 3f, new(84, 7, 5), "#4c443d",
            ["wall", "solid", "fortress"], "box", "Mur nord");
        AddPrimitive(entities, world, "fortress-gate-left", -23, -43, 3f, new(36, 7, 5), "#4c443d",
            ["wall", "solid", "fortress"], "box", "Porte gauche");
        AddPrimitive(entities, world, "fortress-gate-right", 23, -43, 3f, new(36, 7, 5), "#4c443d",
            ["wall", "solid", "fortress"], "box", "Porte droite");
        foreach (var (key, x, z) in new[] { ("sw", -48f, -42f), ("se", 48f, -42f), ("nw", -48f, 68f), ("ne", 48f, 68f) })
        {
            AddPrimitive(entities, world, $"fortress-tower-{key}", x, z, 5f, new(10, 10, 10), "#5c5148",
                ["pillar", "solid", "fortress"], "cylinder", $"Tour {key.ToUpperInvariant()}");
        }
        AddProp(entities, world, "fortress-hall", "building-structure", 0, 44, new(4.5f, 4.5f, 4.5f), 0);
        AddPrimitive(entities, world, "fortress-brazier-a", -19, 20, 1.1f, new(1.2f, 2.2f, 1.2f), "#ff512d",
            ["decor", "light"], "cylinder", "Brasero ouest", true);
        AddPrimitive(entities, world, "fortress-brazier-b", 19, 20, 1.1f, new(1.2f, 2.2f, 1.2f), "#ff512d",
            ["decor", "light"], "cylinder", "Brasero est", true);
        AddProp(entities, world, "fortress-tent-a", "tent", -26, 31, new(2.7f, 2.7f, 2.7f), 14);
        AddProp(entities, world, "fortress-tent-b", "tent", 27, 33, new(2.7f, 2.7f, 2.7f), -18);
        AddProp(entities, world, "fortress-watch-platform", "platform", -31, -19,
            new(2.8f, 2.8f, 2.8f), 8);
        AddPrimitive(entities, world, "fortress-bone-standard", 0, 55, 3f, new(.8f, 6f, .8f), "#d7c4a0",
            ["decor", "solid", "landmark"], "cylinder", "Totem de la Meute");
        AddPortalEntity(entities, world, "portal-fortress-forest", 0, -82, "Retour vers la forêt");
        return CreateConnectedArea(areaId, "gnoll-fortress", "Forteresse de la Meute Grise", AreaKind.Exterior,
            dimensions, entities, world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.Fortress));
    }

    private static AreaDefinition CreateDetailedDesertArea()
    {
        var areaId = Ids.Area("amber-desert");
        var dimensions = new AreaDimensions(144, 144);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.Desert);
        var entities = new List<EntityInstance>
        {
            Entity("amber-desert-spawn", "spawn", -48, -48, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
        };
        // Three readable landmarks frame the traversal: caravan, oasis and ruined solar shrine.
        foreach (var (key, x, z, yaw, scale) in new[]
                 {
                     ("west", -31f, -13f, 22f, 7.5f),
                     ("north", -18f, -2f, -18f, 7f),
                     ("east", -5f, -16f, 8f, 6.5f),
                 })
        {
            AddNatureProp(entities, world, $"desert-caravan-tent-{key}", "detailed-open-tent", x, z,
                new(scale, scale, scale), yaw, .08f, "camp", "tent");
        }
        AddTownProp(entities, world, "desert-caravan-cart", "cart", -38, 2, new(1.6f, 1.6f, 1.6f), 24,
            .08f, "camp", "nonblocking");
        AddTownProp(entities, world, "desert-caravan-stall", "stall-red", -7, 1, new(1.25f, 1.25f, 1.25f), -12,
            .08f, "camp", "nonblocking");
        AddDungeonProp(entities, world, "desert-caravan-table", "table-long-decorated", -19, -16,
            new(1.25f, 1.25f, 1.25f), 8, .08f, "camp", "nonblocking");
        AddDungeonProp(entities, world, "desert-caravan-crates", "crates-stacked", -34, -5,
            new(1.35f, 1.35f, 1.35f), -18, .08f, "camp", "nonblocking");
        AddDungeonProp(entities, world, "desert-caravan-barrels", "barrel-small-stack", -8, -8,
            new(1.3f, 1.3f, 1.3f), 12, .08f, "camp", "nonblocking");
        AddNatureProp(entities, world, "desert-campfire-ring", "campfire-stones", -20, -6,
            new(4f, 4f, 4f), 0, .1f, "camp");
        AddPrimitive(entities, world, "desert-campfire-light", -20, -6, .62f,
            new(.42f, .9f, .42f), "#ff9a42", ["decor", "light", "camp", "nonblocking"],
            "cone", "Feu de caravane", true);
        AddTownProp(entities, world, "desert-camp-bench-a", "stall-bench", -27, -8,
            new(1.1f, 1.1f, 1.1f), 18, .08f, "camp", "nonblocking");
        AddTownProp(entities, world, "desert-camp-bench-b", "stall-bench", -13, -9,
            new(1.1f, 1.1f, 1.1f), -20, .08f, "camp", "nonblocking");

        AddNatureProp(entities, world, "desert-sun-obelisk", "stone-obelisk", 27, 27,
            new(9f, 9f, 9f), -8, .08f, "landmark", "ruin");
        AddCollisionProxy(entities, world, "desert-sun-obelisk-collision", 27, 27, 3.8f,
            new(3.4f, 7.5f, 3.4f), -8, "landmark", "ruin");
        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("west-a", "wall", 14f, 31f, 12f, 1.8f),
                     ("west-b", "wall-pillar", 7f, 30f, 12f, 1.8f),
                     ("east-a", "wall", 40f, 24f, 102f, 1.8f),
                     ("east-b", "wall-narrow-gate", 36f, 12f, 12f, 1.8f),
                     ("rear-a", "wall-narrow", 23f, 43f, 102f, 1.8f),
                     ("rear-b", "wall-pillar", 31f, 43f, 102f, 1.8f),
                 })
        {
            AddCastleProp(entities, world, $"desert-ruin-{key}", asset, x, z,
                new(scale, scale, scale), yaw, .08f, "ruin", "desert");
        }
        AddCollisionProxy(entities, world, "desert-ruin-west-collision", 10, 31, 2,
            new(12, 4, 2.4f), 12, "ruin");
        AddCollisionProxy(entities, world, "desert-ruin-east-collision", 39, 19, 2,
            new(12, 4, 2.4f), 102, "ruin");

        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("ridge-sw", "cliff-large-rock", -52f, 12f, 18f, 10f),
                     ("ridge-w", "cliff-block-slope-rock", -49f, 35f, -22f, 9f),
                     ("ridge-ne", "cliff-large-rock", 53f, 39f, 31f, 11f),
                     ("ridge-se", "cliff-block-slope-rock", 49f, -20f, 146f, 9f),
                     ("rock-a", "large-rock", 11f, -35f, 28f, 5.5f),
                     ("rock-b", "large-rock-c", 43f, -39f, -16f, 5f),
                     ("rock-c", "tall-rock-a", -48f, -26f, 9f, 5.5f),
                     ("rock-d", "tall-rock-c", -3f, 50f, 35f, 6f),
                 })
        {
            AddNatureProp(entities, world, $"desert-{key}", asset, x, z, new(scale, scale, scale), yaw,
                .08f, "desert", "rock");
        }
        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("oasis-palm-a", "palm-tall", -55f, 50f, 8f, 8f),
                     ("oasis-palm-b", "palm-bend", -45f, 55f, -25f, 7f),
                     ("oasis-palm-c", "palm-tall", -35f, 49f, 20f, 7.5f),
                     ("cactus-a", "cactus-tall", -52f, -15f, 10f, 8f),
                     ("cactus-b", "cactus-short", -25f, 28f, -15f, 7f),
                     ("cactus-c", "cactus-tall", 51f, 4f, 18f, 7f),
                     ("cactus-d", "cactus-short", 20f, -48f, 0f, 8f),
                     ("cactus-e", "cactus-tall", 55f, -45f, -22f, 6.5f),
                     ("cactus-f", "cactus-short", 3f, 54f, 12f, 7f),
                 })
        {
            AddNatureProp(entities, world, $"desert-{key}", asset, x, z, new(scale, scale, scale), yaw,
                .08f, "desert", "vegetation");
        }
        AddNatureProp(entities, world, "desert-oasis-bush-a", "detailed-bush", -48, 47,
            new(3.4f, 3.4f, 3.4f), 12, .05f, "oasis", "vegetation");
        AddNatureProp(entities, world, "desert-oasis-bush-b", "large-grass", -39, 55,
            new(3.1f, 3.1f, 3.1f), -15, .05f, "oasis", "vegetation");
        AddPrimitive(entities, world, "desert-oasis-water", -46, 53, .08f,
            new(15, .12f, 10), "#2aa7a1", ["decor", "oasis", "water", "nonblocking"],
            "box", "Bassin de l'oasis");
        AddConnectedAreaBorderDressing(entities, world, dimensions, ConnectedAreaTheme.Desert);
        AddPortalEntity(entities, world, "portal-desert-forest", -58, -56, "Retour vers la forêt");
        return CreateConnectedArea(areaId, "amber-desert", "Dunes d'Ambre", AreaKind.Exterior, dimensions,
            entities, world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.Desert));
    }

    private static AreaDefinition CreateDetailedSnowMountainArea()
    {
        var areaId = Ids.Area("snow-pass");
        var dimensions = new AreaDimensions(160, 208);
        var world = CreateConnectedWorld(areaId, dimensions, ConnectedAreaTheme.SnowMountain);
        var entities = new List<EntityInstance>
        {
            Entity("snow-pass-spawn", "spawn", 0, -82, world, .9f, ["player", "area-anchor"],
                AnimatedCharacterVisual("knight"), Vector3Value.One),
        };
        AddTownHouse(entities, world, "snow-refuge", -24, -13, -14, 3, 2, false);
        AddTownProp(entities, world, "snow-refuge-banner", "banner-green", -17, -7,
            new(1.3f, 1.3f, 1.3f), 76, 2.3f, "refuge", "service", "nonblocking");
        AddDungeonProp(entities, world, "snow-refuge-crates", "crates-stacked", -35, -1,
            new(1.25f, 1.25f, 1.25f), 9, .08f, "refuge", "nonblocking");
        AddDungeonProp(entities, world, "snow-refuge-barrels", "barrel-large-decorated", -14, -21,
            new(1.2f, 1.2f, 1.2f), -12, .08f, "refuge", "nonblocking");
        AddNatureProp(entities, world, "snow-expedition-tent", "detailed-open-tent", 16, -23,
            new(7f, 7f, 7f), 15, .08f, "camp", "tent");
        AddNatureProp(entities, world, "snow-campfire-ring", "campfire-stones", 8, -12,
            new(4f, 4f, 4f), 0, .08f, "camp");
        AddPrimitive(entities, world, "snow-campfire-light", 8, -12, .62f,
            new(.42f, .9f, .42f), "#ffb15c", ["decor", "light", "camp", "nonblocking"],
            "cone", "Feu du refuge", true);
        AddTownProp(entities, world, "snow-refuge-bench", "stall-bench", -13, -8,
            new(1.05f, 1.05f, 1.05f), 78, .08f, "refuge", "nonblocking");
        AddTownProp(entities, world, "snow-trail-stairs", "stairs-wide-wood", -4, 24,
            new(2.2f, 2.2f, 2.2f), -5, .08f, "bridge", "trail", "nonblocking");

        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("west-low", "cliff-block-slope-rock", -51f, -48f, 16f, 12f),
                     ("east-low", "cliff-large-rock", 52f, -31f, -22f, 12f),
                     ("west-mid", "cliff-large-rock", -55f, 12f, 33f, 14f),
                     ("east-mid", "cliff-block-slope-rock", 54f, 29f, 142f, 13f),
                     ("west-high", "cliff-corner-rock", -48f, 66f, -18f, 15f),
                     ("east-high", "cliff-large-rock", 48f, 77f, 26f, 15f),
                     ("summit", "cliff-block-slope-rock", 0f, 88f, 90f, 13f),
                     ("rock-a", "tall-rock-a", -34f, 42f, 18f, 7f),
                     ("rock-b", "large-rock", 31f, 53f, -20f, 7f),
                     ("rock-c", "tall-rock-c", -22f, 80f, 12f, 8f),
                 })
        {
            AddNatureProp(entities, world, $"snow-{key}", asset, x, z, new(scale, scale, scale), yaw,
                .08f, "snow", "rock");
        }
        AddCollisionProxy(entities, world, "snow-west-mid-collision", -55, 12, 3.5f,
            new(14, 7, 12), 33, "cliff", "snow");
        AddCollisionProxy(entities, world, "snow-east-mid-collision", 54, 29, 3.5f,
            new(13, 7, 11), 142, "cliff", "snow");

        foreach (var (key, x, z, yaw, scale, round) in new[]
                 {
                     ("pine-01", -54f, -70f, 8f, 8f, false), ("pine-02", -39f, -57f, -12f, 7f, true),
                     ("pine-03", 43f, -62f, 22f, 8f, false), ("pine-04", 59f, -49f, -8f, 7f, true),
                     ("pine-05", -60f, -18f, 18f, 9f, false), ("pine-06", -43f, -5f, -20f, 7f, true),
                     ("pine-07", 43f, -9f, 10f, 8f, false), ("pine-08", 61f, 4f, -18f, 7f, true),
                     ("pine-09", -62f, 31f, 12f, 9f, false), ("pine-10", -42f, 51f, -10f, 8f, true),
                     ("pine-11", 42f, 41f, 20f, 8f, false), ("pine-12", 62f, 57f, -14f, 9f, true),
                     ("pine-13", -61f, 78f, 6f, 9f, false), ("pine-14", -35f, 91f, -20f, 8f, true),
                     ("pine-15", 31f, 86f, 14f, 8f, false), ("pine-16", 63f, 91f, -8f, 9f, true),
                     ("pine-17", -18f, 50f, 18f, 6f, false), ("pine-18", 21f, 68f, -15f, 6.5f, true),
                 })
        {
            AddNatureProp(entities, world, $"snow-{key}", round ? "pine-round" : "pine-tall", x, z,
                new(scale, scale, scale), yaw, .08f, "snow", "tree");
        }

        AddCastleProp(entities, world, "snow-beacon-base", "tower-square-base", 0, 78,
            new(2.4f, 2.4f, 2.4f), 0, .08f, "beacon", "landmark");
        AddCastleProp(entities, world, "snow-beacon-mid", "tower-square-mid", 0, 78,
            new(2.4f, 2.4f, 2.4f), 0, 5.5f, "beacon", "landmark");
        AddCastleProp(entities, world, "snow-beacon-roof", "tower-square-top-roof", 0, 78,
            new(2.4f, 2.4f, 2.4f), 0, 11f, "beacon", "landmark");
        AddCollisionProxy(entities, world, "snow-beacon-collision", 0, 78, 4,
            new(9, 8, 9), 0, "beacon", "landmark");
        AddPrimitive(entities, world, "snow-beacon-light", 0, 78, 14f,
            new(.8f, 1.6f, .8f), "#8fd8ff", ["decor", "light", "landmark", "nonblocking"],
            "cylinder", "Balise du col", true);
        foreach (var (key, x, z, yaw) in new[]
                 {
                     ("low-west", -11f, -58f, -8f), ("low-east", 10f, -37f, 10f),
                     ("mid-west", -12f, 18f, -6f), ("mid-east", 11f, 47f, 8f),
                 })
        {
            AddPrimitive(entities, world, $"snow-trail-post-{key}", x, z, 1.25f,
                new(.25f, 2.5f, .25f), "#5b3827", ["decor", "trail", "nonblocking"],
                "box", "Balise du sentier");
            AddDungeonProp(entities, world, $"snow-trail-light-{key}", "torch-lit", x, z,
                Vector3Value.One, yaw, 2.35f, "snow", "trail", "light", "nonblocking");
        }
        AddConnectedAreaBorderDressing(entities, world, dimensions, ConnectedAreaTheme.SnowMountain);
        AddPortalEntity(entities, world, "portal-snow-forest", 0, -92, "Retour vers la forêt");
        return CreateConnectedArea(areaId, "snow-pass", "Col des Hautes Neiges", AreaKind.Exterior, dimensions,
            entities, world, CreateConnectedEnvironment(areaId, AreaKind.Exterior, ConnectedAreaTheme.SnowMountain));
    }

    private static AreaDefinition CreateConnectedArea(AreaId areaId, string slug, string displayName, AreaKind kind,
        AreaDimensions dimensions, IReadOnlyList<EntityInstance> entities, AreaWorldProfile world,
        AreaEnvironmentProfile environment) =>
        new(areaId, slug, displayName, entities, kind, dimensions,
            navigation: AreaNavigationDefinition.CreateDefault(areaId), environment: environment,
            runtimePolicies: AreaRuntimePolicyProfile.CreateDefault(areaId), world: world);

    private static PortalLink Portal(string key, string displayName, string sourceEntityKey,
        string destinationAreaKey, string destinationEntityKey) =>
        new(Ids.Portal(key), displayName, Ids.Entity(sourceEntityKey), Ids.Area(destinationAreaKey),
            Ids.Entity(destinationEntityKey), false, true, provenance: "authored:forgotten-forest:connected-world@1");

    private static void AddPortalEntity(ICollection<EntityInstance> entities, AreaWorldProfile world,
        string key, float x, float z, string displayName) =>
        entities.Add(Entity(key, "exit", x, z, world, .14f, ["portal", "transition"],
            Properties(("runtime.displayName", displayName), ("visual.color", "#58d6c7"),
                ("visual.emission", "true")), new(1.5f, .18f, 1.5f)));

    private static void AddCreature(ICollection<EntityInstance> entities, AreaWorldProfile world, string key,
        string blueprintKey, float x, float z, string displayName, string species, bool boss) =>
        entities.Add(Entity(key, blueprintKey, x, z, world, .1f,
            boss ? ["boss", "hostile", species] : ["hostile", species],
            Properties(("runtime.displayName", displayName), ("runtime.species", species)),
            boss ? new(1.25f, 1.25f, 1.25f) : Vector3Value.One));

    private static void AddPrimitive(ICollection<EntityInstance> entities, AreaWorldProfile world, string key,
        float x, float z, float y, Vector3Value scale, string color, IReadOnlyList<string> tags,
        string shape, string displayName, bool emission = false) =>
        entities.Add(Entity(key, "prop", x, z, world, y, tags,
            Properties(("runtime.displayName", displayName), ("visual.color", color),
                ("visual.shape", shape), ("visual.emission", emission ? "true" : "false")), scale));

    private static void AddCollisionProxy(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string key,
        float x,
        float z,
        float y,
        Vector3Value scale,
        float yawDegrees,
        params string[] tags) =>
        entities.Add(Entity(key, "prop", x, z, world, y,
            ["solid", "collision", .. tags],
            Properties(("runtime.displayName", key), ("visual.hidden", "true")), scale, yawDegrees));

    private static void AddTownHouse(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string key,
        float x,
        float z,
        float yawDegrees,
        int widthUnits,
        int depthUnits,
        bool stone,
        bool compactCorners = false)
    {
        const float unit = 4;
        var wall = stone ? "wall" : "wall-wood";
        var door = stone ? "wall-door" : "wall-wood-door";
        var window = stone ? "wall-window-shutters" : "wall-wood-window-shutters";
        var scale = new Vector3Value(unit, unit, unit);
        var halfWidth = widthUnits * unit * .5f;
        var halfDepth = depthUnits * unit * .5f;

        for (var index = 0; index < widthUnits; index++)
        {
            var localX = (index - ((widthUnits - 1) * .5f)) * unit;
            var front = TransformLocal(x, z, localX, halfDepth, yawDegrees);
            var back = TransformLocal(x, z, localX, -halfDepth, yawDegrees);
            AddTownProp(entities, world, $"{key}-front-{index}", index == widthUnits / 2 ? door : window,
                front.X, front.Z, scale, yawDegrees + 90, .08f,
                "architecture", "building", "nonblocking", key);
            AddTownProp(entities, world, $"{key}-back-{index}", index % 2 == 0 ? window : wall,
                back.X, back.Z, scale, yawDegrees + 90, .08f,
                "architecture", "building", "nonblocking", key);
        }

        for (var index = 0; index < depthUnits; index++)
        {
            var localZ = (index - ((depthUnits - 1) * .5f)) * unit;
            var left = TransformLocal(x, z, -halfWidth, localZ, yawDegrees);
            var right = TransformLocal(x, z, halfWidth, localZ, yawDegrees);
            AddTownProp(entities, world, $"{key}-left-{index}", index % 2 == 0 ? window : wall,
                left.X, left.Z, scale, yawDegrees, .08f,
                "architecture", "building", "nonblocking", key);
            AddTownProp(entities, world, $"{key}-right-{index}", index % 2 == 0 ? wall : window,
                right.X, right.Z, scale, yawDegrees, .08f,
                "architecture", "building", "nonblocking", key);
        }

        for (var width = 0; width < widthUnits; width++)
            for (var depth = 0; depth < depthUnits; depth++)
            {
                var localX = (width - ((widthUnits - 1) * .5f)) * unit;
                var localZ = (depth - ((depthUnits - 1) * .5f)) * unit;
                var roof = TransformLocal(x, z, localX, localZ, yawDegrees);
                var roofAsset = depth == 0 ? "roof-high-gable" : "roof-high";
                var roofScale = compactCorners ? new Vector3Value(5.2f, unit, 5.2f) : scale;
                AddTownProp(entities, world, $"{key}-roof-{width}-{depth}", roofAsset,
                    roof.X, roof.Z, roofScale, yawDegrees + 90, 4.05f,
                    "architecture", "building", "roof", "nonblocking", key);
            }

        // Flat wall panels do not share a welded vertex at ninety-degree joins. Use the
        // kit's corner piece (or its stone pillar) at all four joins so every house reads
        // as one coherent volume instead of four intersecting façades.
        // The fantasy-town pack provides one timber-framed corner module shared by both
        // plaster and timber façades; using it keeps the stable asset reference inside the
        // same governed pack and visually ties the stone civic buildings to the village.
        const string cornerAsset = "wall-wood-corner";
        foreach (var (suffix, localX, localZ, cornerYaw) in new[]
                 {
                     ("front-left", -halfWidth, halfDepth, yawDegrees),
                     ("front-right", halfWidth, halfDepth, yawDegrees + 90),
                     ("back-right", halfWidth, -halfDepth, yawDegrees + 180),
                     ("back-left", -halfWidth, -halfDepth, yawDegrees + 270),
                 })
        {
            var corner = TransformLocal(x, z, localX, localZ, yawDegrees);
            if (compactCorners)
            {
                AddPrimitive(entities, world, $"{key}-corner-{suffix}", corner.X, corner.Z, 2.1f,
                    new(.48f, 4.2f, .48f), "#5a2d1c",
                    ["decor", "architecture", "building", "corner", "nonblocking", key],
                    "box", "Poteau d'angle de la maison du lac");
            }
            else
            {
                AddTownProp(entities, world, $"{key}-corner-{suffix}", cornerAsset, corner.X, corner.Z,
                    scale, cornerYaw, .08f, "architecture", "building", "corner", "nonblocking", key);
            }
        }
        AddCollisionProxy(entities, world, $"{key}-collision", x, z, 2,
            new(widthUnits * unit, 4, depthUnits * unit), yawDegrees, "building", key);
    }

    private static void AddForestBorderDressing(ICollection<EntityInstance> entities, AreaWorldProfile world)
    {
        foreach (var (key, asset, x, z, yaw, scale) in new[]
                 {
                     ("northwest-a", "cliff-large-rock", -240f, -239f, 18f, 13f),
                     ("north-a", "dark-detailed-tree", -85f, -242f, -12f, 10f),
                     ("north-b", "cliff-corner-rock", 92f, -241f, 28f, 12f),
                     ("northeast-a", "dark-detailed-tree", 240f, -226f, 9f, 11f),
                     ("west-a", "cliff-block-slope-rock", -240f, -78f, 92f, 13f),
                     ("east-a", "cliff-large-rock", 240f, 76f, -32f, 14f),
                     ("southwest-a", "dark-detailed-tree", -240f, 222f, -18f, 11f),
                     ("south-a", "cliff-corner-rock", -72f, 242f, 8f, 13f),
                     ("south-b", "dark-detailed-tree", 86f, 242f, 14f, 10f),
                     ("southeast-a", "cliff-large-rock", 240f, 231f, 34f, 14f),
                 })
        {
            AddNatureProp(entities, world, $"forest-border-{key}", asset, x, z,
                new(scale, scale, scale), yaw, .04f, "boundary", "forest-border");
        }
    }

    private static void AddConnectedAreaBorderDressing(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        AreaDimensions dimensions,
        ConnectedAreaTheme theme)
    {
        const float borderInset = 14;
        var halfWidth = dimensions.Width * .5f - borderInset;
        var halfDepth = dimensions.Depth * .5f - borderInset;
        var anchors = new[]
        {
            ("northwest", -halfWidth, -halfDepth, 18f),
            ("north", 0f, -halfDepth, -9f),
            ("northeast", halfWidth, -halfDepth, 27f),
            ("west", -halfWidth, 0f, 92f),
            ("east", halfWidth, 0f, -88f),
            ("southwest", -halfWidth, halfDepth, -24f),
            ("south", 0f, halfDepth, 8f),
            ("southeast", halfWidth, halfDepth, 31f),
        };
        foreach (var (key, x, z, yaw) in anchors)
        {
            var (asset, scale) = theme switch
            {
                ConnectedAreaTheme.Village => (key is "north" or "south" ? "oak-tree" : "detailed-bush", 6.5f),
                ConnectedAreaTheme.Fortress => (key is "west" or "east" ? "cliff-block-slope-rock" : "cliff-large-rock", 10f),
                ConnectedAreaTheme.Desert => (key is "north" or "south" ? "tall-rock-a" : "cliff-large-rock", 9f),
                ConnectedAreaTheme.SnowMountain => (key is "north" or "south" ? "pine-tall" : "cliff-corner-rock", 10.5f),
                _ => ("large-rock", 7f),
            };
            AddNatureProp(entities, world, $"{theme.ToString().ToLowerInvariant()}-border-{key}", asset, x, z,
                new(scale, scale, scale), yaw, .04f, "boundary", "border-dressing");
        }
    }

    private static (float X, float Z) TransformLocal(float x, float z, float localX, float localZ, float yawDegrees)
    {
        var radians = yawDegrees * MathF.PI / 180f;
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        return (x + (localX * cosine) - (localZ * sine), z + (localX * sine) + (localZ * cosine));
    }

    private static EntityPropertyOverrides Properties(params (string Key, string Value)[] values) =>
        new(EntityPropertyOverrides.CurrentSchemaVersion, values.Select(value =>
            new EntityPropertyOverride(value.Key, EntityPropertyValueKind.String, value.Value)).ToArray());

    private static AreaWorldProfile CreateConnectedWorld(AreaId areaId, AreaDimensions dimensions,
        ConnectedAreaTheme theme)
    {
        var defaults = AreaWorldProfile.CreateDefault(areaId,
            theme == ConnectedAreaTheme.Inn ? AreaKind.Interior : AreaKind.Exterior, dimensions);
        if (theme == ConnectedAreaTheme.Inn) return defaults;

        var groundId = StableArea(areaId, "material:ground");
        var detailId = StableArea(areaId, "material:detail");
        var rockId = StableArea(areaId, "material:rock");
        var colors = theme switch
        {
            ConnectedAreaTheme.Village => ("#6f633f", "#66834a", "#77736c"),
            ConnectedAreaTheme.Fortress => ("#4b4038", "#625649", "#5c5a58"),
            ConnectedAreaTheme.Desert => ("#c99452", "#e0b56b", "#8a6747"),
            ConnectedAreaTheme.SnowMountain => ("#d9e7ef", "#b8ceda", "#65727d"),
            _ => ("#65513b", "#607d45", "#6d716c"),
        };
        const int samples = 65;
        var heights = new float[samples * samples];
        for (var z = 0; z < samples; z++)
            for (var x = 0; x < samples; x++)
            {
                var nx = (x / (samples - 1f)) * 2 - 1;
                var nz = (z / (samples - 1f)) * 2 - 1;
                heights[(z * samples) + x] = theme switch
                {
                    ConnectedAreaTheme.Village => Math.Clamp(
                        MathF.Sin(nx * 3.4f) * .035f + MathF.Cos(nz * 3.1f) * .03f, -.1f, .1f),
                    ConnectedAreaTheme.Fortress => Math.Clamp(
                        MathF.Sin(nx * 5.1f + nz) * .08f + MathF.Abs(nx) * .12f + MathF.Abs(nz) * .08f, -.2f, .38f),
                    ConnectedAreaTheme.Desert => Math.Clamp(
                        MathF.Sin(nx * 7.2f + nz * 1.7f) * .13f + MathF.Cos(nz * 6.4f - nx) * .11f, -.32f, .32f),
                    ConnectedAreaTheme.SnowMountain => Math.Clamp(
                        (nz + 1) * .24f + MathF.Abs(MathF.Sin(nx * 4.2f + nz * 1.8f)) * .22f +
                        MathF.Cos(nz * 7.4f) * .06f, -.1f, .82f),
                    _ => 0,
                };
            }
        var terrain = new TerrainDefinition(StableArea(areaId, "terrain"), TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap, dimensions.Width, dimensions.Depth, samples, samples, 16,
            20260813 + (int)theme, theme == ConnectedAreaTheme.SnowMountain ? 28 : 11, heights,
            [
                new TerrainMaterialLayer(groundId, "Sol principal", colors.Item1, string.Empty, 5),
                new TerrainMaterialLayer(detailId, "Couverture", colors.Item2, string.Empty, 6),
                new TerrainMaterialLayer(rockId, "Roche", colors.Item3, string.Empty, 4),
            ], [], string.Empty, string.Empty, string.Empty, "authored:connected-world@1");
        var scatter = theme switch
        {
            ConnectedAreaTheme.Village => new[]
            {
                new BiomeLayerDefinition(StableArea(areaId, "biome:grass"), "Prairie de Clairval", detailId,
                    -30, 40, 0, 24, 0, 1, .42f, $"asset:{PolyHavenAssetId("grass_medium_01")}",
                    ["village", "grass", "ground-cover"], ["road", "building"], 50, "curated-pack:forgotten-forest-polyhaven-environment@1.1.0"),
                new BiomeLayerDefinition(StableArea(areaId, "biome:trees"), "Bosquets du village", groundId,
                    -30, 40, 0, 30, 0, 1, .075f, $"asset:{AssetId("tree")}",
                    ["village", "tree", "scale:3"], ["road", "building"], 30, "curated-pack:forgotten-forest-core@1.0.0;source:kenney:mini-forest"),
            },
            ConnectedAreaTheme.Desert => new[]
            {
                new BiomeLayerDefinition(StableArea(areaId, "biome:desert-rock"), "Roches des dunes", rockId,
                    -30, 40, 4, 55, 0, 1, .032f, $"asset:{NatureAssetId("large-rock")}",
                    ["desert", "rock", "scale:5.5"], ["road"], 40,
                    "curated-pack:forgotten-forest-nature-dressing@1.0.0;source:kenney:nature-kit"),
            },
            ConnectedAreaTheme.SnowMountain => new[]
            {
                new BiomeLayerDefinition(StableArea(areaId, "biome:snow-pines"), "Pins enneigés", detailId,
                    -30, 50, 0, 48, 0, 1, .11f, $"asset:{NatureAssetId("pine-tall")}",
                    ["snow", "tree", "pine", "scale:6.5"], ["road", "building"], 45,
                    "curated-pack:forgotten-forest-nature-dressing@1.0.0;source:kenney:nature-kit"),
                new BiomeLayerDefinition(StableArea(areaId, "biome:snow-rock"), "Éboulis gelés", rockId,
                    -30, 50, 15, 78, 0, 1, .04f, $"asset:{NatureAssetId("tall-rock-a")}",
                    ["snow", "rock", "scale:4.5"], ["road"], 55,
                    "curated-pack:forgotten-forest-nature-dressing@1.0.0;source:kenney:nature-kit"),
            },
            _ => new[]
            {
                new BiomeLayerDefinition(StableArea(areaId, "biome:fortress-rock"), "Sol de la forteresse", rockId,
                    -30, 40, 0, 75, 0, 1, .04f, $"asset:{AssetId("rocks-low")}",
                    ["fortress", "rock"], ["road", "building"], 35, "curated-pack:forgotten-forest-core@1.0.0;source:kenney:mini-forest"),
            },
        };
        Vector3Value Point(float x, float z) => new(x, terrain.HeightAt(x, z) + .08f, z);
        var features = theme switch
        {
            ConnectedAreaTheme.Village => new[]
            {
                Feature(areaId, "road-main", "Grand-rue de Clairval", TerrainFeatureKind.Road,
                    [Point(0, -82), Point(0, -25), Point(0, 10), Point(0, 75)], 5.2f, .08f, ["village"]),
                Feature(areaId, "road-market", "Rue du marché", TerrainFeatureKind.Road,
                    [Point(-66, 4), Point(0, 8), Point(68, 6)], 4.2f, .07f, ["village"]),
            },
            ConnectedAreaTheme.Fortress => new[]
            {
                Feature(areaId, "fortress-road", "Rampe de la meute", TerrainFeatureKind.Road,
                    [Point(0, -92), Point(0, -45), Point(0, 12), Point(0, 62)], 5f, .1f, ["fortress"]),
            },
            ConnectedAreaTheme.Desert => new[]
            {
                Feature(areaId, "desert-track", "Piste des caravanes", TerrainFeatureKind.Road,
                    [Point(-62, -62), Point(-24, -20), Point(8, 4), Point(52, 48)], 3.6f, .05f, ["desert"]),
            },
            ConnectedAreaTheme.SnowMountain => new[]
            {
                Feature(areaId, "snow-trail", "Sentier du col", TerrainFeatureKind.Road,
                    [Point(0, -101), Point(-12, -46), Point(8, 4), Point(-4, 54), Point(0, 97)], 3.2f, .08f, ["snow"]),
            },
            _ => [],
        };
        return new AreaWorldProfile(StableArea(areaId, "world"), AreaWorldProfile.CurrentSchemaVersion,
            terrain, scatter, features, defaults.VolumetricNavigation, defaults.FlightRoutes, defaults.Loading,
            "authored:forgotten-forest:connected-world@1");
    }

    private static TerrainFeatureDefinition Feature(AreaId areaId, string key, string name,
        TerrainFeatureKind kind, IReadOnlyList<Vector3Value> points, float width, float height,
        IReadOnlyList<string> biomeTags) =>
        new(StableArea(areaId, $"feature:{key}"), TerrainFeatureDefinition.CurrentSchemaVersion, name, kind,
            points, width, height, 0, TerrainFeatureCrossingPolicy.Allow, biomeTags, string.Empty,
            "authored:forgotten-forest:connected-world@1");

    private static AreaEnvironmentProfile CreateConnectedEnvironment(AreaId areaId, AreaKind kind,
        ConnectedAreaTheme theme)
    {
        var defaults = AreaEnvironmentProfile.CreateDefault(areaId, kind);
        var classification = theme == ConnectedAreaTheme.Inn ? AreaEnvironmentClass.Interior :
            theme is ConnectedAreaTheme.Desert or ConnectedAreaTheme.SnowMountain ? AreaEnvironmentClass.Natural :
            AreaEnvironmentClass.Exterior;
        var (zenith, horizon, ground, temperature, ambient, directional, fogMode, fogDensity, reverb) = theme switch
        {
            ConnectedAreaTheme.Village => ("#5b9bd1", "#ffd8a0", "#718a54", 6100f, 1.02f, 2.28f, FogMode.Distance, .00035f, "village"),
            ConnectedAreaTheme.Inn => ("#243040", "#d39a6a", "#5a4334", 3900f, .86f, .92f, FogMode.Disabled, 0f, "room"),
            ConnectedAreaTheme.Fortress => ("#53677d", "#b99a78", "#554940", 5000f, .96f, 2.18f, FogMode.Distance, .0009f, "stone"),
            ConnectedAreaTheme.Desert => ("#4b9bd0", "#ffdc9e", "#d39a54", 6800f, 1.08f, 2.35f, FogMode.Distance, .00045f, "open"),
            ConnectedAreaTheme.SnowMountain => ("#87b6d5", "#eefaff", "#dce8ed", 7600f, 1.12f, 2.22f, FogMode.Distance, .001f, "mountain"),
            _ => ("#4f83ad", "#e8c896", "#4b6048", 5600f, .5f, 1.4f, FogMode.Distance, .001f, "neutral"),
        };
        var sky = new SkyProfile(SkyProfile.CurrentSchemaVersion,
            kind == AreaKind.Interior ? SkyProfileMode.Procedural : SkyProfileMode.Physical,
            string.Empty, zenith, horizon, ground, .04, .12, .04, 1.05, 2);
        var weather = theme == ConnectedAreaTheme.SnowMountain
            ? SnowWeather(areaId)
            : WeatherProfile.CreateDefault(classification);
        return new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion, classification,
            [theme.ToString().ToLowerInvariant(), kind == AreaKind.Interior ? "indoor" : "outdoor"],
            new AreaLightingProfile(kind == AreaKind.Interior ? DayNightMode.AlwaysNight : DayNightMode.AlwaysDay,
                24, ambient, directional, temperature, true, true),
            new AreaFogProfile(fogMode, fogDensity, 35, kind == AreaKind.Interior ? 80 : 420, 12),
            new AreaAudioProfile(string.Empty, string.Empty, string.Empty, string.Empty, reverb, 1.2f),
            defaults.Rendering, sky, WindProfile.CreateDefault(classification), weather, WaterProfile.Empty,
            AreaReverbProfile.CreateDefault(reverb)).ForArea(areaId);
    }

    private static WeatherProfile SnowWeather(AreaId areaId)
    {
        var snowId = StableArea(areaId, "weather:snow");
        var clearId = StableArea(areaId, "weather:clear");
        return new WeatherProfile(WeatherProfile.CurrentSchemaVersion, snowId,
            [
                new WeatherStateProfile(clearId, "Éclaircies", WeatherKind.Clear, 0, .4f, 0, false,
                    string.Empty, string.Empty, string.Empty, 180, 420, 12),
                new WeatherStateProfile(snowId, "Neige", WeatherKind.Snow, .72f, .86f, .3f, false,
                    string.Empty, "weather.snow", "surface.snow", 420, 900, 18),
            ], true, 20260813, true, true);
    }

    private static Guid StableArea(AreaId areaId, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"connected-world:{areaId}:{key}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private enum ConnectedAreaTheme { Village, Inn, Fortress, Desert, SnowMountain }

    public static AssetCatalogItemId AssetId(string key) => AssetCatalogItemId.ForExternalId($"{AssetPrefix}{key}");

    public static AssetCatalogItemId NatureAssetId(string key) =>
        AssetCatalogItemId.ForExternalId($"{NatureAssetPrefix}{key}");

    public static AssetCatalogItemId PolyHavenAssetId(string key) =>
        AssetCatalogItemId.ForExternalId($"{PolyHavenAssetPrefix}{key}");

    public static AssetCatalogItemId TownAssetId(string key) =>
        AssetCatalogItemId.ForExternalId($"{TownAssetPrefix}{key}");

    public static AssetCatalogItemId CastleAssetId(string key) =>
        AssetCatalogItemId.ForExternalId($"{CastleAssetPrefix}{key}");

    public static AssetCatalogItemId DungeonAssetId(string key) =>
        AssetCatalogItemId.ForExternalId($"{DungeonAssetPrefix}{key}");

    public static AssetCatalogItemId AnimatedCharacterAssetId(string key = "knight") =>
        AssetCatalogItemId.ForExternalId($"{AnimatedCharacterExternalIdPrefix}{key}");

    public static AssetCatalogItemId AnimatedMonsterAssetId(string key = "dragon") =>
        AssetCatalogItemId.ForExternalId($"{AnimatedMonsterExternalIdPrefix}{key}");

    private static BlueprintDefinition Blueprint(string slug, string name, BlueprintKind kind, CombatProfile? combat = null) =>
        new(Ids.Blueprint(slug), slug, name, kind, combat);

    private static EntityInstance Entity(
        string entityKey,
        string blueprintKey,
        float x,
        float z,
        AreaWorldProfile world,
        float yOffset,
        IReadOnlyList<string> tags,
        EntityPropertyOverrides overrides,
        Vector3Value? scale = null,
        float yawDegrees = 0) =>
        new(
            Ids.Entity(entityKey),
            Ids.Blueprint(blueprintKey),
            new TransformValue(
                new Vector3Value(x, world.Terrain.HeightAt(x, z) + yOffset, z),
                Yaw(yawDegrees),
                scale ?? Vector3Value.One),
            tags,
            overrides);

    private static void AddProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string visualKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees,
        float yOffset = 0.25f) =>
        entities.Add(Entity(entityKey, "prop", x, z, world, yOffset, ["decor", "solid"], Visual(visualKey), scale, yawDegrees));

    private static void AddPolyHavenProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string assetKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees,
        float yOffset,
        IReadOnlyList<string> tags) =>
        entities.Add(Entity(entityKey, "prop", x, z, world, yOffset,
            ["decor", "solid", .. tags], Visual(PolyHavenAssetId(assetKey)), scale, yawDegrees));

    private static void AddPackProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        AssetCatalogItemId assetId,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees,
        float yOffset,
        params string[] tags)
    {
        string[] effectiveTags = tags.Contains("nonblocking", StringComparer.OrdinalIgnoreCase)
            ? ["decor", .. tags]
            : ["decor", "solid", .. tags];
        entities.Add(Entity(entityKey, "prop", x, z, world, yOffset,
            effectiveTags, Visual(assetId), scale, yawDegrees));
    }

    private static void AddTownProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string assetKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees = 0,
        float yOffset = .08f,
        params string[] tags) =>
        AddPackProp(entities, world, entityKey, TownAssetId(assetKey), x, z, scale, yawDegrees, yOffset,
            ["town-kit", .. tags]);

    private static void AddCastleProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string assetKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees = 0,
        float yOffset = .08f,
        params string[] tags) =>
        AddPackProp(entities, world, entityKey, CastleAssetId(assetKey), x, z, scale, yawDegrees, yOffset,
            ["castle-kit", "nonblocking", .. tags]);

    private static void AddDungeonProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string assetKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees = 0,
        float yOffset = .08f,
        params string[] tags) =>
        AddPackProp(entities, world, entityKey, DungeonAssetId(assetKey), x, z, scale, yawDegrees, yOffset,
            ["dungeon-kit", .. tags]);

    private static void AddNatureProp(
        ICollection<EntityInstance> entities,
        AreaWorldProfile world,
        string entityKey,
        string assetKey,
        float x,
        float z,
        Vector3Value scale,
        float yawDegrees = 0,
        float yOffset = .08f,
        params string[] tags) =>
        AddPackProp(entities, world, entityKey, NatureAssetId(assetKey), x, z, scale, yawDegrees, yOffset,
            ["nature-kit", "nonblocking", .. tags]);

    private static EntityPropertyOverrides Visual(string assetKey, params (string Key, string Value)[] values) =>
        Visual(AssetId(assetKey), values);

    private static EntityPropertyOverrides AnimatedCharacterVisual(
        string characterKey,
        params (string Key, string Value)[] values)
    {
        var clips = CharacterAnimationClips(characterKey);
        return Visual(AnimatedCharacterAssetId(characterKey),
        [
            ("runtime.animationProfile", "kaykit-adventurer"),
            ("runtime.animationIdle", clips.Idle),
            ("runtime.animationRun", clips.Run),
            ("runtime.animationJump", clips.Jump),
            ("runtime.animationAttack", clips.Attack),
            ("runtime.animationHit", clips.Hit),
            ("runtime.animationDeath", clips.Death),
            .. values,
        ]);
    }

    private static EntityPropertyOverrides AnimatedDragonVisual(
        params (string Key, string Value)[] values) =>
        Visual(AnimatedMonsterAssetId(),
        [
            ("runtime.animationProfile", "quaternius-dragon"),
            ("runtime.animationIdle", "Dragon_Flying"),
            ("runtime.animationRun", "Dragon_Flying"),
            ("runtime.animationJump", "Dragon_Flying"),
            ("runtime.animationAttack", "Dragon_Attack"),
            ("runtime.animationHit", "Dragon_Hit"),
            ("runtime.animationDeath", "Dragon_Death"),
            .. values,
        ]);

    private static CharacterClips CharacterAnimationClips(string characterKey) => characterKey switch
    {
        "mage" => new("Idle", "Running_A", "Jump_Full_Short", "Spellcast_Shoot", "Hit_A", "Death_A"),
        "rogue" => new("Idle", "Running_A", "Jump_Full_Short", "1H_Melee_Attack_Stab", "Hit_A", "Death_A"),
        "rogue-hooded" => new("Idle", "Running_A", "Jump_Full_Short", "Dualwield_Melee_Attack_Slice", "Hit_B", "Death_B"),
        "barbarian" => new("2H_Melee_Idle", "Running_B", "Jump_Full_Short", "2H_Melee_Attack_Chop", "Hit_B", "Death_B"),
        "knight" => new("Idle", "Running_A", "Jump_Full_Short", "1H_Melee_Attack_Slice_Horizontal", "Hit_A", "Death_A"),
        _ => throw new ArgumentOutOfRangeException(nameof(characterKey), characterKey, "Unknown curated character key."),
    };

    private static EntityPropertyOverrides Visual(
        AssetCatalogItemId assetId,
        params (string Key, string Value)[] values)
    {
        var properties = new List<EntityPropertyOverride>
        {
            new("visual.asset", EntityPropertyValueKind.String, assetId.ToString()),
        };
        properties.AddRange(values.Select(value =>
            new EntityPropertyOverride(value.Key, EntityPropertyValueKind.String, value.Value)));
        return new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties);
    }

    private static QuaternionValue Yaw(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new QuaternionValue(0, MathF.Sin(radians / 2f), 0, MathF.Cos(radians / 2f));
    }

    private sealed record CharacterClips(
        string Idle,
        string Run,
        string Jump,
        string Attack,
        string Hit,
        string Death);

    private static AreaEnvironmentProfile CreateEnvironment(AreaWorldProfile world)
    {
        var areaId = Ids.Area("forgotten-forest");
        var defaults = AreaEnvironmentProfile.CreateDefault(areaId, AreaKind.Exterior);
        var audio = new AreaAudioProfile(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "forest",
            1.2f,
            relativeVolume: .9f,
            spatial: new AudioSpatialProfile(AreaSoundMode.Spatial3D, 2, 38, .18f, "inverse-square"));
        var sky = new SkyProfile(
            SkyProfile.CurrentSchemaVersion,
            SkyProfileMode.Physical,
            string.Empty,
            "#4f83ad",
            "#e8c896",
            "#4b6048",
            .04,
            .12,
            .04,
            1.05,
            2);
        var lakeCenter = new Vector3Value(184, world.Terrain.HeightAt(184, 184) + .28f, 184);
        var water = new WaterProfile(WaterProfile.CurrentSchemaVersion,
        [
            new WaterBodyProfile(
                Stable("water:forest-lake"),
                "Lac des Reflets",
                WaterBodyKind.Plane,
                lakeCenter,
                new Vector3Value(116, 4, 104),
                3.2f,
                1,
                .18f,
                .08f,
                7.5f,
                2,
                true,
                WaterQualityTier.Ultra,
                string.Empty,
                "ambient.forest.stream"),
        ]);
        return new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            AreaEnvironmentClass.Natural,
            ["forest", "temperate", "outdoor", "forgotten"],
            new AreaLightingProfile(DayNightMode.AlwaysDay, 24, 0.55f, 1.58f, 5600, true, true),
            new AreaFogProfile(FogMode.Distance, 0.0011f, 180, 620, 11),
            audio,
            new AreaRenderingProfile(
                defaults.Rendering.SchemaVersion,
                defaults.Rendering.MaterialWorkflow,
                defaults.Rendering.ToneMapping,
                defaults.Rendering.Quality,
                defaults.Rendering.ForwardPlus,
                defaults.Rendering.PerPixelLighting,
                defaults.Rendering.MaterialInstances,
                defaults.Rendering.HdrOutput,
                1.12,
                defaults.Rendering.WhitePoint,
                defaults.Rendering.LutResource,
                defaults.Rendering.LightBudgets),
            sky,
            defaults.Wind,
            defaults.Weather,
            water,
            AreaReverbProfile.CreateDefault("forest")).ForArea(areaId);
    }

    private static AreaRuntimePolicyProfile CreateRuntimePolicies(AreaId areaId)
    {
        var defaults = AreaRuntimePolicyProfile.CreateDefault(areaId);
        var resources = new[]
        {
            (Key: "canopy-wind", Cue: "ambient.forest.wind", Resource: ProceduralAmbientAudioService.CanopyWindResource,
                Volume: .34f),
            (Key: "birdsong", Cue: "ambient.forest.birds", Resource: ProceduralAmbientAudioService.BirdsongResource,
                Volume: .42f),
            (Key: "stream", Cue: "ambient.forest.stream", Resource: ProceduralAmbientAudioService.StreamResource,
                Volume: .38f),
        };
        var cues = resources.Select(item =>
        {
            var clip = ProceduralAmbientAudioService.Create(item.Resource);
            return new AudioCueDefinition(
                Ids.Guid($"experience:cue:{item.Key}"),
                item.Cue,
                item.Resource,
                "Master",
                item.Volume,
                false,
                "LicenseRef-OpenRPG-Forge-Generated",
                clip.ContentSha256,
                "1.0",
                "builtin:openrpg-forge:procedural-audio@1");
        }).ToArray();
        var scatter = new[]
        {
            new SpatialScatterEmitter(
                Ids.Guid("experience:scatter:canopy-wind"),
                "ambient.forest.wind",
                new Vector3Value(0, 4, 0),
                new Vector3Value(480, 8, 480),
                false,
                2026080501,
                6,
                10,
                .82f,
                true,
                ["forest"],
                ["interior"],
                "authored:forgotten-forest:procedural-audio@1"),
            new SpatialScatterEmitter(
                Ids.Guid("experience:scatter:birdsong"),
                "ambient.forest.birds",
                new Vector3Value(0, 6, 0),
                new Vector3Value(470, 12, 470),
                false,
                2026080502,
                8,
                14,
                .9f,
                true,
                ["canopy", "understory"],
                ["interior"],
                "authored:forgotten-forest:procedural-audio@1"),
            new SpatialScatterEmitter(
                Ids.Guid("experience:scatter:stream"),
                "ambient.forest.stream",
                new Vector3Value(2, .8f, 1),
                new Vector3Value(440, 3, 24),
                false,
                2026080503,
                5,
                9,
                .88f,
                true,
                ["forest"],
                ["interior"],
                "authored:forgotten-forest:procedural-audio@1"),
        };
        var loading = defaults.Experience.Loading with
        {
            Title = "Forgotten Forest",
            LocalizedTips = new Dictionary<string, IReadOnlyList<string>>
            {
                ["fr"] =
                [
                    "La rivière guide vers le sanctuaire.",
                    "Les fanions marquent le sentier sûr.",
                ],
                ["en"] =
                [
                    "The river leads toward the sanctuary.",
                    "The flags mark the safe trail.",
                ],
            },
            AccessibleFallback = "Préparation de la forêt, de ses sons et de ses repères visuels.",
            Provenance = "authored:forgotten-forest:loading@1",
        };
        var experience = new AreaExperienceProfile(
            areaId.Value,
            AreaExperienceProfile.CurrentSchemaVersion,
            cues,
            scatter,
            [],
            [],
            loading,
            "authored:forgotten-forest:experience@1");
        return new AreaRuntimePolicyProfile(
            areaId.Value,
            AreaRuntimePolicyProfile.CurrentSchemaVersion,
            defaults.Heartbeats,
            defaults.Rest,
            "authored:forgotten-forest:runtime-policies@1",
            defaults.Cinematics,
            defaults.Rules,
            experience);
    }

    private static AreaWorldProfile CreateWorld(AreaId areaId, AreaDimensions dimensions)
    {
        var defaults = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, dimensions);
        var soilId = Stable("material:soil");
        var grassId = Stable("material:grass");
        var rockId = Stable("material:rock");
        var meadowId = Stable("material:meadow");
        const int samples = 129;
        var heights = new float[samples * samples];
        for (var z = 0; z < samples; z++)
        {
            for (var x = 0; x < samples; x++)
            {
                var nx = (x / (samples - 1f)) * 2f - 1f;
                var nz = (z / (samples - 1f)) * 2f - 1f;
                var macro = MathF.Sin(nx * 2.7f + 0.4f) * 0.14f + MathF.Cos(nz * 2.3f - 0.7f) * 0.115f;
                var ridges = MathF.Sin(nx * 6.1f + nz * 1.4f) * 0.085f +
                              MathF.Cos(nz * 5.3f - nx * 1.1f) * 0.08f;
                var detail = MathF.Sin((nx + nz) * 13.2f) * 0.026f +
                             MathF.Cos((nx - nz) * 10.4f) * 0.022f;
                var edgeRise = MathF.Pow(Math.Clamp(MathF.Sqrt(nx * nx + nz * nz), 0, 1), 2) * 0.24f;
                var centralValley = MathF.Exp(-((nx * nx) + (nz * nz)) * 5.5f) * 0.1f;
                var rawHeight = macro + ridges + detail + edgeRise - centralValley;
                var questCorridor = MathF.Exp(-MathF.Pow(nx - nz, 2) * 260f) *
                                    MathF.Exp(-((nx * nx) + (nz * nz)) * 18f);
                var corridorHeight = ((nx + nz) * 0.032f) - 0.035f;
                var corridorBlend = Math.Clamp(questCorridor * .92f, 0, 1);
                heights[z * samples + x] = Math.Clamp(
                    rawHeight + ((corridorHeight - rawHeight) * corridorBlend),
                    -0.52f,
                    0.68f);
            }
        }

        var terrain = new TerrainDefinition(
            Stable("terrain"),
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap,
            dimensions.Width,
            dimensions.Depth,
            samples,
            samples,
            16,
            20260805,
            16,
            heights,
            [
                new TerrainMaterialLayer(soilId, "Terre forestière", "#594431", string.Empty, 5),
                new TerrainMaterialLayer(grassId, "Mousse et fougères", "#456b38", string.Empty, 6),
                new TerrainMaterialLayer(rockId, "Roche humide", "#68716b", string.Empty, 4),
                new TerrainMaterialLayer(meadowId, "Clairière dorée", "#78945b", string.Empty, 7),
            ],
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            "authored:forgotten-forest:sectorized@2");
        const string polyHavenProvenance =
            "curated-pack:forgotten-forest-polyhaven-environment@1.1.0";
        const string kenneyForestProvenance =
            "curated-pack:forgotten-forest-core@1.0.0;source:kenney:mini-forest";
        var biomes = new[]
        {
            new BiomeLayerDefinition(Stable("biome:meadow"), "Herbe de clairière", meadowId, -20, 30, 0, 18, 0, 1,
                0.12f, $"asset:{PolyHavenAssetId("grass_medium_01")}",
                ["meadow", "grass", "ground-cover", "meter-scale"], ["road", "river", "building"], 70,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:canopy"), "Sapins matures", grassId, -20, 30, 0, 36, 0, 1,
                0.22f, $"asset:{AssetId("tree-high")}",
                ["forest", "tree", "canopy", "fir", "meter-scale", "scale:5"], ["road", "river", "building"], 50,
                kenneyForestProvenance),
            new BiomeLayerDefinition(Stable("biome:understory"), "Jeunes sapins", grassId, -20, 30, 0, 44, 0, 1,
                0.28f, $"asset:{AssetId("tree")}",
                ["forest", "tree", "understory", "fir", "meter-scale", "scale:3.5"], ["road", "river", "building"], 40,
                kenneyForestProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-oak"), "Jeunes pins", grassId, -20, 30, 0, 34, 0, 1,
                0.18f, $"asset:{AssetId("tree")}",
                ["forest", "tree", "understory", "pine", "meter-scale", "scale:2.8"], ["road", "river", "building"], 49,
                kenneyForestProvenance),
            new BiomeLayerDefinition(Stable("biome:plants"), "Fougères", grassId, -20, 30, 0, 52, 0, 1,
                0.45f, $"asset:{PolyHavenAssetId("fern_02")}",
                ["forest", "ground-cover", "plant", "meter-scale"], ["road", "river", "building"], 30,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-fall"), "Fleurs du Cap", meadowId, -20, 30, 0, 26, 0, 1,
                0.04f, $"asset:{PolyHavenAssetId("didelta_spinosa")}",
                ["meadow", "flower", "ground-cover", "meter-scale"], ["road", "river", "building"], 47,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-bush"), "Tapis de mousse", grassId, -20, 30, 0, 42, 0, 1,
                0.24f, $"asset:{PolyHavenAssetId("moss_01")}",
                ["forest", "moss", "ground-cover", "meter-scale"], ["road", "river", "building"], 38,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-deadwood"), "Branches sèches", soilId, -20, 30, 0, 38, 0, 1,
                0.018f, $"asset:{PolyHavenAssetId("dry_branches_medium_01")}",
                ["forest", "deadwood", "ground-cover", "meter-scale"], ["road", "river", "building"], 31,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-dark"), "Troncs morts", soilId, -20, 30, 0, 42, 0, 1,
                0.008f, $"asset:{PolyHavenAssetId("dead_tree_trunk")}",
                ["forest", "deadwood", "ground-cover", "meter-scale"], ["road", "river", "building"], 48,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:rocks"), "Rochers moussus I", rockId, -20, 30, 8, 68, 0, 1,
                0.04f, $"asset:{PolyHavenAssetId("rock_moss_set_01")}",
                ["rock", "ground-cover", "meter-scale"], ["road", "river", "building"], 60,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-large-rock"), "Rochers moussus II", rockId, -20, 30, 8, 62, 0, 1,
                0.035f, $"asset:{PolyHavenAssetId("rock_moss_set_02")}",
                ["rock", "ground-cover", "meter-scale"], ["road", "river", "building"], 59,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-mushrooms"), "Souches anciennes I", soilId, -20, 30, 0, 38, 0, 1,
                0.012f, $"asset:{PolyHavenAssetId("tree_stump_01")}",
                ["forest", "deadwood", "ground-cover", "meter-scale"], ["road", "river", "building"], 34,
                polyHavenProvenance),
            new BiomeLayerDefinition(Stable("biome:nature-pine-round"), "Souches anciennes II", soilId, -20, 30, 0, 42, 0, 1,
                0.01f, $"asset:{PolyHavenAssetId("tree_stump_02")}",
                ["forest", "deadwood", "ground-cover", "meter-scale"], ["road", "river", "building"], 45,
                polyHavenProvenance),
        };
        Vector3Value Point(float x, float z, float lift = 0.08f) => new(x, terrain.HeightAt(x, z) + lift, z);
        var lakeHutClearingStart = TransformLocal(113, 161, -8, 0, 12);
        var lakeHutClearingEnd = TransformLocal(113, 161, 8, 0, 12);
        var features = new[]
        {
            new TerrainFeatureDefinition(Stable("feature:trail"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Sentier des fanions", TerrainFeatureKind.Road,
                [Point(-232, -218), Point(-186, -174), Point(-122, -101), Point(-52, -48), Point(-35, -34),
                    Point(-16, -14), Point(2, 1), Point(18, 20), Point(42, 44), Point(54, 52), Point(112, 96),
                    Point(169, 151), Point(228, 210)],
                3.4f, 0.08f, 0, TerrainFeatureCrossingPolicy.RequiresBridge, ["forest"], string.Empty,
                "authored:forgotten-forest@1"),
            new TerrainFeatureDefinition(Stable("feature:river"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Ruisseau d’argent", TerrainFeatureKind.River,
                [Point(-236, 181, 0.02f), Point(-188, 142, 0.02f), Point(-128, 88, 0.02f),
                    Point(-50, 18, 0.02f), Point(-27, 10, 0.02f), Point(2, 1, 0.02f), Point(26, -8, 0.02f),
                    Point(53, -19, 0.02f), Point(118, -70, 0.02f), Point(181, -126, 0.02f),
                    Point(236, -178, 0.02f)],
                3.2f, 0.12f, 0, TerrainFeatureCrossingPolicy.RequiresBridge, ["forest"], string.Empty,
                "authored:forgotten-forest@1"),
            new TerrainFeatureDefinition(Stable("feature:watcher-road"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Chemin des quatre veilleurs", TerrainFeatureKind.Road,
                [Point(-188, 164), Point(-126, 126), Point(-68, 78), Point(0, 32), Point(72, -34),
                    Point(126, -92), Point(171, -166)],
                2.8f, 0.06f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest:sectorized@2"),
            new TerrainFeatureDefinition(Stable("feature:lake-warden-hut-clearing"),
                TerrainFeatureDefinition.CurrentSchemaVersion,
                "Cour dégagée de la maison du garde du lac", TerrainFeatureKind.Building,
                [Point(lakeHutClearingStart.X, lakeHutClearingStart.Z, -2f),
                    Point(lakeHutClearingEnd.X, lakeHutClearingEnd.Z, -2f)],
                26f, 0.01f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest:lake-warden-hut@1"),
            new TerrainFeatureDefinition(Stable("feature:watcher-bridge"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Pont des quatre veilleurs", TerrainFeatureKind.Bridge,
                [Point(71, -40, 0.36f), Point(71, -26, 0.36f)],
                4.4f, 0.24f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest:sectorized@2"),
            new TerrainFeatureDefinition(Stable("feature:northwest-pier-bridge"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Pont de l'embarcadère nord-ouest", TerrainFeatureKind.Bridge,
                [Point(-134, 82, 0.34f), Point(-122, 94, 0.34f)],
                4.2f, 0.22f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest:polyhaven-environment@1"),
            new TerrainFeatureDefinition(Stable("feature:southeast-pier-bridge"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Pont de l'embarcadère sud-est", TerrainFeatureKind.Bridge,
                [Point(112, -76, 0.34f), Point(124, -64, 0.34f)],
                4.2f, 0.22f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest:polyhaven-environment@1"),
        };
        var loading = new AreaLoadingProfile(
            defaults.Loading.Id,
            AreaLoadingProfile.CurrentSchemaVersion,
            true,
            true,
            true,
            1024,
            6,
            2,
            3500,
            defaults.Loading.RequiredResources,
            "authored:forgotten-forest:sectorized@2");
        return new AreaWorldProfile(
            Stable("world"),
            AreaWorldProfile.CurrentSchemaVersion,
            terrain,
            biomes,
            features,
            defaults.VolumetricNavigation,
            defaults.FlightRoutes,
            loading,
            "authored:forgotten-forest:sectorized@2");
    }

    private static AreaNavigationDefinition CreateNavigation(AreaId areaId, AreaWorldProfile world)
    {
        var defaults = AreaNavigationDefinition.CreateDefault(areaId);
        Vector3Value Point(float x, float z) => new(x, world.Terrain.HeightAt(x, z) + .42f, z);
        var watcherCrossing = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Stable("navigation:watcher-bridge-crossing")),
            "Traversée du pont des quatre veilleurs",
            NavigationLinkKind.Jump,
            Point(71, -40),
            Point(71, -26),
            true,
            .9f,
            "authored:forgotten-forest:sectorized@2");
        var northwestPierCrossing = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Stable("navigation:northwest-pier-crossing")),
            "Traversée du pont nord-ouest",
            NavigationLinkKind.Jump,
            Point(-134, 82),
            Point(-122, 94),
            true,
            .9f,
            "authored:forgotten-forest:polyhaven-environment@1");
        var southeastPierCrossing = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Stable("navigation:southeast-pier-crossing")),
            "Traversée du pont sud-est",
            NavigationLinkKind.Jump,
            Point(112, -76),
            Point(124, -64),
            true,
            .9f,
            "authored:forgotten-forest:polyhaven-environment@1");
        return new AreaNavigationDefinition(
            defaults.Id,
            defaults.CellSize,
            defaults.AgentProfiles,
            [watcherCrossing, northwestPierCrossing, southeastPierCrossing],
            "authored:forgotten-forest:sectorized@2");
    }

    private static Guid Stable(string scope) => Ids.Guid($"world:{scope}");

    private sealed class StableIds(string namespaceId)
    {
        private readonly Guid _namespaceId = System.Guid.Parse(namespaceId);
        internal Guid Guid(string scope)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{_namespaceId:D}:{scope}"));
            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return new Guid(bytes[..16]);
        }

        internal ProjectId Project(string key) => ProjectId.From(Guid($"project:{key}"));
        internal ModuleId Module(string key) => ModuleId.From(Guid($"module:{key}"));
        internal AreaId Area(string key) => AreaId.From(Guid($"area:{key}"));
        internal PortalId Portal(string key) => PortalId.From(Guid($"portal:{key}"));
        internal BlueprintId Blueprint(string key) => BlueprintId.From(Guid($"blueprint:{key}"));
        internal EntityId Entity(string key) => EntityId.From(Guid($"entity:{key}"));
        internal AbilityId Ability(string key) => AbilityId.From(Guid($"ability:{key}"));
        internal DialogueId Dialogue(string key) => DialogueId.From(Guid($"dialogue:{key}"));
        internal DialogueNodeId DialogueNode(string key) => DialogueNodeId.From(Guid($"dialogue-node:{key}"));
        internal DialogueChoiceId DialogueChoice(string key) => DialogueChoiceId.From(Guid($"dialogue-choice:{key}"));
        internal QuestId Quest(string key) => QuestId.From(Guid($"quest:{key}"));
        internal QuestStateId QuestState(string key) => QuestStateId.From(Guid($"quest-state:{key}"));
        internal QuestTransitionId QuestTransition(string key) => QuestTransitionId.From(Guid($"quest-transition:{key}"));
    }
}
