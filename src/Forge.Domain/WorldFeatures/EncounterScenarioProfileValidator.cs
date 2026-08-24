using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterScenarioResolution(
    bool Available,
    bool OptionalFallback,
    EncounterScenarioProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class EncounterScenarioProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(
        EncounterScenarioProfile profile,
        EncounterWorldProfile.FoundationPolicy? foundation,
        EncounterCompositionProfile? composition,
        string path,
        IReadOnlySet<Guid>? knownEncounterIds = null,
        IReadOnlySet<Guid>? knownQuestIds = null,
        Guid? ownerEncounterId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostics = new List<DomainDiagnostic>();

        if (profile.Id == Guid.Empty || profile.SchemaVersion != EncounterScenarioProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add(diagnostics, "world.encounter.scenario_identity_invalid",
                "Encounter scenario requires UUID, schema v1 and provenance.", path);

        var componentIds = new List<Guid>
        {
            profile.Activator.Id,
            profile.Lifecycle.Id,
            profile.Lifecycle.Entered.Id,
            profile.Lifecycle.Exited.Id,
            profile.Lifecycle.Exhausted.Id,
        };
        componentIds.AddRange(profile.SpawnAnchors.Select(item => item.Id));
        componentIds.AddRange(profile.Phases.Select(item => item.Id));
        componentIds.AddRange(profile.Phases.SelectMany(item => item.Waves).Select(item => item.Id));
        if (componentIds.Any(item => item == Guid.Empty || item == profile.Id) ||
            componentIds.Distinct().Count() != componentIds.Count)
            Add(diagnostics, "world.encounter.scenario_uuid_invalid",
                "Encounter scenario component UUIDs must be non-empty and unique.", $"{path}/components");

        var canonicalAnchors = foundation?.SpawnAnchorIds.ToHashSet(StringComparer.Ordinal) ?? [];
        if (!Enum.IsDefined(profile.Distribution))
            Add(diagnostics, "world.encounter.spawn_distribution_invalid",
                "Encounter spawn distribution must be a supported canonical value.", $"{path}/distribution");
        if (profile.SpawnAnchors.Count == 0 ||
            profile.SpawnAnchors.Select(item => item.AnchorId).Distinct(StringComparer.Ordinal).Count() !=
            profile.SpawnAnchors.Count ||
            profile.SpawnAnchors.Any(item => string.IsNullOrWhiteSpace(item.AnchorId) ||
                !canonicalAnchors.Contains(item.AnchorId) || item.Capacity <= 0 || !double.IsFinite(item.Weight) ||
                item.Weight < 0 || !Enum.IsDefined(item.Visibility) || !TransformIsBounded(item.Transform)) ||
            profile.SpawnAnchors.All(item => !item.Navigable) ||
            profile.SpawnAnchors.Count(item => item.IsFallback) != 1)
            Add(diagnostics, "world.encounter.spawn_anchor_policy_invalid",
                "Manual spawn anchors require canonical IDs, bounded transforms, capacity, navigation and one fallback.",
                $"{path}/spawnAnchors");

        var activator = profile.Activator;
        if (!Enum.IsDefined(activator.RequiredAuthority) ||
            activator.RequiredTags.Any(string.IsNullOrWhiteSpace) ||
            activator.RequiredTags.Distinct(StringComparer.Ordinal).Count() != activator.RequiredTags.Count)
            Add(diagnostics, "world.encounter.activator_policy_invalid",
                "Encounter activator tags must be non-empty and unique.", $"{path}/activator");

        var lifecycle = profile.Lifecycle;
        if (!Version.TryParse(lifecycle.ForgeScriptApiVersion, out _) ||
            !string.Equals(lifecycle.Entered.Binding, "EncounterEntered", StringComparison.Ordinal) ||
            !string.Equals(lifecycle.Exited.Binding, "EncounterExited", StringComparison.Ordinal) ||
            !string.Equals(lifecycle.Exhausted.Binding, "EncounterExhausted", StringComparison.Ordinal) ||
            new[] { lifecycle.Entered.ScriptId, lifecycle.Exited.ScriptId, lifecycle.Exhausted.ScriptId }
                .Any(item => item is not null && string.IsNullOrWhiteSpace(item)))
            Add(diagnostics, "world.encounter.lifecycle_binding_invalid",
                "Encounter lifecycle requires a versioned API and exact entered, exited and exhausted bindings.",
                $"{path}/lifecycle");

        var groupIds = composition?.Groups.Select(item => item.Id).ToHashSet() ?? [];
        var phaseOrders = profile.Phases.Select(item => item.Order).ToArray();
        if (profile.Phases.Count == 0 || phaseOrders.Any(item => item <= 0) ||
            phaseOrders.Distinct().Count() != phaseOrders.Length ||
            profile.Phases.Any(item => string.IsNullOrWhiteSpace(item.DisplayName) || item.Waves.Count == 0 ||
                item.EntryConditions.Any(string.IsNullOrWhiteSpace) ||
                item.EntryConditions.Distinct(StringComparer.Ordinal).Count() != item.EntryConditions.Count))
            Add(diagnostics, "world.encounter.phase_policy_invalid",
                "Encounter phases require names, unique positive order, conditions and at least one wave.",
                $"{path}/phases");

        var waves = profile.Phases.SelectMany(item => item.Waves).ToArray();
        var waveIds = waves.Select(item => item.Id).ToHashSet();
        var orderedWaveIndex = profile.Phases.OrderBy(item => item.Order)
            .SelectMany(item => item.Waves.OrderBy(wave => wave.Order))
            .Select((wave, index) => (wave.Id, index))
            .ToDictionary(item => item.Id, item => item.index);
        foreach (var phase in profile.Phases)
        {
            if (phase.Waves.Any(item => item.Order <= 0) ||
                phase.Waves.Select(item => item.Order).Distinct().Count() != phase.Waves.Count)
                Add(diagnostics, "world.encounter.wave_order_invalid",
                    "Encounter waves require unique positive order within each phase.", $"{path}/phases/{phase.Id}/waves");

            if (phase.ReinforcementEncounterIds.Distinct().Count() != phase.ReinforcementEncounterIds.Count ||
                phase.ReinforcementEncounterIds.Any(item => item == Guid.Empty || item == ownerEncounterId ||
                    knownEncounterIds is not null && !knownEncounterIds.Contains(item)))
                Add(diagnostics, "world.encounter.reinforcement_reference_invalid",
                    "Encounter reinforcements require unique known encounter UUIDs and cannot reference the owner.",
                    $"{path}/phases/{phase.Id}/reinforcements");

            foreach (var wave in phase.Waves)
            {
                var wavePath = $"{path}/phases/{phase.Id}/waves/{wave.Id}";
                if (wave.GroupIds.Count == 0 || wave.GroupIds.Distinct().Count() != wave.GroupIds.Count ||
                    wave.GroupIds.Any(item => !groupIds.Contains(item)) || !double.IsFinite(wave.DelaySeconds) ||
                    wave.DelaySeconds < 0)
                    Add(diagnostics, "world.encounter.wave_composition_invalid",
                        "Encounter waves require known composition groups and non-negative finite delay.", wavePath);
                if (!Enum.IsDefined(wave.Completion) ||
                    wave.Completion == EncounterWaveCompletionPolicy.TimerElapsed && wave.DelaySeconds <= 0 ||
                    wave.Completion == EncounterWaveCompletionPolicy.Script &&
                    string.IsNullOrWhiteSpace(wave.CompletionTargetId) ||
                    wave.Completion == EncounterWaveCompletionPolicy.ObjectiveCompleted &&
                    (wave.QuestId is null || wave.QuestId == Guid.Empty || string.IsNullOrWhiteSpace(wave.ObjectiveId) ||
                     knownQuestIds is not null && !knownQuestIds.Contains(wave.QuestId.Value)))
                    Add(diagnostics, "world.encounter.wave_completion_invalid",
                        "Wave completion requires timer delay, script target or a known quest objective as appropriate.",
                        $"{wavePath}/completion");
                if (wave.VoiceCueId is not null &&
                    (string.IsNullOrWhiteSpace(wave.VoiceCueId) ||
                     !wave.VoiceCueId.StartsWith("audio:", StringComparison.Ordinal)))
                    Add(diagnostics, "world.encounter.wave_voice_reference_invalid",
                        "Wave voice cues require a typed audio address.", $"{wavePath}/voiceCueId");
                if (wave.NextWaveId is { } next && (!waveIds.Contains(next) || next == wave.Id ||
                    orderedWaveIndex.TryGetValue(wave.Id, out var currentIndex) &&
                    orderedWaveIndex.TryGetValue(next, out var nextIndex) && nextIndex <= currentIndex))
                    Add(diagnostics, "world.encounter.wave_transition_invalid",
                        "Wave transitions must reference a later known wave.", $"{wavePath}/nextWaveId");
            }
        }

        if (profile.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.encounter.scenario_override_invalid",
                "Encounter scenario overrides require non-empty keys and values.", $"{path}/overrides");
        return diagnostics;
    }

    public static EncounterScenarioResolution Resolve(EncounterWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var path = $"module/worldFeatures/encounters/{profile.Id}/scenario";
        if (profile.Scenario is null)
        {
            var missing = profile.ScenarioRequiredAtRuntime
                ? new[] { new DomainDiagnostic("world.encounter.scenario_required",
                    "Encounter scenario is required by this runtime profile.", path) }
                : [];
            return new(false, !profile.ScenarioRequiredAtRuntime, null, missing);
        }

        var diagnostics = Validate(profile.Scenario, profile.Foundation, profile.Composition, path,
            ownerEncounterId: profile.Id);
        return new(diagnostics.Count == 0, false, profile.Scenario, diagnostics);
    }

    private static bool TransformIsBounded(Spatial.TransformValue transform) =>
        new[] { transform.Position.X, transform.Position.Y, transform.Position.Z }.All(value => Math.Abs(value) <= 1_000_000) &&
        new[] { transform.Scale.X, transform.Scale.Y, transform.Scale.Z }.All(value => value > 0 && value <= 1_000);

    private static void Add(List<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));
}
