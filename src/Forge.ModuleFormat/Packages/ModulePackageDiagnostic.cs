namespace Forge.ModuleFormat.Packages;

public sealed record ModulePackageDiagnostic(string Code, string Message, string? Path = null);
