using Forge.Domain.Modules;
using System.Text.Json.Serialization;

namespace Forge.Domain.Rules;

public sealed record ModuleRulesProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public ProgressionPolicy Progression { get; }
    public IReadOnlyList<DifficultyProfile> Difficulties { get; }
    public string ActiveDifficultyKey { get; }
    public MutatorSet Mutators { get; }
    public string Provenance { get; }

    [JsonIgnore]
    public DifficultyProfile ActiveDifficulty => Difficulties.Single(item => item.Key == ActiveDifficultyKey);

    public ModuleRulesProfile(Guid id, int schemaVersion, ProgressionPolicy progression,
        IReadOnlyList<DifficultyProfile> difficulties, string activeDifficultyKey, MutatorSet mutators,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(difficulties);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeDifficultyKey);
        ArgumentNullException.ThrowIfNull(mutators);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = difficulties.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var active = activeDifficultyKey.Trim().ToLowerInvariant();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || values.Length == 0 ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            values.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.All(item => item.Key != active))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Module rules profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Progression = progression;
        Difficulties = values;
        ActiveDifficultyKey = active;
        Mutators = mutators;
        Provenance = provenance.Trim();
    }

    public static ModuleRulesProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var difficulty = DifficultyProfile.CreateDefault(moduleId.Value);
        return new ModuleRulesProfile(moduleId.Value, 1, ProgressionPolicy.CreateDefault(moduleId.Value),
            [difficulty], difficulty.Key, MutatorSet.CreateDefault(moduleId.Value), "migration-default");
    }
}
