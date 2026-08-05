using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Quality;

public sealed record EncounterPartyProfile(string Id, int Size, double Power);

public sealed record EncounterSimulationRun(
    string AreaSlug,
    string PartyProfileId,
    int Seed,
    int SpawnedCount,
    double Threat,
    bool NavigationPassed,
    bool CleanupPassed,
    IReadOnlyList<EntityId> SelectedEntities);

public sealed record EncounterAreaQualityReport(
    AreaId AreaId,
    string AreaSlug,
    int CreatureCount,
    int ReachableCreatures,
    IReadOnlyList<EncounterSimulationRun> Runs,
    bool Passed);

public sealed record EncounterQualityReport(
    bool Passed,
    int TotalRuns,
    IReadOnlyList<EncounterAreaQualityReport> Areas);

public static class EncounterQualityService
{
    public static EncounterQualityReport Simulate(
        ProjectDefinition project,
        IReadOnlyList<EncounterPartyProfile>? partyProfiles = null,
        IReadOnlyList<int>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var profiles = partyProfiles ??
        [
            new EncounterPartyProfile("solo", 1, 1),
            new EncounterPartyProfile("party", 4, 1),
            new EncounterPartyProfile("veteran", 4, 1.75),
        ];
        var selectedSeeds = seeds ?? [17, 42, 2026];
        if (profiles.Count == 0 || profiles.Any(profile => string.IsNullOrWhiteSpace(profile.Id) || profile.Size < 1 ||
            !double.IsFinite(profile.Power) || profile.Power <= 0) || selectedSeeds.Count == 0)
            throw new ArgumentException("Encounter test profiles and seeds are required.");

        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var areaReports = new List<EncounterAreaQualityReport>();
        foreach (var area in project.Areas.OrderBy(area => area.Slug, StringComparer.Ordinal))
        {
            var creatures = area.Entities.Where(entity => blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                blueprint.Kind == BlueprintKind.Creature).OrderBy(entity => entity.Id.ToString(), StringComparer.Ordinal).ToArray();
            if (creatures.Length == 0) continue;
            var agent = area.Navigation.AgentProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, "Moyen", StringComparison.OrdinalIgnoreCase)) ?? area.Navigation.AgentProfiles[0];
            var surface = NavigationSurfaceBuilder.Build(project, area, agent, NavigationDoorState.AllOpen);
            var spawnCells = area.Entities.Where(entity => blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                    blueprint.Kind == BlueprintKind.PlayerSpawn).Select(entity => surface.CellAt(entity.Transform.Position)).ToArray();
            if (spawnCells.Length == 0 && surface.WalkableCells.Count > 0) spawnCells = [surface.WalkableCells[0]];
            var reachable = NavigationPathfinder.ReachableFrom(surface, spawnCells);
            var reachableCreatures = creatures.Where(entity => reachable.Contains(surface.CellAt(entity.Transform.Position))).ToArray();
            var runs = new List<EncounterSimulationRun>();
            foreach (var profile in profiles.OrderBy(profile => profile.Id, StringComparer.Ordinal))
                foreach (var seed in selectedSeeds.Order())
                {
                    var random = new Random(StableSeed(area.Id, profile.Id, seed));
                    var ordered = creatures.OrderBy(_ => random.Next()).ThenBy(entity => entity.Id.ToString(), StringComparer.Ordinal).ToArray();
                    var budget = Math.Max(1, profile.Size * profile.Power * 10);
                    var selection = new List<EntityInstance>();
                    var threat = 0d;
                    foreach (var entity in ordered)
                    {
                        var value = Threat(blueprints[entity.BlueprintId]);
                        if (selection.Count > 0 && threat + value > budget) continue;
                        selection.Add(entity);
                        threat += value;
                    }
                    if (selection.Count == 0) { selection.Add(ordered[0]); threat = Threat(blueprints[ordered[0].BlueprintId]); }
                    var navigationPassed = selection.All(entity => reachable.Contains(surface.CellAt(entity.Transform.Position)));
                    var actors = selection.Select(entity => $"test:{area.Id}:{profile.Id}:{seed}:{entity.Id}").ToHashSet(StringComparer.Ordinal);
                    var unique = actors.Count == selection.Count;
                    actors.Clear();
                    runs.Add(new(area.Slug, profile.Id, seed, selection.Count, threat, navigationPassed,
                        unique && actors.Count == 0, selection.Select(entity => entity.Id).ToArray()));
                }
            areaReports.Add(new(area.Id, area.Slug, creatures.Length, reachableCreatures.Length, runs,
                reachableCreatures.Length == creatures.Length && runs.All(run => run.NavigationPassed && run.CleanupPassed)));
        }
        return new EncounterQualityReport(areaReports.All(area => area.Passed), areaReports.Sum(area => area.Runs.Count), areaReports);
    }

    private static double Threat(BlueprintDefinition blueprint)
    {
        var stats = blueprint.Combat?.Stats;
        return stats is null ? 1 : Math.Max(1, (stats.MaximumHealth + stats.Offense * 2 + stats.Defense * 2) / 5d);
    }

    private static int StableSeed(AreaId areaId, string profileId, int seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg:encounter-test:{areaId}:{profileId}:{seed}"));
        return BitConverter.ToInt32(hash, 0);
    }
}
