using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class ItemAdvancedWorkflowTests
{
    [Fact]
    public void Command_supports_dry_run_apply_and_typed_script_access()
    {
        var (state, item) = CreateState();
        var profile = Profile();
        var handler = new SetItemAdvancedProfileHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetItemAdvancedProfileCommand(CommandId.From(Guid.NewGuid()), state.Revision, item.Id,
            profile, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var updated = applied.State.Project.Blueprints.Single(candidate => candidate.Id == item.Id);
        var script = new ItemAdvancedScriptService(updated);
        var snapshot = script.Read(script.Handle);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.Equal("add", preview.Patch!.Operation);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        Assert.True(snapshot.IsWeapon);
        Assert.True(snapshot.IsArmor);
        Assert.True(snapshot.IsContainer);
        Assert.True(snapshot.IsProcedural);
        Assert.True(script.CanUse(script.Handle, Context()).Allowed);
    }

    [Fact]
    public void Command_rejects_stale_wrong_kind_read_only_and_invalid_profiles()
    {
        var (state, item) = CreateState();
        var handler = new SetItemAdvancedProfileHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var profile = Profile();
        var stale = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(), item.Id,
            profile, false));
        var wrongKind = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            state.Project.Blueprints.Single(candidate => candidate.Kind == BlueprintKind.Placeable).Id, profile, false));
        var invalid = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, item.Id,
            profile with { SchemaVersion = 99 }, false));

        Assert.Contains(stale.Diagnostics, candidate => candidate.Code == "command.revision_conflict");
        Assert.Contains(wrongKind.Diagnostics, candidate => candidate.Code == "item.blueprint_kind_invalid");
        Assert.Contains(invalid.Diagnostics, candidate => candidate.Code == "item.advanced.identity_invalid");
    }

    [Fact]
    public void Project_validation_rejects_duplicate_profile_identity()
    {
        var (state, item) = CreateState();
        var profile = Profile();
        var second = new BlueprintDefinition(BlueprintId.From(Guid.Parse("10300000-0000-4000-8000-000000000009")),
            "second-item", "Second item", BlueprintKind.Item, itemAdvanced: profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            [item.WithItemAdvanced(profile), second, .. state.Project.Blueprints.Where(candidate => candidate.Id != item.Id)],
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);

        Assert.Contains(Forge.Domain.Validation.ProjectValidator.Validate(project),
            candidate => candidate.Code == "item.advanced.uuid_duplicate");
    }

    private static (ProjectState State, BlueprintDefinition Item) CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("10300000-0000-4000-8000-000000000001")),
            "item-area", "Item area", []);
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("10300000-0000-4000-8000-000000000002")),
            "ember-blade", "Ember blade", BlueprintKind.Item);
        var byproduct = new BlueprintDefinition(BlueprintId.From(Guid.Parse("10300000-0000-4000-8000-000000000003")),
            "ash-dust", "Ash dust", BlueprintKind.Item);
        var placeable = new BlueprintDefinition(BlueprintId.From(Guid.Parse("10300000-0000-4000-8000-000000000004")),
            "anvil", "Anvil", BlueprintKind.Placeable);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("10300000-0000-4000-8000-000000000005")),
            "Item project", [item, byproduct, placeable], [area]);
        return (new ProjectState(project, new ProjectRevision(12)), item);
    }

    private static ItemAdvancedProfile Profile()
    {
        var profile = ItemAdvancedProfile.CreateDefault(
            Guid.Parse("10200000-0000-4000-8000-000000000001"), "test:g13-k-m");
        return profile with
        {
            Economy = new("gold", 100, 1, 1, 0, 1, new Dictionary<string, decimal>()),
            Requirements = new(1, 0, ["blades"], [], [], null),
            Weapon = new("longsword", 1, false,
                [new("slash", "physical", 4, 8, 1.8, "attack.slash", ["blade"])], ["martial"]),
            Armor = new(["torso"], 30, 2, 1, "steel", new Dictionary<string, double>(), ["broken"]),
            Container = new(8, 40, 3, 0.8, ["loot"], ["inventory.open"], true, true),
            Recipes = [new(Guid.Parse("10200000-0000-4000-8000-000000000005"), "forge-ember-blade",
                [new("tag:ember-metal", 2, true, 1)], "forge", "smithing", 10, 1, 5, ["ash-dust"],
                "test:recipe")],
            Generator = new(true, 3, 1, 1, ["weapon"], new Dictionary<string, int> { ["common"] = 1 },
                [new(Guid.Parse("10200000-0000-4000-8000-000000000006"), "keen", "weapon", 2, 1,
                    "common", [], ["critical"], "test:affix")], 2)
        };
    }

    private static ItemRuntimeContext Context() => new(3, 0, new HashSet<string>(["blades"]),
        new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), "default", "gold", 1, 100, 5,
        "player-1", true);
}
