using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class LocalAssetPackageInstallerTests
{
    [Fact]
    public void PlansClosesDependenciesAppliesIdempotentlyRevertsAndSupportsASecondWorkspace()
    {
        using var fixture = new Fixture();
        var loaded = AssetCatalogLoader.Load(fixture.RepositoryRoot, fixture.VaultRoot);
        var item = Assert.Single(loaded.Catalog!.Items, candidate =>
            candidate.ExternalId == "test:medieval:market-stall");

        var first = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);

        Assert.True(first.Succeeded, Join(first.Diagnostics));
        Assert.False(first.Plan!.Reused);
        Assert.Equal(3, first.Plan.Files.Count);
        Assert.Single(first.Plan.Manifest.Chunks, chunk => chunk.Role == AssetChunkRole.Model);
        Assert.Equal(2, first.Plan.Manifest.Chunks.Count(chunk => chunk.Role != AssetChunkRole.Model));
        Assert.NotNull(first.Plan.Manifest.Intelligence);
        Assert.Equal("test:medieval:market-stall", first.Plan.Manifest.Intelligence!.ExternalId);
        Assert.Equal(64, first.Plan.Manifest.Intelligence.ModelContentSha256.Length);
        Assert.Equal(64, first.Plan.Manifest.Intelligence.DependencyClosureSha256.Length);
        Assert.Equal("generated-review-required", first.Plan.Manifest.Intelligence.ReviewStatus);
        Assert.Empty(Directory.EnumerateFiles(fixture.FirstWorkspace, "*", SearchOption.AllDirectories));

        var applied = LocalAssetPackageInstaller.Apply(fixture.VaultRoot, fixture.FirstWorkspace, first.Plan);
        Assert.True(applied.Succeeded, Join(applied.Diagnostics));
        Assert.Equal(3, applied.CreatedRelativePaths.Count);
        Assert.All(first.Plan.Files, file => Assert.True(File.Exists(Path.Combine(
            fixture.FirstWorkspace, file.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar)))));

        var repeated = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        var secondWorkspace = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.SecondWorkspace, item);

        Assert.True(repeated.Succeeded, Join(repeated.Diagnostics));
        Assert.True(repeated.Plan!.Reused);
        Assert.True(secondWorkspace.Succeeded, Join(secondWorkspace.Diagnostics));
        Assert.False(secondWorkspace.Plan!.Reused);

        var reverted = LocalAssetPackageInstaller.Revert(fixture.FirstWorkspace, applied);
        Assert.True(reverted.Succeeded, Join(reverted.Diagnostics));
        Assert.All(applied.CreatedRelativePaths, relative => Assert.False(File.Exists(Path.Combine(
            fixture.FirstWorkspace, relative.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Fact]
    public void RejectsCollisionTraversalArchiveHashAndUnverifiedLicenseWithoutWrites()
    {
        using var fixture = new Fixture();
        var item = AssetCatalogLoader.Load(fixture.RepositoryRoot, fixture.VaultRoot).Catalog!.Items
            .Single(candidate => candidate.ExternalId == "test:medieval:market-stall");
        var valid = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        Assert.True(valid.Succeeded, Join(valid.Diagnostics));
        var target = Path.Combine(fixture.FirstWorkspace,
            valid.Plan!.Files[0].DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "collision");

        var collision = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        Assert.Contains(collision.Diagnostics, diagnostic =>
            diagnostic.Code == LocalAssetPackageInstaller.CollisionCode);

        File.Delete(target);
        fixture.WriteModel("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"../escape.bin\"}]}");
        var traversal = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        Assert.Contains(traversal.Diagnostics, diagnostic =>
            diagnostic.Code == LocalAssetPackageInstaller.DependencyPathUnsafeCode);

        fixture.WriteModel(Fixture.ValidGltf);
        fixture.TamperArchive();
        var archive = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        Assert.Contains(archive.Diagnostics, diagnostic =>
            diagnostic.Code == LocalAssetPackageInstaller.SourceHashMismatchCode);

        var unlicensed = new AssetCatalogItem(
            item.Id, item.ExternalId, item.DisplayName, item.Source, item.SourceAssetId,
            item.SourcePage, item.LicenseSpdx, item.LicenseUrl, false, item.ContentSha256,
            item.TotalBytes, item.Categories, item.Tags, item.CatalogStatus, item.Availability,
            item.RelativeModelPath, item.RelativePreviewPath, item.ThumbnailUrl, item.Provenance,
            author: item.Author, attributionRequired: item.AttributionRequired,
            attributionText: item.AttributionText, installedPackIds: item.InstalledPackIds,
            technicalMetadata: item.TechnicalMetadata);
        var license = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.SecondWorkspace, unlicensed);
        Assert.Contains(license.Diagnostics, diagnostic =>
            diagnostic.Code == LocalAssetPackageInstaller.LicenseInvalidCode);
        Assert.Empty(Directory.EnumerateFiles(fixture.SecondWorkspace, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void ApplyRollsBackEveryCreatedFileWhenASourceChangesAfterPlanning()
    {
        using var fixture = new Fixture();
        var item = AssetCatalogLoader.Load(fixture.RepositoryRoot, fixture.VaultRoot).Catalog!.Items
            .Single(candidate => candidate.ExternalId == "test:medieval:market-stall");
        var planned = LocalAssetPackageInstaller.Plan(
            fixture.RepositoryRoot, fixture.VaultRoot, fixture.FirstWorkspace, item);
        Assert.True(planned.Succeeded, Join(planned.Diagnostics));
        fixture.TamperDependency();

        var applied = LocalAssetPackageInstaller.Apply(
            fixture.VaultRoot, fixture.FirstWorkspace, planned.Plan!);

        Assert.False(applied.Succeeded);
        Assert.Contains(applied.Diagnostics, diagnostic =>
            diagnostic.Code == LocalAssetPackageInstaller.SourceHashMismatchCode);
        Assert.Empty(Directory.EnumerateFiles(fixture.FirstWorkspace, "*", SearchOption.AllDirectories));
    }

    private static string Join(IEnumerable<Forge.Domain.Validation.DomainDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private sealed class Fixture : IDisposable
    {
        internal const string ValidGltf =
            "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"market-stall.bin\",\"byteLength\":4}],\"images\":[{\"uri\":\"market-stall.png\"}]}";
        private readonly string _root;
        private readonly string _archive;
        private readonly string _model;

        internal string RepositoryRoot { get; }
        internal string VaultRoot { get; }
        internal string FirstWorkspace { get; }
        internal string SecondWorkspace { get; }

        internal Fixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "openrpg-local-asset-package", Guid.NewGuid().ToString("N"));
            RepositoryRoot = Path.Combine(_root, "repository");
            VaultRoot = Path.Combine(_root, "vault");
            FirstWorkspace = Path.Combine(_root, "workspace-a");
            SecondWorkspace = Path.Combine(_root, "workspace-b");
            var catalogs = Path.Combine(RepositoryRoot, "asset_manifests", "catalogs");
            var modelRoot = Path.Combine(VaultRoot, "normalized", "test", "medieval", "models");
            _archive = Path.Combine(VaultRoot, "originals", "medieval.zip");
            _model = Path.Combine(modelRoot, "market-stall.gltf");
            Directory.CreateDirectory(catalogs);
            Directory.CreateDirectory(modelRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(_archive)!);
            Directory.CreateDirectory(FirstWorkspace);
            Directory.CreateDirectory(SecondWorkspace);
            File.WriteAllBytes(_archive, Encoding.UTF8.GetBytes("governed archive"));
            File.WriteAllBytes(Path.Combine(modelRoot, "market-stall.bin"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(modelRoot, "market-stall.png"), [0x89, 0x50, 0x4e, 0x47]);
            WriteModel(ValidGltf);
            File.WriteAllText(Path.Combine(catalogs, "cc0_3d_all.json"), """
                { "schema_version": 1, "catalog_date": "2026-08-26", "license_policy": "cc0-only", "assets": [] }
                """);
            File.WriteAllText(Path.Combine(catalogs, "kenney_3d_packs.json"), Catalog(HashFile(_archive)));
        }

        internal void WriteModel(string value) => File.WriteAllText(_model, value);

        internal void TamperArchive() => File.AppendAllText(_archive, "tampered");

        internal void TamperDependency()
        {
            using var stream = new FileStream(
                Path.Combine(Path.GetDirectoryName(_model)!, "market-stall.bin"), FileMode.Append, FileAccess.Write);
            stream.WriteByte(5);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string Catalog(string archiveHash) => $$"""
            {
              "schema_version": 1,
              "catalog_date": "2026-08-26",
              "license_policy": "cc0-only",
              "packs": [
                {
                  "pack_id": "test:medieval",
                  "schema_version": 1,
                  "name": "Medieval Test Pack",
                  "source": "test",
                  "source_page": "https://example.invalid/medieval",
                  "license": "CC0-1.0",
                  "license_url": "https://creativecommons.org/publicdomain/zero/1.0/",
                  "license_verified": true,
                  "status": "downloaded-and-extracted",
                  "total_bytes": 128,
                  "categories": ["Props"],
                  "tags": ["medieval", "market"],
                  "extracted_root": "normalized/test/medieval",
                  "model_files": ["models/market-stall.gltf"],
                  "files": [{ "path": "originals/medieval.zip", "sha256": "{{archiveHash}}" }]
                }
              ]
            }
            """;

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
