using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.Scripting;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Quality;

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

        var first = service.Run(_root, TestProjectFactory.CreateNavigationValid());
        var second = service.Run(_root, TestProjectFactory.CreateNavigationValid());

        Assert.True(first.Passed, string.Join("; ", first.CanonicalValidation.Diagnostics
            .Select(item => $"{item.Category}:{item.Code}:{item.Path}:{item.Message}")));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.Localization.Passed);
        Assert.True(Assert.Single(first.Dialogues).Passed);
        Assert.True(Assert.Single(first.Quests).Passed);
        Assert.True(first.Encounters.Passed);
        Assert.True(first.Scripts.Passed);
        Assert.True(first.CanonicalValidation.Passed,
            string.Join("; ", first.CanonicalValidation.Diagnostics.Select(item => $"{item.Code}:{item.Path}")));
        Assert.Equal(5, first.CanonicalValidation.CategoryCounts.Count);
        Assert.Equal(first.CanonicalValidation.Fingerprint, second.CanonicalValidation.Fingerprint);
        Assert.Equal(WorkspaceQualityLabService.PseudoSnapshotRelativePath, first.PseudoLocalizationSnapshotPath);
        Assert.True(File.Exists(Path.Combine(_root, ".forge", "quality", "pseudo-localization.svg")));
    }

    [Fact]
    public void Optional_navigation_diagnostics_remain_visible_without_blocking_legacy_quality_gate()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "quality.forge"),
            "void main() { variable(\"quality\", \"pass\"); say(\"Quality pass\"); }");
        var report = new WorkspaceQualityLabService(new NativeForgeScriptRuntime())
            .Run(_root, TestProjectFactory.CreateValid());

        Assert.True(report.Passed);
        Assert.False(report.CanonicalValidation.Passed);
        Assert.Contains(report.CanonicalValidation.Diagnostics,
            item => item.Category == "navigation" && item.Code == "area.navigation.spawn_missing");
    }

    [Fact]
    public void Required_area_lifecycle_script_is_blocked_before_build_and_passes_when_present()
    {
        var source = TestProjectFactory.CreateNavigationValid();
        var area = source.Areas[0];
        var defaults = area.Foundation;
        var foundation = new AreaFoundationProfile(defaults.Id, 1, "test:r028",
            defaults.Creation, defaults.TileKit,
            new(new(Guid.Parse("a0284000-0000-4000-8000-000000000001"), "area.entered",
                "scripts/required_enter.forge", "1.0.0", true, "test:r028"), null), [], []);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? item.WithFoundation(foundation) : item).ToArray(),
            source.Dialogues, source.Quests);
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));

        var missing = WorkspaceCanonicalValidationService.Run(_root, project,
            QualityFoundationProfile.CreateDefault(project.Id.Value));
        File.WriteAllText(Path.Combine(_root, "scripts", "required_enter.forge"), "void main() {}");
        var present = WorkspaceCanonicalValidationService.Run(_root, project,
            QualityFoundationProfile.CreateDefault(project.Id.Value));

        Assert.False(missing.Passed);
        Assert.Contains(missing.Diagnostics, item =>
            item.Code == WorkspaceCanonicalValidationService.AreaLifecycleScriptMissingCode &&
            item.Path.Contains(area.Id.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(present.Diagnostics,
            item => item.Code == WorkspaceCanonicalValidationService.AreaLifecycleScriptMissingCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
