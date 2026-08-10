using System.Text.RegularExpressions;
using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public static partial class CreatureFoundationValidator
{
    public const string MissingRequiredCode = "creature.foundation.required_missing";

    public static IReadOnlyList<CreatureFoundationDiagnostic> Validate(
        CreatureFoundationProfile profile,
        string path = "creatureFoundation")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.foundation.uuid_required", path, "A stable creature UUID is required.");
        Add(profile.SchemaVersion != CreatureFoundationProfile.CurrentSchemaVersion,
            "creature.foundation.schema_unsupported", path, "The creature foundation schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.foundation.provenance_required", path,
            "Creature foundation provenance is required.");

        Add(!CreaturePresentationProfile.IsCanonicalIdentifier(profile.Identity.Slug) ||
            !profile.Identity.Names.TryGetValue("fr", out var name) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(profile.Identity.Pronouns),
            "creature.foundation.identity_invalid", $"{path}/identity",
            "Identity requires a canonical slug, a French display name and pronouns.");
        Add(profile.Identity.Names.Any(item => !LocalePattern().IsMatch(item.Key) || string.IsNullOrWhiteSpace(item.Value)) ||
            profile.Identity.Biographies.Any(item => !LocalePattern().IsMatch(item.Key)),
            "creature.foundation.localization_invalid", $"{path}/identity/localization",
            "Localized creature text requires canonical locale keys and non-empty names.");
        Add(profile.Identity.NarrativeTags.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item)),
            "creature.foundation.tags_invalid", $"{path}/identity/narrativeTags",
            "Narrative tags must be canonical identifiers.");

        Add(profile.Resources.Count is < 1 or > 32 || HasDuplicate(profile.Resources.Select(item => item.Id)) ||
            HasDuplicate(profile.Resources.Select(item => item.ResourceId)),
            "creature.foundation.resources_duplicate", $"{path}/resources",
            "Creature resources require one to 32 unique UUIDs and identifiers.");
        foreach (var resource in profile.Resources)
            Add(resource.Id == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(resource.ResourceId) ||
                resource.Maximum <= 0 || resource.Current < 0 || resource.Current > resource.Maximum ||
                resource.RegenerationPerSecond < 0 || !Finite(resource.Maximum, resource.Current, resource.RegenerationPerSecond),
                "creature.foundation.resource_invalid", $"{path}/resources/{resource.Id}",
                "A resource requires a UUID, canonical ID and finite bounded values.");

        var movement = profile.Movement;
        Add(!Enum.IsDefined(movement.Mode) || movement.MaximumSpeed <= 0 || movement.Acceleration <= 0 ||
            movement.Deceleration < 0 || movement.MaximumAltitude < movement.MinimumAltitude ||
            movement.MaximumSlopeDegrees is < 0 or > 90 ||
            !Finite(movement.MaximumSpeed, movement.Acceleration, movement.Deceleration,
                movement.MinimumAltitude, movement.MaximumAltitude, movement.MaximumSlopeDegrees),
            "creature.foundation.movement_invalid", $"{path}/movement",
            "Movement requires a supported mode and finite speed, acceleration, altitude and slope bounds.");
        Add(movement.Mode == CreatureFoundationMovementMode.Flying &&
            movement.MaximumAltitude <= movement.MinimumAltitude,
            "creature.foundation.flight_altitude_invalid", $"{path}/movement/maximumAltitude",
            "Flying creatures require a non-empty altitude range.");

        var references = new[] { profile.References.AppearanceId, profile.References.RigId,
            profile.References.PerceptionProfileId, profile.References.CombatProfileId,
            profile.References.InventoryProfileId, profile.References.FactionId };
        Add(references.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value)) ||
            profile.References.DialogueId is not null &&
            !CreaturePresentationProfile.IsCanonicalIdentifier(profile.References.DialogueId),
            "creature.foundation.reference_invalid", $"{path}/references",
            "Creature component references must use canonical identifiers.");
        Add(profile.Spawned.Id == Guid.Empty || profile.Spawned.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(profile.Spawned.Provenance) || profile.Spawned.ForgeScriptApiVersion != "1.0" ||
            profile.Spawned.EventName != "creature.spawned" || !HandlerPattern().IsMatch(profile.Spawned.Handler) ||
            profile.Spawned.Authority != EventAuthorityScope.Server,
            "creature.foundation.spawn_binding_invalid", $"{path}/spawned",
            "Spawn binding requires a UUID, schema v1, ForgeScript 1.0, handler and server authority.");
        Add(profile.Overrides.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) ||
                                          string.IsNullOrWhiteSpace(item.Value)),
            "creature.foundation.override_invalid", $"{path}/overrides",
            "Overrides require canonical keys and explicit values.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    public static CreatureSpawnedValue Spawn(
        CreatureFoundationProfile profile,
        string blueprintId,
        CreatureSpawnContextValue context,
        Func<Guid> eventIdSource)
    {
        var diagnostics = Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(diagnostics[0].Code);
        if (string.IsNullOrWhiteSpace(blueprintId) || context.SpawnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.AreaId) || string.IsNullOrWhiteSpace(context.Cause) ||
            context.Authority != EventAuthorityScope.Server)
            throw new InvalidOperationException("creature.foundation.spawn_context_invalid");
        var eventId = eventIdSource();
        if (eventId == Guid.Empty) throw new InvalidOperationException("creature.foundation.spawn_event_uuid_required");
        return new(eventId, profile.Id, blueprintId, context,
            profile.Resources.ToDictionary(item => item.ResourceId, item => item.Current, StringComparer.Ordinal));
    }

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();

    [GeneratedRegex("^[a-z]{2,3}(?:-[a-z0-9]{2,8})?$")]
    private static partial Regex LocalePattern();
}

public sealed record CreatureFoundationRuntimeResult(
    CreatureFoundationProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureFoundationRuntimeResolver
{
    public static CreatureFoundationRuntimeResult Resolve(CreatureFoundationProfile? profile, bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new CreatureFoundationDiagnostic(CreatureFoundationValidator.MissingRequiredCode,
                    "creatureFoundation", "A required creature foundation is missing.")]
                : []);
        var diagnostics = CreatureFoundationValidator.Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }
}
