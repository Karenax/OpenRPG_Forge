using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Gameplay;

public sealed class GameplaySessionProfileTests
{
    private static readonly ModuleId ModuleId = ModuleId.From(
        Guid.Parse("a0671000-0000-4000-8000-000000000001"));

    [Fact]
    public void R067_default_profile_is_complete_unique_and_valid()
    {
        var profile = GameplaySessionProfile.CreateDefault(ModuleId);
        Assert.Empty(GameplaySessionProfileValidator.Validate(profile,
            new HashSet<string>(["waypoint:start"])));
        Assert.Equal(profile.AllIds().Count, profile.AllIds().Distinct().Count());
        Assert.Equal(3, profile.ControlLocks.Contexts.Count);
        Assert.True(profile.Replay.EmitMinimalPackage);
    }

    [Fact]
    public void R067_validator_reports_each_invalid_boundary_with_stable_path()
    {
        var source = GameplaySessionProfile.CreateDefault(ModuleId);
        var invalid = source with
        {
            Persistence = source.Persistence with { MaximumSnapshots = 0 },
            Checkpoints = [source.Checkpoints[0] with { SafeRadius = 0 }],
            Shell = source.Shell with { Scale = 4 },
            ControlLocks = source.ControlLocks with { Contexts = [] },
            Replay = source.Replay with { MaximumFrames = 0 },
        };
        var diagnostics = GameplaySessionProfileValidator.Validate(invalid,
            new HashSet<string>(["waypoint:other"]));
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.PersistenceCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.CheckpointCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.WaypointReferenceCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.ShellCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.ControlCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySessionProfileValidator.ReplayCode);
        Assert.All(diagnostics, item => Assert.Contains("gameplay/session", item.Path));
    }

    [Fact]
    public void R067_optional_absence_is_safe_and_required_absence_is_diagnostic()
    {
        Assert.Empty(GameplaySessionProfileValidator.ValidateRuntime(null, false));
        Assert.Equal(GameplaySessionProfileValidator.RequiredMissingCode,
            Assert.Single(GameplaySessionProfileValidator.ValidateRuntime(null, true)).Code);
    }
}
