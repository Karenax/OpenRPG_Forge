namespace Forge.Application.Assistant;

public sealed record LoreContextPacket(
    string Query,
    string Content,
    string ContentSha256,
    IReadOnlyList<string> SourceIds,
    int EntryCount,
    bool Truncated);
