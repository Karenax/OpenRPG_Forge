using System.Text.Json;
using Forge.Application.WorldGeneration;

namespace Forge.ModuleFormat.Serialization;

internal static class WorldGenerationExecutionPlanJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(WorldGenerationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsureValid(plan);
        return JsonSerializer.Serialize(new WorldGenerationExecutionPlanDocument(CurrentFormatVersion, plan), Options);
    }

    internal static WorldGenerationExecutionPlan Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<WorldGenerationExecutionPlanDocument>(json, Options)
            ?? throw new JsonException("The world-generation execution-plan document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"World-generation execution-plan format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        if (document.Plan is null)
            throw new JsonException("The world-generation execution plan is missing.");
        EnsureValid(document.Plan);
        return document.Plan;
    }

    private static void EnsureValid(WorldGenerationExecutionPlan plan)
    {
        if (plan.PlanId == Guid.Empty || plan.TransactionId == Guid.Empty || plan.RecipeId == Guid.Empty ||
            plan.AreaId == Guid.Empty)
            throw new JsonException("World-generation execution-plan identifiers cannot be empty.");
        if (plan.ProjectRevision < 0 || plan.RecipeRevision.Value < 0)
            throw new JsonException("World-generation execution-plan revisions cannot be negative.");
        if (plan.RecipeFingerprint.Length != 64 ||
            plan.RecipeFingerprint.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new JsonException("World-generation execution-plan fingerprint must be a lowercase SHA-256 value.");
        var ordered = plan.Steps.OrderBy(item => item.Order).ToArray();
        if (ordered.Select(item => item.Order).Distinct().Count() != ordered.Length ||
            ordered.Where((item, index) => item.Order != index).Any())
            throw new JsonException("World-generation execution-plan steps must use unique contiguous order values.");
        foreach (var step in ordered)
        {
            if (string.IsNullOrWhiteSpace(step.Operation))
                throw new JsonException($"World-generation step '{step.Order}' requires an operation.");
            if (step.DependsOn.Any(dependency => dependency < 0 || dependency >= step.Order))
                throw new JsonException($"World-generation step '{step.Order}' has an invalid dependency.");
        }
    }

    private sealed record WorldGenerationExecutionPlanDocument(
        string FormatVersion,
        WorldGenerationExecutionPlan? Plan);
}
