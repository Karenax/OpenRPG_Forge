using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.Mcp.Navigation;

internal static class WaypointJsonBoundaryMapper
{
    public const string InvalidJsonCode = "waypoint.json.invalid";

    public static bool TryMap(
        ProjectDefinition project,
        WaypointAuthoringPlanRequest request,
        out WaypointAuthoringDomainPlanRequest? mapped,
        out AgentDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        mapped = null;
        diagnostic = null;
        try
        {
            if (!Guid.TryParse(request.AreaId, out var requestedAreaId) || requestedAreaId == Guid.Empty)
                throw new JsonException("Area ID is invalid.");
            if (request.Profile.ValueKind != JsonValueKind.Object)
                throw new JsonException("Waypoint profile JSON must be an object.");

            var candidate = NormalizeValueObjects(JsonNode.Parse(request.Profile.GetRawText())
                ?? throw new JsonException("Waypoint profile JSON is empty."));
            var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(project))?.AsObject()
                ?? throw new JsonException("Canonical project JSON is empty.");
            var areas = document["project"]?["areas"]?.AsArray()
                ?? throw new JsonException("Canonical project JSON has no area collection.");
            var area = areas.Select(node => node?.AsObject()).SingleOrDefault(node =>
                string.Equals(node?["id"]?.GetValue<string>(), request.AreaId,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new JsonException("Area does not exist in the canonical project.");
            var navigation = area["navigation"]?.AsObject()
                ?? throw new JsonException("Canonical area JSON has no navigation object.");
            navigation["waypoints"] = candidate;

            var projected = ProjectJsonSerializer.Deserialize(document.ToJsonString());
            var projectedArea = projected.Areas.Single(item => item.Id.Value == requestedAreaId);
            if (projectedArea.Navigation.Waypoints.AreaId != projectedArea.Id)
                throw new JsonException("Waypoint profile area does not match the selected area.");
            mapped = new WaypointAuthoringDomainPlanRequest(
                request.SchemaVersion,
                request.BaseRevision,
                request.AreaId,
                projectedArea.Navigation.Waypoints);
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            diagnostic = new AgentDiagnostic(
                InvalidJsonCode,
                "Waypoint JSON does not conform to the canonical waypoint schema v1.",
                "request/profile");
            return false;
        }
    }

    private static JsonNode NormalizeValueObjects(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            if (objectNode.Count == 1 && objectNode["value"] is JsonValue valueNode &&
                valueNode.TryGetValue<string>(out var value) && Guid.TryParse(value, out _))
                return JsonValue.Create(value)!;
            foreach (var property in objectNode.ToArray())
            {
                if (property.Value is null)
                    continue;
                var normalized = NormalizeValueObjects(property.Value);
                if (!ReferenceEquals(normalized, property.Value))
                    objectNode[property.Key] = normalized;
            }
            return objectNode;
        }
        if (node is JsonArray arrayNode)
        {
            for (var index = 0; index < arrayNode.Count; index++)
            {
                if (arrayNode[index] is null)
                    continue;
                var original = arrayNode[index]!;
                var normalized = NormalizeValueObjects(original);
                if (!ReferenceEquals(normalized, original))
                    arrayNode[index] = normalized;
            }
        }
        return node;
    }
}
