using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Doors;

public sealed class DoorAccessEventFoundationServiceTests
{
    [Fact]
    public void Canonical_lock_consumes_credential_and_can_relock()
    {
        var door = Door();
        var credentials = new HashSet<string>(["key:ember", "badge:watch"], StringComparer.Ordinal);

        var unlocked = DoorAccessEventFoundationService.Unlock(door,
            new("player", credentials, new HashSet<string>(), null, 0, false, false), credentials);
        var relocked = DoorAccessEventFoundationService.Relock(door, false, "player");

        Assert.True(unlocked.LockResult.Succeeded);
        Assert.True(unlocked.CredentialConsumed);
        Assert.DoesNotContain("key:ember", unlocked.RemainingCredentials);
        Assert.Equal(LockState.Locked, relocked.Lock.State);
    }

    [Fact]
    public void Rejection_trap_portal_and_terminal_state_events_use_existing_rpg_services()
    {
        var door = Door();

        var rejection = DoorAccessEventFoundationService.Reject(door, "player", "locked");
        var ignored = DoorAccessEventFoundationService.TriggerTrap(door, "back", "interact", "outsider");
        var triggered = DoorAccessEventFoundationService.TriggerTrap(door, "front", "interact", "outsider");
        var vetoed = DoorAccessEventFoundationService.RequestPortalUse(door, "player", "click",
            new HashSet<string>(["quest:open"]), _ => "party-not-ready");
        var accepted = DoorAccessEventFoundationService.RequestPortalUse(door, "player", "click",
            new HashSet<string>(["quest:open"]));
        var opened = DoorAccessEventFoundationService.CompleteStateTransition(door, "player", "opening", "open",
            "animation-complete", DateTimeOffset.UnixEpoch);
        var closed = DoorAccessEventFoundationService.CompleteStateTransition(door, "player", "closing", "closed",
            "animation-complete", DateTimeOffset.UnixEpoch);

        Assert.Equal(2, rejection.Texts.Count);
        Assert.False(ignored.Succeeded);
        Assert.True(triggered.Succeeded);
        Assert.True(vetoed.Request.Vetoed);
        Assert.Null(vetoed.Completed);
        Assert.Equal(accepted.Request.EventId, accepted.Completed!.RequestEventId);
        Assert.Equal("DoorOpened", opened.Name);
        Assert.Equal("DoorClosed", closed.Name);
    }

    [Fact]
    public void Optional_and_required_missing_profiles_remain_explicit()
    {
        var door = Door();

        var optional = DoorAccessEventFoundationService.Resolve(door with
        {
            AccessEvents = null,
            AccessEventsRequiredAtRuntime = false,
        });
        var required = DoorAccessEventFoundationService.Resolve(door with { AccessEvents = null });

        Assert.True(optional.OptionalFallback);
        Assert.Equal("world.door.access_events_required", Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void R082_Canonical_lock_transitions_reuse_typed_rpg_events_and_required_policy()
    {
        var source = Door();
        var door = source with
        {
            AccessEvents = source.AccessEvents! with
            {
                LockStateEvents = new(Guid.Parse("82000000-0000-4000-8000-000000000116"), 1,
                    "test:r082", "1.0.0", "DoorLocked", "DoorUnlocked"),
                LockStateEventsRequiredAtRuntime = true,
            },
        };

        var unlocked = DoorAccessEventFoundationService.CompleteLockTransition(door, "player", "credential",
            "key:ember", DoorAccessLockState.Locked, DoorAccessLockState.Unlocked, DateTimeOffset.UnixEpoch);
        var locked = DoorAccessEventFoundationService.CompleteLockTransition(door, "player", "relock", null,
            DoorAccessLockState.Unlocked, DoorAccessLockState.Locked, DateTimeOffset.UnixEpoch);

        Assert.Equal("DoorUnlocked", unlocked.Name);
        Assert.Equal("player", unlocked.ActorId);
        Assert.Equal("credential", unlocked.Method);
        Assert.Equal("key:ember", unlocked.Credential);
        Assert.Equal(LockState.Unlocked, locked.PreviousState);
        Assert.Equal(LockState.Locked, locked.CurrentState);
    }

    private static DoorWorldProfile Door()
    {
        var id = Guid.Parse("81000000-0000-4000-8000-000000000101");
        return new(id, 1, "test:r081",
            BlueprintId.From(Guid.Parse("81000000-0000-4000-8000-000000000102")), null, null, [], null, null,
            null,
            new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                [new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                 new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                 new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", true, false),
                 new("closing", DoorWorldProfile.StateKind.Closing, "animation:closing", true, false)],
                [new("closed", "opening", "interact", 1, null),
                 new("opening", "open", "animation-complete", 0, null),
                 new("open", "closing", "interact", 1, null),
                 new("closing", "closed", "animation-complete", 0, null)],
                new(2, "both", 1, 2, 1, "server", 2, "door.locked"),
                new(DoorWorldProfile.DestinationKind.Waypoint,
                    Guid.Parse("81000000-0000-4000-8000-000000000103"), "anchor:test", 180, true, []),
                [], new Dictionary<string, string>(),
                new("obstacle:test", "link:test", ["closed"], ["open"], .75, .25, true)),
            new(Guid.Parse("81000000-0000-4000-8000-000000000110"), 1, "test:r081",
                "assets/doors/test.glb", TransformValue.Identity,
                new(Guid.Parse("81000000-0000-4000-8000-000000000111"), DoorAccessLockState.Locked,
                    ["key:ember"], "mechanics", 12, null, true, true, true, true, []),
                new(Guid.Parse("81000000-0000-4000-8000-000000000112"), "1.0.0", "DoorOpenRejected",
                    [new("fr-FR", "Verrouillée."), new("en-US", "Locked.")], "audio:locked", "animation:rattle",
                    "scripts/rejected"),
                new(Guid.Parse("81000000-0000-4000-8000-000000000113"), "door:open", "damage:ember", 10,
                    12, 30, "ash-watch", "vfx:ember", "ash-watch", true, false, ["front"], ["interact"]),
                new(Guid.Parse("81000000-0000-4000-8000-000000000114"), "1.0.0", "PortalUseRequested",
                    "PortalUseCompleted"),
                new(Guid.Parse("81000000-0000-4000-8000-000000000115"), "1.0.0", "DoorOpened", "DoorClosed"),
                new Dictionary<string, string>()), true);
    }
}
