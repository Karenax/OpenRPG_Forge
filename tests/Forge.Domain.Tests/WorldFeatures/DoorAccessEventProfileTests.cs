using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class DoorAccessEventProfileTests
{
    [Fact]
    public void Canonical_access_profile_validates_all_five_groups()
    {
        var door = Door();

        var diagnostics = DoorAccessEventProfileValidator.Validate(door.AccessEvents!,
            $"module/worldFeatures/doors/{door.Id}/accessEvents");

        Assert.Empty(diagnostics);
        Assert.True(DoorAccessEventProfileValidator.Resolve(door).Available);
        Assert.Equal("PortalUseRequested", door.AccessEvents!.PortalUse.RequestedBinding);
        Assert.Equal("DoorClosed", door.AccessEvents.StateEvents.ClosedBinding);
    }

    [Fact]
    public void Invalid_lock_rejection_trap_and_event_bindings_have_stable_paths()
    {
        var door = Door();
        var source = door.AccessEvents!;
        var invalid = source with
        {
            Lock = source.Lock with { CredentialIds = [], Difficulty = -1 },
            Rejection = source.Rejection with { Texts = [], EventBinding = "open" },
            Trap = source.Trap with { Sides = [], ResetDelaySeconds = -1 },
            PortalUse = source.PortalUse with { RequestedBinding = "transition" },
            StateEvents = source.StateEvents with { OpenedBinding = "opened" },
        };

        var diagnostics = DoorAccessEventProfileValidator.Validate(invalid, "doors/test/accessEvents");
        var codes = diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("world.door.lock_invalid", codes);
        Assert.Contains("world.door.open_rejected_invalid", codes);
        Assert.Contains("world.door.trap_invalid", codes);
        Assert.Contains("world.door.portal_events_invalid", codes);
        Assert.Contains("world.door.state_events_invalid", codes);
        Assert.All(diagnostics, item => Assert.StartsWith("doors/test/accessEvents", item.Path));
    }

    [Fact]
    public void Component_uuids_must_be_non_empty_and_unique()
    {
        var source = Door().AccessEvents!;
        var invalid = source with { Trap = source.Trap with { Id = source.Lock.Id } };

        var diagnostic = Assert.Single(DoorAccessEventProfileValidator.Validate(invalid, "door/access")
, item => item.Code == "world.door.access_events_uuid_invalid");

        Assert.Equal("door/access/components", diagnostic.Path);
    }

    [Fact]
    public void Missing_optional_profile_falls_back_but_required_profile_diagnoses()
    {
        var door = Door();
        var optional = DoorAccessEventProfileValidator.Resolve(door with
        {
            AccessEvents = null,
            AccessEventsRequiredAtRuntime = false,
        });
        var required = DoorAccessEventProfileValidator.Resolve(door with { AccessEvents = null });

        Assert.True(optional.OptionalFallback);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal("world.door.access_events_required", Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void R082_Lock_event_policy_validates_exact_terminal_bindings()
    {
        var door = WithLockEvents(Door());

        var resolution = DoorAccessEventProfileValidator.ResolveLockEvents(door);

        Assert.True(resolution.Available);
        Assert.Equal("DoorLocked", resolution.Profile!.LockedBinding);
        Assert.Equal("DoorUnlocked", resolution.Profile.UnlockedBinding);
    }

    [Fact]
    public void R082_Invalid_and_missing_required_lock_events_have_stable_diagnostics()
    {
        var door = WithLockEvents(Door());
        var invalid = door with
        {
            AccessEvents = door.AccessEvents! with
            {
                LockStateEvents = door.AccessEvents.LockStateEvents! with
                {
                    Id = door.AccessEvents.Lock.Id,
                    UnlockedBinding = "unlocked",
                },
            },
        };
        var optional = DoorAccessEventProfileValidator.ResolveLockEvents(door with
        {
            AccessEvents = door.AccessEvents! with
            {
                LockStateEvents = null,
                LockStateEventsRequiredAtRuntime = false,
            },
        });
        var required = DoorAccessEventProfileValidator.ResolveLockEvents(door with
        {
            AccessEvents = door.AccessEvents! with { LockStateEvents = null },
        });

        var invalidDiagnostic = Assert.Single(DoorAccessEventProfileValidator.Validate(
            invalid.AccessEvents!, "doors/test/accessEvents"),
            item => item.Code == "world.door.lock_events_invalid");
        Assert.Equal("doors/test/accessEvents/lockStateEvents", invalidDiagnostic.Path);
        Assert.Contains(DoorAccessEventProfileValidator.Validate(invalid.AccessEvents!, "doors/test/accessEvents"),
            item => item.Code == "world.door.access_events_uuid_invalid");
        Assert.True(optional.OptionalFallback);
        Assert.Equal("world.door.lock_events_required", Assert.Single(required.Diagnostics).Code);
    }

    internal static DoorWorldProfile Door()
    {
        var id = Guid.Parse("81000000-0000-4000-8000-000000000001");
        return new(id, 1, "test:r081",
            BlueprintId.From(Guid.Parse("81000000-0000-4000-8000-000000000002")), null, null, [], null, null,
            null, null,
            new(Guid.Parse("81000000-0000-4000-8000-000000000010"), 1, "test:r081",
                "assets/doors/test.glb", TransformValue.Identity,
                new(Guid.Parse("81000000-0000-4000-8000-000000000011"), DoorAccessLockState.Locked,
                    ["key:ember"], "mechanics", 12, null, true, true, true, true, ["door.unlock"]),
                new(Guid.Parse("81000000-0000-4000-8000-000000000012"), "1.0.0", "DoorOpenRejected",
                    [new("fr-FR", "La porte est verrouillée."), new("en-US", "The door is locked.")],
                    "audio:locked", "animation:rattle", "scripts/door_rejected"),
                new(Guid.Parse("81000000-0000-4000-8000-000000000013"), "door:open", "damage:ember",
                    10, 12, 30, "ash-watch", "vfx:ember", "ash-watch", true, false, ["front", "back"],
                    ["interact"]),
                new(Guid.Parse("81000000-0000-4000-8000-000000000014"), "1.0.0",
                    "PortalUseRequested", "PortalUseCompleted"),
                new(Guid.Parse("81000000-0000-4000-8000-000000000015"), "1.0.0",
                    "DoorOpened", "DoorClosed"),
                new Dictionary<string, string> { ["failure.priority"] = "lock-before-trap" }), true);
    }

    internal static DoorWorldProfile WithLockEvents(DoorWorldProfile door) => door with
    {
        AccessEvents = door.AccessEvents! with
        {
            LockStateEvents = new(Guid.Parse("82000000-0000-4000-8000-000000000016"), 1, "test:r082",
                "1.0.0", "DoorLocked", "DoorUnlocked"),
            LockStateEventsRequiredAtRuntime = true,
        },
    };
}
