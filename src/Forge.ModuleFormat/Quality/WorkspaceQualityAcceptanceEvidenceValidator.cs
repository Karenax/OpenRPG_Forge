using System.Text.Json;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Quality;

public sealed record WorkspaceQualityAcceptanceEvidenceReport(
    bool Passed,
    int FixtureCount,
    int FeatureCount,
    int GateAssignmentCount,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class MaliciousModuleFixtureProbe
{
    public const string HugeArrayCode = "qa.fixture.huge_array_rejected";
    public const string InjectionTextCode = "qa.fixture.injection_text_rejected";
    public const string InvalidAssetCode = "qa.fixture.invalid_asset_rejected";

    public static DomainDiagnostic? InspectHugeArray(int entries, MaliciousModuleFixturePolicy policy) =>
        entries > policy.MaximumArrayEntries
            ? new DomainDiagnostic(HugeArrayCode,
                $"Array fixture has {entries} entries; maximum is {policy.MaximumArrayEntries}.",
                "fixtures/huge-array/payload")
            : null;

    public static DomainDiagnostic? InspectInjectionText(string text, MaliciousModuleFixturePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hostile = text.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("${", StringComparison.Ordinal);
        return policy.RejectInjectionText && hostile
            ? new DomainDiagnostic(InjectionTextCode, "Injection-text fixture was rejected.",
                "fixtures/injection-text/payload")
            : null;
    }

    public static DomainDiagnostic? InspectAssetHeader(byte[] bytes, MaliciousModuleFixturePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return policy.RejectInvalidAssets &&
               !UntrustedAssetHeaderValidator.IsSupportedImage(bytes, bytes.Length + 1) &&
               !UntrustedAssetHeaderValidator.IsSupportedModel(bytes, bytes.Length + 1)
            ? new DomainDiagnostic(InvalidAssetCode, "Invalid asset fixture was rejected.",
                "fixtures/invalid-asset/payload")
            : null;
    }
}

public static class WorkspaceQualityAcceptanceEvidenceValidator
{
    public const string RepositoryRootInvalidCode = "qa.acceptance.repository_root_invalid";
    public const string FixtureCatalogInvalidCode = "qa.malicious_fixtures.catalog_invalid";
    public const string FixtureEvidenceInvalidCode = "qa.malicious_fixtures.evidence_invalid";
    public const string CoverageTrackerInvalidCode = "qa.aurora_coverage.tracker_invalid";
    public const string GateMatrixInvalidCode = "qa.acceptance_gates.matrix_invalid";
    public const string AcceptanceProgramInvalidCode = "qa.acceptance_gates.program_invalid";

    public static WorkspaceQualityAcceptanceEvidenceReport Validate(
        string repositoryRoot,
        QualityAssuranceProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var root = Path.GetFullPath(repositoryRoot);
        var diagnostics = QualityAcceptanceProfileValidator.Validate(profile).ToList();
        var gitPath = Path.Combine(root, ".git");
        if (!Directory.Exists(root) || (!Directory.Exists(gitPath) && !File.Exists(gitPath)))
        {
            diagnostics.Add(Diagnostic(RepositoryRootInvalidCode, "Repository root is missing or is not a Git checkout.", root));
            return new(false, 0, 0, 0, diagnostics);
        }

        var fixtures = ValidateFixtures(root, profile.MaliciousFixtures, diagnostics);
        var features = ValidateCoverage(root, profile.AuroraParityCoverage, diagnostics);
        var gates = ValidateGates(root, profile.AcceptanceGates, diagnostics);
        return new(diagnostics.Count == 0, fixtures, features, gates, diagnostics);
    }

    private static int ValidateFixtures(string root, MaliciousModuleFixturePolicy policy,
        List<DomainDiagnostic> diagnostics)
    {
        var path = Resolve(root, policy.CatalogPath);
        MaliciousFixtureCatalog? catalog = null;
        try
        {
            if (File.Exists(path))
                catalog = JsonSerializer.Deserialize<MaliciousFixtureCatalog>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            // Report the stable catalog diagnostic below.
        }

        var fixtures = catalog?.Fixtures ?? [];
        if (catalog is null || catalog.SchemaVersion != 1 || string.IsNullOrWhiteSpace(catalog.Provenance) ||
            fixtures.Count != MaliciousModuleFixturePolicy.RequiredFamilies.Count ||
            fixtures.Select(item => item.Id).Distinct().Count() != fixtures.Count ||
            fixtures.Select(item => item.Family).Distinct(StringComparer.Ordinal).Count() != fixtures.Count ||
            !MaliciousModuleFixturePolicy.RequiredFamilies.SetEquals(fixtures.Select(item => item.Family)) ||
            fixtures.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Payload) || string.IsNullOrWhiteSpace(item.ExpectedDiagnostic) ||
                string.IsNullOrWhiteSpace(item.Provenance) || !SafeRelativePath(item.EvidencePath) ||
                string.IsNullOrWhiteSpace(item.EvidenceMarker)))
        {
            diagnostics.Add(Diagnostic(FixtureCatalogInvalidCode,
                "Malicious fixture catalog must contain the seven unique, evidenced schema-v1 families.",
                policy.CatalogPath));
            return fixtures.Count;
        }

        foreach (var fixture in fixtures)
        {
            var evidence = Resolve(root, fixture.EvidencePath);
            if (!File.Exists(evidence) || !File.ReadAllText(evidence).Contains(fixture.EvidenceMarker, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(FixtureEvidenceInvalidCode,
                    $"Fixture '{fixture.Family}' has no executable evidence marker '{fixture.EvidenceMarker}'.",
                    fixture.EvidencePath));
            }
        }

        return fixtures.Count;
    }

    private static int ValidateCoverage(string root, AuroraParityCoveragePolicy policy,
        List<DomainDiagnostic> diagnostics)
    {
        var path = Resolve(root, policy.TrackerPath);
        var rows = ReadDelimited(path);
        if (rows.Count == 0)
        {
            diagnostics.Add(Diagnostic(CoverageTrackerInvalidCode, "Aurora parity tracker is missing or empty.", policy.TrackerPath));
            return 0;
        }

        var header = rows[0];
        var featureIndex = Array.IndexOf(header, "feature_id");
        var statusIndex = Array.IndexOf(header, "status");
        var evidenceIndex = Array.IndexOf(header, "test_evidence");
        var values = rows.Skip(1).Where(row => row.Length == header.Length).ToArray();
        var invalid = featureIndex < 0 || statusIndex < 0 || evidenceIndex < 0 ||
            values.Length != policy.ExpectedFeatureCount ||
            values.Select(row => row[featureIndex]).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(row => !policy.AllowedStatuses.Contains(row[statusIndex], StringComparer.Ordinal)) ||
            policy.RequireEvidenceForTested && values.Any(row =>
                string.Equals(row[statusIndex], "Tested", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(row[evidenceIndex]));
        if (invalid)
            diagnostics.Add(Diagnostic(CoverageTrackerInvalidCode,
                "Aurora parity tracker must contain 717 unique features, governed statuses and evidence for Tested.",
                policy.TrackerPath));
        return values.Length;
    }

    private static int ValidateGates(string root, AcceptanceGatePolicy policy,
        List<DomainDiagnostic> diagnostics)
    {
        var path = Resolve(root, policy.GateMatrixPath);
        var rows = ReadDelimited(path);
        if (rows.Count == 0)
        {
            diagnostics.Add(Diagnostic(GateMatrixInvalidCode, "Acceptance gate matrix is missing or empty.", policy.GateMatrixPath));
            return 0;
        }

        var header = rows[0];
        var featureIndex = Array.IndexOf(header, "feature_id");
        var gateIndex = Array.IndexOf(header, "target_gate");
        var values = rows.Skip(1).Where(row => row.Length == header.Length).ToArray();
        if (featureIndex < 0 || gateIndex < 0 || values.Length != 717 ||
            values.Select(row => row[featureIndex]).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Any(row => !policy.RequiredGates.Contains(row[gateIndex], StringComparer.Ordinal)))
        {
            diagnostics.Add(Diagnostic(GateMatrixInvalidCode,
                "Gate matrix must assign each of the 717 unique features to a configured gate.", policy.GateMatrixPath));
        }

        var programPath = Path.Combine(root, "docs", "planning", "P0_ACCEPTANCE_PROGRAM.md");
        var program = File.Exists(programPath) ? File.ReadAllText(programPath) : string.Empty;
        if (!policy.RequireLinkedTests || !policy.RequireLinkedDocumentation || !policy.RequireFeatureEvidence ||
            !program.Contains("Relire intégralement", StringComparison.Ordinal) ||
            !program.Contains("tests ciblés", StringComparison.Ordinal) ||
            !program.Contains("preuve spécifique", StringComparison.Ordinal) ||
            !program.Contains("scripts/Verify.ps1", StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(AcceptanceProgramInvalidCode,
                "P0 acceptance program must require source audit, targeted tests, feature evidence, documentation and the global gate.",
                "docs/planning/P0_ACCEPTANCE_PROGRAM.md"));
        }

        return values.Length;
    }

    private static IReadOnlyList<string[]> ReadDelimited(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseDelimitedLine).ToArray();
    }

    private static string[] ParseDelimitedLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == ';' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else current.Append(character);
        }
        values.Add(current.ToString());
        return values.ToArray();
    }

    private static string Resolve(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static bool SafeRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        !value.Replace('\\', '/').Split('/').Any(segment => segment is "" or "." or "..");

    private static DomainDiagnostic Diagnostic(string code, string message, string path) => new(code, message, path);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record MaliciousFixtureCatalog(int SchemaVersion, string Provenance,
        IReadOnlyList<MaliciousFixture> Fixtures);

    private sealed record MaliciousFixture(Guid Id, int SchemaVersion, string Family, string Payload,
        string ExpectedDiagnostic, string EvidencePath, string EvidenceMarker, string Provenance);
}
