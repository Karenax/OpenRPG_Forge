using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed record CreatorDiagnostic
{
    public string Id => DiagnosticId.ToString();

    public DiagnosticId DiagnosticId { get; }

    public int SchemaVersion { get; }

    public CreatorDiagnosticSeverity Severity { get; }

    public CreatorDiagnosticCategory Category { get; }

    public string Code { get; }

    public string Message { get; }

    public string DocumentPath { get; }

    public Guid? SubjectUuid { get; }

    public string Property { get; }

    public string Source { get; }

    public IReadOnlyList<DiagnosticQuickFixDefinition> QuickFixes { get; }

    public string Provenance { get; }

    public string? CorrelationId { get; }

    public CreatorSelection? Target { get; }

    private CreatorDiagnostic(
        DiagnosticId diagnosticId,
        int schemaVersion,
        CreatorDiagnosticSeverity severity,
        CreatorDiagnosticCategory category,
        string code,
        string message,
        string documentPath,
        Guid? subjectUuid,
        string property,
        string source,
        IReadOnlyList<DiagnosticQuickFixDefinition> quickFixes,
        string provenance,
        string? correlationId,
        CreatorSelection? target)
    {
        DiagnosticId = diagnosticId;
        SchemaVersion = schemaVersion;
        Severity = severity;
        Category = category;
        Code = code;
        Message = message;
        DocumentPath = documentPath;
        SubjectUuid = subjectUuid;
        Property = property;
        Source = source;
        QuickFixes = quickFixes;
        Provenance = provenance;
        CorrelationId = correlationId;
        Target = target;
    }

    public static CreatorDiagnostic FromActionable(
        ActionableDiagnostic diagnostic,
        string? correlationId = null,
        CreatorSelection? target = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new CreatorDiagnostic(
            diagnostic.Id,
            diagnostic.SchemaVersion,
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Information => CreatorDiagnosticSeverity.Information,
                DiagnosticSeverity.Warning => CreatorDiagnosticSeverity.Warning,
                _ => CreatorDiagnosticSeverity.Error,
            },
            Classify(diagnostic.Code, diagnostic.DocumentPath, diagnostic.Source),
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.DocumentPath,
            diagnostic.SubjectUuid,
            diagnostic.Property,
            diagnostic.Source,
            diagnostic.QuickFixes,
            diagnostic.Provenance,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            target);
    }

    public static CreatorDiagnostic Create(
        CreatorDiagnosticSeverity severity,
        string code,
        string message,
        string documentPath,
        string? correlationId = null,
        CreatorSelection? target = null,
        CreatorDiagnosticCategory? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        var identity = string.Join('\u001f', code, documentPath, message);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        var property = documentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "document";
        return new CreatorDiagnostic(
            DiagnosticId.From(new Guid(guid)),
            ActionableDiagnostic.CurrentSchemaVersion,
            severity,
            category ?? Classify(code, documentPath, "CreatorTooling"),
            code.Trim(),
            message.Trim(),
            documentPath.Trim(),
            target is not null && Guid.TryParse(target.Id, out var targetId) ? targetId : null,
            property,
            "CreatorTooling",
            [],
            "derived-creator-diagnostic",
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            target);
    }

    public static CreatorDiagnosticCategory Classify(string code, string documentPath, string source)
    {
        var value = $"{code} {documentPath} {source}";
        if (ContainsAny(value, "license", "spdx", "copyright")) return CreatorDiagnosticCategory.License;
        if (ContainsAny(value, "navigation", "navmesh", "pathfinding", "portal")) return CreatorDiagnosticCategory.Navigation;
        if (ContainsAny(value, "geometry", "placement", "terrain", "collision")) return CreatorDiagnosticCategory.Geometry;
        if (ContainsAny(value, "graph", "dialogue", "quest", "cycle")) return CreatorDiagnosticCategory.Graph;
        if (ContainsAny(value, "reference", "missing", "not_found", "unresolved")) return CreatorDiagnosticCategory.Reference;
        if (ContainsAny(value, "schema", "format", "version", "serialization")) return CreatorDiagnosticCategory.Schema;
        return CreatorDiagnosticCategory.Runtime;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
