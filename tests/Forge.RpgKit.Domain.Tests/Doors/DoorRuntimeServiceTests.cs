using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Doors;

public sealed class DoorRuntimeServiceTests
{
    [Fact]
    public void Synchronizes_navigation_obstacle_link_animation_and_visualization()
    {
        var profile = Navigation();
        var closed = DoorRuntimeService.SynchronizeNavigation(profile, "closed", 1); Assert.True(closed.ObstacleEnabled); Assert.False(closed.LinkEnabled); Assert.Contains("blocked", closed.Visualization);
        var openingEarly = DoorRuntimeService.SynchronizeNavigation(profile, "opening", 0.5); Assert.True(openingEarly.ObstacleEnabled);
        var openingLate = DoorRuntimeService.SynchronizeNavigation(profile, "opening", 0.8); Assert.False(openingLate.ObstacleEnabled); Assert.True(openingLate.LinkEnabled);
    }

    [Fact]
    public void Shares_lock_credentials_skill_relock_and_consumes_key()
    {
        var component = Lock(); var credentials = new HashSet<string> { "key:lighthouse", "badge:village" };
        var result = DoorRuntimeService.Unlock(component, new("ilyne", credentials, new HashSet<string>(), null, 0, false, false), credentials, true);
        Assert.True(result.LockResult.Succeeded); Assert.True(result.CredentialConsumed); Assert.DoesNotContain("key:lighthouse", result.RemainingCredentials); Assert.Contains("badge:village", result.RemainingCredentials);
    }

    [Fact]
    public void Emits_localized_rejection_and_triggers_trap_only_for_bound_side_event()
    {
        var rejection = DoorRuntimeService.Reject(Guid.NewGuid(), "player", "locked", [new("fr", "La porte est verrouillée."), new("en", "The door is locked.")], "audio:locked", "animation:rattle", "on_open_rejected", "runtime:test");
        Assert.Equal("locked", rejection.ReasonCode); Assert.Equal(2, rejection.Texts.Count); Assert.Equal("audio:locked", rejection.SoundAssetId);
        var trap = new TrapComponent(Guid.NewGuid(), "open", "damage:glass", 10, 10, TimeSpan.Zero, "glass", "vfx:shards", "village", true, true); var binding = new DoorTrapBinding(trap, new HashSet<string> { "front" }, new HashSet<string> { "open" });
        Assert.False(DoorRuntimeService.TriggerTrap(binding, "back", "open", "outsider").Succeeded); Assert.True(DoorRuntimeService.TriggerTrap(binding, "front", "open", "outsider").Succeeded);
    }

    [Fact]
    public void Validates_destination_animation_collision_nav_credential_trap_script_and_loop()
    {
        var door = Door(); var profile = Profile(door);
        Assert.Empty(DoorRuntimeService.Validate(door, profile, []));
        var invalid = profile with { KnownDestinations = new HashSet<Guid>(), KnownAnimations = new HashSet<string>(), KnownCollisionProfiles = new HashSet<string>(), Navigation = profile.Navigation with { TraversableStateIds = new HashSet<string> { "closed" } }, KnownCredentials = new HashSet<string>(), Trap = profile.Trap! with { Sides = new HashSet<string>() }, ScriptBindings = ["", "missing-script"], KnownScripts = new HashSet<string>() };
        var codes = DoorRuntimeService.Validate(door, invalid, [new(door.Id, door.Id, true, "")]).Select(item => item.Code).ToHashSet();
        Assert.Contains("RPG_DOOR_DESTINATION_MISSING", codes); Assert.Contains("RPG_DOOR_ANIMATION_MISSING", codes); Assert.Contains("RPG_DOOR_COLLISION_PROFILE_MISMATCH", codes); Assert.Contains("RPG_DOOR_NAVIGATION_INVALID", codes); Assert.Contains("RPG_DOOR_CREDENTIAL_MISSING", codes); Assert.Contains("RPG_DOOR_TRAP_BINDING_INVALID", codes); Assert.Contains("RPG_DOOR_SCRIPT_INVALID", codes); Assert.Contains("RPG_DOOR_SCRIPT_MISSING", codes); Assert.Contains("RPG_DOOR_TRANSITION_LOOP", codes);
    }

    private static DoorRuntimeProfile Profile(DoorDefinition door) => new(Navigation(), Lock(), new(new(Guid.NewGuid(), "open", "damage:glass", 10, 10, TimeSpan.Zero, "glass", "vfx", "village", true, false), new HashSet<string> { "front" }, new HashSet<string> { "open" }), ["on_open"], new HashSet<Guid> { door.Portal!.DestinationId }, door.StateMachine.States.Select(item => item.AnimationId).ToHashSet(), new HashSet<string> { "collision:door" }, new HashSet<string> { "key:lighthouse" }, new HashSet<string> { "on_open" }, door.StateMachine.States.Select(item => item.AnimationId).ToHashSet(), "collision:door");
    private static DoorNavigationProfile Navigation() => new("obstacle:lighthouse", "link:lighthouse", true, new HashSet<string> { "closed", "locked" }, new HashSet<string> { "open" });
    private static LockComponent Lock() => new(LockState.Locked, new HashSet<string> { "key:lighthouse" }, "mechanics", 12, null, false, true, false, new HashSet<string>());
    private static DoorDefinition Door()
    {
        var states = new[] { new DoorStateDefinition("open", DoorState.Open, "animation:open", false, true), new DoorStateDefinition("closed", DoorState.Closed, "animation:closed", true, true) };
        return new(Guid.NewGuid(), 1, "author:test", "door", "Door", DoorPlacementMode.FreePlacement, null, null, new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0, null, false, null), new("closed", states, [new("closed", "open", "interact", TimeSpan.Zero, "")]), new(2, "both", new(1, 2, 1), "server", 2, "blocked"), new(PortalDestinationKind.Waypoint, Guid.NewGuid(), "anchor", new(0, 0, 0), true, new HashSet<string>()));
    }
}
