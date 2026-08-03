using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Encounters;

public static class EncounterAuthoringService
{
    public static EncounterWizardPreview Preview(EncounterWizardRequest request, EncounterValidationContext context)
    {
        var encounter = new EncounterDefinition(
            DeterministicId(request.Slug), 1, request.Provenance, request.Slug.Trim(), request.DisplayName.Trim(),
            request.Category.Trim(), request.Theme.Trim(), request.ZoneId, request.Volume, request.Candidates,
            request.DifficultyProfile, request.GroupIds, request.SpawnAnchorIds, request.RewardIds,
            request.NetworkActorCap);
        var diagnostics = Validate(encounter, context);
        var diff = $"add:/encounters/{encounter.Id}|slug:{encounter.Slug}|role:{request.PrimaryRole}|candidates:{encounter.Candidates.Count}|budget:{encounter.DifficultyProfile.EffectiveBudget:0.##}";
        return new(encounter, diagnostics, diff, diagnostics.Count == 0);
    }

    public static EncounterDefinition Apply(EncounterWizardRequest request, EncounterValidationContext context)
    {
        var preview = Preview(request, context);
        if (!preview.CanApply)
        {
            throw new InvalidOperationException(string.Join("; ", preview.Diagnostics.Select(item => $"{item.Code}@{item.Path}")));
        }

        return preview.Encounter;
    }

    public static IReadOnlyList<EncounterDiagnostic> Validate(EncounterDefinition encounter, EncounterValidationContext context)
    {
        var diagnostics = new List<EncounterDiagnostic>();
        var root = $"encounters/{encounter.Id}";
        if (encounter.Id == Guid.Empty || encounter.SchemaVersion != 1 || string.IsNullOrWhiteSpace(encounter.Provenance) || string.IsNullOrWhiteSpace(encounter.Slug) || string.IsNullOrWhiteSpace(encounter.DisplayName))
            diagnostics.Add(new("RPG_ENCOUNTER_IDENTITY_INVALID", root, "Encounter identity is incomplete."));
        if (!context.ZoneIds.Contains(encounter.ZoneId))
            diagnostics.Add(new("RPG_ENCOUNTER_ZONE_MISSING", $"{root}/zoneId", "Encounter zone does not resolve."));
        if (encounter.Candidates.Count == 0)
            diagnostics.Add(new("RPG_ENCOUNTER_POOL_EMPTY", $"{root}/candidates", "Encounter requires at least one spawn candidate."));
        foreach (var candidate in encounter.Candidates)
        {
            var path = $"{root}/candidates/{candidate.CreatureId}";
            if (!context.CreatureIds.Contains(candidate.CreatureId) || !context.BlueprintIds.Contains(candidate.BlueprintId))
                diagnostics.Add(new("RPG_ENCOUNTER_CANDIDATE_MISSING", path, "Candidate creature or blueprint does not resolve."));
            if (candidate.Weight <= 0 || candidate.Cost <= 0 || candidate.MaximumAlive <= 0)
                diagnostics.Add(new("RPG_ENCOUNTER_CANDIDATE_INVALID", path, "Weight, cost and alive limit must be positive."));
        }
        if (encounter.Volume.ThreatRadius < 0 || TriggerRules.Validate(new(Guid.NewGuid(), 1, encounter.Provenance, encounter.Slug, encounter.Volume.Shape, encounter.Volume.Filter, new HashSet<string>(), TimeSpan.Zero, TriggerFirePolicy.Repeat, [new("start-encounter", encounter.Id.ToString(), new Dictionary<string, string>())], null)).Count != 0)
            diagnostics.Add(new("RPG_ENCOUNTER_VOLUME_INVALID", $"{root}/volume", "Activation geometry or threat radius is invalid."));
        if (encounter.Volume.ActivationScript is { Length: > 0 } script && !context.ScriptIds.Contains(script))
            diagnostics.Add(new("RPG_ENCOUNTER_SCRIPT_MISSING", $"{root}/volume/activationScript", "Activation script does not resolve."));
        if (encounter.SpawnAnchorIds.Count == 0 || encounter.SpawnAnchorIds.Any(anchor => !context.SpawnAnchorIds.Contains(anchor)))
            diagnostics.Add(new("RPG_ENCOUNTER_ANCHOR_INVALID", $"{root}/spawnAnchorIds", "At least one reachable spawn anchor is required."));
        if (encounter.RewardIds.Any(reward => !context.RewardIds.Contains(reward)))
            diagnostics.Add(new("RPG_ENCOUNTER_REWARD_MISSING", $"{root}/rewardIds", "Encounter reward does not resolve."));
        if (string.IsNullOrWhiteSpace(encounter.DifficultyProfile.RulesetId) || encounter.DifficultyProfile.ExpectedPartySize <= 0 || encounter.DifficultyProfile.ExpectedPartyPower < 0 || encounter.DifficultyProfile.EffectiveBudget <= 0 || encounter.DifficultyProfile.EffectiveBudget > context.MaximumBudget)
            diagnostics.Add(new("RPG_ENCOUNTER_BUDGET_INVALID", $"{root}/difficultyProfile", "Ruleset, party estimate or effective budget is invalid."));
        if (encounter.NetworkActorCap <= 0 || encounter.NetworkActorCap > context.MaximumNetworkActorCap || encounter.Candidates.Any(candidate => candidate.MaximumAlive > encounter.NetworkActorCap))
            diagnostics.Add(new("RPG_ENCOUNTER_NETWORK_CAP_INVALID", $"{root}/networkActorCap", "Spawn limits exceed the authoritative actor cap."));
        return diagnostics;
    }

    private static Guid DeterministicId(string slug)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:encounter:{slug.Trim().ToLowerInvariant()}"));
        return new Guid(bytes[..16]);
    }
}
