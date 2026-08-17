using System.Numerics;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public sealed record TriggerVolumeContainment(
    bool Available,
    bool Supported,
    bool Contains,
    string DiagnosticCode,
    string Summary);

public sealed record TriggerRestRuntimeContext(
    bool DangerQueryMatched,
    int PartyMembersPresent,
    int PartyMembersExpected);

public sealed record TriggerRestRuntimeResolution(
    bool Available,
    bool Allowed,
    bool CampSetupAllowed,
    string MessageKey,
    string DiagnosticCode,
    string Summary);

public sealed record TriggerClueRuntimeContext(
    int? SkillScore,
    bool QuestActive);

public sealed record TriggerClueRuntimeResolution(
    bool Available,
    bool Revealed,
    bool SkillCheckPassed,
    bool FalseClue,
    string? TraceId,
    double DirectionDegrees,
    double AgeHours,
    Guid? JournalQuestId,
    string DiagnosticCode,
    string Summary);

public sealed record TriggerNarrativeRuntimeState(
    IReadOnlySet<Guid> FiredOnce,
    IReadOnlyDictionary<Guid, DateTimeOffset> LastFiredAt)
{
    public static TriggerNarrativeRuntimeState Empty { get; } = new(
        new HashSet<Guid>(),
        new Dictionary<Guid, DateTimeOffset>());
}

public sealed record TriggerNarrativeRuntimeContext(
    DateTimeOffset Now,
    bool IsAuthoritative,
    bool MultiplayerPolicySatisfied,
    IReadOnlySet<BlueprintId> PresentParticipants);

public sealed record TriggerNarrativeRuntimeResolution(
    TriggerNarrativeRuntimeState State,
    bool Available,
    bool Fired,
    Guid? DialogueId,
    int Priority,
    string DiagnosticCode,
    string Summary);

/// <summary>
/// Canonical, engine-independent resolution for the living-world policies carried by a trigger.
/// Callers provide observed party, skill, quest, participant and authority facts; the resolver never
/// invents those facts and never mutates a project document.
/// </summary>
public static class TriggerLivingWorldRuntimeService
{
    public const string FoundationMissingCode = "runtime.trigger.living_world.foundation_missing";
    public const string ConvexHullUnsupportedCode = "runtime.trigger.living_world.convex_hull_topology_missing";
    public const string InvalidTransformCode = "runtime.trigger.living_world.transform_invalid";
    public const string RestUnsafeCode = "runtime.trigger.rest.unsafe";
    public const string RestDangerCode = "runtime.trigger.rest.danger_present";
    public const string RestPartyCode = "runtime.trigger.rest.party_incomplete";
    public const string RestContextCode = "runtime.trigger.rest.context_invalid";
    public const string ClueSkillUnavailableCode = "runtime.trigger.clue.skill_unavailable";
    public const string ClueSkillFailedCode = "runtime.trigger.clue.skill_failed";
    public const string NarrativeAuthorityCode = "runtime.trigger.narrative.authority_denied";
    public const string NarrativeParticipantsCode = "runtime.trigger.narrative.participants_missing";
    public const string NarrativeVoteCode = "runtime.trigger.narrative.multiplayer_policy_pending";
    public const string NarrativeOnceCode = "runtime.trigger.narrative.already_fired";
    public const string NarrativeCooldownCode = "runtime.trigger.narrative.cooldown";

    public static TriggerVolumeContainment Contains(
        TriggerWorldProfile profile,
        Vector3Value worldPosition)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Foundation is not { } foundation)
        {
            return new(false, false, false, FoundationMissingCode,
                "trigger-volume:foundation-unavailable");
        }

        if (!TryToLocal(foundation.Transform, worldPosition, out var local))
        {
            return new(true, false, false, InvalidTransformCode,
                "trigger-volume:transform-invalid");
        }

        var shape = foundation.Shape;
        var contains = shape.Kind switch
        {
            TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D =>
                ContainsExtrudedPolygon(shape, local),
            TriggerWorldProfile.ShapeKind.Box3D =>
                ContainsBox(shape.Size, local),
            TriggerWorldProfile.ShapeKind.Sphere3D =>
                ContainsSphere(shape.Size, local),
            TriggerWorldProfile.ShapeKind.Capsule3D =>
                ContainsCapsule(shape.Size, local),
            TriggerWorldProfile.ShapeKind.ConvexHull3D => false,
            _ => false,
        };
        if (shape.Kind == TriggerWorldProfile.ShapeKind.ConvexHull3D)
        {
            return new(true, false, false, ConvexHullUnsupportedCode,
                "trigger-volume:convex-hull-needs-authored-faces");
        }

        return new(true, true, contains, string.Empty,
            $"trigger-volume:{shape.Kind}:contains={contains.ToString().ToLowerInvariant()}");
    }

    public static TriggerRestRuntimeResolution ResolveRest(
        TriggerWorldProfile profile,
        TriggerRestRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        if (profile.Rest is not { } rest)
        {
            return new(false, true, false, string.Empty, string.Empty,
                "trigger-rest:optional-fallback");
        }
        if (context.PartyMembersPresent < 0 || context.PartyMembersExpected < 1 ||
            context.PartyMembersPresent > context.PartyMembersExpected)
        {
            return new(true, false, false, rest.DeniedMessageKey, RestContextCode,
                "trigger-rest:context-invalid");
        }
        if (!rest.Safe)
        {
            return new(true, false, false, rest.DeniedMessageKey, RestUnsafeCode,
                "trigger-rest:unsafe");
        }
        if (context.DangerQueryMatched)
        {
            return new(true, false, false, rest.DeniedMessageKey, RestDangerCode,
                $"trigger-rest:danger:{rest.DangerQuery}");
        }
        if (rest.WholePartyRequired && context.PartyMembersPresent != context.PartyMembersExpected)
        {
            return new(true, false, false, rest.DeniedMessageKey, RestPartyCode,
                $"trigger-rest:party={context.PartyMembersPresent}/{context.PartyMembersExpected}");
        }

        return new(true, true, rest.AllowCampSetup, rest.AllowedMessageKey, string.Empty,
            $"trigger-rest:allowed:camp={rest.AllowCampSetup.ToString().ToLowerInvariant()}");
    }

    public static TriggerClueRuntimeResolution ResolveClue(
        TriggerWorldProfile profile,
        TriggerClueRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        if (profile.Clue is not { } clue)
        {
            return new(false, false, false, false, null, 0, 0, null, string.Empty,
                "trigger-clue:optional-fallback");
        }
        if (context.SkillScore is null)
        {
            return new(true, false, false, false, null, clue.DirectionDegrees, clue.AgeHours,
                context.QuestActive ? clue.QuestId : null, ClueSkillUnavailableCode,
                $"trigger-clue:skill-unavailable:{clue.SkillId}");
        }

        var passed = context.SkillScore.Value >= clue.Difficulty;
        var falseClue = !passed && !string.IsNullOrWhiteSpace(clue.FalseClueId);
        var revealed = passed || falseClue;
        var traceId = passed ? clue.TraceId : falseClue ? clue.FalseClueId : null;
        return new(true, revealed, passed, falseClue, traceId, clue.DirectionDegrees, clue.AgeHours,
            context.QuestActive ? clue.QuestId : null,
            passed || falseClue ? string.Empty : ClueSkillFailedCode,
            passed
                ? $"trigger-clue:trace={clue.TraceId}:skill={context.SkillScore}/{clue.Difficulty}"
                : falseClue
                    ? $"trigger-clue:false={clue.FalseClueId}:skill={context.SkillScore}/{clue.Difficulty}"
                    : $"trigger-clue:hidden:skill={context.SkillScore}/{clue.Difficulty}");
    }

    public static TriggerNarrativeRuntimeResolution ResolveNarrative(
        TriggerWorldProfile profile,
        TriggerNarrativeRuntimeState state,
        TriggerNarrativeRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        if (profile.Narrative is not { } narrative)
        {
            return new(state, false, false, null, 0, string.Empty,
                "trigger-narrative:optional-fallback");
        }
        if (!context.IsAuthoritative)
            return Block(state, narrative.Priority, NarrativeAuthorityCode, "trigger-narrative:not-authoritative");
        if (narrative.Participants.Any(item => !context.PresentParticipants.Contains(item)))
            return Block(state, narrative.Priority, NarrativeParticipantsCode, "trigger-narrative:participants-missing");
        if (!context.MultiplayerPolicySatisfied)
            return Block(state, narrative.Priority, NarrativeVoteCode,
                $"trigger-narrative:policy-pending:{narrative.MultiplayerPolicy}");
        if (narrative.Once && state.FiredOnce.Contains(profile.Id))
            return Block(state, narrative.Priority, NarrativeOnceCode, "trigger-narrative:once");
        if (state.LastFiredAt.TryGetValue(profile.Id, out var last) &&
            context.Now - last < TimeSpan.FromSeconds(narrative.CooldownSeconds))
            return Block(state, narrative.Priority, NarrativeCooldownCode, "trigger-narrative:cooldown");

        var firedOnce = state.FiredOnce.ToHashSet();
        if (narrative.Once) firedOnce.Add(profile.Id);
        var lastFired = state.LastFiredAt.ToDictionary();
        lastFired[profile.Id] = context.Now;
        var updated = new TriggerNarrativeRuntimeState(firedOnce, lastFired);
        return new(updated, true, true, narrative.DialogueId, narrative.Priority, string.Empty,
            $"trigger-narrative:dialogue={narrative.DialogueId:D}:priority={narrative.Priority}");
    }

    private static TriggerNarrativeRuntimeResolution Block(
        TriggerNarrativeRuntimeState state,
        int priority,
        string code,
        string summary) => new(state, true, false, null, priority, code, summary);

    private static bool TryToLocal(
        TriggerWorldProfile.TransformPolicy transform,
        Vector3Value world,
        out Vector3 local)
    {
        local = default;
        var scale = transform.Scale;
        var position = transform.Position;
        var rotation = transform.RotationDegrees;
        if (!Finite(scale) || !Finite(position) || !Finite(rotation) ||
            Math.Abs(scale.X) < float.Epsilon || Math.Abs(scale.Y) < float.Epsilon ||
            Math.Abs(scale.Z) < float.Epsilon)
        {
            return false;
        }

        var radians = MathF.PI / 180f;
        var matrix = Matrix4x4.CreateScale(scale.X, scale.Y, scale.Z) *
                     Matrix4x4.CreateFromYawPitchRoll(
                         rotation.Y * radians,
                         rotation.X * radians,
                         rotation.Z * radians) *
                     Matrix4x4.CreateTranslation(position.X, position.Y, position.Z);
        if (!Matrix4x4.Invert(matrix, out var inverse)) return false;
        local = Vector3.Transform(new Vector3(world.X, world.Y, world.Z), inverse);
        return float.IsFinite(local.X) && float.IsFinite(local.Y) && float.IsFinite(local.Z);
    }

    private static bool ContainsExtrudedPolygon(TriggerWorldProfile.ShapePolicy shape, Vector3 point)
    {
        if (shape.Vertices.Count < 3 || !double.IsFinite(shape.Height) || shape.Height <= 0) return false;
        var floor = shape.Vertices.Min(item => item.Y);
        if (point.Y < floor || point.Y > floor + shape.Height) return false;
        if (!PointInPolygon(shape.Vertices, point.X, point.Z)) return false;
        return shape.Holes.All(hole => !PointInPolygon(hole, point.X, point.Z));
    }

    private static bool PointInPolygon(IReadOnlyList<Vector3Value> vertices, float x, float z)
    {
        if (vertices.Count < 3) return false;
        var inside = false;
        for (var current = 0, previous = vertices.Count - 1; current < vertices.Count; previous = current++)
        {
            var a = vertices[current];
            var b = vertices[previous];
            var crosses = a.Z > z != b.Z > z &&
                          x < ((b.X - a.X) * (z - a.Z) / (b.Z - a.Z)) + a.X;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    private static bool ContainsBox(Vector3Value size, Vector3 point) =>
        Math.Abs(point.X) <= size.X / 2f &&
        Math.Abs(point.Y) <= size.Y / 2f &&
        Math.Abs(point.Z) <= size.Z / 2f;

    private static bool ContainsSphere(Vector3Value size, Vector3 point)
    {
        var radius = size.X / 2f;
        return radius > 0 && point.LengthSquared() <= radius * radius;
    }

    private static bool ContainsCapsule(Vector3Value size, Vector3 point)
    {
        var radius = size.X / 2f;
        var height = MathF.Max(size.Y, size.X);
        if (radius <= 0 || height <= 0) return false;
        var halfSegment = MathF.Max(0, (height / 2f) - radius);
        var nearestY = Math.Clamp(point.Y, -halfSegment, halfSegment);
        var delta = point - new Vector3(0, nearestY, 0);
        return delta.LengthSquared() <= radius * radius;
    }

    private static bool Finite(Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
