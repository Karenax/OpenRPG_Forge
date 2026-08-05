using Forge.Domain.Assets;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static class ArtisticCompatibilityValidator
{
    public const string MissingBlueprintCode = "art.style.blueprint_missing";
    public const string ScaleMismatchCode = "art.style.scale_mismatch";
    public const string TexelMismatchCode = "art.style.texel_density_mismatch";
    public const string ShaderMismatchCode = "art.style.shader_mismatch";
    public const string EraMismatchCode = "art.style.era_mismatch";
    public const string RealismMismatchCode = "art.style.realism_mismatch";
    public const string PaletteMismatchCode = "art.style.palette_mismatch";

    public static IReadOnlyList<DomainDiagnostic> ValidateReferences(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var blueprintIds = project.Blueprints.Select(blueprint => blueprint.Id).ToHashSet();
        return project.Module.ArtisticStyle.Assets
            .Where(asset => !blueprintIds.Contains(asset.BlueprintId))
            .Select(asset => new DomainDiagnostic(
                MissingBlueprintCode,
                $"Artistic profile references missing blueprint '{asset.BlueprintId}'.",
                $"module/artisticStyle/assets/{asset.BlueprintId}"))
            .ToArray();
    }

    public static IReadOnlyList<DomainDiagnostic> Analyze(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Analyze(project.Module.ArtisticStyle);
    }

    public static IReadOnlyList<DomainDiagnostic> Analyze(ArtisticStyleProfile target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var asset in target.Assets)
        {
            var path = $"blueprints/{asset.BlueprintId}/artisticStyle";
            AddRangeMismatch(diagnostics, target, asset, path);
            if (!string.Equals(asset.ShaderFamily, target.ShaderFamily, StringComparison.Ordinal))
            {
                diagnostics.Add(new(ShaderMismatchCode,
                    $"Blueprint '{asset.BlueprintId}' uses shader family '{asset.ShaderFamily}' instead of '{target.ShaderFamily}'.",
                    $"{path}/shaderFamily"));
            }

            if (!string.Equals(asset.Era, target.Era, StringComparison.Ordinal))
            {
                diagnostics.Add(new(EraMismatchCode,
                    $"Blueprint '{asset.BlueprintId}' targets era '{asset.Era}' instead of '{target.Era}'.",
                    $"{path}/era"));
            }

            if (asset.Realism != target.Realism)
            {
                diagnostics.Add(new(RealismMismatchCode,
                    $"Blueprint '{asset.BlueprintId}' realism '{asset.Realism}' differs from '{target.Realism}'.",
                    $"{path}/realism"));
            }

            if (target.Palette.Count > 0 && !asset.Palette.Intersect(target.Palette, StringComparer.Ordinal).Any())
            {
                diagnostics.Add(new(PaletteMismatchCode,
                    $"Blueprint '{asset.BlueprintId}' has no color/material palette overlap with the project profile.",
                    $"{path}/palette"));
            }
        }

        return diagnostics;
    }

    private static void AddRangeMismatch(
        ICollection<DomainDiagnostic> diagnostics,
        ArtisticStyleProfile target,
        ArtisticAssetStyle asset,
        string path)
    {
        var scaleDelta = Math.Abs(asset.MetersPerUnit - target.MetersPerUnit) / target.MetersPerUnit * 100;
        if (scaleDelta > target.ScaleTolerancePercent)
        {
            diagnostics.Add(new(ScaleMismatchCode,
                $"Blueprint '{asset.BlueprintId}' metric scale differs by {scaleDelta:0.#}% (allowed {target.ScaleTolerancePercent:0.#}%).",
                $"{path}/metersPerUnit"));
        }

        var texelDelta = Math.Abs(asset.TexelDensity - target.TargetTexelDensity) / (double)target.TargetTexelDensity * 100;
        if (texelDelta > target.TexelTolerancePercent)
        {
            diagnostics.Add(new(TexelMismatchCode,
                $"Blueprint '{asset.BlueprintId}' texel density differs by {texelDelta:0.#}% (allowed {target.TexelTolerancePercent:0.#}%).",
                $"{path}/texelDensity"));
        }
    }
}
