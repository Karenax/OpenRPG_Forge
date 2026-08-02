namespace Forge.ModuleFormat.Saves;

public sealed record RuntimeSaveDiagnostic(string Code, string Message, string? Path = null);

public sealed record RuntimeSaveOperationResult(
    bool Succeeded,
    RuntimeSaveDocument? Save,
    IReadOnlyList<RuntimeSaveDiagnostic> Diagnostics);
