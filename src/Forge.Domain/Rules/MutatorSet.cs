using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Rules;

public sealed record MutatorSet
{
    public const int CurrentSchemaVersion = 1;
    private static readonly HashSet<string> SupportedScalars = new(StringComparer.Ordinal)
    {
        "playerDamage", "incomingDamage", "aiIntensity", "economy", "restRecovery", "progression",
    };

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<GameplayMutatorDefinition> Mutators { get; }
    public string Provenance { get; }

    public MutatorSet(Guid id, int schemaVersion, IReadOnlyList<GameplayMutatorDefinition> mutators,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(mutators);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = mutators.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            values.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Key) ||
                string.IsNullOrWhiteSpace(item.DisplayName) || item.ScalarOverrides.Count == 0 ||
                item.ScalarOverrides.Any(value => !SupportedScalars.Contains(value.Key) ||
                    !double.IsFinite(value.Value) || value.Value is < .05 or > 20)))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Mutator set is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Mutators = values;
        Provenance = provenance.Trim();
    }

    internal static MutatorSet CreateDefault(Guid moduleId) => new(
        RuntimePolicyIdFactory.Create(moduleId, "rules:mutators"), 1,
        [
            new GameplayMutatorDefinition(RuntimePolicyIdFactory.Create(moduleId, "rules:mutator:story"),
                "story-support", "Soutien narratif", false,
                new Dictionary<string, double> { ["incomingDamage"] = .65, ["progression"] = 1.25 },
                ["accessibility", "story"]),
            new GameplayMutatorDefinition(RuntimePolicyIdFactory.Create(moduleId, "rules:mutator:scarcity"),
                "scarce-resources", "Ressources rares", false,
                new Dictionary<string, double> { ["economy"] = .7, ["restRecovery"] = .6 },
                ["survival"]),
        ], "migration-default");
}

public sealed record GameplayMutatorDefinition(Guid Id, string Key, string DisplayName, bool Enabled,
    IReadOnlyDictionary<string, double> ScalarOverrides, IReadOnlyList<string> Tags);
