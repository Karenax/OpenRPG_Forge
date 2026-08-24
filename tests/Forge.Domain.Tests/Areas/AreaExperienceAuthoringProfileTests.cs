using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaExperienceAuthoringProfileTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a0550000-0000-4000-8000-000000000010"));

    [Fact]
    public void R055AudioEventsMapVisibilityAndLodValidateAsOneRuntimeContract()
    {
        var area = Area();

        var diagnostics = AreaExperienceAuthoringValidator.Validate(area);
        var runtime = AreaExperienceRuntimeResolver.Resolve(area, required: true);

        Assert.Empty(diagnostics);
        Assert.Equal(1, runtime.Handle!.AudioSnapshots);
        Assert.Equal(1, runtime.Handle.UserEvents);
        Assert.Equal(1, runtime.Handle.MapPins);
        Assert.Equal(2, runtime.Handle.VisibilityRooms);
        Assert.Equal(1, runtime.Handle.VisibilityPortals);
        Assert.Equal(1, runtime.Handle.Occluders);
        Assert.Equal(1, runtime.Handle.LodOverrides);
        Assert.True(runtime.Handle.FogOfWar);
    }

    [Fact]
    public void R055InvalidProfilesProduceStableLocalizedDiagnostics()
    {
        var source = Area();
        var invalid = new AreaExperienceAuthoringProfile(Guid.Empty, 99, string.Empty,
            [new(Guid.Empty, string.Empty, new Dictionary<string, float> { [string.Empty] = 9 }, -1,
                string.Empty, string.Empty)],
            new([], [], [], true, true, true, 0, 0),
            new(AreaLodSourceMode.Generated, [20, 10], 0, 0, 0, 2, true, true,
                [new(Guid.Empty, string.Empty, AreaLodSourceMode.Generated, [], [], -1, string.Empty)]), true);
        var map = new AreaMapProfile(true, true, "orthographic", 1, [], Guid.Empty, 99, string.Empty,
            false, false, AreaMapExplorationScope.Party, 0, string.Empty);
        var authoring = new AreaAuthoringProfile(1, [], map, [], invalid);
        var area = source.WithAuthoring(authoring);

        var diagnostics = AreaExperienceAuthoringValidator.Validate(area);

        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.IdentityCode);
        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.SchemaCode);
        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.AudioCode);
        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.MapCode);
        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.VisibilityCode);
        Assert.Contains(diagnostics, item => item.Code == AreaExperienceAuthoringValidator.LodCode);
    }

    [Fact]
    public void R055LegacyExperienceIsOptionalButRequiredAbsenceIsDiagnosed()
    {
        var legacy = new AreaDefinition(AreaId, "legacy", "Legacy", []);

        var optional = AreaExperienceRuntimeResolver.Resolve(legacy, required: false);
        var required = AreaExperienceRuntimeResolver.Resolve(legacy, required: true);

        Assert.Null(optional.Handle);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(AreaExperienceRuntimeResolver.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
    }

    internal static AreaDefinition Area()
    {
        var roomA = new AreaVisibilityRoomProfile(Guid.Parse("a0550000-0000-4000-8000-000000000011"),
            "Crypt", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var roomB = new AreaVisibilityRoomProfile(Guid.Parse("a0550000-0000-4000-8000-000000000012"),
            "Shrine", new(new(16, 0, 0), QuaternionValue.Identity, new(12, 8, 12)), false, 96,
            new Dictionary<string, string>());
        var visibility = new AreaVisibilityAuthoringProfile([roomA, roomB],
            [new(Guid.Parse("a0550000-0000-4000-8000-000000000013"), "Shrine arch", roomA.Id, roomB.Id,
                TransformValue.Identity, AreaVisibilityPortalMode.Conditional, "quest.ashes.entered", true)],
            [new(Guid.Parse("a0550000-0000-4000-8000-000000000014"), "Crypt wall",
                TransformValue.Identity, false, "asset:crypt-wall", new Dictionary<string, string>())],
            true, true, true, 16, 128);
        var lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96,
            24, .15f, true, true,
            [new(Guid.Parse("a0550000-0000-4000-8000-000000000015"), "asset:crypt-column",
                AreaLodSourceMode.Generated, [1, .5f, .2f], [12000, 6000, 2400], .2f, "test:r055")]);
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0550000-0000-4000-8000-000000000016"), 1, "test:r055",
            [new(Guid.Parse("a0550000-0000-4000-8000-000000000017"), "Crypt combat",
                new Dictionary<string, float> { ["music"] = .8f }, .75f, "area.r055.combat", "test:r055")],
            visibility, lod, true);
        var eventName = "area.r055.alarm";
        var authoring = new AreaAuthoringProfile(1,
            [new(Guid.Parse("a0550000-0000-4000-8000-000000000018"), eventName,
                "schema://area/alarm/v1", "scripts/alarm.forge", 1, ["OnAreaAlarm"], true, "test:r055",
                AreaUserEvent.CurrentSchemaVersion,
                new Dictionary<string, string> { ["level"] = "int", ["source"] = "string" },
                AreaUserEvent.CurrentForgeScriptApiVersion)],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0550000-0000-4000-8000-000000000019"), "Shrine", 16, 0, 0, false)],
                Guid.Parse("a0550000-0000-4000-8000-000000000020"), 1, "test:r055", true, true,
                AreaMapExplorationScope.Party, 8, "area.map.reveal"), [], experience);
        return new(AreaId, "r055", "R055", [], authoring: authoring);
    }

    [Fact]
    public void Ux2LwCTypedAreaEventsValidateSchemaNamespacePayloadAndSubscriptions()
    {
        var source = Area();
        var binding = Assert.Single(source.Authoring.UserEvents);

        Assert.Empty(AreaUserEventValidator.ValidateExecutable(source));
        Assert.Equal(AreaUserEvent.CurrentSchemaVersion, binding.SchemaVersion);
        Assert.Equal("int", binding.PayloadFields["level"]);

        var invalid = new AreaUserEvent(binding.Id, "area.other.alarm", "https://area/alarm",
            "../alarm.forge", 0, ["bad handler"], true, string.Empty, 99,
            new Dictionary<string, string> { ["Level!"] = "decimal" }, "9.0");
        var invalidArea = source.WithAuthoring(new(source.Authoring.SchemaVersion, [invalid],
            source.Authoring.Map, source.Authoring.Layers, source.Authoring.Experience,
            source.Authoring.RequireLayerRuntimeMetadata));
        var diagnostics = AreaUserEventValidator.ValidateExecutable(invalidArea);

        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.SchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.NameInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.PayloadSchemaInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.ScriptInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AreaUserEventValidator.ApiUnsupportedCode);
    }

    [Fact]
    public void Ux2LwCLegacyAreaEventRemainsReadableButCannotBeDispatchedLive()
    {
        var source = Area();
        var current = Assert.Single(source.Authoring.UserEvents);
        var legacy = new AreaUserEvent(current.Id, current.NamespacedName, current.PayloadSchema,
            current.ScriptPath, current.ApiVersion, ["main"], false, "migration:1.9");
        var area = source.WithAuthoring(new(source.Authoring.SchemaVersion, [legacy], source.Authoring.Map,
            source.Authoring.Layers, source.Authoring.Experience, source.Authoring.RequireLayerRuntimeMetadata));

        Assert.Empty(AreaUserEventValidator.Validate(area));
        Assert.Contains(AreaUserEventValidator.ValidateExecutable(area),
            item => item.Code == AreaUserEventValidator.SchemaUpgradeRequiredCode);
    }
}
