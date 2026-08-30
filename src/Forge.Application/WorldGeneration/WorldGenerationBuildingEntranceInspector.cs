using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationBuildingEntranceDiagnostic(
    string Code,
    string Group,
    string Message);

public sealed record WorldGenerationBuildingEntranceInspection(
    int RequiredGroupCount,
    int ValidEntranceCount,
    double MinimumDoorHingeOffsetMeters,
    double MaximumDoorHingeOffsetMeters,
    double MaximumDoorFramePlaneErrorMeters,
    double MaximumDoorFrameYawErrorDegrees,
    double MaximumStairLateralErrorMeters,
    double MaximumStairYawErrorDegrees,
    IReadOnlyList<WorldGenerationBuildingEntranceDiagnostic> Diagnostics)
{
    public bool Passed => Diagnostics.Count == 0 && ValidEntranceCount == RequiredGroupCount;
}

public static class WorldGenerationBuildingEntranceInspector
{
    public const string RoleMissingCode = "world_generation.building_entrance.role_missing";
    public const string DoorInteractionMissingCode = "world_generation.building_entrance.door_interaction_missing";
    public const string DoorAlignmentInvalidCode = "world_generation.building_entrance.door_alignment_invalid";
    public const string StairAlignmentInvalidCode = "world_generation.building_entrance.stair_alignment_invalid";

    public const double MinimumDoorHingeOffsetMeters = .25;
    public const double MaximumDoorHingeOffsetMeters = 1;
    public const double MaximumDoorFramePlaneErrorMeters = .2;
    public const double MaximumDoorFrameYawErrorDegrees = 3;
    public const double MaximumStairYawErrorDegrees = 3;
    public const double MinimumStairForwardOffsetMeters = .45;
    public const double MaximumStairForwardOffsetMeters = 1.75;
    public const double MaximumStairLateralOffsetMeters = .1;
    public const double MaximumThresholdRiseMeters = 2;
    private const int MinimumHouseCompositionPieceCount = 6;

    public static WorldGenerationBuildingEntranceInspection Inspect(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var diagnostics = new List<WorldGenerationBuildingEntranceDiagnostic>();
        var minimumDoorHingeOffset = double.PositiveInfinity;
        var maximumDoorHingeOffset = 0d;
        var maximumDoorPlaneError = 0d;
        var maximumDoorYawError = 0d;
        var maximumStairLateralError = 0d;
        var maximumStairYawError = 0d;
        var valid = 0;
        var groups = area.Entities
            .Where(IsBuildingMember)
            .GroupBy(GroupKey)
            .Where(group => group.Key is not null && group.Count() >= MinimumHouseCompositionPieceCount)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        foreach (var group in groups)
        {
            var groupKey = group.Key!;
            var members = group.ToArray();
            var frames = members.Where(entity => HasTag(entity, "entrance") && HasTag(entity, "doorway")).ToArray();
            var doors = members.Where(entity => HasTag(entity, "entrance") && HasTag(entity, "interactive-door")).ToArray();
            var stairs = members.Where(entity => HasTag(entity, "entrance") && HasTag(entity, "entrance-stairs")).ToArray();
            if (frames.Length != 1 || doors.Length != 1 || stairs.Length != 1)
            {
                diagnostics.Add(new(RoleMissingCode, groupKey,
                    $"House group requires exactly one doorway, interactive door and entrance stair; found {frames.Length}/{doors.Length}/{stairs.Length}."));
                continue;
            }

            var frame = frames[0];
            var door = doors[0];
            var stair = stairs[0];
            var interaction = EntityClickTriggerSettings.Read(door.Overrides);
            if (!interaction.Enabled || interaction.Action != EntityClickTriggerAction.ToggleDoor ||
                HasTag(door, "open-door") || HasTag(door, "nonblocking"))
            {
                diagnostics.Add(new(DoorInteractionMissingCode, groupKey,
                    "The entrance leaf must start closed, block the opening and expose the independent toggle-door click action."));
            }

            var frameYaw = YawRadians(frame.Transform.Rotation);
            var doorOffset = LocalHorizontalOffset(frame.Transform.Position, door.Transform.Position, frameYaw);
            var doorHingeOffset = Math.Abs(doorOffset.X);
            var doorPlaneError = Math.Abs(doorOffset.Z);
            var doorYawError = YawDeltaDegrees(frame.Transform.Rotation, door.Transform.Rotation);
            var thresholdRise = door.Transform.Position.Y - frame.Transform.Position.Y;
            minimumDoorHingeOffset = Math.Min(minimumDoorHingeOffset, doorHingeOffset);
            maximumDoorHingeOffset = Math.Max(maximumDoorHingeOffset, doorHingeOffset);
            maximumDoorPlaneError = Math.Max(maximumDoorPlaneError, doorPlaneError);
            maximumDoorYawError = Math.Max(maximumDoorYawError, doorYawError);
            if (doorHingeOffset < MinimumDoorHingeOffsetMeters ||
                doorHingeOffset > MaximumDoorHingeOffsetMeters ||
                doorPlaneError > MaximumDoorFramePlaneErrorMeters ||
                doorYawError > MaximumDoorFrameYawErrorDegrees ||
                thresholdRise < 0 || thresholdRise > MaximumThresholdRiseMeters)
            {
                diagnostics.Add(new(DoorAlignmentInvalidCode, groupKey,
                    $"Door hinge/frame alignment is invalid (hinge={doorHingeOffset:0.###} m, plane={doorPlaneError:0.###} m, yaw={doorYawError:0.###} deg, thresholdRise={thresholdRise:0.###} m)."));
            }

            var stairOffset = LocalHorizontalOffset(frame.Transform.Position, stair.Transform.Position, frameYaw);
            var stairYawError = YawDeltaDegrees(frame.Transform.Rotation, stair.Transform.Rotation);
            var stairRise = door.Transform.Position.Y - stair.Transform.Position.Y;
            maximumStairLateralError = Math.Max(maximumStairLateralError, Math.Abs(stairOffset.X));
            maximumStairYawError = Math.Max(maximumStairYawError, stairYawError);
            if (Math.Abs(stairOffset.X) > MaximumStairLateralOffsetMeters ||
                stairOffset.Z < MinimumStairForwardOffsetMeters || stairOffset.Z > MaximumStairForwardOffsetMeters ||
                stairYawError > MaximumStairYawErrorDegrees ||
                stairRise < 0 || stairRise > MaximumThresholdRiseMeters)
            {
                diagnostics.Add(new(StairAlignmentInvalidCode, groupKey,
                    $"Entrance stair is not aligned with the threshold (local={stairOffset.X:0.###},{stairOffset.Z:0.###} m, yaw={stairYawError:0.###} deg, rise={stairRise:0.###} m)."));
            }

            if (!diagnostics.Any(item => string.Equals(item.Group, groupKey, StringComparison.Ordinal)))
                valid++;
        }

        return new(groups.Length, valid,
            double.IsPositiveInfinity(minimumDoorHingeOffset) ? 0 : minimumDoorHingeOffset,
            maximumDoorHingeOffset, maximumDoorPlaneError, maximumDoorYawError,
            maximumStairLateralError, maximumStairYawError, diagnostics);
    }

    private static bool IsBuildingMember(EntityInstance entity) =>
        HasTag(entity, "medieval-house") || HasTag(entity, "village-house") || HasTag(entity, "village-building");

    private static string? GroupKey(EntityInstance entity) => entity.Tags.FirstOrDefault(tag =>
        tag.StartsWith("assistant-node:", StringComparison.OrdinalIgnoreCase));

    private static bool HasTag(EntityInstance entity, string tag) =>
        entity.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    private static (double X, double Z) LocalHorizontalOffset(
        Vector3Value origin,
        Vector3Value point,
        double yaw)
    {
        var dx = point.X - origin.X;
        var dz = point.Z - origin.Z;
        var cosine = Math.Cos(yaw);
        var sine = Math.Sin(yaw);
        return ((cosine * dx) - (sine * dz), (sine * dx) + (cosine * dz));
    }

    private static double YawDeltaDegrees(QuaternionValue first, QuaternionValue second)
    {
        var delta = Math.Abs((YawRadians(first) - YawRadians(second)) * 180 / Math.PI) % 360;
        return delta > 180 ? 360 - delta : delta;
    }

    private static double YawRadians(QuaternionValue rotation) => Math.Atan2(
        2d * ((rotation.W * rotation.Y) + (rotation.X * rotation.Z)),
        1d - (2d * ((rotation.Y * rotation.Y) + (rotation.Z * rotation.Z))));
}
