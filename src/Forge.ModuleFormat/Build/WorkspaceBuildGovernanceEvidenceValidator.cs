using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Build;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Build;

public static class WorkspaceBuildGovernanceEvidenceValidator
{
    public const string WorktreeEvidenceMissingCode = "build.governance.worktree_evidence_missing";
    public const string CiEvidenceMissingCode = "build.governance.ci_evidence_missing";
    public const string FixtureEvidenceMissingCode = "build.governance.fixture_evidence_missing";
    public const string VersioningEvidenceMissingCode = "build.governance.versioning_evidence_missing";
    public const string AdrEvidenceMissingCode = "build.governance.adr_evidence_missing";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<DomainDiagnostic> Validate(
        string repositoryRoot,
        WorkspaceBuildGovernanceProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var root = Path.GetFullPath(repositoryRoot);
        var diagnostics = WorkspaceBuildGovernanceValidator.Validate(profile).ToList();
        if (diagnostics.Count > 0) return diagnostics;

        ValidateWorktrees(root, profile, diagnostics);
        ValidateCi(root, profile, diagnostics);
        ValidateFixtures(root, profile, diagnostics);
        ValidateVersioning(root, profile, diagnostics);
        ValidateAdrs(root, profile, diagnostics);
        return diagnostics;
    }

    private static void ValidateWorktrees(
        string root,
        WorkspaceBuildGovernanceProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var policyPath = Path.Combine(root, "docs", "governance", "WORKTREE_AND_AGENT_POLICY.md");
        var gitPath = Path.Combine(root, ".git");
        if ((!File.Exists(gitPath) && !Directory.Exists(gitPath)) || !File.Exists(policyPath))
        {
            Add(diagnostics, WorktreeEvidenceMissingCode, "Versioned worktree policy or Git baseline is missing.",
                "docs/governance/WORKTREE_AND_AGENT_POLICY.md");
            return;
        }
        var content = File.ReadAllText(policyPath);
        var required = new[] { profile.Worktrees.BranchPrefix, profile.Worktrees.OwnershipManifestPath,
            "périmètres de fichiers", "tracker", "backlog", "graphe", "scripts/Verify.ps1" };
        if (required.Any(value => !content.Contains(value, StringComparison.OrdinalIgnoreCase)))
            Add(diagnostics, WorktreeEvidenceMissingCode, "Worktree policy does not cover ownership, file scopes and integration checks.",
                "docs/governance/WORKTREE_AND_AGENT_POLICY.md");
    }

    private static void ValidateCi(
        string root,
        WorkspaceBuildGovernanceProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var path = Resolve(root, profile.ContinuousIntegration.WorkflowPath);
        if (!File.Exists(path))
        {
            Add(diagnostics, CiEvidenceMissingCode, "Configured CI workflow is missing.",
                profile.ContinuousIntegration.WorkflowPath);
            return;
        }
        var content = File.ReadAllText(path);
        var required = new[]
        {
            "Update-AuroraParityTracker.ps1 -Check",
            "Update-CompleteFunctionBacklog.ps1 -Check",
            "docs check",
            "Build-ForgeScriptNative.ps1",
            "dotnet build",
            "dotnet test",
            "build apps/Forge.Runtime/content/crypt-of-ashes",
            "package validate",
            "package compliance",
            "governance run",
        };
        if (required.Any(value => !content.Contains(value, StringComparison.Ordinal)))
            Add(diagnostics, CiEvidenceMissingCode,
                "CI workflow must validate schemas, managed/native/script builds, tests, a sample package and licenses.",
                profile.ContinuousIntegration.WorkflowPath);
    }

    private static void ValidateFixtures(
        string root,
        WorkspaceBuildGovernanceProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var path = Resolve(root, profile.ReferenceFixtures.CatalogPath);
        try
        {
            var catalog = JsonSerializer.Deserialize<FixtureCatalogDocument>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Fixture catalog is empty.");
            var expected = profile.ReferenceFixtures.Fixtures.OrderBy(item => item.Kind).ToArray();
            var actual = catalog.Fixtures.OrderBy(item => item.Kind).ToArray();
            if (catalog.SchemaVersion != profile.SchemaVersion || catalog.ProfileId != profile.Id ||
                !string.Equals(catalog.Provenance, profile.Provenance, StringComparison.Ordinal) ||
                actual.Length != expected.Length || actual.Where((item, index) =>
                    item.Id != expected[index].Id || item.Kind != expected[index].Kind ||
                    !string.Equals(item.Name, expected[index].Name, StringComparison.Ordinal) ||
                    !string.Equals(item.RelativePath, expected[index].RelativePath, StringComparison.Ordinal) ||
                    !string.Equals(item.ExpectedOutcome, expected[index].ExpectedOutcome, StringComparison.Ordinal)).Any())
                throw new JsonException("Fixture catalog differs from the canonical profile.");

            foreach (var fixture in expected)
            {
                var fixturePath = Resolve(root, fixture.RelativePath);
                if (!File.Exists(fixturePath)) throw new FileNotFoundException("Fixture source is missing.", fixturePath);
                if (fixture.Kind is ReferenceFixtureKind.Tiny or ReferenceFixtureKind.VerticalSlice or ReferenceFixtureKind.Stress)
                {
                    var opened = ProjectSession.Open(Path.GetDirectoryName(fixturePath)!);
                    if (!opened.Succeeded || opened.Session is null)
                        throw new InvalidDataException(
                            $"Fixture '{fixture.Name}' did not open: {string.Join(" | ", opened.Diagnostics.Select(item => item.Code))}");
                }
                else
                {
                    var source = File.ReadAllText(fixturePath);
                    var marker = fixture.Kind == ReferenceFixtureKind.Malicious
                        ? "Validate_RejectsPathTraversalFixture"
                        : "Deserialize_MigratesLegacyProjectToModuleAggregate";
                    if (!source.Contains(marker, StringComparison.Ordinal))
                        throw new InvalidDataException($"Fixture marker '{marker}' is missing.");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException or ArgumentException)
        {
            Add(diagnostics, FixtureEvidenceMissingCode, exception.Message, profile.ReferenceFixtures.CatalogPath);
        }
    }

    private static void ValidateVersioning(
        string root,
        WorkspaceBuildGovernanceProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var policyPath = Path.Combine(root, "docs", "governance", "VERSIONING.md");
        var changelogPath = Resolve(root, profile.Versioning.ChangelogPath);
        if (!File.Exists(policyPath) || !File.Exists(changelogPath))
        {
            Add(diagnostics, VersioningEvidenceMissingCode, "Versioning policy or changelog is missing.",
                "docs/governance/VERSIONING.md");
            return;
        }
        var policy = File.ReadAllText(policyPath);
        var changelog = File.ReadAllText(changelogPath);
        if (!policy.Contains(profile.Versioning.CoreVersion, StringComparison.Ordinal) ||
            !policy.Contains(profile.Versioning.PackageVersion, StringComparison.Ordinal) ||
            !policy.Contains("schéma", StringComparison.OrdinalIgnoreCase) ||
            !changelog.Contains($"[{profile.Versioning.CoreVersion}]", StringComparison.Ordinal))
            Add(diagnostics, VersioningEvidenceMissingCode,
                "Versioning evidence must separate product/package SemVer, schemas, compatibility and changelog.",
                "docs/governance/VERSIONING.md");
    }

    private static void ValidateAdrs(
        string root,
        WorkspaceBuildGovernanceProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        var templatePath = Resolve(root, profile.ArchitectureDecisions.TemplatePath);
        var rulesPath = Path.Combine(Resolve(root, profile.ArchitectureDecisions.DirectoryPath), "README.md");
        if (!File.Exists(templatePath) || !File.Exists(rulesPath))
        {
            Add(diagnostics, AdrEvidenceMissingCode, "ADR template or update rules are missing.",
                profile.ArchitectureDecisions.TemplatePath);
            return;
        }
        var template = File.ReadAllText(templatePath);
        var rules = File.ReadAllText(rulesPath);
        var required = new[] { "Statut", "UUID", "Schéma", "Provenance", "## Décision", "## Preuves",
            "Tests", "Benchmarks", "## Conséquences", "## Remplacement" };
        if (required.Any(value => !template.Contains(value, StringComparison.Ordinal)) ||
            profile.ArchitectureDecisions.AllowedStatuses.Any(status =>
                !rules.Contains(status, StringComparison.OrdinalIgnoreCase)))
            Add(diagnostics, AdrEvidenceMissingCode,
                "ADR evidence must define formal statuses, tests, benchmarks and superseding rules.",
                profile.ArchitectureDecisions.TemplatePath);
    }

    private static string Resolve(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Governance evidence path escapes the repository root.");
        return path;
    }

    private static void Add(ICollection<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));

    private sealed record FixtureCatalogDocument(
        int SchemaVersion,
        Guid ProfileId,
        string Provenance,
        IReadOnlyList<FixtureCatalogEntry> Fixtures);

    private sealed record FixtureCatalogEntry(
        Guid Id,
        string Name,
        ReferenceFixtureKind Kind,
        string RelativePath,
        string ExpectedOutcome);
}
