using System.IO.Compression;
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
        Assert.Contains("migrations/blueprint-schema-v1.json", entries);
        Assert.Contains(entries, path => path.StartsWith("previews/", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
