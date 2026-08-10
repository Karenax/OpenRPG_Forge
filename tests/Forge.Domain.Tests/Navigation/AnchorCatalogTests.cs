using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Navigation;

public sealed class AnchorCatalogTests
{
    [Fact]
    public void Valid_catalog_supports_named_spawn_and_cross_area_destination()
    {
        var data = CreateProject();

        var diagnostics = data.Project.Areas.SelectMany(area =>
            AnchorCatalogValidator.Validate(data.Project, area)).ToArray();

        Assert.Empty(diagnostics);
        var spawn = data.Project.Areas[0].Navigation.Anchors.Definitions.Single(item => item.Kind == AnchorKind.Spawn);
        Assert.Equal("anchor.spawn", spawn.Label.Key);
        Assert.Equal(AnchorScope.Area, spawn.Scope);
    }

    [Fact]
    public void Spawn_resolution_is_priority_ordered_and_uses_explicit_fallback()
    {
        var data = CreateProject();
        var area = data.Project.Areas[0];

        var direct = AnchorRuntimeService.ResolveSpawn(area, "player", "heroes");
        var fallback = AnchorRuntimeService.ResolveSpawn(area, "player", "heroes",
            new HashSet<Guid>([data.PrimarySpawnId]));

        Assert.Equal(data.PrimarySpawnId, direct.Instance.Id);
        Assert.False(direct.UsedFallback);
        Assert.Equal(data.FallbackSpawnId, fallback.Instance.Id);
        Assert.True(fallback.UsedFallback);
    }

    [Fact]
    public void Portal_resolution_applies_target_rotation_velocity_and_preload()
    {
        var data = CreateProject();

        var result = AnchorRuntimeService.ResolvePortal(data.Project, data.Project.Areas[0].Id,
            data.PortalId, new Vector3Value(2, 0.5f, 0), 0);

        Assert.Equal(data.Project.Areas[1].Id, result.TargetAreaId);
        Assert.Equal(data.TargetId, result.TargetAnchorInstanceId);
        Assert.InRange(result.ArrivalYawDegrees, 89.99f, 90.01f);
        Assert.InRange(result.ArrivalVelocity.X, 1.99f, 2.01f);
        Assert.Equal(0.5f, result.ArrivalVelocity.Y);
        Assert.True(result.Preload);
    }

    [Fact]
    public void Validation_reports_reference_bounds_clearance_fallback_and_required_failures()
    {
        var data = CreateProject();
        var source = data.Project.Areas[0];
        var primary = source.Navigation.Anchors.Instances.Single(item => item.Id == data.PrimarySpawnId);
        var broken = primary with
        {
            Transform = At(500, 0, 0, 0),
            Spawn = primary.Spawn! with
            {
                Clearance = new Vector3Value(0.1f, 0.1f, 0.1f),
                FallbackInstanceId = Guid.NewGuid(),
            },
        };
        var catalog = new AnchorCatalog(source.Navigation.Anchors.Id, 1, "test:r009", source.Id, true,
            source.Navigation.Anchors.Definitions,
            source.Navigation.Anchors.Instances.Select(item => item.Id == broken.Id ? broken : item).ToArray());
        var changed = source.WithNavigation(source.Navigation.WithAnchors(catalog));
        var project = new ProjectDefinition(data.Project.Id, data.Project.Title, data.Project.Module,
            data.Project.Blueprints, [changed, data.Project.Areas[1]]);

        var diagnostics = AnchorCatalogValidator.Validate(project, changed);

        Assert.Contains(diagnostics, item => item.Code == AnchorCatalogValidator.TransformBoundsCode);
        Assert.Contains(diagnostics, item => item.Code == AnchorCatalogValidator.SpawnNavigationCode);
        Assert.Contains(diagnostics, item => item.Code == AnchorCatalogValidator.SpawnFallbackCode);
        var requiredEmpty = changed.WithNavigation(changed.Navigation.WithAnchors(new AnchorCatalog(
            Guid.NewGuid(), 1, "test:r009", changed.Id, true, [], [])));
        Assert.Contains(AnchorCatalogValidator.Validate(project, requiredEmpty),
            item => item.Code == AnchorCatalogValidator.RequiredContentCode);
    }

    [Fact]
    public void Optional_empty_catalog_is_safe_and_missing_portal_destination_is_diagnostic()
    {
        var data = CreateProject();
        var source = data.Project.Areas[0];
        var optional = source.WithNavigation(source.Navigation.WithAnchors(AnchorCatalog.CreateDefault(source.Id)));
        var portal = source.Navigation.Anchors.Instances.Single(item => item.Id == data.PortalId);
        var brokenPortal = portal with { Portal = portal.Portal! with { TargetAnchorInstanceId = Guid.NewGuid() } };
        var brokenCatalog = new AnchorCatalog(source.Navigation.Anchors.Id, 1, "test:r009", source.Id, true,
            source.Navigation.Anchors.Definitions,
            source.Navigation.Anchors.Instances.Select(item => item.Id == portal.Id ? brokenPortal : item).ToArray());
        var brokenArea = source.WithNavigation(source.Navigation.WithAnchors(brokenCatalog));
        var brokenProject = new ProjectDefinition(data.Project.Id, data.Project.Title, data.Project.Module,
            data.Project.Blueprints, [brokenArea, data.Project.Areas[1]]);

        Assert.Empty(AnchorCatalogValidator.Validate(data.Project, optional));
        Assert.Contains(AnchorCatalogValidator.Validate(brokenProject, brokenArea),
            item => item.Code == AnchorCatalogValidator.PortalDestinationCode);
    }

    [Fact]
    public void Validation_reports_duplicate_definition_without_throwing()
    {
        var data = CreateProject();
        var source = data.Project.Areas[0];
        var first = source.Navigation.Anchors.Definitions[0];
        var duplicate = first with { Slug = $"{first.Slug}-duplicate" };
        var catalog = new AnchorCatalog(source.Navigation.Anchors.Id, 1, "test:r009", source.Id, true,
            [.. source.Navigation.Anchors.Definitions, duplicate], source.Navigation.Anchors.Instances);
        var changed = source.WithNavigation(source.Navigation.WithAnchors(catalog));
        var project = new ProjectDefinition(data.Project.Id, data.Project.Title, data.Project.Module,
            data.Project.Blueprints, [changed, data.Project.Areas[1]]);

        var diagnostics = AnchorCatalogValidator.Validate(project, changed);

        Assert.Contains(diagnostics, item => item.Code == AnchorCatalogValidator.DefinitionDuplicateCode);
    }

    private static AnchorTestData CreateProject()
    {
        var sourceId = AreaId.From(Guid.Parse("a9010000-0000-4000-8000-000000000001"));
        var targetAreaId = AreaId.From(Guid.Parse("a9010000-0000-4000-8000-000000000002"));
        var blueprintId = BlueprintId.From(Guid.Parse("a9010000-0000-4000-8000-000000000003"));
        var blueprint = new BlueprintDefinition(blueprintId, "player", "Player", BlueprintKind.PlayerSpawn);
        var spawnDefinition = Definition("a9010000-0000-4000-8000-000000000011", "spawn", "anchor.spawn",
            AnchorKind.Spawn, blueprintId);
        var portalDefinition = Definition("a9010000-0000-4000-8000-000000000012", "portal", "anchor.portal",
            AnchorKind.TransitionDestination);
        var targetDefinition = Definition("a9010000-0000-4000-8000-000000000013", "target", "anchor.target",
            AnchorKind.Waypoint);
        var primaryId = Guid.Parse("a9010000-0000-4000-8000-000000000021");
        var fallbackId = Guid.Parse("a9010000-0000-4000-8000-000000000022");
        var portalId = Guid.Parse("a9010000-0000-4000-8000-000000000023");
        var targetId = Guid.Parse("a9010000-0000-4000-8000-000000000024");
        var sourceCatalog = new AnchorCatalog(Guid.NewGuid(), 1, "test:r009", sourceId, true,
            [spawnDefinition, portalDefinition],
            [
                new(primaryId, 1, "test:r009", spawnDefinition.Id, At(0, 0, 0, 0), 1,
                    EntityPropertyOverrides.Empty,
                    new("player", new(1.2f, 2, 1.2f), 100, "heroes", 0, fallbackId, true), null),
                new(fallbackId, 1, "test:r009", spawnDefinition.Id, At(2, 0, 0, 0), 2,
                    EntityPropertyOverrides.Empty,
                    new("fallback", new(1.2f, 2, 1.2f), 10, "heroes", 0, null, true), null),
                new(portalId, 1, "test:r009", portalDefinition.Id, At(4, 0, 0, 0), 3,
                    EntityPropertyOverrides.Empty, null,
                    new(targetAreaId, targetId, PortalOrientationPolicy.AnchorRotation, 0,
                        PortalVelocityPolicy.RedirectForward, true)),
            ]);
        var targetCatalog = new AnchorCatalog(Guid.NewGuid(), 1, "test:r009", targetAreaId, false,
            [targetDefinition],
            [new(targetId, 1, "test:r009", targetDefinition.Id, At(0, 0, 0, 90), 1,
                EntityPropertyOverrides.Empty, null, null)]);
        var source = new AreaDefinition(sourceId, "source", "Source", [],
            navigation: AreaNavigationDefinition.CreateDefault(sourceId).WithAnchors(sourceCatalog));
        var target = new AreaDefinition(targetAreaId, "target", "Target", [],
            navigation: AreaNavigationDefinition.CreateDefault(targetAreaId).WithAnchors(targetCatalog));
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a9010000-0000-4000-8000-000000000030")),
            "Anchors", [blueprint], [source, target]);
        return new(project, primaryId, fallbackId, portalId, targetId);
    }

    private static AnchorDefinition Definition(string id, string slug, string key, AnchorKind kind,
        BlueprintId? blueprintId = null) => new(Guid.Parse(id), 1, "test:r009", slug,
        new LocalizedText(key, slug), kind, AnchorScope.Area,
        [slug], blueprintId, null, true);

    private static TransformValue At(float x, float y, float z, float yaw)
    {
        var half = yaw * MathF.PI / 360;
        return new(new(x, y, z), new(0, MathF.Sin(half), 0, MathF.Cos(half)), Vector3Value.One);
    }

    private sealed record AnchorTestData(ProjectDefinition Project, Guid PrimarySpawnId,
        Guid FallbackSpawnId, Guid PortalId, Guid TargetId);
}
