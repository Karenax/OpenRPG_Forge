using Forge.Domain.Spatial;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionExperienceFoundationServiceTests
{
    [Fact]
    public void R076Death_supports_six_persisted_lifecycle_policies_and_effects()
    {
        var profile = Profile();
        var modes = new[]
        {
            "downed", "permanent-death", "recoverable-corpse", "resurrection", "replacement", "memorial",
        };
        var transitions = modes.Select(mode => CompanionExperienceFoundationService.Defeat(
            profile with { Experience = profile.Experience! with { Death = profile.Experience.Death with { Mode = mode } } },
            DateTimeOffset.UnixEpoch)).ToArray();

        Assert.Equal(6, transitions.Length);
        Assert.True(transitions[0].State.Downed);
        Assert.Contains("drop-inventory", transitions[1].Effects);
        Assert.True(transitions[2].State.CorpseAvailable);
        Assert.Contains("requires:resource:ember", transitions[3].Effects);
        Assert.Equal("creature:heir", transitions[4].State.ReplacementId);
        Assert.True(transitions[5].State.Memorialized);
    }

    [Fact]
    public void R076Cohesion_orders_navigation_portal_and_safe_teleport_with_visibility_guards_and_audit()
    {
        var profile = Profile();
        Assert.Equal(CohesionRecoveryKind.Navigate,
            CompanionExperienceFoundationService.Recover(profile, Context(15, true, false)).Kind);
        Assert.Equal(CohesionRecoveryKind.FollowPortal,
            CompanionExperienceFoundationService.Recover(profile, Context(40, false, true)).Kind);
        var teleported = CompanionExperienceFoundationService.Recover(profile, Context(80, false, false));
        Assert.Equal(CohesionRecoveryKind.SafeTeleport, teleported.Kind);
        Assert.Equal("anchor:party-safe", teleported.AnchorId);
        Assert.Contains("safe-teleport", teleported.Audit);
        Assert.Equal(CohesionRecoveryKind.None,
            CompanionExperienceFoundationService.Recover(profile, Context(80, false, false) with { InCombat = true }).Kind);
        Assert.Equal(CohesionRecoveryKind.None,
            CompanionExperienceFoundationService.Recover(profile, Context(80, false, false) with { VisibleToPlayers = true }).Kind);
    }

    [Fact]
    public void R076Party_hud_is_accessible_and_optional_required_resolution_is_explicit()
    {
        var profile = Profile();
        var hud = CompanionExperienceFoundationService.CreateHud(profile);
        Assert.True(hud.Portraits && hud.Health && hud.Resources && hud.Statuses && hud.QuickCommands &&
                    hud.Reorder && hud.Keyboard && hud.Gamepad && hud.ScreenReaderLabels && hud.HighContrast);
        Assert.Equal(["health", "focus"], hud.VisibleResourceIds);
        Assert.Empty(CompanionExperienceFoundationService.Validate(profile));
        Assert.True(CompanionExperienceFoundationService.Resolve(profile with
        { Experience = null, ExperienceRequiredAtRuntime = false }).OptionalFallback);
        Assert.Equal("RPG_COMPANION_EXPERIENCE_REQUIRED",
            Assert.Single(CompanionExperienceFoundationService.Resolve(profile with { Experience = null }).Diagnostics).Code);
    }

    internal static Forge.Domain.WorldFeatures.CompanionWorldProfile Profile()
    {
        var profile = CompanionControlFoundationServiceTests.Profile();
        return profile with
        {
            Experience = new(Guid.Parse("76000000-0000-4000-8000-000000000001"), 1, "test:r076",
                new(Guid.Parse("76000000-0000-4000-8000-000000000002"), 1, "test:r076",
                    "recoverable-corpse", 30, "resource:ember", "creature:heir", "memorial:elyra", true),
                new(Guid.Parse("76000000-0000-4000-8000-000000000003"), 1, "test:r076",
                    10, 30, 60, true, true, ["party.safe"]),
                new(Guid.Parse("76000000-0000-4000-8000-000000000004"), 1, "test:r076",
                    true, true, true, true, true, true, true, true, true, true, ["health", "focus"])),
            ExperienceRequiredAtRuntime = true,
        };
    }

    private static CohesionContext Context(double distance, bool nav, bool portal) =>
        new(distance, nav, portal, false, false,
        [
            ("anchor:party-safe", new Vector3Value(2, 0, 2),
                new HashSet<string>(["party.safe"], StringComparer.Ordinal), true),
        ]);
}
