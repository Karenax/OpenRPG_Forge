using System.Text.RegularExpressions;
using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public static partial class CreatureInteractionCompositionValidator
{
    public const string MissingRequiredCode = "creature.interaction.required_missing";

    public static IReadOnlyList<CreatureFoundationDiagnostic> Validate(
        CreatureInteractionCompositionProfile profile,
        string path = "creatureFoundation/interactionComposition")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.interaction.uuid_required", path,
            "A stable creature-interaction UUID is required.");
        Add(profile.SchemaVersion != CreatureInteractionCompositionProfile.CurrentSchemaVersion,
            "creature.interaction.schema_unsupported", path, "The creature-interaction schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.interaction.provenance_required", path,
            "Creature-interaction provenance is required.");
        Add(!Valid(profile.AssetId), "creature.interaction.asset_invalid", $"{path}/assetId",
            "Creature interaction requires a canonical asset identity.");

        var componentIds = EnumerateComponentIds(profile).ToArray();
        Add(componentIds.Any(id => id == Guid.Empty) || componentIds.Distinct().Count() != componentIds.Length,
            "creature.interaction.component_uuid_invalid", path,
            "Creature-interaction profile, components, memberships, senses, bindings and event require unique UUIDs.");

        var factions = profile.Factions;
        Add(factions.PrimaryFactionId == Guid.Empty || factions.Memberships.Count is < 1 or > 32 ||
            !factions.Memberships.Any(item => item.FactionId == factions.PrimaryFactionId) ||
            HasDuplicate(factions.Memberships.Select(item => item.FactionId)) || factions.Memberships.Any(item =>
                item.FactionId == Guid.Empty || !double.IsFinite(item.Weight) || item.Weight is < 0 or > 1 ||
                !Valid(item.Rank) || !double.IsFinite(item.PersonalReputation) ||
                item.PersonalReputation is < -100 or > 100),
            "creature.interaction.factions_invalid", $"{path}/factions/{factions.Id}",
            "Faction membership requires a primary faction, unique weighted memberships, ranks, reputations and inheritance.");

        var perception = profile.Perception;
        Add(perception.Senses.Count is < 1 or > 4 || !double.IsFinite(perception.MemorySeconds) ||
            perception.MemorySeconds is < 0 or > 86400 || HasDuplicate(perception.Senses.Select(item => item.Kind)) ||
            perception.Senses.Any(item => !Enum.IsDefined(item.Kind) || !double.IsFinite(item.Range) ||
                item.Range is <= 0 or > 100000 || !double.IsFinite(item.ConeDegrees) ||
                item.ConeDegrees is <= 0 or > 360 || item.StimulusTags.Count is < 1 or > 64 ||
                item.StimulusTags.Any(tag => !Valid(tag))),
            "creature.interaction.perception_invalid", $"{path}/perception/{perception.Id}",
            "Perception requires unique supported senses with bounded range, cone, occlusion, stimuli and memory.");

        Add(profile.DialogueBindings.Count > 64 ||
            HasDuplicate(profile.DialogueBindings.Select(item => item.Id)) || profile.DialogueBindings.Any(item =>
                !Valid(item.Context) || item.Priority is < -1000 or > 1000 || item.DialogueId is null ||
                item.InterlocutorId is not null && !Valid(item.InterlocutorId) ||
                item.RequiredState is not null && !Valid(item.RequiredState)) ||
            profile.DialogueBindings.GroupBy(item => item.Context, StringComparer.Ordinal)
                .Any(group => group.Count(item => item.Fallback) > 1),
            "creature.interaction.dialogue_bindings_invalid", $"{path}/dialogueBindings",
            "Dialogue bindings require stable IDs, context, bounded priority, optional interlocutor/state and one fallback per context.");

        var protection = profile.Protection;
        Add(protection.GameMasterPermissions.Count > 64 || protection.GameMasterPermissions.Any(item => !Valid(item)),
            "creature.interaction.protection_invalid", $"{path}/protection/{protection.Id}",
            "Protection policy permissions must be unique canonical identifiers.");

        var binding = profile.OnConversation;
        Add(binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance) ||
            binding.ForgeScriptApiVersion != "1.0" || binding.EventName != "creature.interaction_requested" ||
            !HandlerPattern().IsMatch(binding.Handler) || binding.Authority != EventAuthorityScope.Server,
            "creature.interaction.on_conversation_invalid", $"{path}/onConversation/{binding.Id}",
            "OnConversation requires schema v1, ForgeScript 1.0, a handler and server authority.");
        Add(profile.Overrides.Any(item => !Valid(item.Key) || string.IsNullOrWhiteSpace(item.Value)),
            "creature.interaction.override_invalid", $"{path}/overrides",
            "Creature-interaction overrides require canonical keys and explicit values.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    public static CreatureInteractionDialogueDecision RequestDialogue(
        CreatureInteractionCompositionProfile profile,
        string context,
        string? interlocutorId,
        string? state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (Validate(profile).Count > 0 || !Valid(context))
            return new(false, null, "creature.interaction.request_invalid");
        var normalizedContext = CreaturePresentationProfile.NormalizeIdentifier(context);
        var normalizedInterlocutor = string.IsNullOrWhiteSpace(interlocutorId)
            ? null
            : CreaturePresentationProfile.NormalizeIdentifier(interlocutorId);
        var normalizedState = string.IsNullOrWhiteSpace(state)
            ? null
            : CreaturePresentationProfile.NormalizeIdentifier(state);
        var selected = profile.DialogueBindings.Where(item => item.Context == normalizedContext &&
                (item.InterlocutorId is null || item.InterlocutorId == normalizedInterlocutor) &&
                (item.RequiredState is null || item.RequiredState == normalizedState))
            .OrderByDescending(item => item.Priority).ThenBy(item => item.DialogueId.ToString(), StringComparer.Ordinal)
            .FirstOrDefault() ?? profile.DialogueBindings.Where(item => item.Context == normalizedContext && item.Fallback)
            .OrderByDescending(item => item.Priority).ThenBy(item => item.DialogueId.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null) return new(false, null, "creature.interaction.dialogue_not_found");
        if (!profile.Protection.DialogueInterruptible && normalizedContext == "interrupt")
            return new(false, null, "creature.interaction.dialogue_protected");
        return new(true, selected, null);
    }

    public static IEnumerable<Guid> EnumerateComponentIds(CreatureInteractionCompositionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        yield return profile.Id;
        yield return profile.Factions.Id;
        foreach (var item in profile.Factions.Memberships) yield return item.Id;
        yield return profile.Perception.Id;
        foreach (var item in profile.Perception.Senses) yield return item.Id;
        foreach (var item in profile.DialogueBindings) yield return item.Id;
        yield return profile.Protection.Id;
        yield return profile.OnConversation.Id;
    }

    private static bool Valid(string value) => CreaturePresentationProfile.IsCanonicalIdentifier(value);

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}
public sealed record CreatureInteractionCompositionRuntimeResult(
    CreatureInteractionCompositionProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureInteractionCompositionRuntimeResolver
{
    public static CreatureInteractionCompositionRuntimeResult Resolve(
        CreatureInteractionCompositionProfile? profile,
        bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new CreatureFoundationDiagnostic(CreatureInteractionCompositionValidator.MissingRequiredCode,
                    "creatureFoundation/interactionComposition", "A required creature-interaction profile is missing.")]
                : []);
        var diagnostics = CreatureInteractionCompositionValidator.Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }
}
