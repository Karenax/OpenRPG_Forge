using Forge.Domain.Areas;
using Forge.Domain.Factions;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class EncounterTacticalRuntimeServiceTests
{
    private static readonly Guid EncounterId = Guid.Parse("a2091000-0000-4000-8000-000000000001");
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a2091000-0000-4000-8000-000000000002"));
    private static readonly Guid FactionId = Guid.Parse("a2091000-0000-4000-8000-000000000003");
    private static readonly Guid RouteId = Guid.Parse("a2091000-0000-4000-8000-000000000004");

    [Fact]
    public void ReinforcementConsumesAuthoredReferencesAlertDelayCapCancelVisibilityAndAuthority()
    {
        var profile = Profile();
        var approved = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 1));
        var alert = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 1, elapsed: 3, active: 1));
        var delay = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 2.9, active: 1));
        var cap = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 4));
        var cancelled = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 1,
                conditions: new HashSet<string>(["boss.dead"], StringComparer.Ordinal)));
        var visible = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 1, visible: true));
        var denied = EncounterTacticalRuntimeService.ResolveReinforcement(profile, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 1, networked: true, authority: false));

        Assert.True(approved.Reinforce);
        Assert.Equal("ash-watch", approved.FactionSlug);
        Assert.Equal(RouteId, approved.RouteId);
        Assert.Equal(3, approved.RemainingActorSlots);
        Assert.Equal("vfx.spawn.ash", approved.SpawnEffectId);
        Assert.Equal("alert-threshold", alert.Reason);
        Assert.Equal("reinforcement-delay", delay.Reason);
        Assert.Equal("actor-cap", cap.Reason);
        Assert.Equal("cancel-condition", cancelled.Reason);
        Assert.Equal("visible-to-camera", visible.Reason);
        Assert.Equal("authority-denied", denied.Reason);
        Assert.Contains(denied.Diagnostics,
            item => item.Code == EncounterTacticalRuntimeService.AuthorityDeniedCode);
    }

    [Fact]
    public void VisibilityConsumesDistanceCameraAndLineOfSightWithoutQueryingTheEngine()
    {
        var profile = Profile();
        var approved = EncounterTacticalRuntimeService.ResolveVisibility(profile, new(10, false, true));
        var close = EncounterTacticalRuntimeService.ResolveVisibility(profile, new(9.9, false, true));
        var visible = EncounterTacticalRuntimeService.ResolveVisibility(profile, new(10, true, true));
        var openLos = EncounterTacticalRuntimeService.ResolveVisibility(profile, new(10, false, false));
        var inView = EncounterTacticalRuntimeService.ResolveVisibility(profile with
        {
            Visibility = profile.Visibility with
            {
                Mode = EncounterWorldProfile.VisibilityMode.InViewAllowed,
                RequireLineOfSightBlocked = false,
            },
        }, new(10, true, false));
        var everyMode = Enum.GetValues<EncounterWorldProfile.VisibilityMode>().Select(mode =>
            EncounterTacticalRuntimeService.ResolveVisibility(profile with
            {
                Visibility = new(mode, 0, false,
                    mode == EncounterWorldProfile.VisibilityMode.Effect ? "vfx.spawn.mode" : string.Empty),
            }, new(0, mode == EncounterWorldProfile.VisibilityMode.InViewAllowed, false))).ToArray();

        Assert.True(approved.SpawnAllowed);
        Assert.Equal("vfx.spawn.ash", approved.SpawnEffectId);
        Assert.Equal("camera-distance", close.Reason);
        Assert.Equal("visible-to-camera", visible.Reason);
        Assert.Equal("line-of-sight-open", openLos.Reason);
        Assert.True(inView.SpawnAllowed);
        Assert.All(everyMode, result => Assert.True(result.SpawnAllowed));
    }

    [Fact]
    public void CleanupConsumesEveryLifecycleDelayUnloadPersistenceAndAuthority()
    {
        var profile = Profile();
        var dead = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Dead, 30, false, false, false, false));
        var escaped = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Escaped, 20, false, false, false, false));
        var abandoned = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Abandoned, 120, false, false, false, false));
        var unload = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Active, 0, true, false, false, false));
        var persistent = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Dead, 300, true, true, false, false));
        var denied = EncounterTacticalRuntimeService.ResolveCleanup(profile,
            new(EncounterActorLifecycleState.Dead, 300, false, false, true, false));

        Assert.True(dead.Eligible);
        Assert.False(escaped.Eligible);
        Assert.True(abandoned.Eligible);
        Assert.True(unload.Eligible);
        Assert.False(persistent.Eligible);
        Assert.True(persistent.Preserve);
        Assert.False(denied.Eligible);
        Assert.Contains(denied.Diagnostics,
            item => item.Code == EncounterTacticalRuntimeService.AuthorityDeniedCode);
    }

    [Fact]
    public void OptionalReinforcementFallsBackAndInvalidPoliciesReturnCodeAndPath()
    {
        var optional = Profile() with { Reinforcements = null };
        var fallback = EncounterTacticalRuntimeService.ResolveReinforcement(optional, Factions(), Waypoints(),
            Context(alert: 2, elapsed: 3, active: 0));
        var invalid = Profile() with
        {
            Reinforcements = Profile().Reinforcements! with
            {
                FactionId = "missing",
                RouteId = Guid.Parse("a2091000-0000-4000-8000-000000000099"),
                DelaySeconds = -1,
                CancelCondition = "custom::",
            },
            Visibility = new(EncounterWorldProfile.VisibilityMode.Effect, -1, false, string.Empty),
            Cleanup = new(double.NaN, -1, double.PositiveInfinity, true, true),
        };
        var diagnostics = EncounterTacticalRuntimeService.Validate(invalid, Factions(), Waypoints());

        Assert.Equal("reinforcement-optional-fallback", fallback.Reason);
        Assert.Contains(diagnostics, item => item.Code == EncounterTacticalRuntimeService.ReinforcementInvalidCode &&
                                             item.Path.EndsWith("/reinforcements", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterTacticalRuntimeService.ReinforcementFactionMissingCode &&
                                             item.Path.EndsWith("/factionId", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterTacticalRuntimeService.ReinforcementRouteMissingCode &&
                                             item.Path.EndsWith("/routeId", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterTacticalRuntimeService.VisibilityInvalidCode &&
                                             item.Path.EndsWith("/visibility", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == EncounterTacticalRuntimeService.CleanupInvalidCode &&
                                             item.Path.EndsWith("/cleanup", StringComparison.Ordinal));
    }

    private static EncounterReinforcementRuntimeContext Context(
        int alert,
        double elapsed,
        int active,
        IReadOnlySet<string>? conditions = null,
        bool visible = false,
        bool networked = false,
        bool authority = true) => new(alert, elapsed, active,
        conditions ?? new HashSet<string>(StringComparer.Ordinal),
        new(10, visible, true), networked, authority);

    private static EncounterWorldProfile Profile() => new(
        EncounterId, 1, "test:ux2-lw-j", AreaId,
        null, null, null,
        new(2, "ash-watch", RouteId, 4, 3, "boss.dead"),
        new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
        new(30, 30, 120, true, true));

    private static FactionCatalog Factions() => new(
        [new(FactionId, 1, "test:ux2-lw-j", "ash-watch", "Veilleurs des cendres", false, [], ["guard"])],
        [], [], []);

    private static WaypointAuthoringProfile Waypoints()
    {
        var route = new PatrolRoute(RouteId, 1, "test:ux2-lw-j", PatrolTraversalPolicy.Once,
        [
            new(Guid.Parse("a2091000-0000-4000-8000-000000000011"), Vector3Value.Zero, 0, 0, 2, null, null),
            new(Guid.Parse("a2091000-0000-4000-8000-000000000012"), new(8, 0, 0), 0, 0, 2, null, null),
        ]);
        return new(Guid.Parse("a2091000-0000-4000-8000-000000000010"), 1, "test:ux2-lw-j",
            AreaId, false, [], [route]);
    }
}
