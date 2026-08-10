namespace Forge.Domain.Audio;

public sealed record AudioEmitterCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public IReadOnlyList<AudioEmitterDefinition> Definitions { get; }
    public IReadOnlyList<AudioEmitterInstance> Instances { get; }

    public AudioEmitterCatalog(
        int schemaVersion,
        IReadOnlyList<AudioEmitterDefinition>? definitions,
        IReadOnlyList<AudioEmitterInstance>? instances)
    {
        SchemaVersion = schemaVersion;
        Definitions = (definitions ?? []).ToArray();
        Instances = (instances ?? []).ToArray();
    }

    public static AudioEmitterCatalog Empty { get; } = new(CurrentSchemaVersion, [], []);
}
