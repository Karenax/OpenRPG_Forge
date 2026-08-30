using System.Security.Cryptography;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetVaultPackageStoreTests
{
    [Fact]
    public void InstallDeduplicatesObjectsAggregatesCreditsAndUninstallKeepsSharedBlobs()
    {
        var root = CreateRoot();
        try
        {
            var vault = Path.Combine(root, "vault");
            var firstModel = Write(vault, "normalized/source/first/model.glb", "shared-model");
            var secondModel = Write(vault, "normalized/source/second/model.glb", "shared-model");
            var preview = Write(vault, "normalized/source/first/preview.png", "first-preview");
            var originalHash = Hash(firstModel);
            var catalog = Catalog(vault,
                Item("source:first", "First", "normalized/source/first/model.glb", "normalized/source/first/preview.png"),
                Item("source:second", "Second", "normalized/source/second/model.glb"));
            var store = new AssetVaultPackageStore(vault);

            var dryRun = store.PlanInstall(catalog, "nature-core", "1.0.0", ["source:first"]);
            var dryRunResult = store.Apply(dryRun);

            Assert.True(dryRunResult.Succeeded);
            Assert.False(dryRunResult.Applied);
            Assert.False(File.Exists(Path.Combine(vault, AssetVaultPackageStore.StateRelativePath)));

            var firstPlan = store.PlanInstall(
                catalog,
                "nature-core",
                "1.0.0",
                ["source:first"],
                dryRun: false);
            var firstResult = store.Apply(firstPlan);

            Assert.True(firstResult.Succeeded);
            Assert.True(firstResult.Applied);
            Assert.Equal(2, firstResult.WrittenObjectCount);
            Assert.Equal(0, firstResult.ReusedObjectCount);
            Assert.True(File.Exists(Path.Combine(vault, firstResult.ReceiptRelativePath!)));
            Assert.Equal(originalHash, Hash(firstModel));
            Assert.Equal(2, store.LoadState().BlobReferenceCounts.Count);
            var installedProfile = Assert.Single(Assert.Single(store.LoadState().Packages).Assets).Intelligence;
            Assert.NotNull(installedProfile);
            Assert.Equal(Hash(firstModel), installedProfile!.ModelContentSha256);
            Assert.Equal("generated-review-required", installedProfile.ReviewStatus);

            var idempotent = store.Apply(store.PlanInstall(
                catalog,
                "nature-core",
                "1.0.0",
                ["source:first"],
                dryRun: false));
            var updateRequired = store.PlanInstall(
                catalog,
                "nature-core",
                "2.0.0",
                ["source:first"],
                dryRun: false);

            Assert.True(idempotent.Succeeded);
            Assert.Equal(0, idempotent.WrittenObjectCount);
            Assert.Equal(2, idempotent.ReusedObjectCount);
            Assert.False(updateRequired.CanApply);
            Assert.Contains(updateRequired.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.PackageAlreadyInstalledCode);

            var secondPlan = store.PlanInstall(
                catalog,
                "nature-extension",
                "1.0.0",
                ["source:second"],
                dryRun: false);
            var secondResult = store.Apply(secondPlan);

            Assert.True(secondResult.Succeeded);
            Assert.True(secondResult.Applied);
            Assert.Equal(0, secondResult.WrittenObjectCount);
            Assert.Equal(1, secondResult.ReusedObjectCount);
            Assert.Equal(Hash(firstModel), Hash(secondModel));
            Assert.Equal(2, store.LoadState().BlobReferenceCounts.Count);
            var credits = AssetCreditCatalog.Aggregate(store.LoadState());
            var credit = Assert.Single(credits);
            Assert.Equal(["source:first", "source:second"], credit.AssetExternalIds);
            Assert.False(credit.AttributionRequired);
            Assert.Contains("courtesy credit", File.ReadAllText(Path.Combine(
                vault,
                AssetVaultPackageStore.CreditsMarkdownRelativePath)));

            var removeFirst = store.Apply(store.PlanUninstall("nature-core", dryRun: false));

            Assert.True(removeFirst.Succeeded);
            Assert.True(removeFirst.Applied);
            Assert.Equal(1, removeFirst.DeletedObjectCount);
            Assert.NotNull(removeFirst.BackupRelativePath);
            Assert.True(File.Exists(Path.Combine(vault, removeFirst.BackupRelativePath!)));
            var retained = Assert.Single(store.LoadState().BlobReferenceCounts);
            Assert.Equal(Hash(secondModel), retained.Key);

            var restoreFirst = store.RestoreUninstall(removeFirst);

            Assert.True(restoreFirst.Succeeded);
            Assert.True(restoreFirst.Applied);
            Assert.Equal(AssetVaultMutationOperation.Restore, restoreFirst.Operation);
            Assert.Equal(1, restoreFirst.WrittenObjectCount);
            Assert.Equal(2, store.LoadState().Packages.Count);
            Assert.Equal(2, store.LoadState().BlobReferenceCounts.Count);
            Assert.True(File.Exists(Path.Combine(vault, restoreFirst.ReceiptRelativePath!)));
            var repeatedRestore = store.RestoreUninstall(removeFirst);
            Assert.False(repeatedRestore.Succeeded);
            Assert.Contains(repeatedRestore.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.RestoreInvalidCode);

            var removeFirstAgain = store.Apply(store.PlanUninstall("nature-core", dryRun: false));
            Assert.True(removeFirstAgain.Succeeded);

            var removeSecond = store.Apply(store.PlanUninstall("nature-extension", dryRun: false));

            Assert.True(removeSecond.Succeeded);
            Assert.True(removeSecond.Applied);
            Assert.Equal(1, removeSecond.DeletedObjectCount);
            Assert.Empty(store.LoadState().Packages);
            Assert.Empty(store.LoadState().BlobReferenceCounts);
            Assert.Contains("No governed asset", File.ReadAllText(Path.Combine(
                vault,
                AssetVaultPackageStore.CreditsMarkdownRelativePath)));
            Assert.Equal(originalHash, Hash(firstModel));
            Assert.True(File.Exists(preview));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplyRejectsStalePlanAndPlanningRejectsUnsafeOrUnverifiedAssets()
    {
        var root = CreateRoot();
        try
        {
            var vault = Path.Combine(root, "vault");
            Write(vault, "normalized/source/safe/model.glb", "safe-model");
            var safe = Item("source:safe", "Safe", "normalized/source/safe/model.glb");
            var unsafeItem = Item("source:unsafe", "Unsafe", "../escape.glb");
            var unverified = Item(
                "source:restricted",
                "Restricted",
                "normalized/source/safe/model.glb",
                license: "CC-BY-4.0",
                licenseVerified: false);
            var catalog = Catalog(vault, safe, unsafeItem, unverified);
            var store = new AssetVaultPackageStore(vault);
            var stale = store.PlanInstall(catalog, "stale-pack", "1.0.0", [safe.ExternalId], dryRun: false);
            var applied = store.Apply(store.PlanInstall(
                catalog,
                "other-pack",
                "1.0.0",
                [safe.ExternalId],
                dryRun: false));

            var staleResult = store.Apply(stale);
            var unsafePlan = store.PlanInstall(
                catalog,
                "unsafe-pack",
                "1.0.0",
                [unsafeItem.ExternalId],
                dryRun: false);
            var restrictedPlan = store.PlanInstall(
                catalog,
                "restricted-pack",
                "1.0.0",
                [unverified.ExternalId],
                dryRun: false);
            var changedSourcePlan = store.PlanInstall(
                catalog,
                "changed-source-pack",
                "1.0.0",
                [safe.ExternalId],
                dryRun: false);
            File.WriteAllText(Path.Combine(vault, "normalized", "source", "safe", "model.glb"), "changed");
            var changedSource = store.Apply(changedSourcePlan);

            Assert.True(applied.Succeeded);
            Assert.False(staleResult.Succeeded);
            Assert.Contains(staleResult.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.StateStaleCode);
            Assert.False(unsafePlan.CanApply);
            Assert.Contains(unsafePlan.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.AssetUnavailableCode);
            Assert.False(restrictedPlan.CanApply);
            Assert.Contains(restrictedPlan.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.LicenseRejectedCode);
            Assert.False(changedSource.Succeeded);
            Assert.Contains(changedSource.Diagnostics, diagnostic =>
                diagnostic.Code == AssetVaultPackageStore.SourceChangedCode);
            Assert.DoesNotContain(store.LoadState().Packages, package =>
                package.PackageId == "changed-source-pack");

            var malformedVault = Path.Combine(root, "malformed-vault");
            var malformedState = Path.Combine(
                malformedVault,
                AssetVaultPackageStore.StateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(malformedState)!);
            File.WriteAllText(malformedState, "{\"schema_version\":1,\"packages\":null}");
            Assert.Throws<InvalidDataException>(() => new AssetVaultPackageStore(malformedVault).LoadState());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CatalogItemInstallationOverlayExposesPackagesAndAggregatedCredits()
    {
        var item = Item("source:tree", "Tree", "normalized/source/tree/model.glb")
            .WithInstalledPacks(["forest-core", "forest-extension"]);
        var catalog = Catalog("C:\\vault", item);

        Assert.True(item.Installed);
        Assert.Equal(["forest-core", "forest-extension"], item.InstalledPackIds);
        Assert.Equal(["forest-core", "forest-extension"], catalog.InstalledPackIds);
        Assert.Single(catalog.InstalledItems);
        Assert.Single(catalog.Credits);
        Assert.Equal(["source:tree"], catalog.Credits[0].AssetExternalIds);
    }

    private static AssetCatalog Catalog(string vault, params AssetCatalogItem[] items) => new(
        "2026-08-05",
        "cc0-only",
        vault,
        items,
        "test-catalog");

    private static AssetCatalogItem Item(
        string externalId,
        string displayName,
        string modelPath,
        string? previewPath = null,
        string license = "CC0-1.0",
        bool licenseVerified = true) => new(
        AssetCatalogItemId.ForExternalId(externalId),
        externalId,
        displayName,
        "source",
        externalId.Split(':').Last(),
        $"https://example.invalid/{externalId.Split(':').Last()}",
        license,
        "https://example.invalid/license",
        licenseVerified,
        new string('a', 64),
        12,
        ["3d-model"],
        ["test"],
        "downloaded-and-extracted",
        AssetAvailability.ExtractedAvailable,
        modelPath,
        previewPath,
        null,
        $"test:{externalId}",
        author: "Source Studio");

    private static string CreateRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-asset-vault-store",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
