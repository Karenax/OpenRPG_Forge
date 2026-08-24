using Forge.Domain.Lifecycle;

namespace Forge.Domain.RuntimePolicies;

public sealed record RestLifecycleProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly IReadOnlySet<string> RequiredEvents = new HashSet<string>(StringComparer.Ordinal)
    {
        "rest.requested",
        "rest.started",
        "rest.interrupted",
        "rest.completed",
    };

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public PartyRestPolicy PartyPolicy { get; }
    public int DefaultDurationSeconds { get; }
    public IReadOnlyList<EventBinding> Bindings { get; }
    public string Provenance { get; }

    public RestLifecycleProfile(Guid id, int schemaVersion, bool enabled, PartyRestPolicy partyPolicy,
        int defaultDurationSeconds, IReadOnlyList<EventBinding> bindings, string provenance)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || defaultDurationSeconds is < 1 or > 86_400)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Rest lifecycle profile is invalid.");
        }

        var values = bindings.OrderBy(binding => binding.EventName, StringComparer.Ordinal).ToArray();
        if (values.Select(binding => binding.Id).Distinct().Count() != values.Length ||
            values.Select(binding => binding.EventName).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(binding => binding.Id == Guid.Empty || binding.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(binding.Provenance) || string.IsNullOrWhiteSpace(binding.ForgeScriptApiVersion) ||
                string.IsNullOrWhiteSpace(binding.Handler) ||
                binding.RequiredAtRuntime && string.IsNullOrWhiteSpace(binding.ScriptPath) ||
                !string.IsNullOrWhiteSpace(binding.ScriptPath) &&
                !ProjectLifecycleEventProfileValidator.IsSafeScriptPath(binding.ScriptPath)) ||
            enabled && !RequiredEvents.SetEquals(values.Select(binding => binding.EventName)))
        {
            throw new ArgumentException("Rest lifecycle bindings are invalid.", nameof(bindings));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        PartyPolicy = partyPolicy;
        DefaultDurationSeconds = defaultDurationSeconds;
        Bindings = values;
        Provenance = provenance.Trim();
    }
}
