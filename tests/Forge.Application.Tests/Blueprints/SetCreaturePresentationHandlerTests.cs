using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreaturePresentationHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1310000-0000-4000-8000-000000000001"));
    private readonly SetCreaturePresentationHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalPresentation()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = Profile();
        var command = new SetCreaturePresentationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Null(history.State.Project.Blueprints.Single().CreaturePresentation);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreaturePresentation!.Id);
        Assert.Contains("creaturePresentation", applied.Patch!.Path, StringComparison.Ordinal);

        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreaturePresentation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreaturePresentation!.Id);
    }

    [Fact]
    public void InvalidPresentationIsRejectedWithoutRevisionChange()
    {
        var state = CreateState();
        var invalid = CreaturePresentationProfile.CreateDefault(Guid.Empty, "");

        var result = _handler.Handle(state, new SetCreaturePresentationCommand(
            CommandId.From(Guid.NewGuid()), state.Revision, CreatureId, invalid, false));

        Assert.False(result.Succeeded);
        Assert.Equal(state.Revision, result.State.Revision);
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.presentation.uuid_required");
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.presentation.provenance_required");
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1310000-0000-4000-8000-000000000002")),
            "presentation-lab", "Presentation lab", []);
        var blueprint = new BlueprintDefinition(CreatureId, "prism-warden", "Gardien prismatique", BlueprintKind.Creature);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("a1310000-0000-4000-8000-000000000003")),
            "Presentation project", [blueprint], [area]), new ProjectRevision(0));
    }

    private static CreaturePresentationProfile Profile() => new(
        Guid.Parse("a1310000-0000-4000-8000-000000000004"), 1, "author:test", "woman", "humanoid-medium",
        new("elle", "elle", "son", "elle-même"), [], true, "rig.humanoid",
        [new("head", "asset.hood", "rig.humanoid", 1, [], "hide-covered")],
        [new("primary", "#EE8855")], [new("back", "tail", "asset.tail", "rig.humanoid")],
        new("voice.prism", "fr-FR", ["audio.prism"], ["calm"], null, ["a"], "CC0-1.0", "author-recorded"));
}
