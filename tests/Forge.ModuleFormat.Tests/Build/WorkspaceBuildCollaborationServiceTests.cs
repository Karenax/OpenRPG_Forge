using System.Text;
using System.Text.Json.Nodes;
using Forge.Domain.Build;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Build;

public sealed class WorkspaceBuildCollaborationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "openrpg-build-collaboration", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DisposableCacheIsContentAddressedIndexedAndInvalidatedWithoutSourceChanges()
    {
        var workspace = CreateWorkspace();
        var projectBefore = File.ReadAllBytes(Path.Combine(workspace, "project.json"));
        var cache = Path.Combine(workspace, ".forge");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "artifact.bin"), "generated", new UTF8Encoding(false));
        var profile = WorkspaceBuildProfile.CreateDefault();

        var preview = WorkspaceBuildCollaborationService.RebuildCacheIndex(workspace, profile, dryRun: true);
        var indexed = WorkspaceBuildCollaborationService.RebuildCacheIndex(workspace, profile, dryRun: false);
        Assert.True(preview.Succeeded);
        Assert.True(indexed.Succeeded);
        Assert.Equal(64, Assert.Single(indexed.Entries).Sha256.Length);
        Assert.True(File.Exists(indexed.IndexPath));

        var invalidation = WorkspaceBuildCollaborationService.InvalidateCache(workspace, profile, dryRun: true);
        Assert.True(invalidation.DryRun);
        Assert.True(File.Exists(indexed.IndexPath));
        var invalidated = WorkspaceBuildCollaborationService.InvalidateCache(workspace, profile, dryRun: false);

        Assert.True(invalidated.Succeeded);
        Assert.Empty(Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories));
        Assert.Equal(projectBefore, File.ReadAllBytes(Path.Combine(workspace, "project.json")));
    }

    [Fact]
    public void NormalizationPreviewsThenWritesCanonicalUtf8LfAndIsIdempotent()
    {
        var workspace = CreateWorkspace();
        var data = Path.Combine(workspace, "data");
        Directory.CreateDirectory(data);
        var path = Path.Combine(data, "unordered.json");
        File.WriteAllText(path, "{\r\n  \"z\": 1.00,\r\n  \"a\": { \"b\": true, \"a\": 2 }\r\n}\r\n",
            new UTF8Encoding(true));
        var before = File.ReadAllBytes(path);

        var preview = WorkspaceBuildCollaborationService.NormalizeDocuments(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: true);
        Assert.True(preview.Succeeded,
            string.Join(" | ", preview.Diagnostics.Select(item => $"{item.Code}:{item.Path}:{item.Message}")));
        Assert.Contains(preview.Changes, item => item.RelativePath == "data/unordered.json");
        Assert.Equal(before, File.ReadAllBytes(path));

        var applied = WorkspaceBuildCollaborationService.NormalizeDocuments(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: false);
        var second = WorkspaceBuildCollaborationService.NormalizeDocuments(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: true);
        var text = File.ReadAllText(path);
        Assert.True(applied.Succeeded);
        Assert.DoesNotContain('\r', text);
        Assert.StartsWith("{\n  \"a\"", text, StringComparison.Ordinal);
        Assert.Contains("\"z\": 1", text, StringComparison.Ordinal);
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.DoesNotContain(second.Changes, item => item.RelativePath == "data/unordered.json");
    }

    [Fact]
    public void MigrationHasDryRunBackupIdempotenceAndExplicitDowngradeRefusal()
    {
        var workspace = CreateWorkspace();
        var projectPath = Path.Combine(workspace, "project.json");
        var document = JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        document["formatVersion"] = "1.26.0";
        File.WriteAllText(projectPath, document.ToJsonString(), new UTF8Encoding(false));
        var before = File.ReadAllBytes(projectPath);

        var preview = WorkspaceBuildCollaborationService.MigrateWorkspace(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: true);
        Assert.True(preview.Succeeded);
        Assert.NotEmpty(preview.ChangedDocuments);
        Assert.Equal(before, File.ReadAllBytes(projectPath));

        var applied = WorkspaceBuildCollaborationService.MigrateWorkspace(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: false);
        var second = WorkspaceBuildCollaborationService.MigrateWorkspace(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: true);
        var downgrade = WorkspaceBuildCollaborationService.MigrateWorkspace(
            workspace, WorkspaceBuildProfile.CreateDefault(), dryRun: true, targetVersion: "0.6.32");

        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        Assert.True(applied.Applied);
        Assert.True(applied.Idempotent);
        Assert.NotNull(applied.BackupDirectory);
        Assert.True(File.Exists(Path.Combine(workspace,
            applied.BackupDirectory!.Replace('/', Path.DirectorySeparatorChar), "project.json")));
        Assert.Empty(second.ChangedDocuments);
        Assert.False(downgrade.Succeeded);
        Assert.False(downgrade.DowngradeSupported);
        Assert.Equal(WorkspaceBuildCollaborationService.MigrationUnsupportedCode,
            Assert.Single(downgrade.Diagnostics).Code);
    }

    [Fact]
    public void MigrationRefusesToMutateWhenTheExplicitBackupLimitIsReached()
    {
        var workspace = CreateWorkspace();
        var projectPath = Path.Combine(workspace, "project.json");
        var document = JsonNode.Parse(File.ReadAllText(projectPath))!.AsObject();
        document["formatVersion"] = "1.26.0";
        File.WriteAllText(projectPath, document.ToJsonString(), new UTF8Encoding(false));
        var before = File.ReadAllBytes(projectPath);
        var defaults = WorkspaceBuildProfile.CreateDefault();
        var collaboration = defaults.Collaboration with
        {
            Migrations = defaults.Collaboration.Migrations with { MaximumBackups = 1 },
        };
        var profile = WithCollaboration(defaults, collaboration);
        Directory.CreateDirectory(Path.Combine(workspace, ".forge", "migration-backups", "existing"));

        var report = WorkspaceBuildCollaborationService.MigrateWorkspace(workspace, profile, dryRun: false);

        Assert.False(report.Succeeded);
        Assert.False(report.Applied);
        Assert.Contains(report.Diagnostics, item => item.Code == WorkspaceBuildCollaborationService.MigrationIoFailedCode &&
                                                    item.Message.Contains("backup limit", StringComparison.Ordinal));
        Assert.Equal(before, File.ReadAllBytes(projectPath));
    }

    [Fact]
    public void ReferenceIndexHasStableForwardAndReverseEdgesAndRebuildsWithoutCache()
    {
        var workspace = CreateWorkspace();
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var profile = WorkspaceBuildProfile.CreateDefault().Collaboration;

        var first = WorkspaceBuildCollaborationService.BuildReferenceIndex(
            workspace, session.Project, session.ProjectFilePath, profile);
        var second = WorkspaceBuildCollaborationService.BuildReferenceIndex(
            workspace, session.Project, session.ProjectFilePath, profile);

        Assert.Empty(first.Diagnostics);
        Assert.Equal(first.GraphId, second.GraphId);
        Assert.Equal(first.Nodes.Select(item => (item.Id, item.Path, item.Kind)),
            second.Nodes.Select(item => (item.Id, item.Path, item.Kind)));
        Assert.Equal(first.Nodes.Select(item => string.Join('|', item.Dependencies)),
            second.Nodes.Select(item => string.Join('|', item.Dependencies)));
        Assert.Equal(first.Nodes.Select(item => string.Join('|', item.Dependents)),
            second.Nodes.Select(item => string.Join('|', item.Dependents)));
        Assert.Contains(first.Nodes, node => node.Dependencies.Count > 0);
        Assert.Contains(first.Nodes, node => node.Dependents.Count > 0);
        Assert.Contains(first.Nodes.Single(node => node.Kind == BuildResourceKind.ProjectDocument).Dependents,
            path => path.StartsWith("areas/", StringComparison.Ordinal));
    }

    [Fact]
    public void SemanticSnapshotAndDiffCoverRequiredDomainsInTextAndStructuredForm()
    {
        var workspace = CreateWorkspace();
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var graph = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath).Graph;
        var snapshot = WorkspaceBuildCollaborationService.CreateSemanticSnapshot(session.Project, graph);
        var before = new[]
        {
            new WorkspaceSemanticResource("area:a", "area", "A", new string('a', 64)),
            new WorkspaceSemanticResource("graph:g", "graph", "G", new string('b', 64)),
            new WorkspaceSemanticResource("entity:e", "entity", "E", new string('c', 64)),
            new WorkspaceSemanticResource("package:p", "package", "P", new string('d', 64)),
            new WorkspaceSemanticResource("script:s", "script", "S", new string('e', 64)),
        };
        var after = new[]
        {
            before[0] with { Sha256 = new string('f', 64) },
            before[1],
            before[3],
            before[4],
            new WorkspaceSemanticResource("entity:new", "entity", "New", new string('1', 64)),
        };
        var diff = WorkspaceBuildCollaborationService.Diff(
            before, after, WorkspaceBuildProfile.CreateDefault().Collaboration);

        Assert.Contains(snapshot, item => item.Domain == "area");
        Assert.Contains(snapshot, item => item.Domain == "entity");
        Assert.Contains(snapshot, item => item.Domain == "graph");
        Assert.Contains(snapshot, item => item.Domain == "script");
        Assert.True(diff.Succeeded);
        Assert.Equal(3, diff.Changes.Count);
        Assert.Contains(diff.Changes, item => item.Kind == WorkspaceSemanticChangeKind.Modified && item.Domain == "area");
        Assert.Contains(diff.Changes, item => item.Kind == WorkspaceSemanticChangeKind.Removed && item.Id == "entity:e");
        Assert.Contains(diff.Changes, item => item.Kind == WorkspaceSemanticChangeKind.Added && item.Id == "entity:new");
        Assert.Contains("modified area", diff.Text, StringComparison.Ordinal);
    }

    private string CreateWorkspace()
    {
        var workspace = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        Directory.CreateDirectory(Path.Combine(workspace, "scripts"));
        Directory.CreateDirectory(Path.Combine(workspace, "areas"));
        File.Copy(Path.Combine(fixture, "forge.project.json"), Path.Combine(workspace, "forge.project.json"));
        File.Copy(Path.Combine(fixture, "project.json"), Path.Combine(workspace, "project.json"));
        foreach (var area in Directory.EnumerateFiles(Path.Combine(fixture, "areas"), "*.json"))
            File.Copy(area, Path.Combine(workspace, "areas", Path.GetFileName(area)));
        File.WriteAllText(Path.Combine(workspace, "scripts", "fixture.forge"), "void main() {}\n",
            new UTF8Encoding(false));
        return workspace;
    }

    private static WorkspaceBuildProfile WithCollaboration(
        WorkspaceBuildProfile profile,
        WorkspaceBuildCollaborationProfile collaboration) => new(
        profile.Id,
        profile.SchemaVersion,
        profile.Provenance,
        profile.RequireGitRepository,
        profile.TextDocuments,
        profile.ExplicitGeneratedFiles,
        profile.ReproducibleAssets,
        profile.Layout,
        profile.CliOperations,
        profile.StableOrdering,
        profile.NormalizedTimestamps,
        profile.LockedDependencies,
        profile.ContentHashes,
        profile.VerifyReproducibility,
        profile.IncrementalBuild,
        profile.CachePath,
        profile.RebuildAffectedOnly,
        profile.Governance,
        collaboration);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
