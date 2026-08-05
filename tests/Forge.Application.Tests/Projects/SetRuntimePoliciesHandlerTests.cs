using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Tests.Projects;

public sealed class SetRuntimePoliciesHandlerTests
{
    [Fact]
    public void Module_and_area_policies_support_dry_run_apply_and_stable_patches()
    {
        var context = new PlaceEntityTestContext();
        var clock = new FixedClock(PlaceEntityTestContext.Timestamp);
        var moduleDefaults = context.State.Project.Module.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World clock", HeartbeatScope.Module,
            true, 2_000, 15, false, "1.0", "OnGlobalHeartbeat", "test");
        var moduleProfile = new ModuleRuntimePolicyProfile(moduleDefaults.Id, 1, [global], moduleDefaults.Rest,
            moduleDefaults.SequenceAbort, "test");
        var moduleCommand = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            context.State.Revision, moduleProfile, true);
        var moduleHandler = new SetModuleRuntimePoliciesHandler(clock);
        var preview = moduleHandler.Handle(context.State, moduleCommand);
        Assert.True(preview.Succeeded);
        Assert.Null(preview.NewRevision);
        Assert.Equal("module/runtimePolicies", preview.Patch!.Path);
        var applied = moduleHandler.Handle(context.State, moduleCommand with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Single(applied.State.Project.Module.RuntimePolicies.Heartbeats);

        var area = applied.State.Project.Areas.Single();
        var areaSchedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology", HeartbeatScope.Area,
            true, 3_000, 10, true, "1.0", "OnAreaHeartbeat", "test");
        var areaProfile = new AreaRuntimePolicyProfile(area.Id.Value, 1, [areaSchedule], area.RuntimePolicies.Rest,
            "test");
        var areaHandler = new SetAreaRuntimePoliciesHandler(clock);
        var areaResult = areaHandler.Handle(applied.State, new SetAreaRuntimePoliciesCommand(
            CommandId.From(Guid.NewGuid()), applied.State.Revision, area.Id, areaProfile, false));
        Assert.True(areaResult.Succeeded);
        Assert.Single(areaResult.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
        Assert.Contains(area.Id.ToString(), areaResult.Patch!.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Area_policy_rejects_an_owner_identity_mismatch()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var invalid = new AreaRuntimePolicyProfile(Guid.NewGuid(), 1, [], area.RuntimePolicies.Rest, "test");
        var result = new SetAreaRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp)).Handle(
            context.State, new SetAreaRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
                area.Id, invalid, false));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == "runtime.policy.area_identity");
    }

    [Fact]
    public void Combined_policy_command_is_one_atomic_undoable_transaction()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single();
        var moduleDefaults = context.State.Project.Module.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World clock", HeartbeatScope.Module,
            true, 2_000, 12, false, "1.0", "OnGlobalHeartbeat", "test");
        var local = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "Area ecology", HeartbeatScope.Area,
            true, 1_000, 8, true, "1.0", "OnAreaHeartbeat", "test");
        var module = new ModuleRuntimePolicyProfile(moduleDefaults.Id, 1, [global], moduleDefaults.Rest,
            moduleDefaults.SequenceAbort, "test");
        var areaProfile = new AreaRuntimePolicyProfile(area.RuntimePolicies.Id, 1, [local],
            area.RuntimePolicies.Rest, "test");
        var handler = new SetRuntimePoliciesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            module, area.Id, areaProfile, false);
        var result = handler.Handle(context.State, command);
        Assert.True(result.Succeeded);
        Assert.Equal("runtimePolicies", result.Patch!.Path);
        Assert.Single(result.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Single(result.State.Project.Areas.Single().RuntimePolicies.Heartbeats);

        var history = new ProjectCommandHistory(context.State);
        history.Record(result);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Empty(history.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Heartbeats);
        Assert.Single(history.State.Project.Areas.Single().RuntimePolicies.Heartbeats);
    }
}
