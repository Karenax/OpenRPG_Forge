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
    public const string BlueprintPermissionUnknownCode = "distribution.blueprint.permission_unknown";
    public const string DependencyLicenseUnknownCode = "distribution.dependency.license_unknown";
    public const string DependencyVersionInvalidCode = "distribution.dependency.version_invalid";
    public const string DependencyApprovalRequiredCode = "distribution.dependency.approval_required";
    public const string DependencyReviewRequiredCode = "distribution.dependency.review_required";
    public const string DependencySecurityAuditMissingCode = "distribution.dependency.security_audit_missing";
    public const string StableSignatureRequiredCode = "distribution.release.stable_signature_required";
    public const string StableDeltaPolicyRequiredCode = "distribution.release.stable_delta_policy_required";
    public const string WindowsPackagingSafetyRequiredCode = "distribution.release.windows_packaging_safety_required";
    public const string DataTableLicenseUnknownCode = "distribution.data_table.license_unknown";
    public const string DataTableProvenanceUnknownCode = "distribution.data_table.provenance_unknown";
    public const string ContentPackageLicenseUnknownCode = "distribution.content_package.license_unknown";
    public const string ContentPackageProvenanceUnknownCode = "distribution.content_package.provenance_unknown";
    public const string ContentResourceLicenseUnknownCode = "distribution.content_resource.license_unknown";
    public const string ContentResourceProvenanceUnknownCode = "distribution.content_resource.provenance_unknown";

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

        var release = project.Module.Distribution.Release;
        if (target == ModuleTargetKind.Shipping && release.Channel == ModuleReleaseChannel.Stable)
        {
            if (release.Signing.Mode != ModuleSigningMode.Required)
            {
                diagnostics.Add(new DomainDiagnostic(
                    StableSignatureRequiredCode,
                    "Stable Shipping releases require a trusted package signature.",
                    "module/distribution/release/signing/mode"));
            }

            if (!release.Delta.Enabled || !release.Delta.AllowResume || !release.Delta.DeduplicateByHash ||
                !release.Delta.AllowRollback)
            {
                diagnostics.Add(new DomainDiagnostic(
                    StableDeltaPolicyRequiredCode,
                    "Stable Shipping releases require resumable, deduplicated and rollback-safe deltas.",
                    "module/distribution/release/delta"));
            }

            if (release.Platforms.Contains(ModuleDistributionPlatform.WindowsX64) &&
                (!release.WindowsPackaging.DetectPrerequisites || !release.WindowsPackaging.WriteInstallLog ||
                 !release.WindowsPackaging.IncludeUninstaller))
            {
                diagnostics.Add(new DomainDiagnostic(
                    WindowsPackagingSafetyRequiredCode,
                    "Stable Windows packaging requires prerequisite detection, install logs and an uninstaller.",
                    "module/distribution/release/windowsPackaging"));
            }
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

            if (dependency.ApprovalState != DependencyApprovalState.Approved)
            {
                diagnostics.Add(new DomainDiagnostic(DependencyApprovalRequiredCode,
                    $"Dependency '{dependency.Name}' is not approved for distribution.",
                    $"module/distribution/dependencies/{dependencyIndex}/approvalState"));
            }

            if (dependency.ApproverRoles.Count < dependency.RequiredApprovals)
            {
                diagnostics.Add(new DomainDiagnostic(DependencyReviewRequiredCode,
                    $"Dependency '{dependency.Name}' does not have enough independent review roles.",
                    $"module/distribution/dependencies/{dependencyIndex}/approverRoles"));
            }

            if (string.IsNullOrWhiteSpace(dependency.SecurityAuditReference))
            {
                diagnostics.Add(new DomainDiagnostic(DependencySecurityAuditMissingCode,
                    $"Dependency '{dependency.Name}' has no security audit reference.",
                    $"module/distribution/dependencies/{dependencyIndex}/securityAuditReference"));
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

            if (IsUnknownProvenance(blueprint.Provenance.PermissionEvidence))
            {
                diagnostics.Add(new DomainDiagnostic(
                    BlueprintPermissionUnknownCode,
                    $"Blueprint '{blueprint.DisplayName}' does not declare permission evidence.",
                    $"blueprints/{index}/provenance/permissionEvidence"));
            }
        }

        for (var index = 0; index < project.Module.Content.DataTables.Count; index++)
        {
            var table = project.Module.Content.DataTables[index];
            if (!IsApprovedLicense(table.LicenseSpdx))
            {
                diagnostics.Add(new DomainDiagnostic(
                    DataTableLicenseUnknownCode,
                    $"Data table '{table.DisplayName}' does not declare an approved SPDX license.",
                    $"module/content/dataTables/{index}/licenseSpdx"));
            }

            if (IsUnknownProvenance(table.Provenance) || IsUnknownProvenance(table.ContentHash))
            {
                diagnostics.Add(new DomainDiagnostic(
                    DataTableProvenanceUnknownCode,
                    $"Data table '{table.DisplayName}' has incomplete provenance or content hash.",
                    $"module/content/dataTables/{index}/provenance"));
            }
        }

        foreach (var package in project.Module.Content.Packages.Packages)
        {
            var packagePath = $"module/content/packages/items/{package.Id}";
            if (!IsApprovedLicense(package.LicenseSpdx))
                diagnostics.Add(new DomainDiagnostic(ContentPackageLicenseUnknownCode,
                    $"Content package '{package.PackageNamespace}' does not declare an approved SPDX license.",
                    $"{packagePath}/licenseSpdx"));
            if (IsUnknownProvenance(package.Provenance) || IsUnknownProvenance(package.ContentSha256))
                diagnostics.Add(new DomainDiagnostic(ContentPackageProvenanceUnknownCode,
                    $"Content package '{package.PackageNamespace}' has incomplete provenance or content hash.",
                    $"{packagePath}/provenance"));
            foreach (var source in package.Sources)
                ValidateContentResource(source.Id, source.LogicalResourceId, source.LicenseSpdx,
                    source.Provenance, source.ContentSha256, $"{packagePath}/sources/{source.Id}", diagnostics);
            foreach (var asset in package.Assets)
                ValidateContentResource(asset.Id, asset.LogicalResourceId, asset.LicenseSpdx,
                    asset.Provenance, asset.ContentSha256, $"{packagePath}/assets/{asset.Id}", diagnostics);
        }

        return diagnostics;
    }

    private static void ValidateContentResource(Guid id, string logicalResourceId, string licenseSpdx,
        string provenance, string contentSha256, string path, List<DomainDiagnostic> diagnostics)
    {
        if (!IsApprovedLicense(licenseSpdx))
            diagnostics.Add(new DomainDiagnostic(ContentResourceLicenseUnknownCode,
                $"Content resource '{logicalResourceId}' ({id:D}) does not declare an approved SPDX license.",
                $"{path}/licenseSpdx"));
        if (IsUnknownProvenance(provenance) || IsUnknownProvenance(contentSha256))
            diagnostics.Add(new DomainDiagnostic(ContentResourceProvenanceUnknownCode,
                $"Content resource '{logicalResourceId}' ({id:D}) has incomplete provenance or content hash.",
                $"{path}/provenance"));
    }

    public static bool IsApprovedLicense(string? value)
        => SpdxLicenseValidator.IsSupported(value);

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
