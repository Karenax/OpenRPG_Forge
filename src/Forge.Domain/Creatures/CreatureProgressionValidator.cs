namespace Forge.Domain.Creatures;

public static class CreatureProgressionValidator
{
    private static readonly IReadOnlySet<string> PrerequisiteKinds =
        new HashSet<string>(["track", "skill", "talent"], StringComparer.Ordinal);

    public static IReadOnlyList<CreatureProgressionDiagnostic> Validate(
        CreatureProgressionProfile profile,
        string path = "creatureProgression",
        IReadOnlySet<string>? availableAbilityIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureProgressionDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.progression.uuid_required", path,
            "A stable progression UUID is required.");
        Add(profile.SchemaVersion != CreatureProgressionProfile.CurrentSchemaVersion,
            "creature.progression.schema_unsupported", path, "The progression schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.progression.provenance_required", path,
            "Progression provenance is required.");
        Add(!CreaturePresentationProfile.IsCanonicalIdentifier(profile.RulesetId),
            "creature.progression.ruleset_invalid", $"{path}/rulesetId", "A canonical ruleset identifier is required.");
        Add(profile.Tracks.Count == 0, "creature.progression.track_required", $"{path}/tracks",
            "At least one progression track is required.");
        Add(!profile.AllowMultipleTracks && profile.Tracks.Count > 1,
            "creature.progression.multiclass_forbidden", $"{path}/tracks",
            "This profile does not allow multiple progression tracks.");

        ValidateTracks(profile, path, diagnostics);
        ValidateDisposition(profile, path, diagnostics);
        ValidateSkills(profile, path, diagnostics);
        ValidateTalents(profile, path, diagnostics);
        ValidateLoadout(profile, path, availableAbilityIds, diagnostics);
        ValidateOverrides(profile, path, diagnostics);
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidateTracks(
        CreatureProgressionProfile profile,
        string path,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        if (HasDuplicate(profile.Tracks.Select(item => item.TrackId)) || profile.Tracks.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.TrackId) ||
                string.IsNullOrWhiteSpace(item.DisplayName) || item.Level < 1 ||
                item.MaximumLevel < item.Level || item.MaximumLevel > 999 ||
                !double.IsFinite(item.Experience) || item.Experience < 0 ||
                HasDuplicate(item.GrantedTalentIds) ||
                item.GrantedTalentIds.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value))))
        {
            diagnostics.Add(new("creature.progression.track_invalid", $"{path}/tracks",
                "Tracks require unique IDs, bounded levels, finite experience and unique talent grants."));
        }
    }

    private static void ValidateDisposition(
        CreatureProgressionProfile profile,
        string path,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        if (HasDuplicate(profile.DispositionAxes.Select(item => item.AxisId)) || profile.DispositionAxes.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.AxisId) ||
                string.IsNullOrWhiteSpace(item.NegativeLabel) || string.IsNullOrWhiteSpace(item.PositiveLabel) ||
                !double.IsFinite(item.Value) || item.Value is < -1 or > 1))
        {
            diagnostics.Add(new("creature.progression.disposition_invalid", $"{path}/dispositionAxes",
                "Disposition axes require unique project-defined IDs, two labels and a value between -1 and 1."));
        }
    }

    private static void ValidateSkills(
        CreatureProgressionProfile profile,
        string path,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        if (HasDuplicate(profile.Skills.Select(item => item.SkillId)) || profile.Skills.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.SkillId) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.GoverningAttributeId) ||
                string.IsNullOrWhiteSpace(item.DisplayName) || item.Rank < 0 ||
                item.MaximumRank < item.Rank || item.MaximumRank > 999 || item.TrainingPoints < 0 ||
                !double.IsFinite(item.DefaultDifficulty) || item.DefaultDifficulty < 0 ||
                item.Expertise && !item.Trained))
        {
            diagnostics.Add(new("creature.progression.skill_invalid", $"{path}/skills",
                "Skills require unique IDs, bounded ranks, training points and a finite default difficulty."));
        }
    }

    private static void ValidateTalents(
        CreatureProgressionProfile profile,
        string path,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        var rawTalentIds = profile.Talents.Select(item => item.TalentId).ToArray();
        var trackIds = profile.Tracks.Select(item => item.TrackId).ToHashSet(StringComparer.Ordinal);
        var skillIds = profile.Skills.Select(item => item.SkillId).ToHashSet(StringComparer.Ordinal);
        var talentIds = rawTalentIds.ToHashSet(StringComparer.Ordinal);
        if (HasDuplicate(rawTalentIds) || profile.Talents.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.TalentId) ||
                string.IsNullOrWhiteSpace(item.DisplayName) || item.Rank < 0 ||
                item.MaximumRank < item.Rank || item.MaximumRank > 999 ||
                HasDuplicate(item.GrantedAbilityIds) || HasDuplicate(item.HookIds) ||
                item.GrantedAbilityIds.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value)) ||
                item.HookIds.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value)) ||
                item.Prerequisites.Any(prerequisite => !PrerequisiteKinds.Contains(prerequisite.Kind) ||
                    prerequisite.MinimumRank < 1 || !ReferenceExists(prerequisite, trackIds, skillIds, talentIds) ||
                    !PrerequisiteSatisfied(prerequisite, profile))))
        {
            diagnostics.Add(new("creature.progression.talent_invalid", $"{path}/talents",
                "Talents require unique IDs, bounded ranks, resolved prerequisites and canonical grants/hooks."));
        }

        var grantedTalents = profile.Tracks.SelectMany(item => item.GrantedTalentIds).ToArray();
        if (grantedTalents.Any(item => !talentIds.Contains(item)))
        {
            diagnostics.Add(new("creature.progression.track_talent_missing", $"{path}/tracks",
                "Every talent granted by a progression track must resolve to a talent definition."));
        }
    }

    private static void ValidateLoadout(
        CreatureProgressionProfile profile,
        string path,
        IReadOnlySet<string>? availableAbilityIds,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        var loadout = profile.Loadout;
        var learned = loadout.LearnedAbilityIds.ToHashSet(StringComparer.Ordinal);
        if (!CreaturePresentationProfile.IsCanonicalIdentifier(loadout.ProviderId) || loadout.Capacity is < 0 or > 999 ||
            HasDuplicate(loadout.LearnedAbilityIds) || HasDuplicate(loadout.PreparedAbilities.Select(item => item.AbilityId)) ||
            loadout.LearnedAbilityIds.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item)) ||
            loadout.PreparedAbilities.Count > loadout.Capacity || loadout.PreparedAbilities.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.AbilityId) || !learned.Contains(item.AbilityId) ||
                item.Rank < 1 || item.Charges < 0 || !double.IsFinite(item.CooldownSeconds) ||
                item.CooldownSeconds < 0 || item.ResourceCost < 0 ||
                item.ResourceId is not null && !CreaturePresentationProfile.IsCanonicalIdentifier(item.ResourceId) ||
                HasDuplicate(item.Modifiers) ||
                item.Modifiers.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value))))
        {
            diagnostics.Add(new("creature.progression.loadout_invalid", $"{path}/loadout",
                "The loadout provider, learned powers, capacity, prepared powers and costs must be coherent."));
        }

        var referenced = loadout.LearnedAbilityIds.Concat(
            profile.Talents.SelectMany(item => item.GrantedAbilityIds)).Distinct(StringComparer.Ordinal);
        if (availableAbilityIds is not null && referenced.Any(item => !availableAbilityIds.Contains(item)))
        {
            diagnostics.Add(new("creature.progression.ability_missing", $"{path}/loadout",
                "Every learned or talent-granted power must resolve to the creature combat profile."));
        }
    }

    private static void ValidateOverrides(
        CreatureProgressionProfile profile,
        string path,
        ICollection<CreatureProgressionDiagnostic> diagnostics)
    {
        if (profile.Overrides.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) ||
                                          string.IsNullOrWhiteSpace(item.Value)))
        {
            diagnostics.Add(new("creature.progression.override_invalid", $"{path}/overrides",
                "Overrides require canonical keys and explicit non-empty values."));
        }
    }

    private static bool ReferenceExists(
        CreatureProgressionProfile.TalentPrerequisite prerequisite,
        IReadOnlySet<string> tracks,
        IReadOnlySet<string> skills,
        IReadOnlySet<string> talents) => prerequisite.Kind switch
        {
            "track" => tracks.Contains(prerequisite.ReferenceId),
            "skill" => skills.Contains(prerequisite.ReferenceId),
            "talent" => talents.Contains(prerequisite.ReferenceId),
            _ => false,
        };

    private static bool PrerequisiteSatisfied(
        CreatureProgressionProfile.TalentPrerequisite prerequisite,
        CreatureProgressionProfile profile) => prerequisite.Kind switch
        {
            "track" => profile.Tracks.Single(item => item.TrackId == prerequisite.ReferenceId).Level >= prerequisite.MinimumRank,
            "skill" => profile.Skills.Single(item => item.SkillId == prerequisite.ReferenceId).Rank >= prerequisite.MinimumRank,
            "talent" => profile.Talents.Single(item => item.TalentId == prerequisite.ReferenceId).Rank >= prerequisite.MinimumRank,
            _ => false,
        };

    private static bool HasDuplicate(IEnumerable<string> values)
    {
        var items = values.ToArray();
        return items.Distinct(StringComparer.Ordinal).Count() != items.Length;
    }
}
