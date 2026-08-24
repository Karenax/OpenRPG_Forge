using Forge.Domain.Areas;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.RuntimePolicies;

public sealed class RuntimePolicyProfileTests
{
    [Fact]
    public void Defaults_are_inactive_owner_scoped_and_valid()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var areaId = AreaId.From(Guid.NewGuid());
        var module = new ModuleDefinition(moduleId, "module", "Module", string.Empty, areaId);
        var area = new AreaDefinition(areaId, "area", "Area", []);

        Assert.Equal(moduleId.Value, module.RuntimePolicies.Id);
        Assert.False(module.RuntimePolicies.Rest.Enabled);
        Assert.Empty(module.RuntimePolicies.Heartbeats);
        Assert.Equal(areaId.Value, area.RuntimePolicies.Id);
        Assert.Equal(RestPermissionMode.Allowed, area.RuntimePolicies.Rest.Mode);
        Assert.Empty(RuntimePolicyValidator.Validate(module));
        Assert.Empty(RuntimePolicyValidator.Validate(area));
    }

    [Fact]
    public void Validator_reports_duplicate_names_and_out_of_bounds_rest_volumes()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var areaId = AreaId.From(Guid.NewGuid());
        var module = new ModuleDefinition(moduleId, "module", "Module", string.Empty, areaId);
        var heartbeatA = Schedule("Simulation", HeartbeatScope.Module);
        var heartbeatB = Schedule("simulation", HeartbeatScope.Module);
        var runtime = new ModuleRuntimePolicyProfile(moduleId.Value, 1, [heartbeatA, heartbeatB],
            module.RuntimePolicies.Rest, module.RuntimePolicies.SequenceAbort, "test");
        module = module.WithRuntimePolicies(runtime);
        Assert.Contains(RuntimePolicyValidator.Validate(module), item => item.Code == RuntimePolicyValidator.HeartbeatNameCode);

        var volume = new RestPolicyVolume(Guid.NewGuid(), "Outside", new Vector3Value(20, 0, 0),
            new Vector3Value(8, 4, 8), RestPermissionMode.Forbidden, "rest.denied", string.Empty, 5, 0, 1, "test");
        var rest = new AreaRestPolicy(Guid.NewGuid(), 1, RestPermissionMode.Allowed, string.Empty, string.Empty,
            0, 0, [volume], "test");
        var area = new AreaDefinition(areaId, "area", "Area", [], dimensions: new AreaDimensions(16, 16));
        area = area.WithRuntimePolicies(new AreaRuntimePolicyProfile(areaId.Value, 1, [], rest, "test"));
        Assert.Contains(RuntimePolicyValidator.Validate(area), item => item.Code == RuntimePolicyValidator.RestVolumeBoundsCode);
    }

    [Fact]
    public void Rest_profile_requires_the_complete_typed_lifecycle_when_enabled()
    {
        var binding = new EventBinding(Guid.NewGuid(), 1, "test", "1.0", "rest.requested",
            "OnRestRequested", EventAuthorityScope.Server);
        Assert.Throws<ArgumentException>(() => new RestLifecycleProfile(Guid.NewGuid(), 1, true,
            PartyRestPolicy.Individual, 8, [binding], "test"));
    }

    [Fact]
    public void Ux0GHeartbeatAndRestBindingsRequireSafeRelativeScriptsWhenMarkedRequired()
    {
        var schedule = new HeartbeatScheduleDefinition(Guid.NewGuid(), 1, "World simulation",
            HeartbeatScope.Module, true, 1_500, 25, true, "1.0", "main", "test:ux0-g",
            "scripts/world-simulation.forge", true);
        Assert.Equal("scripts/world-simulation.forge", schedule.ScriptPath);
        Assert.True(schedule.RequiredAtRuntime);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatScheduleDefinition(Guid.NewGuid(), 1,
            "Unsafe", HeartbeatScope.Module, true, 1_000, 20, true, "1.0", "main", "test:ux0-g",
            "../outside.forge", true));

        var defaults = ModuleRuntimePolicyProfile.CreateDefault(ModuleId.From(Guid.NewGuid()));
        var requiredWithoutScript = defaults.Rest.Bindings.Select(binding => binding.EventName == "rest.completed"
            ? binding with { RequiredAtRuntime = true }
            : binding).ToArray();
        Assert.Throws<ArgumentException>(() => new RestLifecycleProfile(defaults.Rest.Id, 1, true,
            PartyRestPolicy.WholeParty, 12, requiredWithoutScript, "test:ux0-g"));
    }

    [Fact]
    public void Ux0HSequenceAbortBindingRequiresASafeServerScriptWhenMarkedRequired()
    {
        var source = ModuleRuntimePolicyProfile.CreateDefault(ModuleId.From(Guid.NewGuid())).SequenceAbort;
        var validBinding = source.Binding with
        {
            Handler = "main",
            ScriptPath = "scripts/sequence-abort.forge",
            RequiredAtRuntime = true,
            Provenance = "test:ux0-h",
        };

        var valid = new SequenceAbortPolicy(source.Id, source.SchemaVersion, true, validBinding,
            SequenceAbortRestoreFlags.All, 32, "test:ux0-h");

        Assert.Equal("scripts/sequence-abort.forge", valid.Binding.ScriptPath);
        Assert.True(valid.Binding.RequiredAtRuntime);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceAbortPolicy(source.Id,
            source.SchemaVersion, true, validBinding with { ScriptPath = "../outside.forge" },
            SequenceAbortRestoreFlags.All, 32, "test:ux0-h"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceAbortPolicy(source.Id,
            source.SchemaVersion, true, validBinding with { ScriptPath = null },
            SequenceAbortRestoreFlags.All, 32, "test:ux0-h"));
    }

    private static HeartbeatScheduleDefinition Schedule(string name, HeartbeatScope scope) =>
        new(Guid.NewGuid(), 1, name, scope, true, 1_000, 10, true, "1.0", "OnHeartbeat", "test");
}
