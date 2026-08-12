using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Application.Companions;

public sealed record CompanionExperienceResolution(
    CompanionWorldProfile.ExperienceProfile? Profile,
    bool OptionalFallback,
    IReadOnlyList<CompanionDiagnostic> Diagnostics);

public static class CompanionExperienceFoundationService
{
    public static CompanionExperienceResolution Resolve(CompanionWorldProfile profile)
    {
        if (profile.Experience is not null) return new(profile.Experience, false, []);
        return profile.ExperienceRequiredAtRuntime
            ? new(null, false,
            [
                new("RPG_COMPANION_EXPERIENCE_REQUIRED", "companion/experience",
                    "Required companion experience profile is missing."),
            ])
            : new(null, true, []);
    }

    public static CompanionDeathPolicy CreateDeath(CompanionWorldProfile profile)
    {
        var death = Require(profile).Death;
        return new(ParseDeathMode(death.Mode), TimeSpan.FromSeconds(death.DownedDurationSeconds),
            death.ResurrectionResourceId, death.ReplacementCreatureId, death.MemorialId, death.DropInventory);
    }

    public static CohesionPolicy CreateCohesion(CompanionWorldProfile profile)
    {
        var cohesion = Require(profile).Cohesion;
        return new(cohesion.NavigateThreshold, cohesion.PortalThreshold, cohesion.TeleportThreshold,
            cohesion.FollowPortals, cohesion.AllowSafeTeleport,
            cohesion.SafeAnchorTags.ToHashSet(StringComparer.Ordinal));
    }

    public static PartyHudConfiguration CreateHud(CompanionWorldProfile profile)
    {
        var hud = Require(profile).Hud;
        return new(hud.Portraits, hud.Health, hud.Resources, hud.Statuses, hud.QuickCommands, hud.Reorder,
            hud.Keyboard, hud.Gamepad, hud.ScreenReaderLabels, hud.HighContrast, hud.VisibleResourceIds);
    }

    public static CompanionLifeTransition Defeat(CompanionWorldProfile profile, DateTimeOffset at) =>
        CompanionExperienceService.Defeat(CreateDeath(profile), at);

    public static CohesionDecision Recover(CompanionWorldProfile profile, CohesionContext context) =>
        CompanionExperienceService.Recover(CreateCohesion(profile), context);

    public static IReadOnlyList<CompanionDiagnostic> Validate(CompanionWorldProfile profile)
    {
        if (profile.Experience is null) return Resolve(profile).Diagnostics;
        var equipment = profile.Control is null
            ? new CompanionEquipmentPolicy(CompanionEquipmentPermission.Full, new HashSet<Guid>(),
                new Dictionary<string, Guid> { ["main-hand"] = profile.Id }, true, "companion:runtime")
            : CompanionControlFoundationService.CreateEquipment(profile);
        return CompanionExperienceService.Validate(equipment, CreateDeath(profile), CreateCohesion(profile),
            CreateHud(profile));
    }

    private static CompanionWorldProfile.ExperienceProfile Require(CompanionWorldProfile profile) =>
        Resolve(profile).Profile ?? throw new InvalidOperationException("Companion experience profile is unavailable.");

    private static CompanionDeathMode ParseDeathMode(string value) => value switch
    {
        "downed" => CompanionDeathMode.Downed,
        "permanent-death" => CompanionDeathMode.PermanentDeath,
        "recoverable-corpse" => CompanionDeathMode.RecoverableCorpse,
        "resurrection" => CompanionDeathMode.Resurrection,
        "replacement" => CompanionDeathMode.Replacement,
        "memorial" => CompanionDeathMode.Memorial,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported companion death mode."),
    };
}
