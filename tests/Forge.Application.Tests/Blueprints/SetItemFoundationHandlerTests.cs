using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Items;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetItemFoundationHandlerTests
{
    private readonly SetItemFoundationHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalItemFoundation()
    {
        var state = State();
        var blueprint = Assert.Single(state.Project.Blueprints);
        var profile = Profile(blueprint.Slug);
        var command = new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), state.Revision, blueprint.Id,
            profile, false, true);

        var preview = _handler.Handle(state, command);
        var applied = _handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state); history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Null(preview.State.Project.Blueprints.Single().ItemFoundation);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().ItemFoundation!.Id);
        var script = ItemFoundationScriptService.Read(history.State.Project, blueprint.Id);
        Assert.Equal("ash-key", script!.Creation!.StackCompatibilityKey);
        Assert.Equal(10, script.Creation.StackMaximum);
        Assert.Equal(profile.Gameplay!.Id, script.Gameplay!.ProfileId);
        Assert.Equal(2, script.Gameplay.RequiredEquipmentSlots.Count);
        Assert.Equal(profile.Operations!.Id, script.Operations!.ProfileId);
        Assert.Equal(9, script.Operations.LifecycleEvents.Count);
        Assert.Null(history.Undo().State.Project.Blueprints.Single().ItemFoundation);
        Assert.Equal(profile.Id, history.Redo().State.Project.Blueprints.Single().ItemFoundation!.Id);
        Assert.Contains("type ritual-key", preview.Patch!.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAndLossyTypeMigrationAreRejectedWithoutMutation()
    {
        var state = State();
        var blueprint = Assert.Single(state.Project.Blueprints);
        var profile = Profile(blueprint.Slug);
        var applied = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, blueprint.Id,
            profile, false, false));
        var current = applied.State;
        var target = new ItemTypeDefinition(Guid.NewGuid(), 1, "test:r044", "quest-relic", ["belt"],
            ["ward"], "rig.relic");
        var changed = profile with { TypeId = target.Id, Type = target, AppearanceRig = target.RequiredRig };

        var rejected = _handler.Handle(current, new(CommandId.From(Guid.NewGuid()), current.Revision, blueprint.Id,
            changed, false, false));
        var accepted = _handler.Handle(current, new(CommandId.From(Guid.NewGuid()), current.Revision, blueprint.Id,
            ItemFoundationValidator.MigrateType(profile, target, true), true, true));
        var invalid = _handler.Handle(current, new(CommandId.From(Guid.NewGuid()), current.Revision, blueprint.Id,
            profile with { ContentSha256 = "invalid" }, false, false));

        Assert.Equal("item.foundation.type_migration_preview_required", Assert.Single(rejected.Diagnostics).Code);
        Assert.Equal(current.Revision, rejected.State.Revision);
        Assert.True(accepted.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == "item.foundation.content_governance_invalid");
        Assert.Equal(current.Revision, invalid.State.Revision);
    }

    private static ProjectState State()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("c2440000-0000-4000-8000-000000000090")),
            "item-lab", "Item lab", []);
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("c2440000-0000-4000-8000-000000000091")),
            "ash-key", "Clé des cendres", BlueprintKind.Item);
        return new(new ProjectDefinition(ProjectId.From(Guid.Parse("c2440000-0000-4000-8000-000000000092")),
            "Item project", [item], [area]), new ProjectRevision(0));
    }

    private static ItemFoundationProfile Profile(string slug)
    {
        var typeId = Guid.Parse("c2440000-0000-4000-8000-000000000093");
        var foundation = new ItemFoundationProfile(Guid.Parse("c2440000-0000-4000-8000-000000000094"), 1, "test:r044", true,
            "crypt-of-ashes", "CC0-1.0", new string('b', 64), typeId,
            new(typeId, 1, "test:r044", "ritual-key", ["hand", "inventory"], ["utility"], "rig.hand"),
            slug, "asset.items.ash-key", "rig.hand", 1, 1, 1, 25, ["hand"],
            [new(Guid.Parse("c2440000-0000-4000-8000-000000000095"), "utility", 1,
                "effect.ash-key", "OnUse")],
            [new("fr", "Clé des cendres", "Une clé.", "Une clé de la crypte.", "Objet inconnu", "Clé", "")],
            ["identified", "durability"],
            new(Guid.Parse("c2880000-0000-4000-8000-000000000021"), 1, "test:r088", true,
                "crypt-of-ashes", "CC0-1.0", new string('8', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed"]),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000022"), 1, "test:r088",
                    ["asset.items.ash-key.mesh"], ["asset.materials.iron"], ["hand-r"],
                    "asset.items.ash-key.icon", ["asset.vfx.ash"], "asset.items.ash-key.equipped"),
                new(1.25, .4, 1, 2, .5, .25), new(10, "ash-key", true),
                new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")));
        return foundation with { Gameplay = Gameplay(), Operations = Operations() };
    }

    private static ItemGameplayProfile Gameplay() => new(
        Guid.Parse("c2890000-0000-4000-8000-000000000021"), 1, "test:r089", true,
        "crypt-of-ashes", "CC0-1.0", new string('9', 64), new(true, true, true, true, true, true, true),
        new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
            item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
            ["item.transfer.quest"]),
        [new(Guid.Parse("c2890000-0000-4000-8000-000000000022"), 1, "test:r089", "ward",
            new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 ward",
            "BeforeItemModifier", "AfterItemModifier")],
        new(Guid.Parse("c2890000-0000-4000-8000-000000000023"), "point",
            new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
            "ItemAbilityRequested", "ItemAbilityResolved"),
        new([new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
            new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
            new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"])], ["hand-r", "hand-l"]));

    private static ItemOperationsProfile Operations() => new(Guid.NewGuid(), 1, "test:r090", true,
        "crypt-of-ashes", "CC0-1.0", new string('7', 64),
        new(Guid.NewGuid(), 1, "test:r090",
        [new(Guid.NewGuid(), "relic", 1, 1, 1, 2, "crypt-open", true)]),
        new(Guid.NewGuid(), 1, "test:r090", 0,
        [new(Guid.NewGuid(), "backpack", null, 0, 0, 1, ["quest"], "asset.items.ash-key.preview")],
            true, true, true),
        new("1.0", Enum.GetValues<ItemLifecycleEventType>().Select(kind =>
            new ItemLifecycleBindingPolicy(Guid.NewGuid(), 1, "test:r090", kind, $"On{kind}", "server")).ToArray()));
}
