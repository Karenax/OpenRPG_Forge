namespace Forge.Domain.Audio;

public sealed record AudioClipSet(
    Guid Id,
    int SchemaVersion,
    string Name,
    AudioClipSelectionMode Selection,
    IReadOnlyList<AudioClipDefinition> Clips,
    string Provenance)
{
    public const int CurrentSchemaVersion = 1;
}
