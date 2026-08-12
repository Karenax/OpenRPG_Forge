using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public interface IAssetPackageMigrationHostStep
{
    Guid StepId { get; }

    void Apply(string workspaceRoot, IReadOnlyList<string> installedRelativePaths);
}

public static class AssetPackageMigrationStore
{
    public const string PlanInvalidCode = "asset.package.migration_plan_invalid";
    public const string HostStepMismatchCode = "asset.package.migration_host_step_mismatch";
    public const string ApplyFailedCode = "asset.package.migration_apply_failed";

    public static AssetPackageMigrationExecutionResult Execute(
        string workspaceRoot,
        AssetPackageInstallation installation,
        AssetPackageMigrationPlan plan,
        IAssetPackageMigrationHostStep hostStep)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(hostStep);
        if (!plan.CanApply || plan.Step is null)
        {
            return Failure(installation, PlanInvalidCode, "Package migration plan is not applicable.");
        }

        if (!plan.Step.TrustedHostCode || plan.Step.Id != hostStep.StepId ||
            plan.Step.PackageId != installation.PackageId ||
            !string.Equals(plan.Step.FromVersion, installation.Version, StringComparison.Ordinal))
        {
            return Failure(installation, HostStepMismatchCode,
                "Compiled host migration step does not match the approved plan.");
        }

        var root = Path.GetFullPath(workspaceRoot);
        (string Relative, string Absolute)[] files;
        try
        {
            files = plan.BackupFiles.Select(path => ResolveConfined(root, path)).ToArray();
        }
        catch (ArgumentException exception)
        {
            return Failure(installation, PlanInvalidCode, exception.Message);
        }
        if (files.Any(path => !File.Exists(path.Absolute)))
        {
            return Failure(installation, PlanInvalidCode, "Every planned migration input must exist before apply.");
        }

        var migrationRoot = Path.Combine(root, ".forge", "package-migrations", plan.Step.Id.ToString("D"));
        var backupRoot = Path.Combine(migrationRoot, "backup");
        if (plan.DryRun)
        {
            return new(true, installation, null, null, [], true);
        }

        try
        {
            foreach (var file in files)
            {
                var backup = Path.Combine(backupRoot, file.Relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(file.Absolute, backup, overwrite: true);
            }

            hostStep.Apply(root, files.Select(file => file.Relative).ToArray());
            var changelogPath = Path.Combine(migrationRoot, "CHANGELOG.txt");
            Directory.CreateDirectory(migrationRoot);
            File.WriteAllText(changelogPath,
                $"{plan.Step.FromVersion} -> {plan.Step.ToVersion}{Environment.NewLine}{plan.Step.Changelog}{Environment.NewLine}");
            var migrated = installation with { Version = plan.Step.ToVersion };
            return new(true, migrated, Path.GetRelativePath(root, backupRoot).Replace('\\', '/'),
                Path.GetRelativePath(root, changelogPath).Replace('\\', '/'), [], false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Restore(root, backupRoot, files);
            return Failure(installation, ApplyFailedCode, $"Package migration failed and backup was restored: {exception.Message}");
        }
    }

    public static void RestoreBackup(string workspaceRoot, string backupRelativePath, IReadOnlyList<string> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRelativePath);
        ArgumentNullException.ThrowIfNull(files);
        var root = Path.GetFullPath(workspaceRoot);
        var backupRoot = ResolveConfined(root, backupRelativePath).Absolute;
        Restore(root, backupRoot, files.Select(path => ResolveConfined(root, path)).ToArray());
    }

    private static void Restore(string root, string backupRoot, IReadOnlyList<(string Relative, string Absolute)> files)
    {
        foreach (var file in files)
        {
            var backup = Path.Combine(backupRoot, file.Relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(backup)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(file.Absolute)!);
            File.Copy(backup, file.Absolute, overwrite: true);
        }
    }

    private static (string Relative, string Absolute) ResolveConfined(string root, string relative)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relative))
            throw new ArgumentException("Package migration paths must be safe and relative.", nameof(relative));
        var normalized = relative.Replace('\\', '/');
        var absolute = Path.GetFullPath(Path.Combine(root, normalized));
        if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Package migration path escapes the workspace.", nameof(relative));
        return (normalized, absolute);
    }

    private static AssetPackageMigrationExecutionResult Failure(
        AssetPackageInstallation installation,
        string code,
        string message) => new(false, installation, null, null,
        [new DomainDiagnostic(code, message, $"assets/packages/{installation.PackageId}/migration")], false);
}

public sealed record AssetPackageMigrationExecutionResult(
    bool Succeeded,
    AssetPackageInstallation Installation,
    string? BackupRelativePath,
    string? ChangelogRelativePath,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    bool DryRun);
