using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class AreaExperiencePersistenceTests
{
    [Fact]
    public void Project_and_area_round_trip_preserve_area_experience_profile()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "experience", "Experience", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Experience persistence", [], [area]);

        var projectJson = ProjectJsonSerializer.Serialize(project);
        var areaJson = AreaJsonSerializer.Serialize(area);
        var restoredProject = ProjectJsonSerializer.Deserialize(projectJson);
        var restoredArea = AreaJsonSerializer.Deserialize(areaJson);

        Assert.Equal(area.Id.Value, restoredProject.Areas[0].RuntimePolicies.Experience.Id);
        Assert.Equal("Chargement", restoredArea.RuntimePolicies.Experience.Loading.Title);
        Assert.Contains("\"experience\"", projectJson, StringComparison.Ordinal);
        Assert.Equal("0.6.38", ProjectJsonSerializer.CurrentFormatVersion);
        Assert.Equal("1.10.0", AreaJsonSerializer.CurrentFormatVersion);
    }

    [Fact]
    public void Previous_formats_migrate_missing_experience_to_stable_default()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "legacy", "Legacy", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Legacy experience", [], [area]);
        var projectJson = ProjectJsonSerializer.Serialize(project)
            .Replace(ProjectJsonSerializer.CurrentFormatVersion,
                ProjectJsonSerializer.PreAreaExperienceFormatVersion, StringComparison.Ordinal)
            .Replace("\n        \"experience\": {", "\n        \"experienceRemoved\": {", StringComparison.Ordinal);

        var restored = ProjectJsonSerializer.Deserialize(projectJson);
        Assert.Equal(area.Id.Value, restored.Areas[0].RuntimePolicies.Experience.Id);
        Assert.Equal("Chargement", restored.Areas[0].RuntimePolicies.Experience.Loading.Title);
    }

    [Fact]
    public void Ux2LwCProjectAndAreaRoundTripsPreserveTypedAreaUserEventContract()
    {
        var source = new AreaDefinition(AreaId.From(Guid.Parse("a0240000-0000-4000-8000-000000000010")),
            "typed-events", "Typed events", []);
        var authoring = source.Authoring;
        var binding = new AreaUserEvent(Guid.Parse("a0240000-0000-4000-8000-000000000011"),
            "area.typed-events.alarm", "schema://area/alarm/v2", "scripts/alarm.forge", 2,
            ["OnAreaAlarm"], true, "test:ux2-lw-c", AreaUserEvent.CurrentSchemaVersion,
            new Dictionary<string, string>
            {
                ["actor"] = "guid",
                ["level"] = "int",
                ["message"] = "string",
            }, AreaUserEvent.CurrentForgeScriptApiVersion);
        var area = source.WithAuthoring(new(authoring.SchemaVersion, [binding], authoring.Map,
            authoring.Layers, authoring.Experience, authoring.RequireLayerRuntimeMetadata));
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a0240000-0000-4000-8000-000000000012")),
            "Typed event persistence", [], [area]);

        var restoredProject = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));
        var restoredArea = AreaJsonSerializer.Deserialize(AreaJsonSerializer.Serialize(area));
        var projectBinding = Assert.Single(restoredProject.Areas.Single().Authoring.UserEvents);
        var areaBinding = Assert.Single(restoredArea.Authoring.UserEvents);

        Assert.Equal(AreaUserEvent.CurrentSchemaVersion, projectBinding.SchemaVersion);
        Assert.Equal(binding.PayloadFields.OrderBy(item => item.Key, StringComparer.Ordinal),
            projectBinding.PayloadFields.OrderBy(item => item.Key, StringComparer.Ordinal));
        Assert.Equal(binding.Subscriptions, areaBinding.Subscriptions);
        Assert.Equal(binding.ForgeScriptApiVersion, areaBinding.ForgeScriptApiVersion);
    }

    [Fact]
    public void Ux2LwCPreEventSchemaAreaRemainsReadableAsLegacyAndRequiresExplicitUpgrade()
    {
        var source = new AreaDefinition(AreaId.From(Guid.Parse("a0240000-0000-4000-8000-000000000020")),
            "legacy-events", "Legacy events", []);
        var authoring = source.Authoring;
        var legacy = new AreaUserEvent(Guid.Parse("a0240000-0000-4000-8000-000000000021"),
            "area.legacy-events.alarm", "schema://area/alarm/v1", "scripts/alarm.forge", 1,
            ["main"], false, "migration:1.9");
        var area = source.WithAuthoring(new(authoring.SchemaVersion, [legacy], authoring.Map,
            authoring.Layers, authoring.Experience, authoring.RequireLayerRuntimeMetadata));
        var json = AreaJsonSerializer.Serialize(area)
            .Replace(AreaJsonSerializer.CurrentFormatVersion,
                AreaJsonSerializer.PreAreaUserEventFormatVersion, StringComparison.Ordinal);

        var restored = AreaJsonSerializer.Deserialize(json);
        var restoredBinding = Assert.Single(restored.Authoring.UserEvents);

        Assert.Equal(AreaUserEvent.LegacySchemaVersion, restoredBinding.SchemaVersion);
        Assert.Empty(restoredBinding.PayloadFields);
        Assert.Contains(AreaUserEventValidator.ValidateExecutable(restored),
            item => item.Code == AreaUserEventValidator.SchemaUpgradeRequiredCode);
    }
}
