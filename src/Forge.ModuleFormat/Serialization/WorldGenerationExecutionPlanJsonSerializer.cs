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
        if (string.IsNullOrEmpty(plan.RecipeFingerprint) || plan.RecipeFingerprint.Length != 64 ||
            plan.RecipeFingerprint.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new JsonException("World-generation execution-plan fingerprint must be a lowercase SHA-256 value.");
        if (plan.Steps is null || plan.Steps.Any(step => step is null))
            throw new JsonException("World-generation execution-plan steps are missing.");
        if (plan.Diagnostics is null || (plan.ReadyToApply && plan.Diagnostics.Count > 0) ||
            plan.Diagnostics.Any(item => item is null || string.IsNullOrWhiteSpace(item.Code) ||
                item.Code.Length > 160 || string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 4096 ||
                string.IsNullOrWhiteSpace(item.Path) || item.Path.Length > 512))
            throw new JsonException("World-generation execution-plan diagnostics are invalid.");
        if (string.IsNullOrEmpty(plan.PlanFingerprint) || plan.PlanFingerprint.Length != 64 ||
            plan.PlanFingerprint.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)) ||
            !string.Equals(plan.PlanFingerprint, WorldGenerationExecutionPlanFingerprint.Compute(plan),
                StringComparison.Ordinal))
            throw new JsonException("World-generation plan fingerprint is invalid or does not match its steps.");
        var ordered = plan.Steps.OrderBy(item => item.Order).ToArray();
        if (ordered.Length == 0 || ordered.Length > Enum.GetValues<Forge.Domain.WorldGeneration.WorldGenerationStage>().Length ||
            ordered.Select(item => item.Order).Distinct().Count() != ordered.Length ||
            ordered.Where((item, index) => item.Order != index).Any())
            throw new JsonException("World-generation execution-plan steps must use unique contiguous order values.");
        foreach (var step in ordered)
        {
            if (step.Parameters is null || step.DependsOn is null || string.IsNullOrWhiteSpace(step.Operation) ||
                !Enum.IsDefined(step.Stage) || !Enum.IsDefined(step.Backend) ||
                step.Backend == Forge.Domain.WorldGeneration.WorldGenerationBackend.Auto ||
                !Forge.Domain.WorldGeneration.WorldGenerationBackendPolicy.Supports(step.Backend, step.Stage) ||
                !string.Equals(step.Operation, WorldGenerationOperations.For(step.Stage, step.Backend),
                    StringComparison.Ordinal))
                throw new JsonException($"World-generation step '{step.Order}' has an invalid backend or operation.");
            if (step.DependsOn.Any(dependency => dependency < 0 || dependency >= step.Order))
                throw new JsonException($"World-generation step '{step.Order}' has an invalid dependency.");
            if (step.Parameters.Count > 64 || step.Parameters.Any(item =>
                    string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 64 || item.Value is null ||
                    item.Value.Length > 4096))
                throw new JsonException($"World-generation step '{step.Order}' has invalid parameters.");
        }
    }

    private sealed record WorldGenerationExecutionPlanDocument(
        string FormatVersion,
        WorldGenerationExecutionPlan? Plan);
}
