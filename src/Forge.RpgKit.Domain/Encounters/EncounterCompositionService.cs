using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Encounters;

public enum EncounterActivationPolicy { Once, Repeat, Timed, Wave, Persistent, PopulationMaintained, Script }
public enum SpawnDistribution { Sequential, Random, Weighted, Even }
public enum SpawnVisibilityPolicy { Hidden, BehindCover, Portal, Effect, InViewAllowed }
public enum NetworkAuthority { Local, Host, Server }

public sealed record CountRange(int Minimum, int Maximum);
public sealed record EncounterGroup(string Id, CountRange Count, double Budget, IReadOnlyList<Guid> CandidateIds);
public sealed record PartyAssessment(int PlayerCount, double Power, IReadOnlySet<string> Tags, string ProviderId);
public sealed record SpawnAnchor(string Id, TransformValue Transform, int Capacity, bool Navigable, SpawnVisibilityPolicy Visibility, bool IsFallback, double Weight);
public sealed record Activator(string Id, bool IsPlayer, bool IsSummon, bool IsCompanion, bool IsGameMaster, bool IsSpectator, NetworkAuthority Authority, IReadOnlySet<string> Tags);
public sealed record ActivatorQuery(bool PlayersOnly, bool IgnoreSummons, bool IgnoreCompanions, bool IgnoreGameMasters, bool IgnoreSpectators, NetworkAuthority RequiredAuthority, IReadOnlySet<string> RequiredTags);
public sealed record EncounterRuntimePolicy(EncounterActivationPolicy Activation, TimeSpan Interval, IReadOnlyList<EncounterGroup> Groups, IReadOnlyList<SpawnAnchor> Anchors, SpawnDistribution Distribution, ActivatorQuery Activators);
public sealed record EncounterComposition(IReadOnlyList<SpawnCandidate> Candidates, int TotalCount, double TotalCost, IReadOnlyList<string> AnchorIds, string Summary);

public static class EncounterCompositionService
{
    public static EncounterComposition Preview(EncounterDefinition encounter, EncounterRuntimePolicy policy, PartyAssessment party, int seed)
    {
        if (string.IsNullOrWhiteSpace(party.ProviderId)) throw new ArgumentException("Party assessment provider is required.", nameof(party));
        var random = new Random(seed); var selected = new List<SpawnCandidate>(); var anchors = new List<string>();
        foreach (var group in policy.Groups)
        {
            var candidates = encounter.Candidates.Where(item => group.CandidateIds.Count == 0 || group.CandidateIds.Contains(item.CreatureId)).OrderBy(item => item.BlueprintId, StringComparer.Ordinal).ToArray();
            var partyScale = Math.Max(0.5, party.Power / Math.Max(1, encounter.DifficultyProfile.ExpectedPartyPower));
            var budget = Math.Min(group.Budget, encounter.DifficultyProfile.EffectiveBudget * partyScale);
            var target = Math.Clamp((int)Math.Round(group.Count.Minimum + ((group.Count.Maximum - group.Count.Minimum) * Math.Min(1, partyScale))), group.Count.Minimum, group.Count.Maximum);
            while (selected.Count < target && candidates.Length > 0)
            {
                var affordable = candidates.Where(item => item.Cost <= budget && selected.Count(existing => existing.CreatureId == item.CreatureId) < item.MaximumAlive).ToArray();
                if (affordable.Length == 0) break;
                var candidate = affordable[random.Next(affordable.Length)]; selected.Add(candidate); budget -= candidate.Cost;
            }
        }
        var usable = policy.Anchors.Where(item => item.Navigable && item.Capacity > 0).OrderByDescending(item => item.Weight).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        for (var i = 0; i < selected.Count; i++) if (usable.Length > 0) anchors.Add(usable[i % usable.Length].Id);
        return new(selected, selected.Count, selected.Sum(item => item.Cost), anchors, $"players:{party.PlayerCount}|power:{party.Power:0.##}|count:{selected.Count}|cost:{selected.Sum(item => item.Cost):0.##}|seed:{seed}");
    }

    public static bool CanActivate(Activator activator, ActivatorQuery query) =>
        (!query.PlayersOnly || activator.IsPlayer) && (!query.IgnoreSummons || !activator.IsSummon) &&
        (!query.IgnoreCompanions || !activator.IsCompanion) && (!query.IgnoreGameMasters || !activator.IsGameMaster) &&
        (!query.IgnoreSpectators || !activator.IsSpectator) && activator.Authority == query.RequiredAuthority &&
        query.RequiredTags.All(activator.Tags.Contains);

    public static IReadOnlyList<EncounterDiagnostic> Validate(EncounterDefinition encounter, EncounterRuntimePolicy policy)
    {
        var root = $"encounters/{encounter.Id}/runtimePolicy"; var diagnostics = new List<EncounterDiagnostic>();
        if (policy.Groups.Count == 0 || policy.Groups.Any(group => group.Count.Minimum < 0 || group.Count.Maximum < group.Count.Minimum || group.Budget <= 0 || group.Count.Maximum > encounter.NetworkActorCap)) diagnostics.Add(new("RPG_ENCOUNTER_COUNT_INVALID", $"{root}/groups", "Group counts, budgets and server cap must be coherent."));
        if (policy.Anchors.Count == 0 || policy.Anchors.All(anchor => !anchor.Navigable) || policy.Anchors.Any(anchor => anchor.Capacity <= 0 || anchor.Weight < 0)) diagnostics.Add(new("RPG_ENCOUNTER_SPAWN_ANCHOR_INVALID", $"{root}/anchors", "A navigable anchor with positive capacity is required."));
        if (policy.Activation == EncounterActivationPolicy.Timed && policy.Interval <= TimeSpan.Zero) diagnostics.Add(new("RPG_ENCOUNTER_INTERVAL_INVALID", $"{root}/interval", "Timed activation requires a positive interval."));
        return diagnostics;
    }
}
