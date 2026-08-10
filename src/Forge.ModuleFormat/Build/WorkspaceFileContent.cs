namespace Forge.ModuleFormat.Build;

public sealed record WorkspaceFileContent(
    string FullPath,
    string RelativePath,
    byte[] Content);
