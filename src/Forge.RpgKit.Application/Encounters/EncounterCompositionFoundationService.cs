using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;
using Forge.RpgKit.Domain.Triggers;
using RpgSpawnCandidate = Forge.RpgKit.Domain.Encounters.SpawnCandidate;
using RpgTransform = Forge.Domain.Spatial.TransformValue;
using RpgVector3 = Forge.RpgKit.Domain.Placeables.Vector3Value;

namespace Forge.RpgKit.Application.Encounters;

public sealed record EncounterCompositionPreview(
    EncounterDefinition Definition,
    EncounterRuntimePolicy RuntimePolicy,
    EncounterComposition Composition,
    IReadOnlyList<EncounterDiagnostic> Diagnostics);

public sealed record EncounterScenarioRuntimePreview(
    EncounterDefinition Definition,
    EncounterRuntimePolicy RuntimePolicy,
    IReadOnlyList<EncounterPhase> Phases,
    bool ActivatorAccepted,
    EncounterEntered Entered,
    EncounterExited Exited,
    EncounterExhausted Exhausted,
    EncounterStatistics Statistics,
    IReadOnlyList<EncounterDiagnostic> Diagnostics);

public sealed record EncounterStatisticalPreview(
    EncounterStatisticsProfile Profile,
    EncounterStatistics Statistics,
    string Summary);

public static class EncounterCompositionFoundationService
{
    public static EncounterCompositionResolution Resolve(EncounterWorldProfile profile) =>
        EncounterCompositionProfileValidator.Resolve(profile);

    public static EncounterScenarioResolution ResolveScenario(EncounterWorldProfile profile) =>
        EncounterScenarioProfileValidator.Resolve(profile);

    public static EncounterStatisticsResolution ResolveStatistics(EncounterWorldProfile profile) =>
        EncounterStatisticsProfileValidator.Resolve(profile);

    public static EncounterDefinition CreateDefinition(EncounterWorldProfile profile)
    {
        var composition = Required(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException("WORLD_ENCOUNTER_FOUNDATION_MISSING");
        var volume = foundation.Volume;
        var shape = new TriggerShape(Map(volume.Shape), volume.Points.Select(Map).ToArray(), [],
            ShapeSize(volume), volume.Height, .25);
        var filter = new TriggerFilter(new HashSet<string> { "creature" },
            volume.RequiredEntityTags.ToHashSet(StringComparer.Ordinal), new HashSet<string>(),
            new HashSet<string> { "characters" });
        var candidates = foundation.Candidates.Select(item => new RpgSpawnCandidate(item.BlueprintId.Value,
            item.BlueprintId.ToString(), Map(item.Role), item.Weight, item.Cost,
            item.Conditions.ToHashSet(StringComparer.Ordinal), item.Variants, item.MaximumAlive)).ToArray();
        var difficulty = composition.Difficulty;
        return new(profile.Id, composition.SchemaVersion, composition.Provenance, composition.Slug,
            composition.DisplayName, composition.Category, composition.Theme, profile.AreaId.ToString(),
            new(shape, filter, volume.RequireLineOfSight, volume.ThreatRadius, volume.ActivationScriptId),
            candidates,
            new(difficulty.RulesetId, (EncounterDifficulty)difficulty.Band, difficulty.ExpectedPartySize,
                difficulty.ExpectedPartyPower, difficulty.BaseBudget, difficulty.PartySizeMultiplier,
                difficulty.DifficultyMultiplier), composition.Groups.Select(item => item.Slug).ToArray(),
            foundation.SpawnAnchorIds, foundation.RewardIds, foundation.NetworkActorCap);
    }

    public static EncounterRuntimePolicy CreateRuntimePolicy(EncounterWorldProfile profile)
    {
        var composition = Required(profile);
        var foundation = profile.Foundation ?? throw new InvalidOperationException("WORLD_ENCOUNTER_FOUNDATION_MISSING");
        var candidateIds = foundation.Candidates.ToDictionary(item => item.Id, item => item.BlueprintId.Value);
        var groups = composition.Groups.Select(item => new EncounterGroup(item.Slug,
            new(item.Minimum, item.Maximum), item.Budget,
            item.CandidateIds.Select(id => candidateIds[id]).ToArray())).ToArray();
        var scenario = OptionalScenario(profile);
        var anchors = scenario is null
            ? foundation.SpawnAnchorIds.Select((item, index) => new SpawnAnchor(item,
                RpgTransform.Identity, foundation.NetworkActorCap, true, Map(profile.Visibility.Mode), index == 0,
                index == 0 ? 2 : 1)).ToArray()
            : scenario.SpawnAnchors.Select(item => new SpawnAnchor(item.AnchorId, item.Transform, item.Capacity,
                item.Navigable, Map(item.Visibility), item.IsFallback, item.Weight)).ToArray();
        var activation = composition.Activation;
        return new((EncounterActivationPolicy)activation.Mode, TimeSpan.FromSeconds(activation.IntervalSeconds),
            groups, anchors, scenario is null ? (SpawnDistribution)activation.Distribution : Map(scenario.Distribution),
            scenario is null
                ? new(true, true, true, true, true, NetworkAuthority.Server, new HashSet<string>())
                : new(scenario.Activator.PlayersOnly, scenario.Activator.IgnoreSummons,
                    scenario.Activator.IgnoreCompanions, scenario.Activator.IgnoreGameMasters,
                    scenario.Activator.IgnoreSpectators, Map(scenario.Activator.RequiredAuthority),
                    scenario.Activator.RequiredTags.ToHashSet(StringComparer.Ordinal)));
    }

    public static IReadOnlyList<EncounterPhase> CreatePhases(EncounterWorldProfile profile)
    {
        var scenario = RequiredScenario(profile);
        var runtimeGroups = CreateRuntimePolicy(profile).Groups;
        var canonicalGroups = Required(profile).Groups.Select((item, index) => (item.Id, Group: runtimeGroups[index]))
            .ToDictionary(item => item.Id, item => item.Group);
        return scenario.Phases.Select(phase => new EncounterPhase(phase.Id.ToString("D"), phase.Order,
            phase.Waves.Select(wave => new EncounterWave(wave.Id.ToString("D"), wave.Order,
                wave.GroupIds.Select(id => canonicalGroups[id]).ToArray(), TimeSpan.FromSeconds(wave.DelaySeconds),
                Map(wave.Completion), wave.CompletionTargetId, wave.QuestId, wave.ObjectiveId, wave.VoiceCueId,
                wave.NextWaveId?.ToString("D"))).ToArray(),
            phase.EntryConditions.ToHashSet(StringComparer.Ordinal),
            phase.ReinforcementEncounterIds.Select(item => item.ToString("D")).ToArray())).ToArray();
    }

    public static EncounterScenarioRuntimePreview PreviewScenario(
        EncounterWorldProfile profile,
        Forge.RpgKit.Domain.Encounters.Activator activator,
        int playerCount,
        double partyPower,
        IReadOnlySet<string> partyTags,
        DateTimeOffset enteredAt,
        TimeSpan durationInside,
        int runs = 3)
    {
        var definition = CreateDefinition(profile);
        var policy = CreateRuntimePolicy(profile);
        var phases = CreatePhases(profile);
        var lifecycle = new EncounterLifecycle();
        var entered = lifecycle.Enter(definition, activator.Id, enteredAt);
        var exited = lifecycle.Exit(definition, activator.Id, enteredAt.Add(durationInside)) ??
                     throw new InvalidOperationException("RPG_ENCOUNTER_EXIT_WITHOUT_ENTER");
        var party = new PartyAssessment(playerCount, partyPower, partyTags,
            Required(profile).Selection.PartyAssessmentProviderId);
        var statistics = EncounterWaveService.Simulate(definition, policy, party, runs,
            Required(profile).Selection.DeterministicSeed, item => item.TotalCount * 5,
            item => item.TotalCost * .5);
        var exhausted = EncounterLifecycle.Exhaust(definition, exited.At, statistics.Results.Sum(item => item.SpawnedCount),
            "waves-completed", statistics.Results.Sum(item => item.SpawnedCount), 0,
            phases.Sum(item => item.Waves.Count), definition.RewardIds);
        return new(definition, policy, phases, EncounterCompositionService.CanActivate(activator, policy.Activators),
            entered, exited, exhausted, statistics,
            EncounterCompositionService.Validate(definition, policy)
                .Concat(EncounterWaveService.Validate(definition, phases)).ToArray());
    }

    public static EncounterStatisticalPreview PreviewStatistics(EncounterWorldProfile profile)
    {
        var settings = RequiredStatistics(profile);
        var composition = Required(profile);
        var definition = CreateDefinition(profile);
        var policy = CreateRuntimePolicy(profile);
        var party = new PartyAssessment(composition.Difficulty.ExpectedPartySize,
            composition.Difficulty.ExpectedPartyPower, new HashSet<string>(),
            composition.Selection.PartyAssessmentProviderId);
        var statistics = EncounterWaveService.Simulate(definition, policy, party, settings.Runs,
            settings.BaseSeed, item => item.TotalCount * settings.SecondsPerActor,
            item => item.TotalCost * settings.RewardPerCost, settings.Percentiles);
        if (!settings.IncludeRunDetails)
            statistics = statistics with { Results = [] };
        var summary = $"runs:{statistics.Runs}|seed:{settings.BaseSeed}|count:{statistics.MeanCount:0.##}|" +
                      $"cost:{statistics.MeanCost:0.##}|duration:{statistics.MeanDurationSeconds:0.##}|" +
                      $"reward:{statistics.MeanRewardValue:0.##}|variance:{statistics.CostVariance:0.####}";
        return new(settings, statistics, summary);
    }

    public static EncounterCompositionPreview Preview(EncounterWorldProfile profile, int playerCount,
        double partyPower, IReadOnlySet<string> partyTags)
    {
        var composition = Required(profile);
        var definition = CreateDefinition(profile);
        var policy = CreateRuntimePolicy(profile);
        var diagnostics = EncounterCompositionService.Validate(definition, policy);
        var preview = EncounterCompositionService.Preview(definition, policy,
            new(playerCount, partyPower, partyTags, composition.Selection.PartyAssessmentProviderId),
            composition.Selection.DeterministicSeed);
        return new(definition, policy, preview, diagnostics);
    }

    public static IReadOnlyList<EncounterDiagnostic> ValidateAuthoring(EncounterWorldProfile profile,
        EncounterValidationContext context) => EncounterAuthoringService.Validate(CreateDefinition(profile), context);

    private static EncounterCompositionProfile Required(EncounterWorldProfile profile)
    {
        var resolution = Resolve(profile);
        if (!resolution.Available || resolution.Profile is null)
            throw new InvalidOperationException(resolution.Diagnostics.FirstOrDefault()?.Code ??
                                                "WORLD_ENCOUNTER_COMPOSITION_MISSING");
        return resolution.Profile;
    }

    private static EncounterScenarioProfile? OptionalScenario(EncounterWorldProfile profile)
    {
        var resolution = ResolveScenario(profile);
        if (resolution.Available)
            return resolution.Profile;
        if (resolution.OptionalFallback)
            return null;
        throw new InvalidOperationException(resolution.Diagnostics.FirstOrDefault()?.Code ??
                                            "WORLD_ENCOUNTER_SCENARIO_INVALID");
    }

    private static EncounterScenarioProfile RequiredScenario(EncounterWorldProfile profile) =>
        OptionalScenario(profile) ?? throw new InvalidOperationException("WORLD_ENCOUNTER_SCENARIO_MISSING");

    private static EncounterStatisticsProfile RequiredStatistics(EncounterWorldProfile profile)
    {
        var resolution = ResolveStatistics(profile);
        if (!resolution.Available || resolution.Profile is null)
            throw new InvalidOperationException(resolution.Diagnostics.FirstOrDefault()?.Code ??
                                                "WORLD_ENCOUNTER_STATISTICS_MISSING");
        return resolution.Profile;
    }

    private static EncounterRole Map(EncounterWorldProfile.CandidateRole role) => role switch
    {
        EncounterWorldProfile.CandidateRole.Brute => EncounterRole.Defender,
        EncounterWorldProfile.CandidateRole.Skirmisher => EncounterRole.Skirmisher,
        EncounterWorldProfile.CandidateRole.Controller => EncounterRole.Controller,
        EncounterWorldProfile.CandidateRole.Support => EncounterRole.Support,
        EncounterWorldProfile.CandidateRole.Boss => EncounterRole.Boss,
        EncounterWorldProfile.CandidateRole.Ambient => EncounterRole.NonCombatant,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private static TriggerShapeKind Map(EncounterWorldProfile.VolumeShape shape) => shape switch
    {
        EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D => TriggerShapeKind.ExtrudedPolygon,
        EncounterWorldProfile.VolumeShape.Box3D => TriggerShapeKind.Box,
        EncounterWorldProfile.VolumeShape.Sphere3D => TriggerShapeKind.Sphere,
        EncounterWorldProfile.VolumeShape.Capsule3D => TriggerShapeKind.Capsule,
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static SpawnVisibilityPolicy Map(EncounterWorldProfile.VisibilityMode mode) => mode switch
    {
        EncounterWorldProfile.VisibilityMode.Hidden => SpawnVisibilityPolicy.Hidden,
        EncounterWorldProfile.VisibilityMode.BehindCover => SpawnVisibilityPolicy.BehindCover,
        EncounterWorldProfile.VisibilityMode.Portal => SpawnVisibilityPolicy.Portal,
        EncounterWorldProfile.VisibilityMode.Effect => SpawnVisibilityPolicy.Effect,
        EncounterWorldProfile.VisibilityMode.InViewAllowed => SpawnVisibilityPolicy.InViewAllowed,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static SpawnDistribution Map(EncounterSpawnDistribution distribution) => distribution switch
    {
        EncounterSpawnDistribution.Sequential => SpawnDistribution.Sequential,
        EncounterSpawnDistribution.Random => SpawnDistribution.Random,
        EncounterSpawnDistribution.Weighted => SpawnDistribution.Weighted,
        EncounterSpawnDistribution.Even => SpawnDistribution.Even,
        _ => throw new ArgumentOutOfRangeException(nameof(distribution)),
    };

    private static NetworkAuthority Map(EncounterNetworkAuthority authority) => authority switch
    {
        EncounterNetworkAuthority.Local => NetworkAuthority.Local,
        EncounterNetworkAuthority.Host => NetworkAuthority.Host,
        EncounterNetworkAuthority.Server => NetworkAuthority.Server,
        _ => throw new ArgumentOutOfRangeException(nameof(authority)),
    };

    private static WaveCompletionPolicy Map(EncounterWaveCompletionPolicy completion) => completion switch
    {
        EncounterWaveCompletionPolicy.AllDefeated => WaveCompletionPolicy.AllDefeated,
        EncounterWaveCompletionPolicy.ObjectiveCompleted => WaveCompletionPolicy.ObjectiveCompleted,
        EncounterWaveCompletionPolicy.TimerElapsed => WaveCompletionPolicy.TimerElapsed,
        EncounterWaveCompletionPolicy.Script => WaveCompletionPolicy.Script,
        _ => throw new ArgumentOutOfRangeException(nameof(completion)),
    };

    private static RpgVector3 Map(Forge.Domain.Spatial.Vector3Value value) => new(value.X, value.Y, value.Z);

    private static RpgVector3 ShapeSize(EncounterWorldProfile.ActivationVolume volume)
    {
        if (volume.Points.Count >= 2)
        {
            var x = Math.Max(.001, volume.Points.Max(item => item.X) - volume.Points.Min(item => item.X));
            var y = Math.Max(.001, volume.Height);
            var z = Math.Max(.001, volume.Points.Max(item => item.Z) - volume.Points.Min(item => item.Z));
            return new(x, y, z);
        }
        var diameter = Math.Max(.001, volume.Radius * 2);
        return new(diameter, Math.Max(diameter, volume.Height), diameter);
    }
}
