using System.IO.Compression;
using System.Text.Json;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Content;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Tests.Serialization;

namespace Forge.ModuleFormat.Tests.Content;

public sealed class ContentWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "content-workflow-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BatchTransferSupportsDryRunFiltersCollisionsAndPreserveBoth()
    {
        var source = Path.Combine(_root, "source");
        var destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        Directory.CreateDirectory(Path.Combine(destination, "nested"));
        File.WriteAllText(Path.Combine(source, "nested", "creatures.json"), "new");
        File.WriteAllText(Path.Combine(source, "ignored.txt"), "ignore");
        File.WriteAllText(Path.Combine(destination, "nested", "creatures.json"), "old");
        var request = new ContentBatchTransferRequest(
            ContentBatchOperation.Import,
            source,
            destination,
            "*.json",
            [".json"],
            ContentBatchCollisionPolicy.PreserveBoth,
            "fixture",
            "CC0-1.0",
            dryRun: true);

        var preview = ContentBatchTransferService.Execute(request);
        var applied = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
            request.Operation,
            request.SourceRoot,
            request.DestinationRoot,
            request.Glob,
            request.Extensions,
            request.CollisionPolicy,
            request.Provenance,
            request.LicenseSpdx,
            dryRun: false));
        var preserved = Assert.Single(applied.Entries);

        Assert.True(preview.Valid);
        Assert.Equal(ContentBatchEntryStatus.Planned, Assert.Single(preview.Entries).Status);
        Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "nested", "creatures.json")));
        Assert.True(applied.Valid);
        Assert.Equal(ContentBatchEntryStatus.PreservedBoth, preserved.Status);
        Assert.EndsWith($"creatures~{preserved.ContentSha256[..8]}.json", preserved.TargetPath, StringComparison.Ordinal);
        Assert.Equal("new", File.ReadAllText(preserved.TargetPath));
        Assert.Equal("old", File.ReadAllText(Path.Combine(destination, "nested", "creatures.json")));
    }

    [Fact]
    public void BlueprintExportIsDeterministicAndContainsGovernanceEntries()
    {
        Directory.CreateDirectory(_root);
        var project = TestProjectFactory.CreateValid();
        var firstPath = Path.Combine(_root, "first.orpblueprints");
        var secondPath = Path.Combine(_root, "second.orpblueprints");

        var first = BlueprintExportPackageBuilder.Build(project, [project.Blueprints.Single().Id], firstPath);
        var second = BlueprintExportPackageBuilder.Build(project, [project.Blueprints.Single().Id], secondPath);
        using var archive = ZipFile.OpenRead(firstPath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(1, first.BlueprintCount);
        Assert.Contains("manifest.json", entries);
        Assert.Contains(entries, path => path.StartsWith("blueprints/", StringComparison.Ordinal));
        Assert.Contains("licenses/index.json", entries);
        Assert.Contains("credits/index.json", entries);
        Assert.Contains("migrations/blueprint-schema-v1.json", entries);
        Assert.Contains(entries, path => path.StartsWith("previews/", StringComparison.Ordinal));
    }

    [Fact]
    public void BlueprintExportClosesBlueprintDependenciesAndEmitsDeterministicCredits()
    {
        Directory.CreateDirectory(_root);
        var original = TestProjectFactory.CreateValid();
        var dependency = original.Blueprints.Single();
        var rootId = BlueprintId.From(Guid.Parse("b0310000-0000-4000-8000-000000000201"));
        var provenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            "project://blueprints/export-root",
            "Forge team",
            "CC0-1.0",
            new string('e', 64),
            permissionEvidence: "spdx:CC0-1.0");
        var authoring = new BlueprintAuthoringMetadata(
            BlueprintAuthoringMetadata.CurrentSchemaVersion,
            "1.0.0",
            "project.export_root",
            $"content/blueprints/{rootId}/export-root.blueprint.json",
            dependencies: [dependency.Authoring.ManifestPath],
            previewPath: $"content/blueprints/{rootId}/export-root.preview.png");
        var root = new BlueprintDefinition(
            rootId, "export-root", "Export root", BlueprintKind.Placeable,
            provenance: provenance, authoring: authoring);
        var project = new ProjectDefinition(
            original.Id, original.Title, original.Module, [.. original.Blueprints, root], original.Areas,
            original.Dialogues, original.Quests);
        var output = Path.Combine(_root, "closure.orpblueprints");

        var result = BlueprintExportPackageBuilder.Build(project, [root.Id], output);
        using var archive = ZipFile.OpenRead(output);
        var manifestEntry = archive.GetEntry("manifest.json")!;
        using var reader = new StreamReader(manifestEntry.Open());
        using var manifest = JsonDocument.Parse(reader.ReadToEnd());

        Assert.Equal(2, result.BlueprintCount);
        Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("blueprints/", StringComparison.Ordinal)));
        Assert.Contains(manifest.RootElement.GetProperty("transitiveDependencies").EnumerateArray(),
            item => item.GetString() == dependency.Authoring.ManifestPath);
        Assert.NotNull(archive.GetEntry("credits/index.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
