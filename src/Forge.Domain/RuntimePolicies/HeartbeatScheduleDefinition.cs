using Forge.Domain.Lifecycle;

namespace Forge.Domain.RuntimePolicies;

public sealed record HeartbeatScheduleDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Name { get; }
    public HeartbeatScope Scope { get; }
    public bool Enabled { get; }
    public int IntervalMilliseconds { get; }
    public int BudgetMilliseconds { get; }
    public bool SuspendWhenInactive { get; }
    public string ForgeScriptApiVersion { get; }
    public string Handler { get; }
    public string Provenance { get; }
    public string? ScriptPath { get; }
    public bool RequiredAtRuntime { get; }

    public HeartbeatScheduleDefinition(Guid id, int schemaVersion, string name, HeartbeatScope scope,
        bool enabled, int intervalMilliseconds, int budgetMilliseconds, bool suspendWhenInactive,
        string forgeScriptApiVersion, string handler, string provenance, string? scriptPath = null,
        bool requiredAtRuntime = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(forgeScriptApiVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            intervalMilliseconds is < 100 or > 86_400_000 || budgetMilliseconds is < 1 or > 1_000 ||
            budgetMilliseconds > intervalMilliseconds ||
            requiredAtRuntime && string.IsNullOrWhiteSpace(scriptPath) ||
            !string.IsNullOrWhiteSpace(scriptPath) &&
            !ProjectLifecycleEventProfileValidator.IsSafeScriptPath(scriptPath))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Heartbeat schedule is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Scope = scope;
        Enabled = enabled;
        IntervalMilliseconds = intervalMilliseconds;
        BudgetMilliseconds = budgetMilliseconds;
        SuspendWhenInactive = suspendWhenInactive;
        ForgeScriptApiVersion = forgeScriptApiVersion.Trim();
        Handler = handler.Trim();
        Provenance = provenance.Trim();
        ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : scriptPath.Trim().Replace('\\', '/');
        RequiredAtRuntime = requiredAtRuntime;
    }
}
