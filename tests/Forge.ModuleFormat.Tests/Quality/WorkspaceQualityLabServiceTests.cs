using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.Scripting;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspaceQualityLabServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Workspace_lab_runs_all_five_domains_writes_pseudo_snapshot_and_is_deterministic()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "quality.forge"),
            "void main() { variable(\"quality\", \"pass\"); say(\"Quality pass\"); }");
        var service = new WorkspaceQualityLabService(new NativeForgeScriptRuntime());

        var first = service.Run(_root, TestProjectFactory.CreateValid());
        var second = service.Run(_root, TestProjectFactory.CreateValid());

        Assert.True(first.Passed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.Localization.Passed);
        Assert.True(Assert.Single(first.Dialogues).Passed);
        Assert.True(Assert.Single(first.Quests).Passed);
        Assert.True(first.Encounters.Passed);
        Assert.True(first.Scripts.Passed);
        Assert.Equal(WorkspaceQualityLabService.PseudoSnapshotRelativePath, first.PseudoLocalizationSnapshotPath);
        Assert.True(File.Exists(Path.Combine(_root, ".forge", "quality", "pseudo-localization.svg")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
