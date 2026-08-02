using System.Text.RegularExpressions;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static partial class DistributionComplianceValidator
{
    public const string TargetDisabledCode = "distribution.target.disabled";
    public const string ModuleLicenseUnknownCode = "distribution.module.license_unknown";
    public const string ModuleProvenanceUnknownCode = "distribution.module.provenance_unknown";
    public const string BlueprintLicenseUnknownCode = "distribution.blueprint.license_unknown";
    public const string BlueprintProvenanceUnknownCode = "distribution.blueprint.provenance_unknown";
    public const string DependencyLicenseUnknownCode = "distribution.dependency.license_unknown";
    public const string DependencyVersionInvalidCode = "distribution.dependency.version_invalid";

    private static readonly HashSet<string> ApprovedLicenseIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0", "CC-BY-4.0",
        "CC-BY-SA-4.0", "GPL-2.0-only", "GPL-2.0-or-later", "GPL-3.0-only", "GPL-3.0-or-later",
        "LGPL-2.1-only", "LGPL-2.1-or-later", "LGPL-3.0-only", "LGPL-3.0-or-later", "MPL-2.0",
        "OFL-1.1", "ISC", "Zlib", "Unlicense", "LicenseRef-Proprietary",
    };

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project, ModuleTargetKind target)
    {
        ArgumentNullException.ThrowIfNull(project);
        var profile = project.Module.Distribution.GetProfile(target);
        var diagnostics = new List<DomainDiagnostic>();
        if (!profile.Enabled)
        {
            diagnostics.Add(new DomainDiagnostic(
                TargetDisabledCode,
                $"Target profile '{target}' is disabled.",
                $"module/distribution/targetProfiles/{target}/enabled"));
            return diagnostics;
        }

        var dependencyIndex = 0;
        foreach (var dependency in project.Module.Distribution.Dependencies)
        {
            if (!dependency.RequiredForTargets.Contains(target))
            {
                dependencyIndex++;
                continue;
            }

            if (!SemanticVersionRegex().IsMatch(dependency.Version))
            {
                diagnostics.Add(new DomainDiagnostic(
                    DependencyVersionInvalidCode,
                    $"Dependency '{dependency.Name}' version '{dependency.Version}' is not semantic.",
                    $"module/distribution/dependencies/{dependencyIndex}/version"));
            }

            if (profile.RequireApprovedLicenses && !IsApprovedLicense(dependency.LicenseSpdx))
            {
                diagnostics.Add(new DomainDiagnostic(
                    DependencyLicenseUnknownCode,
                    $"Dependency '{dependency.Name}' does not declare an approved SPDX license.",
                    $"module/distribution/dependencies/{dependencyIndex}/licenseSpdx"));
            }

            dependencyIndex++;
        }

        if (!profile.RequireApprovedLicenses)
        {
            return diagnostics;
        }

        if (!IsApprovedLicense(project.Module.Metadata.License))
        {
            diagnostics.Add(new DomainDiagnostic(
                ModuleLicenseUnknownCode,
                $"Module '{project.Module.DisplayName}' does not declare an approved SPDX license.",
                "module/metadata/license"));
        }

        if (IsUnknownProvenance(project.Module.Metadata.Provenance))
        {
            diagnostics.Add(new DomainDiagnostic(
                ModuleProvenanceUnknownCode,
                $"Module '{project.Module.DisplayName}' has unknown publication provenance.",
                "module/metadata/provenance"));
        }

        for (var index = 0; index < project.Blueprints.Count; index++)
        {
            var blueprint = project.Blueprints[index];
            if (!IsApprovedLicense(blueprint.Provenance.LicenseSpdx))
            {
                diagnostics.Add(new DomainDiagnostic(
                    BlueprintLicenseUnknownCode,
                    $"Blueprint '{blueprint.DisplayName}' does not declare an approved SPDX license.",
                    $"blueprints/{index}/provenance/licenseSpdx"));
            }

            if (IsUnknownProvenance(blueprint.Provenance.Source) ||
                IsUnknownProvenance(blueprint.Provenance.Author) ||
                IsUnknownProvenance(blueprint.Provenance.ContentHash))
            {
                diagnostics.Add(new DomainDiagnostic(
                    BlueprintProvenanceUnknownCode,
                    $"Blueprint '{blueprint.DisplayName}' has incomplete source, author or content provenance.",
                    $"blueprints/{index}/provenance"));
            }
        }

        return diagnostics;
    }

    public static bool IsApprovedLicense(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = LicenseTokenRegex().Matches(value)
            .Select(match => match.Value)
            .Where(token => token is not "AND" and not "OR" and not "WITH")
            .ToArray();
        return tokens.Length > 0 && tokens.All(token =>
            ApprovedLicenseIdentifiers.Contains(token) || token.StartsWith("LicenseRef-", StringComparison.Ordinal));
    }

    private static bool IsUnknownProvenance(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("unverified", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("migration-default", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("not provided", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("[A-Za-z0-9][A-Za-z0-9.+-]*")]
    private static partial Regex LicenseTokenRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionRegex();
}
