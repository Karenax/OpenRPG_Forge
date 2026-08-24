using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class TriggerLivingWorldRuntimeServiceTests
{
    private static readonly BlueprintId TriggerBlueprint = BlueprintId.From(
        Guid.Parse("e0140000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId CompanionBlueprint = BlueprintId.From(
        Guid.Parse("e0140000-0000-4000-8000-000000000002"));

    [Fact]
    public void Ux2LwE_RestRequiresActualDangerAndWholePartyFacts()
    {
        var profile = Profile();

        var optional = TriggerLivingWorldRuntimeService.ResolveRest(profile with { Rest = null },
            new(false, 1, 1));
        var danger = TriggerLivingWorldRuntimeService.ResolveRest(profile, new(true, 2, 2));
        var incomplete = TriggerLivingWorldRuntimeService.ResolveRest(profile, new(false, 1, 2));
        var allowed = TriggerLivingWorldRuntimeService.ResolveRest(profile, new(false, 2, 2));

        Assert.False(optional.Available);
        Assert.True(optional.Allowed);
        Assert.Equal(TriggerLivingWorldRuntimeService.RestDangerCode, danger.DiagnosticCode);
        Assert.Equal("rest.denied", danger.MessageKey);
        Assert.Equal(TriggerLivingWorldRuntimeService.RestPartyCode, incomplete.DiagnosticCode);
        Assert.True(allowed.Allowed);
        Assert.True(allowed.CampSetupAllowed);
        Assert.Equal("rest.allowed", allowed.MessageKey);
    }

    [Fact]
    public void Ux2LwE_ClueUsesRealSkillOutcomeAndDeterministicFalseTrace()
    {
        var profile = Profile();

        var unavailable = TriggerLivingWorldRuntimeService.ResolveClue(profile, new(null, true));
        var falseTrace = TriggerLivingWorldRuntimeService.ResolveClue(profile, new(7, true));
        var passed = TriggerLivingWorldRuntimeService.ResolveClue(profile, new(12, true));

        Assert.Equal(TriggerLivingWorldRuntimeService.ClueSkillUnavailableCode, unavailable.DiagnosticCode);
        Assert.False(unavailable.Revealed);
        Assert.True(falseTrace.Revealed);
        Assert.True(falseTrace.FalseClue);
        Assert.Equal("trail.decoy", falseTrace.TraceId);
        Assert.False(falseTrace.SkillCheckPassed);
        Assert.True(passed.SkillCheckPassed);
        Assert.Equal("trail.ash", passed.TraceId);
        Assert.Equal(profile.Clue!.QuestId, passed.JournalQuestId);
        Assert.Equal(45, passed.DirectionDegrees);
        Assert.Equal(2.5, passed.AgeHours);
    }

    [Fact]
    public void Ux2LwE_NarrativeEnforcesAuthorityParticipantsVoteOnceAndCooldown()
    {
        var profile = Profile();
        var now = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var participants = new HashSet<BlueprintId> { CompanionBlueprint };

        var noAuthority = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            TriggerNarrativeRuntimeState.Empty, new(now, false, true, participants));
        var missingParticipant = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            TriggerNarrativeRuntimeState.Empty, new(now, true, true, new HashSet<BlueprintId>()));
        var pendingVote = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            TriggerNarrativeRuntimeState.Empty, new(now, true, false, participants));
        var fired = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            TriggerNarrativeRuntimeState.Empty, new(now, true, true, participants));
        var repeated = TriggerLivingWorldRuntimeService.ResolveNarrative(profile,
            fired.State, new(now.AddMinutes(2), true, true, participants));

        Assert.Equal(TriggerLivingWorldRuntimeService.NarrativeAuthorityCode, noAuthority.DiagnosticCode);
        Assert.Equal(TriggerLivingWorldRuntimeService.NarrativeParticipantsCode,
            missingParticipant.DiagnosticCode);
        Assert.Equal(TriggerLivingWorldRuntimeService.NarrativeVoteCode, pendingVote.DiagnosticCode);
        Assert.True(fired.Fired);
        Assert.Equal(profile.Narrative!.DialogueId, fired.DialogueId);
        Assert.Equal(TriggerLivingWorldRuntimeService.NarrativeOnceCode, repeated.DiagnosticCode);
    }

    [Fact]
    public void Ux2LwE_ContainmentUsesTransformPolygonAndHoleWithoutEngineNodes()
    {
        var profile = Profile();

        var inside = TriggerLivingWorldRuntimeService.Contains(profile, new(12, 1, 10));
        var hole = TriggerLivingWorldRuntimeService.Contains(profile, new(10, 1, 10));
        var outside = TriggerLivingWorldRuntimeService.Contains(profile, new(15, 1, 10));
        var convex = TriggerLivingWorldRuntimeService.Contains(profile with
        {
            Foundation = profile.Foundation! with
            {
                Shape = profile.Foundation.Shape with
                {
                    Kind = TriggerWorldProfile.ShapeKind.ConvexHull3D,
                },
            },
        }, new(10, 1, 10));

        Assert.True(inside.Supported);
        Assert.True(inside.Contains);
        Assert.False(hole.Contains);
        Assert.False(outside.Contains);
        Assert.False(convex.Supported);
        Assert.Equal(TriggerLivingWorldRuntimeService.ConvexHullUnsupportedCode, convex.DiagnosticCode);
    }

    private static TriggerWorldProfile Profile()
    {
        var profileId = Guid.Parse("e0140000-0000-4000-8000-000000000010");
        return new TriggerWorldProfile(
            profileId,
            1,
            "test:ux2-lw-e",
            TriggerBlueprint,
            AreaId.From(Guid.Parse("e0140000-0000-4000-8000-000000000011")),
            null,
            null,
            null,
            "trigger.living-world",
            null,
            new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
            new("tracking", 10, "trail.ash", 45, 2.5, "trail.decoy",
                Guid.Parse("e0140000-0000-4000-8000-000000000012")),
            new([CompanionBlueprint], 8, true, 60, "party-vote",
                Guid.Parse("e0140000-0000-4000-8000-000000000013")),
            null,
            Foundation(),
            true);
    }

    private static TriggerWorldProfile.FoundationPolicy Foundation() => new(
        Guid.Parse("e0140000-0000-4000-8000-000000000020"),
        1,
        "test:ux2-lw-e",
        "living-world-volume",
        new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
            [new(-3, 0, -3), new(3, 0, -3), new(3, 0, 3), new(-3, 0, 3)],
            [[new(-.5f, 0, -.5f), new(.5f, 0, -.5f), new(.5f, 0, .5f), new(-.5f, 0, .5f)]],
            new(6, 3, 6),
            3,
            .25,
            true),
        new(["creature"], ["player"], [], ["characters"]),
        [],
        0,
        TriggerWorldProfile.FirePolicy.Repeat,
        [],
        null,
        new(new(10, 0, 10), Vector3Value.Zero, Vector3Value.One, null),
        new Dictionary<string, string>(),
        new(Guid.Parse("e0140000-0000-4000-8000-000000000021"), 1, "test:ux2-lw-e", "1.0.0",
            "VolumeEntered", null, null),
        new(Guid.Parse("e0140000-0000-4000-8000-000000000022"), 1, "test:ux2-lw-e", "1.0.0",
            "VolumeExited", null, null));
}
