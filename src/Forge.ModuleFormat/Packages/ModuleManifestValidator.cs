using System.Text.RegularExpressions;
using Forge.Domain.Modules;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Packages;

public static partial class ModuleManifestValidator
{
    public const string InvalidFormatCode = "module.manifest.format_invalid";
    public const string InvalidVersionCode = "module.manifest.version_invalid";
    public const string InvalidSlugCode = "module.manifest.slug_invalid";
    public const string InvalidHashCode = "module.manifest.hash_invalid";
    public const string DuplicateDependencyCode = "module.manifest.dependency_duplicate";
    public const string InvalidTargetCode = "module.manifest.target_invalid";
    public const string InvalidNoticePathCode = "module.manifest.notice_path_invalid";
    public const string InvalidThirdPartyDependencyCode = "module.manifest.third_party_dependency_invalid";

    public static IReadOnlyList<ModulePackageDiagnostic> Validate(ModuleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var diagnostics = new List<ModulePackageDiagnostic>();
        if (!string.Equals(
                manifest.FormatVersion,
                ModuleManifest.CurrentFormatVersion,
                StringComparison.Ordinal))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidFormatCode,
                $"Unsupported module format '{manifest.FormatVersion}'.",
                "manifest.json/formatVersion"));
        }

        foreach (var (value, path) in new[]
                 {
                     (manifest.ModuleVersion, "manifest.json/moduleVersion"),
                     (manifest.RuntimeMinimumVersion, "manifest.json/runtimeMinimumVersion"),
                     (manifest.ForgeScriptApiVersion, "manifest.json/forgeScriptApiVersion"),
                 })
        {
            if (!SemanticVersionRegex().IsMatch(value ?? string.Empty))
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    InvalidVersionCode,
                    $"'{value}' is not a semantic version.",
                    path));
            }
        }

        if (!SlugRegex().IsMatch(manifest.Slug ?? string.Empty))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidSlugCode,
                $"Module slug '{manifest.Slug}' is invalid.",
                "manifest.json/slug"));
        }

        if (!Sha256Regex().IsMatch(manifest.ContentSha256 ?? string.Empty))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidHashCode,
                "Content hash must be a lowercase SHA-256 value.",
                "manifest.json/contentSha256"));
        }

        if (manifest.Dependencies.Distinct(StringComparer.Ordinal).Count() != manifest.Dependencies.Count)
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                DuplicateDependencyCode,
                "Module dependencies must be unique.",
                "manifest.json/dependencies"));
        }

        if (!Enum.IsDefined(manifest.Target))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidTargetCode,
                $"Module target '{manifest.Target}' is invalid.",
                "manifest.json/target"));
        }

        if (!string.Equals(
                manifest.LicenseNoticePath,
                DistributionNoticeBuilder.PackagePath,
                StringComparison.Ordinal))
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                InvalidNoticePathCode,
                $"License notice path must be '{DistributionNoticeBuilder.PackagePath}'.",
                "manifest.json/licenseNoticePath"));
        }

        if (manifest.ThirdPartyDependencies.Select(item => item.Id).Distinct().Count() !=
                manifest.ThirdPartyDependencies.Count ||
            manifest.ThirdPartyDependencies.Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.ThirdPartyDependencies.Count)
        {
            diagnostics.Add(new ModulePackageDiagnostic(
                DuplicateDependencyCode,
                "Third-party dependency IDs and names must be unique.",
                "manifest.json/thirdPartyDependencies"));
        }

        for (var index = 0; index < manifest.ThirdPartyDependencies.Count; index++)
        {
            var dependency = manifest.ThirdPartyDependencies[index];
            if (string.IsNullOrWhiteSpace(dependency.Name) ||
                !SemanticVersionRegex().IsMatch(dependency.Version ?? string.Empty) ||
                string.IsNullOrWhiteSpace(dependency.Owner) ||
                string.IsNullOrWhiteSpace(dependency.SourceReference) ||
                string.IsNullOrWhiteSpace(dependency.LicenseSpdx) ||
                dependency.ApprovalState != DependencyApprovalState.Approved ||
                dependency.RequiredApprovals is < 1 or > 8 ||
                dependency.ApproverRoles.Count < dependency.RequiredApprovals ||
                string.IsNullOrWhiteSpace(dependency.SecurityAuditReference))
            {
                diagnostics.Add(new ModulePackageDiagnostic(
                    InvalidThirdPartyDependencyCode,
                    $"Third-party dependency '{dependency.Name}' has incomplete approval metadata.",
                    $"manifest.json/thirdPartyDependencies/{index}"));
            }
        }

        return diagnostics;
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Regex();
}
