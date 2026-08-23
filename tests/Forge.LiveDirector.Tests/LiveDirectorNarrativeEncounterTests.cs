using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Encounters;
using Forge.LiveDirector.Narrative;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorNarrativeEncounterTests
{
    private static readonly Guid Actor = Guid.Parse("fa000000-0000-4000-8000-000000000001");
    private static readonly Guid SubjectId = Guid.Parse("fa000000-0000-4000-8000-000000000002");
    private static AuthorizationSubject Gm => new(Actor, NetworkRole.Administrator, new HashSet<Guid>());

    [Fact]
    public void Quest_transition_previews_consequences_validates_scope_and_updates_selected_players()
    {
        var audit = Journal();
        var service = new LiveQuestService(new(), audit);
        service.Upsert(new(SubjectId, "Cendres", "started", new Dictionary<string, IReadOnlySet<string>> { ["started"] = new HashSet<string> { "completed" } }));
        var player = Guid.NewGuid();
        var request = new LiveQuestService.Request(Guid.NewGuid(), SubjectId, "completed", "selected", [player], "GM resolution", 0);
        var preview = service.Transition(Gm, request, false);
        Assert.Contains("scripts:authoritative", preview.Consequences);
        Assert.StartsWith("rpg-kit:quest-journal:", preview.RpgProjection);
        var applied = service.Transition(Gm, request, true);
        Assert.True(applied.Applied);
        Assert.Equal("completed", service.Inspect(SubjectId)!.State);
        Assert.Equal("completed", service.InspectPlayerState(SubjectId, player));
        var rollback = service.Rollback(Gm, Guid.NewGuid(), "sandbox ended",
            applied.Revision, true);
        Assert.True(rollback.Restored);
        Assert.Equal("started", service.Inspect(SubjectId)!.State);
        Assert.True(audit.Verify());
    }

    [Fact]
    public void Encounter_supports_inspect_activate_pause_wave_scale_and_despawn()
    {
        var audit = Journal();
        var service = new LiveEncounterService(new(), audit);
        service.Upsert(new(SubjectId, "Gardien", "idle", 0, 1m, 4));
        Assert.Equal("LIVE_ENCOUNTER_INSPECTED", service.Execute(Gm, Request("inspect", null, 0), true).Code);
        Assert.True(service.Execute(Gm, Request("activate", null, 0), true).Applied);
        Assert.Equal(2, service.Execute(Gm, Request("wave", "2", 1), true).After!.Wave);
        Assert.Equal(1.5m, service.Execute(Gm, Request("scale", "1.5", 2), true).After!.Scale);
        Assert.Equal("paused", service.Execute(Gm, Request("pause", null, 3), true).After!.State);
        var despawn = service.Execute(Gm, Request("despawn", null, 4), true);
        Assert.Equal(0, despawn.After!.ActiveCreatures);
        Assert.Equal(["spawn-authority", "ai-runtime"], despawn.RuntimeAdapters);
        var revision = despawn.Revision;
        for (var index = 0; index < 5; index++)
        {
            var rollback = service.Rollback(Gm, Guid.NewGuid(), "sandbox ended", revision,
                true);
            Assert.True(rollback.Restored);
            revision = rollback.Revision;
        }
        Assert.Equal("idle", service.Inspect(SubjectId)!.State);
        Assert.Equal(0, service.HistoryCount);
        Assert.True(audit.Verify());
    }

    private static LiveEncounterService.Request Request(string action, string? value, long revision) => new(Guid.NewGuid(), SubjectId, action, value, "live direction", revision);
    private static LiveAuditJournal Journal() => new(() => DateTimeOffset.UnixEpoch);
}
