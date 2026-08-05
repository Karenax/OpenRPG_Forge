using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Rules;

public sealed record ProgressionPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string RulesetId { get; }
    public IReadOnlyList<ProgressionCurvePoint> Curve { get; }
    public double GroupMultiplier { get; }
    public double DifficultyMultiplier { get; }
    public IReadOnlyDictionary<string, double> SourceMultipliers { get; }
    public bool ShareWithParty { get; }
    public string Provenance { get; }

    public ProgressionPolicy(Guid id, int schemaVersion, string rulesetId,
        IReadOnlyList<ProgressionCurvePoint> curve, double groupMultiplier, double difficultyMultiplier,
        IReadOnlyDictionary<string, double> sourceMultipliers, bool shareWithParty, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesetId);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(sourceMultipliers);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var points = curve.OrderBy(point => point.Level).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || points.Length == 0 ||
            points[0].Level != 1 || points[0].RequiredExperience != 0 ||
            points.Any(point => point.Level <= 0 || !double.IsFinite(point.RequiredExperience) ||
                                point.RequiredExperience < 0) ||
            points.Select(point => point.Level).Distinct().Count() != points.Length ||
            points.Zip(points.Skip(1), (left, right) => right.RequiredExperience > left.RequiredExperience)
                .Any(increasing => !increasing) ||
            !PositiveFinite(groupMultiplier) || !PositiveFinite(difficultyMultiplier) ||
            sourceMultipliers.Count == 0 || sourceMultipliers.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || !PositiveFinite(item.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Progression policy is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        RulesetId = rulesetId.Trim();
        Curve = points;
        GroupMultiplier = groupMultiplier;
        DifficultyMultiplier = difficultyMultiplier;
        SourceMultipliers = sourceMultipliers.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key.Trim(), item => item.Value, StringComparer.Ordinal);
        ShareWithParty = shareWithParty;
        Provenance = provenance.Trim();
    }

    internal static ProgressionPolicy CreateDefault(Guid moduleId)
    {
        return new ProgressionPolicy(RuntimePolicyIdFactory.Create(moduleId, "rules:progression"), 1,
            "forge.generic", [new(1, 0), new(2, 100), new(3, 300), new(4, 700), new(5, 1_500)],
            1, 1, new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["combat"] = 1,
                ["exploration"] = .75,
                ["quest"] = 1.25,
            }, true, "migration-default");
    }

    private static bool PositiveFinite(double value) => double.IsFinite(value) && value > 0;
}

public sealed record ProgressionCurvePoint(int Level, double RequiredExperience);
