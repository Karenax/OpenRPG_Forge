using System.Text.RegularExpressions;
using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public static partial class CreatureEventValidator
{
    public const string MissingRequiredCode = "creature.events.required_missing";
    private static readonly string[] SupportedApiVersions = ["1.0"];

    public static IReadOnlyList<CreatureEventDiagnostic> Validate(
        CreatureEventProfile profile,
        string path = "creatureEvents")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureEventDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.events.uuid_required", path,
            "A stable creature-event UUID is required.");
        Add(profile.SchemaVersion != CreatureEventProfile.CurrentSchemaVersion,
            "creature.events.schema_unsupported", path, "The creature-event schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.events.provenance_required", path,
            "Creature-event provenance is required.");

        if (profile.CombatBoundary is not null)
        {
            var expected = profile.CombatBoundary.Mode == CreatureCombatBoundaryMode.TurnEnded
                ? "creature.turn_ended"
                : "creature.combat_tick_completed";
            ValidateBinding(profile.CombatBoundary.Binding, expected, $"{path}/combatBoundary/binding", diagnostics);
        }
        if (profile.Inventory is not null)
        {
            ValidateBinding(profile.Inventory.Accessed, "creature.inventory_accessed",
                $"{path}/inventory/accessed", diagnostics);
            ValidateBinding(profile.Inventory.Transferred, "creature.item_transferred",
                $"{path}/inventory/transferred", diagnostics);
        }
        if (profile.Rest is not null)
            ValidateBinding(profile.Rest.Completed, "creature.rest_completed", $"{path}/rest/completed", diagnostics);
        if (profile.Heartbeat is not null)
        {
            ValidateBinding(profile.Heartbeat.Binding, "creature.heartbeat", $"{path}/heartbeat/binding", diagnostics);
            Add(profile.Heartbeat.IntervalMilliseconds is < 250 or > 86_400_000,
                "creature.events.heartbeat_interval_invalid", $"{path}/heartbeat/intervalMilliseconds",
                "Heartbeat cadence must be between 250 ms and 24 hours.");
            Add(profile.Heartbeat.BudgetMilliseconds is < 1 or > 1000 ||
                profile.Heartbeat.BudgetMilliseconds > profile.Heartbeat.IntervalMilliseconds,
                "creature.events.heartbeat_budget_invalid", $"{path}/heartbeat/budgetMilliseconds",
                "Heartbeat budget must be positive, bounded and no longer than its cadence.");
        }

        if (profile.CustomEvents.Count > 64 || HasDuplicate(profile.CustomEvents.Select(item => item.SchemaId)) ||
            HasDuplicate(profile.CustomEvents.Select(item => item.QualifiedName)))
            diagnostics.Add(new("creature.events.custom_duplicate", $"{path}/customEvents",
                "Custom event schemas require unique UUIDs and qualified names."));
        foreach (var custom in profile.CustomEvents)
        {
            var customPath = $"{path}/customEvents/{custom.SchemaId}";
            Add(custom.SchemaId == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(custom.Namespace) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(custom.Name) || custom.PayloadVersion is < 1 or > 999,
                "creature.events.custom_schema_invalid", customPath,
                "Custom events require a stable UUID, canonical namespace/name and bounded payload version.");
            Add(custom.Fields.Count > 64 || custom.Fields.Any(item =>
                    !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) || !Enum.IsDefined(item.Value)),
                "creature.events.custom_fields_invalid", $"{customPath}/fields",
                "Custom event fields must be unique canonical names with supported types.");
            ValidateBinding(custom.Binding, $"custom.{custom.QualifiedName}", $"{customPath}/binding", diagnostics);
        }

        var bindings = EnumerateBindings(profile).ToArray();
        if (HasDuplicate(bindings.Select(item => item.Id)))
            diagnostics.Add(new("creature.events.binding_uuid_duplicate", path,
                "Every creature-event binding must own a unique UUID."));
        if (profile.Overrides.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) ||
                                          string.IsNullOrWhiteSpace(item.Value)))
            diagnostics.Add(new("creature.events.override_invalid", $"{path}/overrides",
                "Overrides require canonical keys and explicit non-empty values."));
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    public static IReadOnlyList<EventBinding> EnumerateBindings(CreatureEventProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = new List<EventBinding>();
        if (profile.CombatBoundary is not null) result.Add(profile.CombatBoundary.Binding);
        if (profile.Inventory is not null)
        {
            result.Add(profile.Inventory.Accessed);
            result.Add(profile.Inventory.Transferred);
        }
        if (profile.Rest is not null) result.Add(profile.Rest.Completed);
        if (profile.Heartbeat is not null) result.Add(profile.Heartbeat.Binding);
        result.AddRange(profile.CustomEvents.Select(item => item.Binding));
        return result;
    }

    private static void ValidateBinding(
        EventBinding binding,
        string expectedEvent,
        string path,
        ICollection<CreatureEventDiagnostic> diagnostics)
    {
        if (binding.Id == Guid.Empty || binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.Provenance) ||
            !SupportedApiVersions.Contains(binding.ForgeScriptApiVersion, StringComparer.Ordinal) ||
            !string.Equals(binding.EventName, expectedEvent, StringComparison.Ordinal) ||
            !HandlerPattern().IsMatch(binding.Handler) || binding.Authority != EventAuthorityScope.Server)
            diagnostics.Add(new("creature.events.binding_invalid", path,
                $"Binding '{expectedEvent}' requires a UUID, schema v1, ForgeScript 1.0, a handler and server authority."));
    }

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerPattern();
}
