using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;

namespace Forge.Application.Tests.Dialogues;

public sealed class DialogueGraphScriptServiceTests
{
    [Fact]
    public void Typed_handle_reads_graph_and_rejects_stale_identity()
    {
        var entry = DialogueNodeId.Parse("d0051000-0000-4000-8000-000000000001");
        var end = DialogueNodeId.Parse("d0051000-0000-4000-8000-000000000002");
        var graph = new DialogueDefinition(DialogueId.Parse("d0051000-0000-4000-8000-000000000010"),
            "script", "Script", entry,
            [
                new(entry, null, new("dialogue.entry", "Entrée"), choices:
                [new(DialogueChoiceId.Parse("d0051000-0000-4000-8000-000000000020"),
                    new LocalizedText("dialogue.link", "Lien",
                        [LocalizedTextVariant.Create("dialogue.link", "en", "Link")]),
                    [new(DialogueConditionKind.HasItem, "key", "true")],
                    [new(DialogueActionKind.PlayAudio, "link", "play")],
                    end, DialogueEdgeKind.Reference)]),
                new(end, null, new("dialogue.end", "Fin"), role: DialogueNodeRole.End),
            ]);
        var service = new DialogueGraphScriptService(graph);

        var snapshot = service.Read(service.Handle);

        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(1, snapshot.EdgeCount);
        Assert.Equal(1, snapshot.ReferenceEdgeCount);
        Assert.Equal(1, snapshot.LocalizationVariantCount);
        Assert.Equal(1, snapshot.ConditionCount);
        Assert.Equal(1, snapshot.ActionCount);
        Assert.Equal(entry, service.ReadNode(service.Handle, entry).Id);
        Assert.Throws<InvalidOperationException>(() => service.Read(
            new DialogueGraphHandle(DialogueId.Parse("d0051000-0000-4000-8000-000000000099"), 1)));
    }
}
