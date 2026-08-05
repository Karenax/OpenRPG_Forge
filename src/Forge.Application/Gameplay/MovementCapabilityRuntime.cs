using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public enum MovementCapabilityKind
{
    Ground,
    Swimming,
    Diving,
    Climbing,
}

public sealed record MovementCapabilityContext(bool InsideWater, bool DiveRequested, bool OnClimbSurface,
    bool ClimbRequested, float BreathRemaining, float StaminaRemaining);

public sealed record MovementCapabilityDecision(bool Accepted, MovementCapabilityKind Kind, float Speed,
    float VerticalAcceleration, float BreathRemaining, float StaminaRemaining, string Code);

public static class MovementCapabilityRuntime
{
    public static MovementCapabilityDecision Resolve(ModuleGameplayControlProfile profile,
        MovementCapabilityContext context, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (context.InsideWater && profile.Swimming.Enabled)
        {
            var diving = context.DiveRequested && context.BreathRemaining > 0;
            var breath = diving
                ? MathF.Max(0, context.BreathRemaining - deltaSeconds)
                : MathF.Min(profile.Swimming.BreathSeconds, context.BreathRemaining + deltaSeconds * 2);
            return new MovementCapabilityDecision(true,
                diving ? MovementCapabilityKind.Diving : MovementCapabilityKind.Swimming,
                diving ? profile.Swimming.DiveSpeed : profile.Swimming.SurfaceSpeed,
                diving ? -profile.Swimming.Buoyancy * .25f : profile.Swimming.Buoyancy,
                breath, context.StaminaRemaining, diving && breath <= 0 ? "swim.breath_exhausted" : "swim.active");
        }

        if (context.OnClimbSurface && context.ClimbRequested && profile.Climbing.Enabled)
        {
            var stamina = MathF.Max(0,
                context.StaminaRemaining - profile.Climbing.StaminaDrainPerSecond * deltaSeconds);
            return stamina <= 0
                ? new MovementCapabilityDecision(false, MovementCapabilityKind.Ground, 0, 0,
                    context.BreathRemaining, 0, "climb.stamina_exhausted")
                : new MovementCapabilityDecision(true, MovementCapabilityKind.Climbing,
                    profile.Climbing.ClimbSpeed, profile.Climbing.ClimbSpeed, context.BreathRemaining, stamina,
                    "climb.active");
        }

        return new MovementCapabilityDecision(true, MovementCapabilityKind.Ground, 6, 0,
            MathF.Min(profile.Swimming.BreathSeconds, context.BreathRemaining + deltaSeconds * 2),
            MathF.Min(profile.Climbing.StaminaCapacity, context.StaminaRemaining + deltaSeconds * 8),
            "ground.active");
    }
}
