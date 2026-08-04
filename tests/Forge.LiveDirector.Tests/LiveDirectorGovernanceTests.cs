using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Chat;
using Forge.LiveDirector.Scenes;
using Forge.LiveDirector.State;
using Forge.LiveDirector.Visibility;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorGovernanceTests
{
    private static readonly Guid Actor = Guid.Parse("f0000000-0000-4000-8000-000000000001");
    private static readonly Guid Target = Guid.Parse("f0000000-0000-4000-8000-000000000002");
    private static AuthorizationSubject Gm => new(Actor, NetworkRole.Administrator, new HashSet<Guid>());

    [Fact]
    public void Typed_variables_preview_validate_audit_and_rollback()
    {
        var audit = Journal();
        var variables = new LiveVariableService(new(), audit);
        variables.Define(new("difficulty", "enum", null, null, new HashSet<string> { "calm", "hard" }));
        var command = new LiveVariableService.Request(Guid.NewGuid(), Target, "difficulty", "hard", "raise tension", 0);
        Assert.Equal("LIVE_VARIABLE_PREVIEW", variables.Set(Gm, command, false).Code);
        Assert.True(variables.Set(Gm, command, true).Applied);
        Assert.Equal("LIVE_VARIABLE_ROLLED_BACK", variables.Rollback(Gm, "scene ended", Guid.NewGuid()));
        Assert.Equal(2, audit.Entries.Count);
        Assert.True(audit.Verify());
    }

    [Fact]
    public void Prepared_scene_requires_rollback_plan_and_previews_all_facets()
    {
        var audit = Journal();
        var scenes = new LiveSceneService(new(), audit);
        var sceneId = Guid.NewGuid();
        scenes.Save(new(sceneId, "Tempête", ["wolf", "wolf"], "warning", "storm", "rain", ["gate-enter"], ["despawn", "restore-weather"]));
        var command = Guid.NewGuid();
        Assert.Contains("weather=rain", scenes.Run(Gm, sceneId, command, "dramatic beat", 0, false).Diff);
        Assert.True(scenes.Run(Gm, sceneId, command, "dramatic beat", 0, true).Applied);
        Assert.True(audit.Verify());
    }

    [Fact]
    public void Visibility_supports_omniscient_and_selected_player_overlays()
    {
        var service = new LiveVisibilityService(new());
        var view = service.Configure(Gm, "omniscient", null, true, true, true, true);
        Assert.True(view.Secrets);
        Assert.Throws<ArgumentException>(() => service.Configure(Gm, "selected-player", null, false, false, false, false));
    }

    [Fact]
    public void Chat_supports_gm_faction_and_impersonation_channels_with_immutable_audit()
    {
        var audit = Journal();
        var chat = new LiveDirectorChatService(new(), audit, () => DateTimeOffset.UnixEpoch);
        Assert.Equal("gm", chat.Send(Gm, "gm", null, "Briefing", null, Guid.NewGuid()).Channel);
        Assert.Equal(Target, chat.Send(Gm, "impersonation", null, "Approchez", Target, Guid.NewGuid()).ImpersonatedObjectId);
        Assert.Equal(2, chat.Messages.Count);
        Assert.True(audit.Verify());
    }

    private static LiveAuditJournal Journal() => new(() => new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero));
}
