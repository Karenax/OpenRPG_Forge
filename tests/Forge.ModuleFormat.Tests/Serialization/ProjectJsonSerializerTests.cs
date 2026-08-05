using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ProjectJsonSerializerTests
{
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
            "test");
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
        Assert.Equal(ProjectVariableValueKind.Boolean, Assert.Single(restored.Module.Variables).Kind);
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
        var distribution = new ModuleDistributionConfiguration(
            defaults.Id,
            defaults.TargetProfiles,
            [approval],
            "test",
            release);
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
        Assert.Contains("\"secretsProfile\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secretValue", serialized, StringComparison.OrdinalIgnoreCase);
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
