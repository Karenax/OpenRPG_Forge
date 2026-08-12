using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Validation;

namespace Forge.RpgKit.Application.Combat;

public sealed record CombatSimulationArchetype(BlueprintId BlueprintId, string Name, CombatProfile Combat);

public sealed record CombatMatchupReport(
    BlueprintId FirstArchetypeId,
    BlueprintId SecondArchetypeId,
    int Runs,
    int FirstWins,
    int SecondWins,
    int Draws,
    double FirstWinRate,
    double AverageDurationSeconds,
    double AverageFirstResourceUsed,
    double AverageSecondResourceUsed,
    double AveragePathingSeconds,
    IReadOnlyDictionary<float, int> FirstPhaseTriggers,
    IReadOnlyDictionary<float, int> SecondPhaseTriggers);

public sealed record CombatSimulationReport(
    Guid ProfileId,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<CombatMatchupReport> Matchups,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    string Fingerprint)
{
    public bool Succeeded => Diagnostics.Count == 0 && Matchups.Count > 0;
}

public static class CombatBalanceSimulationService
{
    public const string DisabledCode = "assistant.combat_simulation.disabled";
    public const string ArchetypeMissingCode = "assistant.combat_simulation.archetype_missing";
    public const string AbilityMissingCode = "assistant.combat_simulation.damage_ability_missing";

    public static CombatSimulationReport Run(CombatSimulationProfile profile,
        IReadOnlyList<CombatSimulationArchetype> availableArchetypes,
        IReadOnlyDictionary<string, float>? pathingDistancesMeters = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(availableArchetypes);
        var diagnostics = CombatSimulationProfileValidator.Validate(profile).ToList();
        if (!profile.Enabled)
            diagnostics.Add(new(DisabledCode, "Combat simulation is disabled.", "assistant/combatSimulation/enabled"));
        var ids = profile.ArchetypeBlueprintIds.Count == 0
            ? availableArchetypes.Select(item => item.BlueprintId).ToHashSet()
            : profile.ArchetypeBlueprintIds.ToHashSet();
        var selected = availableArchetypes.Where(item => ids.Contains(item.BlueprintId))
            .OrderBy(item => item.BlueprintId.Value).ToArray();
        foreach (var missing in ids.Where(id => selected.All(item => item.BlueprintId != id)))
            diagnostics.Add(new(ArchetypeMissingCode, $"Combat archetype '{missing}' is missing.",
                $"assistant/combatSimulation/archetypeBlueprintIds/{missing}"));
        foreach (var archetype in selected.Where(item => DamageAbilities(item.Combat).Count == 0))
            diagnostics.Add(new(AbilityMissingCode, $"Combat archetype '{archetype.Name}' has no enemy damage ability.",
                $"blueprints/{archetype.BlueprintId}/combat/abilities"));
        if (selected.Length < 2)
            diagnostics.Add(new(ArchetypeMissingCode, "At least two combat archetypes are required.",
                "assistant/combatSimulation/archetypeBlueprintIds"));
        if (diagnostics.Count > 0) return Report(profile, [], diagnostics);

        var matchups = new List<CombatMatchupReport>();
        for (var first = 0; first < selected.Length - 1; first++)
            for (var second = first + 1; second < selected.Length; second++)
            {
                var left = selected[first];
                var right = selected[second];
                var key = PairKey(left.BlueprintId, right.BlueprintId);
                var distance = pathingDistancesMeters?.GetValueOrDefault(key) ?? 0;
                if (!float.IsFinite(distance) || distance < 0)
                    throw new ArgumentOutOfRangeException(nameof(pathingDistancesMeters));
                matchups.Add(RunMatchup(profile, left, right, distance));
            }
        return Report(profile, matchups, diagnostics);
    }

    public static string PairKey(BlueprintId first, BlueprintId second) =>
        string.CompareOrdinal(first.ToString(), second.ToString()) <= 0
            ? $"{first}|{second}" : $"{second}|{first}";

    private static CombatMatchupReport RunMatchup(CombatSimulationProfile profile,
        CombatSimulationArchetype first, CombatSimulationArchetype second, float distance)
    {
        var firstWins = 0;
        var secondWins = 0;
        var draws = 0;
        var duration = 0d;
        var firstResource = 0d;
        var secondResource = 0d;
        var pathing = profile.IncludePathing ? distance / profile.MovementMetersPerSecond : 0;
        var firstPhases = profile.PhaseHealthThresholds.ToDictionary(value => value, _ => 0);
        var secondPhases = profile.PhaseHealthThresholds.ToDictionary(value => value, _ => 0);
        for (var run = 0; run < profile.RunsPerMatchup; run++)
        {
            var result = RunSingle(profile, first, second, run, pathing);
            firstWins += result.Winner == 1 ? 1 : 0;
            secondWins += result.Winner == 2 ? 1 : 0;
            draws += result.Winner == 0 ? 1 : 0;
            duration += result.Duration;
            firstResource += result.FirstResourceUsed;
            secondResource += result.SecondResourceUsed;
            foreach (var threshold in result.FirstTriggered) firstPhases[threshold]++;
            foreach (var threshold in result.SecondTriggered) secondPhases[threshold]++;
        }
        return new(first.BlueprintId, second.BlueprintId, profile.RunsPerMatchup,
            firstWins, secondWins, draws, firstWins / (double)profile.RunsPerMatchup,
            duration / profile.RunsPerMatchup, firstResource / profile.RunsPerMatchup,
            secondResource / profile.RunsPerMatchup, pathing, firstPhases, secondPhases);
    }

    private static SingleRun RunSingle(CombatSimulationProfile profile, CombatSimulationArchetype first,
        CombatSimulationArchetype second, int run, double pathing)
    {
        var firstHealth = first.Combat.Stats.MaximumHealth;
        var secondHealth = second.Combat.Stats.MaximumHealth;
        var firstResource = first.Combat.Stats.MaximumResource;
        var secondResource = second.Combat.Stats.MaximumResource;
        var firstInitialResource = firstResource;
        var secondInitialResource = secondResource;
        var firstCooldowns = new Dictionary<AbilityId, int>();
        var secondCooldowns = new Dictionary<AbilityId, int>();
        var firstTriggered = new HashSet<float>();
        var secondTriggered = new HashSet<float>();
        var firstActsFirst = Roll(profile.FirstSeed, run, first.BlueprintId, second.BlueprintId, 0) >= .5;
        var round = 0;
        for (; round < profile.MaximumRounds && firstHealth > 0 && secondHealth > 0; round++)
        {
            if (firstActsFirst)
            {
                Attack(first, second, run, round, ref firstResource, ref secondHealth, firstCooldowns, profile, secondTriggered);
                if (secondHealth > 0) Attack(second, first, run, round, ref secondResource, ref firstHealth, secondCooldowns, profile, firstTriggered);
            }
            else
            {
                Attack(second, first, run, round, ref secondResource, ref firstHealth, secondCooldowns, profile, firstTriggered);
                if (firstHealth > 0) Attack(first, second, run, round, ref firstResource, ref secondHealth, firstCooldowns, profile, secondTriggered);
            }
        }
        var winner = firstHealth == secondHealth ? 0 : firstHealth > secondHealth ? 1 : 2;
        return new(winner, pathing + round * profile.RoundSeconds,
            firstInitialResource - firstResource, secondInitialResource - secondResource,
            firstTriggered, secondTriggered);
    }

    private static void Attack(CombatSimulationArchetype attacker, CombatSimulationArchetype defender,
        int run, int round, ref int resource, ref int defenderHealth, IDictionary<AbilityId, int> cooldowns,
        CombatSimulationProfile profile, ISet<float> defenderPhases)
    {
        var availableResource = resource;
        var abilities = DamageAbilities(attacker.Combat).Where(item => item.ResourceCost <= availableResource &&
            (!cooldowns.TryGetValue(item.Id, out var readyRound) || round >= readyRound)).ToArray();
        if (abilities.Length == 0) return;
        var index = (int)Math.Floor(Roll(profile.FirstSeed, run, attacker.BlueprintId, defender.BlueprintId, round + 1) * abilities.Length);
        var ability = abilities[Math.Min(index, abilities.Length - 1)];
        var magnitude = ability.Effects.Where(item => item.Kind == AbilityEffectKind.Damage).Sum(item => item.Magnitude);
        var variance = .9 + (.2 * Roll(profile.FirstSeed, run, attacker.BlueprintId, defender.BlueprintId, round + 4096));
        var damage = Math.Max(1, (int)Math.Round((attacker.Combat.Stats.Offense + magnitude - defender.Combat.Stats.Defense) * variance));
        var before = defenderHealth;
        defenderHealth = Math.Max(0, defenderHealth - damage);
        resource -= ability.ResourceCost;
        cooldowns[ability.Id] = round + Math.Max(1, (int)Math.Ceiling(ability.CooldownSeconds / profile.RoundSeconds));
        var remainingHealth = defenderHealth;
        foreach (var threshold in profile.PhaseHealthThresholds.Where(threshold =>
                     before / (float)defender.Combat.Stats.MaximumHealth > threshold &&
                     remainingHealth / (float)defender.Combat.Stats.MaximumHealth <= threshold))
            defenderPhases.Add(threshold);
    }

    private static IReadOnlyList<AbilityDefinition> DamageAbilities(CombatProfile profile) => profile.Abilities
        .Where(item => item.Targeting is AbilityTargeting.SingleEnemy or AbilityTargeting.PointArea &&
                       item.Effects.Any(effect => effect.Kind == AbilityEffectKind.Damage))
        .OrderBy(item => item.Id.Value).ToArray();

    private static double Roll(int seed, int run, BlueprintId first, BlueprintId second, int ordinal)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{run}|{first}|{second}|{ordinal}"));
        return BitConverter.ToUInt32(bytes) / (double)uint.MaxValue;
    }

    private static CombatSimulationReport Report(CombatSimulationProfile profile,
        IReadOnlyList<CombatMatchupReport> matchups, IReadOnlyList<DomainDiagnostic> diagnostics)
    {
        var canonical = string.Join('|', matchups.Select(item =>
            $"{item.FirstArchetypeId}:{item.SecondArchetypeId}:{item.FirstWins}:{item.SecondWins}:{item.Draws}:{item.AverageDurationSeconds:R}"));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{profile.Id:D}|{profile.FirstSeed}|{profile.RunsPerMatchup}|{canonical}")));
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, matchups, diagnostics, fingerprint);
    }

    private sealed record SingleRun(int Winner, double Duration, int FirstResourceUsed,
        int SecondResourceUsed, IReadOnlySet<float> FirstTriggered, IReadOnlySet<float> SecondTriggered);
}
