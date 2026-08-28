using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.WorkspaceMigrator;

internal static class WorldGenerationVerticalSliceMigration
{
    private const string Provenance = "module-author:world-generation-vertical-slice-v1";

    internal static ProjectDefinition Create(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var areaId = AreaId.From(Stable(project.Module.Id.Value, "world-generation:area"));
        var existingGeneratedArea = project.Areas.SingleOrDefault(area => area.Id == areaId);

        var entryArea = project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
        var playerBlueprint = project.Blueprints.SingleOrDefault(blueprint =>
                                  blueprint.Kind == BlueprintKind.PlayerSpawn) ??
                              throw new InvalidOperationException(
                                  "A world-generation vertical slice requires exactly one player-spawn blueprint.");
        var entryPlayer = entryArea.Entities.Single(entity => entity.BlueprintId == playerBlueprint.Id);
        var portalBlueprintId = BlueprintId.From(Stable(project.Module.Id.Value, "world-generation:portal-blueprint"));
        var portalBlueprint = project.Blueprints.SingleOrDefault(blueprint => blueprint.Id == portalBlueprintId) ??
                              new BlueprintDefinition(
                                  portalBlueprintId,
                                  "world-generation-portal",
                                  "Passage vers un monde généré",
                                  BlueprintKind.Trigger);

        var entryPortalEntityId = EntityId.From(Stable(project.Module.Id.Value, "world-generation:entry-portal"));
        var generatedPlayerEntityId = EntityId.From(Stable(project.Module.Id.Value, "world-generation:player"));
        var returnPortalEntityId = EntityId.From(Stable(project.Module.Id.Value, "world-generation:return-portal"));
        var entryPortal = new EntityInstance(
            entryPortalEntityId,
            portalBlueprint.Id,
            At(entryPlayer.Transform.Position.X + 4, entryPlayer.Transform.Position.Y,
                entryPlayer.Transform.Position.Z),
            ["portal", "vertical-slice", "world-generation"],
            DisplayName("Passage vers les Hautes Brumes"));
        var generatedPlayer = new EntityInstance(
            generatedPlayerEntityId,
            playerBlueprint.Id,
            At(64, 180, 64),
            ["player", "vertical-slice", "world-generation"]);
        var returnPortal = new EntityInstance(
            returnPortalEntityId,
            portalBlueprint.Id,
            At(70, 180, 64),
            ["portal", "vertical-slice", "world-generation"],
            DisplayName($"Retour vers {entryArea.DisplayName}"));

        var entryLink = new PortalLink(
            PortalId.From(Stable(project.Module.Id.Value, "world-generation:entry-link")),
            "Vers les Hautes Brumes",
            entryPortalEntityId,
            areaId,
            generatedPlayerEntityId,
            bidirectional: false,
            preloadDestination: true,
            provenance: Provenance);
        var returnLink = new PortalLink(
            PortalId.From(Stable(project.Module.Id.Value, "world-generation:return-link")),
            $"Retour vers {entryArea.DisplayName}",
            returnPortalEntityId,
            entryArea.Id,
            entryPlayer.Id,
            bidirectional: false,
            preloadDestination: true,
            provenance: Provenance);

        var dimensions = new AreaDimensions(2048, 2048);
        var baseWorld = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, dimensions);
        var terrain = new TerrainDefinition(
            baseWorld.Terrain.Id,
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.None,
            dimensions.Width,
            dimensions.Depth,
            2,
            2,
            2,
            2026,
            0,
            [0, 0, 0, 0],
            [],
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            Provenance);
        var world = new AreaWorldProfile(
            baseWorld.Id,
            baseWorld.SchemaVersion,
            terrain,
            [],
            [],
            baseWorld.VolumetricNavigation,
            [],
            existingGeneratedArea?.World.Loading ?? baseWorld.Loading,
            Provenance);
        var baseEnvironment = AreaEnvironmentProfile.CreateDefault(areaId, AreaKind.Exterior);
        var environment = new AreaEnvironmentProfile(
            baseEnvironment.SchemaVersion,
            baseEnvironment.Classification,
            ["generated-terrain", "vertical-slice", "world-generation:canonical"],
            baseEnvironment.Lighting,
            baseEnvironment.Fog,
            baseEnvironment.Audio,
            baseEnvironment.Rendering,
            baseEnvironment.Sky,
            baseEnvironment.Wind,
            baseEnvironment.Weather,
            baseEnvironment.Water,
            baseEnvironment.Reverb,
            baseEnvironment.Authoring,
            baseEnvironment.RequireAuthoringRuntimeMetadata);
        var defaultNavigation = AreaNavigationDefinition.CreateDefault(areaId);
        var navigation = new AreaNavigationDefinition(
            defaultNavigation.Id,
            4,
            defaultNavigation.AgentProfiles,
            defaultNavigation.OffMeshLinks,
            Provenance,
            anchors: defaultNavigation.Anchors,
            waypoints: defaultNavigation.Waypoints);
        var generatedArea = new AreaDefinition(
            areaId,
            "les-hautes-brumes-generees",
            "Les Hautes Brumes générées",
            [generatedPlayer, returnPortal],
            AreaKind.Exterior,
            dimensions,
            [returnLink],
            navigation: navigation,
            environment: environment,
            world: world);
        var updatedEntry = entryArea
            .WithEntities(entryArea.Entities.Any(entity => entity.Id == entryPortal.Id)
                ? entryArea.Entities
                : [.. entryArea.Entities, entryPortal])
            .WithPortals(entryArea.Portals.Any(portal => portal.Id == entryLink.Id)
                ? entryArea.Portals
                : [.. entryArea.Portals, entryLink]);
        var areas = project.Areas
            .Where(area => area.Id != areaId)
            .Select(area => area.Id == updatedEntry.Id ? updatedEntry : area)
            .Append(generatedArea)
            .ToArray();
        var updated = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints.Any(blueprint => blueprint.Id == portalBlueprint.Id)
                ? project.Blueprints
                : [.. project.Blueprints, portalBlueprint],
            areas,
            project.Dialogues,
            project.Quests);
        var diagnostics = ProjectValidator.Validate(updated);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException(
                $"World-generation vertical slice is invalid: {diagnostics[0].Code} " +
                $"({diagnostics[0].Path}): {diagnostics[0].Message}");
        return updated;
    }

    private static TransformValue At(float x, float y, float z) =>
        new(new Vector3Value(x, y, z), QuaternionValue.Identity, Vector3Value.One);

    private static EntityPropertyOverrides DisplayName(string value) =>
        new(EntityPropertyOverrides.CurrentSchemaVersion,
            [new EntityPropertyOverride("runtime.displayName", EntityPropertyValueKind.String, value)]);

    private static Guid Stable(Guid moduleId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:{moduleId:D}:{discriminator}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
