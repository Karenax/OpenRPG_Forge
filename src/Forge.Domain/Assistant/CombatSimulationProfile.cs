using Forge.Domain.Blueprints;
using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public sealed record CombatSimulationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    int RunsPerMatchup,
    int FirstSeed,
    int MaximumRounds,
    float RoundSeconds,
    bool IncludePathing,
    float MovementMetersPerSecond,
    IReadOnlyList<float> PhaseHealthThresholds,
    IReadOnlyList<BlueprintId> ArchetypeBlueprintIds)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("a1430000-0000-4000-8000-000000000001");

    public static CombatSimulationProfile CreateDefault() => new(
        DefaultId, CurrentSchemaVersion, "openrpg-forge:r143", false, 32, 2026, 200, .5f,
        true, 5, [.75f, .5f, .25f], []);
}

public static class CombatSimulationProfileValidator
{
    public const string IdentityInvalidCode = "assistant.combat_simulation.identity_invalid";
    public const string BudgetInvalidCode = "assistant.combat_simulation.budget_invalid";
    public const string ArchetypesInvalidCode = "assistant.combat_simulation.archetypes_invalid";
    public const string PhasesInvalidCode = "assistant.combat_simulation.phases_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(CombatSimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != CombatSimulationProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Combat simulation requires UUID, schema and provenance.",
                "assistant/combatSimulation"));
        if (profile.RunsPerMatchup is < 1 or > 256 || profile.MaximumRounds is < 1 or > 5000 ||
            !float.IsFinite(profile.RoundSeconds) || profile.RoundSeconds is <= 0 or > 60 ||
            !float.IsFinite(profile.MovementMetersPerSecond) || profile.MovementMetersPerSecond is <= 0 or > 500)
            diagnostics.Add(new(BudgetInvalidCode, "Combat simulation run, round and movement budgets are invalid.",
                "assistant/combatSimulation/budgets"));
        if (profile.ArchetypeBlueprintIds.Distinct().Count() != profile.ArchetypeBlueprintIds.Count ||
            profile.ArchetypeBlueprintIds.Count > 32)
            diagnostics.Add(new(ArchetypesInvalidCode, "Combat simulation archetypes must be unique and bounded.",
                "assistant/combatSimulation/archetypeBlueprintIds"));
        if (profile.PhaseHealthThresholds.Count is < 1 or > 16 ||
            profile.PhaseHealthThresholds.Any(value => !float.IsFinite(value) || value is <= 0 or >= 1) ||
            profile.PhaseHealthThresholds.Distinct().Count() != profile.PhaseHealthThresholds.Count)
            diagnostics.Add(new(PhasesInvalidCode, "Combat phase thresholds must be unique finite ratios.",
                "assistant/combatSimulation/phaseHealthThresholds"));
        return diagnostics;
    }
}
