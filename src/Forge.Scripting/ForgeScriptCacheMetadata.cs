namespace Forge.Scripting;

public sealed record ForgeScriptCacheMetadata(
    string Id,
    int SchemaVersion,
    string CacheKey,
    string SourceSha256,
    string BytecodeSha256,
    string ApiVersion,
    string CompilerVersion,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;
}
