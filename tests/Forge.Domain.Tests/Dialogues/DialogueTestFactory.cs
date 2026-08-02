using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

internal static class DialogueTestFactory
{
    internal static readonly DialogueNodeId EntryNodeId = DialogueNodeId.From(
        Guid.Parse("0250b999-f333-4b03-9140-642f36fcc739"));

    internal static readonly DialogueNodeId RewardNodeId = DialogueNodeId.From(
        Guid.Parse("4b8f083b-e51b-4212-b7b7-1f0eb4f73f8a"));

    internal static readonly DialogueChoiceId AcceptChoiceId = DialogueChoiceId.From(
        Guid.Parse("608dcd72-7edc-4f8a-af9d-ece16a17164a"));

    internal static readonly DialogueChoiceId FinishChoiceId = DialogueChoiceId.From(
        Guid.Parse("8fd5aa7c-602f-4b29-b3b7-c5339969bcf0"));

    internal static DialogueDefinition Create() => new(
        DialogueId.From(Guid.Parse("85693d84-26e7-4256-959a-e506fbcc2209")),
        "elyra-ashes",
        "Elyra et les cendres",
        EntryNodeId,
        [
            new DialogueNode(
                EntryNodeId,
                BlueprintId.From(Guid.Parse("5e891ed9-3ed6-45d7-873a-8f895e938658")),
                new LocalizedText("dialogue.elyra.greeting", "La crypte s’éveille. Avez-vous la clé ?"),
                choices:
                [
                    new DialogueChoice(
                        AcceptChoiceId,
                        new LocalizedText("dialogue.elyra.choice.key", "Je porte la clé de braise."),
                        [new DialogueCondition(DialogueConditionKind.HasItem, "ash-key", "true")],
                        [new DialogueAction(DialogueActionKind.StartQuest, "crypt-ashes", "active")],
                        RewardNodeId),
                    new DialogueChoice(
                        DialogueChoiceId.From(Guid.Parse("3b3229bd-aed7-45af-94b4-b0daf012dd37")),
                        new LocalizedText("dialogue.elyra.choice.leave", "Je reviendrai.")),
                ]),
            new DialogueNode(
                RewardNodeId,
                BlueprintId.From(Guid.Parse("5e891ed9-3ed6-45d7-873a-8f895e938658")),
                new LocalizedText("dialogue.elyra.reward", "Alors la porte reconnaîtra votre passage."),
                [new DialogueAction(DialogueActionKind.SetVariable, "door.ash.unlocked", "true")],
                [new DialogueChoice(
                    FinishChoiceId,
                    new LocalizedText("dialogue.elyra.choice.finish", "J’irai jusqu’au Gardien."),
                    actions: [new DialogueAction(DialogueActionKind.AdvanceQuest, "crypt-ashes", "warden")])]),
        ]);
}
