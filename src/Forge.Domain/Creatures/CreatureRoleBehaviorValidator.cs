namespace Forge.Domain.Creatures;

public static class CreatureRoleBehaviorValidator
{
    public static IReadOnlyList<CreatureRoleBehaviorDiagnostic> Validate(
        CreatureRoleBehaviorProfile profile,
        CreatureProgressionProfile? progression = null,
        string path = "creatureRoleBehavior",
        IReadOnlySet<string>? availableWaypointIds = null,
        IReadOnlySet<string>? availableConversationIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<CreatureRoleBehaviorDiagnostic>();
        Add(profile.Id == Guid.Empty, "creature.role_behavior.uuid_required", path,
            "A stable role and behavior UUID is required.");
        Add(profile.SchemaVersion != CreatureRoleBehaviorProfile.CurrentSchemaVersion,
            "creature.role_behavior.schema_unsupported", path, "The role and behavior schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "creature.role_behavior.provenance_required", path,
            "Role and behavior provenance is required.");

        ValidateRole(profile, path, diagnostics);
        ValidateAmbient(profile, path, availableWaypointIds, availableConversationIds, diagnostics);
        ValidateAutoLevel(profile, progression, path, diagnostics);
        if (profile.Overrides.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) ||
                                          string.IsNullOrWhiteSpace(item.Value)))
        {
            diagnostics.Add(new("creature.role_behavior.override_invalid", $"{path}/overrides",
                "Overrides require canonical keys and explicit non-empty values."));
        }
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidateRole(
        CreatureRoleBehaviorProfile profile,
        string path,
        ICollection<CreatureRoleBehaviorDiagnostic> diagnostics)
    {
        var role = profile.Role;
        if (!CreaturePresentationProfile.IsCanonicalIdentifier(role.RulesetTemplateId))
            diagnostics.Add(new("creature.role.ruleset_invalid", $"{path}/role/rulesetTemplateId",
                "A canonical ruleset template identifier is required."));
        if (HasDuplicate(role.RoleTags) || role.RoleTags.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item)))
            diagnostics.Add(new("creature.role.tags_invalid", $"{path}/role/roleTags",
                "Role tags must be unique canonical identifiers."));
        if (role.LoreFields.Count > 64 || role.LoreFields.Any(item =>
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.Key) || string.IsNullOrWhiteSpace(item.Value) ||
                item.Value.Length > 512))
            diagnostics.Add(new("creature.role.lore_invalid", $"{path}/role/loreFields",
                "Lore fields require canonical keys and explicit values of at most 512 characters."));
    }

    private static void ValidateAmbient(
        CreatureRoleBehaviorProfile profile,
        string path,
        IReadOnlySet<string>? availableWaypointIds,
        IReadOnlySet<string>? availableConversationIds,
        ICollection<CreatureRoleBehaviorDiagnostic> diagnostics)
    {
        var ambient = profile.Ambient;
        if (!CreaturePresentationProfile.IsCanonicalIdentifier(ambient.GraphId))
            diagnostics.Add(new("creature.ambient.graph_invalid", $"{path}/ambient/graphId",
                "A canonical ambient graph identifier is required."));
        if (ambient.IdleSets.Count == 0 || HasDuplicate(ambient.IdleSets.Select(item => item.Id)) ||
            HasDuplicate(ambient.IdleSets.Select(item => item.SetId)) || ambient.IdleSets.Any(item =>
                item.Id == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(item.SetId) ||
                item.AnimationIds.Count == 0 || HasDuplicate(item.AnimationIds) ||
                item.AnimationIds.Any(value => !CreaturePresentationProfile.IsCanonicalIdentifier(value)) ||
                !double.IsFinite(item.Weight) || item.Weight <= 0 ||
                !double.IsFinite(item.MinimumSeconds) || !double.IsFinite(item.MaximumSeconds) ||
                item.MinimumSeconds <= 0 || item.MaximumSeconds < item.MinimumSeconds || item.MaximumSeconds > 86400))
            diagnostics.Add(new("creature.ambient.idle_invalid", $"{path}/ambient/idleSets",
                "Idle sets require UUIDs, unique IDs, animation IDs, positive weights and bounded durations."));

        var idleIds = ambient.IdleSets.Select(item => item.SetId).ToHashSet(StringComparer.Ordinal);
        if (HasDuplicate(ambient.Schedules.Select(item => item.Id)) ||
            HasDuplicate(ambient.Schedules.Select(item => item.ScheduleId)) || ambient.Schedules.Any(item =>
                item.Id == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(item.ScheduleId) ||
                item.StartMinute is < 0 or > 1439 || item.EndMinute is < 0 or > 1439 ||
                item.StartMinute == item.EndMinute || !idleIds.Contains(item.IdleSetId) ||
                item.WaypointId is not null && !CreaturePresentationProfile.IsCanonicalIdentifier(item.WaypointId) ||
                item.ConversationId is not null && !CreaturePresentationProfile.IsCanonicalIdentifier(item.ConversationId)))
            diagnostics.Add(new("creature.ambient.schedule_invalid", $"{path}/ambient/schedules",
                "Schedules require UUIDs, unique IDs, bounded time windows and a resolved idle set."));
        if (availableWaypointIds is not null && ambient.Schedules.Any(item =>
                item.WaypointId is not null && !availableWaypointIds.Contains(item.WaypointId)))
            diagnostics.Add(new("creature.ambient.waypoint_missing", $"{path}/ambient/schedules",
                "Every scheduled waypoint must resolve in the project."));
        if (availableConversationIds is not null && ambient.Schedules.Any(item =>
                item.ConversationId is not null && !availableConversationIds.Contains(item.ConversationId)))
            diagnostics.Add(new("creature.ambient.conversation_missing", $"{path}/ambient/schedules",
                "Every scheduled conversation must resolve in the project."));

        if (HasDuplicate(ambient.Interactions.Select(item => item.Id)) ||
            HasDuplicate(ambient.Interactions.Select(item => item.InteractionId)) || ambient.Interactions.Any(item =>
                item.Id == Guid.Empty || !CreaturePresentationProfile.IsCanonicalIdentifier(item.InteractionId) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.TargetId) ||
                !CreaturePresentationProfile.IsCanonicalIdentifier(item.AnimationId) ||
                !double.IsFinite(item.CooldownSeconds) || item.CooldownSeconds < 0 || item.CooldownSeconds > 86400))
            diagnostics.Add(new("creature.ambient.interaction_invalid", $"{path}/ambient/interactions",
                "Interactions require UUIDs, unique IDs, canonical targets/animations and bounded cooldowns."));
        if (!double.IsFinite(ambient.Interruption.ResumeDelaySeconds) ||
            ambient.Interruption.ResumeDelaySeconds is < 0 or > 86400)
            diagnostics.Add(new("creature.ambient.interruption_invalid", $"{path}/ambient/interruption",
                "The ambient resume delay must be finite and bounded."));
    }

    private static void ValidateAutoLevel(
        CreatureRoleBehaviorProfile profile,
        CreatureProgressionProfile? progression,
        string path,
        ICollection<CreatureRoleBehaviorDiagnostic> diagnostics)
    {
        var template = profile.AutoLevel;
        var invalid = template.Id == Guid.Empty ||
                      !CreaturePresentationProfile.IsCanonicalIdentifier(template.TemplateId) ||
                      template.TargetLevel is < 1 or > 999 || template.ManualLock && template.ApplyOnSave ||
                      HasDuplicate(template.Tracks.Select(item => item.TrackId)) ||
                      template.Tracks.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.TrackId) || item.Weight <= 0) ||
                      HasDuplicate(template.Skills.Select(item => item.SkillId)) ||
                      template.Skills.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.SkillId) ||
                          item.RankEveryLevels < 1 || item.MaximumRank < 0 || item.MaximumRank > 999) ||
                      HasDuplicate(template.Talents.Select(item => item.TalentId)) ||
                      template.Talents.Any(item => !CreaturePresentationProfile.IsCanonicalIdentifier(item.TalentId) ||
                          item.UnlockLevel is < 1 or > 999);
        if (invalid)
            diagnostics.Add(new("creature.autolevel.template_invalid", $"{path}/autoLevel",
                "The progression template requires a UUID, deterministic seed, bounded target and coherent allocations."));

        if (template.ManualLock) return;
        if (progression is null)
        {
            diagnostics.Add(new("creature.autolevel.progression_missing", $"{path}/autoLevel",
                "An unlocked auto-level template requires a creature progression profile."));
            return;
        }
        var tracks = progression.Tracks.Select(item => item.TrackId).ToHashSet(StringComparer.Ordinal);
        var skills = progression.Skills.Select(item => item.SkillId).ToHashSet(StringComparer.Ordinal);
        var talents = progression.Talents.Select(item => item.TalentId).ToHashSet(StringComparer.Ordinal);
        if (template.Tracks.Count != tracks.Count || template.TargetLevel < template.Tracks.Count ||
            template.Tracks.Any(item => !tracks.Contains(item.TrackId)) ||
            template.Skills.Any(item => !skills.Contains(item.SkillId)) ||
            template.Talents.Any(item => !talents.Contains(item.TalentId)))
            diagnostics.Add(new("creature.autolevel.reference_missing", $"{path}/autoLevel",
                "Track, skill and talent allocations must resolve in the creature progression profile."));
    }

    private static bool HasDuplicate<T>(IEnumerable<T> values) where T : notnull
    {
        var items = values.ToArray();
        return items.Distinct().Count() != items.Length;
    }
}
