using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Projects;

public sealed class SetModuleDistributionHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_AppliesFiveProfilesAndRegistryWithDryRunUndoRedo()
    {
        var state = CreateState();
        var original = state.Project.Module.Distribution;
        var dependency = new ThirdPartyDependencyApproval(
            ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000002")),
            "Approved library",
            "2.0.0",
            "Apache-2.0",
            "Forge team",
            "Required for module export.",
            "monthly",
            "Replace through the adapter boundary.",
            "https://example.invalid/approved-library",
            [ModuleTargetKind.Client, ModuleTargetKind.Shipping],
            "test");
        var distribution = new ModuleDistributionConfiguration(
            original.Id,
            original.TargetProfiles,
            [dependency],
            "test");
        var command = new SetModuleDistributionCommand(
            CommandId.From(Guid.Parse("d7130000-0000-4000-8000-000000000003")),
            state.Revision,
            distribution,
            DryRun: false);
        var handler = new SetModuleDistributionHandler(new FixedClock(Timestamp));

        var dryRun = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.Same(state, dryRun.State);
        Assert.False(dryRun.Audit.Applied);
        Assert.Equal(5, applied.State.Project.Module.Distribution.TargetProfiles.Count);
        Assert.Equal(dependency, Assert.Single(applied.State.Project.Module.Distribution.Dependencies));
        Assert.Equal(DependencyApprovalState.Approved, dependency.ApprovalState);
        Assert.Equal(DependencyRiskLevel.Low, dependency.RiskLevel);
        Assert.Equal(2, dependency.RequiredApprovals);
        Assert.Equal(2, dependency.ApproverRoles.Count);
        Assert.Equal(dependency.SourceReference, dependency.SecurityAuditReference);
        Assert.Equal(Timestamp, applied.Audit.Timestamp);
        Assert.Equal("module/distribution", applied.Patch!.Path);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.Distribution.Dependencies);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(dependency.Id, Assert.Single(history.State.Project.Module.Distribution.Dependencies).Id);
    }

    [Fact]
    public void Handle_RejectsRevisionConflictWithoutMutation()
    {
        var state = CreateState();
        var command = new SetModuleDistributionCommand(
            CommandId.From(Guid.Parse("d7130000-0000-4000-8000-000000000004")),
            state.Revision.Next(),
            state.Project.Module.Distribution,
            DryRun: false);

        var result = new SetModuleDistributionHandler(new FixedClock(Timestamp)).Handle(state, command);

        Assert.False(result.Succeeded);
        Assert.Same(state, result.State);
        Assert.Equal(SetModuleDistributionHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("d7130000-0000-4000-8000-000000000005")),
            "area",
            "Area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("d7130000-0000-4000-8000-000000000006")),
            "Distribution command test",
            [],
            [area]);
        return new ProjectState(project, new ProjectRevision(7));
    }
}
