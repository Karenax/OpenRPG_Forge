using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Serialization;

internal static class AssetPackageManifestJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(IReadOnlyList<AssetPackageManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        var normalized = NormalizeAndValidate(manifests);
        return JsonSerializer.Serialize(
            new AssetPackageManifestDocument(CurrentFormatVersion, normalized),
            Options) + "\n";
    }

    internal static IReadOnlyList<AssetPackageManifest> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<AssetPackageManifestDocument>(json, Options)
            ?? throw new JsonException("The asset package manifest document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Asset package manifest format '{document.FormatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        return NormalizeAndValidate(document.Manifests ?? []);
    }

    private static IReadOnlyList<AssetPackageManifest> NormalizeAndValidate(
        IReadOnlyList<AssetPackageManifest> manifests)
    {
        if (manifests.Select(manifest => manifest.Id).Distinct().Count() != manifests.Count ||
            manifests.Select(manifest => manifest.AssetId).Distinct().Count() != manifests.Count)
        {
            throw new JsonException("Asset package manifests must have unique manifest and asset IDs.");
        }

        var normalized = manifests
            .Select(Normalize)
            .OrderBy(manifest => manifest.AssetId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var diagnostic = normalized.SelectMany(AssetPackageManifestValidator.Validate).FirstOrDefault();
        if (diagnostic is not null)
        {
            throw new JsonException(
                $"Asset package manifest is invalid: {diagnostic.Code} — {diagnostic.Message}");
        }

        return normalized;
    }

    private static AssetPackageManifest Normalize(AssetPackageManifest manifest) =>
        manifest with
        {
            ModificationProvenance = manifest.ModificationProvenance.Trim(),
            LicenseSpdx = manifest.LicenseSpdx.Trim(),
            SourceContentSha256 = manifest.SourceContentSha256.Trim().ToLowerInvariant(),
            PlatformBudgets = (manifest.PlatformBudgets ?? new Dictionary<AssetTargetPlatform, long>())
                .OrderBy(item => item.Key)
                .ToDictionary(item => item.Key, item => item.Value),
            Chunks = (manifest.Chunks ?? [])
                .Select(chunk => chunk with
                {
                    RelativePath = chunk.RelativePath.Replace('\\', '/').Trim(),
                    ContentSha256 = chunk.ContentSha256.Trim().ToLowerInvariant(),
                    Platforms = (chunk.Platforms ?? []).Distinct().Order().ToArray(),
                })
                .OrderBy(chunk => chunk.Role)
                .ThenBy(chunk => chunk.RelativePath, StringComparer.Ordinal)
                .ToArray(),
            Collision = manifest.Collision is null
                ? null
                : manifest.Collision with
                {
                    MaterialTags = (manifest.Collision.MaterialTags ?? [])
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    CookPlatforms = (manifest.Collision.CookPlatforms ?? []).Distinct().Order().ToArray(),
                },
            Navigation = manifest.Navigation is null
                ? null
                : manifest.Navigation with
                {
                    MaterialTags = (manifest.Navigation.MaterialTags ?? [])
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    CookPlatforms = (manifest.Navigation.CookPlatforms ?? []).Distinct().Order().ToArray(),
                },
            Font = manifest.Font is null
                ? null
                : manifest.Font with
                {
                    PrimaryFamily = manifest.Font.PrimaryFamily.Trim(),
                    Fallbacks = (manifest.Font.Fallbacks ?? [])
                        .Select(fallback => fallback with { Family = fallback.Family.Trim() })
                        .OrderBy(fallback => fallback.Family, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    ShapingScripts = (manifest.Font.ShapingScripts ?? [])
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    LocalizationTestLocales = (manifest.Font.LocalizationTestLocales ?? [])
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    DpiBuckets = (manifest.Font.DpiBuckets ?? []).Distinct().Order().ToArray(),
                },
            Animation = manifest.Animation is null
                ? null
                : manifest.Animation with
                {
                    Rig = manifest.Animation.Rig.Trim(),
                    RetargetMap = string.IsNullOrWhiteSpace(manifest.Animation.RetargetMap)
                        ? null
                        : manifest.Animation.RetargetMap.Replace('\\', '/').Trim(),
                    Clips = (manifest.Animation.Clips ?? [])
                        .Select(clip => clip with
                        {
                            Name = clip.Name.Trim(),
                            Events = (clip.Events ?? [])
                                .Select(value => value.Trim())
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(value => value, StringComparer.Ordinal)
                                .ToArray(),
                        })
                        .OrderBy(clip => clip.Name, StringComparer.Ordinal)
                        .ToArray(),
                },
            Vfx = manifest.Vfx is null
                ? null
                : manifest.Vfx with { Provider = manifest.Vfx.Provider.Trim() },
            Lod = manifest.Lod is null
                ? null
                : manifest.Lod with
                {
                    Levels = (manifest.Lod.Levels ?? []).OrderBy(level => level.Level).ToArray(),
                },
            ModelMetadata = NormalizeModelMetadata(manifest.ModelMetadata),
            Intelligence = NormalizeIntelligence(manifest.Intelligence),
        };

    private static AssetIntelligenceProfile? NormalizeIntelligence(AssetIntelligenceProfile? profile)
    {
        if (profile is null) return null;
        return profile with
        {
            ExternalId = profile.ExternalId.Trim(),
            ModelContentSha256 = profile.ModelContentSha256.Trim().ToLowerInvariant(),
            DependencyClosureSha256 = profile.DependencyClosureSha256.Trim().ToLowerInvariant(),
            Descriptions = (profile.Descriptions ?? new Dictionary<string, string>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(
                    item => item.Key.Trim().ToLowerInvariant(),
                    item => item.Value.Trim(),
                    StringComparer.Ordinal),
            SemanticType = profile.SemanticType.Trim().ToLowerInvariant(),
            Family = profile.Family.Trim().ToLowerInvariant(),
            AssemblyRole = profile.AssemblyRole.Trim().ToLowerInvariant(),
            UseCases = (profile.UseCases ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Placement = profile.Placement with
            {
                Surface = profile.Placement.Surface.Trim().ToLowerInvariant(),
                SnapMode = profile.Placement.SnapMode.Trim().ToLowerInvariant(),
                Anchors = (profile.Placement.Anchors ?? [])
                    .OrderBy(anchor => anchor.Name, StringComparer.Ordinal)
                    .ToArray(),
            },
            Technical = profile.Technical with
            {
                Materials = (profile.Technical.Materials ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                Animations = (profile.Technical.Animations ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            },
            Readiness = profile.Readiness.Trim().ToLowerInvariant(),
            ReviewStatus = profile.ReviewStatus.Trim().ToLowerInvariant(),
            GeneratedBy = profile.GeneratedBy.Trim().ToLowerInvariant(),
            Evidence = (profile.Evidence ?? [])
                .OrderBy(item => item.Field, StringComparer.Ordinal)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static AssetModelMetadata? NormalizeModelMetadata(AssetModelMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata with
        {
            Styles = (metadata.Styles ?? [])
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Variants = (metadata.Variants ?? [])
                .Select(variant => variant with
                {
                    Key = variant.Key.Trim().ToLowerInvariant(),
                    DisplayName = variant.DisplayName.Trim(),
                    Platforms = (variant.Platforms ?? []).Distinct().Order().ToArray(),
                    Tags = (variant.Tags ?? [])
                        .Select(value => value.Trim().ToLowerInvariant())
                        .Where(value => value.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                })
                .OrderBy(variant => variant.Key, StringComparer.Ordinal)
                .ToArray(),
            Compatibility = metadata.Compatibility is null
                ? null!
                : metadata.Compatibility with
                {
                    ShaderFamily = metadata.Compatibility.ShaderFamily.Trim().ToLowerInvariant(),
                    Platforms = (metadata.Compatibility.Platforms ?? []).Distinct().Order().ToArray(),
                },
            PreviewRelativePath = metadata.PreviewRelativePath?.Replace('\\', '/').Trim() ?? string.Empty,
            PreviewContentSha256 = metadata.PreviewContentSha256?.Trim().ToLowerInvariant() ?? string.Empty,
            QualityReportRelativePath = metadata.QualityReportRelativePath?.Replace('\\', '/').Trim() ?? string.Empty,
            QualityReportContentSha256 = metadata.QualityReportContentSha256?.Trim().ToLowerInvariant() ?? string.Empty,
        };
    }

    private sealed record AssetPackageManifestDocument(
        string FormatVersion,
        IReadOnlyList<AssetPackageManifest>? Manifests);
}
