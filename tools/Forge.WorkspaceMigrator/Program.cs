using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;

var addRuntimeExtensionSample = args.Contains("--runtime-extension-sample", StringComparer.Ordinal);
var addCreatureLootSample = args.Contains("--creature-loot-sample", StringComparer.Ordinal);
var addCreatureRoleBehaviorSample = args.Contains("--creature-role-behavior-sample", StringComparer.Ordinal);
var workspaceArguments = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (workspaceArguments.Length == 0)
{
    Console.Error.WriteLine("Usage: Forge.WorkspaceMigrator [--runtime-extension-sample] [--creature-loot-sample] [--creature-role-behavior-sample] <workspace> [workspace ...]");
    return 2;
}

foreach (var argument in workspaceArguments)
{
    var root = Path.GetFullPath(argument);
    var opened = ProjectSession.Open(root);
    if (!opened.Succeeded || opened.Session is null)
    {
        Console.Error.WriteLine($"MIGRATION_FAILED root={root} diagnostics=" +
            string.Join(';', opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        return 3;
    }
    var project = addRuntimeExtensionSample ? AddRuntimeExtensionSample(opened.Session.Project) : opened.Session.Project;
    if (addCreatureLootSample) project = AddCreatureLootSample(project);
    if (addCreatureRoleBehaviorSample) project = AddCreatureRoleBehaviorSample(project);
    var saved = opened.Session.Save(project);
    if (!saved.Succeeded)
    {
        Console.Error.WriteLine($"MIGRATION_FAILED root={root} diagnostics=" +
            string.Join(';', saved.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        return 4;
    }
    Console.WriteLine($"OPENRPG_FORGE_WORKSPACE_MIGRATED root={root} " +
        $"workspace={ProjectSession.CurrentProjectFormatVersion}");
}

return 0;

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
            blueprint.CreaturePresentation, blueprint.CreatureProgression, loot, blueprint.CreatureRoleBehavior);
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
            blueprint.CreaturePresentation, preview.Result, blueprint.CreatureLoot, behavior);
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
