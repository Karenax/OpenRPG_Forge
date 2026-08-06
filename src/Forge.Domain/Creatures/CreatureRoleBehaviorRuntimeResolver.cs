using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Creatures;

public sealed record CreatureAutoLevelChange(string Path, string Before, string After);

public sealed record CreatureAutoLevelPreview(
    bool Succeeded,
    bool ManualLocked,
    CreatureProgressionProfile? Result,
    IReadOnlyList<CreatureAutoLevelChange> Changes,
    IReadOnlyList<CreatureRoleBehaviorDiagnostic> Diagnostics);

public sealed record CreatureRoleBehaviorRuntimeSnapshot(
    bool Succeeded,
    bool IgnoredOptionalProfile,
    IReadOnlyDictionary<string, string> LoreFields,
    IReadOnlyList<string> RoleTags,
    string? ActiveIdleSetId,
    string? ActiveAnimationId,
    string? WaypointId,
    string? ConversationId,
    CreatureAutoLevelPreview? AutoLevel,
    IReadOnlyList<CreatureRoleBehaviorDiagnostic> Diagnostics);

public static class CreatureRoleBehaviorRuntimeResolver
{
    public static CreatureRoleBehaviorRuntimeSnapshot Resolve(
        CreatureRoleBehaviorProfile? profile,
        CreatureProgressionProfile? progression,
        int minuteOfDay,
        bool interrupted,
        bool required = false)
    {
        if (profile is null)
        {
            var diagnostics = required
                ? new[] { new CreatureRoleBehaviorDiagnostic("creature.role_behavior.required_missing",
                    "creatureRoleBehavior", "The runtime requires a creature role and behavior profile.") }
                : [];
            return new(!required, !required, new Dictionary<string, string>(), [], null, null, null, null, null,
                diagnostics);
        }

        var validation = CreatureRoleBehaviorValidator.Validate(profile, progression);
        if (validation.Count > 0)
            return new(false, false, new Dictionary<string, string>(), [], null, null, null, null, null, validation);

        var preview = PreviewAutoLevel(profile, progression);
        if (!preview.Succeeded)
            return new(false, false, new Dictionary<string, string>(), [], null, null, null, null, preview,
                preview.Diagnostics);

        var minute = ((minuteOfDay % 1440) + 1440) % 1440;
        var schedule = profile.Ambient.Schedules.FirstOrDefault(item => IsActive(item, minute));
        var idleSet = schedule is null
            ? SelectWeightedIdle(profile.Ambient.IdleSets, profile.AutoLevel.Seed, minute)
            : profile.Ambient.IdleSets.Single(item => item.SetId == schedule.IdleSetId);
        var animation = interrupted && profile.Ambient.Interruption.ResumeMode == AmbientResumeMode.Stop
            ? null
            : SelectAnimation(idleSet, profile.AutoLevel.Seed, minute);
        return new(true, false, profile.Role.LoreFields, profile.Role.RoleTags,
            animation is null ? null : idleSet.SetId, animation, schedule?.WaypointId, schedule?.ConversationId,
            preview, []);
    }

    public static CreatureAutoLevelPreview PreviewAutoLevel(
        CreatureRoleBehaviorProfile profile,
        CreatureProgressionProfile? progression)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = CreatureRoleBehaviorValidator.Validate(profile, progression);
        if (diagnostics.Count > 0)
            return new(false, profile.AutoLevel.ManualLock, progression, [], diagnostics);
        if (profile.AutoLevel.ManualLock)
            return new(true, true, progression, [], []);
        if (progression is null)
            return new(false, false, null, [], [new("creature.autolevel.progression_missing",
                "creatureRoleBehavior/autoLevel", "An unlocked auto-level template requires progression.")]);

        var template = profile.AutoLevel;
        var levels = AllocateTrackLevels(template);
        var tracks = progression.Tracks.Select(item => item with { Level = levels[item.TrackId] }).ToArray();
        var skillRules = template.Skills.ToDictionary(item => item.SkillId, StringComparer.Ordinal);
        var skills = progression.Skills.Select(item => skillRules.TryGetValue(item.SkillId, out var rule)
            ? item with { Rank = Math.Min(rule.MaximumRank, template.TargetLevel / rule.RankEveryLevels) }
            : item).ToArray();
        var talentRules = template.Talents.ToDictionary(item => item.TalentId, StringComparer.Ordinal);
        var talents = progression.Talents.Select(item => talentRules.TryGetValue(item.TalentId, out var rule)
            ? item with { Rank = template.TargetLevel >= rule.UnlockLevel ? Math.Max(1, item.Rank) : 0 }
            : item).ToArray();
        var result = new CreatureProgressionProfile(
            progression.Id,
            progression.SchemaVersion,
            progression.Provenance,
            progression.RulesetId,
            progression.AllowMultipleTracks,
            progression.RequiredAtRuntime,
            tracks,
            progression.DispositionAxes,
            skills,
            talents,
            progression.Loadout,
            progression.Overrides);
        var changes = new List<CreatureAutoLevelChange>();
        AddChanges(progression.Tracks, tracks, item => item.TrackId, item => item.Level, "tracks", changes);
        AddChanges(progression.Skills, skills, item => item.SkillId, item => item.Rank, "skills", changes);
        AddChanges(progression.Talents, talents, item => item.TalentId, item => item.Rank, "talents", changes);
        return new(true, false, result, changes, []);
    }

    private static IReadOnlyDictionary<string, int> AllocateTrackLevels(
        CreatureRoleBehaviorProfile.ProgressionTemplate template)
    {
        var levels = template.Tracks.ToDictionary(item => item.TrackId, _ => 1, StringComparer.Ordinal);
        for (var allocated = template.Tracks.Count; allocated < template.TargetLevel; allocated++)
        {
            var chosen = template.Tracks
                .OrderBy(item => levels[item.TrackId] / (double)item.Weight)
                .ThenBy(item => StableOrder(template.Seed, item.TrackId, allocated))
                .ThenBy(item => item.TrackId, StringComparer.Ordinal)
                .First();
            levels[chosen.TrackId]++;
        }
        return levels;
    }

    private static void AddChanges<T>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, string> id,
        Func<T, int> value,
        string segment,
        ICollection<CreatureAutoLevelChange> changes)
    {
        var previous = before.ToDictionary(id, value, StringComparer.Ordinal);
        foreach (var item in after)
        {
            var current = value(item);
            if (previous[id(item)] != current)
                changes.Add(new($"creatureProgression/{segment}/{id(item)}", previous[id(item)].ToString(),
                    current.ToString()));
        }
    }

    private static bool IsActive(CreatureRoleBehaviorProfile.AmbientScheduleEntry schedule, int minute) =>
        schedule.StartMinute < schedule.EndMinute
            ? minute >= schedule.StartMinute && minute < schedule.EndMinute
            : minute >= schedule.StartMinute || minute < schedule.EndMinute;

    private static CreatureRoleBehaviorProfile.AmbientIdleSet SelectWeightedIdle(
        IReadOnlyList<CreatureRoleBehaviorProfile.AmbientIdleSet> idleSets,
        int seed,
        int minute)
    {
        var total = idleSets.Sum(item => item.Weight);
        var cursor = StableUnit(seed, "idle", minute) * total;
        foreach (var idle in idleSets)
        {
            cursor -= idle.Weight;
            if (cursor <= 0) return idle;
        }
        return idleSets[^1];
    }

    private static string SelectAnimation(
        CreatureRoleBehaviorProfile.AmbientIdleSet idleSet,
        int seed,
        int minute)
    {
        var index = (int)Math.Floor(StableUnit(seed, idleSet.SetId, minute) * idleSet.AnimationIds.Count);
        return idleSet.AnimationIds[Math.Min(index, idleSet.AnimationIds.Count - 1)];
    }

    private static ulong StableOrder(int seed, string value, int step)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{value}|{step}"));
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static double StableUnit(int seed, string value, int step) =>
        StableOrder(seed, value, step) / (double)ulong.MaxValue;
}
