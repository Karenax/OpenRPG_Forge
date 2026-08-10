using Forge.Application.Scripting;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Scripting;

namespace Forge.Application.Tests.Scripting;

public sealed class ForgeScriptWorldCommandServiceTests
{
    [Fact]
    public void Native_intents_preview_apply_undo_and_redo_the_canonical_world_atomically()
    {
        var heroBlueprint = BlueprintId.From(Guid.Parse("13000000-0000-4000-8000-000000000001"));
        var guardBlueprint = BlueprintId.From(Guid.Parse("13000000-0000-4000-8000-000000000002"));
        var hero = new EntityInstance(EntityId.From(Guid.Parse("13000000-0000-4000-8000-000000000010")),
            heroBlueprint, TransformValue.Identity, ["hero"]);
        var area = new AreaDefinition(AreaId.From(Guid.Parse("13000000-0000-4000-8000-000000000020")),
            "crypt", "Crypt", [hero]);
        var service = new ForgeScriptWorldCommandService([area],
            new Dictionary<string, BlueprintId>(StringComparer.Ordinal) { ["ash-guard"] = guardBlueprint });
        var runtime = new NativeForgeScriptRuntime();
        var compilation = runtime.Compile("""
            void main()
            {
                AreaRef area = current_area();
                EntityRef hero = find_entity(area, "hero");
                Vec3 goal = vec3(6, 0, 4);
                MovementTaskRef task = move_to(hero, goal, "run");
                set_velocity(hero, vec3(0, 0, 3));
                teleport(hero, area, goal);
                EntityRef guard = spawn_at(area, "ash-guard", vec3(2, 0, 1));
                destroy_entity(guard);
                cancel_movement(task);
            }
            """);
        var execution = runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));
        var command = new ForgeScriptWorldIntentCommand(
            Guid.Parse("13000000-0000-4000-8000-000000000030"), 0, "test:r013", execution.Events, true);

        var preview = service.Preview(command);
        var applied = service.Apply(command);

        Assert.True(preview.Succeeded);
        Assert.Equal(0, service.Undo().Revision);
        var redone = service.Redo();
        Assert.True(applied.Succeeded);
        Assert.True(redone.Succeeded);
        Assert.Equal(1, redone.Revision);
        var restoredHero = Assert.Single(redone.State.Areas).Entities.Single();
        Assert.Equal(new Vector3Value(6, 0, 4), restoredHero.Transform.Position);
        Assert.Equal(new Vector3Value(0, 0, 3), redone.State.Velocities[hero.Id]);
        Assert.Equal(ScriptMovementTaskStatus.Cancelled, Assert.Single(redone.State.MovementTasks).Value.Status);
        Assert.DoesNotContain(redone.State.Areas.SelectMany(item => item.Entities),
            item => item.BlueprintId == guardBlueprint);
    }

    [Fact]
    public void Invalid_required_intent_and_stale_revision_leave_the_world_unchanged()
    {
        var blueprint = BlueprintId.From(Guid.NewGuid());
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "area", "Area", []);
        var service = new ForgeScriptWorldCommandService([area],
            new Dictionary<string, BlueprintId> { ["known"] = blueprint });
        var invalid = new ForgeScriptWorldIntentCommand(Guid.NewGuid(), 0, "test:r013",
            [new("world.raw_engine_pointer", "1")], true);

        var rejected = service.Apply(invalid);
        var stale = service.Apply(invalid with { Events = [], ExpectedRevision = 1 });

        Assert.False(rejected.Succeeded);
        Assert.Equal("script.world.intent_invalid", Assert.Single(rejected.Diagnostics).Code);
        Assert.False(stale.Succeeded);
        Assert.Equal("script.world.revision_conflict", Assert.Single(stale.Diagnostics).Code);
        Assert.Equal(0, service.State.Revision);
        Assert.False(service.CanUndo);
    }
}
