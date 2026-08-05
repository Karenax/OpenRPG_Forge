namespace Forge.ModuleFormat.Content;

public sealed record BlueprintExportResult(
    string OutputPath,
    string ContentSha256,
    long Bytes,
    int BlueprintCount,
    int DependencyCount,
    int PreviewCount);
