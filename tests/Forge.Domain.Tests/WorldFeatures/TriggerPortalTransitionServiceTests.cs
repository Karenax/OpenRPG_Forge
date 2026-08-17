using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class TriggerPortalTransitionServiceTests
{
    [Fact]
    public void ResolveRequiresAFiredEntryAndEveryPortalPermission()
    {
        var profile = Profile();
        var entered = Entered(fired: true);

        var denied = TriggerPortalTransitionService.Resolve(profile, entered,
            new HashSet<string>(["travel"], StringComparer.Ordinal));
        var accepted = TriggerPortalTransitionService.Resolve(profile, entered,
            new HashSet<string>(["travel", "chapter.two"], StringComparer.Ordinal));
        var dormant = TriggerPortalTransitionService.Resolve(profile, Entered(fired: false),
            new HashSet<string>(["travel", "chapter.two"], StringComparer.Ordinal));

        Assert.True(denied.Requested);
        Assert.False(denied.Authorized);
        Assert.Null(denied.Portal);
        Assert.Equal(["chapter.two"], denied.MissingPermissions);
        Assert.Equal(TriggerPortalTransitionService.PermissionDeniedCode, denied.DiagnosticCode);
        Assert.True(accepted.Requested);
        Assert.True(accepted.Authorized);
        Assert.Same(profile.Foundation!.Portal, accepted.Portal);
        Assert.Empty(accepted.MissingPermissions);
        Assert.False(dormant.Requested);
        Assert.Equal("trigger-not-fired", dormant.Reason);
    }

    [Fact]
    public void ResolveKeepsOptionalAbsenceSeparateFromRequiredFoundationFailure()
    {
        var entered = Entered(fired: true);
        var optionalFoundation = Profile() with { Foundation = null, FoundationRequiredAtRuntime = false };
        var requiredFoundation = optionalFoundation with { FoundationRequiredAtRuntime = true };
        var optionalPortal = Profile() with
        {
            Foundation = Profile().Foundation! with { Portal = null },
        };

        var optional = TriggerPortalTransitionService.Resolve(optionalFoundation, entered, new HashSet<string>());
        var required = TriggerPortalTransitionService.Resolve(requiredFoundation, entered, new HashSet<string>());
        var noPortal = TriggerPortalTransitionService.Resolve(optionalPortal, entered, new HashSet<string>());

        Assert.Empty(optional.DiagnosticCode);
        Assert.Equal("foundation-optional-fallback", optional.Reason);
        Assert.Equal(TriggerPortalTransitionService.FoundationRequiredCode, required.DiagnosticCode);
        Assert.Equal("foundation-required-missing", required.Reason);
        Assert.Empty(noPortal.DiagnosticCode);
        Assert.Equal("portal-optional-fallback", noPortal.Reason);
    }

    private static WorldFeatureRuntimeResolver.TriggerEnterResult Entered(bool fired)
    {
        var state = new WorldFeatureRuntimeResolver.TriggerRuntimeState(
            new Dictionary<string, DateTimeOffset>(),
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            false);
        return new(state,
            fired
                ? new WorldFeatureRuntimeResolver.TriggerVolumeEntered(Guid.NewGuid(), 1, "test:nav-h",
                    Guid.NewGuid(), "player", Vector3Value.Zero, new(0, 0, 1),
                    new HashSet<string>(["characters"], StringComparer.Ordinal),
                    new Dictionary<string, string>(), DateTimeOffset.UnixEpoch, "VolumeEntered", "1.0.0",
                    null, null)
                : null,
            [], fired, fired ? "fired" : "filtered");
    }

    private static TriggerWorldProfile Profile()
    {
        var profileId = Guid.Parse("ac620000-0000-4000-8000-000000000001");
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Guid.Parse("ac620000-0000-4000-8000-000000000002"), 1, "test:nav-h", "passage-brumes",
            new(TriggerWorldProfile.ShapeKind.Box3D, [], [], new(4, 3, 4), 3, .25, true),
            new(["creature"], ["player"], [], ["characters"]), ["travel.ready"], 0,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new(Guid.Parse("ac620000-0000-4000-8000-000000000003"), "transition", "area:crypt",
                new Dictionary<string, string>())],
            new(TriggerWorldProfile.DestinationKind.Waypoint,
                Guid.Parse("ac620000-0000-4000-8000-000000000004"), "arrival-crypt", new(0, 180, 0), true,
                ["travel", "chapter.two"]),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string> { ["editor.overlay"] = "volume" },
            new(Guid.Parse("ac620000-0000-4000-8000-000000000005"), 1, "test:nav-h", "1.0.0",
                "VolumeEntered", null, null),
            new(Guid.Parse("ac620000-0000-4000-8000-000000000006"), 1, "test:nav-h", "1.0.0",
                "VolumeExited", null, null));
        return new(profileId, 1, "test:nav-h",
            BlueprintId.From(Guid.Parse("ac620000-0000-4000-8000-000000000007")),
            AreaId.From(Guid.Parse("ac620000-0000-4000-8000-000000000008")),
            null, null, null, "trigger.nav-h", null, null, null, null, null, foundation, true);
    }
}
