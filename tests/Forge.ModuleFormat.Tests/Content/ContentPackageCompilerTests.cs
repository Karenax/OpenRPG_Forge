using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Content;

public sealed class ContentPackageCompilerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-r016", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Canonical_catalog_round_trips_exactly_with_sources_dependencies_and_priority()
    {
        var source = Encoding.UTF8.GetBytes("{\"name\":\"Ash Warden\"}\n");
        var project = WithCatalog(TestProjectFactory.CreateValid(), source, includeScript: false);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        var package = Assert.Single(restored.Module.Content.Packages.Packages);
        Assert.Equal("crypt.encounters", package.PackageNamespace);
        Assert.Equal("1.2.0", package.Version);
        Assert.Equal(42, package.Priority);
        Assert.Equal("creature.ash-warden", Assert.Single(package.Sources).LogicalResourceId);
        Assert.Equal("portrait.ash-warden", Assert.Single(package.Assets).LogicalResourceId);
        Assert.Equal(["legacy.core:text/warden"], Assert.Single(package.Sources).Aliases);
        Assert.Equal(["crypt.encounters:text/creature.ash-warden"], Assert.Single(package.Assets).References);
    }

    [Fact]
    public void Existing_project_without_package_catalog_migrates_to_deterministic_empty_catalog()
    {
        var source = TestProjectFactory.CreateValid();
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(source))!.AsObject();
        document["project"]!["module"]!["content"]!.AsObject().Remove("packages");

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());

        Assert.Empty(first.Module.Content.Packages.Packages);
        Assert.Equal(source.Module.Id.Value, first.Module.Content.Packages.Id);
        Assert.Equal(ProjectJsonSerializer.Serialize(first), ProjectJsonSerializer.Serialize(second));
    }

    [Fact]
    public void Export_validates_resolves_compiles_hashes_and_emits_reproducible_orpmod()
    {
        const string scriptText = "void main() { say(\"r016\"); }\n";
        var source = Encoding.UTF8.GetBytes(scriptText);
        var project = WithCatalog(TestProjectFactory.CreateValid(), source, includeScript: true);
        var workspace = Path.Combine(_root, "workspace");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        WriteSource(workspace, "scripts/bootstrap.forge", source);
        var first = Path.Combine(_root, "first.orpmod");
        var second = Path.Combine(_root, "second.orpmod");

        ModulePackager.Export(workspace, first);
        File.Delete(Path.Combine(workspace, ".forge", "build-state.json"));
        ModulePackager.Export(workspace, second);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        using var archive = ZipFile.OpenRead(first);
        Assert.Contains(archive.Entries, entry => entry.FullName == ContentPackageCompiler.LockPackagePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == ContentPackageCompiler.ManifestPackagePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "content/compiled/crypt.encounters/bootstrap.asbc");
        Assert.Contains(archive.Entries, entry => entry.FullName == "content/scripts/bootstrap.forge");
        using (var reader = new StreamReader(archive.GetEntry(ContentPackageCompiler.ManifestPackagePath)!.Open()))
        {
            var manifest = reader.ReadToEnd();
            Assert.Contains("\"dependencies\"", manifest, StringComparison.Ordinal);
            Assert.Contains("\"sources\"", manifest, StringComparison.Ordinal);
            Assert.Contains("\"assets\"", manifest, StringComparison.Ordinal);
            Assert.Contains("\"licenseSpdx\"", manifest, StringComparison.Ordinal);
        }
        Assert.True(ModulePackageValidator.Validate(first).IsValid);
    }

    [Fact]
    public void Export_refuses_required_missing_source_and_hash_mismatch_before_writing()
    {
        var expected = Encoding.UTF8.GetBytes("{\"valid\":true}\n");
        var project = WithCatalog(TestProjectFactory.CreateValid(), expected, includeScript: false);
        var workspace = Path.Combine(_root, "invalid");
        Assert.True(ProjectSession.Create(workspace, project).Succeeded);
        var destination = Path.Combine(_root, "invalid.orpmod");

        var missing = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, destination));
        Assert.Contains(ContentPackageCompiler.MissingSourceCode, missing.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(destination));

        WriteSource(workspace, "data/warden.json", Encoding.UTF8.GetBytes("{\"wrong\":true}\n"));
        var mismatch = Assert.Throws<InvalidOperationException>(() => ModulePackager.Export(workspace, destination));
        Assert.Contains(ContentPackageCompiler.SourceHashCode, mismatch.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void Compiler_ignores_absent_optional_source_but_keeps_it_in_manifest_hash()
    {
        var moduleId = Guid.NewGuid();
        var source = new ContentPackageSource(Guid.NewGuid(), 1, "tests:r016", "ui.optional",
            "data/optional.json", ContentSourceKind.Json, false, "CC0-1.0", new string('a', 64), 4096);
        var draft = new ContentPackageDefinition(Guid.NewGuid(), 1, "tests:r016", "forge.optional",
            "1.0.0", "CC0-1.0", new string('0', 64), 1, [], [], [source]);
        var package = new ContentPackageDefinition(draft.Id, 1, draft.Provenance, draft.PackageNamespace,
            draft.Version, draft.LicenseSpdx, ContentPackageCompiler.ComputePackageHash(draft), draft.Priority,
            draft.SealedResources, draft.Dependencies, draft.Sources);
        var catalog = new ContentPackageCatalog(moduleId, 1, "tests:r016",
            ContentConflictPolicy.HighestPriority, [package]);

        var report = ContentPackageCompiler.Compile(_root, moduleId, catalog);

        Assert.True(report.Success);
        Assert.DoesNotContain(report.Diagnostics, item => item.Code == ContentPackageCompiler.MissingSourceCode);
        Assert.Contains(report.GeneratedArtifacts, item => item.PackagePath == ContentPackageCompiler.LockPackagePath);
    }

    [Fact]
    public void Lock_store_replaces_atomically_and_preserves_deterministic_json()
    {
        var store = new ContentPackageLockStore(_root);
        var moduleId = Guid.NewGuid();
        var entries = new[] { new ContentPackageLockEntry(Guid.NewGuid(), "forge.core", "1.0.0", new string('a', 64), 2) };

        store.Save(moduleId, entries);
        var first = File.ReadAllText(store.Path);
        store.Save(moduleId, entries);
        var second = File.ReadAllText(store.Path);

        Assert.Equal(first, second);
        using var document = store.Load();
        Assert.Equal(moduleId, document.RootElement.GetProperty("moduleId").GetGuid());
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(store.Path)!, "*.tmp"));
    }

    private static ProjectDefinition WithCatalog(ProjectDefinition source, byte[] content, bool includeScript)
    {
        var relative = includeScript ? "scripts/bootstrap.forge" : "data/warden.json";
        var kind = includeScript ? ContentSourceKind.ForgeScript : ContentSourceKind.Json;
        var resource = includeScript ? "script.bootstrap" : "creature.ash-warden";
        var item = new ContentPackageSource(Guid.Parse("c0160000-0000-4000-8000-000000000003"), 1,
            "tests:r016", resource, relative, kind, true, "CC0-1.0", Sha256(content), 65_536,
            ["legacy.core:text/warden"], []);
        var asset = new ContentPackageAsset(Guid.Parse("c0160000-0000-4000-8000-000000000004"), 1,
            "tests:r016", "portrait.ash-warden", "assets/portraits/ash-warden.png", false,
            "CC0-1.0", new string('d', 64), 4_194_304, [],
            [includeScript ? "crypt.encounters:script/script.bootstrap" : "crypt.encounters:text/creature.ash-warden"]);
        var draft = new ContentPackageDefinition(Guid.Parse("c0160000-0000-4000-8000-000000000002"), 1,
            "tests:r016", "crypt.encounters", "1.2.0", "CC0-1.0", new string('0', 64), 42,
            includeScript ? ["script.bootstrap"] : [], [], [item], [asset]);
        var package = new ContentPackageDefinition(draft.Id, draft.SchemaVersion, draft.Provenance,
            draft.PackageNamespace, draft.Version, draft.LicenseSpdx, ContentPackageCompiler.ComputePackageHash(draft),
            draft.Priority, draft.SealedResources, draft.Dependencies, draft.Sources, draft.Assets);
        var catalog = new ContentPackageCatalog(source.Module.Id.Value, 1, "tests:r016",
            ContentConflictPolicy.HighestPriority, [package]);
        var previous = source.Module.Content;
        var workspace = new ContentWorkspaceConfiguration(previous.Id, previous.Batch, previous.DataTables,
            previous.MergePolicy, previous.Provenance, previous.SchemaVersion, catalog);
        var module = new ModuleDefinition(source.Module.Id, source.Module.Slug, source.Module.DisplayName,
            source.Module.Description, source.Module.EntryAreaId, source.Module.Metadata, source.Module.Variables,
            source.Module.Distribution, source.Module.Localization, workspace, source.Module.ArtisticStyle,
            source.Module.RuntimePolicies, source.Module.Factions, source.Module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static void WriteSource(string root, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
