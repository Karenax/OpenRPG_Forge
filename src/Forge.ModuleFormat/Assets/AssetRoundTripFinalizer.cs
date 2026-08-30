using System.Security.Cryptography;
using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Assets;

public static class AssetRoundTripFinalizer
{
    public const string ResponseRelativePath = "assets/roundtrip/worker-response.json";
    public const string ResponseInvalidCode = "asset.roundtrip.response_invalid";
    public const string GovernanceMismatchCode = "asset.roundtrip.governance_mismatch";
    public const string ArtifactInvalidCode = "asset.roundtrip.artifact_invalid";
    public const string QualityInvalidCode = "asset.roundtrip.quality_invalid";

    public static AssetRoundTripFinalizeResult Finalize(
        string workspaceRoot,
        AssetCatalogItem item,
        AssetPackageManifest manifest,
        AssetExternalEditConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);

        var root = Path.GetFullPath(workspaceRoot);
        try
        {
            if (item.Id != manifest.AssetId || item.Id != configuration.AssetId ||
                manifest.Id != configuration.ManifestId ||
                !string.Equals(item.LicenseSpdx, configuration.LicenseSpdx, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    GovernanceMismatchCode,
                    "Catalog item, manifest and external edit governance do not describe the same asset.",
                    $"assets/{item.Id}");
            }

            var responsePath = ResolveConfinedPath(root, ResponseRelativePath);
            if (!File.Exists(responsePath))
            {
                return Failure(ResponseInvalidCode, "Blender worker response is missing.", ResponseRelativePath);
            }

            var response = JsonSerializer.Deserialize<WorkerResponse>(File.ReadAllText(responsePath), JsonOptions);
            if (response is null || !response.Valid || !response.Executed ||
                response.SchemaVersion != BlenderWorkerRequestStore.CurrentSchemaVersion ||
                response.JobId != configuration.Id.Value ||
                !string.Equals(response.SourceSha256, configuration.SourceContentSha256, StringComparison.OrdinalIgnoreCase) ||
                !SameRelativePath(response.OutputRelativePath, configuration.OutputRelativePath) ||
                response.OutputBytes <= 0 || !IsSha256(response.OutputSha256))
            {
                return Failure(
                    ResponseInvalidCode,
                    "Blender worker response does not match the governed round-trip request.",
                    ResponseRelativePath);
            }

            var output = ValidateArtifact(
                root,
                response.OutputRelativePath,
                response.OutputSha256,
                response.OutputBytes,
                "GLB output");
            if (output is not null)
            {
                return output;
            }

            if (response.Preview is null || response.Preview.Width != 512 || response.Preview.Height != 512 ||
                response.Preview.Bytes <= 0 || !IsSha256(response.Preview.Sha256))
            {
                return Failure(QualityInvalidCode, "A governed 512 x 512 preview is required.", ResponseRelativePath);
            }

            var preview = ValidateArtifact(
                root,
                response.Preview.RelativePath,
                response.Preview.Sha256,
                response.Preview.Bytes,
                "preview");
            if (preview is not null)
            {
                return preview;
            }

            if (response.Quality is null || !response.Quality.Passed || response.Quality.Scores is null ||
                response.Quality.Scores.Overall is < 0 or > 100 ||
                !AssetCatalogValidator.IsSafeRelativePath(response.QualityReportRelativePath))
            {
                return Failure(QualityInvalidCode, "The Blender quality gate did not pass.", ResponseRelativePath);
            }

            var qualityPath = ResolveConfinedPath(root, response.QualityReportRelativePath);
            if (!File.Exists(qualityPath))
            {
                return Failure(QualityInvalidCode, "The governed quality report is missing.", response.QualityReportRelativePath);
            }

            var quality = JsonSerializer.Deserialize<QualityReport>(File.ReadAllText(qualityPath), JsonOptions);
            if (quality is null || quality.SchemaVersion != 1 || !quality.Passed || quality.Metrics is null ||
                quality.Scores is null || quality.Scores.Overall != response.Quality.Scores.Overall ||
                !string.Equals(quality.AssetId, item.Id.ToString(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(quality.SourceSha256, configuration.SourceContentSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(quality.LicenseSpdx, configuration.LicenseSpdx, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(quality.Provenance, configuration.ModificationProvenance, StringComparison.Ordinal) ||
                !ValidMetrics(quality.Metrics) || quality.Scores.Overall is < 0 or > 100)
            {
                return Failure(
                    GovernanceMismatchCode,
                    "Quality report metrics or governance do not match the external edit configuration.",
                    response.QualityReportRelativePath);
            }

            var modelChunk = manifest.Chunks.SingleOrDefault(chunk => chunk.Role == AssetChunkRole.Model);
            if (modelChunk is null)
            {
                return Failure(ArtifactInvalidCode, "Manifest requires exactly one model chunk.", $"assets/manifests/{manifest.Id}");
            }

            var outputHash = response.OutputSha256.ToLowerInvariant();
            var updatedChunk = modelChunk with
            {
                RelativePath = NormalizeRelativePath(response.OutputRelativePath),
                ContentSha256 = outputHash,
                UncompressedBytes = response.OutputBytes,
                StoredBytes = response.OutputBytes,
                Compression = AssetChunkCompression.None,
            };
            var chunks = manifest.Chunks
                .Select(chunk => chunk.Id == modelChunk.Id ? updatedChunk : chunk)
                .ToArray();
            var platforms = configuration.TargetPlatforms.Distinct().Order().ToArray();
            var styles = item.Categories
                .Concat(item.Tags)
                .Append(item.Source)
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value => value.Any(char.IsLetter))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(24)
                .ToArray();
            var metrics = quality.Metrics;
            var metadata = new AssetModelMetadata(
                new AssetModelDimensions(
                    metrics.DimensionsMeters[0],
                    metrics.DimensionsMeters[2],
                    metrics.DimensionsMeters[1]),
                new AssetModelMetrics(
                    metrics.BaseMeshCount,
                    metrics.VertexCount,
                    metrics.TriangleCount,
                    metrics.MaterialCount,
                    metrics.TextureCount,
                    metrics.LodMeshCount,
                    metrics.CollisionMeshCount,
                    quality.Scores.Overall),
                styles,
                [new AssetVariantManifest("default", item.DisplayName, modelChunk.Id, platforms, styles)],
                new AssetCompatibilityManifest(
                    "pbr-metallic-roughness",
                    ResolveRealism(item.Source),
                    1m,
                    1024,
                    platforms,
                    RuntimeImportVerified: false),
                NormalizeRelativePath(response.Preview.RelativePath),
                response.Preview.Sha256.ToLowerInvariant(),
                response.Preview.Bytes,
                NormalizeRelativePath(response.QualityReportRelativePath),
                HashFileSha256(qualityPath));
            var intelligence = manifest.Intelligence ?? item.Intelligence;
            if (metrics.BoundsMinMeters is { Length: 3 } boundsMinimum &&
                metrics.BoundsMaxMeters is { Length: 3 } boundsMaximum)
            {
                // Blender audits Z-up coordinates. The governed GLB export is Y-up and maps
                // Blender (x, y, z) to Forge (x, z, -y).
                var bounds = new AssetMetadataBounds(
                    new AssetMetadataVector3(
                        (double)boundsMinimum[0],
                        (double)boundsMinimum[2],
                        (double)-boundsMaximum[1]),
                    new AssetMetadataVector3(
                        (double)boundsMaximum[0],
                        (double)boundsMaximum[2],
                        (double)-boundsMinimum[1]));
                intelligence = AssetIntelligenceProfileAnalyzer.WithBlenderGeometry(
                    intelligence,
                    bounds,
                    new AssetIntelligenceTechnicalProfile(
                        metrics.BaseMeshCount,
                        metrics.VertexCount,
                        metrics.TriangleCount,
                        metrics.MaterialCount,
                        metrics.TextureCount,
                        metrics.ArmatureCount,
                        metrics.BoneCount,
                        metrics.AnimationClipCount,
                        item.TechnicalMetadata.Materials,
                        metrics.AnimationClips ?? item.TechnicalMetadata.Animations),
                    metrics.PivotClass ?? "unknown");
            }
            var updated = manifest with
            {
                Chunks = chunks,
                Collision = manifest.Collision is null
                    ? null
                    : manifest.Collision with
                    {
                        ChunkId = modelChunk.Id,
                        Generation = AssetCollisionGeneration.Embedded,
                    },
                Lod = manifest.Lod is null
                    ? null
                    : manifest.Lod with
                    {
                        Levels = manifest.Lod.Levels
                            .Select(level => level with
                            {
                                ChunkId = modelChunk.Id,
                                MaximumTriangles = Math.Max(
                                    1,
                                    (int)Math.Ceiling(metrics.TriangleCount * level.ScreenRatio)),
                            })
                            .ToArray(),
                    },
                ModelMetadata = metadata,
                Intelligence = intelligence,
            };
            var diagnostics = AssetPackageManifestValidator.Validate(updated);
            if (diagnostics.Count > 0)
            {
                return Failure(
                    QualityInvalidCode,
                    $"Finalized manifest is invalid: {diagnostics[0].Code} - {diagnostics[0].Message}",
                    diagnostics[0].Path);
            }

            return new(updated, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OverflowException)
        {
            return Failure(ResponseInvalidCode, exception.Message, ResponseRelativePath);
        }
    }

    private static AssetRoundTripFinalizeResult? ValidateArtifact(
        string root,
        string relativePath,
        string expectedHash,
        long expectedBytes,
        string label)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            return Failure(ArtifactInvalidCode, $"The {label} path is unsafe.", relativePath);
        }

        var path = ResolveConfinedPath(root, relativePath);
        if (!File.Exists(path) || new FileInfo(path).Length != expectedBytes ||
            !string.Equals(HashFileSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ArtifactInvalidCode, $"The {label} hash or size is invalid.", relativePath);
        }

        return null;
    }

    private static bool ValidMetrics(QualityMetrics metrics) =>
        metrics.DimensionsMeters is { Length: 3 } &&
        metrics.DimensionsMeters.All(value => value is > 0 and <= 1000) &&
        metrics.BaseMeshCount > 0 && metrics.VertexCount > 0 && metrics.TriangleCount > 0 &&
        metrics.MaterialCount >= 0 && metrics.TextureCount >= 0 && metrics.LodMeshCount >= 0 &&
        metrics.CollisionMeshCount >= 0;

    private static ArtisticRealism ResolveRealism(string source) => source.Trim().ToLowerInvariant() switch
    {
        "ambientcg" or "polyhaven" => ArtisticRealism.Realistic,
        "kenney" => ArtisticRealism.Stylized,
        _ => ArtisticRealism.StylizedRealism,
    };

    private static string ResolveConfinedPath(string root, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            throw new ArgumentException("Asset path is not a safe relative path.", nameof(relativePath));
        }

        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, path);
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Asset path escapes the workspace.", nameof(relativePath));
        }

        return path;
    }

    private static bool SameRelativePath(string? left, string? right) =>
        left is not null && right is not null &&
        string.Equals(NormalizeRelativePath(left), NormalizeRelativePath(right), StringComparison.Ordinal);

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/').Trim();

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string HashFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static AssetRoundTripFinalizeResult Failure(string code, string message, string path) =>
        new(null, [new(code, message, path)]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record WorkerResponse(
        bool Valid,
        bool Executed,
        int SchemaVersion,
        Guid JobId,
        string SourceSha256,
        string OutputRelativePath,
        string OutputSha256,
        long OutputBytes,
        WorkerPreview? Preview,
        WorkerQuality? Quality,
        string QualityReportRelativePath);

    private sealed record WorkerPreview(
        string RelativePath,
        string Sha256,
        long Bytes,
        int Width,
        int Height);

    private sealed record WorkerQuality(bool Passed, QualityScores? Scores);

    private sealed record QualityReport(
        int SchemaVersion,
        string AssetId,
        string SourceSha256,
        string LicenseSpdx,
        string Provenance,
        bool Passed,
        QualityMetrics? Metrics,
        QualityScores? Scores);

    private sealed record QualityMetrics(
        int BaseMeshCount,
        int LodMeshCount,
        int CollisionMeshCount,
        int VertexCount,
        int TriangleCount,
        int MaterialCount,
        int TextureCount,
        decimal[] DimensionsMeters,
        decimal[]? BoundsMinMeters = null,
        decimal[]? BoundsMaxMeters = null,
        string? PivotClass = null,
        string? PivotPolicy = null,
        int? ArmatureCount = null,
        int? BoneCount = null,
        int? AnimationClipCount = null,
        string[]? AnimationClips = null);

    private sealed record QualityScores(int Overall);
}

public sealed record AssetRoundTripFinalizeDiagnostic(string Code, string Message, string Path);

public sealed record AssetRoundTripFinalizeResult(
    AssetPackageManifest? Manifest,
    IReadOnlyList<AssetRoundTripFinalizeDiagnostic> Diagnostics)
{
    public bool Succeeded => Manifest is not null && Diagnostics.Count == 0;
}
