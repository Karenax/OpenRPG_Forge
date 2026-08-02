namespace Forge.Application.Commands;

public sealed record AuditEntry(
    CommandId CommandId,
    string CommandName,
    DateTimeOffset Timestamp,
    bool Applied);
