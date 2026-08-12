using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Domain.RuntimeExtensions;

public enum CrashDumpAdapterKind
{
    None,
    Managed,
    Native,
}

public sealed record AgentTaskManifest
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string Goal { get; }
    public IReadOnlyList<string> ScopePaths { get; }
    public IReadOnlyList<string> ForbiddenPaths { get; }
    public IReadOnlyList<string> AcceptanceTests { get; }
    public string Owner { get; }
    public IReadOnlyList<Guid> Dependencies { get; }
    public string Provenance { get; }

    public AgentTaskManifest(Guid id, int schemaVersion, string key, string goal,
        IReadOnlyList<string> scopePaths, IReadOnlyList<string> forbiddenPaths,
        IReadOnlyList<string> acceptanceTests, string owner, IReadOnlyList<Guid> dependencies,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(scopePaths);
        ArgumentNullException.ThrowIfNull(forbiddenPaths);
        ArgumentNullException.ThrowIfNull(acceptanceTests);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "Task manifests require a stable UUID and schema v1.");
        Id = id;
        SchemaVersion = schemaVersion;
        Key = DataDrivenRuleSchema.NormalizeKey(key, nameof(key));
        Goal = RequireBoundedText(goal, 1_000, nameof(goal));
        ScopePaths = NormalizePaths(scopePaths, nameof(scopePaths));
        ForbiddenPaths = NormalizePaths(forbiddenPaths, nameof(forbiddenPaths));
        AcceptanceTests = acceptanceTests.Select(item => RequireBoundedText(item, 500, nameof(acceptanceTests)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Owner = DataDrivenRuleSchema.NormalizeKey(owner, nameof(owner));
        Dependencies = dependencies.Distinct().Order().ToArray();
        if (Dependencies.Contains(Guid.Empty) || Dependencies.Contains(id))
            throw new ArgumentException("Task dependencies require non-empty UUIDs and cannot reference the task itself.", nameof(dependencies));
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static IReadOnlyList<string> NormalizePaths(IReadOnlyList<string> values, string parameter)
    {
        var normalized = values.Select(item => DataDrivenRuleSchema.RequireText(item, parameter).Replace('\\', '/').Trim('/'))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (normalized.Any(item => item.Length > 260 || Path.IsPathRooted(item) || item.Length == 0 ||
                                   item.Split('/').Any(segment => segment is "." or "..")))
            throw new ArgumentException("Task paths must be safe, bounded repository-relative paths.", parameter);
        return normalized;
    }

    private static string RequireBoundedText(string value, int maximumLength, string parameter)
    {
        var normalized = DataDrivenRuleSchema.RequireText(value, parameter);
        if (normalized.Length > maximumLength || normalized.Any(character => char.IsControl(character) && character != '\t'))
            throw new ArgumentException($"Text must contain at most {maximumLength} printable characters.", parameter);
        return normalized;
    }
}

public sealed record CrashArtifactPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool CaptureSanitizedLogs { get; }
    public CrashDumpAdapterKind DumpAdapter { get; }
    public int MaxLogLines { get; }
    public long MaxArtifactBytes { get; }
    public bool IncludeVersion { get; }
    public bool IncludePackageFingerprint { get; }
    public bool IncludeReproductionId { get; }
    public IReadOnlyList<string> SensitiveKeys { get; }
    public string Provenance { get; }

    public CrashArtifactPolicy(Guid id, int schemaVersion, bool captureSanitizedLogs,
        CrashDumpAdapterKind dumpAdapter, int maxLogLines, long maxArtifactBytes, bool includeVersion,
        bool includePackageFingerprint, bool includeReproductionId, IReadOnlyList<string> sensitiveKeys,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(sensitiveKeys);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maxLogLines is < 10 or > 100_000 ||
            maxArtifactBytes is < 65_536 or > 536_870_912)
            throw new ArgumentOutOfRangeException(nameof(id), "Crash artifacts require schema v1 and bounded retention quotas.");
        Id = id;
        SchemaVersion = schemaVersion;
        CaptureSanitizedLogs = captureSanitizedLogs;
        DumpAdapter = dumpAdapter;
        MaxLogLines = maxLogLines;
        MaxArtifactBytes = maxArtifactBytes;
        IncludeVersion = includeVersion;
        IncludePackageFingerprint = includePackageFingerprint;
        IncludeReproductionId = includeReproductionId;
        SensitiveKeys = sensitiveKeys.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(sensitiveKeys)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record ProjectHealthPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<string> Domains { get; }
    public bool IncludeTestStatus { get; }
    public bool IncludePerformance { get; }
    public bool IncludeLicenses { get; }
    public bool IncludeMigrations { get; }
    public bool IncludeCoverage { get; }
    public bool FailBuildWhenUnhealthy { get; }
    public string Provenance { get; }

    public ProjectHealthPolicy(Guid id, int schemaVersion, IReadOnlyList<string> domains,
        bool includeTestStatus, bool includePerformance, bool includeLicenses, bool includeMigrations,
        bool includeCoverage, bool failBuildWhenUnhealthy, string provenance)
    {
        ArgumentNullException.ThrowIfNull(domains);
        var normalized = domains.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(domains)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || normalized.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Project health requires schema v1 and at least one domain.");
        Id = id;
        SchemaVersion = schemaVersion;
        Domains = normalized;
        IncludeTestStatus = includeTestStatus;
        IncludePerformance = includePerformance;
        IncludeLicenses = includeLicenses;
        IncludeMigrations = includeMigrations;
        IncludeCoverage = includeCoverage;
        FailBuildWhenUnhealthy = failBuildWhenUnhealthy;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record CrashRecoveryPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int AutosaveIntervalSeconds { get; }
    public int RetainedSnapshots { get; }
    public bool OfferRecoveryOnOpen { get; }
    public bool QuarantineCorruptFiles { get; }
    public bool CompareBeforeRestore { get; }
    public string Provenance { get; }

    public CrashRecoveryPolicy(Guid id, int schemaVersion, int autosaveIntervalSeconds,
        int retainedSnapshots, bool offerRecoveryOnOpen, bool quarantineCorruptFiles,
        bool compareBeforeRestore, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            autosaveIntervalSeconds is < 10 or > 3_600 || retainedSnapshots is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(id), "Crash recovery requires schema v1 and bounded autosave retention.");
        Id = id;
        SchemaVersion = schemaVersion;
        AutosaveIntervalSeconds = autosaveIntervalSeconds;
        RetainedSnapshots = retainedSnapshots;
        OfferRecoveryOnOpen = offerRecoveryOnOpen;
        QuarantineCorruptFiles = quarantineCorruptFiles;
        CompareBeforeRestore = compareBeforeRestore;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

[Flags]
public enum RecoveryRestoreSection
{
    None = 0,
    ProjectAndModule = 1 << 0,
    Blueprints = 1 << 1,
    Areas = 1 << 2,
    Dialogues = 1 << 3,
    Quests = 1 << 4,
    All = ProjectAndModule | Blueprints | Areas | Dialogues | Quests,
}

public sealed record RecoveryRestoreSelection
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public RecoveryRestoreSection Sections { get; }
    public string Provenance { get; }

    public RecoveryRestoreSelection(Guid id, int schemaVersion, RecoveryRestoreSection sections, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || sections == RecoveryRestoreSection.None ||
            (sections & ~RecoveryRestoreSection.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(sections),
                "Recovery selection requires a stable UUID, schema v1 and at least one supported section.");
        Id = id;
        SchemaVersion = schemaVersion;
        Sections = sections;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record RecoveryRuntimeResolution(CrashRecoveryPolicy? Policy,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Available => Policy is not null;
}

public static class RecoveryRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.recovery.required_missing";

    public static RecoveryRuntimeResolution Resolve(CrashRecoveryPolicy? policy, bool required) =>
        policy is not null || !required
            ? new RecoveryRuntimeResolution(policy, [])
            : new RecoveryRuntimeResolution(null,
            [
                new DomainDiagnostic(RequiredMissingCode,
                    "The required crash-recovery extension is missing.",
                    "module/runtimePolicies/extensions/operationalReadiness/recovery"),
            ]);
}

public sealed record ContentSynchronizationPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Resumable { get; }
    public bool DeduplicateByHash { get; }
    public bool VerifyContent { get; }
    public bool RequireTrustedSigner { get; }
    public string StorageDirectory { get; }
    public long MaximumCacheBytes { get; }
    public IReadOnlyList<string> AllowedLicenses { get; }
    public IReadOnlyList<string> TrustedSignerFingerprints { get; }
    public string Provenance { get; }

    public ContentSynchronizationPolicy(Guid id, int schemaVersion, bool resumable, bool deduplicateByHash,
        bool verifyContent, bool requireTrustedSigner, string storageDirectory, long maximumCacheBytes,
        IReadOnlyList<string> allowedLicenses, IReadOnlyList<string> trustedSignerFingerprints, string provenance)
    {
        ArgumentNullException.ThrowIfNull(allowedLicenses);
        ArgumentNullException.ThrowIfNull(trustedSignerFingerprints);
        var storage = DataDrivenRuleSchema.RequireText(storageDirectory, nameof(storageDirectory)).Replace('\\', '/').Trim('/');
        var licenses = allowedLicenses.Select(item => DataDrivenRuleSchema.RequireText(item, nameof(allowedLicenses)).ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var fingerprints = trustedSignerFingerprints.Select(item => DataDrivenRuleSchema.RequireText(item, nameof(trustedSignerFingerprints)).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || maximumCacheBytes is < 1_048_576 or > 549_755_813_888 ||
            Path.IsPathRooted(storage) || storage.Length == 0 || storage.Split('/').Any(segment => segment is "." or "..") ||
            licenses.Length == 0 || fingerprints.Any(item => item.Length != 64 || item.Any(character => !Uri.IsHexDigit(character))) ||
            requireTrustedSigner && fingerprints.Length == 0)
            throw new ArgumentException("Content synchronization requires safe storage, licenses, trust and bounded cache settings.");
        Id = id;
        SchemaVersion = schemaVersion;
        Resumable = resumable;
        DeduplicateByHash = deduplicateByHash;
        VerifyContent = verifyContent;
        RequireTrustedSigner = requireTrustedSigner;
        StorageDirectory = storage;
        MaximumCacheBytes = maximumCacheBytes;
        AllowedLicenses = licenses;
        TrustedSignerFingerprints = fingerprints;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record OperationalReadinessProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<AgentTaskManifest> TaskManifests { get; }
    public CrashArtifactPolicy CrashArtifacts { get; }
    public ProjectHealthPolicy ProjectHealth { get; }
    public CrashRecoveryPolicy Recovery { get; }
    public ContentSynchronizationPolicy ContentSynchronization { get; }
    public string Provenance { get; }

    public OperationalReadinessProfile(Guid id, int schemaVersion, IReadOnlyList<AgentTaskManifest> taskManifests,
        CrashArtifactPolicy crashArtifacts, ProjectHealthPolicy projectHealth, CrashRecoveryPolicy recovery,
        ContentSynchronizationPolicy contentSynchronization, string provenance)
    {
        ArgumentNullException.ThrowIfNull(taskManifests);
        ArgumentNullException.ThrowIfNull(crashArtifacts);
        ArgumentNullException.ThrowIfNull(projectHealth);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(contentSynchronization);
        var tasks = taskManifests.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var ids = tasks.Select(item => item.Id).Append(id).Append(crashArtifacts.Id).Append(projectHealth.Id)
            .Append(recovery.Id).Append(contentSynchronization.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ids.Distinct().Count() != ids.Length ||
            tasks.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != tasks.Length)
            throw new ArgumentException("Operational readiness requires schema v1 and unique stable UUIDs and task keys.");
        Id = id;
        SchemaVersion = schemaVersion;
        TaskManifests = tasks;
        CrashArtifacts = crashArtifacts;
        ProjectHealth = projectHealth;
        Recovery = recovery;
        ContentSynchronization = contentSynchronization;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static OperationalReadinessProfile CreateDefault(Guid moduleId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(moduleId, $"operational-readiness:{key}");
        var task = new AgentTaskManifest(Stable("task:validate-module"), 1, "validate-module",
            "Validate the module without changing canonical source files.", ["project.json", "areas"],
            ["Divers/OpenRPG_Forge_Aurora_Feature_Dossier"], ["forge package validate", "forge docs check"],
            "project-maintainers", [], "migration-default");
        return new OperationalReadinessProfile(Stable("root"), 1, [task],
            new CrashArtifactPolicy(Stable("crash-artifacts"), 1, true, CrashDumpAdapterKind.Managed,
                2_000, 33_554_432, true, true, true,
                ["authorization", "cookie", "password", "secret", "token"], "migration-default"),
            new ProjectHealthPolicy(Stable("project-health"), 1,
                ["assets", "build", "content", "migrations", "performance", "tests"],
                true, true, true, true, true, true, "migration-default"),
            new CrashRecoveryPolicy(Stable("recovery"), 1, 60, 12, true, true, true, "migration-default"),
            new ContentSynchronizationPolicy(Stable("content-sync"), 1, true, true, true, false,
                ".forge/content-cache", 10_737_418_240, ["CC0-1.0"], [], "migration-default"),
            "migration-default");
    }
}

public static class OperationalReadinessValidator
{
    public const string MissingTaskCode = "runtime.readiness.task_missing";
    public const string TaskScopeCode = "runtime.readiness.task_scope_empty";
    public const string TaskAcceptanceCode = "runtime.readiness.task_acceptance_empty";
    public const string TaskDependencyCode = "runtime.readiness.task_dependency_missing";
    public const string TaskDependencyCycleCode = "runtime.readiness.task_dependency_cycle";

    public static IReadOnlyList<DomainDiagnostic> Validate(OperationalReadinessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.TaskManifests.Count == 0)
            diagnostics.Add(new DomainDiagnostic(MissingTaskCode, "At least one bounded task manifest is required.",
                "module/runtimePolicies/extensions/operationalReadiness/taskManifests"));
        var ids = profile.TaskManifests.Select(item => item.Id).ToHashSet();
        foreach (var task in profile.TaskManifests)
        {
            var path = $"module/runtimePolicies/extensions/operationalReadiness/taskManifests/{task.Id:D}";
            if (task.ScopePaths.Count == 0)
                diagnostics.Add(new DomainDiagnostic(TaskScopeCode, "A task manifest requires at least one scoped path.", $"{path}/scopePaths"));
            if (task.AcceptanceTests.Count == 0)
                diagnostics.Add(new DomainDiagnostic(TaskAcceptanceCode, "A task manifest requires at least one acceptance test.", $"{path}/acceptanceTests"));
            foreach (var dependency in task.Dependencies.Where(dependency => !ids.Contains(dependency)))
                diagnostics.Add(new DomainDiagnostic(TaskDependencyCode, $"Task dependency '{dependency:D}' does not resolve.", $"{path}/dependencies"));
        }
        if (ContainsCycle(profile.TaskManifests))
            diagnostics.Add(new DomainDiagnostic(TaskDependencyCycleCode, "Task manifest dependencies must remain acyclic.",
                "module/runtimePolicies/extensions/operationalReadiness/taskManifests"));
        return diagnostics;
    }

    private static bool ContainsCycle(IReadOnlyList<AgentTaskManifest> tasks)
    {
        var dependencies = tasks.ToDictionary(item => item.Id, item => item.Dependencies.Where(dependency =>
            tasks.Any(candidate => candidate.Id == dependency)).ToArray());
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (visiting.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            visiting.Add(id);
            foreach (var dependency in dependencies[id]) if (Visit(dependency)) return true;
            visiting.Remove(id);
            return false;
        }
        return dependencies.Keys.Any(Visit);
    }
}
