using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.Rendering;

public sealed record AreaRenderPlan(
    AreaRenderingProfile Rendering,
    SkyProfile Sky,
    AreaLightingProfile Lighting,
    AreaFogProfile Fog,
    DynamicLightBudgetProfile LightBudget,
    int ObservedDynamicLights,
    int ObservedShadowCastingLights,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaRenderPlanBuilder
{
    public const string ForwardPlusRequiredCode = "render.forward_plus.required";
    public const string PerPixelRequiredCode = "render.per_pixel.required";
    public const string DynamicLightWarningCode = "render.lights.dynamic_warning";
    public const string DynamicLightBudgetCode = "render.lights.dynamic_budget_exceeded";
    public const string ShadowLightWarningCode = "render.lights.shadow_warning";
    public const string ShadowLightBudgetCode = "render.lights.shadow_budget_exceeded";

    public static AreaRenderPlan Build(
        AreaEnvironmentProfile profile,
        string platform,
        int observedDynamicLights,
        int observedShadowCastingLights)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (observedDynamicLights < 0 || observedShadowCastingLights is < 0 ||
            observedShadowCastingLights > observedDynamicLights)
        {
            throw new ArgumentOutOfRangeException(nameof(observedDynamicLights));
        }

        var budget = profile.Rendering.LightBudgets.FirstOrDefault(candidate =>
                         string.Equals(candidate.Platform, platform, StringComparison.OrdinalIgnoreCase)) ??
                     profile.Rendering.LightBudgets.FirstOrDefault(candidate => candidate.Platform == "desktop") ??
                     profile.Rendering.LightBudgets[0];
        var diagnostics = new List<DomainDiagnostic>();
        if (!profile.Rendering.ForwardPlus)
        {
            diagnostics.Add(new(
                ForwardPlusRequiredCode,
                "The reference PBR profile requires the Forward+ renderer.",
                "environment/rendering/forwardPlus"));
        }

        if (!profile.Rendering.PerPixelLighting)
        {
            diagnostics.Add(new(
                PerPixelRequiredCode,
                "The reference lighting profile requires per-pixel shading.",
                "environment/rendering/perPixelLighting"));
        }

        AddBudgetDiagnostic(
            diagnostics,
            observedDynamicLights,
            budget.MaximumDynamicLights,
            budget.WarningThreshold,
            DynamicLightWarningCode,
            DynamicLightBudgetCode,
            "dynamic lights",
            "environment/rendering/lightBudgets/dynamic");
        AddBudgetDiagnostic(
            diagnostics,
            observedShadowCastingLights,
            budget.MaximumShadowCastingLights,
            budget.WarningThreshold,
            ShadowLightWarningCode,
            ShadowLightBudgetCode,
            "shadow-casting lights",
            "environment/rendering/lightBudgets/shadows");
        return new(
            profile.Rendering,
            profile.Sky,
            profile.Lighting,
            profile.Fog,
            budget,
            observedDynamicLights,
            observedShadowCastingLights,
            diagnostics);
    }

    private static void AddBudgetDiagnostic(
        ICollection<DomainDiagnostic> diagnostics,
        int observed,
        int maximum,
        double warningThreshold,
        string warningCode,
        string exceededCode,
        string label,
        string path)
    {
        if (observed > maximum)
        {
            diagnostics.Add(new(exceededCode, $"Observed {observed} {label}; platform budget is {maximum}.", path));
        }
        else if (maximum > 0 && observed >= Math.Ceiling(maximum * warningThreshold))
        {
            diagnostics.Add(new(warningCode, $"Observed {observed} {label}; platform warning threshold is {warningThreshold:P0} of {maximum}.", path));
        }
    }
}
