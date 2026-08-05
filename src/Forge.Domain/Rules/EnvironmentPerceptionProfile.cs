using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Rules;

public sealed record EnvironmentPerceptionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string RulesetId { get; }
    public IReadOnlyList<EnvironmentPerceptionModifier> Modifiers { get; }
    public string Provenance { get; }

    public EnvironmentPerceptionProfile(Guid id, int schemaVersion, string rulesetId,
        IReadOnlyList<EnvironmentPerceptionModifier> modifiers, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesetId);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = modifiers.OrderBy(item => item.SenseId, StringComparer.Ordinal)
            .ThenBy(item => item.ConditionTag, StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(item => $"{item.SenseId}:{item.ConditionTag}")
                .Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.SenseId) ||
                !double.IsFinite(item.Multiplier) || item.Multiplier is < 0 or > 10 ||
                !double.IsFinite(item.AdditiveModifier) || item.AdditiveModifier is < -10 or > 10))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Environment perception profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        RulesetId = rulesetId.Trim();
        Modifiers = values;
        Provenance = provenance.Trim();
    }

    internal static EnvironmentPerceptionProfile CreateDefault(AreaId areaId) => new(areaId.Value, 1,
        "forge.generic",
        [
            new EnvironmentPerceptionModifier(RuntimePolicyIdFactory.Create(areaId.Value, "rules:sense:vision"),
                "vision", 1, 0, string.Empty),
            new EnvironmentPerceptionModifier(RuntimePolicyIdFactory.Create(areaId.Value, "rules:sense:hearing"),
                "hearing", 1, 0, string.Empty),
        ], "migration-default");
}

public sealed record EnvironmentPerceptionModifier(Guid Id, string SenseId, double Multiplier,
    double AdditiveModifier, string ConditionTag);
