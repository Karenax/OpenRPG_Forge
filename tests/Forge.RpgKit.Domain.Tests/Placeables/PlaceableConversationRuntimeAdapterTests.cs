using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableConversationRuntimeAdapterTests
{
    private static readonly Guid DialogueGuid = Guid.Parse("c2110000-0000-4000-8000-000000000001");
    private static readonly BlueprintDefinition PlaceableBlueprint = new(
        BlueprintId.From(Guid.Parse("c2110000-0000-4000-8000-000000000002")),
        "autel-parlant",
        "Autel parlant",
        BlueprintKind.Placeable);
    private static readonly BlueprintDefinition ItemBlueprint = new(
        BlueprintId.From(Guid.Parse("c2110000-0000-4000-8000-000000000003")),
        "sceau-des-cendres",
        "Sceau des cendres",
        BlueprintKind.Item);

    [Fact]
    public void Runtime_projects_all_five_binding_sources_in_priority_order()
    {
        var placeable = Placeable();
        var adapter = new PlaceableConversationRuntimeAdapter();
        var resolution = adapter.Resolve(placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]);
        var runtime = adapter.Build(resolution.Handle!, placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]);

        Assert.True(resolution.Available);
        Assert.Equal(5, runtime.Bindings.Count);
        Assert.Equal(50, runtime.Bindings[0].Priority);
        Assert.Equal(
            Enum.GetValues<PlaceableConversationTrigger>().Order(),
            runtime.Bindings.Select(binding => binding.Trigger).Order());
    }

    [Fact]
    public void Interaction_state_user_item_and_event_contexts_select_the_expected_dialogue()
    {
        var placeable = Placeable();
        var adapter = new PlaceableConversationRuntimeAdapter();
        var handle = adapter.Resolve(placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]).Handle!;
        var cases = new[]
        {
            Context(PlaceableConversationTrigger.Interaction, "examiner"),
            Context(PlaceableConversationTrigger.State, "broken"),
            Context(PlaceableConversationTrigger.User, "tag:initié"),
            Context(PlaceableConversationTrigger.Item, string.Empty, ItemBlueprint.Id),
            Context(PlaceableConversationTrigger.Event, "OnDisturbed"),
        };

        foreach (var context in cases)
        {
            var decision = adapter.Evaluate(handle, placeable, context);
            Assert.True(decision.Matched);
            Assert.Equal(DialogueGuid, decision.DialogueId);
            Assert.Equal("RPG_PLACEABLE_CONVERSATION_MATCHED", decision.Code);
        }
    }

    [Fact]
    public void Higher_priority_wins_and_once_per_user_binding_is_not_replayed()
    {
        var placeable = Placeable();
        var adapter = new PlaceableConversationRuntimeAdapter();
        var handle = adapter.Resolve(placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]).Handle!;
        var interaction = placeable.Conversation!.Bindings.Single(binding =>
            binding.Trigger == PlaceableConversationTrigger.Interaction);
        var duplicate = interaction with
        {
            Id = Guid.Parse("c2110000-0000-4000-8000-000000000020"),
            Priority = interaction.Priority - 1,
            OncePerUser = false,
        };
        placeable = placeable with
        {
            Conversation = placeable.Conversation with { Bindings = [interaction, duplicate] },
        };
        handle = adapter.Resolve(placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]).Handle!;

        var first = adapter.Evaluate(handle, placeable,
            Context(PlaceableConversationTrigger.Interaction, "examiner"));
        var replay = adapter.Evaluate(handle, placeable,
            Context(PlaceableConversationTrigger.Interaction, "examiner",
                completed: new HashSet<Guid> { interaction.Id }));
        var exhausted = adapter.Evaluate(handle, placeable with
        {
            Conversation = placeable.Conversation with { Bindings = [interaction] },
        }, Context(PlaceableConversationTrigger.Interaction, "examiner",
            completed: new HashSet<Guid> { interaction.Id }));

        Assert.Equal(interaction.Id, first.BindingId);
        Assert.Equal(duplicate.Id, replay.BindingId);
        Assert.Equal("RPG_PLACEABLE_CONVERSATION_ALREADY_USED", exhausted.Code);
    }

    [Fact]
    public void Optional_required_unknown_item_and_stale_profiles_fail_deterministically()
    {
        var placeable = Placeable();
        var adapter = new PlaceableConversationRuntimeAdapter();
        var optional = adapter.Resolve(placeable with
        {
            Conversation = null,
            ConversationRequiredAtRuntime = false,
        }, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]);
        var required = adapter.Resolve(placeable with
        {
            Conversation = null,
            ConversationRequiredAtRuntime = true,
        }, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]);
        var itemBinding = placeable.Conversation!.Bindings.Single(binding =>
            binding.Trigger == PlaceableConversationTrigger.Item);
        var unknownItem = adapter.Resolve(placeable with
        {
            Conversation = placeable.Conversation with
            {
                Bindings = [itemBinding with
                {
                    ItemBlueprintId = BlueprintId.From(Guid.Parse("c2110000-0000-4000-8000-000000000099")),
                }],
            },
        }, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]);
        var handle = adapter.Resolve(placeable, [Dialogue()], [PlaceableBlueprint, ItemBlueprint]).Handle!;

        Assert.True(optional.OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_CONVERSATION_REQUIRED_MISSING", required.DiagnosticCode);
        Assert.Equal("WORLD_PLACEABLE_CONVERSATION_ITEM_MISSING", unknownItem.DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { ConversationId = Guid.NewGuid() },
            placeable,
            [Dialogue()],
            [PlaceableBlueprint, ItemBlueprint]));
    }

    private static PlaceableConversationContext Context(
        PlaceableConversationTrigger trigger,
        string selector,
        BlueprintId? item = null,
        IReadOnlySet<Guid>? completed = null) =>
        new(trigger, selector, item, completed ?? new HashSet<Guid>());

    private static PlaceableWorldProfile Placeable()
    {
        var bindingIds = Enumerable.Range(10, 5)
            .Select(index => Guid.Parse($"c2110000-0000-4000-8000-{index:000000000000}"))
            .ToArray();
        return new(
            Guid.Parse("c2110000-0000-4000-8000-000000000004"),
            1,
            "test:ux1-o",
            PlaceableBlueprint.Id,
            null,
            DialogueGuid,
            null,
            new([], new(false, 1, 1, true, "placeable")),
            null,
            Conversation: new(
                Guid.Parse("c2110000-0000-4000-8000-000000000005"),
                PlaceableConversationProfile.CurrentSchemaVersion,
                "test:ux1-o",
                [
                    new(bindingIds[0], DialogueGuid, PlaceableConversationTrigger.Interaction,
                        "examiner", null, "default", 50, true),
                    new(bindingIds[1], DialogueGuid, PlaceableConversationTrigger.State,
                        "broken", null, "warning", 40, false),
                    new(bindingIds[2], DialogueGuid, PlaceableConversationTrigger.User,
                        "tag:initié", null, "default", 30, false),
                    new(bindingIds[3], DialogueGuid, PlaceableConversationTrigger.Item,
                        string.Empty, ItemBlueprint.Id, "warning", 20, false),
                    new(bindingIds[4], DialogueGuid, PlaceableConversationTrigger.Event,
                        "OnDisturbed", null, "default", 10, false),
                ]),
            ConversationRequiredAtRuntime: true);
    }

    private static DialogueDefinition Dialogue()
    {
        var nodeId = DialogueNodeId.From(Guid.Parse("c2110000-0000-4000-8000-000000000030"));
        return new(
            DialogueId.From(DialogueGuid),
            "voix-de-lautell",
            "Voix de l’autel",
            nodeId,
            [new(nodeId, null, new("dialogue.autel", "Qui ose approcher ?"))],
            provenance: "test:ux1-o",
            entryPoints:
            [
                new(Guid.Parse("c2110000-0000-4000-8000-000000000031"), 1, "default", nodeId, 0,
                    "test:ux1-o"),
                new(Guid.Parse("c2110000-0000-4000-8000-000000000032"), 1, "warning", nodeId, 1,
                    "test:ux1-o"),
            ]);
    }
}
