using Forge.Domain.Lifecycle;

namespace Forge.Domain.RuntimePolicies;

public sealed record SequenceAbortPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public EventBinding Binding { get; }
    public SequenceAbortRestoreFlags Restore { get; }
    public int MaximumNarrativeRollbackSteps { get; }
    public string Provenance { get; }

    public SequenceAbortPolicy(Guid id, int schemaVersion, bool enabled, EventBinding binding,
        SequenceAbortRestoreFlags restore, int maximumNarrativeRollbackSteps, string provenance)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || binding.Id == Guid.Empty ||
            binding.SchemaVersion != 1 || !string.Equals(binding.EventName, "sequence.aborted", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(binding.ForgeScriptApiVersion) || string.IsNullOrWhiteSpace(binding.Handler) ||
            binding.Authority != EventAuthorityScope.Server ||
            binding.RequiredAtRuntime && string.IsNullOrWhiteSpace(binding.ScriptPath) ||
            !string.IsNullOrWhiteSpace(binding.ScriptPath) &&
            !ProjectLifecycleEventProfileValidator.IsSafeScriptPath(binding.ScriptPath) ||
            restore == SequenceAbortRestoreFlags.None || maximumNarrativeRollbackSteps is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Sequence abort policy is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        Binding = binding;
        Restore = restore;
        MaximumNarrativeRollbackSteps = maximumNarrativeRollbackSteps;
        Provenance = provenance.Trim();
    }
}
