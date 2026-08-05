namespace Forge.Domain.Gameplay;

public enum MultiplayerPausePolicy
{
    Disabled,
    HostOnly,
    Vote,
    Everyone,
}

public sealed record TimeControlPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool PauseEnabled { get; }
    public bool SlowMotionEnabled { get; }
    public float SlowMotionScale { get; }
    public bool PlanningEnabled { get; }
    public int MaximumPlannedActions { get; }
    public bool TurnModeEnabled { get; }
    public MultiplayerPausePolicy MultiplayerPolicy { get; }
    public float RequiredVoteFraction { get; }
    public IReadOnlyList<string> PausePermissions { get; }
    public string Provenance { get; }

    public TimeControlPolicy(Guid id, int schemaVersion, bool pauseEnabled, bool slowMotionEnabled,
        float slowMotionScale, bool planningEnabled, int maximumPlannedActions, bool turnModeEnabled,
        MultiplayerPausePolicy multiplayerPolicy, float requiredVoteFraction,
        IReadOnlyList<string> pausePermissions, string provenance)
    {
        ArgumentNullException.ThrowIfNull(pausePermissions);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var permissions = pausePermissions.Select(value => (value ?? string.Empty).Trim().ToLowerInvariant())
            .Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || slowMotionScale is <= 0 or >= 1 ||
            maximumPlannedActions is < 1 or > 64 || requiredVoteFraction is <= 0 or > 1 ||
            (multiplayerPolicy != MultiplayerPausePolicy.Disabled && permissions.Length == 0))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Time control policy is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        PauseEnabled = pauseEnabled;
        SlowMotionEnabled = slowMotionEnabled;
        SlowMotionScale = slowMotionScale;
        PlanningEnabled = planningEnabled;
        MaximumPlannedActions = maximumPlannedActions;
        TurnModeEnabled = turnModeEnabled;
        MultiplayerPolicy = multiplayerPolicy;
        RequiredVoteFraction = requiredVoteFraction;
        PausePermissions = permissions;
        Provenance = provenance.Trim();
    }
}
