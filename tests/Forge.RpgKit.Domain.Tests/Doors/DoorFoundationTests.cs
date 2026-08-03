using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Doors;

public sealed class DoorFoundationTests
{
    [Fact]
    public void Validates_socket_and_free_door_blueprints_with_full_state_machine()
    {
        var socket = Door() with { PlacementMode = DoorPlacementMode.ArchitectureSocket, ArchitectureSocketId = "socket:lighthouse", KitAdapter = new("coastal-kit", "arched", new Dictionary<string, string> { ["door"] = "glass" }, new(0, 0, 0)) };
        Assert.Empty(DoorRules.Validate(socket)); Assert.Contains(socket.StateMachine.States, item => item.Kind == DoorState.Jammed); Assert.Contains(socket.StateMachine.States, item => item.Kind == DoorState.Broken);
        var free = Door(); Assert.Empty(DoorRules.Validate(free));
    }

    [Fact]
    public void Executes_side_authority_queue_clearance_animation_and_collision_contract()
    {
        var door = Door(); var request = new DoorInteractionRequest(Guid.NewGuid(), door.Id, "player", "front", new(0, 0, 1), new HashSet<string> { "portal.travel" }, DateTimeOffset.UnixEpoch);
        var result = DoorRules.Execute(door, "closed", request, 0, new HashSet<string>()); Assert.True(result.Accepted); Assert.Equal("opening", result.CurrentState); Assert.False(result.CollisionEnabled); Assert.Equal("opening", result.Transition!.ToStateId);
        Assert.False(DoorRules.Execute(door, "closed", request with { Side = "back" }, 0, new HashSet<string>()).Accepted);
        Assert.Equal("door-queue-full", DoorRules.Execute(door, "closed", request, 2, new HashSet<string>()).Feedback);
    }

    [Fact]
    public void Creates_atomic_bidirectional_pair_and_explicit_one_way_link()
    {
        var source = Guid.NewGuid(); var target = Guid.NewGuid(); var known = new HashSet<Guid> { source, target };
        var pair = DoorLinkAuthoringService.Link([], known, source, target, false, "Retour au village"); Assert.Equal(2, pair.Added.Count); Assert.Empty(DoorLinkAuthoringService.Validate(pair.Links, known));
        var oneWay = DoorLinkAuthoringService.Link([], known, source, target, true, ""); Assert.Single(oneWay.Added); Assert.True(oneWay.Added[0].OneWay);
        Assert.Throws<InvalidOperationException>(() => DoorLinkAuthoringService.Link([], known, source, target, false, ""));
    }

    private static DoorDefinition Door()
    {
        var states = new[] { State("open", DoorState.Open, false), State("closed", DoorState.Closed, true), State("opening", DoorState.Opening, false), State("closing", DoorState.Closing, true), State("locked", DoorState.Locked, true), State("jammed", DoorState.Jammed, true), State("broken", DoorState.Broken, false), State("disabled", DoorState.Disabled, true), State("custom-glowing", DoorState.Custom, true) };
        var transitions = new[] { new DoorTransition("closed", "opening", "interact", TimeSpan.FromSeconds(1), ""), new DoorTransition("opening", "open", "animation-complete", TimeSpan.Zero, ""), new DoorTransition("open", "closing", "interact", TimeSpan.FromSeconds(1), ""), new DoorTransition("closing", "closed", "animation-complete", TimeSpan.Zero, "") };
        return new(Guid.NewGuid(), 1, "author:test", "lighthouse-door", "Porte du phare", DoorPlacementMode.FreePlacement, null, null, Transform(), new("closed", states, transitions), new(2.5, "front", new(1, 2, 1), "server", 2, "La porte ne répond pas."), new(PortalDestinationKind.Waypoint, Guid.NewGuid(), "anchor:lighthouse-top", new(0, 180, 0), true, new HashSet<string> { "portal.travel" }));
    }
    private static DoorStateDefinition State(string id, DoorState kind, bool collision) => new(id, kind, $"animation:{id}", collision, kind is not DoorState.Disabled);
    private static PlaceableTransform Transform() => new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0.25, null, true, null);
}
