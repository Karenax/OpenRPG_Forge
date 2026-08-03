using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Encounters;

public enum EncounterDifficulty { Story, Easy, Standard, Hard, Extreme, Custom }

public sealed record EncounterVolume(
    TriggerShape Shape,
    TriggerFilter Filter,
    bool RequireLineOfSight,
    double ThreatRadius,
    string? ActivationScript);

public sealed record SpawnCandidate(
    Guid CreatureId,
    string BlueprintId,
    EncounterRole Role,
    double Weight,
    double Cost,
    IReadOnlySet<string> Conditions,
    IReadOnlyList<string> Variants,
    int MaximumAlive);

public sealed record EncounterDifficultyProfile(
    string RulesetId,
    EncounterDifficulty Difficulty,
    int ExpectedPartySize,
    double ExpectedPartyPower,
    double BaseBudget,
    double PartySizeMultiplier,
    double DifficultyMultiplier)
{
    public double EffectiveBudget => BaseBudget * PartySizeMultiplier * DifficultyMultiplier;
}

public sealed record EncounterDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    string DisplayName,
    string Category,
    string Theme,
    string ZoneId,
    EncounterVolume Volume,
    IReadOnlyList<SpawnCandidate> Candidates,
    EncounterDifficultyProfile DifficultyProfile,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> SpawnAnchorIds,
    IReadOnlyList<string> RewardIds,
    int NetworkActorCap);

public sealed record EncounterWizardRequest(
    string Slug,
    string DisplayName,
    string Category,
    string Theme,
    string ZoneId,
    EncounterRole PrimaryRole,
    IReadOnlyList<SpawnCandidate> Candidates,
    EncounterVolume Volume,
    EncounterDifficultyProfile DifficultyProfile,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> SpawnAnchorIds,
    IReadOnlyList<string> RewardIds,
    int NetworkActorCap,
    string Provenance);

public sealed record EncounterDiagnostic(string Code, string Path, string Message);

public sealed record EncounterValidationContext(
    IReadOnlySet<Guid> CreatureIds,
    IReadOnlySet<string> BlueprintIds,
    IReadOnlySet<string> ZoneIds,
    IReadOnlySet<string> SpawnAnchorIds,
    IReadOnlySet<string> ScriptIds,
    IReadOnlySet<string> RewardIds,
    int MaximumNetworkActorCap,
    double MaximumBudget);

public sealed record EncounterWizardPreview(
    EncounterDefinition Encounter,
    IReadOnlyList<EncounterDiagnostic> Diagnostics,
    string SemanticDiff,
    bool CanApply);
