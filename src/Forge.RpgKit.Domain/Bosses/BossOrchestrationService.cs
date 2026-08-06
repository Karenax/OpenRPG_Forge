using System.Text.Json;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;

namespace Forge.RpgKit.Domain.Bosses;

public sealed record BossPhasePlan(
    string Id,
    int Order,
    double HealthThreshold,
    IReadOnlyList<string> AbilityIds,
    string TransitionScript,
    IReadOnlyList<string> SummonBlueprintSlugs);

public sealed record BossOrchestrationDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    BlueprintDefinition Blueprint,
    CreatureDefinition Creature,
    EncounterDefinition Encounter,
    IReadOnlyList<BossPhasePlan> Phases,
    DialogueDefinition IntroductionDialogue,
    IReadOnlyList<LootDrop> Rewards,
    string DeathScript);

public sealed record BossDiagnostic(string Code, string Path, string Message);

public sealed record BossBalanceReport(
    int Runs,
    double MeanDurationSeconds,
    double MeanEncounterCost,
    double MeanRewardValue,
    double CostVariance,
    bool Passed,
    string Summary);

public static class BossOrchestrationService
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlyList<BossDiagnostic> Validate(
        BossOrchestrationDefinition boss,
        IReadOnlySet<string> availableScripts,
        IReadOnlySet<string> availableBlueprintSlugs)
    {
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(availableScripts);
        ArgumentNullException.ThrowIfNull(availableBlueprintSlugs);
        var root = $"bosses/{boss.Id:D}";
        var diagnostics = new List<BossDiagnostic>();
        if (boss.Id == Guid.Empty || boss.SchemaVersion != CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(boss.Provenance) || string.IsNullOrWhiteSpace(boss.Slug) ||
            string.IsNullOrWhiteSpace(boss.DisplayName))
            diagnostics.Add(new("RPG_BOSS_IDENTITY_INVALID", root, "Boss identity, schema and provenance are required."));
        if (boss.Blueprint.Kind != BlueprintKind.Creature || boss.Blueprint.Combat is null ||
            boss.Blueprint.Id != boss.Creature.BlueprintId || boss.Creature.Authoring.Role != EncounterRole.Boss)
            diagnostics.Add(new("RPG_BOSS_CREATURE_INVALID", $"{root}/creature", "Boss blueprint and creature role must resolve to the same canonical creature."));
        if (boss.Encounter.Candidates.Count != 1 || boss.Encounter.Candidates[0].CreatureId != boss.Creature.Id ||
            boss.Encounter.Candidates[0].Role != EncounterRole.Boss || boss.Encounter.RewardIds.Count == 0)
            diagnostics.Add(new("RPG_BOSS_ENCOUNTER_INVALID", $"{root}/encounter", "Boss encounter must contain exactly the boss candidate and at least one reward."));

        var ordered = boss.Phases.OrderBy(item => item.Order).ToArray();
        var abilityIds = boss.Blueprint.Combat?.Abilities.Select(item => item.Id.ToString())
            .ToHashSet(StringComparer.Ordinal) ?? [];
        if (ordered.Length is < 2 or > 6 ||
            ordered.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            ordered.Select(item => item.Order).Distinct().Count() != ordered.Length ||
            ordered.Any(item => item.HealthThreshold is <= 0 or > 1) ||
            ordered.Zip(ordered.Skip(1), (left, right) => left.HealthThreshold > right.HealthThreshold).Any(valid => !valid))
            diagnostics.Add(new("RPG_BOSS_PHASE_INVALID", $"{root}/phases", "Boss phases require unique order and strictly descending health thresholds."));
        foreach (var phase in ordered)
        {
            if (phase.AbilityIds.Count == 0 || phase.AbilityIds.Any(id => !abilityIds.Contains(id)))
                diagnostics.Add(new("RPG_BOSS_PHASE_ABILITY_MISSING", $"{root}/phases/{phase.Id}/abilityIds", "Every phase ability must resolve to the canonical combat profile."));
            if (string.IsNullOrWhiteSpace(phase.TransitionScript) || !availableScripts.Contains(phase.TransitionScript))
                diagnostics.Add(new("RPG_BOSS_PHASE_SCRIPT_MISSING", $"{root}/phases/{phase.Id}/transitionScript", $"Phase script '{phase.TransitionScript}' is unavailable."));
            foreach (var summon in phase.SummonBlueprintSlugs.Where(item => !availableBlueprintSlugs.Contains(item)))
                diagnostics.Add(new("RPG_BOSS_SUMMON_MISSING", $"{root}/phases/{phase.Id}/summons/{summon}", $"Summon blueprint '{summon}' is unavailable."));
        }
        if (string.IsNullOrWhiteSpace(boss.DeathScript) || !availableScripts.Contains(boss.DeathScript))
            diagnostics.Add(new("RPG_BOSS_DEATH_SCRIPT_MISSING", $"{root}/deathScript", $"Death script '{boss.DeathScript}' is unavailable."));
        foreach (var diagnostic in EncounterWaveService.Validate(boss.Encounter, ToEncounterPhases(boss)))
            diagnostics.Add(new(diagnostic.Code, diagnostic.Path, diagnostic.Message));
        foreach (var diagnostic in DialogueValidator.Validate(boss.IntroductionDialogue))
            diagnostics.Add(new(diagnostic.Code, diagnostic.Path ?? $"{root}/dialogue", diagnostic.Message));
        if (boss.Rewards.Count == 0)
            diagnostics.Add(new("RPG_BOSS_REWARD_REQUIRED", $"{root}/rewards", "At least one canonical loot reward is required."));
        return diagnostics;
    }

    public static BossBalanceReport Simulate(BossOrchestrationDefinition boss, int runs, int seed)
    {
        ArgumentNullException.ThrowIfNull(boss);
        var candidate = boss.Encounter.Candidates.Single();
        var group = new EncounterGroup("boss", new CountRange(1, 1), candidate.Cost, [candidate.CreatureId]);
        var anchor = new SpawnAnchor("boss-anchor", Forge.Domain.Spatial.TransformValue.Identity, 1, true,
            SpawnVisibilityPolicy.Effect, true, 1);
        var policy = new EncounterRuntimePolicy(EncounterActivationPolicy.Once, TimeSpan.Zero, [group], [anchor],
            SpawnDistribution.Sequential,
            new ActivatorQuery(true, true, true, true, true, NetworkAuthority.Server, new HashSet<string>()));
        var party = new PartyAssessment(boss.Encounter.DifficultyProfile.ExpectedPartySize,
            boss.Encounter.DifficultyProfile.ExpectedPartyPower, new HashSet<string>(), "boss-balance-v1");
        var stats = EncounterWaveService.Simulate(boss.Encounter, policy, party, runs, seed,
            composition => 90 + (composition.TotalCost * 6) + (boss.Phases.Count * 18),
            _ => boss.Rewards.Sum(item => item.Quantity * item.Chance * 100));
        var passed = stats.MeanCount == 1 && stats.MeanDurationSeconds is >= 90 and <= 480 &&
                     stats.MeanRewardValue > 0 && stats.CostVariance <= 0.000001;
        return new(stats.Runs, stats.MeanDurationSeconds, stats.MeanCost, stats.MeanRewardValue,
            stats.CostVariance, passed,
            $"runs:{stats.Runs}|bosses:{stats.MeanCount:0.##}|duration:{stats.MeanDurationSeconds:0.##}|cost:{stats.MeanCost:0.##}|reward:{stats.MeanRewardValue:0.##}|variance:{stats.CostVariance:0.######}");
    }

    public static IReadOnlyList<EncounterPhase> ToEncounterPhases(BossOrchestrationDefinition boss) =>
        boss.Phases.OrderBy(item => item.Order).Select(phase => new EncounterPhase(
            phase.Id,
            phase.Order,
            [new EncounterWave($"{phase.Id}:wave", 1,
                [new EncounterGroup("boss", new CountRange(1, 1), boss.Encounter.Candidates.Single().Cost,
                    [boss.Creature.Id])], TimeSpan.Zero,
                phase == boss.Phases.OrderBy(item => item.Order).Last()
                    ? WaveCompletionPolicy.AllDefeated
                    : WaveCompletionPolicy.Script,
                phase == boss.Phases.OrderBy(item => item.Order).Last() ? null : phase.TransitionScript)],
            new HashSet<string> { $"health<={phase.HealthThreshold:0.##}" },
            phase.SummonBlueprintSlugs.Select(item => $"summon:{item}").ToArray())).ToArray();

    public static string ToCanonicalJson(BossOrchestrationDefinition boss, BossBalanceReport balance)
    {
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(balance);
        return JsonSerializer.Serialize(new
        {
            id = boss.Id,
            schemaVersion = boss.SchemaVersion,
            boss.Slug,
            boss.DisplayName,
            blueprintId = boss.Blueprint.Id.Value,
            creatureId = boss.Creature.Id,
            encounterId = boss.Encounter.Id,
            dialogueId = boss.IntroductionDialogue.Id.Value,
            abilities = boss.Blueprint.Combat!.Abilities.Select(item => new
            {
                id = item.Id.Value,
                item.Slug,
                item.DisplayName,
                item.ResourceCost,
                item.CooldownSeconds,
                targeting = item.Targeting.ToString(),
                effects = item.Effects.Select(effect => new { kind = effect.Kind.ToString(), effect.Magnitude }),
                vfx = item.VisualCue.CueKey,
            }),
            phases = boss.Phases.OrderBy(item => item.Order).Select(item => new
            {
                item.Id,
                item.Order,
                item.HealthThreshold,
                item.AbilityIds,
                item.TransitionScript,
                item.SummonBlueprintSlugs,
            }),
            rewards = boss.Rewards.Select(item => new { item.ItemSlug, item.Quantity, item.Chance }),
            boss.DeathScript,
            balance = new
            {
                balance.Runs,
                balance.MeanDurationSeconds,
                balance.MeanEncounterCost,
                balance.MeanRewardValue,
                balance.CostVariance,
                balance.Passed
            },
            boss.Provenance,
        });
    }
}
