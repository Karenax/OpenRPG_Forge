namespace Forge.Domain.WorldFeatures;

public enum EncounterDifficultyBand
{
    Story,
    Easy,
    Standard,
    Hard,
    Extreme,
    Custom,
}

public enum EncounterActivationMode
{
    Once,
    Repeat,
    Timed,
    Wave,
    Persistent,
    PopulationMaintained,
    Script,
}

public enum EncounterSpawnDistribution
{
    Sequential,
    Random,
    Weighted,
    Even,
}

public sealed record EncounterCompositionProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    string Category,
    string Theme,
    EncounterWorldProfile.CandidateRole PrimaryRole,
    EncounterCompositionProfile.DifficultyPolicy Difficulty,
    IReadOnlyList<EncounterCompositionProfile.GroupPolicy> Groups,
    EncounterCompositionProfile.SelectionPolicy Selection,
    EncounterCompositionProfile.ActivationPolicy Activation,
    IReadOnlyDictionary<string, string> Overrides)
{
    public const int CurrentSchemaVersion = 1;

    public sealed record DifficultyPolicy(
        Guid Id,
        string RulesetId,
        string PartyAssessmentProviderId,
        EncounterDifficultyBand Band,
        int ExpectedPartySize,
        double ExpectedPartyPower,
        double BaseBudget,
        double PartySizeMultiplier,
        double DifficultyMultiplier)
    {
        public double EffectiveBudget => BaseBudget * PartySizeMultiplier * DifficultyMultiplier;
    }

    public sealed record GroupPolicy(
        Guid Id,
        string Slug,
        int Minimum,
        int Maximum,
        double Budget,
        IReadOnlyList<Guid> CandidateIds);

    public sealed record SelectionPolicy(
        Guid Id,
        string PartyAssessmentProviderId,
        string Strategy,
        int DeterministicSeed,
        bool PreviewComposition);

    public sealed record ActivationPolicy(
        Guid Id,
        EncounterActivationMode Mode,
        double IntervalSeconds,
        string? ScriptId,
        EncounterSpawnDistribution Distribution);
}
