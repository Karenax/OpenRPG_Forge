using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;

namespace Forge.EngineAdapter.WorldGeneration;

public sealed record WorldGenerationBridgeRequest(
    string ProtocolVersion,
    Guid PlanId,
    Guid TransactionId,
    long ProjectRevision,
    long RecipeRevision,
    Guid RecipeId,
    Guid AreaId,
    string RecipeFingerprint,
    string PlanFingerprint,
    int Seed,
    bool DryRun,
    WorldGenerationExecutionStep Step,
    WorldGenerationRecipe Recipe);

public sealed record WorldGenerationBridgeReceipt(
    string ProtocolVersion,
    Guid PlanId,
    int StepOrder,
    bool Succeeded,
    string Backend,
    IReadOnlyList<string> Artifacts,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics);

public static class WorldGenerationBridgeProtocol
{
    public const string CurrentVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string SerializeRequest(WorldGenerationBridgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        return JsonSerializer.Serialize(request, Options);
    }

    public static WorldGenerationBridgeRequest DeserializeRequest(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var request = JsonSerializer.Deserialize<WorldGenerationBridgeRequest>(json, Options)
            ?? throw new JsonException("The world-generation bridge request is empty.");
        Validate(request);
        return request;
    }

    public static string SerializeReceipt(WorldGenerationBridgeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!string.Equals(receipt.ProtocolVersion, CurrentVersion, StringComparison.Ordinal))
            throw new NotSupportedException($"Bridge protocol '{receipt.ProtocolVersion}' is unsupported.");
        return JsonSerializer.Serialize(receipt, Options);
    }

    public static WorldGenerationBridgeRequest CreateRequest(
        WorldGenerationExecutionContext context,
        WorldGenerationExecutionStep step) => new(
        CurrentVersion,
        context.Plan.PlanId,
        context.Plan.TransactionId,
        context.Plan.ProjectRevision,
        context.Plan.RecipeRevision.Value,
        context.Recipe.Id,
        context.Recipe.AreaId,
        context.Plan.RecipeFingerprint,
        context.Plan.PlanFingerprint,
        context.Recipe.Seed,
        context.DryRun,
        step,
        context.Recipe);

    private static void Validate(WorldGenerationBridgeRequest request)
    {
        if (!string.Equals(request.ProtocolVersion, CurrentVersion, StringComparison.Ordinal))
            throw new NotSupportedException($"Bridge protocol '{request.ProtocolVersion}' is unsupported.");
        if (request.PlanId == Guid.Empty || request.TransactionId == Guid.Empty || request.RecipeId == Guid.Empty ||
            request.AreaId == Guid.Empty)
            throw new JsonException("World-generation bridge identifiers cannot be empty.");
        if (request.ProjectRevision < 0 || request.RecipeRevision < 0)
            throw new JsonException("World-generation bridge revisions cannot be negative.");
        if (request.Recipe is null || request.Step is null)
            throw new JsonException("World-generation bridge recipe or step is missing.");
        if (!string.Equals(request.RecipeFingerprint, request.Recipe.Fingerprint(), StringComparison.Ordinal))
            throw new JsonException("World-generation bridge recipe fingerprint does not match its payload.");
        if (string.IsNullOrEmpty(request.PlanFingerprint) || request.PlanFingerprint.Length != 64 ||
            request.PlanFingerprint.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new JsonException("World-generation bridge plan fingerprint must be a lowercase SHA-256 value.");
        if (request.RecipeId != request.Recipe.Id || request.AreaId != request.Recipe.AreaId ||
            request.Seed != request.Recipe.Seed)
            throw new JsonException("World-generation bridge metadata does not match its recipe.");
        if (request.Step.Order < 0 || !Enum.IsDefined(request.Step.Stage) ||
            !Enum.IsDefined(request.Step.Backend) || request.Step.Backend == WorldGenerationBackend.Auto ||
            !WorldGenerationBackendPolicy.Supports(request.Step.Backend, request.Step.Stage) ||
            request.Step.Parameters is null || request.Step.DependsOn is null ||
            !string.Equals(request.Step.Operation,
                WorldGenerationOperations.For(request.Step.Stage, request.Step.Backend), StringComparison.Ordinal))
            throw new JsonException("World-generation bridge step is invalid.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
