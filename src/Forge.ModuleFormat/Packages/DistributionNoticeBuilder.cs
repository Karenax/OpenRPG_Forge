using System.Text;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Packages;

public static class DistributionNoticeBuilder
{
    public const string PackagePath = "THIRD-PARTY-NOTICES.txt";

    public static string Build(ProjectDefinition project, ModuleTargetKind target)
    {
        ArgumentNullException.ThrowIfNull(project);
        var builder = new StringBuilder();
        builder.AppendLine("OpenRPG Forge - Distribution notices");
        builder.AppendLine($"Module: {project.Module.DisplayName} ({project.Module.Metadata.Version})");
        builder.AppendLine($"Target: {target}");
        builder.AppendLine($"Module license: {project.Module.Metadata.License}");
        builder.AppendLine($"Module author: {project.Module.Metadata.Author}");
        builder.AppendLine($"Module provenance: {project.Module.Metadata.Provenance}");
        builder.AppendLine();
        builder.AppendLine("CONTENT PROVENANCE");
        foreach (var blueprint in project.Blueprints.OrderBy(item => item.Slug, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"- {blueprint.DisplayName} | {blueprint.Provenance.LicenseSpdx} | " +
                $"{blueprint.Provenance.Author} | {blueprint.Provenance.Source} | {blueprint.Provenance.ContentHash}");
        }

        builder.AppendLine();
        builder.AppendLine("DATA TABLES");
        foreach (var table in project.Module.Content.DataTables.OrderBy(item => item.Slug, StringComparer.Ordinal))
            builder.AppendLine($"- {table.DisplayName} | {table.LicenseSpdx} | provenance={table.Provenance} | hash={table.ContentHash}");
        if (project.Module.Content.DataTables.Count == 0)
            builder.AppendLine("- None declared.");

        builder.AppendLine();
        builder.AppendLine("CONTENT PACKAGES");
        foreach (var package in project.Module.Content.Packages.Packages
                     .OrderBy(item => item.PackageNamespace, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {package.PackageNamespace} {package.Version} | {package.LicenseSpdx} | provenance={package.Provenance} | hash={package.ContentSha256}");
            foreach (var source in package.Sources.OrderBy(item => item.LogicalResourceId, StringComparer.Ordinal))
                builder.AppendLine($"  - source:{source.LogicalResourceId} | {source.LicenseSpdx} | provenance={source.Provenance} | hash={source.ContentSha256} | path={source.RelativePath}");
            foreach (var asset in package.Assets.OrderBy(item => item.LogicalResourceId, StringComparer.Ordinal))
                builder.AppendLine($"  - asset:{asset.LogicalResourceId} | {asset.LicenseSpdx} | provenance={asset.Provenance} | hash={asset.ContentSha256} | path={asset.RelativePath}");
        }
        if (project.Module.Content.Packages.Packages.Count == 0)
            builder.AppendLine("- None declared.");

        builder.AppendLine();
        builder.AppendLine("THIRD-PARTY DEPENDENCIES");
        var dependencies = project.Module.Distribution.Dependencies
            .Where(item => item.RequiredForTargets.Contains(target))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (dependencies.Length == 0)
        {
            builder.AppendLine("- None declared for this target.");
        }
        else
        {
            foreach (var dependency in dependencies)
            {
                builder.AppendLine(
                    $"- {dependency.Name} {dependency.Version} | {dependency.LicenseSpdx} | " +
                    $"owner={dependency.Owner} | source={dependency.SourceReference}");
                builder.AppendLine($"  Rationale: {dependency.Rationale}");
                builder.AppendLine($"  Update cadence: {dependency.UpdateCadence}");
                builder.AppendLine($"  Removal plan: {dependency.RemovalPlan}");
            }
        }

        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
