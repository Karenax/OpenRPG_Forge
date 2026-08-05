namespace Forge.Domain.Modules;

public sealed record ModuleDeltaConfiguration
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumChunkSizeBytes = 64 * 1024;
    public const int MaximumChunkSizeBytes = 16 * 1024 * 1024;

    public int SchemaVersion { get; }

    public bool Enabled { get; }

    public int ChunkSizeBytes { get; }

    public bool AllowResume { get; }

    public bool DeduplicateByHash { get; }

    public bool AllowRollback { get; }

    public int RetainedVersions { get; }

    public ModuleDeltaConfiguration(
        bool enabled,
        int chunkSizeBytes,
        bool allowResume,
        bool deduplicateByHash,
        bool allowRollback,
        int retainedVersions,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Module delta schema '{schemaVersion}' is not supported.");
        }

        if (chunkSizeBytes < MinimumChunkSizeBytes || chunkSizeBytes > MaximumChunkSizeBytes ||
            (chunkSizeBytes & (chunkSizeBytes - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSizeBytes),
                $"Delta chunk size must be a power of two between {MinimumChunkSizeBytes} and {MaximumChunkSizeBytes} bytes.");
        }

        if (retainedVersions is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedVersions), "Retained versions must be between 1 and 16.");
        }

        Enabled = enabled;
        ChunkSizeBytes = chunkSizeBytes;
        AllowResume = allowResume;
        DeduplicateByHash = deduplicateByHash;
        AllowRollback = allowRollback;
        RetainedVersions = retainedVersions;
        SchemaVersion = schemaVersion;
    }
}
