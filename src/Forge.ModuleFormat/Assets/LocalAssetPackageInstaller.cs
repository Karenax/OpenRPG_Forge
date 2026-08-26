using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public static class LocalAssetPackageInstaller
{
    public const string LicenseInvalidCode = "asset.local_install.license_invalid";
    public const string ModelMissingCode = "asset.local_install.model_missing";
    public const string ModelFormatUnsupportedCode = "asset.local_install.model_format_unsupported";
    public const string SourceCatalogMissingCode = "asset.local_install.source_catalog_missing";
    public const string SourcePathUnsafeCode = "asset.local_install.source_path_unsafe";
    public const string SourceHashMismatchCode = "asset.local_install.source_hash_mismatch";
    public const string DependencyPathUnsafeCode = "asset.local_install.dependency_path_unsafe";
    public const string DependencyMissingCode = "asset.local_install.dependency_missing";
    public const string CollisionCode = "asset.local_install.collision";
    public const string ApplyFailedCode = "asset.local_install.apply_failed";
    public const string RevertFailedCode = "asset.local_install.revert_failed";

    public static LocalAssetPackagePlanResult Plan(
        string repositoryRoot,
        string vaultRoot,
        string workspaceRoot,
        AssetCatalogItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(item);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        vaultRoot = Path.GetFullPath(vaultRoot);
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        var diagnostics = new List<DomainDiagnostic>();

        if (!item.LicenseVerified || !IsAcceptedLicense(item.LicenseSpdx))
        {
            diagnostics.Add(new(
                LicenseInvalidCode,
                $"Asset '{item.ExternalId}' requires a verified CC0 license before local installation.",
                $"assets/{item.Id}/license"));
            return Failure(diagnostics);
        }

        if (!TryResolveContained(vaultRoot, item.RelativeModelPath, out var modelSource) ||
            !File.Exists(modelSource))
        {
            diagnostics.Add(new(
                ModelMissingCode,
                $"Catalog model '{item.RelativeModelPath}' is unavailable in the local Asset Vault.",
                $"assets/{item.Id}/relativeModelPath"));
            return Failure(diagnostics);
        }

        var modelExtension = Path.GetExtension(modelSource);
        if (!modelExtension.Equals(".gltf", StringComparison.OrdinalIgnoreCase) &&
            !modelExtension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(
                ModelFormatUnsupportedCode,
                "Local blueprint installation requires a governed glTF or GLB model.",
                $"assets/{item.Id}/relativeModelPath"));
            return Failure(diagnostics);
        }

        ValidateDeclaredSources(repositoryRoot, vaultRoot, item, diagnostics);
        if (diagnostics.Count > 0) return Failure(diagnostics);

        var sources = new List<(string Absolute, string DestinationName, AssetChunkRole Role)>
        {
            (modelSource, Path.GetFileName(modelSource), AssetChunkRole.Model),
        };
        if (modelExtension.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            CloseGltfDependencies(vaultRoot, modelSource, sources, diagnostics);
            if (diagnostics.Count > 0) return Failure(diagnostics);
        }

        var installRoot = $"assets/imported/{item.Id}";
        var files = sources
            .DistinctBy(source => source.Absolute, StringComparer.OrdinalIgnoreCase)
            .Select(source => CreateFile(vaultRoot, installRoot, source))
            .ToArray();
        foreach (var file in files)
        {
            if (!TryResolveContained(workspaceRoot, file.DestinationRelativePath, out var destination))
            {
                diagnostics.Add(new(
                    SourcePathUnsafeCode,
                    $"Installation target '{file.DestinationRelativePath}' escapes the workspace.",
                    file.DestinationRelativePath));
                continue;
            }

            if (File.Exists(destination) && !Matches(destination, file.ContentSha256, file.Bytes))
            {
                diagnostics.Add(new(
                    CollisionCode,
                    $"Installation target '{file.DestinationRelativePath}' already contains different content.",
                    file.DestinationRelativePath));
            }
        }

        if (diagnostics.Count > 0) return Failure(diagnostics);
        var chunks = files.Select(file => new AssetManifestChunk(
            StableGuid($"{item.Id}|{file.DestinationRelativePath}"),
            file.Role,
            true,
            file.DestinationRelativePath,
            file.ContentSha256,
            file.Bytes,
            file.Bytes,
            AssetChunkCompression.None,
            [AssetTargetPlatform.Any])).ToArray();
        var totalBytes = Math.Max(1, chunks.Sum(chunk => chunk.StoredBytes));
        var manifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(item.Id),
            item.Id,
            $"asset-vault:{item.ExternalId}",
            item.LicenseSpdx,
            item.ContentSha256,
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = totalBytes },
            chunks,
            null,
            null,
            null,
            null);
        diagnostics.AddRange(AssetPackageManifestValidator.Validate(manifest));
        if (diagnostics.Count > 0) return Failure(diagnostics);
        var reused = files.All(file => TryResolveContained(
                workspaceRoot, file.DestinationRelativePath, out var destination) &&
            File.Exists(destination) && Matches(destination, file.ContentSha256, file.Bytes));
        return new(true, new LocalAssetPackageInstallPlan(item.Id, manifest, files, reused), []);
    }

    public static LocalAssetPackageApplyResult Apply(
        string vaultRoot,
        string workspaceRoot,
        LocalAssetPackageInstallPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(plan);
        vaultRoot = Path.GetFullPath(vaultRoot);
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        var created = new List<string>();
        var staging = Path.Combine(workspaceRoot, ".forge", ".asset-install-staging", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var staged = new List<(LocalAssetPackageFile File, string Path)>();
            foreach (var file in plan.Files)
            {
                if (!TryResolveContained(vaultRoot, file.SourceRelativePath, out var source) ||
                    !File.Exists(source) || !Matches(source, file.ContentSha256, file.Bytes))
                {
                    throw new LocalAssetApplyException(SourceHashMismatchCode,
                        $"Governed source '{file.SourceRelativePath}' changed after planning.",
                        file.SourceRelativePath);
                }

                var stagedPath = Path.Combine(staging, staged.Count.ToString("D4"));
                File.Copy(source, stagedPath);
                staged.Add((file, stagedPath));
            }

            foreach (var item in staged)
            {
                if (!TryResolveContained(workspaceRoot, item.File.DestinationRelativePath, out var destination))
                {
                    throw new LocalAssetApplyException(SourcePathUnsafeCode,
                        $"Target '{item.File.DestinationRelativePath}' escapes the workspace.",
                        item.File.DestinationRelativePath);
                }

                if (File.Exists(destination))
                {
                    if (!Matches(destination, item.File.ContentSha256, item.File.Bytes))
                    {
                        throw new LocalAssetApplyException(CollisionCode,
                            $"Target '{item.File.DestinationRelativePath}' collides with different content.",
                            item.File.DestinationRelativePath);
                    }
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(item.Path, destination);
                created.Add(item.File.DestinationRelativePath);
            }

            return new(true, created, []);
        }
        catch (LocalAssetApplyException exception)
        {
            foreach (var relative in created.AsEnumerable().Reverse())
            {
                if (TryResolveContained(workspaceRoot, relative, out var path) && File.Exists(path)) File.Delete(path);
            }
            return new(false, [], [new DomainDiagnostic(exception.Code, exception.Message, exception.Path)]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            foreach (var relative in created.AsEnumerable().Reverse())
            {
                if (TryResolveContained(workspaceRoot, relative, out var path) && File.Exists(path)) File.Delete(path);
            }
            return new(false, [], [new DomainDiagnostic(ApplyFailedCode, exception.Message, "assets/install")]);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private sealed class LocalAssetApplyException(string code, string message, string path)
        : Exception(message)
    {
        public string Code { get; } = code;
        public string Path { get; } = path;
    }

    public static LocalAssetPackageRevertResult Revert(
        string workspaceRoot,
        LocalAssetPackageApplyResult execution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(execution);
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        try
        {
            foreach (var relative in execution.CreatedRelativePaths.Reverse())
            {
                if (!TryResolveContained(workspaceRoot, relative, out var path))
                {
                    throw new InvalidDataException($"Revert target '{relative}' escapes the workspace.");
                }
                if (File.Exists(path)) File.Delete(path);
            }
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new(false, [new DomainDiagnostic(RevertFailedCode, exception.Message, "assets/install")]);
        }
    }

    private static void ValidateDeclaredSources(
        string repositoryRoot,
        string vaultRoot,
        AssetCatalogItem item,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var catalogDirectory = Path.Combine(repositoryRoot, "asset_manifests", "catalogs");
        JsonElement? matched = null;
        IEnumerable<string> catalogPaths = Directory.Exists(catalogDirectory)
            ? Directory.EnumerateFiles(catalogDirectory, "*.json").OrderBy(value => value, StringComparer.Ordinal)
            : Array.Empty<string>();
        foreach (var path in catalogPaths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            foreach (var collectionName in new[] { "assets", "packs" })
            {
                if (!root.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var entry in collection.EnumerateArray())
                {
                    var identifierName = collectionName == "assets" ? "asset_id" : "pack_id";
                    var identifier = entry.TryGetProperty(identifierName, out var id) ? id.GetString() : null;
                    if (identifier is not null &&
                        (string.Equals(identifier, item.ExternalId, StringComparison.OrdinalIgnoreCase) ||
                         item.ExternalId.StartsWith(identifier + ":", StringComparison.OrdinalIgnoreCase)))
                    {
                        matched = entry.Clone();
                    }
                }
            }
        }

        if (matched is null || !matched.Value.TryGetProperty("files", out var files) ||
            files.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new(SourceCatalogMissingCode,
                $"Catalog source entry for '{item.ExternalId}' was not found.", $"assets/{item.Id}/source"));
            return;
        }

        foreach (var file in files.EnumerateArray())
        {
            var relative = file.TryGetProperty("path", out var pathValue) ? pathValue.GetString() : null;
            var expected = file.TryGetProperty("sha256", out var hashValue) ? hashValue.GetString() : null;
            if (!TryResolveContained(vaultRoot, relative, out var source))
            {
                diagnostics.Add(new(SourcePathUnsafeCode,
                    $"Catalog source path '{relative}' is unsafe.", $"assets/{item.Id}/source"));
            }
            else if (!File.Exists(source) || expected is not { Length: 64 } ||
                     !Matches(source, expected, new FileInfo(source).Length))
            {
                diagnostics.Add(new(SourceHashMismatchCode,
                    $"Catalog source '{relative}' is missing or failed SHA-256 verification.",
                    $"assets/{item.Id}/source"));
            }
        }
    }

    private static void CloseGltfDependencies(
        string vaultRoot,
        string modelSource,
        ICollection<(string Absolute, string DestinationName, AssetChunkRole Role)> sources,
        ICollection<DomainDiagnostic> diagnostics)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(modelSource));
        foreach (var property in new[] { "buffers", "images" })
        {
            if (!document.RootElement.TryGetProperty(property, out var values) ||
                values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var value in values.EnumerateArray())
            {
                var uri = value.TryGetProperty("uri", out var uriValue) ? uriValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(uri) || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (Uri.TryCreate(uri, UriKind.Absolute, out _) || uri.Contains('\\') ||
                    uri.Split('/').Any(segment => segment is "" or "." or ".."))
                {
                    diagnostics.Add(new(DependencyPathUnsafeCode,
                        $"glTF dependency URI '{uri}' is not a safe local relative path.", uri));
                    continue;
                }

                var decoded = Uri.UnescapeDataString(uri);
                var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(modelSource)!,
                    decoded.Replace('/', Path.DirectorySeparatorChar)));
                if (!TryMakeRelative(vaultRoot, candidate, out _) || !File.Exists(candidate))
                {
                    diagnostics.Add(new(DependencyMissingCode,
                        $"glTF dependency '{uri}' is missing from the governed Asset Vault.", uri));
                    continue;
                }

                sources.Add((candidate, decoded.Replace('\\', '/'), AssetChunkRole.Texture));
            }
        }
    }

    private static LocalAssetPackageFile CreateFile(
        string vaultRoot,
        string installRoot,
        (string Absolute, string DestinationName, AssetChunkRole Role) source)
    {
        TryMakeRelative(vaultRoot, source.Absolute, out var sourceRelative);
        var destination = $"{installRoot}/{source.DestinationName}".Replace('\\', '/');
        var info = new FileInfo(source.Absolute);
        return new(sourceRelative!, destination, Hash(source.Absolute), info.Length, source.Role);
    }

    private static LocalAssetPackagePlanResult Failure(IReadOnlyList<DomainDiagnostic> diagnostics) =>
        new(false, null, diagnostics);

    private static bool IsAcceptedLicense(string value) =>
        value.Equals("CC0-1.0", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("CC0-1.0-Universal", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveContained(string root, string? relative, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return false;
        var normalized = relative.Replace('\\', '/');
        if (normalized.Split('/').Any(segment => segment is "" or "." or "..")) return false;
        root = Path.GetFullPath(root);
        path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMakeRelative(string root, string candidate, out string? relative)
    {
        root = Path.GetFullPath(root);
        candidate = Path.GetFullPath(candidate);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            relative = null;
            return false;
        }
        relative = Path.GetRelativePath(root, candidate).Replace('\\', '/');
        return true;
    }

    private static bool Matches(string path, string expectedHash, long expectedBytes) =>
        new FileInfo(path).Length == expectedBytes &&
        string.Equals(Hash(path), expectedHash, StringComparison.OrdinalIgnoreCase);

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:local-asset:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record LocalAssetPackageFile(
    string SourceRelativePath,
    string DestinationRelativePath,
    string ContentSha256,
    long Bytes,
    AssetChunkRole Role);

public sealed record LocalAssetPackageInstallPlan(
    AssetCatalogItemId AssetId,
    AssetPackageManifest Manifest,
    IReadOnlyList<LocalAssetPackageFile> Files,
    bool Reused);

public sealed record LocalAssetPackagePlanResult(
    bool Succeeded,
    LocalAssetPackageInstallPlan? Plan,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record LocalAssetPackageApplyResult(
    bool Succeeded,
    IReadOnlyList<string> CreatedRelativePaths,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record LocalAssetPackageRevertResult(
    bool Succeeded,
    IReadOnlyList<DomainDiagnostic> Diagnostics);
