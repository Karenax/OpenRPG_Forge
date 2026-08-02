using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Creator;

public sealed record CreatorDiagnostic
{
    public string Id { get; }

    public CreatorDiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string DocumentPath { get; }

    public string? CorrelationId { get; }

    public CreatorSelection? Target { get; }

    private CreatorDiagnostic(
        string id,
        CreatorDiagnosticSeverity severity,
        string code,
        string message,
        string documentPath,
        string? correlationId,
        CreatorSelection? target)
    {
        Id = id;
        Severity = severity;
        Code = code;
        Message = message;
        DocumentPath = documentPath;
        CorrelationId = correlationId;
        Target = target;
    }

    public static CreatorDiagnostic Create(
        CreatorDiagnosticSeverity severity,
        string code,
        string message,
        string documentPath,
        string? correlationId = null,
        CreatorSelection? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        var identity = string.Join('\u001f', code, documentPath, message, correlationId ?? string.Empty);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return new CreatorDiagnostic(
            $"diagnostic:{hash[..16]}",
            severity,
            code.Trim(),
            message.Trim(),
            documentPath.Trim(),
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            target);
    }
}
