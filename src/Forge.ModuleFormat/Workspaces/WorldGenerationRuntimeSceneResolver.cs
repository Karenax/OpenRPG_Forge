using System.Security.Cryptography;

namespace Forge.ModuleFormat.Workspaces;

public static class WorldGenerationRuntimeSceneResolver
{
    public const string ResourceMissingCode = "world_generation.runtime.resource_missing";
    public const string SceneReadFailedCode = "world_generation.runtime.scene_read_failed";

    public static WorldGenerationRuntimeSceneResolution Resolve(
        string workspaceRoot,
        Guid areaId,
        IReadOnlyCollection<string> requiredResources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (areaId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(areaId));
        ArgumentNullException.ThrowIfNull(requiredResources);

        var canonicalPath = Forge.Application.WorldGeneration.PromoteWorldGenerationHandler
            .CanonicalScenePath(areaId);
        if (!requiredResources.Contains(canonicalPath, StringComparer.Ordinal))
            return new(null, null, null, null, []);

        var promotion = WorldGenerationPromotionStore.Load(workspaceRoot, areaId);
        if (!promotion.Succeeded || promotion.Promotion is null || promotion.SceneSha256 is null)
            return new(null, null, null, null, promotion.Diagnostics);
        if (!string.Equals(promotion.Promotion.CanonicalSceneRelativePath, canonicalPath,
                StringComparison.Ordinal))
            return Failure(ResourceMissingCode,
                "Canonical world-generation promotion does not target the active area resource.", canonicalPath);

        try
        {
            var absolutePath = WorldGenerationWorkspacePath.Resolve(
                workspaceRoot,
                canonicalPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
            if (!File.Exists(absolutePath))
                return Failure(ResourceMissingCode, "Canonical generated scene is missing.", canonicalPath);
            var bytes = File.ReadAllBytes(absolutePath);
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(sha256, promotion.SceneSha256, StringComparison.Ordinal))
                return Failure(WorldGenerationPromotionStore.EvidenceMismatchCode,
                    "Canonical generated scene changed after its promotion was verified.", canonicalPath);
            return new(canonicalPath, sha256, bytes, promotion.Promotion.ProductionVisuals, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure(SceneReadFailedCode, exception.Message, canonicalPath);
        }
    }

    private static WorldGenerationRuntimeSceneResolution Failure(string code, string message, string path) =>
        new(null, null, null, null, [new ProjectSessionDiagnostic(code, message, path)]);
}

public sealed record WorldGenerationRuntimeSceneResolution(
    string? RelativePath,
    string? Sha256,
    byte[]? Content,
    Forge.Application.WorldGeneration.WorldGenerationProductionVisualEvidence? ProductionVisuals,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool HasScene => RelativePath is not null && Sha256 is not null && Content is not null &&
                            Diagnostics.Count == 0;
}
