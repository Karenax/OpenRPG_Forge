using Forge.Domain.Audio;
using Forge.Domain.Spatial;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class AudioEmitterAgentServiceTests
{
    [Fact]
    public void Supports_filtered_read_dry_run_apply_persistence_and_revert()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetAudioEmitters();
        Assert.Empty(current.Catalog.Definitions);
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000301");
        var catalog = Catalog(definitionId);

        var planned = workspace.Project.PlanAudioEmitters(new AudioEmitterPlanRequest(
            1, current.Revision, current.AreaId, catalog));
        Assert.True(planned.Succeeded);
        Assert.Contains("environment", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());

        var applied = workspace.Project.ApplyAudioEmitterPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        Assert.Equal("Campfire", workspace.Project.GetAudioEmitters(search: "fire").Catalog.Definitions.Single().Name);
        Assert.Equal("Campfire", workspace.Project.GetAudioEmitters(search: "crépitement").Catalog.Definitions.Single().Name);
        Assert.Single(workspace.Project.GetAudioEmitters(emitterId: definitionId.ToString()).Catalog.Definitions);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Equal(definitionId, persisted.Areas.Single().Environment.Audio.Emitters.Definitions.Single().Id);

        var reverted = workspace.Project.RevertAudioEmitterPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetAudioEmitters().Catalog.Definitions);
        Assert.Equal(["planned", "apply", "revert"], reverted.Audit.Select(item => item.Action));
    }

    [Fact]
    public void Dry_run_rejects_unlicensed_audio_without_changing_revision()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetAudioEmitters();
        var definition = Catalog(Guid.NewGuid()).Definitions.Single();
        var invalid = new AudioEmitterCatalog(1,
            [definition with { LicenseSpdx = "UNLICENSED", ContentSha256 = "bad" }], []);

        var result = workspace.Project.PlanAudioEmitters(new AudioEmitterPlanRequest(
            1, current.Revision, current.AreaId, invalid));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == AudioEmitterCatalogValidator.DefinitionInvalidCode);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    private static AudioEmitterCatalog Catalog(Guid definitionId) => new(1,
    [
        new(definitionId, 1, "Campfire",
            new AudioClipSet(Guid.NewGuid(), 1, "Fire", AudioClipSelectionMode.Ordered,
                [new(Guid.NewGuid(), "audio/campfire.ogg", 1, ["fire", "ambient"], "small", "fr", -19,
                    string.Empty, "audio/ogg", 128, "CC0-1.0", "mcp-test", new string('a', 64),
                    "crépitement du feu", [.1f, .6f, .2f], 12)],
                "mcp-test"),
            new(AudioLoopMode.Loop, 0, 0, 0, .1f, string.Empty),
            new(1, 22, "inverse-square", 180, -12, .2f, .5f, true), null, 0,
            string.Empty, string.Empty, true, "mcp-test", "assets/manifest.json", "CC0-1.0",
            new string('b', 64)),
    ],
    [new(Guid.NewGuid(), definitionId,
        new TransformValue(new Vector3Value(3, 1, -2), QuaternionValue.Identity, Vector3Value.One), true,
        "mcp-test")]);
}
