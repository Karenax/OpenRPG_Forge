namespace Forge.Domain.Assets;

public sealed record AssetImportPlan(
    int SchemaVersion,
    AssetCatalogItemId AssetId,
    string SourceRelativePath,
    string DestinationRelativeDirectory,
    string ContentSha256,
    string LicenseSpdx,
    string Provenance,
    AssetProcessingProfile Profile,
    string CacheKey,
    IReadOnlyList<AssetBuildTask> Tasks,
    bool DryRun)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AssetBuildTask(
    string Name,
    string OutputRelativePath,
    string CacheKey,
    IReadOnlyList<string> Dependencies);
