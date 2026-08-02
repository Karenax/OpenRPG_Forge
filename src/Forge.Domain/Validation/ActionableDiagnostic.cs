namespace Forge.Domain.Validation;

public sealed record ActionableDiagnostic
{
    public const int CurrentSchemaVersion = 1;

    public DiagnosticId Id { get; }

    public int SchemaVersion { get; }

    public DiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string DocumentPath { get; }

    public Guid? SubjectUuid { get; }

    public string Property { get; }

    public string Source { get; }

    public IReadOnlyList<DiagnosticQuickFixDefinition> QuickFixes { get; }

    public string Provenance { get; }

    public ActionableDiagnostic(
        DiagnosticId id,
        DiagnosticSeverity severity,
        string code,
        string message,
        string documentPath,
        Guid? subjectUuid,
        string property,
        string source,
        IReadOnlyList<DiagnosticQuickFixDefinition>? quickFixes,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Actionable diagnostic schema '{schemaVersion}' is not supported.");
        }

        var fixes = (quickFixes ?? []).OrderBy(fix => fix.Label, StringComparer.Ordinal).ToArray();
        if (fixes.Select(fix => fix.Id).Distinct().Count() != fixes.Length)
        {
            throw new ArgumentException("Diagnostic quick-fix IDs must be unique.", nameof(quickFixes));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Severity = severity;
        Code = code.Trim();
        Message = message.Trim();
        DocumentPath = documentPath.Trim();
        SubjectUuid = subjectUuid;
        Property = property.Trim();
        Source = source.Trim();
        QuickFixes = fixes;
        Provenance = provenance.Trim();
    }
}
