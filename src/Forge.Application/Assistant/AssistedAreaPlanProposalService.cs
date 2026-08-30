using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public static class AssistedAreaPlanProposalService
{
    public const string PromptKey = "area-plan";
    public const string PromptVersion = "3.0.0";
    private const int MaximumContextEntities = 16;
    private const int MaximumLoreCharacters = 2_000;
    private const string ResponseJsonSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["summary", "nodes", "links"],
          "properties": {
            "summary": { "type": "string", "minLength": 1, "maxLength": 500 },
            "nodes": {
              "type": "array",
              "minItems": 4,
              "maxItems": 24,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["key", "kind", "label", "x", "z", "width", "depth", "contentBlueprintIds", "tags"],
                "properties": {
                  "key": { "type": "string", "pattern": "^[a-z0-9][a-z0-9._-]{0,63}$" },
                  "kind": { "type": "string", "enum": ["Entry", "Exploration", "Encounter", "Social", "Service", "Objective", "Rest", "Exit"] },
                  "label": { "type": "string", "minLength": 1, "maxLength": 120 },
                  "x": { "type": "number" },
                  "z": { "type": "number" },
                  "width": { "type": "number", "minimum": 2, "maximum": 512 },
                  "depth": { "type": "number", "minimum": 2, "maximum": 512 },
                  "contentBlueprintIds": {
                    "type": "array",
                    "maxItems": 64,
                    "items": { "type": "string", "format": "uuid" }
                  },
                  "tags": {
                    "type": "array",
                    "maxItems": 24,
                    "items": { "type": "string", "pattern": "^[a-z0-9][a-z0-9._:-]{1,127}$" }
                  }
                }
              }
            },
            "links": {
              "type": "array",
              "minItems": 1,
              "maxItems": 192,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["from", "to", "traversal", "required"],
                "properties": {
                  "from": { "type": "string", "pattern": "^[a-z0-9][a-z0-9._-]{0,63}$" },
                  "to": { "type": "string", "pattern": "^[a-z0-9][a-z0-9._-]{0,63}$" },
                  "traversal": { "type": "string", "pattern": "^[a-z0-9][a-z0-9._:-]{1,127}$" },
                  "required": { "type": "boolean" }
                }
              }
            }
          }
        }
        """;

    public static async Task<AssistedAreaPlanProposal> ProposeAsync(
        AssistedAreaPlanBrief brief,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(lore);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(providerDefinition);

        var lorePacket = LoreContextAssembler.Assemble(lore, project, string.Join(' ', brief.Objectives), 8);
        var loreContent = lorePacket.Content.Length <= MaximumLoreCharacters
            ? lorePacket.Content
            : lorePacket.Content[..MaximumLoreCharacters];
        var targetArea = project.Areas.SingleOrDefault(area => area.Id.Value == brief.AreaId);
        var existingEntities = projectContentAuthorized && targetArea is not null
            ? targetArea.Entities.Take(MaximumContextEntities).Select(entity => (object)new
            {
                id = entity.Id.Value.ToString("D"),
                blueprintId = entity.BlueprintId.Value.ToString("D"),
                position = new
                {
                    entity.Transform.Position.X,
                    entity.Transform.Position.Y,
                    entity.Transform.Position.Z,
                },
                scale = new
                {
                    entity.Transform.Scale.X,
                    entity.Transform.Scale.Y,
                    entity.Transform.Scale.Z,
                },
                tags = entity.Tags,
            }).ToArray()
            : Array.Empty<object>();
        var availableKits = brief.KitBlueprintIds.Select(id =>
        {
            var blueprint = project.Blueprints.SingleOrDefault(item => item.Id.Value == id);
            return new
            {
                id = id.ToString("D"),
                available = blueprint is not null,
                kind = blueprint?.Kind.ToString(),
                slug = projectContentAuthorized ? blueprint?.Slug : null,
                displayName = projectContentAuthorized ? blueprint?.DisplayName : null,
            };
        }).ToArray();
        object? areaContext = targetArea is null
            ? null
            : new
            {
                id = targetArea.Id.Value.ToString("D"),
                displayName = projectContentAuthorized ? targetArea.DisplayName : null,
                slug = projectContentAuthorized ? targetArea.Slug : null,
                kind = targetArea.Kind.ToString(),
                dimensions = new { targetArea.Dimensions.Width, targetArea.Dimensions.Depth },
                existingEntityCount = targetArea.Entities.Count,
                existingEntitiesTruncated = targetArea.Entities.Count > existingEntities.Length,
                existingEntities,
                portalCount = targetArea.Portals.Count,
                terrain = new
                {
                    provider = targetArea.World.Terrain.Provider.ToString(),
                    targetArea.World.Terrain.Width,
                    targetArea.World.Terrain.Depth,
                    targetArea.World.Terrain.HeightScale,
                    promotedPlacementSurface = targetArea.World.PlacementSurface is not null,
                    environmentOccupancyCells = targetArea.World.EnvironmentOccupancy?.Cells.Count ?? 0,
                },
                navigation = new
                {
                    targetArea.Navigation.CellSize,
                    agents = targetArea.Navigation.AgentProfiles.Select(agent => new
                    {
                        agent.Name,
                        agent.Radius,
                        agent.Height,
                        agent.CanOpenDoors,
                        agent.CanUseOffMeshLinks,
                        agent.CanFly,
                    }).ToArray(),
                    offMeshLinkCount = targetArea.Navigation.OffMeshLinks.Count,
                },
            };
        var system = $"""
            You propose a bounded OpenRPG Forge area topology. Return only one JSON object matching the response schema.
            Never emit geometry mutations, executable code, file paths or invented canonical identifiers.
            The following versioned map-authoring contract is authoritative.
            <map-authoring-contract version="{MapAuthoringContract.Version}" sha256="{MapAuthoringContract.ContentSha256}">
            {MapAuthoringContract.Content}
            </map-authoring-contract>
            """;
        var user = JsonSerializer.Serialize(new
        {
            contract = new
            {
                version = MapAuthoringContract.Version,
                sha256 = MapAuthoringContract.ContentSha256,
            },
            brief = new
            {
                planId = brief.PlanId.ToString("D"),
                areaId = brief.AreaId.ToString("D"),
                brief.Title,
                archetype = brief.Archetype.ToString(),
                brief.Seed,
                brief.NodeCount,
                brief.WidthMeters,
                brief.DepthMeters,
                kitBlueprintIds = brief.KitBlueprintIds.Select(id => id.ToString("D")).ToArray(),
                constraints = brief.Constraints,
                objectives = brief.Objectives,
                loreSourceIds = brief.LoreSourceIds,
                environmentIntent = brief.EnvironmentIntent ?? AssistedAreaEnvironmentIntent.Default,
            },
            targetArea = areaContext,
            availableKits,
            lore = projectContentAuthorized
                ? new
                {
                    content = loreContent,
                    lorePacket.ContentSha256,
                    SourceIds = lorePacket.SourceIds.ToArray(),
                    lorePacket.EntryCount,
                    truncated = lorePacket.Truncated || lorePacket.Content.Length > MaximumLoreCharacters,
                }
                : new
                {
                    content = "[project content withheld]",
                    lorePacket.ContentSha256,
                    SourceIds = Array.Empty<string>(),
                    EntryCount = 0,
                    truncated = lorePacket.EntryCount > 0,
                },
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', PromptKey, PromptVersion, system, user, ResponseJsonSchema))))
            .ToLowerInvariant();
        var prompt = new AssistantPromptRenderResult(
            PromptKey,
            PromptVersion,
            system,
            user,
            ResponseJsonSchema,
            hash);
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.PlanId,
            Guid.NewGuid(),
            prompt,
            AssistantPermissionLevel.Propose,
            Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized,
            projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
            ? AssistedAreaPlanGenerator.Generate(
                brief,
                project,
                response.ProviderKey,
                response.EstimatedCost,
                response.Provenance,
                hash,
                response.InputTokens,
                response.OutputTokens,
                MapAuthoringContract.ContentSha256)
            : AssistedAreaPlanProviderResponseGenerator.Generate(
                brief,
                project,
                response,
                hash,
                MapAuthoringContract.ContentSha256);
    }
}
