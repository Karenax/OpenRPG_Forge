using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureRoleBehaviorProfileTests
{
    [Fact]
    public void CompleteProfileValidatesAndAutoLevelPreviewIsDeterministic()
    {
        var profile = Complete();
        var progression = Progression();

        var diagnostics = CreatureRoleBehaviorValidator.Validate(profile, progression);
        var first = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(profile, progression);
        var second = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(profile, progression);

        Assert.Empty(diagnostics);
        Assert.True(first.Succeeded);
        Assert.False(first.ManualLocked);
        Assert.Equal(first.Result!.Tracks.Select(item => (item.TrackId, item.Level)),
            second.Result!.Tracks.Select(item => (item.TrackId, item.Level)));
        Assert.Equal(first.Result.Skills.Select(item => (item.SkillId, item.Rank)),
            second.Result.Skills.Select(item => (item.SkillId, item.Rank)));
        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(6, first.Result.Tracks.Sum(item => item.Level));
        Assert.Equal(4, first.Result.Tracks.Single(item => item.TrackId == "warden").Level);
        Assert.Equal(2, first.Result.Tracks.Single(item => item.TrackId == "mystic").Level);
        Assert.Equal(3, first.Result.Skills.Single(item => item.SkillId == "awareness").Rank);
        Assert.Equal(1, first.Result.Talents.Single(item => item.TalentId == "ember-stance").Rank);
        Assert.NotEmpty(first.Changes);
    }

    [Fact]
    public void ScheduleSelectsAmbientAnimationAndLoreHasNoImplicitMechanics()
    {
        var snapshot = CreatureRoleBehaviorRuntimeResolver.Resolve(Complete(), Progression(), 22 * 60, false, true);

        Assert.True(snapshot.Succeeded);
        Assert.Equal("idle.night", snapshot.ActiveIdleSetId);
        Assert.Contains(snapshot.ActiveAnimationId, new[] { "look-around", "warm-hands" });
        Assert.Equal("campfire", snapshot.WaypointId);
        Assert.Equal("deity.embers", snapshot.LoreFields["deity"]);
        Assert.Contains("guardian", snapshot.RoleTags);
    }

    [Fact]
    public void ManualLockProducesNoProgressionDiffAndOptionalAbsenceIsSafe()
    {
        var profile = Complete();
        var locked = new CreatureRoleBehaviorProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.RequiredAtRuntime, profile.Role, profile.Ambient,
            profile.AutoLevel with { ApplyOnSave = false, ManualLock = true }, profile.Overrides);

        var preview = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(locked, Progression());
        var optional = CreatureRoleBehaviorRuntimeResolver.Resolve(null, null, 0, false);
        var required = CreatureRoleBehaviorRuntimeResolver.Resolve(null, null, 0, false, true);

        Assert.True(preview.Succeeded);
        Assert.True(preview.ManualLocked);
        Assert.Empty(preview.Changes);
        Assert.True(optional.Succeeded);
        Assert.True(optional.IgnoredOptionalProfile);
        Assert.False(required.Succeeded);
        Assert.Equal("creature.role_behavior.required_missing", Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void InvalidProfileReportsStableLocalizedDiagnostics()
    {
        var invalid = new CreatureRoleBehaviorProfile(Guid.Empty, 99, "", true,
            new("invalid ruleset", new Dictionary<string, string> { ["invalid key"] = "" }, ["bad tag"]),
            new("invalid graph", [new(Guid.Empty, "", [], 0, 0, -1)],
                [new(Guid.Empty, "", -1, 2000, "missing", "missing waypoint", null)],
                [new(Guid.Empty, "", "", "", -1)], new(true, true, true, AmbientResumeMode.Resume, -1)),
            new(Guid.Empty, "invalid template", 0, 0, false, false,
                [new("missing", 0)], [new("missing", 0, -1)], [new("missing", 0)]),
            new Dictionary<string, string> { ["invalid key"] = "" });

        var diagnostics = CreatureRoleBehaviorValidator.Validate(invalid, Progression(),
            "blueprints/test/creatureRoleBehavior", new HashSet<string>(), new HashSet<string>());

        Assert.Contains(diagnostics, item => item.Code == "creature.role_behavior.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.role.lore_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.ambient.idle_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.ambient.schedule_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.ambient.interaction_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.autolevel.template_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.autolevel.reference_missing");
        Assert.Contains(diagnostics, item => item.Code == "creature.role_behavior.override_invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/test/creatureRoleBehavior", item.Path));
    }

    internal static CreatureRoleBehaviorProfile Complete() => new(
        Guid.Parse("a1400000-0000-4000-8000-000000000001"), 1, "author:test", true,
        new("forge.generic", new Dictionary<string, string>
        {
            ["deity"] = "deity.embers",
            ["occupation"] = "crypt-warden",
        }, ["guardian", "mystic"]),
        new("ambient.warden",
        [
            new(Guid.Parse("a1400000-0000-4000-8000-000000000002"), "idle.day", ["idle", "inspect"], 2, 2, 5),
            new(Guid.Parse("a1400000-0000-4000-8000-000000000003"), "idle.night", ["look-around", "warm-hands"], 1, 3, 8),
        ],
        [
            new(Guid.Parse("a1400000-0000-4000-8000-000000000004"), "night-watch", 1200, 360,
                "idle.night", "campfire", "warden-warning"),
        ],
        [
            new(Guid.Parse("a1400000-0000-4000-8000-000000000005"), "inspect-brazier", "brazier", "inspect", 12),
        ], new(true, true, true, AmbientResumeMode.Resume, .5)),
        new(Guid.Parse("a1400000-0000-4000-8000-000000000006"), "warden-balanced", 42, 6, true, false,
            [new("warden", 2), new("mystic", 1)],
            [new("awareness", 2, 10), new("lore", 3, 10)],
            [new("ember-stance", 4)]),
        new Dictionary<string, string> { ["ambient.radius"] = "4" });

    internal static CreatureProgressionProfile Progression() => new(
        Guid.Parse("a1400000-0000-4000-8000-000000000010"), 1, "author:test", "forge.generic", true, false,
        [new("warden", "Gardien", 1, 20, 0, []), new("mystic", "Mystique", 1, 20, 0, [])], [],
        [
            new("awareness", "Vigilance", "insight", 0, 10, 0, false, false, 10),
            new("lore", "Savoir", "insight", 0, 10, 0, false, false, 10),
        ],
        [new("ember-stance", "Posture des braises", 0, 1, true, [], [], [])],
        new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
}
