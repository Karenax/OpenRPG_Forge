using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterCompositionResolution(
    bool Available,
    bool OptionalFallback,
    EncounterCompositionProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class EncounterCompositionProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(
        EncounterCompositionProfile profile,
        EncounterWorldProfile.FoundationPolicy? foundation,
        string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostics = new List<DomainDiagnostic>();

        if (profile.Id == Guid.Empty || profile.SchemaVersion != EncounterCompositionProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.Slug) ||
            string.IsNullOrWhiteSpace(profile.DisplayName) || string.IsNullOrWhiteSpace(profile.Category) ||
            string.IsNullOrWhiteSpace(profile.Theme))
            Add(diagnostics, "world.encounter.composition_identity_invalid",
                "Encounter composition requires UUID, schema v1, provenance, slug, name, category and theme.", path);

        var componentIds = new[]
        {
            profile.Difficulty.Id,
            profile.Selection.Id,
            profile.Activation.Id,
        }.Concat(profile.Groups.Select(item => item.Id)).ToArray();
        if (componentIds.Any(item => item == Guid.Empty || item == profile.Id) ||
            componentIds.Distinct().Count() != componentIds.Length)
            Add(diagnostics, "world.encounter.composition_uuid_invalid",
                "Encounter composition component UUIDs must be non-empty and unique.", $"{path}/components");

        var difficulty = profile.Difficulty;
        if (string.IsNullOrWhiteSpace(difficulty.RulesetId) ||
            string.IsNullOrWhiteSpace(difficulty.PartyAssessmentProviderId) || difficulty.ExpectedPartySize <= 0 ||
            !double.IsFinite(difficulty.ExpectedPartyPower) || difficulty.ExpectedPartyPower < 0 ||
            !double.IsFinite(difficulty.BaseBudget) || difficulty.BaseBudget <= 0 ||
            !double.IsFinite(difficulty.PartySizeMultiplier) || difficulty.PartySizeMultiplier <= 0 ||
            !double.IsFinite(difficulty.DifficultyMultiplier) || difficulty.DifficultyMultiplier <= 0 ||
            !double.IsFinite(difficulty.EffectiveBudget) || difficulty.EffectiveBudget <= 0)
            Add(diagnostics, "world.encounter.difficulty_invalid",
                "Encounter difficulty requires ruleset, party provider, positive estimates, budget and multipliers.",
                $"{path}/difficulty");

        var candidateIds = foundation?.Candidates.Select(item => item.Id).ToHashSet() ?? [];
        var networkCap = foundation?.NetworkActorCap ?? 0;
        if (profile.Groups.Count == 0 || profile.Groups.Any(group => group.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(group.Slug) || group.Minimum < 0 || group.Maximum < group.Minimum ||
            group.Budget <= 0 || group.CandidateIds.Count == 0 ||
            group.CandidateIds.Any(candidate => !candidateIds.Contains(candidate)) ||
            group.CandidateIds.Distinct().Count() != group.CandidateIds.Count ||
            networkCap > 0 && group.Maximum > networkCap))
            Add(diagnostics, "world.encounter.count_range_invalid",
                "Encounter groups require unique candidates, coherent min/max counts, budget and server cap.",
                $"{path}/groups");

        if (profile.Groups.Sum(group => group.Budget) > difficulty.EffectiveBudget ||
            foundation is not null && difficulty.EffectiveBudget > foundation.SpawnBudget)
            Add(diagnostics, "world.encounter.composition_budget_invalid",
                "Encounter group budgets must fit the effective difficulty and canonical spawn budgets.",
                $"{path}/groups");

        var selection = profile.Selection;
        if (string.IsNullOrWhiteSpace(selection.PartyAssessmentProviderId) ||
            !string.Equals(selection.PartyAssessmentProviderId, difficulty.PartyAssessmentProviderId,
                StringComparison.Ordinal) || string.IsNullOrWhiteSpace(selection.Strategy))
            Add(diagnostics, "world.encounter.selection_invalid",
                "Encounter selection requires one consistent data-driven party provider and strategy.",
                $"{path}/selection");

        var activation = profile.Activation;
        if (!double.IsFinite(activation.IntervalSeconds) || activation.IntervalSeconds < 0 ||
            activation.Mode == EncounterActivationMode.Timed && activation.IntervalSeconds <= 0 ||
            activation.Mode == EncounterActivationMode.Script && string.IsNullOrWhiteSpace(activation.ScriptId) ||
            activation.Mode != EncounterActivationMode.Script && activation.ScriptId is { Length: 0 })
            Add(diagnostics, "world.encounter.activation_invalid",
                "Encounter activation requires a valid mode, interval and script binding when scripted.",
                $"{path}/activation");

        if (profile.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.encounter.composition_override_invalid",
                "Encounter composition overrides require non-empty keys and values.", $"{path}/overrides");
        return diagnostics;
    }

    public static EncounterCompositionResolution Resolve(EncounterWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var path = $"module/worldFeatures/encounters/{profile.Id}/composition";
        if (profile.Composition is null)
        {
            var missing = profile.CompositionRequiredAtRuntime
                ? new[] { new DomainDiagnostic("world.encounter.composition_required",
                    "Encounter composition is required by this runtime profile.", path) }
                : [];
            return new(false, !profile.CompositionRequiredAtRuntime, null, missing);
        }
        var diagnostics = Validate(profile.Composition, profile.Foundation, path);
        return new(diagnostics.Count == 0, false, profile.Composition, diagnostics);
    }

    private static void Add(List<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));
}
