namespace Forge.Domain.Build;

public sealed record WorkspaceBuildProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("b0010000-0000-4000-8000-000000000001");
    public static readonly IReadOnlyList<string> RequiredCliOperations =
        ["build", "validate", "test", "pack", "migrate", "normalize", "diff", "references"];

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool RequireGitRepository { get; }
    public bool TextDocuments { get; }
    public bool ExplicitGeneratedFiles { get; }
    public bool ReproducibleAssets { get; }
    public IReadOnlyDictionary<string, string> Layout { get; }
    public IReadOnlyList<string> CliOperations { get; }
    public bool StableOrdering { get; }
    public bool NormalizedTimestamps { get; }
    public bool LockedDependencies { get; }
    public bool ContentHashes { get; }
    public bool VerifyReproducibility { get; }
    public bool IncrementalBuild { get; }
    public string CachePath { get; }
    public bool RebuildAffectedOnly { get; }
    public WorkspaceBuildGovernanceProfile Governance { get; }
    public WorkspaceBuildCollaborationProfile Collaboration { get; }
    public ProjectCompatibilityProfile Compatibility { get; }

    public WorkspaceBuildProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requireGitRepository,
        bool textDocuments,
        bool explicitGeneratedFiles,
        bool reproducibleAssets,
        IReadOnlyDictionary<string, string>? layout,
        IReadOnlyList<string>? cliOperations,
        bool stableOrdering,
        bool normalizedTimestamps,
        bool lockedDependencies,
        bool contentHashes,
        bool verifyReproducibility,
        bool incrementalBuild,
        string cachePath,
        bool rebuildAffectedOnly,
        WorkspaceBuildGovernanceProfile? governance = null,
        WorkspaceBuildCollaborationProfile? collaboration = null,
        ProjectCompatibilityProfile? compatibility = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = (provenance ?? string.Empty).Trim();
        RequireGitRepository = requireGitRepository;
        TextDocuments = textDocuments;
        ExplicitGeneratedFiles = explicitGeneratedFiles;
        ReproducibleAssets = reproducibleAssets;
        Layout = (layout ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => Normalize(item.Value), StringComparer.Ordinal);
        CliOperations = (cliOperations ?? []).Select(item => item.Trim().ToLowerInvariant())
            .Order(StringComparer.Ordinal).ToArray();
        StableOrdering = stableOrdering;
        NormalizedTimestamps = normalizedTimestamps;
        LockedDependencies = lockedDependencies;
        ContentHashes = contentHashes;
        VerifyReproducibility = verifyReproducibility;
        IncrementalBuild = incrementalBuild;
        CachePath = Normalize(cachePath);
        RebuildAffectedOnly = rebuildAffectedOnly;
        Governance = governance ?? WorkspaceBuildGovernanceProfile.CreateDefault();
        Collaboration = collaboration ?? WorkspaceBuildCollaborationProfile.CreateDefault();
        Compatibility = compatibility ?? ProjectCompatibilityProfile.CreateDefault();
    }

    public static WorkspaceBuildProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:p0-r001",
        true,
        true,
        true,
        true,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assets"] = "assets",
            ["cache"] = ".forge",
            ["data"] = "data",
            ["docs"] = "docs",
            ["localization"] = "localization",
            ["packages"] = "packages",
            ["scripts"] = "scripts",
            ["tests"] = "tests",
        },
        RequiredCliOperations,
        true,
        true,
        true,
        true,
        true,
        true,
        ".forge",
        true,
        WorkspaceBuildGovernanceProfile.CreateDefault(),
        WorkspaceBuildCollaborationProfile.CreateDefault(),
        ProjectCompatibilityProfile.CreateDefault());

    private static string Normalize(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
}
