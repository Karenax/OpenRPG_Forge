using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Tests.RuntimeExtensions;

public sealed class OperationalReadinessProfileTests
{
    [Fact]
    public void Defaults_are_stable_bounded_and_cover_all_G11Q_policies()
    {
        var moduleId = ModuleId.From(Guid.Parse("e0210000-0000-4000-8000-000000000001"));

        var first = OperationalReadinessProfile.CreateDefault(moduleId.Value);
        var second = OperationalReadinessProfile.CreateDefault(moduleId.Value);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.TaskManifests.Select(item => item.Id), second.TaskManifests.Select(item => item.Id));
        Assert.Equal(first.CrashArtifacts.Id, second.CrashArtifacts.Id);
        Assert.Equal(first.ProjectHealth.Id, second.ProjectHealth.Id);
        Assert.Equal(first.Recovery.Id, second.Recovery.Id);
        Assert.Equal(first.ContentSynchronization.Id, second.ContentSynchronization.Id);
        Assert.Single(first.TaskManifests);
        Assert.True(first.CrashArtifacts.CaptureSanitizedLogs);
        Assert.True(first.ProjectHealth.IncludeCoverage);
        Assert.True(first.Recovery.QuarantineCorruptFiles);
        Assert.True(first.ContentSynchronization.Resumable);
        Assert.True(first.ContentSynchronization.DeduplicateByHash);
        Assert.Empty(OperationalReadinessValidator.Validate(first));
    }

    [Fact]
    public void Validator_reports_empty_scope_acceptance_missing_dependencies_and_cycles()
    {
        var root = Guid.Parse("e0210000-0000-4000-8000-000000000010");
        var firstId = Guid.Parse("e0210000-0000-4000-8000-000000000011");
        var secondId = Guid.Parse("e0210000-0000-4000-8000-000000000012");
        var missing = Guid.Parse("e0210000-0000-4000-8000-000000000099");
        var defaults = OperationalReadinessProfile.CreateDefault(root);
        var first = new AgentTaskManifest(firstId, 1, "first", "First task", [], [], [], "owner",
            [secondId, missing], "test");
        var second = new AgentTaskManifest(secondId, 1, "second", "Second task", ["src"], [], ["test"],
            "owner", [firstId], "test");
        var profile = new OperationalReadinessProfile(defaults.Id, 1, [first, second], defaults.CrashArtifacts,
            defaults.ProjectHealth, defaults.Recovery, defaults.ContentSynchronization, "test");

        var diagnostics = OperationalReadinessValidator.Validate(profile);

        Assert.Contains(diagnostics, item => item.Code == OperationalReadinessValidator.TaskScopeCode);
        Assert.Contains(diagnostics, item => item.Code == OperationalReadinessValidator.TaskAcceptanceCode);
        Assert.Contains(diagnostics, item => item.Code == OperationalReadinessValidator.TaskDependencyCode);
        Assert.Contains(diagnostics, item => item.Code == OperationalReadinessValidator.TaskDependencyCycleCode);
    }

    [Fact]
    public void Task_and_content_paths_reject_host_escape()
    {
        Assert.Throws<ArgumentException>(() => new AgentTaskManifest(Guid.NewGuid(), 1, "escape", "Escape",
            ["../outside"], [], ["test"], "owner", [], "test"));
        Assert.Throws<ArgumentException>(() => new ContentSynchronizationPolicy(Guid.NewGuid(), 1,
            true, true, true, false, "../cache", 1_048_576, ["CC0-1.0"], [], "test"));
    }

    [Fact]
    public void Recovery_selection_and_runtime_resolution_are_bounded_and_explicit()
    {
        var policy = OperationalReadinessProfile.CreateDefault(
            Guid.Parse("e0600000-0000-4000-8000-000000000001")).Recovery;
        var selection = new RecoveryRestoreSelection(
            Guid.Parse("e0600000-0000-4000-8000-000000000002"), 1,
            RecoveryRestoreSection.ProjectAndModule | RecoveryRestoreSection.Areas, "test:r060");

        Assert.True(selection.Sections.HasFlag(RecoveryRestoreSection.Areas));
        Assert.True(RecoveryRuntimeResolver.Resolve(policy, true).Available);
        Assert.Empty(RecoveryRuntimeResolver.Resolve(null, false).Diagnostics);
        Assert.Equal(RecoveryRuntimeResolver.RequiredMissingCode,
            Assert.Single(RecoveryRuntimeResolver.Resolve(null, true).Diagnostics).Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecoveryRestoreSelection(
            Guid.NewGuid(), 1, RecoveryRestoreSection.None, "test:r060"));
    }
}
