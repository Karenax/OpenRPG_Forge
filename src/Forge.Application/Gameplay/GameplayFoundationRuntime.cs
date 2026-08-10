using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;

namespace Forge.Application.Gameplay;

public sealed record GroundedMovementState(Vector3Value Velocity, bool Grounded, bool Crouched);
public sealed record GroundedMovementInput(Vector3Value Direction, bool Run, bool Jump, bool Crouch,
    Vector3Value? RootMotionDelta = null);
public sealed record GroundedMovementFrame(GroundedMovementState State, float AppliedSpeed,
    bool Jumped, bool UsedRootMotion, bool Predicted);

public sealed record InteractionCandidate(Guid Id, Vector3Value Position, IReadOnlySet<string> Tags,
    IReadOnlySet<string> AvailableVerbs);
public sealed record InteractionFocus(InteractionCandidate Candidate, InteractionVerbDefinition Verb,
    float Distance, float HoldProgress, bool Ready);

public sealed record RuntimeInputResolution(RuntimeInputActionDefinition Action,
    RuntimeInputBindingDefinition Binding, string Glyph);

public static class GameplayFoundationRuntime
{
    public static GroundedMovementFrame Step(GroundedMovementProfile profile, GroundedMovementState state,
        GroundedMovementInput input, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled || deltaSeconds <= 0 || !float.IsFinite(deltaSeconds))
            return new(state, 0, false, false, profile.NetworkPrediction);
        var magnitude = Length(input.Direction);
        var direction = magnitude <= .0001f ? Vector3Value.Zero : Scale(input.Direction, 1 / magnitude);
        var targetSpeed = input.Crouch ? profile.WalkSpeed * profile.CrouchSpeedMultiplier :
            input.Run ? profile.RunSpeed : profile.WalkSpeed;
        var horizontal = new Vector3Value(state.Velocity.X, 0, state.Velocity.Z);
        var target = Scale(direction, targetSpeed);
        var rate = magnitude > .0001f ? profile.Acceleration : profile.Deceleration;
        var nextHorizontal = MoveTowards(horizontal, target, rate * deltaSeconds);
        var jumped = input.Jump && state.Grounded && !input.Crouch && profile.JumpVelocity > 0;
        var vertical = jumped ? profile.JumpVelocity : state.Velocity.Y;
        var root = input.RootMotionDelta.GetValueOrDefault();
        var usedRoot = profile.RootMotion && input.RootMotionDelta.HasValue;
        var velocity = usedRoot
            ? new Vector3Value(root.X / deltaSeconds, root.Y / deltaSeconds, root.Z / deltaSeconds)
            : new Vector3Value(nextHorizontal.X, vertical, nextHorizontal.Z);
        return new(new(velocity, state.Grounded && !jumped, input.Crouch), targetSpeed, jumped, usedRoot,
            profile.NetworkPrediction);
    }

    public static InteractionFocus? ResolveFocus(InteractionProfile profile, Vector3Value origin,
        Vector3Value forward, IReadOnlyList<InteractionCandidate> candidates, string verbKey,
        float heldSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!profile.Enabled) return null;
        var verb = profile.Verbs.SingleOrDefault(item => string.Equals(item.Key, verbKey, StringComparison.Ordinal));
        if (verb is null) return null;
        var allowedTags = verb.TargetTags.ToHashSet(StringComparer.Ordinal);
        var normalizedForward = Normalize(forward);
        var focus = candidates.Where(candidate => candidate.AvailableVerbs.Contains(verb.Key) &&
                candidate.Tags.Any(allowedTags.Contains))
            .Select(candidate => new { Candidate = candidate, Offset = Subtract(candidate.Position, origin) })
            .Select(item => new { item.Candidate, item.Offset, Distance = Length(item.Offset) })
            .Where(item => item.Distance <= profile.FocusDistance && item.Distance > .0001f)
            .Where(item => profile.Targeting != InteractionTargetingMode.ViewCone ||
                Dot(Normalize(item.Offset), normalizedForward) >= MathF.Cos(profile.ViewAngleDegrees * MathF.PI / 360))
            .OrderBy(item => item.Distance).ThenBy(item => item.Candidate.Id).FirstOrDefault();
        if (focus is null) return null;
        var progress = verb.HoldSeconds <= 0 ? 1 : Math.Clamp(heldSeconds / verb.HoldSeconds, 0, 1);
        return new(focus.Candidate, verb, focus.Distance, progress, progress >= 1);
    }

    public static GameplayCameraProfile ResolveCamera(GameplayCameraCatalog catalog, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var selected = string.IsNullOrWhiteSpace(key) ? catalog.DefaultProfileKey : key;
        return catalog.Profiles.Single(item => string.Equals(item.Key, selected, StringComparison.Ordinal));
    }

    public static RuntimeInputResolution? ResolveInput(RuntimeInputProfile profile, string actionKey,
        RuntimeInputDevice device, IReadOnlySet<string> activeContexts)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(activeContexts);
        var action = profile.Actions.SingleOrDefault(item =>
            string.Equals(item.Key, actionKey, StringComparison.Ordinal) && activeContexts.Contains(item.Context));
        if (action is null) return null;
        var binding = profile.Bindings.Where(item => string.Equals(item.ActionKey, action.Key, StringComparison.Ordinal) &&
                item.Device == device)
            .OrderByDescending(item => item.Primary).ThenBy(item => item.Id).FirstOrDefault();
        return binding is null ? null : new(action, binding, binding.Glyph);
    }

    public static RuntimeInputProfile Rebind(RuntimeInputProfile profile, string actionKey,
        RuntimeInputDevice device, string binding, string glyph, string provenance)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.AllowRebinding) throw new InvalidOperationException("GAME_INPUT_REBIND_DISABLED");
        if (profile.Actions.All(item => !string.Equals(item.Key, actionKey, StringComparison.Ordinal)))
            throw new InvalidOperationException("GAME_INPUT_ACTION_MISSING");
        ArgumentException.ThrowIfNullOrWhiteSpace(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(glyph);
        var matches = profile.Bindings.Where(item =>
            string.Equals(item.ActionKey, actionKey, StringComparison.Ordinal) && item.Device == device).ToArray();
        var replacement = new RuntimeInputBindingDefinition(
            matches.FirstOrDefault()?.Id ?? Guid.NewGuid(), actionKey, device, binding.Trim(), glyph.Trim(), true);
        var bindings = profile.Bindings.Except(matches).Append(replacement)
            .OrderBy(item => item.ActionKey, StringComparer.Ordinal).ThenBy(item => item.Device).ThenBy(item => item.Id)
            .ToArray();
        return profile with { Bindings = bindings, Provenance = provenance.Trim() };
    }

    public static bool ShouldAutosave(RuntimeSavePolicy policy, DateTimeOffset? previous,
        DateTimeOffset now) => policy.AutosaveIntervalSeconds > 0 &&
        (previous is null || now - previous.Value >= TimeSpan.FromSeconds(policy.AutosaveIntervalSeconds));

    private static Vector3Value MoveTowards(Vector3Value current, Vector3Value target, float maximumDelta)
    {
        var delta = Subtract(target, current);
        var length = Length(delta);
        return length <= maximumDelta || length <= .0001f ? target : Add(current, Scale(delta, maximumDelta / length));
    }
    private static Vector3Value Normalize(Vector3Value value) => Length(value) <= .0001f ? Vector3Value.Zero : Scale(value, 1 / Length(value));
    private static float Length(Vector3Value value) => MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
    private static float Dot(Vector3Value left, Vector3Value right) => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
    private static Vector3Value Add(Vector3Value left, Vector3Value right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    private static Vector3Value Subtract(Vector3Value left, Vector3Value right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    private static Vector3Value Scale(Vector3Value value, float scalar) => new(value.X * scalar, value.Y * scalar, value.Z * scalar);
}
