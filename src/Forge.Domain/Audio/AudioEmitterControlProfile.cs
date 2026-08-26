namespace Forge.Domain.Audio;

public sealed record AudioEmitterControlProfile(
    int SchemaVersion,
    float Volume,
    bool Muted,
    IReadOnlyList<string> Conditions)
{
    public const int CurrentSchemaVersion = 1;

    public static AudioEmitterControlProfile Default { get; } =
        new(CurrentSchemaVersion, 1, false, []);
}
