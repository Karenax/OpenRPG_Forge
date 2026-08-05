using System.Security.Cryptography;
using System.Text;
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
using Forge.Domain.Spatial;

namespace Forge.Application.Projects;

public sealed record ForgottenForestAuthoredModule(
    ProjectState State,
    IReadOnlyDictionary<string, string> Scripts);

public static class ForgottenForestModuleAuthoringService
{
    private const string AssetPrefix = "kenney:mini-forest:";
    private static readonly StableIds Ids = new("f5d1ca15-4a39-46ba-a8e2-74dde7188258");

    public static ForgottenForestAuthoredModule Create()
    {
        var areaId = Ids.Area("forgotten-forest");
        var dimensions = new AreaDimensions(128, 128);
        var world = CreateWorld(areaId, dimensions);
        var navigation = CreateNavigation(areaId, world);
        var environment = CreateEnvironment();
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
                Visual("character-archer"), new(1.8f, 1.8f, 1.8f)),
            Entity("guide", "guide", -37, -38, world, 0.08f, ["npc", "guide"],
                Visual("character-archer", ("runtime.role", "guide"), ("runtime.displayName", "Maëlys")), new(1.8f, 1.8f, 1.8f)),
            Entity("moonseed", "moonseed", -14, 13, world, 0.12f, ["quest", "item"],
                Visual("flag", ("runtime.item", "moonseed"), ("runtime.displayName", "Graine de lune")), new(1.7f, 1.7f, 1.7f)),
            Entity("gate", "gate", 14, 18, world, 0.35f, ["gate", "solid"],
                Visual("fence", ("runtime.displayName", "Passage des Ronces")), new(3.2f, 2.2f, 1.2f), 32),
            Entity("scout-a", "scout", -2, 5, world, 0.08f, ["hostile"], Visual("character-archer"), new(1.8f, 1.8f, 1.8f)),
            Entity("scout-b", "scout", 25, 25, world, 0.08f, ["hostile"], Visual("character-archer"), new(1.8f, 1.8f, 1.8f)),
            Entity("boss", "boss", 42, 42, world, 0.08f, ["boss", "hostile"],
                Visual("character-archer", ("runtime.role", "boss"),
                    ("phaseTwoScript", "scripts/root_guardian_phase_two.forge"),
                    ("deathScript", "scripts/root_guardian_death.forge")), new(2.15f, 2.15f, 2.15f)),
            Entity("exit", "exit", 51, 50, world, 0.12f, ["exit"], Visual("building-structure"), new(2.2f, 2.2f, 2.2f)),
        };

        AddProp(entities, world, "bridge", "bridge", 2, 1, new(2.4f, 1.2f, 2.4f), 45);
        AddProp(entities, world, "camp-tent", "tent", -39, -31, new(2.2f, 2.2f, 2.2f), -20);
        AddProp(entities, world, "camp-target", "target", -32, -28, new(1.5f, 1.5f, 1.5f), 15);
        AddProp(entities, world, "watch-platform", "platform", 30, 34, new(2.2f, 2.2f, 2.2f), 10);
        AddProp(entities, world, "watch-building", "building-structure", 37, 35, new(2.1f, 2.1f, 2.1f), -10);
        AddProp(entities, world, "watch-roof", "building-roof", 37, 35, new(2.1f, 2.1f, 2.1f), -10, 2.4f);
        AddProp(entities, world, "watch-ladder", "ladder", 32, 35, new(1.8f, 1.8f, 1.8f), -10);
        AddProp(entities, world, "landmark-flag", "flag", 30, 30, new(1.7f, 1.7f, 1.7f), 0);
        AddProp(entities, world, "rock-landmark-a", "rocks-high", -27, 19, new(2.1f, 2.1f, 2.1f), 23);
        AddProp(entities, world, "rock-landmark-b", "rocks-ramp", 17, -23, new(2.4f, 2.4f, 2.4f), -32);

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
            world: world);
        var module = new ModuleDefinition(
            Ids.Module("forgotten-forest"),
            "forgotten-forest",
            "Forgotten Forest",
            "Une tranche verticale jouable : explorer, dialoguer, récupérer la Graine de lune, ouvrir le passage et vaincre le Gardien.",
            area.Id);
        var project = new ProjectDefinition(
            Ids.Project("forgotten-forest"),
            "Forgotten Forest",
            module,
            blueprints,
            [area],
            [dialogue],
            [quest]);
        var scripts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scripts/root_guardian_phase_two.forge"] =
                "void main()\n{\n    say(\"Le Gardien fait jaillir deux rôdeurs des racines !\");\n    spawn(\"rootbound-scout\");\n    spawn(\"rootbound-scout\");\n    cast_ability(0, \"thorn-claw\");\n}\n",
            ["scripts/root_guardian_death.forge"] =
                "void main()\n{\n    say(\"Le cœur de la forêt bat de nouveau. Rejoignez le sanctuaire.\");\n    quest(\"wake-forgotten-forest\");\n}\n",
        };
        return new ForgottenForestAuthoredModule(new ProjectState(project, new ProjectRevision(0)), scripts);
    }

    public static AssetCatalogItemId AssetId(string key) => AssetCatalogItemId.ForExternalId($"{AssetPrefix}{key}");

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

    private static EntityPropertyOverrides Visual(string assetKey, params (string Key, string Value)[] values)
    {
        var properties = new List<EntityPropertyOverride>
        {
            new("visual.asset", EntityPropertyValueKind.String, AssetId(assetKey).ToString()),
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

    private static AreaEnvironmentProfile CreateEnvironment()
    {
        var defaults = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        return new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            AreaEnvironmentClass.Natural,
            ["forest", "temperate", "outdoor", "forgotten"],
            new AreaLightingProfile(DayNightMode.AlwaysDay, 24, 0.62f, 1.25f, 5600, true, true),
            new AreaFogProfile(FogMode.Distance, 0.012f, 50, 150, 5),
            defaults.Audio,
            defaults.Rendering,
            defaults.Sky,
            defaults.Wind,
            defaults.Weather,
            defaults.Water,
            defaults.Reverb);
    }

    private static AreaWorldProfile CreateWorld(AreaId areaId, AreaDimensions dimensions)
    {
        var defaults = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, dimensions);
        var soilId = Stable("material:soil");
        var grassId = Stable("material:grass");
        var rockId = Stable("material:rock");
        const int samples = 33;
        var heights = new float[samples * samples];
        for (var z = 0; z < samples; z++)
        {
            for (var x = 0; x < samples; x++)
            {
                var nx = (x / (samples - 1f)) * 2f - 1f;
                var nz = (z / (samples - 1f)) * 2f - 1f;
                var ridges = MathF.Sin(nx * 5.7f + 0.8f) * 0.12f + MathF.Cos(nz * 4.9f - 0.4f) * 0.10f;
                var detail = MathF.Sin((nx + nz) * 9.2f) * 0.035f + MathF.Cos((nx - nz) * 7.4f) * 0.03f;
                var edgeRise = MathF.Pow(Math.Clamp(MathF.Sqrt(nx * nx + nz * nz), 0, 1), 2) * 0.18f;
                var roadFlatten = MathF.Exp(-MathF.Pow(nx - nz, 2) * 18f) * 0.12f;
                heights[z * samples + x] = Math.Clamp(ridges + detail + edgeRise - roadFlatten, -0.45f, 0.55f);
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
            8,
            heights,
            [
                new TerrainMaterialLayer(soilId, "Terre forestière", "#594431", string.Empty, 5),
                new TerrainMaterialLayer(grassId, "Mousse et fougères", "#456b38", string.Empty, 6),
                new TerrainMaterialLayer(rockId, "Roche humide", "#68716b", string.Empty, 4),
            ],
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            "authored:forgotten-forest@1");
        var biomes = new[]
        {
            new BiomeLayerDefinition(Stable("biome:canopy"), "Canopée", grassId, -20, 30, 0, 36, 0, 1,
                0.68f, $"asset:{AssetId("tree-high")}", ["forest", "tree", "canopy"], ["road", "river", "building"], 50,
                "authored:forgotten-forest@1"),
            new BiomeLayerDefinition(Stable("biome:understory"), "Sous-bois", grassId, -20, 30, 0, 44, 0, 1,
                0.46f, $"asset:{AssetId("tree")}", ["forest", "tree", "understory"], ["road", "river", "building"], 40,
                "authored:forgotten-forest@1"),
            new BiomeLayerDefinition(Stable("biome:plants"), "Fougères", grassId, -20, 30, 0, 52, 0, 1,
                0.82f, $"asset:{AssetId("plant")}", ["forest", "ground-cover", "plant"], ["road", "river"], 30,
                "authored:forgotten-forest@1"),
            new BiomeLayerDefinition(Stable("biome:rocks"), "Rochers", rockId, -20, 30, 10, 68, 0, 1,
                0.18f, $"asset:{AssetId("rocks-low")}", ["rock", "ground-cover"], ["road", "river"], 20,
                "authored:forgotten-forest@1"),
        };
        Vector3Value Point(float x, float z, float lift = 0.08f) => new(x, terrain.HeightAt(x, z) + lift, z);
        var features = new[]
        {
            new TerrainFeatureDefinition(Stable("feature:trail"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Sentier des fanions", TerrainFeatureKind.Road,
                [Point(-52, -48), Point(-35, -34), Point(-16, -14), Point(2, 1), Point(18, 20), Point(42, 44), Point(54, 52)],
                3.4f, 0.08f, 0, TerrainFeatureCrossingPolicy.RequiresBridge, ["forest"], string.Empty,
                "authored:forgotten-forest@1"),
            new TerrainFeatureDefinition(Stable("feature:river"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Ruisseau d’argent", TerrainFeatureKind.River,
                [Point(-50, 18, 0.02f), Point(-27, 10, 0.02f), Point(2, 1, 0.02f), Point(26, -8, 0.02f), Point(53, -19, 0.02f)],
                3.2f, 0.12f, 0, TerrainFeatureCrossingPolicy.RequiresBridge, ["forest"], string.Empty,
                "authored:forgotten-forest@1"),
            new TerrainFeatureDefinition(Stable("feature:bridge"), TerrainFeatureDefinition.CurrentSchemaVersion,
                "Pont du gué", TerrainFeatureKind.Bridge,
                [Point(-2, -3, 0.34f), Point(6, 6, 0.34f)],
                4.2f, 0.22f, 0, TerrainFeatureCrossingPolicy.Allow, ["forest"], string.Empty,
                "authored:forgotten-forest@1"),
        };
        return new AreaWorldProfile(
            Stable("world"),
            AreaWorldProfile.CurrentSchemaVersion,
            terrain,
            biomes,
            features,
            defaults.VolumetricNavigation,
            defaults.FlightRoutes,
            defaults.Loading,
            "authored:forgotten-forest@1");
    }

    private static AreaNavigationDefinition CreateNavigation(AreaId areaId, AreaWorldProfile world)
    {
        var defaults = AreaNavigationDefinition.CreateDefault(areaId);
        Vector3Value Point(float x, float z) => new(x, world.Terrain.HeightAt(x, z) + .42f, z);
        var crossing = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Stable("navigation:bridge-crossing")),
            "Traversée du pont du gué",
            NavigationLinkKind.Jump,
            Point(-2, -3),
            Point(6, 6),
            true,
            .9f,
            "authored:forgotten-forest@1");
        return new AreaNavigationDefinition(
            defaults.Id,
            defaults.CellSize,
            defaults.AgentProfiles,
            [crossing],
            "authored:forgotten-forest@1");
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
