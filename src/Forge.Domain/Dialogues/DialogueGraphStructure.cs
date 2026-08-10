using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;

namespace Forge.Domain.Dialogues;

public enum DialogueNodeRole
{
    Unspecified,
    SpeakerLine,
    PlayerChoice,
    Narration,
    System,
    ActionOnly,
    Subgraph,
    End,
}

public enum DialogueEdgeKind
{
    Flow,
    Reference,
}

public sealed record DialogueParticipant(
    Guid Id,
    int SchemaVersion,
    string Role,
    BlueprintId? BlueprintId,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;

    public static DialogueParticipant FromSpeaker(BlueprintId? blueprintId) => new(
        StableId(blueprintId?.ToString() ?? "narrator"),
        CurrentSchemaVersion,
        blueprintId is null ? "narrator" : $"speaker-{blueprintId.ToString()[..8]}",
        blueprintId,
        "migration-default");

    internal static Guid StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:dialogue-participant:{value}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}

public sealed record DialogueEntryPoint(
    Guid Id,
    int SchemaVersion,
    string Key,
    DialogueNodeId NodeId,
    int Order,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record DialogueGraphEdge(
    DialogueChoiceId Id,
    DialogueNodeId SourceNodeId,
    DialogueNodeId TargetNodeId,
    DialogueEdgeKind Kind,
    int Order,
    int Priority);
