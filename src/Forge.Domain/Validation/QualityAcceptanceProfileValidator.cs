using System.Text.RegularExpressions;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Domain.Validation;

public static partial class QualityAcceptanceProfileValidator
{
    public const string DiagnosticsInvalidCode = "qa.diagnostics.profile_invalid";
    public const string MaliciousFixturesInvalidCode = "qa.malicious_fixtures.profile_invalid";
    public const string AuroraCoverageInvalidCode = "qa.aurora_coverage.profile_invalid";
    public const string AcceptanceGatesInvalidCode = "qa.acceptance_gates.profile_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(QualityAssuranceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        ValidateDiagnostics(profile.ClickableDiagnostics, diagnostics);
        ValidateFixtures(profile.MaliciousFixtures, diagnostics);
        ValidateCoverage(profile.AuroraParityCoverage, diagnostics);
        ValidateGates(profile.AcceptanceGates, diagnostics);

        var ids = new[]
        {
            profile.ClickableDiagnostics.Id,
            profile.MaliciousFixtures.Id,
            profile.AuroraParityCoverage.Id,
            profile.AcceptanceGates.Id,
        };
        if (ids.Distinct().Count() != ids.Length)
        {
            diagnostics.Add(new DomainDiagnostic(AcceptanceGatesInvalidCode,
                "Quality acceptance policy UUIDs must be unique.",
                "module/runtimePolicies/extensions/modernTooling/qualityAssurance/acceptanceGates/id"));
        }

        return diagnostics;
    }

    private static void ValidateDiagnostics(ClickableDiagnosticPolicy value, List<DomainDiagnostic> diagnostics)
    {
        var categories = Normalized(value.Categories);
        var severities = Normalized(value.Severities);
        if (!ValidIdentity(value.Id, value.SchemaVersion, value.Provenance) ||
            !ClickableDiagnosticPolicy.SupportedProfiles.Contains(Normalize(value.ActiveProfile)) ||
            categories.Length == 0 || categories.Any(item => !ClickableDiagnosticPolicy.SupportedCategories.Contains(item)) ||
            severities.Length == 0 || severities.Any(item => !ClickableDiagnosticPolicy.SupportedSeverities.Contains(item)) ||
            !value.EnableQuickFixes)
        {
            diagnostics.Add(new DomainDiagnostic(DiagnosticsInvalidCode,
                "Clickable diagnostics require schema v1, a supported profile, categories, severities and guided fixes.",
                "module/runtimePolicies/extensions/modernTooling/qualityAssurance/clickableDiagnostics"));
        }
    }

    private static void ValidateFixtures(MaliciousModuleFixturePolicy value, List<DomainDiagnostic> diagnostics)
    {
        var families = Normalized(value.Families);
        if (!ValidIdentity(value.Id, value.SchemaVersion, value.Provenance) || !SafeRelativePath(value.CatalogPath) ||
            !MaliciousModuleFixturePolicy.RequiredFamilies.SetEquals(families) ||
            value.MaximumArrayEntries is < 1_024 or > 1_000_000 ||
            !value.RejectInjectionText || !value.RejectInvalidAssets)
        {
            diagnostics.Add(new DomainDiagnostic(MaliciousFixturesInvalidCode,
                "Malicious fixtures require schema v1, the seven governed families and bounded hostile inputs.",
                "module/runtimePolicies/extensions/modernTooling/qualityAssurance/maliciousFixtures"));
        }
    }

    private static void ValidateCoverage(AuroraParityCoveragePolicy value, List<DomainDiagnostic> diagnostics)
    {
        var statuses = value.AllowedStatuses?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (!ValidIdentity(value.Id, value.SchemaVersion, value.Provenance) || !SafeRelativePath(value.TrackerPath) ||
            !AuroraParityCoveragePolicy.SupportedStatuses.SetEquals(statuses) || value.ExpectedFeatureCount != 717 ||
            !value.RequireEvidenceForTested)
        {
            diagnostics.Add(new DomainDiagnostic(AuroraCoverageInvalidCode,
                "Aurora parity requires the canonical tracker, all governed statuses, 717 unique features and evidence for Tested.",
                "module/runtimePolicies/extensions/modernTooling/qualityAssurance/auroraParityCoverage"));
        }
    }

    private static void ValidateGates(AcceptanceGatePolicy value, List<DomainDiagnostic> diagnostics)
    {
        var gates = value.RequiredGates?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var requiredAuroraGates = Enumerable.Range(0, 9).Select(index => $"G{index}").ToHashSet(StringComparer.Ordinal);
        if (!ValidIdentity(value.Id, value.SchemaVersion, value.Provenance) || !SafeRelativePath(value.GateMatrixPath) ||
            gates.Any(item => !GatePattern().IsMatch(item)) || !requiredAuroraGates.IsSubsetOf(gates) ||
            !value.RequireLinkedTests || !value.RequireLinkedDocumentation || !value.RequireFeatureEvidence)
        {
            diagnostics.Add(new DomainDiagnostic(AcceptanceGatesInvalidCode,
                "Acceptance gates require G0-G8, linked tests, linked documentation and feature evidence.",
                "module/runtimePolicies/extensions/modernTooling/qualityAssurance/acceptanceGates"));
        }
    }

    private static bool ValidIdentity(Guid id, int schemaVersion, string provenance) =>
        id != Guid.Empty && schemaVersion == 1 && !string.IsNullOrWhiteSpace(provenance);

    private static string[] Normalized(IReadOnlyList<string>? values) => values?
        .Where(item => !string.IsNullOrWhiteSpace(item)).Select(Normalize)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool SafeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return false;
        var normalized = value.Replace('\\', '/');
        return !normalized.Split('/').Any(segment => segment is "" or "." or "..");
    }

    [GeneratedRegex("^G(?:0|[1-9][0-9]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex GatePattern();
}
