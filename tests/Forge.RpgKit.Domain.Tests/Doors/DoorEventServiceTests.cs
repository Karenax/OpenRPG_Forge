using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Doors;

public sealed class DoorEventServiceTests
{
    [Fact]
    public void Requests_portal_with_actor_destination_cause_conditions_and_controlled_veto()
    {
        var door = Door(); var conditions = new HashSet<string> { "lighthouse-powered", "ilyne-present" };
        var vetoed = DoorEventService.RequestPortalUse(door, "player", "use-door", conditions, "runtime:test", request => request.Conditions.Contains("ilyne-present") ? "companion-must-stay" : null);
        Assert.True(vetoed.Request.Vetoed); Assert.Equal("companion-must-stay", vetoed.Request.VetoReason); Assert.Null(vetoed.Completed);
        var accepted = DoorEventService.RequestPortalUse(door, "player", "use-door", conditions, "runtime:test");
        Assert.False(accepted.Request.Vetoed); Assert.Equal(door.Portal!.DestinationId, accepted.Completed!.DestinationId); Assert.Equal(accepted.Request.EventId, accepted.Completed.RequestEventId);
    }

    [Fact]
    public void Emits_opened_and_closed_only_after_terminal_state_transition()
    {
        var id = Guid.NewGuid(); var at = DateTimeOffset.UnixEpoch;
        var opened = DoorEventService.CompleteStateTransition(id, "player", "opening", "open", "animation-complete", "runtime:test", at);
        var closed = DoorEventService.CompleteStateTransition(id, "player", "closing", "closed", "animation-complete", "runtime:test", at);
        Assert.Equal("DoorOpened", opened.Name); Assert.Equal("DoorClosed", closed.Name); Assert.Throws<InvalidOperationException>(() => DoorEventService.CompleteStateTransition(id, "player", "closed", "opening", "interact", "runtime:test", at));
    }

    [Fact]
    public void Emits_locked_and_unlocked_with_method_actor_and_credential()
    {
        var id = Guid.NewGuid(); var at = DateTimeOffset.UnixEpoch;
        var unlocked = DoorEventService.CompleteLockTransition(id, "ilyne", "credential", "key:lighthouse", LockState.Locked, LockState.Unlocked, "runtime:test", at);
        var locked = DoorEventService.CompleteLockTransition(id, "ilyne", "relock", null, LockState.Unlocked, LockState.Locked, "runtime:test", at);
        Assert.Equal("DoorUnlocked", unlocked.Name); Assert.Equal("credential", unlocked.Method); Assert.Equal("key:lighthouse", unlocked.Credential); Assert.Equal("DoorLocked", locked.Name);
    }

    private static DoorDefinition Door() => new(Guid.NewGuid(), 1, "author:test", "lighthouse-door", "Porte", DoorPlacementMode.FreePlacement, null, null, new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0, null, false, null), new("closed", [new("closed", DoorState.Closed, "closed", true, true), new("open", DoorState.Open, "open", false, true)], [new("closed", "open", "interact", TimeSpan.Zero, "")]), new(2, "both", new(1, 2, 1), "server", 1, "blocked"), new(PortalDestinationKind.Waypoint, Guid.NewGuid(), "anchor:lighthouse", new(0, 180, 0), true, new HashSet<string>()));
}
