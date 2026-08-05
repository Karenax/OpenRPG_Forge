using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Packages;

public sealed record ModuleManifest(
    string FormatVersion,
    ModuleId ModuleId,
    string Slug,
    string ModuleVersion,
    string RuntimeMinimumVersion,
    string ForgeScriptApiVersion,
    AreaId EntryAreaId,
    IReadOnlyList<string> Dependencies,
    ModuleTargetKind Target,
    IReadOnlyList<ModuleManifestDependency> ThirdPartyDependencies,
    string LicenseNoticePath,
    string ContentSha256)
{
    public const string CurrentFormatVersion = "1.2.0";

    public static ModuleManifest Create(
        ProjectDefinition project,
        string moduleVersion,
        string runtimeMinimumVersion,
        string forgeScriptApiVersion,
        string contentSha256,
        IReadOnlyList<string>? dependencies = null,
        ModuleTargetKind target = ModuleTargetKind.Editor)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ModuleManifest(
            CurrentFormatVersion,
            project.Module.Id,
            project.Module.Slug,
            moduleVersion,
            runtimeMinimumVersion,
            forgeScriptApiVersion,
            project.Module.EntryAreaId,
            dependencies ?? [],
            target,
            project.Module.Distribution.Dependencies
                .Where(dependency => dependency.RequiredForTargets.Contains(target))
                .Select(dependency => new ModuleManifestDependency(
                    dependency.Id,
                    dependency.Name,
                    dependency.Version,
                    dependency.LicenseSpdx,
                    dependency.Owner,
                    dependency.SourceReference,
                    dependency.ApprovalState,
                    dependency.RiskLevel,
                    dependency.ApproverRoles,
                    dependency.RequiredApprovals,
                    dependency.SecurityAuditReference))
                .OrderBy(dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DistributionNoticeBuilder.PackagePath,
            contentSha256);
    }
}
