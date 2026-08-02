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
