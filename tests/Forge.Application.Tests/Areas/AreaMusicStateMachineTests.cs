using Forge.Application.Areas;
using Forge.Domain.Areas;
namespace Forge.Application.Tests.Areas;

public sealed class AreaMusicStateMachineTests
{
    [Fact]
    public void Transitions_obey_cooldown_and_captions_fallback_by_locale()
    {
        var root = Guid.NewGuid(); var child = Guid.NewGuid();
        var orchestration = new AreaAudioOrchestration(
            [new(MusicState.Combat, "combat.ogg", ["drums.ogg"], 2, 5)],
            [new(root, "Master", null, 1, "default", "all", .8f, true), new(child, "Music", root, .8f, "combat", "all", .7f, false)],
            [new("dragon-roar.ogg", [new("fr", "[Rugissement de dragon]"), new("en", "[Dragon roar]")], true, "critical")]);
        Assert.Null(AreaMusicStateMachine.Transition(orchestration, MusicState.Exploration, MusicState.Combat, 4));
        Assert.Equal("combat.ogg", AreaMusicStateMachine.Transition(orchestration, MusicState.Exploration, MusicState.Combat, 5)!.Track);
        Assert.Equal("[Rugissement de dragon]", AreaMusicStateMachine.ResolveCaption(orchestration, "dragon-roar.ogg", "de"));
    }
}
