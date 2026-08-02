namespace Forge.Domain.Validation;

public sealed record DomainDiagnostic(string Code, string Message, string Path);
