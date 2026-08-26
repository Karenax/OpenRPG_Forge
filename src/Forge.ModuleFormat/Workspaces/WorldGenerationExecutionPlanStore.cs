using System.Text.Json;
using Forge.Application.WorldGeneration;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class WorldGenerationExecutionPlanStore
{
    public const string RelativeDirectory = "world/generation/plans";
    public const string ReadFailedCode = "world_generation.plan.read_failed";
    public const string WriteFailedCode = "world_generation.plan.write_failed";
    public const string DeleteFailedCode = "world_generation.plan.delete_failed";

    public static WorldGenerationExecutionPlanLoadResult Load(string workspaceRoot, Guid planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (planId == Guid.Empty) throw new ArgumentException("Execution-plan ID cannot be empty.", nameof(planId));
        var path = ResolvePath(workspaceRoot, planId);
        if (!File.Exists(path)) return new(null, true, []);
        try
        {
            return new(WorldGenerationExecutionPlanJsonSerializer.Deserialize(File.ReadAllText(path)), false, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(null, false, [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static IReadOnlyList<Guid> List(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var directory = ResolveDirectory(workspaceRoot);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(value => Guid.TryParseExact(value, "N", out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Order()
            .ToArray();
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, WorldGenerationExecutionPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(plan);
        var path = ResolvePath(workspaceRoot, plan.PlanId);
        try
        {
            AtomicWorkspaceFile.Write(path, WorldGenerationExecutionPlanJsonSerializer.Serialize(plan));
            return new(true, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Delete(string workspaceRoot, Guid planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (planId == Guid.Empty) throw new ArgumentException("Execution-plan ID cannot be empty.", nameof(planId));
        var path = ResolvePath(workspaceRoot, planId);
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, [new ProjectSessionDiagnostic(DeleteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolveDirectory(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), "world", "generation", "plans");

    private static string ResolvePath(string workspaceRoot, Guid planId) =>
        Path.Combine(ResolveDirectory(workspaceRoot), $"{planId:N}.json");
}

public sealed record WorldGenerationExecutionPlanLoadResult(
    WorldGenerationExecutionPlan? Plan,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
