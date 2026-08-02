namespace Forge.Scripting;

public sealed record ForgeScriptDiagnostic(
    string Source,
    int Line,
    int Column,
    string Severity,
    string Message);
