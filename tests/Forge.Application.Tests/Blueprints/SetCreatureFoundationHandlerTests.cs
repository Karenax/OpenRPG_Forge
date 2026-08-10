using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreatureFoundationHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1610000-0000-4000-8000-000000000001"));
    private readonly SetCreatureFoundationHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoRedoAndScriptReadUseOneCanonicalMutation()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = Complete();
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);
        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.Contains("2 resource pools", preview.Patch!.Summary);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureFoundation!.Id);
        var scriptView = CreatureFoundationScriptService.Read(history.State.Project, CreatureId);
        Assert.Equal("Flying", scriptView!.MovementMode);
        Assert.Equal("OnSpawned", scriptView.SpawnHandler);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureFoundation!.Id);
    }

    [Fact]
    public void InvalidSlugIsRejectedBeforeDocumentMutation()
    {
        var state = CreateState();
        var source = Complete();
        var invalid = new Forge.Domain.Creatures.CreatureFoundationProfile(source.Id, source.SchemaVersion,
            source.Provenance, source.RequiredAtRuntime,
            source.Identity with { Slug = "another-creature" }, source.DefaultTransform, source.Resources,
            source.Movement, source.References, source.Spawned, source.Overrides);
        var result = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, CreatureId, invalid, false));
        Assert.Equal("creature.foundation.slug_mismatch", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(state.Revision, result.State.Revision);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1610000-0000-4000-8000-000000000002")),
            "crypt", "Crypte", []);
        var blueprint = new BlueprintDefinition(CreatureId, "ash-warden", "Gardien des cendres", BlueprintKind.Creature);
        return new(new ProjectDefinition(ProjectId.From(Guid.Parse("a1610000-0000-4000-8000-000000000003")),
            "R038", [blueprint], [area]), new ProjectRevision(0));
    }

    internal static CreatureFoundationProfile Complete()
    {
        var profile = CreatureFoundationProfile.CreateDefault(
            Guid.Parse("a1600000-0000-4000-8000-000000000001"), "ash-warden", "Gardien des cendres", "author:r038");
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, true,
            profile.Identity, profile.DefaultTransform,
            [new(Guid.Parse("a1600000-0000-4000-8000-000000000002"), "health", 100, 84, .5, true),
             new(Guid.Parse("a1600000-0000-4000-8000-000000000003"), "stamina", 40, 40, 2, false)],
            new(CreatureFoundationMovementMode.Flying, 6, 12, 8, 0, 30, 50), profile.References,
            new(Guid.Parse("a1600000-0000-4000-8000-000000000004"), 1, "author:r038", "1.0",
                "creature.spawned", "OnSpawned", EventAuthorityScope.Server), profile.Overrides);
    }
}
