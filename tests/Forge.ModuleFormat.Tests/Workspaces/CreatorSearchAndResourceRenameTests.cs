using Forge.Domain.Authoring;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class CreatorSearchAndResourceRenameTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SearchProfile_RoundTripsAtomically()
    {
        Directory.CreateDirectory(_root);
        var projectId = Guid.NewGuid();
        var defaults = CreatorSearchProfileStore.Load(_root, projectId);
        var configured = defaults with
        {
            Provenance = "test:r034-search",
            ResultLimit = 73,
            IncludeAssets = false,
            IncludeDiagnostics = false,
        };

        CreatorSearchProfileStore.Save(_root, configured);
        var restored = CreatorSearchProfileStore.Load(_root, projectId);

        Assert.Equal(configured, restored);
        Assert.NotEqual(Guid.Empty, restored.Id);
        Assert.Equal(CreatorSearchProfile.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ResourceRename_MigratesReferencesPreservesUuidAndSupportsUndoRedo()
    {
        var scripts = Path.Combine(_root, "scripts");
        var assets = Path.Combine(_root, "assets");
        Directory.CreateDirectory(scripts);
        Directory.CreateDirectory(assets);
        var source = Path.Combine(scripts, "old_name.forge");
        var reference = Path.Combine(_root, "project.json");
        File.WriteAllText(source, "fn main() {}\n");
        File.WriteAllText(reference, "{\"script\":\"scripts/old_name.forge\"}\n");

        var preview = WorkspaceResourceRenameService.Preview(_root, "scripts/old_name.forge",
            "scripts/new_name.forge", "test:r034-resource-rename");
        WorkspaceResourceRenameService.Apply(preview);

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(scripts, "new_name.forge")));
        Assert.Contains("scripts/new_name.forge", File.ReadAllText(reference), StringComparison.Ordinal);
        Assert.Equal(preview.ResourceId,
            WorkspaceResourceIdentityStore.Resolve(_root, "scripts/new_name.forge"));
        Assert.Single(preview.ReferenceEdits);

        WorkspaceResourceRenameService.Undo(preview);
        Assert.True(File.Exists(source));
        Assert.Contains("scripts/old_name.forge", File.ReadAllText(reference), StringComparison.Ordinal);
        Assert.Equal(preview.ResourceId, WorkspaceResourceIdentityStore.Resolve(_root, "scripts/old_name.forge"));

        WorkspaceResourceRenameService.Redo(preview);
        Assert.True(File.Exists(Path.Combine(scripts, "new_name.forge")));
        Assert.Contains("scripts/new_name.forge", File.ReadAllText(reference), StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceRename_RejectsTraversalAndLeavesWorkspaceUntouched()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        var source = Path.Combine(_root, "scripts", "safe.forge");
        File.WriteAllText(source, "safe\n");

        Assert.Throws<ArgumentException>(() => WorkspaceResourceRenameService.Preview(
            _root, "scripts/safe.forge", "../escape.forge", "test:r034-invalid"));
        Assert.True(File.Exists(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
