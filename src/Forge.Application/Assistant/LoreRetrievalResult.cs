using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record LoreRetrievalResult(
    Guid EntryId,
    LoreEntryKind Kind,
    string Key,
    string Title,
    string Content,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<Guid> RelatedEntryIds,
    IReadOnlyList<string> Tags,
    string Provenance,
    int Score);
