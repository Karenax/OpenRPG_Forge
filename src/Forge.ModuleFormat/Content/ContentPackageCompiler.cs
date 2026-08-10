using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Content;
using Forge.Domain.Validation;
using Forge.Scripting;

namespace Forge.ModuleFormat.Content;

public sealed record ContentPackageCompiledArtifact(string PackagePath, byte[] Content);

public sealed record ContentPackageCompilationReport(
    bool Success,
    IReadOnlyList<ContentPackageLockEntry> LockEntries,
    IReadOnlyList<ResolvedContentResource> Resources,
    IReadOnlyList<ContentPackageCompiledArtifact> GeneratedArtifacts,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class ContentPackageCompiler
{
    public const string LockPackagePath = "content/packages.lock.json";
    public const string ManifestPackagePath = "content/packages.manifest.json";
    public const string MissingSourceCode = "content.package.source_required_missing";
    public const string SourceHashCode = "content.package.source_hash_mismatch";
    public const string SourceBudgetCode = "content.package.source_budget_exceeded";
    public const string SourceFormatCode = "content.package.source_format_invalid";
    public const string PackageHashCode = "content.package.hash_mismatch";
    public const string ScriptCompilationCode = "content.package.script_compilation_failed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static ContentPackageCompilationReport Compile(
        string workspaceRoot,
        Guid moduleId,
        ContentPackageCatalog catalog,
        IForgeScriptRuntime? scriptRuntime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(catalog);
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var diagnostics = ContentPackageCatalogValidator.Validate(catalog, moduleId).ToList();
        var resolution = ContentPackageResolver.Resolve(catalog);
        diagnostics.AddRange(resolution.Diagnostics.Where(item => !diagnostics.Contains(item)));
        var artifacts = new List<ContentPackageCompiledArtifact>();

        foreach (var package in catalog.Packages)
        {
            foreach (var source in package.Sources)
            {
                var relative = ContentPackageDefinition.NormalizePath(source.RelativePath);
                var fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                var diagnosticPath = $"module/content/packages/items/{package.Id}/sources/{source.Id}";
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                {
                    if (source.Required)
                        diagnostics.Add(Diagnostic(MissingSourceCode, "Required package source is missing.", diagnosticPath, source.Id));
                    continue;
                }

                var bytes = File.ReadAllBytes(fullPath);
                if (bytes.LongLength > source.PlatformByteBudget)
                    diagnostics.Add(Diagnostic(SourceBudgetCode,
                        $"Source contains {bytes.LongLength} bytes but its platform budget is {source.PlatformByteBudget}.", diagnosticPath, source.Id));
                var hash = Sha256(bytes);
                if (!string.Equals(hash, source.ContentSha256, StringComparison.Ordinal))
                    diagnostics.Add(Diagnostic(SourceHashCode, $"Source SHA-256 is {hash} but manifest declares {source.ContentSha256}.", diagnosticPath, source.Id));

                try
                {
                    var text = new UTF8Encoding(false, true).GetString(bytes);
                    if (source.Kind == ContentSourceKind.Json)
                        using (JsonDocument.Parse(text)) { }
                    if (source.Kind == ContentSourceKind.ForgeScript)
                    {
                        scriptRuntime ??= new NativeForgeScriptRuntime();
                        var result = scriptRuntime.Compile(text);
                        if (!result.Success || result.Artifact is null)
                        {
                            diagnostics.Add(Diagnostic(ScriptCompilationCode,
                                string.Join("; ", result.Diagnostics.Select(item => item.Message)), diagnosticPath, source.Id));
                        }
                        else
                        {
                            artifacts.Add(new ContentPackageCompiledArtifact(
                                $"content/compiled/{package.PackageNamespace}/{Path.GetFileNameWithoutExtension(relative)}.asbc",
                                result.Artifact.Bytecode));
                        }
                    }
                }
                catch (Exception exception) when (exception is DecoderFallbackException or JsonException)
                {
                    diagnostics.Add(Diagnostic(SourceFormatCode, exception.Message, diagnosticPath, source.Id));
                }
            }

            foreach (var asset in package.Assets)
            {
                var relative = ContentPackageDefinition.NormalizePath(asset.RelativePath);
                var fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                var diagnosticPath = $"module/content/packages/items/{package.Id}/assets/{asset.Id}";
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                {
                    if (asset.Required)
                        diagnostics.Add(Diagnostic(MissingSourceCode, "Required package asset is missing.", diagnosticPath, asset.Id));
                    continue;
                }
                var bytes = File.ReadAllBytes(fullPath);
                if (bytes.LongLength > asset.PlatformByteBudget)
                    diagnostics.Add(Diagnostic(SourceBudgetCode,
                        $"Asset contains {bytes.LongLength} bytes but its platform budget is {asset.PlatformByteBudget}.", diagnosticPath, asset.Id));
                var hash = Sha256(bytes);
                if (!string.Equals(hash, asset.ContentSha256, StringComparison.Ordinal))
                    diagnostics.Add(Diagnostic(SourceHashCode,
                        $"Asset SHA-256 is {hash} but manifest declares {asset.ContentSha256}.", diagnosticPath, asset.Id));
            }

            var computedHash = ComputePackageHash(package);
            if (!string.Equals(computedHash, package.ContentSha256, StringComparison.Ordinal))
                diagnostics.Add(Diagnostic(PackageHashCode,
                    $"Package SHA-256 is {computedHash} but manifest declares {package.ContentSha256}.",
                    $"module/content/packages/items/{package.Id}/contentSha256", package.Id));
        }

        var uniqueDiagnostics = diagnostics.Distinct().OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
        if (uniqueDiagnostics.Length == 0)
        {
            artifacts.Add(new ContentPackageCompiledArtifact(LockPackagePath,
                Encoding.UTF8.GetBytes(SerializeLock(moduleId, resolution.LockEntries))));
            artifacts.Add(new ContentPackageCompiledArtifact(ManifestPackagePath,
                Encoding.UTF8.GetBytes(SerializeManifest(moduleId, catalog, resolution.Resources))));
        }
        return new ContentPackageCompilationReport(uniqueDiagnostics.Length == 0, resolution.LockEntries,
            resolution.Resources, artifacts.OrderBy(item => item.PackagePath, StringComparer.Ordinal).ToArray(), uniqueDiagnostics);
    }

    public static string ComputePackageHash(ContentPackageDefinition package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var canonical = new StringBuilder()
            .Append(package.PackageNamespace).Append('\n')
            .Append(package.Version).Append('\n')
            .Append(package.LicenseSpdx).Append('\n')
            .Append(package.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        foreach (var sealedResource in package.SealedResources.Order(StringComparer.Ordinal))
            canonical.Append("sealed:").Append(sealedResource).Append('\n');
        foreach (var dependency in package.Dependencies.OrderBy(item => item.PackageNamespace, StringComparer.Ordinal))
            canonical.Append("dependency:").Append(dependency.PackageNamespace).Append('|')
                .Append(dependency.VersionConstraint).Append('|').Append(dependency.Kind).Append('\n');
        foreach (var source in package.Sources.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            canonical.Append("source:").Append(source.LogicalResourceId).Append('|')
                .Append(ContentPackageDefinition.NormalizePath(source.RelativePath)).Append('|')
                .Append(source.Kind).Append('|').Append(source.Required).Append('|')
                .Append(source.LicenseSpdx).Append('|').Append(source.ContentSha256).Append('|')
                .Append(source.PlatformByteBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(string.Join(',', (source.Aliases ?? []).Order(StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', (source.References ?? []).Order(StringComparer.Ordinal))).Append('\n');
        foreach (var asset in package.Assets.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
            canonical.Append("asset:").Append(asset.LogicalResourceId).Append('|')
                .Append(ContentPackageDefinition.NormalizePath(asset.RelativePath)).Append('|')
                .Append(asset.Required).Append('|').Append(asset.LicenseSpdx).Append('|')
                .Append(asset.ContentSha256).Append('|')
                .Append(asset.PlatformByteBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(string.Join(',', (asset.Aliases ?? []).Order(StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', (asset.References ?? []).Order(StringComparer.Ordinal))).Append('\n');
        return Sha256(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    public static string SerializeLock(Guid moduleId, IReadOnlyList<ContentPackageLockEntry> entries) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            moduleId,
            generatedBy = "OpenRPG Forge",
            packages = entries.OrderBy(item => item.PackageNamespace, StringComparer.Ordinal),
        }, JsonOptions) + "\n";

    private static string SerializeManifest(
        Guid moduleId,
        ContentPackageCatalog catalog,
        IReadOnlyList<ResolvedContentResource> resources) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            moduleId,
            catalogId = catalog.Id,
            catalog.Provenance,
            conflictPolicy = catalog.ConflictPolicy.ToString(),
            packages = catalog.Packages.Select(package => new
            {
                package.Id,
                package.SchemaVersion,
                package.Provenance,
                package.PackageNamespace,
                package.Version,
                package.LicenseSpdx,
                package.ContentSha256,
                package.Priority,
                package.SealedResources,
                package.Dependencies,
                package.Sources,
                package.Assets,
            }),
            resources,
        }, JsonOptions) + "\n";

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static DomainDiagnostic Diagnostic(string code, string message, string path, Guid id) =>
        new(code, $"{message} [UUID {id:D}]", path);
}
