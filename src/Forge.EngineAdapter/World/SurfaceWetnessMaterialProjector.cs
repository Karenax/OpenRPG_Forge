using Forge.Domain.Areas;

namespace Forge.EngineAdapter.World;

public sealed record PbrMaterialState(
    float AlbedoR,
    float AlbedoG,
    float AlbedoB,
    float Alpha,
    float Roughness,
    float Specular);

public static class SurfaceWetnessMaterialProjector
{
    public static PbrMaterialState Project(
        PbrMaterialState dry,
        float surfaceWetness,
        SurfaceWetnessMaterialProfile coefficients)
    {
        ArgumentNullException.ThrowIfNull(dry);
        ArgumentNullException.ThrowIfNull(coefficients);
        if (!float.IsFinite(surfaceWetness) || surfaceWetness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(surfaceWetness));
        Validate(dry);
        if (surfaceWetness <= 0) return dry;

        var albedoScale = 1f - (coefficients.AlbedoDarkening * surfaceWetness);
        return new(
            Math.Clamp(dry.AlbedoR * albedoScale, 0, 1),
            Math.Clamp(dry.AlbedoG * albedoScale, 0, 1),
            Math.Clamp(dry.AlbedoB * albedoScale, 0, 1),
            dry.Alpha,
            Math.Clamp(dry.Roughness - (coefficients.RoughnessReduction * surfaceWetness), .08f, 1),
            Math.Clamp(dry.Specular + (coefficients.SpecularIncrease * surfaceWetness), 0, 1));
    }

    private static void Validate(PbrMaterialState value)
    {
        if (!float.IsFinite(value.AlbedoR) || !float.IsFinite(value.AlbedoG) ||
            !float.IsFinite(value.AlbedoB) || !float.IsFinite(value.Alpha) ||
            !float.IsFinite(value.Roughness) || !float.IsFinite(value.Specular) ||
            value.AlbedoR is < 0 or > 1 || value.AlbedoG is < 0 or > 1 ||
            value.AlbedoB is < 0 or > 1 || value.Alpha is < 0 or > 1 ||
            value.Roughness is < 0 or > 1 || value.Specular is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public sealed record SurfaceWetnessTransitionState(
    float StartValue,
    float TargetValue,
    double DurationSeconds,
    double ElapsedSeconds);

public sealed record SurfaceWetnessTransitionResult(
    SurfaceWetnessTransitionState State,
    float Value,
    bool Completed);

public static class SurfaceWetnessTransition
{
    public static SurfaceWetnessTransitionState Start(
        float startValue,
        float targetValue,
        double durationSeconds)
    {
        ValidateWetness(startValue, nameof(startValue));
        ValidateWetness(targetValue, nameof(targetValue));
        if (!double.IsFinite(durationSeconds) || durationSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        return new(startValue, targetValue, durationSeconds, 0);
    }

    public static SurfaceWetnessTransitionResult Advance(
        SurfaceWetnessTransitionState state,
        double deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        var elapsed = Math.Min(state.DurationSeconds, state.ElapsedSeconds + deltaSeconds);
        var amount = state.DurationSeconds <= 0 ? 1f : (float)(elapsed / state.DurationSeconds);
        var value = state.StartValue + ((state.TargetValue - state.StartValue) * amount);
        var next = state with { ElapsedSeconds = elapsed };
        return new(next, value, elapsed >= state.DurationSeconds);
    }

    private static void ValidateWetness(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
