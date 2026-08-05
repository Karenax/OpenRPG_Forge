using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;

namespace Forge.Application.Validation;

public enum ProjectHealthCategory
{
    Diagnostics,
    Tests,
    Performance,
    Licenses,
    Migrations,
    Coverage,
}

public enum ProjectHealthSeverity
{
    Information,
    Warning,
    Error,
}

public enum ProjectHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}

public sealed record ProjectHealthSignal(string Domain, ProjectHealthCategory Category,
    ProjectHealthSeverity Severity, string Code, string Detail, string DocumentPath, bool Passed);

public sealed record ProjectHealthDomainReport(string Domain, ProjectHealthStatus Status,
    int Passed, int Warning, int Failed, IReadOnlyList<ProjectHealthSignal> Signals);

public sealed record ProjectHealthReport(Guid Id, int SchemaVersion, ProjectHealthStatus Status,
    DateTimeOffset CheckedAt, string Fingerprint, IReadOnlyList<ProjectHealthDomainReport> Domains,
    bool BuildAllowed);

public static class ProjectHealthReportService
{
    public const int CurrentSchemaVersion = 1;

    public static ProjectHealthReport Evaluate(ProjectDefinition project, DateTimeOffset checkedAt,
        IReadOnlyList<ProjectHealthSignal>? externalSignals = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var policy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.ProjectHealth;
        var signals = new List<ProjectHealthSignal>();
        signals.AddRange(ProjectValidator.Validate(project).Select(diagnostic =>
            new ProjectHealthSignal("content", ProjectHealthCategory.Diagnostics, ProjectHealthSeverity.Error,
                diagnostic.Code, diagnostic.Message, diagnostic.Path, false)));
        if (signals.Count == 0)
            signals.Add(new ProjectHealthSignal("content", ProjectHealthCategory.Diagnostics,
                ProjectHealthSeverity.Information, "health.project.valid", "Canonical project validation passed.",
                "project", true));

        if (policy.IncludeTestStatus)
            signals.Add(new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
                externalSignals?.Any(item => item.Category == ProjectHealthCategory.Tests) == true
                    ? ProjectHealthSeverity.Information : ProjectHealthSeverity.Warning,
                externalSignals?.Any(item => item.Category == ProjectHealthCategory.Tests) == true
                    ? "health.tests.attached" : "health.tests.evidence_missing",
                externalSignals?.Any(item => item.Category == ProjectHealthCategory.Tests) == true
                    ? "External test evidence is attached." : "No test execution evidence is attached to this report.",
                "health/tests", externalSignals?.Any(item => item.Category == ProjectHealthCategory.Tests) == true));
        if (policy.IncludePerformance)
            signals.Add(new ProjectHealthSignal("performance", ProjectHealthCategory.Performance,
                ProjectHealthSeverity.Information, "health.performance.instrumented",
                "ForgeScript compiler telemetry is enabled.", "module/runtimePolicies/extensions/modernTooling/compiler",
                project.Module.RuntimePolicies.Extensions.ModernTooling.Compiler.PerformanceTelemetry));
        if (policy.IncludeLicenses)
        {
            var invalidLicenses = project.Blueprints.Where(item => item.PaletteOrigin == BlueprintPaletteOrigin.InstalledPack &&
                string.Equals(item.Provenance.LicenseSpdx, "UNLICENSED", StringComparison.OrdinalIgnoreCase)).ToArray();
            signals.Add(new ProjectHealthSignal("assets", ProjectHealthCategory.Licenses,
                invalidLicenses.Length == 0 ? ProjectHealthSeverity.Information : ProjectHealthSeverity.Error,
                invalidLicenses.Length == 0 ? "health.licenses.valid" : "health.licenses.invalid",
                invalidLicenses.Length == 0 ? "Distribution asset licenses are known." :
                    $"{invalidLicenses.Length} installed blueprint license(s) are unknown.",
                "module/content/blueprints", invalidLicenses.Length == 0));
        }
        if (policy.IncludeMigrations)
            signals.Add(new ProjectHealthSignal("migrations", ProjectHealthCategory.Migrations,
                ProjectHealthSeverity.Information, "health.migrations.current",
                "The project was loaded through the current serializer contract.", "project/formatVersion", true));
        if (policy.IncludeCoverage)
            signals.Add(new ProjectHealthSignal("build", ProjectHealthCategory.Coverage,
                project.Module.RuntimePolicies.Extensions.OperationalReadiness.TaskManifests.Count > 0
                    ? ProjectHealthSeverity.Information : ProjectHealthSeverity.Error,
                "health.tasks.coverage", "Operational work is bounded by task manifests.",
                "module/runtimePolicies/extensions/operationalReadiness/taskManifests",
                project.Module.RuntimePolicies.Extensions.OperationalReadiness.TaskManifests.Count > 0));
        if (externalSignals is not null) signals.AddRange(externalSignals);
        return Build(policy, signals, checkedAt);
    }

    public static ProjectHealthReport Build(ProjectHealthPolicy policy,
        IEnumerable<ProjectHealthSignal> signals, DateTimeOffset checkedAt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(signals);
        var included = signals.Where(item => Include(policy, item.Category))
            .Select(Normalize).OrderBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.Category).ThenBy(item => item.Code, StringComparer.Ordinal).ToList();
        foreach (var domain in policy.Domains.Where(domain => included.All(item => item.Domain != domain)))
            included.Add(new ProjectHealthSignal(domain, ProjectHealthCategory.Diagnostics,
                ProjectHealthSeverity.Warning, "health.domain.evidence_missing",
                $"No health evidence is available for domain '{domain}'.", $"health/domains/{domain}", false));

        var domains = included.GroupBy(item => item.Domain, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderBy(item => item.Category).ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
                return new ProjectHealthDomainReport(group.Key, Status(ordered),
                    ordered.Count(item => item.Passed),
                    ordered.Count(item => !item.Passed && item.Severity == ProjectHealthSeverity.Warning),
                    ordered.Count(item => !item.Passed && item.Severity == ProjectHealthSeverity.Error), ordered);
            }).ToArray();
        var status = domains.Length == 0 ? ProjectHealthStatus.Degraded : domains.Max(item => item.Status);
        var canonical = string.Join('\n', domains.SelectMany(domain => domain.Signals.Select(signal =>
            $"{domain.Domain}|{signal.Category}|{signal.Severity}|{signal.Code}|{signal.Passed}|{signal.DocumentPath}|{signal.Detail}")));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new ProjectHealthReport(policy.Id, CurrentSchemaVersion, status, checkedAt.ToUniversalTime(), fingerprint,
            domains, !policy.FailBuildWhenUnhealthy || status != ProjectHealthStatus.Unhealthy);
    }

    private static bool Include(ProjectHealthPolicy policy, ProjectHealthCategory category) => category switch
    {
        ProjectHealthCategory.Tests => policy.IncludeTestStatus,
        ProjectHealthCategory.Performance => policy.IncludePerformance,
        ProjectHealthCategory.Licenses => policy.IncludeLicenses,
        ProjectHealthCategory.Migrations => policy.IncludeMigrations,
        ProjectHealthCategory.Coverage => policy.IncludeCoverage,
        _ => true,
    };

    private static ProjectHealthSignal Normalize(ProjectHealthSignal signal)
    {
        var domain = NormalizeKey(signal.Domain, nameof(signal.Domain));
        var code = NormalizeKey(signal.Code, nameof(signal.Code));
        var detail = RequireText(signal.Detail, nameof(signal.Detail));
        var path = AgentTaskManifest.NormalizePaths([signal.DocumentPath], nameof(signal.DocumentPath))[0];
        return signal with { Domain = domain, Code = code, Detail = detail, DocumentPath = path };
    }

    private static string NormalizeKey(string value, string parameter)
    {
        var normalized = RequireText(value, parameter).ToLowerInvariant();
        if (normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            throw new ArgumentException("Health keys may contain only ASCII letters, digits, dots, dashes and underscores.", parameter);
        return normalized;
    }

    private static string RequireText(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value.Trim();
    }

    private static ProjectHealthStatus Status(IReadOnlyList<ProjectHealthSignal> signals)
    {
        if (signals.Any(item => !item.Passed && item.Severity == ProjectHealthSeverity.Error))
            return ProjectHealthStatus.Unhealthy;
        if (signals.Any(item => !item.Passed)) return ProjectHealthStatus.Degraded;
        return ProjectHealthStatus.Healthy;
    }
}
