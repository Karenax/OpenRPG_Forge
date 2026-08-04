using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public sealed record AssetPackageInstallation(
    AssetPackageManifestId PackageId,
    string Version,
    IReadOnlyList<AssetPackageManifestId> Dependencies,
    IReadOnlyList<string> InstalledFiles);

public sealed record AssetPackageMigrationStep(
    Guid Id,
    AssetPackageManifestId PackageId,
    string FromVersion,
    string ToVersion,
    bool TrustedHostCode,
    bool BackupRequired,
    string Changelog);

public sealed record AssetPackageMigrationPlan(
    bool CanApply,
    bool DryRun,
    AssetPackageMigrationStep? Step,
    IReadOnlyList<string> BackupFiles,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AssetPackageMigrationPlanner
{
    public const string StepMissingCode = "asset.package.migration_step_missing";
    public const string StepUntrustedCode = "asset.package.migration_step_untrusted";
    public const string BackupMissingCode = "asset.package.migration_backup_missing";

    public static AssetPackageMigrationPlan Plan(
        AssetPackageInstallation installation,
        string targetVersion,
        IReadOnlyList<AssetPackageMigrationStep> trustedSteps,
        bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetVersion);
        ArgumentNullException.ThrowIfNull(trustedSteps);
        var path = $"assets/packages/{installation.PackageId}/migration";
        var step = trustedSteps.SingleOrDefault(candidate =>
            candidate.PackageId == installation.PackageId &&
            string.Equals(candidate.FromVersion, installation.Version, StringComparison.Ordinal) &&
            string.Equals(candidate.ToVersion, targetVersion, StringComparison.Ordinal));
        var diagnostics = new List<DomainDiagnostic>();
        if (step is null)
        {
            diagnostics.Add(new(StepMissingCode, "No exact package migration step is registered.", path));
        }
        else
        {
            if (!step.TrustedHostCode)
            {
                diagnostics.Add(new(StepUntrustedCode, "Package migrations must run as trusted host code.", path));
            }

            if (!step.BackupRequired || installation.InstalledFiles.Count == 0)
            {
                diagnostics.Add(new(BackupMissingCode, "Migration requires an explicit backup set.", path));
            }
        }

        return new(
            diagnostics.Count == 0,
            dryRun,
            step,
            diagnostics.Count == 0 ? installation.InstalledFiles.Order(StringComparer.Ordinal).ToArray() : [],
            diagnostics);
    }
}

public sealed record AssetPackageReplacement(
    AssetPackageManifestId RemovedPackageId,
    AssetPackageManifestId ReplacementPackageId);

public sealed record AssetPackageUninstallImpact(
    bool CanUninstall,
    AssetPackageManifestId PackageId,
    IReadOnlyList<AssetPackageManifestId> BlockingDependents,
    IReadOnlyList<string> BackupFiles,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AssetPackageUninstallAnalyzer
{
    public const string DependentPackageCode = "asset.package.uninstall_dependent";
    public const string BackupMissingCode = "asset.package.uninstall_backup_missing";

    public static AssetPackageUninstallImpact Analyze(
        AssetPackageManifestId packageId,
        IReadOnlyList<AssetPackageInstallation> installations,
        IReadOnlyList<AssetPackageReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(installations);
        ArgumentNullException.ThrowIfNull(replacements);
        var installation = installations.SingleOrDefault(item => item.PackageId == packageId);
        var replacement = replacements.SingleOrDefault(item => item.RemovedPackageId == packageId);
        var blocking = installations
            .Where(candidate => candidate.PackageId != packageId && candidate.Dependencies.Contains(packageId))
            .Where(_ => replacement is null || !installations.Any(item => item.PackageId == replacement.ReplacementPackageId))
            .Select(candidate => candidate.PackageId)
            .OrderBy(value => value.ToString(), StringComparer.Ordinal)
            .ToArray();
        var diagnostics = blocking.Select(dependent => new DomainDiagnostic(
            DependentPackageCode,
            $"Package '{dependent}' still depends on '{packageId}'.",
            $"assets/packages/{packageId}/dependents/{dependent}"))
            .ToList();
        var backups = installation?.InstalledFiles.Order(StringComparer.Ordinal).ToArray() ?? [];
        if (installation is not null && backups.Length == 0)
        {
            diagnostics.Add(new(
                BackupMissingCode,
                "Safe uninstall requires a non-empty backup set.",
                $"assets/packages/{packageId}/backup"));
        }

        return new(diagnostics.Count == 0, packageId, blocking, backups, diagnostics);
    }
}
