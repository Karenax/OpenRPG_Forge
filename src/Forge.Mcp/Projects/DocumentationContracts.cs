namespace Forge.Mcp.Projects;

public sealed record GeneratedDocumentationResource(
    int SchemaVersion,
    bool Current,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ChangedFiles);
