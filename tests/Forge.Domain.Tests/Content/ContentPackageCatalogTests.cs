using Forge.Domain.Content;

namespace Forge.Domain.Tests.Content;

public sealed class ContentPackageCatalogTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Validates_and_resolves_required_optional_and_priority_deterministically()
    {
        var core = Package("forge.core", "1.2.0", 10, HashA,
            [Source("creature.warden", "content/warden.json", HashA)]);
        var expansion = Package("forge.expansion", "2.0.0", 20, HashB,
            [Source("creature.warden", "content/warden-elite.json", HashB)],
            [Dependency("forge.core", "^1.0.0", ContentDependencyKind.Required),
             Dependency("forge.missing-cosmetics", "~1.0.0", ContentDependencyKind.Optional)]);
        var catalog = Catalog(core, expansion);

        Assert.Empty(ContentPackageCatalogValidator.Validate(catalog));
        var first = ContentPackageResolver.Resolve(catalog);
        var second = ContentPackageResolver.Resolve(catalog);

        Assert.True(first.Valid);
        Assert.Equal(["forge.core", "forge.expansion"], first.LockEntries.Select(item => item.PackageNamespace));
        Assert.Equal("forge.expansion", Assert.Single(first.Resources).PackageNamespace);
        Assert.Equal(first.LockEntries, second.LockEntries);
        Assert.Equal(first.Resources, second.Resources);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public void Required_and_peer_absence_are_diagnostic_while_optional_absence_is_ignored()
    {
        var package = Package("forge.module", "1.0.0", 10, HashA, [],
            [Dependency("forge.required", "^1.0.0", ContentDependencyKind.Required),
             Dependency("forge.optional", "^1.0.0", ContentDependencyKind.Optional),
             Dependency("forge.peer", ">=1.0.0", ContentDependencyKind.Peer)]);

        var diagnostics = ContentPackageCatalogValidator.Validate(Catalog(package));

        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.MissingDependencyCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.PeerDependencyCode);
        Assert.DoesNotContain(diagnostics, item => item.Message.Contains("forge.optional", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_cycles_version_mismatch_unsafe_paths_duplicate_resources_and_invalid_governance()
    {
        var a = Package("forge.a", "1.0.0", 10, "bad", [
            Source("same", "../escape.json", HashA),
            Source("same", "content/other.json", HashA),
        ], [Dependency("forge.b", "^2.0.0", ContentDependencyKind.Required)], licence: "UNLICENSED");
        var b = Package("forge.b", "1.0.0", 10, HashB, [],
            [Dependency("forge.a", "1.0.0", ContentDependencyKind.Required)]);

        var diagnostics = ContentPackageCatalogValidator.Validate(Catalog(a, b));

        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.InvalidHashCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.InvalidLicenseCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.InvalidPathCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.DuplicateResourceCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.DependencyVersionCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.DependencyCycleCode);
        Assert.All(diagnostics, item => Assert.False(string.IsNullOrWhiteSpace(item.Path)));
    }

    [Fact]
    public void Sealed_resource_cannot_be_overridden_and_conflict_policy_can_refuse_ambiguity()
    {
        var sealedCore = Package("forge.core", "1.0.0", 1, HashA,
            [Source("rules.balance", "content/balance.json", HashA)], sealedResources: ["rules.balance"]);
        var overridePackage = Package("forge.override", "1.0.0", 100, HashB,
            [Source("rules.balance", "content/override.json", HashB)]);

        var sealedResult = ContentPackageResolver.Resolve(Catalog(sealedCore, overridePackage));
        Assert.Contains(sealedResult.Diagnostics, item => item.Code == ContentPackageCatalogValidator.SealedOverrideCode);
        Assert.Equal("forge.core", Assert.Single(sealedResult.Resources).PackageNamespace);

        var strict = new ContentPackageCatalog(Guid.NewGuid(), 1, "test", ContentConflictPolicy.Error,
            [Package("forge.left", "1.0.0", 10, HashA, [Source("same", "left.json", HashA)]),
             Package("forge.right", "1.0.0", 10, HashB, [Source("same", "right.json", HashB)])]);
        Assert.Contains(ContentPackageResolver.Resolve(strict).Diagnostics,
            item => item.Code == ContentPackageCatalogValidator.ConflictCode);
    }

    private static ContentPackageCatalog Catalog(params ContentPackageDefinition[] packages) =>
        new(Guid.NewGuid(), 1, "tests", ContentConflictPolicy.HighestPriority, packages);

    private static ContentPackageDefinition Package(
        string ns,
        string version,
        int priority,
        string hash,
        IReadOnlyList<ContentPackageSource> sources,
        IReadOnlyList<ContentPackageDependency>? dependencies = null,
        string licence = "CC0-1.0",
        IReadOnlyList<string>? sealedResources = null) =>
        new(Guid.NewGuid(), 1, "tests", ns, version, licence, hash, priority, sealedResources, dependencies, sources);

    private static ContentPackageSource Source(string resource, string path, string hash) =>
        new(Guid.NewGuid(), 1, "tests", resource, path, ContentSourceKind.Json, true, "CC0-1.0", hash, 1024 * 1024);

    private static ContentPackageDependency Dependency(string ns, string constraint, ContentDependencyKind kind) =>
        new(Guid.NewGuid(), 1, "tests", ns, constraint, kind);
}
