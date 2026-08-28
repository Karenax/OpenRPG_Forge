using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.ModuleFormat.Workspaces;
using Forge.WorkspaceMigrator;

var addRuntimeExtensionSample = args.Contains("--runtime-extension-sample", StringComparer.Ordinal);
var addCreatureLootSample = args.Contains("--creature-loot-sample", StringComparer.Ordinal);
var addCreatureRoleBehaviorSample = args.Contains("--creature-role-behavior-sample", StringComparer.Ordinal);
var addCreatureEventsSample = args.Contains("--creature-events-sample", StringComparer.Ordinal);
var addAdvancedFactionSample = args.Contains("--faction-advanced-sample", StringComparer.Ordinal);
var addDialoguePresentationSample = args.Contains("--dialogue-presentation-sample", StringComparer.Ordinal);
var addQuestOperationsSample = args.Contains("--quest-operations-sample", StringComparer.Ordinal);
var addItemAdvancedSample = args.Contains("--item-advanced-sample", StringComparer.Ordinal);
var materializeCryptRuntimeContent = args.Contains("--materialize-crypt-runtime-content", StringComparer.Ordinal);
var materializeWorldGenerationSlice = args.Contains("--materialize-world-generation-slice", StringComparer.Ordinal);
var queueWorldGenerationSlice = args.Contains("--queue-world-generation-slice", StringComparer.Ordinal);
var forceWorldGenerationSlice = args.Contains("--force-world-generation-slice", StringComparer.Ordinal);
var promoteWorldGenerationSlice = args.Contains("--promote-world-generation-slice", StringComparer.Ordinal);
var contentMigrationRequested = addRuntimeExtensionSample || addCreatureLootSample ||
    addCreatureRoleBehaviorSample || addCreatureEventsSample || addAdvancedFactionSample ||
    addDialoguePresentationSample || addQuestOperationsSample || addItemAdvancedSample ||
    materializeCryptRuntimeContent || materializeWorldGenerationSlice;
var worldGenerationOperationOnly = (queueWorldGenerationSlice || promoteWorldGenerationSlice) &&
    !contentMigrationRequested;
var workspaceArguments = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (workspaceArguments.Length == 0)
{
    Console.Error.WriteLine("Usage: Forge.WorkspaceMigrator [--runtime-extension-sample] [--creature-loot-sample] [--creature-role-behavior-sample] [--creature-events-sample] [--faction-advanced-sample] [--dialogue-presentation-sample] [--quest-operations-sample] [--item-advanced-sample] [--materialize-crypt-runtime-content] [--materialize-world-generation-slice] [--queue-world-generation-slice] [--force-world-generation-slice] [--promote-world-generation-slice] <workspace> [workspace ...]");
    return 2;
}

foreach (var argument in workspaceArguments)
{
    var root = Path.GetFullPath(argument);
    var opened = materializeCryptRuntimeContent
        ? ProjectSession.OpenForMigration(root,
            new HashSet<string>(["world.encounter.anchor_reference_missing"], StringComparer.Ordinal))
        : ProjectSession.Open(root);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.Error.WriteLine($"MIGRATION_FAILED root={root} diagnostics=" +
            string.Join(';', opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        return 3;
    }
    var project = addRuntimeExtensionSample ? AddRuntimeExtensionSample(opened.Session.Project) : opened.Session.Project;
    if (addCreatureLootSample) project = AddCreatureLootSample(project);
    if (addCreatureRoleBehaviorSample) project = AddCreatureRoleBehaviorSample(project);
    if (addCreatureEventsSample) project = AddCreatureEventsSample(project);
    if (addAdvancedFactionSample) project = AddAdvancedFactionSample(project);
    if (addDialoguePresentationSample) project = AddDialoguePresentationSample(project);
    if (addQuestOperationsSample) project = AddQuestOperationsSample(project);
    if (addItemAdvancedSample) project = AddItemAdvancedSample(project);
    if (materializeCryptRuntimeContent) project = MaterializeCryptRuntimeContent(project);
    if (materializeWorldGenerationSlice) project = WorldGenerationVerticalSliceMigration.Create(project);
    if (!worldGenerationOperationOnly)
    {
        var saved = opened.Session.Save(project);
        if (!saved.Succeeded)
        {
            Console.Error.WriteLine($"MIGRATION_FAILED root={root} diagnostics=" +
                string.Join(';', saved.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return 4;
        }
        if (materializeCryptRuntimeContent && !MaterializeCryptEncounterScripts(root))
        {
            Console.Error.WriteLine($"MIGRATION_FAILED root={root} diagnostics=crypt.encounter.scripts_write_failed");
            return 5;
        }
        Console.WriteLine($"OPENRPG_FORGE_WORKSPACE_MIGRATED root={root} " +
            $"workspace={ProjectSession.CurrentProjectFormatVersion}");
    }
    else
    {
        Console.WriteLine($"OPENRPG_FORGE_WORKSPACE_OPENED root={root} " +
            $"revision={opened.Session.Revision} workspace={ProjectSession.CurrentProjectFormatVersion}");
    }
    if (queueWorldGenerationSlice)
    {
        var queue = WorldGenerationVerticalSliceOrchestrator.Queue(root, project, forceWorldGenerationSlice);
        Console.WriteLine($"OPENRPG_FORGE_WORLD_GENERATION_QUEUED root={root} " +
            $"plan={queue.PlanId} job={queue.JobId} projectRevision={queue.ProjectRevision} " +
            $"recipeRevision={queue.RecipeRevision} status={queue.Status} reused={queue.Reused}");
    }
    if (promoteWorldGenerationSlice)
    {
        var promotion = WorldGenerationVerticalSliceOrchestrator.PromoteLatest(
            root,
            project,
            "codex:human-visual-review:2026-08-28:five-captures");
        Console.WriteLine($"OPENRPG_FORGE_WORLD_GENERATION_PROMOTED root={root} " +
            $"promotionPlan={promotion.PromotionPlanId} jobPlan={promotion.JobPlanId} " +
            $"scene={promotion.CanonicalSceneRelativePath} sha256={promotion.CanonicalSceneSha256} " +
            $"projectRevision={promotion.ProjectRevision}");
    }
}

return 0;

static bool MaterializeCryptEncounterScripts(string root)
{
    try
    {
        var scripts = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scripts);
        var authored = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["encounter_activate.forge"] =
                "void main()\n{\n    variable(\"encounter.warden.active\", \"true\");\n}\n",
            ["encounter_entered.forge"] =
                "void main()\n{\n    variable(\"encounter.warden.state\", \"entered\");\n}\n",
            ["encounter_exited.forge"] =
                "void main()\n{\n    variable(\"encounter.warden.state\", \"exited\");\n}\n",
            ["encounter_exhausted.forge"] =
                "void main()\n{\n    variable(\"encounter.warden.state\", \"exhausted\");\n}\n",
        };
        foreach (var name in new[]
                 {
                     "pillar_attack", "pillar_ability", "pillar_heartbeat", "pillar_pulse", "pillar_used",
                     "pillar_before_used", "pillar_after_used", "door_damage", "door_destroyed", "door_attack",
                     "door_ability", "door_heartbeat", "door_seal", "door_open_rejected", "door_ash_opened",
                     "trigger_click", "trigger_enter", "trigger_exit", "merchant_open", "merchant_rule",
                     "companion_recruit",
                 })
            authored[$"{name}.forge"] =
                $"void main()\n{{\n    variable(\"crypt.script.last\", \"{name}\");\n}}\n";
        foreach (var script in authored)
        {
            var path = Path.Combine(scripts, script.Key);
            if (!File.Exists(path)) File.WriteAllText(path, script.Value);
        }
        return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        return false;
    }
}

static ProjectDefinition MaterializeCryptRuntimeContent(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;

    project = AddCreatureEventsSample(project);
    var extensions = QualifyCryptPerformanceBudgets(
        ProductionCapabilityTemplateFactory.Create(project, "module-author:crypt-of-ashes"));
    var sourcePolicies = project.Module.RuntimePolicies;
    var policies = new ModuleRuntimePolicyProfile(
        sourcePolicies.Id,
        sourcePolicies.SchemaVersion,
        sourcePolicies.Heartbeats,
        sourcePolicies.Rest,
        sourcePolicies.SequenceAbort,
        "module-author:crypt-of-ashes",
        sourcePolicies.Cinematics,
        sourcePolicies.Gameplay,
        sourcePolicies.Rules,
        extensions,
        sourcePolicies.GameplayEvents,
        sourcePolicies.World,
        sourcePolicies.LifecycleEvents);
    var catalog = CryptWorldFeatureMigration.Create(project);
    var module = project.Module.WithRuntimePolicies(policies).WithWorldFeatures(catalog);
    var playerSpawnBlueprint = project.Blueprints.Single(item => item.Kind == BlueprintKind.PlayerSpawn);
    var areas = project.Areas.Select(area => area.Id == project.Module.EntryAreaId
        ? MaterializeCryptEntryArea(area, playerSpawnBlueprint.Id)
        : area).ToArray();
    return new ProjectDefinition(project.Id, project.Title, module, project.Blueprints, areas,
        project.Dialogues, project.Quests);
}

static ModuleRuntimeExtensionProfile QualifyCryptPerformanceBudgets(ModuleRuntimeExtensionProfile extensions)
{
    const string provenance = "module-author:crypt-of-ashes:performance-qualified";
    var tooling = extensions.ModernTooling;
    var quality = tooling.QualityAssurance;
    var budgets = quality.PlatformBudgets.Select(budget => new PlatformPerformanceBudget(
        budget.Id,
        budget.SchemaVersion,
        budget.Platform,
        budget.MaximumFrameMilliseconds,
        budget.MaximumMemoryMegabytes,
        budget.MaximumTextureMegabytes,
        budget.MaximumDrawCalls,
        budget.MaximumDynamicLights,
        budget.Platform switch
        {
            "dedicated-server" => 20,
            "desktop" => 16.67,
            _ => budget.MaximumScriptMilliseconds,
        },
        budget.MaximumNetworkKilobitsPerSecond,
        budget.WarningThreshold,
        budget.GateBuild,
        provenance)).ToArray();
    var qualifiedQuality = new QualityAssuranceProfile(
        quality.Id,
        quality.SchemaVersion,
        quality.Network,
        quality.Visual,
        quality.Benchmarks,
        budgets,
        quality.Profiler,
        provenance,
        quality.Accessibility,
        quality.SecurityFuzz,
        quality.ClickableDiagnostics,
        quality.MaliciousFixtures,
        quality.AuroraParityCoverage,
        quality.AcceptanceGates);
    var qualifiedTooling = new ModernRuntimeToolingProfile(
        tooling.Id,
        tooling.SchemaVersion,
        tooling.RuntimeCopy,
        tooling.AreaEffectIndicator,
        tooling.UiToolkit,
        tooling.Compiler,
        tooling.Documentation,
        tooling.Provenance,
        qualifiedQuality);
    return new ModuleRuntimeExtensionProfile(
        extensions.Id,
        extensions.SchemaVersion,
        extensions.RuleSchemas,
        extensions.UiDocuments,
        extensions.JsonDocuments,
        extensions.SnapshotPolicy,
        extensions.Provenance,
        qualifiedTooling,
        extensions.OperationalReadiness,
        extensions.PostLaunch);
}

static AreaDefinition MaterializeCryptEntryArea(AreaDefinition area, BlueprintId playerSpawnBlueprintId)
{
    var environment = area.Environment;
    var tags = environment.Tags.Concat([
        "art-kit:kaykit-dungeon-remastered",
        "atmosphere:ember",
        "camera:unbounded",
        "completion:boss-exit",
        "systemic:world-as-weapon",
    ]).ToArray();
    var authoredEnvironment = new AreaEnvironmentProfile(
        environment.SchemaVersion,
        environment.Classification,
        tags,
        environment.Lighting,
        environment.Fog,
        environment.Audio,
        environment.Rendering,
        environment.Sky,
        environment.Wind,
        environment.Weather,
        environment.Water,
        environment.Reverb,
        environment.Authoring,
        environment.RequireAuthoringRuntimeMetadata);
    var entities = area.Entities.Select(MaterializeCryptCharacterVisual).ToArray();
    return CryptNavigationMigration.MaterializeEncounterAnchor(
        area.WithEnvironment(authoredEnvironment).WithEntities(entities), playerSpawnBlueprintId);
}

static EntityInstance MaterializeCryptCharacterVisual(EntityInstance entity)
{
    var assetId = entity.Tags.Contains("player", StringComparer.OrdinalIgnoreCase)
        ? "053511fd-e165-1951-9869-6224f82b747f"
        : entity.Tags.Contains("elyra", StringComparer.OrdinalIgnoreCase)
            ? "220be95a-282c-cc52-aaf9-4fe811b32609"
            : entity.Tags.Contains("sentinel", StringComparer.OrdinalIgnoreCase)
                ? "056fe96c-5c83-145d-98fd-0623b9350cf5"
                : entity.Tags.Contains("warden", StringComparer.OrdinalIgnoreCase)
                    ? "3bf93c4b-7df6-455c-be89-ba38d40e941c"
                    : null;
    if (assetId is null) return entity;

    var properties = entity.Overrides.Properties
        .Where(item => item.Key is not "visual.asset" and not "runtime.animationProfile")
        .Concat([
            new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId),
            new EntityPropertyOverride("runtime.animationProfile", EntityPropertyValueKind.String,
                "kaykit-adventurer"),
        ])
        .ToArray();
    return new EntityInstance(entity.Id, entity.BlueprintId, entity.Transform, entity.Tags,
        new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties),
        entity.BlueprintVersionRequirement);
}

static ProjectDefinition AddQuestOperationsSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var quests = project.Quests.Select(quest =>
    {
        if (!string.Equals(quest.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return quest;
        var exclusiveTransition = quest.Transitions.First().Id;
        var profile = new QuestOperationsProfile(
            Guid.Parse("a1600000-0000-4000-8000-000000000001"), 1, "sample:g13-j", true,
            new(QuestJournalSortingPolicy.ImportanceThenUpdated, 90, true, true, 95, true),
            new(true, 3_600, 3, QuestRepeatResetMode.Full, [], .75, 2026),
            new(QuestShareMode.Unanimous, true, QuestSeparationPolicy.SnapshotIndividual, true),
            [new(Guid.Parse("a1600000-0000-4000-8000-000000000002"), exclusiveTransition,
                "crypt-ending", true,
                new LocalizedText("quest.crypt.choice.confirm", "Accepter cette voie irréversible ?"),
                new LocalizedText("quest.crypt.choice.consequence", "Les autres voies seront verrouillées."),
                [], "sample:g13-j")],
            new(QuestNotificationMode.Grouped,
                new LocalizedText("quest.crypt.notice.title", "Crypte mise à jour"),
                new LocalizedText("quest.crypt.notice.body", "Un nouvel objectif est disponible."),
                "Quête Crypte des Cendres mise à jour : nouvel objectif disponible.", "crypt-main", 3));
        return quest.WithOperations(profile);
    }).ToArray();
    return new(project.Id, project.Title, project.Module, project.Blueprints, project.Areas,
        project.Dialogues, quests);
}

static ProjectDefinition AddItemAdvancedSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var blueprints = project.Blueprints.Select(blueprint =>
    {
        if (!string.Equals(blueprint.Slug, "ash-key", StringComparison.Ordinal)) return blueprint;
        var profile = ItemAdvancedProfile.CreateDefault(
            Guid.Parse("a1610000-0000-4000-8000-000000000001"), "sample:g13-k-m") with
        {
            RequiredAtRuntime = true,
            Economy = new("gold", 125, 1.1, 1.4, .5, 1,
                new Dictionary<string, decimal> { ["crypt"] = 150 }),
            Requirements = new(1, 2, ["lore"], ["ash-attuned"], ["surface-alliance"],
                "quest.accepted"),
            Identification = new("unknown", 12, 5, "gold", true,
                [new("unknown", 0, ["name"]), new("recognized", 6, ["origin", "value"]),
                 new("identified", 12, ["unlock", "on-hit", "unique"])]),
            Ownership = new(true, "ash-watch", "crypt", true,
                [new(Guid.Parse("a1610000-0000-4000-8000-000000000002"), "ash-watch", "crypt",
                    "warden-seal", DateTimeOffset.Parse("2026-08-01T00:00:00Z"), "sample:g13-k")]),
            OnHitProcs = [new(Guid.Parse("a1610000-0000-4000-8000-000000000003"), "unlock-strike", 1,
                2, "target.ash-sealed", "ash-key-pulse", "wielder", true, "sample:g13-k")],
            Weapon = new("ritual-key", .8, false,
                [new("key-thrust", "fire", 1, 4, 1.2, "attack.key-thrust", ["ritual"])],
                ["key", "ritual"]),
            Armor = new(["hand"], 5, .2, 0, "ember-steel",
                new Dictionary<string, double> { ["fire"] = .25 }, ["dim", "cracked"]),
            Ammo = new("ash-spark", "assets/projectiles/ash-spark.glb", 12, .5, "fire", true),
            Durability = new(true, 40, 40, .25, 1, "smithing", 2, "item.ash-key.broken",
                "visual.ash-key.cracked"),
            Container = new(4, 10, 2, .75, ["gem", "key-fragment"], ["inventory.open"], true, true),
            LootContexts = [new(Guid.Parse("a1610000-0000-4000-8000-000000000004"), "crypt-chest",
                "crypt", "ash-watch", 1, 20, "normal", ["undead"], "quest.accepted", "eclipse", 5,
                "sample:g13-m")],
            Unique = new(true, "unique:ash-key", ItemUniqueScope.World, 60, true),
            LootOwnership = new(ItemLootOwnershipMode.Personal, 120, true, true),
            Recipes = [new(Guid.Parse("a1610000-0000-4000-8000-000000000005"), "repair-ash-key",
                [new("tag:ember-metal", 2, true, 1)], "forge", "smithing", 10, 1, 6, [],
                "sample:g13-m")],
            Generator = new(true, 6, 1, 2, ["ritual-key"],
                new Dictionary<string, int> { ["common"] = 80, ["rare"] = 20 },
                [new(Guid.Parse("a1610000-0000-4000-8000-000000000006"), "keen", "ritual-key", 2, 5,
                        "common", ["heavy"], ["critical"], "sample:g13-m"),
                 new(Guid.Parse("a1610000-0000-4000-8000-000000000007"), "ember", "ritual-key", 4, 2,
                        "rare", [], ["fire-damage"], "sample:g13-m")], 17)
        };
        return blueprint.WithItemAdvanced(profile);
    }).ToArray();
    return new(project.Id, project.Title, project.Module, blueprints, project.Areas,
        project.Dialogues, project.Quests);
}

static ProjectDefinition AddDialoguePresentationSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal) ||
        project.Dialogues.Count == 0) return project;
    var dialogues = project.Dialogues.Select(dialogue =>
    {
        if (!string.Equals(dialogue.Slug, "elyra-crypt-of-ashes", StringComparison.Ordinal)) return dialogue;
        var entry = dialogue.EntryNodeId;
        var interjectionNode = dialogue.Nodes.Skip(1).FirstOrDefault()?.Id ?? entry;
        var speaker = dialogue.Nodes.First(item => item.Id == entry).SpeakerBlueprintId
            ?? project.Blueprints.First().Id;
        var profile = new DialoguePresentationProfile(
            Guid.Parse("d1350000-0000-4000-8000-000000000010"), 1, "sample:g13-h", false, true,
            [new(Guid.Parse("d1350000-0000-4000-8000-000000000011"), entry, speaker,
                "dialogue.explain", 0, 1800, 180, DialogueGesture.Explain, null,
                DialogueAnimationInterruptionPolicy.BlendOut, "idle", "sample:g13-h")],
            [new(Guid.Parse("d1350000-0000-4000-8000-000000000012"), entry, speaker, "fr-FR", null,
                "elyra-crypt-01", dialogue.Nodes.First(item => item.Id == entry).Text,
                "viseme-neutral", "concerned", 1800, null, null, true, "sample:g13-h")],
            [new(Guid.Parse("d1350000-0000-4000-8000-000000000013"), entry,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.Medium, 52, 1800, 250,
                true, true, true, .08f, 10, "sample:g13-h")],
            [new(Guid.Parse("d1350000-0000-4000-8000-000000000014"), entry, "Accueil d’Elyra",
                DialogueBookmarkScope.Shared, "Valider cadrage, animation et sous-titre.", "aurora-review",
                $"dialogue://{dialogue.Id}/{entry}", "sample:g13-h")],
            [new(Guid.Parse("d1350000-0000-4000-8000-000000000015"), entry, interjectionNode, speaker,
                ["guardian"], true, 0, 100, 30, "elyra-main",
                DialogueInterjectionConflictPolicy.HighestPriority, "sample:g13-h")]);
        return dialogue.WithPresentation(profile);
    }).ToArray();
    return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints, project.Areas,
        dialogues, project.Quests);
}

static ProjectDefinition AddAdvancedFactionSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var covenant = Guid.Parse("b2050000-0000-4000-8000-000000000001");
    var watch = Guid.Parse("b2050000-0000-4000-8000-000000000002");
    var surface = Guid.Parse("b2050000-0000-4000-8000-000000000003");
    var created = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
    var catalog = new FactionCatalog(
        [
            new(covenant, 1, "sample:g13-g", "crypt-covenant", "Pacte de la crypte", false, [],
                ["crypt", "guardian"], new Dictionary<string, string>
                {
                    ["intruder-response"] = "challenge",
                    ["relic-duty"] = "protect",
                }),
            new(watch, 1, "sample:g13-g", "ash-watch", "Veilleurs des cendres", false, [covenant],
                ["watch", "undead"]),
            new(surface, 1, "sample:g13-g", "surface-alliance", "Alliance de la surface", false, [],
                ["adventurer", "civilian"]),
        ],
        [new(covenant, surface, -75, "tomb-intrusion", "hostile"),
         new(surface, covenant, -30, "fear-of-the-crypt", "wary")],
        [new("hostile", -100, -1, 1), new("neutral", 0, 50, 1), new("allied", 51, 100, 1)],
        [],
        new FactionAdvancedProfile(
            Guid.Parse("b2050000-0000-4000-8000-000000000010"), 1, "sample:g13-g", true,
            [new(Guid.Parse("b2050000-0000-4000-8000-000000000011"), watch, true, true, true)],
            [new(Guid.Parse("b2050000-0000-4000-8000-000000000012"), watch, .55, .45, 35, 900, -12, true)],
            [
                new(Guid.Parse("b2050000-0000-4000-8000-000000000013"), 1, "sample:g13-g",
                    "ash-warden-boss", watch, "warden", null, 100, 1, true, null),
                new(Guid.Parse("b2050000-0000-4000-8000-000000000014"), 1, "sample:g13-g",
                    "hero", surface, "pathfinder", null, 85, 1, true, null),
                new(Guid.Parse("b2050000-0000-4000-8000-000000000015"), 1, "sample:g13-g",
                    "hero", watch, "novice", "Ash Pilgrim", -35, .2, false, created.AddYears(4)),
            ],
            [new(Guid.Parse("b2050000-0000-4000-8000-000000000016"), 1, "sample:g13-g", watch,
                DynamicFactionKind.Runtime, created, created.AddYears(4), true, true, 1,
                [new(Guid.Parse("b2050000-0000-4000-8000-000000000017"), surface,
                    FactionDiplomacyStatus.War, created, null, "crypt-intrusion")])]),
        true);
    return new ProjectDefinition(project.Id, project.Title, project.Module.WithFactions(catalog),
        project.Blueprints, project.Areas, project.Dialogues, project.Quests);
}

static ProjectDefinition AddCreatureLootSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var blueprints = project.Blueprints.Select(blueprint =>
    {
        if (!string.Equals(blueprint.Slug, "ash-warden-boss", StringComparison.Ordinal)) return blueprint;
        var loot = new CreatureLootProfile(
            Guid.Parse("a1430000-0000-4000-8000-000000000001"), 1, "sample:g13-d", true,
            [new(Guid.Parse("a1430000-0000-4000-8000-000000000002"), "warden-ember", "relic", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null)],
            new(false, "stealth", "awareness", 10, 10, true, "crime.theft", 30, "loot.pickpocket"),
            new(CreatureCorpseMode.RagdollContainer, 60, "corpse.standard", "ragdoll.humanoid", true,
                "party", 8, CorpseCleanupMode.Timed),
            new Dictionary<string, string> { ["presentation"] = "ember-relic" });
        return new BlueprintDefinition(blueprint.Id, blueprint.Slug, blueprint.DisplayName, blueprint.Kind,
            blueprint.Combat, blueprint.PaletteOrigin, blueprint.IsFavorite, blueprint.Provenance,
            blueprint.CreaturePresentation, blueprint.CreatureProgression, loot, blueprint.CreatureRoleBehavior,
            blueprint.CreatureEvents, blueprint.ItemAdvanced);
    }).ToArray();
    return new ProjectDefinition(project.Id, project.Title, project.Module, blueprints, project.Areas,
        project.Dialogues, project.Quests);
}

static ProjectDefinition AddCreatureRoleBehaviorSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var blueprints = project.Blueprints.Select(blueprint =>
    {
        if (!string.Equals(blueprint.Slug, "ash-warden-boss", StringComparison.Ordinal)) return blueprint;
        var progression = blueprint.CreatureProgression ?? new CreatureProgressionProfile(
            Guid.Parse("a1470000-0000-4000-8000-000000000001"), 1, "sample:g13-e", "forge.generic", false, true,
            [new("warden", "Gardien", 1, 20, 0, [])], [],
            [new("awareness", "Vigilance", "insight", 1, 10, 0, true, false, 10)],
            [new("ember-stance", "Posture des braises", 0, 1, true, [], [], [])],
            new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
        var behavior = new CreatureRoleBehaviorProfile(
            Guid.Parse("a1470000-0000-4000-8000-000000000002"), 1, "sample:g13-e", true,
            new("forge.generic", new Dictionary<string, string>
            {
                ["deity"] = "deity.embers",
                ["occupation"] = "crypt-warden",
            }, ["guardian", "boss"]),
            new("ambient.ash-warden",
                [
                    new(Guid.Parse("a1470000-0000-4000-8000-000000000003"), "idle.watch",
                        ["idle", "look-around"], 2, 2, 6),
                    new(Guid.Parse("a1470000-0000-4000-8000-000000000004"), "idle.ritual",
                        ["inspect", "warm-hands"], 1, 3, 8),
                ],
                [new(Guid.Parse("a1470000-0000-4000-8000-000000000005"), "night-watch", 1200, 360,
                    "idle.ritual", null, null)],
                [new(Guid.Parse("a1470000-0000-4000-8000-000000000006"), "inspect-rune", "ash-rune",
                    "inspect", 12)],
                new(true, true, true, AmbientResumeMode.Resume, .5)),
            new(Guid.Parse("a1470000-0000-4000-8000-000000000007"), "warden-balanced", 42, 6, true, false,
                [new("warden", 1)], [new("awareness", 2, 10)], [new("ember-stance", 4)]),
            new Dictionary<string, string> { ["ambient.radius"] = "4" });
        var preview = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(behavior, progression);
        if (!preview.Succeeded || preview.Result is null)
            throw new InvalidOperationException("The G13-E sample profile cannot resolve its deterministic progression.");
        return new BlueprintDefinition(blueprint.Id, blueprint.Slug, blueprint.DisplayName, blueprint.Kind,
            blueprint.Combat, blueprint.PaletteOrigin, blueprint.IsFavorite, blueprint.Provenance,
            blueprint.CreaturePresentation, preview.Result, blueprint.CreatureLoot, behavior, blueprint.CreatureEvents,
            blueprint.ItemAdvanced);
    }).ToArray();
    return new ProjectDefinition(project.Id, project.Title, project.Module, blueprints, project.Areas,
        project.Dialogues, project.Quests);
}

static ProjectDefinition AddCreatureEventsSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal)) return project;
    var blueprints = project.Blueprints.Select(blueprint =>
    {
        if (!string.Equals(blueprint.Slug, "ash-warden-boss", StringComparison.Ordinal)) return blueprint;
        static EventBinding Binding(
            string id,
            string eventName,
            string handler,
            string provenance = "sample:g13-f") => new(
            Guid.Parse(id), 1, provenance, "1.0", eventName, handler, EventAuthorityScope.Server);
        var overrides = blueprint.CreatureEvents?.Overrides.ToDictionary(item => item.Key, item => item.Value,
                            StringComparer.Ordinal) ??
                        new Dictionary<string, string>(StringComparer.Ordinal);
        overrides["runtime.phase_two.custom_event"] = "crypt.braziers_lit";
        overrides["telemetry.channel"] = "ash-warden";
        var damageDeath = blueprint.CreatureEvents?.DamageDeath ?? new(
            Binding("a1570000-0000-4000-8000-000000000012", "creature.before_damage", "BeforeDamage",
                "sample:r039"),
            Binding("a1570000-0000-4000-8000-000000000013", "creature.damage_applied", "OnDamaged",
                "sample:r039"),
            Binding("a1570000-0000-4000-8000-000000000014", "entity.died", "OnDeath", "sample:r039"),
            4,
            0.5);
        var events = new CreatureEventProfile(
            Guid.Parse("a1570000-0000-4000-8000-000000000001"), 1, "sample:g13-f", true,
            new(CreatureCombatBoundaryMode.CombatTickCompleted,
                Binding("a1570000-0000-4000-8000-000000000002", "creature.combat_tick_completed",
                    "OnCombatTickCompleted")),
            new(
                Binding("a1570000-0000-4000-8000-000000000003", "creature.inventory_accessed",
                    "OnInventoryAccessed"),
                Binding("a1570000-0000-4000-8000-000000000004", "creature.item_transferred",
                    "OnItemTransferred")),
            new(Binding("a1570000-0000-4000-8000-000000000005", "creature.rest_completed",
                "OnRestCompleted")),
            new(true, 1_500, 8, true,
                Binding("a1570000-0000-4000-8000-000000000006", "creature.heartbeat", "OnHeartbeat")),
            [new(Guid.Parse("a1570000-0000-4000-8000-000000000010"), "crypt", "braziers_lit", 1,
                new Dictionary<string, CreatureCustomFieldType>
                {
                    ["count"] = CreatureCustomFieldType.Integer,
                    ["source"] = CreatureCustomFieldType.EntityId,
                }, true, Binding("a1570000-0000-4000-8000-000000000011", "custom.crypt.braziers_lit",
                    "OnBraziersLit"))],
            overrides,
            damageDeath);
        return blueprint.WithCreatureEvents(events);
    }).ToArray();
    return new ProjectDefinition(project.Id, project.Title, project.Module, blueprints, project.Areas,
        project.Dialogues, project.Quests);
}

static ProjectDefinition AddRuntimeExtensionSample(ProjectDefinition project)
{
    if (!string.Equals(project.Module.Slug, "crypt-of-ashes", StringComparison.Ordinal))
        return project;
    var source = project.Module.RuntimePolicies.Extensions;
    var schemas = new[]
    {
        new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000001"), 1,
            "class.ash-warden", RuleSchemaKind.Class, "{\"type\":\"object\"}",
            "{\"hitDie\":10,\"castingModel\":\"focus\"}", true, "sample:g11-o"),
        new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000002"), 1,
            "weapon.ember-blade", RuleSchemaKind.Weapon, "{\"type\":\"object\"}",
            "{\"damage\":8,\"damageType\":\"fire\"}", false, "sample:g11-o"),
        new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000003"), 1,
            "vfx.ash-nova", RuleSchemaKind.Vfx, "{\"type\":\"object\"}",
            "{\"cue\":\"cue.ash-nova\",\"radius\":4}", false, "sample:g11-o"),
        new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000004"), 1,
            "casting.focus", RuleSchemaKind.Casting, "{\"type\":\"object\"}",
            "{\"resource\":\"focus\",\"interruptible\":true}", false, "sample:g11-o"),
    };
    var runtimeJson = new RuntimeJsonDocumentDefinition(
        Guid.Parse("e0150000-0000-4000-8000-000000000001"), 1, "adventure-state",
        "{\"type\":\"object\",\"required\":[\"chapter\",\"status\",\"progress\"]}",
        "{\"chapter\":\"Crypte des Cendres\",\"status\":\"Explorez la crypte\",\"progress\":0,\"objectives\":[\"Trouver Elyra\",\"Ouvrir la porte\"],\"autoTrack\":true}",
        4096, 8, true, "1.0", "sample:g11-o");
    var widgets = new[]
    {
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000001"), "chapter",
            ScriptableUiWidgetKind.Label, null, "Crypte des Cendres", "adventure-state:/chapter", false, 0, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000002"), "status",
            ScriptableUiWidgetKind.Label, null, "Explorez la crypte", "adventure-state:/status", false, 1, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000003"), "progress",
            ScriptableUiWidgetKind.Progress, null, "Progression", "adventure-state:/progress", false, 2, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000004"), "journal",
            ScriptableUiWidgetKind.Button, null, "Ouvrir le journal", null, true, 3, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000005"), "adventure-form",
            ScriptableUiWidgetKind.Form, null, "Suivi", null, false, 4, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000006"), "objectives",
            ScriptableUiWidgetKind.List, "adventure-form", "Objectifs", "adventure-state:/objectives", false, 5, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000007"), "tactical-map",
            ScriptableUiWidgetKind.Canvas, "adventure-form", "Carte tactique", null, false, 6, "player"),
        new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000008"), "auto-track",
            ScriptableUiWidgetKind.CheckBox, "adventure-form", "Suivi automatique", "adventure-state:/autoTrack", true, 7, "player"),
    };
    var ui = new ScriptableUiDocument(Guid.Parse("e0120000-0000-4000-8000-000000000010"), 1,
        "adventure-hud", "État de l’aventure", "forge-dark", true, widgets,
        [new ScriptableUiEventBinding(Guid.Parse("e0130000-0000-4000-8000-000000000001"), "journal",
            ScriptableUiEventKind.Pressed, "ToggleJournal", "1.0", "gameplay", "player"),
         new ScriptableUiEventBinding(Guid.Parse("e0130000-0000-4000-8000-000000000002"), "auto-track",
            ScriptableUiEventKind.Toggled, "SetAutoTrack", "1.0", "gameplay", "player")], "sample:g11-p");
    var extensions = new ModuleRuntimeExtensionProfile(source.Id, 1, schemas, [ui], [runtimeJson],
        source.SnapshotPolicy, "sample:g11-o");
    var policy = project.Module.RuntimePolicies;
    var policies = new ModuleRuntimePolicyProfile(policy.Id, policy.SchemaVersion, policy.Heartbeats, policy.Rest,
        policy.SequenceAbort, policy.Provenance, policy.Cinematics, policy.Gameplay, policy.Rules, extensions);
    return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
        project.Blueprints, project.Areas, project.Dialogues, project.Quests);
}
