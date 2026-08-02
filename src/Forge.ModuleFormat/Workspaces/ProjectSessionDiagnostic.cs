namespace Forge.ModuleFormat.Workspaces;

public sealed record ProjectSessionDiagnostic(string Code, string Message, string? Path = null);
