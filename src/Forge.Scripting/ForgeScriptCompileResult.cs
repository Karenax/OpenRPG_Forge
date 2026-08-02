namespace Forge.Scripting;

public sealed record ForgeScriptCompileResult(
    bool Success,
    ForgeScriptArtifact? Artifact,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);
