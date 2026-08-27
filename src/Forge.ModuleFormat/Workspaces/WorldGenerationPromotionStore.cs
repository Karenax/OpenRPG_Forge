using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.WorldGeneration;

namespace Forge.ModuleFormat.Workspaces;

public static class WorldGenerationPromotionStore
{
    public const string ReadFailedCode = "world_generation.promotion.read_failed";
    public const string WriteFailedCode = "world_generation.promotion.write_failed";
    public const string EvidenceMismatchCode = "world_generation.promotion.evidence_mismatch";
    public const string ProviderDependencyCode = "world_generation.promotion.provider_dependency";
    public const string CanonicalConflictCode = "world_generation.promotion.canonical_conflict";
    private static readonly string[] ForbiddenSceneTokens =
    [
        "addons/terrain_3d",
        "addons/flow_nodes_editor",
        "Terrain3D",
        "FlowGraphNode3D",
        "GDExtension",
    ];
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string DerivedSceneRelativePath(Guid planId)
    {
        if (planId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(planId));
        return $"world/generation/artifacts/{planId:N}/derived-world.tscn";
    }

    public static string ValidationReportRelativePath(Guid planId)
    {
        if (planId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(planId));
        return $"world/generation/artifacts/{planId:N}/validation-report.json";
    }

    public static string PromotionManifestRelativePath(Guid areaId)
    {
        if (areaId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(areaId));
        return $"world/generation/promotions/{areaId:N}.json";
    }

    public static ProjectSessionSaveResult SaveValidationReport(
        string workspaceRoot,
        WorldGenerationValidationReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(report);
        var relativePath = ValidationReportRelativePath(report.PlanId);
        string path;
        try
        {
            path = ResolveRelative(workspaceRoot, relativePath);
            if (report.SchemaVersion != WorldGenerationValidationReport.CurrentSchemaVersion)
                throw new NotSupportedException(
                    $"World-generation validation report schema '{report.SchemaVersion}' is unsupported.");
            AtomicWorkspaceFile.Write(path, JsonSerializer.Serialize(report, Options));
            return new(true, []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            return new(false,
                [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, relativePath)]);
        }
    }

    public static WorldGenerationPromotionInspectionResult Inspect(
        string workspaceRoot,
        WorldGenerationJob job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(job);
        var reportRelative = ValidationReportRelativePath(job.PlanId);
        var sceneRelative = DerivedSceneRelativePath(job.PlanId);
        try
        {
            var reportPath = ResolveRelative(workspaceRoot, reportRelative);
            if (!File.Exists(reportPath))
                throw new FileNotFoundException("World-generation validation report is missing.", reportPath);
            var report = JsonSerializer.Deserialize<WorldGenerationValidationReport>(
                    File.ReadAllText(reportPath, Encoding.UTF8), Options)
                ?? throw new JsonException("World-generation validation report is empty.");
            if (report.SchemaVersion != WorldGenerationValidationReport.CurrentSchemaVersion ||
                report.JobId != job.JobId || report.PlanId != job.PlanId ||
                report.RecipeId != job.Recipe.Id || report.AreaId != job.Recipe.AreaId ||
                report.ProjectRevision != job.ProjectRevision || report.RecipeRevision != job.RecipeRevision ||
                !string.Equals(report.RecipeFingerprint, job.RecipeFingerprint, StringComparison.Ordinal) ||
                !string.Equals(report.PlanFingerprint, job.Plan.PlanFingerprint, StringComparison.Ordinal))
                return InspectionFailure(EvidenceMismatchCode,
                    "Validation report does not match the completed job.", reportRelative);

            var neutral = ValidateNeutralScene(workspaceRoot, sceneRelative);
            if (!neutral.Succeeded)
                return new(null, null, neutral.Diagnostics);
            if (!string.Equals(neutral.SceneSha256, report.DerivedSceneSha256, StringComparison.Ordinal))
                return InspectionFailure(EvidenceMismatchCode,
                    "Derived scene SHA-256 does not match the validation report.", sceneRelative);
            return new(report, neutral.SceneSha256, []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            return InspectionFailure(ReadFailedCode, exception.Message, reportRelative);
        }
    }

    public static WorldGenerationNeutralSceneValidationResult ValidateNeutralScene(
        string workspaceRoot,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        try
        {
            if (!IsSafeRelativePath(relativePath) ||
                !relativePath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Generated scene path must be a safe relative .tscn path.",
                    nameof(relativePath));
            var path = ResolveRelative(workspaceRoot, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("Generated scene is missing.", path);
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.UTF8.GetString(bytes);
            var forbidden = ForbiddenSceneTokens.FirstOrDefault(token =>
                text.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (forbidden is not null)
                return new(null,
                    [new ProjectSessionDiagnostic(ProviderDependencyCode,
                        $"Provider-neutral scene still references '{forbidden}'.", relativePath)]);
            return new(Sha256(bytes), []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            return new(null,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, relativePath)]);
        }
    }

    public static WorldGenerationCanonicalExpectationResult ValidateCanonicalExpectation(
        string workspaceRoot,
        Guid areaId,
        string? expectedCanonicalSceneSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var relativePath = PromoteWorldGenerationHandler.CanonicalScenePath(areaId);
        try
        {
            if (expectedCanonicalSceneSha256 is not null && !IsSha256(expectedCanonicalSceneSha256))
                throw new ArgumentException("Expected canonical scene SHA-256 is invalid.",
                    nameof(expectedCanonicalSceneSha256));
            var path = ResolveRelative(workspaceRoot, relativePath);
            if (!File.Exists(path))
            {
                return expectedCanonicalSceneSha256 is null
                    ? new(true, null, [])
                    : new(false, null,
                        [new ProjectSessionDiagnostic(CanonicalConflictCode,
                            "Expected canonical scene fingerprint was supplied but no canonical scene exists.",
                            relativePath)]);
            }
            var neutral = ValidateNeutralScene(workspaceRoot, relativePath);
            if (!neutral.Succeeded)
                return new(false, neutral.SceneSha256, neutral.Diagnostics);
            if (expectedCanonicalSceneSha256 is null ||
                !string.Equals(neutral.SceneSha256, expectedCanonicalSceneSha256, StringComparison.Ordinal))
                return new(false, neutral.SceneSha256,
                    [new ProjectSessionDiagnostic(CanonicalConflictCode,
                        "Canonical scene exists and its current fingerprint was not acknowledged.",
                        relativePath)]);
            return new(true, neutral.SceneSha256, []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            return new(false, null,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, relativePath)]);
        }
    }

    public static WorldGenerationPromotionWriteResult Apply(
        string workspaceRoot,
        WorldGenerationCanonicalPromotion promotion,
        string? expectedCanonicalSceneSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(promotion);
        var patches = new List<WorldGenerationPromotionFilePatch>();
        try
        {
            ValidatePromotion(promotion);
            if (expectedCanonicalSceneSha256 is not null && !IsSha256(expectedCanonicalSceneSha256))
                throw new ArgumentException("Expected canonical scene SHA-256 is invalid.",
                    nameof(expectedCanonicalSceneSha256));
            var sourceValidation = ValidateNeutralScene(workspaceRoot, promotion.DerivedSceneRelativePath);
            if (!sourceValidation.Succeeded)
                return new(false, [], sourceValidation.Diagnostics);
            if (!string.Equals(sourceValidation.SceneSha256, promotion.CanonicalSceneSha256,
                    StringComparison.Ordinal))
                return WriteFailure(EvidenceMismatchCode,
                    "Derived scene changed after validation.", promotion.DerivedSceneRelativePath);

            var canonicalPath = ResolveRelative(workspaceRoot, promotion.CanonicalSceneRelativePath);
            var beforeCanonical = File.Exists(canonicalPath) ? File.ReadAllBytes(canonicalPath) : null;
            if (beforeCanonical is not null)
            {
                var existingSha = Sha256(beforeCanonical);
                if (expectedCanonicalSceneSha256 is null ||
                    !string.Equals(existingSha, expectedCanonicalSceneSha256, StringComparison.Ordinal))
                    return WriteFailure(CanonicalConflictCode,
                        "Canonical scene exists and its current fingerprint was not acknowledged.",
                        promotion.CanonicalSceneRelativePath);
            }
            else if (expectedCanonicalSceneSha256 is not null)
            {
                return WriteFailure(CanonicalConflictCode,
                    "Expected canonical scene fingerprint was supplied but no canonical scene exists.",
                    promotion.CanonicalSceneRelativePath);
            }

            var sourcePath = ResolveRelative(workspaceRoot, promotion.DerivedSceneRelativePath);
            var canonicalBytes = File.ReadAllBytes(sourcePath);
            var manifestRelative = PromotionManifestRelativePath(promotion.AreaId);
            var manifestPath = ResolveRelative(workspaceRoot, manifestRelative);
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(promotion, Options));
            patches.Add(new(promotion.CanonicalSceneRelativePath, beforeCanonical, canonicalBytes));
            patches.Add(new(manifestRelative,
                File.Exists(manifestPath) ? File.ReadAllBytes(manifestPath) : null,
                manifestBytes));

            foreach (var patch in patches)
                WriteBytesAtomically(ResolveRelative(workspaceRoot, patch.RelativePath), patch.After);
            return new(true, patches.ToArray(), []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            try
            {
                Restore(workspaceRoot, patches, useAfter: false);
            }
            catch
            {
                // Preserve the original write failure; snapshots remain available to the caller.
            }
            return WriteFailure(WriteFailedCode, exception.Message,
                promotion.CanonicalSceneRelativePath, patches);
        }
    }

    public static WorldGenerationPromotionLoadResult Load(string workspaceRoot, Guid areaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var relativePath = PromotionManifestRelativePath(areaId);
        try
        {
            var path = ResolveRelative(workspaceRoot, relativePath);
            if (!File.Exists(path)) return new(null, null, true, []);
            var promotion = JsonSerializer.Deserialize<WorldGenerationCanonicalPromotion>(
                    File.ReadAllText(path, Encoding.UTF8), Options)
                ?? throw new JsonException("World-generation promotion manifest is empty.");
            ValidatePromotion(promotion);
            if (promotion.AreaId != areaId)
                throw new InvalidDataException("Promotion manifest area identity is invalid.");
            var scene = ValidateNeutralScene(workspaceRoot, promotion.CanonicalSceneRelativePath);
            if (!scene.Succeeded)
                return new(null, null, false, scene.Diagnostics);
            if (!string.Equals(scene.SceneSha256, promotion.CanonicalSceneSha256, StringComparison.Ordinal))
                return new(null, scene.SceneSha256, false,
                    [new ProjectSessionDiagnostic(EvidenceMismatchCode,
                        "Canonical scene does not match its promotion manifest.",
                        promotion.CanonicalSceneRelativePath)]);
            return new(promotion, scene.SceneSha256, false, []);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            return new(null, null, false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, relativePath)]);
        }
    }

    public static void Restore(
        string workspaceRoot,
        IReadOnlyList<WorldGenerationPromotionFilePatch> patches,
        bool useAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(patches);
        foreach (var patch in patches.Reverse())
        {
            if (!IsSafeRelativePath(patch.RelativePath))
                throw new ArgumentException("Promotion patch path is unsafe.", nameof(patches));
            var path = ResolveRelative(workspaceRoot, patch.RelativePath);
            var content = useAfter ? patch.After : patch.Before;
            if (content is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                WriteBytesAtomically(path, content);
            }
        }
    }

    private static void ValidatePromotion(WorldGenerationCanonicalPromotion promotion)
    {
        if (promotion.SchemaVersion != WorldGenerationCanonicalPromotion.CurrentSchemaVersion ||
            promotion.PromotionId == Guid.Empty || promotion.TransactionId == Guid.Empty ||
            promotion.JobId == Guid.Empty || promotion.PlanId == Guid.Empty ||
            promotion.RecipeId == Guid.Empty || promotion.AreaId == Guid.Empty ||
            promotion.BaseProjectRevision < 0 || promotion.NewProjectRevision != promotion.BaseProjectRevision + 1 ||
            promotion.RecipeRevision < 0 || !IsSha256(promotion.RecipeFingerprint) ||
            !IsSha256(promotion.PlanFingerprint) || !IsSha256(promotion.CanonicalSceneSha256) ||
            !IsSha256(promotion.ContentFingerprint) || !IsSha256(promotion.AssetSelectionFingerprint) ||
            !string.Equals(promotion.DerivedSceneRelativePath,
                DerivedSceneRelativePath(promotion.PlanId), StringComparison.Ordinal) ||
            !string.Equals(promotion.CanonicalSceneRelativePath,
                PromoteWorldGenerationHandler.CanonicalScenePath(promotion.AreaId), StringComparison.Ordinal) ||
            promotion.Providers is null || promotion.Providers.Count == 0 ||
            promotion.ProtectedNodePaths is null || string.IsNullOrWhiteSpace(promotion.AcceptanceProvenance))
            throw new InvalidDataException("World-generation canonical promotion manifest is invalid.");
    }

    private static WorldGenerationPromotionInspectionResult InspectionFailure(
        string code,
        string message,
        string path) =>
        new(null, null, [new ProjectSessionDiagnostic(code, message, path)]);

    private static WorldGenerationPromotionWriteResult WriteFailure(
        string code,
        string message,
        string path,
        IReadOnlyList<WorldGenerationPromotionFilePatch>? patches = null) =>
        new(false, patches ?? [], [new ProjectSessionDiagnostic(code, message, path)]);

    private static string ResolveRelative(string workspaceRoot, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
            throw new ArgumentException("World-generation promotion path is unsafe.", nameof(relativePath));
        return WorldGenerationWorkspacePath.Resolve(
            workspaceRoot,
            relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsSafeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 ||
            value.StartsWith('/') || value.StartsWith('\\') || value.Contains(':') || value.IndexOf('\0') >= 0)
            return false;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment is not "." and not ".." &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static void WriteBytesAtomically(string targetPath, byte[] content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("Promotion path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static bool IsStoreException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or
            ArgumentException or InvalidOperationException;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed record WorldGenerationPromotionInspectionResult(
    WorldGenerationValidationReport? Report,
    string? SceneSha256,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Report is not null && SceneSha256 is not null && Diagnostics.Count == 0;
}

public sealed record WorldGenerationNeutralSceneValidationResult(
    string? SceneSha256,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => SceneSha256 is not null && Diagnostics.Count == 0;
}

public sealed record WorldGenerationCanonicalExpectationResult(
    bool Succeeded,
    string? CurrentSceneSha256,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics);

public sealed record WorldGenerationPromotionFilePatch(
    string RelativePath,
    byte[]? Before,
    byte[] After);

public sealed record WorldGenerationPromotionWriteResult(
    bool Succeeded,
    IReadOnlyList<WorldGenerationPromotionFilePatch> Patches,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics);

public sealed record WorldGenerationPromotionLoadResult(
    WorldGenerationCanonicalPromotion? Promotion,
    string? SceneSha256,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Promotion is not null && SceneSha256 is not null && Diagnostics.Count == 0;
}
