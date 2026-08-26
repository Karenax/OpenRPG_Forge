using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;

namespace Forge.EngineAdapter.WorldGeneration;

public abstract class WorldGenerationBridgeAdapter : IWorldGenerationAdapter
{
    public abstract WorldGenerationBackend Backend { get; }

    public virtual bool CanExecute(WorldGenerationExecutionStep step) => step.Backend == Backend;

    public async ValueTask<WorldGenerationStepResult> ExecuteAsync(
        WorldGenerationExecutionContext context,
        WorldGenerationExecutionStep step,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        if (!CanExecute(step))
            return Failure(step, "world_generation.adapter_stage_unsupported",
                $"Backend '{Backend}' cannot execute '{step.Operation}'.");
        if (!string.Equals(context.Plan.RecipeFingerprint, context.Recipe.Fingerprint(), StringComparison.Ordinal))
            return Failure(step, "world_generation.recipe_changed",
                "The recipe changed after the execution plan was created.");

        var request = WorldGenerationBridgeProtocol.CreateRequest(context, step);
        var relativePath = ArtifactPath(request);
        try
        {
            await context.ArtifactSink.WriteTextAsync(
                relativePath,
                WorldGenerationBridgeProtocol.SerializeRequest(request),
                cancellationToken).ConfigureAwait(false);
            return new(true, step.Stage, step.Backend, [relativePath], []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {
            return Failure(step, "world_generation.bridge_write_failed", exception.Message);
        }
    }

    protected virtual string ArtifactPath(WorldGenerationBridgeRequest request)
    {
        var mode = request.DryRun ? "preview" : "apply";
        var stage = request.Step.Stage.ToString().ToLowerInvariant();
        return $"{request.RecipeId:N}/{request.RecipeFingerprint}/{request.PlanId:N}/{mode}/{request.Step.Order:D2}-{stage}.request.json";
    }

    private WorldGenerationStepResult Failure(
        WorldGenerationExecutionStep step,
        string code,
        string message) =>
        new(false, step.Stage, Backend, [],
            [new WorldGenerationValidationIssue(code, message, $"world-generation/steps/{step.Order}")]);
}

public sealed class Terrain3DWorldGenerationAdapter : WorldGenerationBridgeAdapter
{
    public override WorldGenerationBackend Backend => WorldGenerationBackend.Terrain3D;

    public override bool CanExecute(WorldGenerationExecutionStep step) =>
        step.Backend == Backend && step.Stage is WorldGenerationStage.Terrain or WorldGenerationStage.Hydrology;
}

public sealed class PcgGodotWorldGenerationAdapter : WorldGenerationBridgeAdapter
{
    public override WorldGenerationBackend Backend => WorldGenerationBackend.PcgGodot;

    public override bool CanExecute(WorldGenerationExecutionStep step) =>
        step.Backend == Backend && step.Stage is WorldGenerationStage.Biomes or WorldGenerationStage.Roads or
            WorldGenerationStage.Settlements or WorldGenerationStage.Scatter;
}

public sealed class GodotNativeWorldGenerationAdapter : WorldGenerationBridgeAdapter
{
    public override WorldGenerationBackend Backend => WorldGenerationBackend.GodotNative;

    // The OpenRPG Godot integration addon consumes these requests and provides a native implementation
    // for every stage when an optional external backend is absent.
    public override bool CanExecute(WorldGenerationExecutionStep step) => true;
}
