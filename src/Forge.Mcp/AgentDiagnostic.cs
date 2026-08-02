namespace Forge.Mcp;

public sealed record AgentQuickFix(
    string Id,
    int SchemaVersion,
    string Label,
    string Description,
    string Kind,
    string AreaId,
    string? EntityId,
    string Provenance);

public sealed record AgentDiagnostic(
    string Code,
    string Message,
    string? Path = null,
    string? Id = null,
    int SchemaVersion = 1,
    string? Severity = null,
    string? SubjectUuid = null,
    string? Property = null,
    string? Source = null,
    IReadOnlyList<AgentQuickFix>? QuickFixes = null,
    string? Provenance = null);
