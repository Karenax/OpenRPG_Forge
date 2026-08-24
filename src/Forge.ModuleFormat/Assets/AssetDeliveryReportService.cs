using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Assets;

public static class AssetDeliveryReportService
{
    public const int CurrentSchemaVersion = 1;

    public sealed record Policy(
        int MinimumCatalogAssets,
        int MaximumCatalogAssets,
        int ExpectedManifestCount,
        int ExpectedAssetReferenceCount,
        int ExpectedBenchmarkCheckCount,
        string RequiredLicenseSpdx)
    {
        public static Policy ForgottenForestAv5 { get; } = new(665, 665, 28, 44, 16, "CC0-1.0");
        public static Policy ForgottenForestAv6E { get; } = new(673, 673, 129, 809, 16, "CC0-1.0");
    }

    public sealed record FileMetrics(int Files, long Bytes);

    public sealed record CatalogMetrics(
        string CatalogDate,
        int Assets,
        int LocalAssets,
        int CatalogOnlyMediaAssets,
        int ExtractedAssets,
        int LicenseVerifiedAssets,
        long DeclaredBytes,
        IReadOnlyDictionary<string, int> Sources,
        IReadOnlyDictionary<string, int> Licenses);

    public sealed record VaultMetrics(
        string Root,
        FileMetrics Originals,
        FileMetrics Normalized,
        FileMetrics Metadata,
        FileMetrics Total);

    public sealed record ModuleMetrics(
        string Workspace,
        int WorkspaceFiles,
        long WorkspaceBytes,
        int Manifests,
        int Chunks,
        long GovernedStoredBytes,
        int AssetReferences,
        int ReferencedManifestAssets,
        int UngovernedReferences,
        int ChunkIntegrityFailures,
        IReadOnlyDictionary<string, int> Licenses,
        string Package,
        long PackageBytes,
        string PackageSha256);

    public sealed record PerformanceMetrics(
        string MeasuredAtUtc,
        bool Passed,
        int Checks,
        int TerrainVertices,
        int TerrainTriangles,
        int CollisionShapes,
        int ScatterInstances,
        int NavigationPolygons,
        int LodAssets,
        int LodLevels,
        int CullingGroups,
        long GovernedAssetBytes,
        long ManagedAllocatedBytes,
        long ProcessWorkingSetDeltaBytes,
        double ReconstructionMilliseconds,
        double CollisionMilliseconds,
        double NavigationMilliseconds,
        double SculptureMilliseconds,
        double NavigationPathMilliseconds);

    public sealed record Check(string Key, string Expected, string Actual, bool Passed);

    public sealed record Report(
        int SchemaVersion,
        string Scope,
        CatalogMetrics Catalog,
        VaultMetrics Vault,
        ModuleMetrics Module,
        PerformanceMetrics Performance,
        IReadOnlyList<Check> Checks,
        bool Passed,
        string Provenance);

    public static Report Measure(
        AssetCatalog catalog,
        string vaultRoot,
        string moduleWorkspace,
        string modulePackagePath,
        string terrainBenchmarkPath,
        Policy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        policy ??= Policy.ForgottenForestAv6E;
        ValidatePolicy(policy);

        vaultRoot = RequireDirectory(vaultRoot, nameof(vaultRoot));
        moduleWorkspace = RequireDirectory(moduleWorkspace, nameof(moduleWorkspace));
        modulePackagePath = RequireFile(modulePackagePath, nameof(modulePackagePath));
        terrainBenchmarkPath = RequireFile(terrainBenchmarkPath, nameof(terrainBenchmarkPath));

        var catalogMetrics = MeasureCatalog(catalog);
        var vaultMetrics = MeasureVault(vaultRoot);
        var moduleMetrics = MeasureModule(moduleWorkspace, modulePackagePath);
        var performanceMetrics = MeasurePerformance(terrainBenchmarkPath);
        var checks = BuildChecks(policy, catalogMetrics, vaultMetrics, moduleMetrics, performanceMetrics);

        return new Report(
            CurrentSchemaVersion,
            "forgotten-forest-av6e",
            catalogMetrics,
            vaultMetrics,
            moduleMetrics,
            performanceMetrics,
            checks,
            checks.All(check => check.Passed),
            "measured:openrpg-forge:asset-delivery-report@2");
    }

    public static string RenderMarkdown(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.AppendLine("# Rapport de livraison Asset Vault et Forgotten Forest");
        builder.AppendLine();
        builder.AppendLine($"- Schéma : `{report.SchemaVersion}`");
        builder.AppendLine($"- Résultat : **{(report.Passed ? "PASS" : "FAIL")}**");
        builder.AppendLine($"- Provenance : `{report.Provenance}`");
        builder.AppendLine();
        builder.AppendLine("## Mesures réelles");
        builder.AppendLine();
        builder.AppendLine("| Domaine | Mesure | Valeur |");
        builder.AppendLine("| --- | --- | ---: |");
        builder.AppendLine($"| Catalogue | Assets / locaux / extraits | {report.Catalog.Assets} / {report.Catalog.LocalAssets} / {report.Catalog.ExtractedAssets} |");
        builder.AppendLine($"| Catalogue | Licences vérifiées | {report.Catalog.LicenseVerifiedAssets} |");
        builder.AppendLine($"| Vault | Fichiers / octets | {report.Vault.Total.Files} / {report.Vault.Total.Bytes} |");
        builder.AppendLine($"| Module | Fichiers / octets workspace | {report.Module.WorkspaceFiles} / {report.Module.WorkspaceBytes} |");
        builder.AppendLine($"| Module | Manifestes / chunks / références | {report.Module.Manifests} / {report.Module.Chunks} / {report.Module.AssetReferences} |");
        builder.AppendLine($"| Module | Taille `.orpmod` | {report.Module.PackageBytes} |");
        builder.AppendLine($"| Terrain | Reconstruction / collision / navigation | {Format(report.Performance.ReconstructionMilliseconds)} / {Format(report.Performance.CollisionMilliseconds)} / {Format(report.Performance.NavigationMilliseconds)} ms |");
        builder.AppendLine($"| Terrain | Sculpture / chemin | {Format(report.Performance.SculptureMilliseconds)} / {Format(report.Performance.NavigationPathMilliseconds)} ms |");
        builder.AppendLine();
        builder.AppendLine("## Contrôles de livraison");
        builder.AppendLine();
        builder.AppendLine("| Contrôle | Attendu | Mesuré | État |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var check in report.Checks)
        {
            builder.AppendLine($"| `{check.Key}` | {Escape(check.Expected)} | {Escape(check.Actual)} | {(check.Passed ? "PASS" : "FAIL")} |");
        }

        return builder.ToString();
    }

    private static CatalogMetrics MeasureCatalog(AssetCatalog catalog) => new(
        catalog.CatalogDate,
        catalog.Items.Count,
        catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly),
        catalog.Items.Count(item => item.Availability == AssetAvailability.CatalogOnly && IsMaterialOrHdri(item)),
        catalog.Items.Count(item => item.Availability == AssetAvailability.ExtractedAvailable),
        catalog.Items.Count(item => item.LicenseVerified),
        catalog.Items.Sum(item => item.TotalBytes),
        CountBy(catalog.Items.Select(item => item.Source)),
        CountBy(catalog.Items.Select(item => item.LicenseSpdx)));

    private static VaultMetrics MeasureVault(string vaultRoot)
    {
        var originals = MeasureFiles(Path.Combine(vaultRoot, "originals"));
        var normalized = MeasureFiles(Path.Combine(vaultRoot, "normalized"));
        var metadata = MeasureFiles(Path.Combine(vaultRoot, "metadata"));
        return new VaultMetrics(
            vaultRoot,
            originals,
            normalized,
            metadata,
            new FileMetrics(
                originals.Files + normalized.Files + metadata.Files,
                originals.Bytes + normalized.Bytes + metadata.Bytes));
    }

    private static ModuleMetrics MeasureModule(string workspace, string packagePath)
    {
        var manifestPath = RequireFile(Path.Combine(workspace, "assets", "asset-manifests.json"), "assetManifests");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifests = manifestDocument.RootElement.GetProperty("manifests").EnumerateArray().ToArray();
        var manifestAssetIds = manifests
            .Select(manifest => manifest.GetProperty("assetId").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var licenseCounts = CountBy(manifests.Select(manifest =>
            manifest.GetProperty("licenseSpdx").GetString() ?? string.Empty));
        var chunks = manifests.SelectMany(manifest => manifest.GetProperty("chunks").EnumerateArray()).ToArray();
        var chunkFailures = chunks.Count(chunk => !VerifyChunk(workspace, chunk));
        var references = new List<string>();
        foreach (var areaPath in EnumerateCurrentAreaDocuments(workspace))
        {
            using var areaDocument = JsonDocument.Parse(File.ReadAllText(areaPath));
            CollectAssetReferences(areaDocument.RootElement, references);
        }

        var normalizedReferences = references
            .Select(NormalizeAssetReference)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var workspaceFiles = MeasureFiles(workspace);
        var package = new FileInfo(packagePath);
        return new ModuleMetrics(
            workspace,
            workspaceFiles.Files,
            workspaceFiles.Bytes,
            manifests.Length,
            chunks.Length,
            chunks.Sum(chunk => chunk.GetProperty("storedBytes").GetInt64()),
            normalizedReferences.Length,
            normalizedReferences.Where(manifestAssetIds.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            normalizedReferences.Count(reference => !manifestAssetIds.Contains(reference)),
            chunkFailures,
            licenseCounts,
            package.FullName,
            package.Length,
            HashFile(package.FullName));
    }

    private static IReadOnlyList<string> EnumerateCurrentAreaDocuments(string workspace)
    {
        var areasPath = Path.Combine(workspace, "areas");
        var projectPath = Path.Combine(workspace, "project.json");
        if (!File.Exists(projectPath))
        {
            return Directory.EnumerateFiles(areasPath, "*.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        using var projectDocument = JsonDocument.Parse(File.ReadAllText(projectPath));
        var project = projectDocument.RootElement.GetProperty("project");
        var containedRoot = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return project.GetProperty("areas").EnumerateArray()
            .Select(reference => reference.GetProperty("documentPath").GetString() ?? string.Empty)
            .Select(relativePath => Path.GetFullPath(Path.Combine(
                workspace,
                relativePath.Replace('/', Path.DirectorySeparatorChar))))
            .Where(path => path.StartsWith(containedRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static PerformanceMetrics MeasurePerformance(string benchmarkPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(benchmarkPath));
        var root = document.RootElement;
        var sample = root.GetProperty("sample");
        return new PerformanceMetrics(
            root.GetProperty("measuredAtUtc").GetString() ?? string.Empty,
            root.GetProperty("passed").GetBoolean(),
            root.GetProperty("checks").GetArrayLength(),
            sample.GetProperty("terrainVertices").GetInt32(),
            sample.GetProperty("terrainTriangles").GetInt32(),
            sample.GetProperty("collisionShapes").GetInt32(),
            sample.GetProperty("scatterInstances").GetInt32(),
            sample.GetProperty("navigationPolygons").GetInt32(),
            sample.GetProperty("lodAssetCount").GetInt32(),
            sample.GetProperty("lodLevelCount").GetInt32(),
            sample.GetProperty("cullingGroupCount").GetInt32(),
            sample.GetProperty("governedAssetBytes").GetInt64(),
            sample.GetProperty("managedAllocatedBytes").GetInt64(),
            sample.GetProperty("processWorkingSetDeltaBytes").GetInt64(),
            sample.GetProperty("reconstructionMilliseconds").GetDouble(),
            sample.GetProperty("collisionMilliseconds").GetDouble(),
            sample.GetProperty("navigationMilliseconds").GetDouble(),
            sample.GetProperty("sculptureMilliseconds").GetDouble(),
            sample.GetProperty("navigationPathMilliseconds").GetDouble());
    }

    private static IReadOnlyList<Check> BuildChecks(
        Policy policy,
        CatalogMetrics catalog,
        VaultMetrics vault,
        ModuleMetrics module,
        PerformanceMetrics performance) =>
    [
        new("catalog-size-floor", $">={policy.MinimumCatalogAssets}", catalog.Assets.ToString(CultureInfo.InvariantCulture),
            catalog.Assets >= policy.MinimumCatalogAssets),
        new("catalog-expansion-frozen", $"<={policy.MaximumCatalogAssets}", catalog.Assets.ToString(CultureInfo.InvariantCulture),
            catalog.Assets <= policy.MaximumCatalogAssets),
        new("catalog-local-availability", "all 3D assets; Materials/HDRI indexed", $"{catalog.LocalAssets}+{catalog.CatalogOnlyMediaAssets}/{catalog.Assets}",
            catalog.LocalAssets + catalog.CatalogOnlyMediaAssets == catalog.Assets),
        new("catalog-license-evidence", "all verified", $"{catalog.LicenseVerifiedAssets}/{catalog.Assets}",
            catalog.LicenseVerifiedAssets == catalog.Assets),
        new("vault-originals", ">0 files and bytes", $"{vault.Originals.Files}/{vault.Originals.Bytes}",
            vault.Originals.Files > 0 && vault.Originals.Bytes > 0),
        new("vault-normalized", ">0 files and bytes", $"{vault.Normalized.Files}/{vault.Normalized.Bytes}",
            vault.Normalized.Files > 0 && vault.Normalized.Bytes > 0),
        // The local Vault may already contain governed metadata for a future catalog revision.
        // Delivery requires coverage of the frozen project catalog, not deletion of newer CC0 entries.
        new("vault-metadata", $">={catalog.Assets.ToString(CultureInfo.InvariantCulture)}",
            vault.Metadata.Files.ToString(CultureInfo.InvariantCulture), vault.Metadata.Files >= catalog.Assets),
        new("module-manifests", policy.ExpectedManifestCount.ToString(CultureInfo.InvariantCulture), module.Manifests.ToString(CultureInfo.InvariantCulture),
            module.Manifests == policy.ExpectedManifestCount),
        new("module-asset-references", policy.ExpectedAssetReferenceCount.ToString(CultureInfo.InvariantCulture), module.AssetReferences.ToString(CultureInfo.InvariantCulture),
            module.AssetReferences == policy.ExpectedAssetReferenceCount),
        new("module-governance", "0 ungoverned", module.UngovernedReferences.ToString(CultureInfo.InvariantCulture),
            module.UngovernedReferences == 0),
        new("module-chunk-integrity", "0 failures", module.ChunkIntegrityFailures.ToString(CultureInfo.InvariantCulture),
            module.ChunkIntegrityFailures == 0),
        new("module-licenses", $"{policy.ExpectedManifestCount} {policy.RequiredLicenseSpdx}",
            module.Licenses.TryGetValue(policy.RequiredLicenseSpdx, out var licensed) ? licensed.ToString(CultureInfo.InvariantCulture) : "0",
            module.Licenses.Count == 1 && licensed == policy.ExpectedManifestCount),
        new("module-package", ">0 bytes and SHA-256", $"{module.PackageBytes}/{module.PackageSha256}",
            module.PackageBytes > 0 && IsSha256(module.PackageSha256)),
        new("terrain-benchmark", "passed", performance.Passed.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(), performance.Passed),
        new("terrain-benchmark-coverage", policy.ExpectedBenchmarkCheckCount.ToString(CultureInfo.InvariantCulture),
            performance.Checks.ToString(CultureInfo.InvariantCulture), performance.Checks == policy.ExpectedBenchmarkCheckCount),
        new("benchmark-asset-byte-alignment", module.GovernedStoredBytes.ToString(CultureInfo.InvariantCulture),
            performance.GovernedAssetBytes.ToString(CultureInfo.InvariantCulture),
            performance.GovernedAssetBytes == module.GovernedStoredBytes),
    ];

    private static void CollectAssetReferences(JsonElement element, ICollection<string> references)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String &&
                string.Equals(key.GetString(), "visual.asset", StringComparison.Ordinal) &&
                element.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            {
                references.Add(value.GetString() ?? string.Empty);
            }

            if (element.TryGetProperty("scatterResource", out var scatter) && scatter.ValueKind == JsonValueKind.String)
            {
                references.Add(scatter.GetString() ?? string.Empty);
            }

            foreach (var property in element.EnumerateObject()) CollectAssetReferences(property.Value, references);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) CollectAssetReferences(item, references);
        }
    }

    private static bool IsMaterialOrHdri(AssetCatalogItem item) =>
        item.Categories.Any(category =>
            string.Equals(category, "Materials", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "HDRI", StringComparison.OrdinalIgnoreCase));

    private static bool VerifyChunk(string workspace, JsonElement chunk)
    {
        var relativePath = chunk.GetProperty("relativePath").GetString() ?? string.Empty;
        var expectedHash = chunk.GetProperty("contentSha256").GetString() ?? string.Empty;
        var expectedBytes = chunk.GetProperty("storedBytes").GetInt64();
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath) || !IsSha256(expectedHash)) return false;
        var absolute = Path.GetFullPath(Path.Combine(workspace, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute)) return false;
        var file = new FileInfo(absolute);
        return file.Length == expectedBytes && string.Equals(HashFile(absolute), expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, int> CountBy(IEnumerable<string> values) => values
        .GroupBy(value => value, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static FileMetrics MeasureFiles(string directory)
    {
        if (!Directory.Exists(directory)) return new FileMetrics(0, 0);
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .ToArray();
        return new FileMetrics(files.Length, files.Sum(file => file.Length));
    }

    private static string NormalizeAssetReference(string reference) => reference.StartsWith("asset:", StringComparison.OrdinalIgnoreCase)
        ? reference["asset:".Length..]
        : reference;

    private static string RequireDirectory(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"Required directory was not found: {fullPath}");
    }

    private static string RequireFile(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException($"Required file was not found: {fullPath}", fullPath);
    }

    private static void ValidatePolicy(Policy policy)
    {
        if (policy.MinimumCatalogAssets < 1 || policy.MaximumCatalogAssets < policy.MinimumCatalogAssets ||
            policy.ExpectedManifestCount < 1 || policy.ExpectedAssetReferenceCount < 1 ||
            policy.ExpectedBenchmarkCheckCount < 1 || string.IsNullOrWhiteSpace(policy.RequiredLicenseSpdx))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Asset delivery policy is invalid.");
        }
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
