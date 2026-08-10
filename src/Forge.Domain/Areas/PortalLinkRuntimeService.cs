using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public sealed record PortalLinkTravelResolution(
    PortalId PortalId,
    AreaId SourceAreaId,
    AreaId DestinationAreaId,
    TransformValue DestinationTransform,
    float ArrivalYawDegrees,
    Vector3Value ArrivalVelocity,
    bool Reversed,
    bool PreloadDestination,
    string BeforeTransitionScript,
    string AfterTransitionScript,
    bool HooksRequiredAtRuntime);

public static class PortalLinkRuntimeService
{
    public static PortalLinkTravelResolution Resolve(
        ProjectDefinition project,
        AreaId currentAreaId,
        PortalId portalId,
        Vector3Value incomingVelocity,
        float incomingYawDegrees)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(currentAreaId);
        ArgumentNullException.ThrowIfNull(portalId);
        if (!float.IsFinite(incomingYawDegrees))
            throw new ArgumentOutOfRangeException(nameof(incomingYawDegrees));

        var currentArea = project.Areas.SingleOrDefault(item => item.Id == currentAreaId)
            ?? throw new InvalidOperationException("area.portal.source_area_missing");
        var direct = currentArea.Portals.SingleOrDefault(item => item.Id == portalId);
        var reverseOwner = direct is null
            ? project.Areas.SingleOrDefault(area => area.Portals.Any(item =>
                item.Id == portalId && item.Bidirectional && item.DestinationAreaId == currentAreaId))
            : null;
        var portal = direct ?? reverseOwner?.Portals.Single(item => item.Id == portalId)
            ?? throw new InvalidOperationException("area.portal.link_missing");
        var reversed = direct is null;
        var destinationArea = reversed
            ? reverseOwner!
            : project.Areas.Single(item => item.Id == portal.DestinationAreaId);
        var destinationEntityId = reversed ? portal.SourceEntityId : portal.DestinationEntityId;
        var destination = destinationArea.Entities.SingleOrDefault(item => item.Id == destinationEntityId)
            ?? throw new InvalidOperationException("area.portal.destination_entity_missing");
        var yaw = portal.Orientation switch
        {
            PortalOrientationPolicy.Preserve => incomingYawDegrees,
            PortalOrientationPolicy.FixedYaw => portal.FixedYawDegrees,
            PortalOrientationPolicy.AnchorRotation => QuaternionYaw(destination.Transform.Rotation),
            _ => throw new InvalidOperationException("area.portal.orientation_invalid"),
        };
        var velocity = portal.Velocity switch
        {
            PortalVelocityPolicy.Preserve => incomingVelocity,
            PortalVelocityPolicy.Reset => Vector3Value.Zero,
            PortalVelocityPolicy.RedirectForward => Redirect(incomingVelocity, yaw),
            _ => throw new InvalidOperationException("area.portal.velocity_invalid"),
        };
        return new(portal.Id, currentAreaId, destinationArea.Id, destination.Transform, yaw, velocity, reversed,
            portal.PreloadDestination, portal.BeforeTransitionScript, portal.AfterTransitionScript,
            portal.HooksRequiredAtRuntime);
    }

    private static Vector3Value Redirect(Vector3Value incoming, float yaw)
    {
        var speed = MathF.Sqrt((incoming.X * incoming.X) + (incoming.Z * incoming.Z));
        var radians = yaw * MathF.PI / 180f;
        return new(MathF.Sin(radians) * speed, incoming.Y, MathF.Cos(radians) * speed);
    }

    private static float QuaternionYaw(QuaternionValue value)
    {
        var siny = 2 * (value.W * value.Y + value.X * value.Z);
        var cosy = 1 - 2 * (value.Y * value.Y + value.Z * value.Z);
        return MathF.Atan2(siny, cosy) * 180 / MathF.PI;
    }
}

public static class PortalLinkValidator
{
    public const string SchemaCode = "area.portal.schema_unsupported";
    public const string IdentityCode = "area.portal.identity_invalid";
    public const string TransformCode = "area.portal.transform_invalid";
    public const string HookCode = "area.portal.hook_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area, PortalLink portal)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(portal);
        var root = $"areas/{area.Id}/portals/{portal.Id}";
        var diagnostics = new List<DomainDiagnostic>();
        if (portal.SchemaVersion != PortalLink.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaCode, "Portal schema version is unsupported.", $"{root}/schemaVersion"));
        if (string.IsNullOrWhiteSpace(portal.DisplayName) || string.IsNullOrWhiteSpace(portal.Provenance))
            diagnostics.Add(new(IdentityCode, "Portal name and provenance are required.", root));
        if (!float.IsFinite(portal.FixedYawDegrees))
            diagnostics.Add(new(TransformCode, "Portal fixed orientation must be finite.", $"{root}/fixedYawDegrees"));
        if (portal.HooksRequiredAtRuntime && string.IsNullOrWhiteSpace(portal.BeforeTransitionScript) &&
            string.IsNullOrWhiteSpace(portal.AfterTransitionScript) ||
            !SafeScript(portal.BeforeTransitionScript) || !SafeScript(portal.AfterTransitionScript))
            diagnostics.Add(new(HookCode,
                "Portal hooks must be confined relative .forge paths and required hooks cannot both be empty.",
                $"{root}/hooks"));
        return diagnostics;
    }

    private static bool SafeScript(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("scripts/", StringComparison.Ordinal) &&
               normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("../", StringComparison.Ordinal) && !Path.IsPathRooted(path);
    }
}
