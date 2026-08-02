namespace Forge.Domain.Modules;

public sealed record ThirdPartyDependencyApproval
{
    public const int CurrentSchemaVersion = 1;

    public ThirdPartyDependencyId Id { get; }

    public int SchemaVersion { get; }

    public string Name { get; }

    public string Version { get; }

    public string LicenseSpdx { get; }

    public string Owner { get; }

    public string Rationale { get; }

    public string UpdateCadence { get; }

    public string RemovalPlan { get; }

    public string SourceReference { get; }

    public IReadOnlyList<ModuleTargetKind> RequiredForTargets { get; }

    public string Provenance { get; }

    public ThirdPartyDependencyApproval(
        ThirdPartyDependencyId id,
        string name,
        string version,
        string licenseSpdx,
        string owner,
        string rationale,
        string updateCadence,
        string removalPlan,
        string sourceReference,
        IReadOnlyList<ModuleTargetKind> requiredForTargets,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateCadence);
        ArgumentException.ThrowIfNullOrWhiteSpace(removalPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        ArgumentNullException.ThrowIfNull(requiredForTargets);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Third-party dependency schema '{schemaVersion}' is not supported.");
        }

        var targets = requiredForTargets.Distinct().Order().ToArray();
        if (targets.Length == 0)
        {
            throw new ArgumentException("A third-party dependency must be assigned to at least one target.",
                nameof(requiredForTargets));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Version = version.Trim();
        LicenseSpdx = licenseSpdx.Trim();
        Owner = owner.Trim();
        Rationale = rationale.Trim();
        UpdateCadence = updateCadence.Trim();
        RemovalPlan = removalPlan.Trim();
        SourceReference = sourceReference.Trim();
        RequiredForTargets = targets;
        Provenance = provenance.Trim();
    }
}
