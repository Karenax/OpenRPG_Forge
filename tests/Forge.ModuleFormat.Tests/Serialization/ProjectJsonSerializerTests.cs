using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Lifecycle;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ProjectJsonSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreatureFoundation()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var interactionFactionId = Guid.Parse("a1620000-0000-4000-8000-000000000081");
        var interactionDialogueId = source.Dialogues.Single().Id;
        var foundation = CreatureFoundationProfile.CreateDefault(Guid.Parse("a1620000-0000-4000-8000-000000000001"),
            original.Slug, original.DisplayName, "author:r038");
        foundation = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion, foundation.Provenance,
            true, foundation.Identity, foundation.DefaultTransform,
            [new(Guid.Parse("a1620000-0000-4000-8000-000000000002"), "health", 80, 64, .25, true)],
            new(CreatureFoundationMovementMode.Swimming, 4, 8, 6, -5, 0, 60), foundation.References,
            foundation.Spawned, new Dictionary<string, string> { ["spawn.policy"] = "validated" },
            CreatureAuthoringCompositionProfile.CreateDefault(
                Guid.Parse("a1620000-0000-4000-8000-000000000077"), "author:r077"), true,
            CreatureRulesCompositionProfile.CreateDefault(
                Guid.Parse("a1620000-0000-4000-8000-000000000078"), "author:r078"), true,
            new CreatureInteractionCompositionProfile(
                Guid.Parse("a1620000-0000-4000-8000-000000000079"), 1, "author:r079",
                "asset.interaction.guardian", TransformValue.Identity,
                new(Guid.Parse("a1620000-0000-4000-8000-000000000080"),
                    interactionFactionId,
                    [new(Guid.Parse("a1620000-0000-4000-8000-000000000082"),
                        interactionFactionId, 1, "rank.guardian", 10, false)]),
                new(Guid.Parse("a1620000-0000-4000-8000-000000000083"),
                    [new(Guid.Parse("a1620000-0000-4000-8000-000000000084"),
                        CreatureInteractionSenseKind.Vision, 25, 120, true, ["stimulus.visual"])], 8),
                [new(Guid.Parse("a1620000-0000-4000-8000-000000000085"), "greeting", 10,
                    interactionDialogueId, null, null, true)],
                new(Guid.Parse("a1620000-0000-4000-8000-000000000087"), true, true, false,
                    ["creature.plot.override"]),
                new(Guid.Parse("a1620000-0000-4000-8000-000000000088"), 1, "author:r079", "1.0",
                    "creature.interaction_requested", "OnConversation", EventAuthorityScope.Server),
                new Dictionary<string, string>()), true,
            CreatureEventAiCompositionProfile.CreateDefault(
                Guid.Parse("a1620000-0000-4000-8000-000000000089"), "author:r080"), true);
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, original.CreatureEvents, original.ItemAdvanced, original.Authoring,
            foundation);
        var module = source.Module.WithFactions(new FactionCatalog(
            [new FactionDefinition(interactionFactionId, 1, "test:r079", "guardians", "Guardians", false, [],
                ["guardian"])], [], [], []));
        var project = new ProjectDefinition(source.Id, source.Title, module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredProfile = Assert.IsType<CreatureFoundationProfile>(restored.Blueprints.Single().CreatureFoundation);
        Assert.Equal(CreatureFoundationMovementMode.Swimming, restoredProfile.Movement.Mode);
        Assert.Equal(64, Assert.Single(restoredProfile.Resources).Current);
        Assert.Equal("validated", restoredProfile.Overrides["spawn.policy"]);
        Assert.True(restoredProfile.AuthoringCompositionRequiredAtRuntime);
        Assert.Equal("species.human", restoredProfile.AuthoringComposition!.Species.SpeciesId);
        Assert.Equal("mesh.guardian", restoredProfile.AuthoringComposition.Appearance.MeshAssetId);
        Assert.Equal(5, restoredProfile.AuthoringComposition.AnimationGraph.States.Count);
        Assert.True(restoredProfile.RulesCompositionRequiredAtRuntime);
        Assert.Equal(CreatureRulesInitialLifeState.Alive, restoredProfile.RulesComposition!.InitialState.LifeState);
        Assert.Equal("ruleset.core", restoredProfile.RulesComposition.Attributes.RulesetId);
        Assert.Equal("container.backpack", Assert.Single(restoredProfile.RulesComposition.Inventory.Containers).ContainerId);
        Assert.True(restoredProfile.InteractionCompositionRequiredAtRuntime);
        Assert.Equal(CreatureInteractionSenseKind.Vision,
            Assert.Single(restoredProfile.InteractionComposition!.Perception.Senses).Kind);
        Assert.Equal("greeting", Assert.Single(restoredProfile.InteractionComposition.DialogueBindings).Context);
        Assert.Equal("OnConversation", restoredProfile.InteractionComposition.OnConversation.Handler);
        Assert.True(restoredProfile.EventAiCompositionRequiredAtRuntime);
        Assert.Equal(CreatureEventAiControllerKind.StateMachine,
            restoredProfile.EventAiComposition!.Controller.Kind);
        Assert.Equal(2, restoredProfile.EventAiComposition.Controller.Blackboard.Count);
        Assert.Equal("OnSpellCastAt", restoredProfile.EventAiComposition.Ability.Impacted.Handler);
    }

    [Fact]
    public void Deserialize_MigratesPreCreatureFoundationFormatWithEmptyFoundation()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreatureFoundationFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creatureFoundation");
        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        Assert.Null(restored.Blueprints.Single().CreatureFoundation);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsBlueprintPermissionEvidence()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var provenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            original.Provenance.Source,
            original.Provenance.Author,
            "CC0-1.0",
            new string('a', 64),
            original.Provenance.IsAiGenerated,
            original.Provenance.Modifications,
            "contract:r031-round-trip");
        var blueprint = new BlueprintDefinition(
            original.Id, original.Slug, original.DisplayName, original.Kind, original.Combat,
            original.PaletteOrigin, original.IsFavorite, provenance, original.CreaturePresentation,
            original.CreatureProgression, original.CreatureLoot, original.CreatureRoleBehavior,
            original.CreatureEvents, original.ItemAdvanced, original.Authoring);
        var project = new ProjectDefinition(
            source.Id, source.Title, source.Module, [blueprint], source.Areas, source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal("contract:r031-round-trip", restored.Blueprints.Single().Provenance.PermissionEvidence);
        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Deserialize_MigratesPreCreatureEventsFormatWithEmptyEventProfile()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreatureEventsFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creatureEvents");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(restored.Blueprints.Single().CreatureEvents);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreatureEventBindings()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var events = CreateCreatureEvents();
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, events);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredEvents = Assert.IsType<CreatureEventProfile>(restored.Blueprints.Single().CreatureEvents);
        Assert.Equal(CreatureCombatBoundaryMode.TurnEnded, restoredEvents.CombatBoundary!.Mode);
        Assert.Equal(8, restoredEvents.Heartbeat!.BudgetMilliseconds);
        Assert.Equal("crypt.braziers_lit", Assert.Single(restoredEvents.CustomEvents).QualifiedName);
        Assert.Equal("BeforeDamage", restoredEvents.DamageDeath!.BeforeDamage.Handler);
        Assert.Equal("OnDeath", restoredEvents.DamageDeath.Died.Handler);
    }

    [Fact]
    public void Deserialize_MigratesPreCreaturePresentationFormatWithEmptyPresentation()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreaturePresentationFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creaturePresentation");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(restored.Blueprints.Single().CreaturePresentation);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreaturePresentation()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var presentation = new CreaturePresentationProfile(
            Guid.Parse("a1320000-0000-4000-8000-000000000001"), 1, "author:test", "nonbinary", "humanoid-medium",
            new("iel", "iel", "son", "soi"),
            [new("neutral", "assets/portraits/warden.png", 512, 512, "CC0-1.0", new string('b', 64), "Gardien")],
            false, "rig.humanoid", [new("head", "asset.helm", "rig.humanoid", 2, [], "hide-covered")],
            [new("primary", "#D68A3A")], [new("back", "wings", "asset.wings", "rig.humanoid")],
            new("voice.warden", "fr-FR", ["audio.warden"], ["angry"], "lipsync.fr-v1", ["a"], "CC0-1.0", "author-recorded"));
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance, presentation);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredPresentation = Assert.IsType<CreaturePresentationProfile>(restored.Blueprints.Single().CreaturePresentation);
        Assert.Equal("nonbinary", restoredPresentation.GenderId);
        Assert.Equal("asset.wings", Assert.Single(restoredPresentation.Attachments).AssetId);
        Assert.Equal("audio.warden", Assert.Single(restoredPresentation.Voice!.AudioBankIds));
    }

    [Fact]
    public void Deserialize_MigratesPreCreatureProgressionFormatWithEmptyProgression()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreatureProgressionFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creatureProgression");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(restored.Blueprints.Single().CreatureProgression);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreatureProgression()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1340000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", true, true,
            [new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"])],
            [new("mercy", "Impitoyable", "Miséricordieux", 0.5)],
            [new("lore", "Savoir", "insight", 2, 10, 2, true, false, 12)],
            [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
                [new("track", "warden", 2)], ["ember-strike"], ["combat.on-hit"])],
            new("prepared", AbilityLoadoutMode.Prepared, 2, ["ember-strike"],
                [new("ember-strike", 1, 2, 0.5, "focus", 1, ["empowered"])]),
            new Dictionary<string, string> { ["xp-rate"] = "1.0" });
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredProgression = Assert.IsType<CreatureProgressionProfile>(
            restored.Blueprints.Single().CreatureProgression);
        Assert.Equal(3, Assert.Single(restoredProgression.Tracks).Level);
        Assert.Equal("mercy", Assert.Single(restoredProgression.DispositionAxes).AxisId);
        Assert.Equal("ember-strike", Assert.Single(restoredProgression.Loadout.PreparedAbilities).AbilityId);
    }

    [Fact]
    public void Deserialize_MigratesPreCreatureRoleBehaviorFormatWithEmptyProfile()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreatureRoleBehaviorFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creatureRoleBehavior");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(restored.Blueprints.Single().CreatureRoleBehavior);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreatureRoleBehaviorAndTemplate()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1440000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", false, false,
            [new("warden", "Gardien", 1, 20, 0, [])], [],
            [new("awareness", "Vigilance", "insight", 1, 10, 0, true, false, 10)],
            [new("ember-stance", "Posture", 0, 1, true, [], [], [])],
            new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
        var behavior = new CreatureRoleBehaviorProfile(
            Guid.Parse("a1440000-0000-4000-8000-000000000002"), 1, "author:test", true,
            new("forge.generic", new Dictionary<string, string> { ["deity"] = "deity.embers" }, ["guardian"]),
            new("ambient.warden",
                [new(Guid.Parse("a1440000-0000-4000-8000-000000000003"), "idle.watch", ["idle", "inspect"], 1, 2, 6)],
                [new(Guid.Parse("a1440000-0000-4000-8000-000000000004"), "night-watch", 1200, 360,
                    "idle.watch", "campfire", "warden-warning")], [],
                new(true, true, true, AmbientResumeMode.Resume, .5)),
            new(Guid.Parse("a1440000-0000-4000-8000-000000000005"), "warden-template", 42, 6, true, false,
                [new("warden", 1)], [new("awareness", 2, 10)], [new("ember-stance", 4)]),
            new Dictionary<string, string> { ["ambient.radius"] = "4" });
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression, original.CreatureLoot, behavior);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredBehavior = Assert.IsType<CreatureRoleBehaviorProfile>(
            restored.Blueprints.Single().CreatureRoleBehavior);
        Assert.Equal("deity.embers", restoredBehavior.Role.LoreFields["deity"]);
        Assert.Equal("idle.watch", Assert.Single(restoredBehavior.Ambient.IdleSets).SetId);
        Assert.Equal(42, restoredBehavior.AutoLevel.Seed);
    }

    [Fact]
    public void Deserialize_MigratesPreCreatureLootFormatWithEmptyLootProfile()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCreatureLootFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
            blueprint!.AsObject().Remove("creatureLoot");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(restored.Blueprints.Single().CreatureLoot);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCreatureLootLifecycle()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var progression = new CreatureProgressionProfile(
            Guid.Parse("a1400000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", false, false,
            [new("warden", "Gardien", 1, 20, 0, [])], [],
            [new("sleight", "Larcin", "agility", 2, 10, 2, true, false, 12),
             new("awareness", "Vigilance", "insight", 2, 10, 2, true, false, 10)],
            [], new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
        var loot = new CreatureLootProfile(
            Guid.Parse("a1400000-0000-4000-8000-000000000002"), 1, "author:test", true,
            [new(Guid.Parse("a1400000-0000-4000-8000-000000000003"), "warden-ember", "pouch", 1, 1, 1,
                true, true, LootOwnershipMode.Party, LootInstancingMode.PerParty, null)],
            new(true, "sleight", "awareness", 12, 10, true, "crime.theft", 30, "loot.pickpocket"),
            new(CreatureCorpseMode.RagdollContainer, 45, "corpse.standard", "ragdoll.humanoid", true,
                "party", 8, CorpseCleanupMode.Timed),
            new Dictionary<string, string> { ["sparkle"] = "rare-only" });
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, progression, loot);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint], source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredLoot = Assert.IsType<CreatureLootProfile>(restored.Blueprints.Single().CreatureLoot);
        Assert.Equal("warden-ember", Assert.Single(restoredLoot.Entries).ItemSlug);
        Assert.Equal("sleight", restoredLoot.Theft.SkillId);
        Assert.Equal(CreatureCorpseMode.RagdollContainer, restoredLoot.Corpse.Mode);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsCanonicalDocument()
    {
        var serialized = ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid());

        var restored = ProjectJsonSerializer.Deserialize(serialized);
        var reserialized = ProjectJsonSerializer.Serialize(restored);

        Assert.Equal(serialized, reserialized);
        Assert.Equal("OpenRPG Forge test project", restored.Title);
        Assert.Single(restored.Blueprints);
        var combat = Assert.IsType<Forge.Domain.Combat.CombatProfile>(restored.Blueprints.Single().Combat);
        Assert.Equal(8, combat.Stats.MaximumHealth);
        Assert.Equal("cue.ember-strike", Assert.Single(combat.Abilities).VisualCue.CueKey);
        Assert.Equal("warden-ember", Assert.Single(combat.Loot).ItemSlug);
        var dialogue = Assert.Single(restored.Dialogues);
        Assert.Equal("dialogue.warden.warning", Assert.Single(dialogue.Nodes).Text.Key);
        Assert.Equal("quest.warden.active", Assert.Single(restored.Quests).States.First().Journal.LocalizationKey);
        var entity = Assert.Single(Assert.Single(restored.Areas).Entities);
        Assert.Equal(EntityPropertyOverrides.CurrentSchemaVersion, entity.Overrides.SchemaVersion);
        Assert.Equal(3, entity.Overrides.Properties.Count);
        Assert.Equal("80", entity.Overrides.Properties.Single(property => property.Key == "hitPoints").Value);
        Assert.Equal(5, restored.Module.Distribution.TargetProfiles.Count);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsLocalizedMetadataAndTypedVariables()
    {
        var source = TestProjectFactory.CreateValid();
        var metadata = new ProjectMetadata(
            ProjectMetadataId.From(Guid.Parse("cd000000-0000-4000-8000-000000000001")),
            "fr",
            [
                new ProjectLocalizedMetadata("fr", "Crypte", "Résumé", "Description", "Crédits"),
                new ProjectLocalizedMetadata("en", "Crypt", "Summary", "Description", "Credits"),
            ],
            "Atelier Forge",
            "2.0.0",
            "CC0-1.0",
            "assets/cover.png",
            ["aventure"],
            "Tout public",
            "test");
        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("ce000000-0000-4000-8000-000000000001")),
            "world.completed",
            ProjectVariableValueKind.Boolean,
            ProjectVariableScope.Save,
            "false",
            "Progression",
            "test",
            1,
            ProjectVariableReplicationMode.Party,
            false,
            true);
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            metadata,
            [variable]);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            module,
            source.Blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal("2.0.0", restored.Module.Metadata.Version);
        Assert.Equal(2, restored.Module.Metadata.Localizations.Count);
        var restoredVariable = Assert.Single(restored.Module.Variables);
        Assert.Equal(ProjectVariableValueKind.Boolean, restoredVariable.Kind);
        Assert.Equal(ProjectVariableReplicationMode.Party, restoredVariable.Replication);
        Assert.False(restoredVariable.DebugVisible);
        Assert.True(restoredVariable.RequiredAtRuntime);
    }

    [Fact]
    public void Deserialize_MigratesSafeRenameFormatWithDefaultModuleConfiguration()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.SafeRenameFormatVersion;
        document["project"]!["module"]!.AsObject().Remove("metadata");
        document["project"]!["module"]!.AsObject().Remove("variables");

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal("migration-default", migrated.Module.Metadata.Provenance);
        Assert.Empty(migrated.Module.Variables);
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(migrated));
    }

    [Fact]
    public void Deserialize_MigratesPreNavigationFormatWithStableAreaProfiles()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreNavigationFormatVersion;
        document["project"]!["areas"]!.AsArray()[0]!.AsObject().Remove("navigation");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(4, first.Areas[0].Navigation.AgentProfiles.Count);
        Assert.Equal(first.Areas[0].Navigation.Id, second.Areas[0].Navigation.Id);
        Assert.Equal(
            first.Areas[0].Navigation.AgentProfiles.Select(profile => profile.Id),
            second.Areas[0].Navigation.AgentProfiles.Select(profile => profile.Id));
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsTargetProfilesAndDependencyApprovals()
    {
        var source = TestProjectFactory.CreateValid();
        var defaults = source.Module.Distribution;
        var approval = new ThirdPartyDependencyApproval(
            ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000010")),
            "Distribution library",
            "3.1.0",
            "MIT",
            "Forge team",
            "Required for deterministic distribution tests.",
            "quarterly",
            "Replace with a first-party implementation.",
            "https://example.invalid/distribution-library",
            [ModuleTargetKind.Client, ModuleTargetKind.Shipping],
            "test");
        var release = new ModuleReleaseConfiguration(
            defaults.Release.Id,
            ModuleReleaseChannel.Preview,
            [ModuleDistributionPlatform.WindowsX64, ModuleDistributionPlatform.LinuxX64],
            new ModuleSigningConfiguration(ModuleSigningMode.Optional, "preview", [], [], true),
            new ModuleDeltaConfiguration(true, 512 * 1024, true, true, true, 3),
            defaults.Release.WindowsPackaging,
            "test");
        var launcher = new GameLauncherConfiguration(
            defaults.Launcher.Id,
            enabled: true,
            "La Crypte",
            "Le feu attend sous la pierre.",
            "Cendres",
            "#A36B42",
            "branding/hero.png",
            "https://example.com/news",
            "https://example.com/support",
            settingsEnabled: true,
            GameLauncherMultiplayerMode.DirectConnectPreview,
            6121,
            "test");
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [approval],
            "test",
            release,
            launcher: launcher);
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            source.Module.Metadata,
            source.Module.Variables,
            distribution);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            module,
            source.Blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(5, restored.Module.Distribution.TargetProfiles.Count);
        var restoredApproval = Assert.Single(restored.Module.Distribution.Dependencies);
        Assert.Equal(approval.Id, restoredApproval.Id);
        Assert.Equal(approval.Name, restoredApproval.Name);
        Assert.Equal(approval.Version, restoredApproval.Version);
        Assert.Equal(approval.LicenseSpdx, restoredApproval.LicenseSpdx);
        Assert.Equal(approval.RequiredForTargets, restoredApproval.RequiredForTargets);
        Assert.Equal(ModuleReleaseChannel.Preview, restored.Module.Distribution.Release.Channel);
        Assert.Equal(2, restored.Module.Distribution.Release.Platforms.Count);
        Assert.Equal(512 * 1024, restored.Module.Distribution.Release.Delta.ChunkSizeBytes);
        Assert.True(restored.Module.Distribution.Launcher.Enabled);
        Assert.Equal("Cendres", restored.Module.Distribution.Launcher.ExecutableName);
        Assert.Equal(GameLauncherMultiplayerMode.DirectConnectPreview,
            restored.Module.Distribution.Launcher.Multiplayer);
        Assert.Equal(6121, restored.Module.Distribution.Launcher.DefaultServerPort);
        Assert.Contains("\"launcher\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"secretsProfile\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secretValue", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_ProjectWithoutLauncherProfile_GetsAStableDisabledProfile()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["project"]!["module"]!["distribution"]!.AsObject().Remove("launcher");
        var legacyJson = document.ToJsonString();

        var first = ProjectJsonSerializer.Deserialize(legacyJson);
        var second = ProjectJsonSerializer.Deserialize(legacyJson);

        Assert.False(first.Module.Distribution.Launcher.Enabled);
        Assert.Equal(first.Module.Distribution.Launcher.Id, second.Module.Distribution.Launcher.Id);
        Assert.Equal("Jeu_OpenRPG", first.Module.Distribution.Launcher.ExecutableName);
    }

    [Fact]
    public void Deserialize_MigratesPreDistributionFormatWithStableDefaultProfiles()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreDistributionFormatVersion;
        document["project"]!["module"]!.AsObject().Remove("distribution");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(5, first.Module.Distribution.TargetProfiles.Count);
        Assert.Equal(first.Module.Distribution.Id, second.Module.Distribution.Id);
        Assert.Equal(
            first.Module.Distribution.TargetProfiles.Select(profile => profile.Id),
            second.Module.Distribution.TargetProfiles.Select(profile => profile.Id));
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(first));
    }

    [Fact]
    public void Deserialize_MigratesDistributionWithoutReleasePolicyDeterministically()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["project"]!["module"]!["distribution"]!.AsObject().Remove("release");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(first.Module.Distribution.Release.Id, second.Module.Distribution.Release.Id);
        Assert.Equal(ModuleReleaseChannel.Development, first.Module.Distribution.Release.Channel);
        Assert.Equal([ModuleDistributionPlatform.WindowsX64], first.Module.Distribution.Release.Platforms);
        Assert.True(first.Module.Distribution.Release.Delta.AllowRollback);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsTypedContentWorkspace()
    {
        var source = TestProjectFactory.CreateValid();
        var table = new DataTableDefinition(
            DataTableId.From(Guid.Parse("e3000000-0000-4000-8000-000000000001")),
            "encounters",
            "Encounters",
            [new DataColumnDefinition(
                DataColumnId.From(Guid.Parse("e3000000-0000-4000-8000-000000000002")),
                "difficulty", "Difficulty", DataColumnKind.Integer, required: true)],
            [new DataRowDefinition(
                DataRowId.From(Guid.Parse("e3000000-0000-4000-8000-000000000003")),
                new Dictionary<string, string> { ["difficulty"] = "4" })],
            licenseSpdx: "CC0-1.0",
            contentHash: new string('c', 64));
        var content = new ContentWorkspaceConfiguration(
            ContentWorkspaceConfigurationId.From(source.Module.Id.Value),
            new ContentBatchConfiguration("*.json", [".json"], ContentBatchCollisionPolicy.Replace, "test", "CC0-1.0"),
            [table],
            ContentMergePolicy.Manual,
            "test");
        var module = new ModuleDefinition(
            source.Module.Id,
            source.Module.Slug,
            source.Module.DisplayName,
            source.Module.Description,
            source.Module.EntryAreaId,
            source.Module.Metadata,
            source.Module.Variables,
            source.Module.Distribution,
            source.Module.Localization,
            content);
        var project = new ProjectDefinition(
            source.Id, source.Title, module, source.Blueprints, source.Areas, source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(ContentMergePolicy.Manual, restored.Module.Content.MergePolicy);
        Assert.Equal(DataColumnKind.Integer, Assert.Single(Assert.Single(restored.Module.Content.DataTables).Columns).Kind);
    }

    [Fact]
    public void Deserialize_MigratesPreContentWorkspaceFormatDeterministically()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreContentWorkspaceFormatVersion;
        document["project"]!["module"]!.AsObject().Remove("content");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Empty(first.Module.Content.DataTables);
        Assert.Equal(first.Module.Content.Id, second.Module.Content.Id);
        Assert.Equal(ContentMergePolicy.PreserveBoth, first.Module.Content.MergePolicy);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsArtisticCompatibilityProfile()
    {
        var source = TestProjectFactory.CreateValid();
        var asset = new ArtisticAssetStyle(
            source.Blueprints.Single().Id, 1, 512, "pbr-metallic-roughness", "original-fantasy",
            ArtisticRealism.StylizedRealism, ["earth", "stone"], "test");
        var style = new ArtisticStyleProfile(
            ArtisticStyleProfileId.From(source.Module.Id.Value), 1, 512, 20, 50,
            "pbr-metallic-roughness", "original-fantasy", ArtisticRealism.StylizedRealism,
            ["earth", "stone"], [asset], "test");
        var module = new ModuleDefinition(
            source.Module.Id, source.Module.Slug, source.Module.DisplayName, source.Module.Description,
            source.Module.EntryAreaId, source.Module.Metadata, source.Module.Variables, source.Module.Distribution,
            source.Module.Localization, source.Module.Content, style);
        var project = new ProjectDefinition(
            source.Id, source.Title, module, source.Blueprints, source.Areas, source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(ArtisticRealism.StylizedRealism, restored.Module.ArtisticStyle.Realism);
        Assert.Equal(512, restored.Module.ArtisticStyle.TargetTexelDensity);
        Assert.Equal(source.Blueprints.Single().Id, Assert.Single(restored.Module.ArtisticStyle.Assets).BlueprintId);
    }

    [Fact]
    public void Deserialize_MigratesPreArtisticStyleFormatDeterministically()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreArtisticStyleFormatVersion;
        document["project"]!["module"]!.AsObject().Remove("artisticStyle");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(first.Module.ArtisticStyle.Id, second.Module.ArtisticStyle.Id);
        Assert.Equal(ArtisticRealism.StylizedRealism, first.Module.ArtisticStyle.Realism);
        Assert.Empty(first.Module.ArtisticStyle.Assets);
    }

    [Fact]
    public void Deserialize_MigratesPreRenderingProfileFormatWithCanonicalDefaults()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreRenderingProfileFormatVersion;
        var environment = document["project"]!["areas"]!.AsArray()[0]!["environment"]!.AsObject();
        environment["schemaVersion"] = 1;
        environment.Remove("rendering");
        environment.Remove("sky");

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var restoredEnvironment = Assert.Single(migrated.Areas).Environment;

        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, restoredEnvironment.SchemaVersion);
        Assert.Equal(PbrMaterialWorkflow.MetallicRoughness, restoredEnvironment.Rendering.MaterialWorkflow);
        Assert.True(restoredEnvironment.Rendering.HdrOutput);
        Assert.True(restoredEnvironment.Rendering.PerPixelLighting);
        Assert.Equal(SkyProfileMode.Procedural, restoredEnvironment.Sky.Mode);
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(migrated));
    }

    [Fact]
    public void Deserialize_MigratesPreClimateProfileFormatWithCanonicalDefaults()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreClimateProfileFormatVersion;
        var environment = document["project"]!["areas"]!.AsArray()[0]!["environment"]!.AsObject();
        environment["schemaVersion"] = 2;
        environment.Remove("wind");
        environment.Remove("weather");
        environment.Remove("water");
        environment.Remove("reverb");

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var restored = Assert.Single(migrated.Areas).Environment;

        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(WindProfile.CurrentSchemaVersion, restored.Wind.SchemaVersion);
        Assert.Equal(WeatherProfile.CurrentSchemaVersion, restored.Weather.SchemaVersion);
        Assert.Empty(restored.Water.Bodies);
        Assert.Equal(restored.Audio.ReverbPreset, restored.Reverb.DefaultPreset);
    }

    [Fact]
    public void Deserialize_MigratesPreWorldProfileWithStableAreaDefaults()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreWorldProfileFormatVersion;
        document["project"]!["areas"]!.AsArray()[0]!.AsObject().Remove("world");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(first.Areas[0].World.Id, second.Areas[0].World.Id);
        Assert.Equal(first.Areas[0].World.Terrain.Id, second.Areas[0].World.Terrain.Id);
        Assert.Equal(TerrainProviderKind.None, first.Areas[0].World.Terrain.Provider);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(first), StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_MigratesPreTerrainPaintWithEmptySparseMask()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreTerrainPaintFormatVersion;
        document["project"]!["areas"]!.AsArray()[0]!["world"]!["terrain"]!.AsObject().Remove("materialPaint");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Empty(restored.Areas[0].World.Terrain.MaterialPaint);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(restored), StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_MigratesPreRuntimePoliciesWithStableInactiveDefaults()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreRuntimePolicyFormatVersion;
        document["project"]!["module"]!.AsObject().Remove("runtimePolicies");
        document["project"]!["areas"]!.AsArray()[0]!.AsObject().Remove("runtimePolicies");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Equal(first.Module.RuntimePolicies.Id, second.Module.RuntimePolicies.Id);
        Assert.False(first.Module.RuntimePolicies.Rest.Enabled);
        Assert.Empty(first.Module.RuntimePolicies.Heartbeats);
        Assert.Equal(first.Areas[0].RuntimePolicies.Id, second.Areas[0].RuntimePolicies.Id);
        Assert.Empty(first.Areas[0].RuntimePolicies.Heartbeats);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(first), StringComparison.Ordinal);
    }

    [Fact]
    public void R051ProjectGameplayEventsRoundTripAndMissingLegacyPropertyUsesStableDefaults()
    {
        var source = TestProjectFactory.CreateValid();
        var baseline = source.Module.RuntimePolicies;
        var projectEvents = new ProjectGameplayEventProfile(
            baseline.GameplayEvents.Id,
            1,
            "test:r051",
            true,
            true,
            false,
            false,
            true,
            baseline.GameplayEvents.Bindings.Select(binding => binding with
            {
                Provenance = "test:r051",
                Handler = binding.EventName == "player.ready" ? "OnReadyRoundTrip" : binding.Handler,
            }).ToArray());
        var policies = new ModuleRuntimePolicyProfile(baseline.Id, baseline.SchemaVersion, baseline.Heartbeats,
            baseline.Rest, baseline.SequenceAbort, "test:r051", baseline.Cinematics, baseline.Gameplay,
            baseline.Rules, baseline.Extensions, projectEvents);
        var module = source.Module.WithRuntimePolicies(policies);
        var project = new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.True(restored.Module.RuntimePolicies.GameplayEvents.RequiredAtRuntime);
        Assert.False(restored.Module.RuntimePolicies.GameplayEvents.UseDownedState);
        Assert.Equal("OnReadyRoundTrip", restored.Module.RuntimePolicies.GameplayEvents.Bindings.Single(binding =>
            binding.EventName == "player.ready").Handler);

        var legacy = JsonNode.Parse(serialized)!.AsObject();
        legacy["project"]!["module"]!["runtimePolicies"]!.AsObject().Remove("gameplayEvents");
        var migrated = ProjectJsonSerializer.Deserialize(legacy.ToJsonString());
        Assert.Equal(14, migrated.Module.RuntimePolicies.GameplayEvents.Bindings.Count);
        Assert.False(migrated.Module.RuntimePolicies.GameplayEvents.RequiredAtRuntime);
    }

    [Fact]
    public void R052ProjectWorldAndLocalizedPlayerPagesRoundTripWithLegacyDefaults()
    {
        var source = TestProjectFactory.CreateValid();
        var baseline = source.Module.RuntimePolicies;
        var areaId = source.Areas[0].Id.ToString();
        var custom = new CustomEventSchema(Guid.Parse("52000000-0000-4000-8000-000000000052"), 1,
            "test:r052", "crypt", "braziers_lit", 2,
            new Dictionary<string, string> { ["count"] = "int" }, false, "1.0", "OnBraziersLit",
            EventAuthorityScope.Server);
        var world = new ProjectWorldProfile(baseline.Id, 1, "test:r052", true, true, [custom],
            baseline.World.Clock with
            {
                Provenance = "test:r052",
                Speed = 4,
                AreaTimeZoneOffsets = new Dictionary<string, int> { [areaId] = 60 },
            });
        var policies = new ModuleRuntimePolicyProfile(baseline.Id, baseline.SchemaVersion, baseline.Heartbeats,
            baseline.Rest, baseline.SequenceAbort, "test:r052", baseline.Cinematics, baseline.Gameplay,
            baseline.Rules, baseline.Extensions, baseline.GameplayEvents, world);
        var originalMetadata = source.Module.Metadata;
        var localized = originalMetadata.Localizations.Select(item => new ProjectLocalizedMetadata(item.Locale,
            item.Name, item.Summary, "Synopsis", "Crédits", "Installation", "Règles", "Contrôles",
            "Licences", "Avertissements", "Compatibilité")).ToArray();
        var metadata = new ProjectMetadata(originalMetadata.Id, originalMetadata.DefaultLocale, localized,
            originalMetadata.Author, originalMetadata.Version, originalMetadata.License, originalMetadata.ImagePath,
            originalMetadata.Tags, originalMetadata.Classification, "test:r052", originalMetadata.SchemaVersion,
            originalMetadata.Foundation);
        var module = new ModuleDefinition(source.Module.Id, source.Module.Slug, source.Module.DisplayName,
            source.Module.Description, source.Module.EntryAreaId, metadata, source.Module.Variables,
            source.Module.Distribution, source.Module.Localization, source.Module.Content,
            source.Module.ArtisticStyle, policies, source.Module.Factions, source.Module.WorldFeatures);
        var project = new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);
        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal("OnBraziersLit", restored.Module.RuntimePolicies.World.CustomEvents.Single().Handler);
        Assert.Equal(4, restored.Module.RuntimePolicies.World.Clock.Speed);
        Assert.Equal("Contrôles", restored.Module.Metadata.GetDefaultLocalization().Controls);
        Assert.Equal(8, ProjectDocumentationValidator.Pages(
            restored.Module.Metadata.GetDefaultLocalization()).Count);

        var legacy = JsonNode.Parse(serialized)!.AsObject();
        legacy["project"]!["module"]!["runtimePolicies"]!.AsObject().Remove("world");
        foreach (var item in legacy["project"]!["module"]!["metadata"]!["localizations"]!.AsArray())
        {
            item!.AsObject().Remove("installation");
            item.AsObject().Remove("rules");
            item.AsObject().Remove("controls");
            item.AsObject().Remove("licenses");
            item.AsObject().Remove("warnings");
            item.AsObject().Remove("compatibility");
        }
        var migrated = ProjectJsonSerializer.Deserialize(legacy.ToJsonString());
        Assert.Empty(migrated.Module.RuntimePolicies.World.CustomEvents);
        Assert.Equal(string.Empty, migrated.Module.Metadata.GetDefaultLocalization().Controls);
    }

    [Fact]
    public void R053AdvancedTileEditingRoundTripsWithLegacyOptionalFallback()
    {
        var source = TestProjectFactory.CreateValid();
        var area = source.Areas[0];
        var entity = area.Entities[0];
        var definition = new ModularTileDefinition(Guid.Parse("53000000-0000-4000-8000-000000000001"),
            "crypt-floor", entity.BlueprintId, ModularTileRotationMode.Orthogonal, true, true,
            [new(Guid.Parse("53000000-0000-4000-8000-000000000002"), "east", "crypt-edge",
                TransformValue.Identity, ModularTileSocketDirection.East)], []);
        var placement = new ModularTilePlacement(Guid.Parse("53000000-0000-4000-8000-000000000003"),
            definition.Id, entity.Id, 0, 0, 0, ModularTileMirrorMode.None, string.Empty,
            Guid.Parse("53000000-0000-4000-8000-000000000004"));
        var group = new ModularTileGroup(placement.GroupId!.Value, "Crypt room", [placement.Id]);
        var editing = new AreaTileEditingProfile(Guid.Parse("53000000-0000-4000-8000-000000000005"), 1,
            "test:r053", [new(Guid.Parse("53000000-0000-4000-8000-000000000006"), group.Id,
                [new("width", "int", "1")], new(1, 1, 3),
                [new(Guid.Parse("53000000-0000-4000-8000-000000000007"), "door", "crypt-edge", 0, 0,
                    ModularTileSocketDirection.East)], [new("default", [definition.Id])], true)],
            new(AreaTileEditOperationKind.Replace, definition.Id, "crypt-remap", definition.Id, "crypt-floor", true),
            new(2026, [placement.Id], true),
            [new(Guid.Parse("53000000-0000-4000-8000-000000000008"), placement.Id, "lit", "torch-loop",
                true, new Dictionary<string, string> { ["floor"] = "wet-stone" },
                new(true, "#ff9858", 2, 8), true, true, ["door"],
                new Dictionary<string, string> { ["state.open"] = "false" })],
            new(1, 1, 512, 512, "Center", true, [new(placement.Id, 1, 0)]));
        var original = area.Foundation;
        var kit = new ModularTileKitProfile(original.TileKit.Id, 1, "test:r053", 1, [definition], [],
            [placement], [group], [], editing);
        var foundation = new AreaFoundationProfile(original.Id, 1, "test:r053", original.Creation, kit,
            original.Lifecycle, original.Volumes, original.Dependencies, true);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithFoundation(foundation) : item).ToArray(),
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);
        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredFoundation = restored.Areas.Single(item => item.Id == area.Id).Foundation;
        Assert.True(restoredFoundation.RequireTileEditingRuntimeMetadata);
        Assert.Equal("wet-stone", restoredFoundation.TileKit.Editing!.Properties[0].MaterialOverrides["floor"]);
        Assert.True(restoredFoundation.TileKit.Editing.Groups[0].ConvertToEditableInstances);

        var legacy = JsonNode.Parse(serialized)!.AsObject();
        var legacyFoundation = legacy["project"]!["areas"]![0]!["foundation"]!.AsObject();
        legacyFoundation.Remove("requireTileEditingRuntimeMetadata");
        legacyFoundation["tileKit"]!.AsObject().Remove("editing");
        var migrated = ProjectJsonSerializer.Deserialize(legacy.ToJsonString());
        Assert.False(migrated.Areas.Single(item => item.Id == area.Id).Foundation
            .RequireTileEditingRuntimeMetadata);
        Assert.Null(migrated.Areas.Single(item => item.Id == area.Id).Foundation.TileKit.Editing);
    }

    [Fact]
    public void R054AreaEnvironmentAuthoringRoundTripsWithLegacyOptionalFallback()
    {
        var source = TestProjectFactory.CreateValid();
        var area = source.Areas[0];
        var baseline = area.Environment;
        var authoring = new AreaEnvironmentAuthoringProfile(
            Guid.Parse("54000000-0000-4000-8000-000000000001"), 1, "test:r054",
            new(AreaEnvironmentClass.Exterior, "custom:moonlit", "forest-pack",
                true, true, true, true, true, true),
            new("physical-moonlit", "#30405f", "#ffe2b5", false,
                [new(0, 0, .8f, -45), new(12, 1.4f, 0, 65), new(24, 0, .8f, -45)],
                [new(Guid.Parse("54000000-0000-4000-8000-000000000002"), 18,
                    "world.sunset", "scripts/sunset.forge", true)],
                [new(Guid.Parse("54000000-0000-4000-8000-000000000003"), "Forest probe",
                    TransformValue.Identity, "asset:forest-probe", null, 24, false,
                    new Dictionary<string, string> { ["priority"] = "high" })],
                [new(Guid.Parse("54000000-0000-4000-8000-000000000004"), "Moon shrine",
                    TransformValue.Identity, "physical-shrine", FogMode.Volumetric, 3,
                    new Dictionary<string, string> { ["moon.boost"] = "1.5" })]),
            new("#b7c9dd", "#263248", new("asset:fog-noise", 12, .1f, .05f, 1.2f),
                [new(Guid.Parse("54000000-0000-4000-8000-000000000005"), "Low mist",
                    TransformValue.Identity, FogMode.Volumetric, .3f, .2f, "#9fb4c8", 2,
                    new Dictionary<string, string> { ["quality"] = "desktop" })],
                new Dictionary<string, AreaFogQualityTier> { ["desktop"] = AreaFogQualityTier.High }), true);
        var environment = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            AreaEnvironmentClass.Exterior, ["moonlit"],
            new(DayNightMode.Clock, 30, .4f, 1.4f, 5400, true, true),
            new(FogMode.Volumetric, .3f, 4, 90, 1.5f), baseline.Audio, baseline.Rendering,
            baseline.Sky, baseline.Wind, baseline.Weather, baseline.Water, baseline.Reverb, authoring, true);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithEnvironment(environment) : item).ToArray(),
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);
        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredEnvironment = restored.Areas.Single(item => item.Id == area.Id).Environment;
        Assert.Equal("custom:moonlit", restoredEnvironment.Authoring.Classification.Key);
        Assert.Equal("asset:fog-noise", restoredEnvironment.Authoring.Fog.Noise!.AssetId);
        Assert.True(restoredEnvironment.RequireAuthoringRuntimeMetadata);

        var legacy = JsonNode.Parse(serialized)!.AsObject();
        var legacyEnvironment = legacy["project"]!["areas"]![0]!["environment"]!.AsObject();
        legacyEnvironment.Remove("authoring");
        legacyEnvironment.Remove("requireAuthoringRuntimeMetadata");
        var migrated = ProjectJsonSerializer.Deserialize(legacy.ToJsonString());
        var migratedEnvironment = migrated.Areas.Single(item => item.Id == area.Id).Environment;
        Assert.Equal("migration-default", migratedEnvironment.Authoring.Provenance);
        Assert.False(migratedEnvironment.RequireAuthoringRuntimeMetadata);
    }

    [Fact]
    public void R055AreaExperienceRoundTripsAudioEventsMapVisibilityAndLod()
    {
        var source = TestProjectFactory.CreateValid();
        var area = source.Areas[0];
        var room = new AreaVisibilityRoomProfile(Guid.Parse("a0554000-0000-4000-8000-000000000001"),
            "Room", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var visibility = new AreaVisibilityAuthoringProfile([room], [], [], true, true, true, 16, 128);
        var lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96,
            24, .15f, true, true,
            [new(Guid.Parse("a0554000-0000-4000-8000-000000000002"), "asset:column",
                AreaLodSourceMode.Generated, [1, .5f], [12000, 6000], .2f, "test:r055")]);
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0554000-0000-4000-8000-000000000003"), 1, "test:r055",
            [new(Guid.Parse("a0554000-0000-4000-8000-000000000004"), "Combat",
                new Dictionary<string, float> { ["music"] = .8f }, .75f,
                $"area.{area.Slug}.combat", "test:r055")], visibility, lod, true);
        var eventName = $"area.{area.Slug}.alarm";
        var authoring = new AreaAuthoringProfile(1,
            [new(Guid.Parse("a0554000-0000-4000-8000-000000000005"), eventName,
                "schema://area/alarm/v1", "scripts/alarm.forge", 1, ["OnAreaAlarm"], true, "test:r055",
                AreaUserEvent.CurrentSchemaVersion,
                new Dictionary<string, string> { ["level"] = "int" },
                AreaUserEvent.CurrentForgeScriptApiVersion)],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0554000-0000-4000-8000-000000000006"), "Shrine", 16, 0, 0, false)],
                Guid.Parse("a0554000-0000-4000-8000-000000000007"), 1, "test:r055", true, true,
                AreaMapExplorationScope.Party, 8, "area.map.reveal"), [], experience);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithAuthoring(authoring) : item).ToArray(),
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var restoredAuthoring = restored.Areas.Single(item => item.Id == area.Id).Authoring;
        Assert.Equal(experience.Id, restoredAuthoring.Experience.Id);
        Assert.Single(restoredAuthoring.Experience.AudioSnapshots);
        Assert.Single(restoredAuthoring.UserEvents);
        Assert.Equal("int", restoredAuthoring.UserEvents[0].PayloadFields["level"]);
        Assert.True(restoredAuthoring.Map.Generate3D);
        Assert.Single(restoredAuthoring.Experience.Visibility.Rooms);
        Assert.Single(restoredAuthoring.Experience.Lod.AssetOverrides);
    }

    [Fact]
    public void R056AreaLayersRoundTripHierarchyGovernanceAndRequiredRuntimeMetadata()
    {
        var source = TestProjectFactory.CreateValid();
        var area = source.Areas[0];
        var root = Guid.Parse("a0564000-0000-4000-8000-000000000001");
        var child = Guid.Parse("a0564000-0000-4000-8000-000000000002");
        var entityId = area.Entities[0].Id;
        var authoring = new AreaAuthoringProfile(area.Authoring.SchemaVersion, area.Authoring.UserEvents,
            area.Authoring.Map,
        [
            new(root, "Architecture", null, true, false, "builder", [], provenance: "test:r056"),
            new(child, "Actors", root, true, false, "gameplay", [entityId],
                provenance: "test:r056", exportEnabled: false),
        ], area.Authoring.Experience, true);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithAuthoring(authoring) : item).ToArray(),
            source.Dialogues, source.Quests);

        var serialized = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(serialized);

        Assert.Equal(serialized, ProjectJsonSerializer.Serialize(restored));
        var layers = restored.Areas.Single(item => item.Id == area.Id).Authoring;
        Assert.True(layers.RequireLayerRuntimeMetadata);
        Assert.Equal(root, layers.Layers.Single(item => item.Id == child).ParentId);
        Assert.False(layers.Layers.Single(item => item.Id == child).ExportEnabled);
        Assert.All(layers.Layers, item =>
        {
            Assert.Equal(AreaEditorLayer.CurrentSchemaVersion, item.SchemaVersion);
            Assert.Equal("test:r056", item.Provenance);
        });
    }

    [Fact]
    public void Deserialize_MigratesPaletteProvenanceFormatWithProjectDefaults()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PaletteProvenanceFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
        {
            blueprint!.AsObject().Remove("paletteOrigin");
            blueprint.AsObject().Remove("isFavorite");
            blueprint.AsObject().Remove("provenance");
        }

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var restored = Assert.Single(migrated.Blueprints);

        Assert.Equal(BlueprintPaletteOrigin.ProjectContent, restored.PaletteOrigin);
        Assert.False(restored.IsFavorite);
        Assert.StartsWith("project://", restored.Provenance.Source, StringComparison.Ordinal);
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(migrated));
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedFormatVersion()
    {
        var serialized = ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid());
        var unsupported = serialized.Replace(
            $"\"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            "\"99.0.0\"",
            StringComparison.Ordinal);

        Assert.Throws<NotSupportedException>(() => ProjectJsonSerializer.Deserialize(unsupported));
    }

    [Fact]
    public void Deserialize_MigratesLegacyProjectToModuleAggregate()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.LegacyFormatVersion;
        document["project"]!.AsObject().Remove("module");
        RemoveEntityOverrides(document);
        RemoveDialogues(document);
        RemoveQuests(document);

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var current = ProjectJsonSerializer.Serialize(migrated);

        Assert.Equal(migrated.Id.Value, migrated.Module.Id.Value);
        Assert.Equal(migrated.Areas.Single().Id, migrated.Module.EntryAreaId);
        Assert.Contains($"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"", current);
        Assert.Contains("\"module\"", current);
        Assert.Contains("\"overrides\"", current);
        Assert.Empty(migrated.Dialogues);
        Assert.Empty(migrated.Quests);
    }

    [Fact]
    public void Deserialize_MigratesModuleAggregateFormatWithEmptyOverrides()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.ModuleAggregateFormatVersion;
        RemoveEntityOverrides(document);
        RemoveDialogues(document);
        RemoveQuests(document);

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var entity = Assert.Single(Assert.Single(migrated.Areas).Entities);

        Assert.Empty(entity.Overrides.Properties);
        Assert.Empty(migrated.Dialogues);
        Assert.Empty(migrated.Quests);
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(migrated));
    }

    [Fact]
    public void Deserialize_MigratesEntityOverridesFormatWithEmptyDialogues()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.EntityOverridesFormatVersion;
        RemoveDialogues(document);
        RemoveQuests(document);

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Empty(migrated.Dialogues);
        Assert.Equal(
            3,
            Assert.Single(Assert.Single(migrated.Areas).Entities).Overrides.Properties.Count);
    }

    [Fact]
    public void Deserialize_MigratesDialogueFormatWithEmptyQuests()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.DialogueFormatVersion;
        RemoveQuests(document);

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Single(migrated.Dialogues);
        Assert.Empty(migrated.Quests);
    }

    [Fact]
    public void Deserialize_MigratesQuestFormatWithEmptyCombatProfiles()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.QuestFormatVersion;
        foreach (var blueprint in document["project"]!["blueprints"]!.AsArray())
        {
            blueprint!.AsObject().Remove("combat");
        }

        var migrated = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Null(Assert.Single(migrated.Blueprints).Combat);
        Assert.Contains(
            $"\"formatVersion\": \"{ProjectJsonSerializer.CurrentFormatVersion}\"",
            ProjectJsonSerializer.Serialize(migrated));
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        Assert.ThrowsAny<JsonException>(() => ProjectJsonSerializer.Deserialize("{not-json}"));
    }

    [Fact]
    public void Serialize_RejectsMissingBlueprintReference()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "invalid-area",
            "Invalid area",
            [new EntityInstance(
                EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56")),
                BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc")),
                TransformValue.Identity)]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Invalid project",
            [],
            [area]);

        var exception = Assert.Throws<ProjectValidationException>(() => ProjectJsonSerializer.Serialize(project));

        Assert.Single(exception.Diagnostics);
    }

    private static CreatureEventProfile CreateCreatureEvents()
    {
        static EventBinding Binding(string id, string eventName, string handler) => new(
            Guid.Parse(id), 1, "author:test", "1.0", eventName, handler, EventAuthorityScope.Server);

        return new CreatureEventProfile(
            Guid.Parse("a1540000-0000-4000-8000-000000000001"), 1, "author:test", true,
            new(CreatureCombatBoundaryMode.TurnEnded,
                Binding("a1540000-0000-4000-8000-000000000002", "creature.turn_ended", "OnTurnEnded")),
            new(
                Binding("a1540000-0000-4000-8000-000000000003", "creature.inventory_accessed", "OnInventoryAccessed"),
                Binding("a1540000-0000-4000-8000-000000000004", "creature.item_transferred", "OnItemTransferred")),
            new(Binding("a1540000-0000-4000-8000-000000000005", "creature.rest_completed", "OnRestCompleted")),
            new(true, 2_000, 8, true,
                Binding("a1540000-0000-4000-8000-000000000006", "creature.heartbeat", "OnHeartbeat")),
            [new(Guid.Parse("a1540000-0000-4000-8000-000000000010"), "crypt", "braziers_lit", 2,
                new Dictionary<string, CreatureCustomFieldType> { ["count"] = CreatureCustomFieldType.Integer },
                true, Binding("a1540000-0000-4000-8000-000000000011",
                    "custom.crypt.braziers_lit", "OnBraziersLit"))],
            new Dictionary<string, string>(),
            new(
                Binding("a1540000-0000-4000-8000-000000000012", "creature.before_damage", "BeforeDamage"),
                Binding("a1540000-0000-4000-8000-000000000013", "creature.damage_applied", "OnDamaged"),
                Binding("a1540000-0000-4000-8000-000000000014", "entity.died", "OnDeath"),
                4, 0.5));
    }

    private static void RemoveEntityOverrides(JsonObject document)
    {
        foreach (var area in document["project"]!["areas"]!.AsArray())
        {
            foreach (var entity in area!["entities"]!.AsArray())
            {
                entity!.AsObject().Remove("overrides");
            }
        }
    }

    private static void RemoveDialogues(JsonObject document) =>
        document["project"]!.AsObject().Remove("dialogues");

    private static void RemoveQuests(JsonObject document) =>
        document["project"]!.AsObject().Remove("quests");
}
