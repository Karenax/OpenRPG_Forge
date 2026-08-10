using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Content;

public enum ContentDependencyKind
{
    Required,
    Optional,
    Peer,
}

public enum ContentSourceKind
{
    Json,
    Text,
    Yaml,
    ForgeScript,
}

public enum ContentConflictPolicy
{
    Error,
    HighestPriority,
    NamespaceOrder,
}

public sealed record ContentPackageDependency(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string PackageNamespace,
    string VersionConstraint,
    ContentDependencyKind Kind);

public sealed record ContentPackageSource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string LogicalResourceId,
    string RelativePath,
    ContentSourceKind Kind,
    bool Required,
    string LicenseSpdx,
    string ContentSha256,
    long PlatformByteBudget,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? References = null);

public sealed record ContentPackageAsset(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string LogicalResourceId,
    string RelativePath,
    bool Required,
    string LicenseSpdx,
    string ContentSha256,
    long PlatformByteBudget,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? References = null);

public sealed record ContentPackageDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string PackageNamespace { get; }
    public string Version { get; }
    public string LicenseSpdx { get; }
    public string ContentSha256 { get; }
    public int Priority { get; }
    public IReadOnlyList<string> SealedResources { get; }
    public IReadOnlyList<ContentPackageDependency> Dependencies { get; }
    public IReadOnlyList<ContentPackageSource> Sources { get; }
    public IReadOnlyList<ContentPackageAsset> Assets { get; }

    public ContentPackageDefinition(
        Guid id,
        int schemaVersion,
        string provenance,
        string packageNamespace,
        string version,
        string licenseSpdx,
        string contentSha256,
        int priority,
        IReadOnlyList<string>? sealedResources,
        IReadOnlyList<ContentPackageDependency>? dependencies,
        IReadOnlyList<ContentPackageSource>? sources,
        IReadOnlyList<ContentPackageAsset>? assets = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        PackageNamespace = (packageNamespace ?? string.Empty).Trim().ToLowerInvariant();
        Version = (version ?? string.Empty).Trim();
        LicenseSpdx = (licenseSpdx ?? string.Empty).Trim();
        ContentSha256 = (contentSha256 ?? string.Empty).Trim().ToLowerInvariant();
        Priority = priority;
        SealedResources = (sealedResources ?? []).Select(NormalizeResourceId)
            .Order(StringComparer.Ordinal).ToArray();
        Dependencies = (dependencies ?? []).OrderBy(item => item.PackageNamespace, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        Sources = (sources ?? []).OrderBy(item => NormalizePath(item.RelativePath), StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        Assets = (assets ?? []).OrderBy(item => NormalizePath(item.RelativePath), StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
    }

    private static string NormalizeResourceId(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizePath(string? value) =>
        (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
}

public sealed record ContentPackageCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public ContentConflictPolicy ConflictPolicy { get; }
    public IReadOnlyList<ContentPackageDefinition> Packages { get; }

    public ContentPackageCatalog(
        Guid id,
        int schemaVersion,
        string provenance,
        ContentConflictPolicy conflictPolicy,
        IReadOnlyList<ContentPackageDefinition>? packages)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        ConflictPolicy = conflictPolicy;
        Packages = (packages ?? []).OrderBy(item => item.PackageNamespace, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal).ToArray();
    }

    public static ContentPackageCatalog CreateEmpty(Guid moduleId) =>
        new(moduleId, CurrentSchemaVersion, "project-author", ContentConflictPolicy.HighestPriority, []);
}

public sealed record ContentPackageLockEntry(
    Guid PackageId,
    string PackageNamespace,
    string Version,
    string ContentSha256,
    int Priority);

public sealed record ResolvedContentResource(
    string LogicalResourceId,
    string RelativePath,
    Guid SourceId,
    Guid PackageId,
    string PackageNamespace,
    int Priority);

public sealed record ContentPackageResolution(
    IReadOnlyList<ContentPackageLockEntry> LockEntries,
    IReadOnlyList<ResolvedContentResource> Resources,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Valid => Diagnostics.Count == 0;
}

public static partial class ContentPackageCatalogValidator
{
    public const string InvalidCatalogIdCode = "content.package.catalog_id_invalid";
    public const string UnsupportedSchemaCode = "content.package.schema_unsupported";
    public const string MissingFieldCode = "content.package.field_required";
    public const string DuplicateUuidCode = "content.package.uuid_duplicate";
    public const string DuplicateNamespaceCode = "content.package.namespace_duplicate";
    public const string InvalidNamespaceCode = "content.package.namespace_invalid";
    public const string InvalidVersionCode = "content.package.version_invalid";
    public const string InvalidLicenseCode = "content.package.license_invalid";
    public const string InvalidHashCode = "content.package.hash_invalid";
    public const string InvalidPathCode = "content.package.source_path_invalid";
    public const string DuplicateSourcePathCode = "content.package.source_path_duplicate";
    public const string DuplicateResourceCode = "content.package.resource_duplicate";
    public const string MissingDependencyCode = "content.package.dependency_required_missing";
    public const string PeerDependencyCode = "content.package.dependency_peer_missing";
    public const string DependencyVersionCode = "content.package.dependency_version_mismatch";
    public const string DependencyCycleCode = "content.package.dependency_cycle";
    public const string PlatformBudgetCode = "content.package.platform_budget_invalid";
    public const string SealedOverrideCode = "content.package.resource_sealed";
    public const string ConflictCode = "content.package.resource_conflict";
    public const string ResourceAddressCode = "content.resource.address_invalid";
    public const string ResourceAliasCollisionCode = "content.resource.alias_collision";
    public const string ResourceReferenceCode = "content.resource.reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ContentPackageCatalog catalog, Guid? expectedModuleId = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        if (catalog.Id == Guid.Empty || expectedModuleId is not null && catalog.Id != expectedModuleId)
        {
            diagnostics.Add(new DomainDiagnostic(InvalidCatalogIdCode,
                "The content package catalog UUID must match the module UUID.", "module/content/packages/id"));
        }
        if (catalog.SchemaVersion != ContentPackageCatalog.CurrentSchemaVersion)
            diagnostics.Add(Diagnostic(UnsupportedSchemaCode, "Unsupported content package catalog schema.", "schemaVersion", catalog.Id));
        if (string.IsNullOrWhiteSpace(catalog.Provenance))
            diagnostics.Add(Diagnostic(MissingFieldCode, "Catalog provenance is required.", "provenance", catalog.Id));

        var ids = new HashSet<Guid>();
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var sourceIds = new HashSet<Guid>();
        foreach (var package in catalog.Packages)
        {
            var path = $"module/content/packages/items/{package.Id}";
            if (package.Id == Guid.Empty || !ids.Add(package.Id))
                diagnostics.Add(Diagnostic(DuplicateUuidCode, "Package UUIDs must be non-empty and unique.", path, package.Id));
            if (package.SchemaVersion != ContentPackageDefinition.CurrentSchemaVersion)
                diagnostics.Add(Diagnostic(UnsupportedSchemaCode, "Unsupported content package schema.", $"{path}/schemaVersion", package.Id));
            if (string.IsNullOrWhiteSpace(package.Provenance))
                diagnostics.Add(Diagnostic(MissingFieldCode, "Package provenance is required.", $"{path}/provenance", package.Id));
            if (!NamespacePattern().IsMatch(package.PackageNamespace))
                diagnostics.Add(Diagnostic(InvalidNamespaceCode, "Package namespace must use lowercase dotted identifiers.", $"{path}/packageNamespace", package.Id));
            else if (!namespaces.Add(package.PackageNamespace))
                diagnostics.Add(Diagnostic(DuplicateNamespaceCode, "Package namespaces must be unique.", $"{path}/packageNamespace", package.Id));
            if (!SemanticVersion.TryParse(package.Version, out _))
                diagnostics.Add(Diagnostic(InvalidVersionCode, "Package version must be semantic versioning.", $"{path}/version", package.Id));
            if (!SpdxPattern().IsMatch(package.LicenseSpdx) || package.LicenseSpdx.Equals("UNLICENSED", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(Diagnostic(InvalidLicenseCode, "A known SPDX licence is required.", $"{path}/licenseSpdx", package.Id));
            if (!HashPattern().IsMatch(package.ContentSha256))
                diagnostics.Add(Diagnostic(InvalidHashCode, "Package contentSha256 must contain 64 lowercase hexadecimal characters.", $"{path}/contentSha256", package.Id));

            var paths = new HashSet<string>(StringComparer.Ordinal);
            var resources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in package.Sources)
            {
                var sourcePath = $"{path}/sources/{source.Id}";
                if (source.Id == Guid.Empty || !sourceIds.Add(source.Id))
                    diagnostics.Add(Diagnostic(DuplicateUuidCode, "Source UUIDs must be non-empty and globally unique.", sourcePath, source.Id));
                if (source.SchemaVersion != ContentPackageDefinition.CurrentSchemaVersion)
                    diagnostics.Add(Diagnostic(UnsupportedSchemaCode, "Unsupported content source schema.", $"{sourcePath}/schemaVersion", source.Id));
                if (string.IsNullOrWhiteSpace(source.Provenance))
                    diagnostics.Add(Diagnostic(MissingFieldCode, "Source provenance is required.", $"{sourcePath}/provenance", source.Id));
                if (string.IsNullOrWhiteSpace(source.LogicalResourceId) || !resources.Add(source.LogicalResourceId.Trim().ToLowerInvariant()))
                    diagnostics.Add(Diagnostic(DuplicateResourceCode, "Each logical resource must map to one source file per package.", $"{sourcePath}/logicalResourceId", source.Id));
                var normalized = ContentPackageDefinition.NormalizePath(source.RelativePath);
                if (!IsSafeRelativePath(source.RelativePath) || !KindMatchesExtension(source.Kind, normalized))
                    diagnostics.Add(Diagnostic(InvalidPathCode, "Source path must be safe, relative and match its text format.", $"{sourcePath}/relativePath", source.Id));
                else if (!paths.Add(normalized))
                    diagnostics.Add(Diagnostic(DuplicateSourcePathCode, "A source file may contain only one logical resource.", $"{sourcePath}/relativePath", source.Id));
                if (!SpdxPattern().IsMatch(source.LicenseSpdx) || source.LicenseSpdx.Equals("UNLICENSED", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(Diagnostic(InvalidLicenseCode, "A known SPDX licence is required for every source.", $"{sourcePath}/licenseSpdx", source.Id));
                if (!HashPattern().IsMatch(source.ContentSha256))
                    diagnostics.Add(Diagnostic(InvalidHashCode, "Source contentSha256 must contain 64 lowercase hexadecimal characters.", $"{sourcePath}/contentSha256", source.Id));
                if (source.PlatformByteBudget <= 0)
                    diagnostics.Add(Diagnostic(PlatformBudgetCode, "Source platform byte budget must be positive.", $"{sourcePath}/platformByteBudget", source.Id));
            }

            foreach (var asset in package.Assets)
            {
                var assetPath = $"{path}/assets/{asset.Id}";
                if (asset.Id == Guid.Empty || !sourceIds.Add(asset.Id))
                    diagnostics.Add(Diagnostic(DuplicateUuidCode, "Asset UUIDs must be non-empty and globally unique.", assetPath, asset.Id));
                if (asset.SchemaVersion != ContentPackageDefinition.CurrentSchemaVersion)
                    diagnostics.Add(Diagnostic(UnsupportedSchemaCode, "Unsupported content asset schema.", $"{assetPath}/schemaVersion", asset.Id));
                if (string.IsNullOrWhiteSpace(asset.Provenance))
                    diagnostics.Add(Diagnostic(MissingFieldCode, "Asset provenance is required.", $"{assetPath}/provenance", asset.Id));
                if (string.IsNullOrWhiteSpace(asset.LogicalResourceId) || !resources.Add(asset.LogicalResourceId.Trim().ToLowerInvariant()))
                    diagnostics.Add(Diagnostic(DuplicateResourceCode, "Each logical resource must map to one file per package.", $"{assetPath}/logicalResourceId", asset.Id));
                var normalized = ContentPackageDefinition.NormalizePath(asset.RelativePath);
                if (!IsSafeRelativePath(asset.RelativePath) || string.IsNullOrWhiteSpace(Path.GetExtension(normalized)))
                    diagnostics.Add(Diagnostic(InvalidPathCode, "Asset path must be a safe relative file path.", $"{assetPath}/relativePath", asset.Id));
                else if (!paths.Add(normalized))
                    diagnostics.Add(Diagnostic(DuplicateSourcePathCode, "A package file may contain only one logical resource.", $"{assetPath}/relativePath", asset.Id));
                if (!SpdxPattern().IsMatch(asset.LicenseSpdx) || asset.LicenseSpdx.Equals("UNLICENSED", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(Diagnostic(InvalidLicenseCode, "A known SPDX licence is required for every asset.", $"{assetPath}/licenseSpdx", asset.Id));
                if (!HashPattern().IsMatch(asset.ContentSha256))
                    diagnostics.Add(Diagnostic(InvalidHashCode, "Asset contentSha256 must contain 64 lowercase hexadecimal characters.", $"{assetPath}/contentSha256", asset.Id));
                if (asset.PlatformByteBudget <= 0)
                    diagnostics.Add(Diagnostic(PlatformBudgetCode, "Asset platform byte budget must be positive.", $"{assetPath}/platformByteBudget", asset.Id));
            }

            var dependencyIds = new HashSet<Guid>();
            foreach (var dependency in package.Dependencies)
            {
                var dependencyPath = $"{path}/dependencies/{dependency.Id}";
                if (dependency.Id == Guid.Empty || !dependencyIds.Add(dependency.Id))
                    diagnostics.Add(Diagnostic(DuplicateUuidCode, "Dependency UUIDs must be non-empty and unique per package.", dependencyPath, dependency.Id));
                if (dependency.SchemaVersion != ContentPackageDefinition.CurrentSchemaVersion)
                    diagnostics.Add(Diagnostic(UnsupportedSchemaCode, "Unsupported content dependency schema.", $"{dependencyPath}/schemaVersion", dependency.Id));
                if (string.IsNullOrWhiteSpace(dependency.Provenance) || string.IsNullOrWhiteSpace(dependency.PackageNamespace))
                    diagnostics.Add(Diagnostic(MissingFieldCode, "Dependency provenance and namespace are required.", dependencyPath, dependency.Id));
                if (!SemanticVersionConstraint.IsValid(dependency.VersionConstraint))
                    diagnostics.Add(Diagnostic(InvalidVersionCode, "Dependency version constraint is invalid.", $"{dependencyPath}/versionConstraint", dependency.Id));
            }
        }

        diagnostics.AddRange(ValidateDependencyGraph(catalog));
        diagnostics.AddRange(ContentResourceRegistry.Build(catalog).Diagnostics);
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateDependencyGraph(ContentPackageCatalog catalog)
    {
        var result = new List<DomainDiagnostic>();
        var packages = catalog.Packages.GroupBy(item => item.PackageNamespace, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var package in catalog.Packages)
        {
            foreach (var dependency in package.Dependencies)
            {
                if (!packages.TryGetValue(dependency.PackageNamespace, out var target))
                {
                    if (dependency.Kind == ContentDependencyKind.Required)
                        result.Add(Diagnostic(MissingDependencyCode, $"Required package '{dependency.PackageNamespace}' is unavailable offline.", $"module/content/packages/items/{package.Id}/dependencies/{dependency.Id}", dependency.Id));
                    else if (dependency.Kind == ContentDependencyKind.Peer)
                        result.Add(Diagnostic(PeerDependencyCode, $"Peer package '{dependency.PackageNamespace}' must be supplied by the host.", $"module/content/packages/items/{package.Id}/dependencies/{dependency.Id}", dependency.Id));
                    continue;
                }
                if (!SemanticVersionConstraint.Matches(dependency.VersionConstraint, target.Version))
                    result.Add(Diagnostic(DependencyVersionCode, $"Package '{target.PackageNamespace}' version {target.Version} does not satisfy {dependency.VersionConstraint}.", $"module/content/packages/items/{package.Id}/dependencies/{dependency.Id}", dependency.Id));
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string ns)
        {
            if (visiting.Contains(ns)) return true;
            if (!visited.Add(ns) || !packages.TryGetValue(ns, out var package)) return false;
            visiting.Add(ns);
            foreach (var dependency in package.Dependencies.Where(item => item.Kind == ContentDependencyKind.Required && packages.ContainsKey(item.PackageNamespace)))
                if (Visit(dependency.PackageNamespace)) return true;
            visiting.Remove(ns);
            return false;
        }
        foreach (var package in catalog.Packages)
        {
            visiting.Clear();
            if (Visit(package.PackageNamespace))
            {
                result.Add(Diagnostic(DependencyCycleCode, $"Required dependency cycle reaches '{package.PackageNamespace}'.", $"module/content/packages/items/{package.Id}/dependencies", package.Id));
                break;
            }
        }
        return result;
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        var normalized = ContentPackageDefinition.NormalizePath(path);
        return normalized.Length > 0 && normalized.Split('/').All(part => part is not "" and not "." and not "..");
    }

    private static bool KindMatchesExtension(ContentSourceKind kind, string path) => kind switch
    {
        ContentSourceKind.Json => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
        ContentSourceKind.Text => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase),
        ContentSourceKind.Yaml => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase),
        ContentSourceKind.ForgeScript => path.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".as", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static DomainDiagnostic Diagnostic(string code, string message, string path, Guid id) =>
        new(code, $"{message} [UUID {id:D}]", path);

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SpdxPattern();
}

public static class ContentPackageResolver
{
    public static ContentPackageResolution Resolve(ContentPackageCatalog catalog)
    {
        var diagnostics = ContentPackageCatalogValidator.Validate(catalog).ToList();
        if (diagnostics.Any(item => item.Code is ContentPackageCatalogValidator.MissingDependencyCode or
                ContentPackageCatalogValidator.DependencyVersionCode or ContentPackageCatalogValidator.DependencyCycleCode))
            return new ContentPackageResolution([], [], diagnostics);

        var resources = new List<ResolvedContentResource>();
        var providers = catalog.Packages.SelectMany(package =>
            package.Sources.Select(source => (Package: package, Id: source.Id,
                    LogicalResourceId: source.LogicalResourceId, RelativePath: source.RelativePath))
                .Concat(package.Assets.Select(asset => (Package: package, Id: asset.Id,
                    LogicalResourceId: asset.LogicalResourceId, RelativePath: asset.RelativePath))));
        foreach (var group in providers
                     .GroupBy(item => item.LogicalResourceId.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var candidates = group.OrderByDescending(item => item.Package.Priority)
                .ThenBy(item => item.Package.PackageNamespace, StringComparer.Ordinal).ToArray();
            var sealedCandidates = candidates.Where(item => item.Package.SealedResources.Contains(group.Key, StringComparer.Ordinal)).ToArray();
            var winner = candidates[0];
            if (sealedCandidates.Length == 1)
            {
                winner = sealedCandidates[0];
                if (candidates[0].Package.Id != winner.Package.Id)
                    diagnostics.Add(new DomainDiagnostic(ContentPackageCatalogValidator.SealedOverrideCode,
                        $"Resource '{group.Key}' remains owned by sealed package '{winner.Package.PackageNamespace}'. [UUID {winner.Package.Id:D}]",
                        $"module/content/packages/resources/{group.Key}"));
            }
            else if (sealedCandidates.Length > 1)
            {
                diagnostics.Add(new DomainDiagnostic(ContentPackageCatalogValidator.ConflictCode,
                    $"Resource '{group.Key}' is sealed by multiple packages.", $"module/content/packages/resources/{group.Key}"));
                continue;
            }
            else if (candidates.Length > 1 && catalog.ConflictPolicy == ContentConflictPolicy.Error)
            {
                diagnostics.Add(new DomainDiagnostic(ContentPackageCatalogValidator.ConflictCode,
                    $"Resource '{group.Key}' has multiple providers.", $"module/content/packages/resources/{group.Key}"));
                continue;
            }
            else if (catalog.ConflictPolicy == ContentConflictPolicy.NamespaceOrder)
            {
                winner = candidates.OrderBy(item => item.Package.PackageNamespace, StringComparer.Ordinal).First();
            }

            resources.Add(new ResolvedContentResource(group.Key, ContentPackageDefinition.NormalizePath(winner.RelativePath),
                winner.Id, winner.Package.Id, winner.Package.PackageNamespace, winner.Package.Priority));
        }

        var lockEntries = catalog.Packages.Select(package => new ContentPackageLockEntry(
                package.Id, package.PackageNamespace, package.Version, package.ContentSha256, package.Priority))
            .OrderBy(item => item.PackageNamespace, StringComparer.Ordinal).ToArray();
        return new ContentPackageResolution(lockEntries, resources, diagnostics
            .OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());
    }
}

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var core = value.Split(['-', '+'], 2)[0].Split('.');
        if (core.Length != 3 || !int.TryParse(core[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(core[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(core[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) return false;
        version = new SemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(SemanticVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
}

internal static class SemanticVersionConstraint
{
    public static bool IsValid(string? constraint) =>
        !string.IsNullOrWhiteSpace(constraint) && Matches(constraint, "1.2.3", validateOnly: true);

    public static bool Matches(string constraint, string version) => Matches(constraint, version, validateOnly: false);

    private static bool Matches(string constraint, string version, bool validateOnly)
    {
        if (!SemanticVersion.TryParse(version, out var actual)) return false;
        var trimmed = (constraint ?? string.Empty).Trim();
        var op = trimmed.StartsWith("^", StringComparison.Ordinal) || trimmed.StartsWith("~", StringComparison.Ordinal)
            ? trimmed[..1]
            : trimmed.StartsWith(">=", StringComparison.Ordinal) || trimmed.StartsWith("<=", StringComparison.Ordinal)
                ? trimmed[..2]
                : trimmed.StartsWith(">", StringComparison.Ordinal) || trimmed.StartsWith("<", StringComparison.Ordinal) || trimmed.StartsWith("=", StringComparison.Ordinal)
                    ? trimmed[..1]
                    : "=";
        var value = op == "=" && !trimmed.StartsWith("=", StringComparison.Ordinal) ? trimmed : trimmed[op.Length..].Trim();
        if (!SemanticVersion.TryParse(value, out var expected)) return false;
        if (validateOnly) return true;
        var comparison = actual.CompareTo(expected);
        return op switch
        {
            "^" => actual.Major == expected.Major && comparison >= 0,
            "~" => actual.Major == expected.Major && actual.Minor == expected.Minor && comparison >= 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            _ => comparison == 0,
        };
    }
}
