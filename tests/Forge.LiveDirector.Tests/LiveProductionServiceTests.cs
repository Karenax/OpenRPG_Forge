using Forge.Domain.Modules;
using Forge.Domain.RuntimeExtensions;
using Forge.LiveDirector.Production;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveProductionServiceTests
{
    private static readonly Guid Actor = Guid.Parse("43000000-0000-4000-8000-000000000001");
    private static readonly Guid Zone = Guid.Parse("43000000-0000-4000-8000-000000000002");
    private static readonly Guid Target = Guid.Parse("43000000-0000-4000-8000-000000000003");
    private static readonly Guid Recording = Guid.Parse("43000000-0000-4000-8000-000000000004");

    [Fact]
    public void Intervention_requires_permission_revision_reason_and_confirmation_then_applies()
    {
        var (service, chooser, gm) = Create();
        var request = new LiveProductionService.InterventionRequest(Guid.NewGuid(), "adjust-level", [Target],
            0, "story correction", LevelDelta: 2, Confirmed: true);

        var preview = service.Execute(gm, request, false);
        var applied = service.Execute(gm, request, true);
        var replay = service.Execute(gm, request, true);

        Assert.Equal("LIVE_PRODUCTION_PREVIEW", preview.Code);
        Assert.True(applied.Applied);
        Assert.Contains("level-adjusted:2", chooser.Find(Target)!.State, StringComparison.Ordinal);
        Assert.Equal("LIVE_PRODUCTION_REVISION_STALE", replay.Code);
        Assert.Contains("reason=story correction", applied.AuditEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void Recording_resolves_saved_group_and_redacts_private_data()
    {
        var (service, _, gm) = Create();
        var result = service.StartRecording(gm, 2,
            new Dictionary<string, IReadOnlyList<Guid>> { ["tag:companions"] = [Target] });

        Assert.True(result.Started);
        Assert.Equal([Target], result.Members);
        Assert.True(result.Redacted);
        Assert.Equal(Recording, result.RecordingId);
    }

    private static (LiveProductionService Service, LiveWorldChooser Chooser, AuthorizationSubject Gm) Create()
    {
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(Target, Zone, "creature", "Elyra", "companion", null, "active", []));
        var source = LiveDirectorProductionProfile.CreateDefault(ModuleId.From(Guid.Parse("43000000-0000-4000-8000-000000000010")).Value);
        var profile = new LiveDirectorProductionProfile(source.Id, 1,
            source.ForcedRest with { Enabled = true }, source.LevelAdjustment with { Enabled = true },
            [new LiveDirectorProductionProfile.SavedGroup(Guid.Parse("43000000-0000-4000-8000-000000000011"),
                "companions", "tag:companions", 2, 16)],
            source.AppearanceOverrides with { Enabled = true },
            source.SessionRecording with { Enabled = true }, "test");
        var actor = new AuthorizationSubject(Actor, NetworkRole.Administrator, new HashSet<Guid> { Zone });
        return (new LiveProductionService(chooser, authorization, profile,
            () => DateTimeOffset.Parse("2026-08-07T12:00:00Z"), () => Recording), chooser, actor);
    }
}
