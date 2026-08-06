namespace Forge.ModuleFormat.Assets;

public sealed record AssetVaultPlannedObject(
    string ContentSha256,
    long Bytes,
    string SourcePath,
    string ObjectRelativePath,
    bool Reused);
