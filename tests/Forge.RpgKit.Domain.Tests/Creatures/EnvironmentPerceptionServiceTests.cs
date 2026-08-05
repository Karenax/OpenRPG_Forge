using Forge.Domain.Rules;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class EnvironmentPerceptionServiceTests
{
    [Fact]
    public void Area_modifiers_change_creature_senses_by_kind_and_condition()
    {
        var creature = new CreaturePerceptionProfile(
            [new(SenseKind.Vision, 10, 120, true, new HashSet<string> { "light" })], 15);
        var environment = new EnvironmentPerceptionProfile(Guid.NewGuid(), 1, "custom",
            [new EnvironmentPerceptionModifier(Guid.NewGuid(), "vision", .5, .1, "fog")], "test");
        var stimulus = new Stimulus("torch", "light", new Vector3Value(4, 0, 0), 1);

        var clear = EnvironmentPerceptionService.Detect(creature, environment,
            new HashSet<string>(), Vector3Value.Zero, [stimulus], 5);
        var fog = EnvironmentPerceptionService.Detect(creature, environment,
            new HashSet<string> { "fog" }, Vector3Value.Zero, [stimulus], 5);

        Assert.Equal(.6, Assert.Single(clear).Confidence, 5);
        Assert.Equal(.3, Assert.Single(fog).Confidence, 5);
    }
}
