using System.Text.Json;
using Forge.Application.WorldGeneration;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class WorldGenerationJobStore
{
    public const string RelativeDirectory = "world/generation/jobs";
    public const string ReadFailedCode = "world_generation.job.read_failed";
    public const string WriteFailedCode = "world_generation.job.write_failed";
    public const string DeleteFailedCode = "world_generation.job.delete_failed";

    public static WorldGenerationJobLoadResult Load(string workspaceRoot, Guid planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (planId == Guid.Empty) throw new ArgumentException("Plan ID cannot be empty.", nameof(planId));
        var path = ResolvePath(workspaceRoot, planId);
        if (!File.Exists(path)) return new(null, true, []);
        try
        {
            return new(WorldGenerationJobJsonSerializer.Deserialize(File.ReadAllText(path)), false, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(null, false, [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static IReadOnlyList<WorldGenerationJob> List(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var directory = ResolveDirectory(workspaceRoot);
        if (!Directory.Exists(directory)) return [];
        var jobs = new List<WorldGenerationJob>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try { jobs.Add(WorldGenerationJobJsonSerializer.Deserialize(File.ReadAllText(file))); }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
            {
                // Querying the queue remains useful when a single external worker left a corrupt receipt.
            }
        }
        return jobs.OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.PlanId).ToArray();
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, WorldGenerationJob job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(job);
        var path = ResolvePath(workspaceRoot, job.PlanId);
        try
        {
            AtomicWorkspaceFile.Write(path, WorldGenerationJobJsonSerializer.Serialize(job));
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
        if (planId == Guid.Empty) throw new ArgumentException("Plan ID cannot be empty.", nameof(planId));
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

    public static string ResolveDirectory(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return WorldGenerationWorkspacePath.Resolve(workspaceRoot, "world", "generation", "jobs");
    }

    private static string ResolvePath(string workspaceRoot, Guid planId) =>
        WorldGenerationWorkspacePath.Resolve(
            workspaceRoot, "world", "generation", "jobs", $"{planId:N}.json");
}

public sealed record WorldGenerationJobLoadResult(
    WorldGenerationJob? Job,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
