using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;
using RpgVector3 = Forge.RpgKit.Domain.Placeables.Vector3Value;

namespace Forge.RpgKit.Application.Doors;

public sealed record DoorWorldValidationResult(
    bool Available,
    bool OptionalFallback,
    IReadOnlyList<DoorDiagnostic> Diagnostics);

public static class DoorWorldValidationService
{
    public static DoorWorldValidationResult Validate(ProjectDefinition project, Guid doorProfileId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var path = $"module/worldFeatures/doors/{doorProfileId}";
        if (doorProfileId == Guid.Empty)
            return Failure("world.door.validation_profile_id_invalid",
                "Door validation requires a non-empty profile ID.", path);

        var profile = project.Module.WorldFeatures.Doors.SingleOrDefault(item => item.Id == doorProfileId);
        if (profile is null)
            return Failure("world.door.validation_profile_missing",
                "The selected door profile does not exist.", path);

        var diagnostics = WorldFeatureCatalogValidator.Validate(project)
            .Where(item => item.Path.StartsWith(path, StringComparison.Ordinal))
            .Select(item => new DoorDiagnostic(item.Code, item.Path, item.Message))
            .ToList();
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == profile.BlueprintId);
        if (blueprint is null || blueprint.Kind != BlueprintKind.Door)
            AddOnce(diagnostics, "world.door.validation_blueprint_missing", $"{path}/blueprintId",
                "Door validation requires its canonical door blueprint.");
        if (profile.Foundation is null)
            AddOnce(diagnostics, "world.door.validation_foundation_missing", $"{path}/foundation",
                "Door validation requires an authored foundation.");
        else if (profile.Foundation.Navigation is null)
            AddOnce(diagnostics, "world.door.validation_navigation_missing", $"{path}/foundation/navigation",
                "Door validation requires an authored navigation policy.");

        if (profile.Validation is null)
        {
            if (profile.ValidationRequiredAtRuntime)
                AddOnce(diagnostics, "world.door.validation_required_missing", $"{path}/validation",
                    "Required door validation context is missing.");
            return new(false, !profile.ValidationRequiredAtRuntime, diagnostics);
        }

        if (diagnostics.Count > 0 || blueprint is null || profile.Foundation?.Navigation is null)
            return new(false, false, diagnostics);

        var policy = profile.Validation!;
        var foundation = profile.Foundation!;
        var access = profile.AccessEvents;
        var definition = MapDefinition(profile, blueprint.Slug, blueprint.DisplayName);
        var navigation = foundation.Navigation!;
        var scripts = profile.Events.Select(item => item.ScriptId)
            .Concat(access is null ? Enumerable.Empty<string>() : new[] { access.Rejection.ScriptId })
            .ToArray();
        var runtime = new DoorRuntimeProfile(
            new(navigation.ObstacleId, navigation.LinkId, navigation.VisualizeInEditor,
                navigation.BlockingStateIds.ToHashSet(StringComparer.Ordinal),
                navigation.TraversableStateIds.ToHashSet(StringComparer.Ordinal)),
            access is null ? null : DoorAccessEventFoundationService.CreateLock(profile),
            access is null ? null : DoorAccessEventFoundationService.CreateTrap(profile),
            scripts,
            policy.KnownDestinationIds.ToHashSet(),
            policy.KnownAnimationIds.ToHashSet(StringComparer.Ordinal),
            policy.KnownCollisionProfileIds.ToHashSet(StringComparer.Ordinal),
            policy.KnownCredentialIds.ToHashSet(StringComparer.Ordinal),
            policy.KnownScriptIds.ToHashSet(StringComparer.Ordinal));
        var links = project.Module.WorldFeatures.Doors
            .SelectMany(item => item.Foundation?.Links.Select(link =>
                new DoorLink(item.Id, link.TargetId, link.OneWay, link.ReturnNavigationLabel)) ??
                Enumerable.Empty<DoorLink>())
            .ToArray();
        diagnostics.AddRange(DoorRuntimeService.Validate(definition, runtime, links)
            .Select(item => item with { Path = CanonicalPath(item.Path) }));
        return new(diagnostics.Count == 0, false, diagnostics);
    }

    internal static DoorDefinition MapDefinition(DoorWorldProfile profile, string slug, string displayName)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var foundation = profile.Foundation ?? throw new InvalidOperationException("WORLD_DOOR_FOUNDATION_MISSING");
        var transform = profile.AccessEvents?.Transform ?? TransformValue.Identity;
        var adapter = foundation.Adapter is null
            ? null
            : new DoorKitAdapter(foundation.Adapter.KitId, foundation.Adapter.SocketKind,
                foundation.Adapter.MaterialBindings,
                new(foundation.Adapter.WidthOffset, foundation.Adapter.HeightOffset, foundation.Adapter.DepthOffset));
        var states = foundation.States.Select(item => new DoorStateDefinition(item.Id, (DoorState)item.Kind,
            item.AnimationId, item.CollisionEnabled, item.InteractionEnabled)).ToArray();
        var transitions = foundation.Transitions.Select(item => new DoorTransition(item.FromStateId, item.ToStateId,
            item.Trigger, TimeSpan.FromSeconds(item.DurationSeconds), item.Condition ?? string.Empty)).ToArray();
        var interaction = foundation.Interaction;
        var portal = foundation.Portal is null
            ? null
            : new PortalComponent((PortalDestinationKind)foundation.Portal.DestinationKind,
                foundation.Portal.DestinationId, foundation.Portal.SpawnAnchorId,
                new(0, foundation.Portal.ExitYawDegrees, 0), foundation.Portal.Preload,
                foundation.Portal.Permissions.ToHashSet(StringComparer.Ordinal));
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, slug, displayName,
            (DoorPlacementMode)foundation.Placement, foundation.ArchitectureSocketId, adapter,
            new(new(transform.Position.X, transform.Position.Y, transform.Position.Z), new RpgVector3(0, 0, 0),
                new(transform.Scale.X, transform.Scale.Y, transform.Scale.Z), new RpgVector3(0, 0, 0), 0, null,
                false, null),
            new(foundation.InitialStateId, states, transitions),
            new(interaction.Range, interaction.AllowedSide,
                new(interaction.ClearanceWidth, interaction.ClearanceHeight, interaction.ClearanceDepth),
                interaction.Authority, interaction.QueueCapacity, interaction.FailureFeedbackKey), portal);
    }

    private static DoorWorldValidationResult Failure(string code, string message, string path) =>
        new(false, false, [new DoorDiagnostic(code, path, message)]);

    private static void AddOnce(List<DoorDiagnostic> diagnostics, string code, string path, string message)
    {
        if (diagnostics.All(item => !string.Equals(item.Code, code, StringComparison.Ordinal)))
            diagnostics.Add(new(code, path, message));
    }

    private static string CanonicalPath(string path) =>
        path.StartsWith("doors/", StringComparison.Ordinal) ? $"module/worldFeatures/{path}" : path;
}
