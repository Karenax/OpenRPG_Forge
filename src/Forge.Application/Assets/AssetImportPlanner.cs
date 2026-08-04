using Forge.Domain.Assets;
using Forge.Domain.Validation;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Application.Assets;

public static class AssetImportPlanner
{
    public const string LicenseBlockedCode = "asset.import.license_blocked";
    public const string SourceUnavailableCode = "asset.import.source_unavailable";
    public const string PathUnsafeCode = "asset.import.path_unsafe";
    public const string ProfileMismatchCode = "asset.import.profile_mismatch";

    public static AssetImportPlanResult Plan(AssetCatalogItem item, AssetProcessingProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        var diagnostics = new List<DomainDiagnostic>();
        if (!item.LicenseVerified)
        {
            diagnostics.Add(new DomainDiagnostic(
                LicenseBlockedCode,
                $"Import of '{item.ExternalId}' is blocked until its license is verified.",
                $"assets/{item.Id}/license"));
        }

        if (item.Availability == AssetAvailability.CatalogOnly || item.RelativeModelPath is null)
        {
            diagnostics.Add(new DomainDiagnostic(
                SourceUnavailableCode,
                $"Asset '{item.ExternalId}' is catalogued but its model is not installed locally.",
                $"assets/{item.Id}/availability"));
        }
        else if (!AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
        {
            diagnostics.Add(new DomainDiagnostic(
                PathUnsafeCode,
                $"Asset '{item.ExternalId}' has an unsafe source path.",
                $"assets/{item.Id}/relativeModelPath"));
        }

        profile ??= InferProfile(item);
        if (profile.AssetId != item.Id ||
            !string.Equals(profile.ContentSha256, item.ContentSha256, StringComparison.Ordinal) ||
            !string.Equals(profile.LicenseSpdx, item.LicenseSpdx, StringComparison.Ordinal) ||
            !string.Equals(profile.Provenance, item.Provenance, StringComparison.Ordinal))
        {
            diagnostics.Add(new DomainDiagnostic(
                ProfileMismatchCode,
                "Asset processing profile identity or governance does not match the catalog item.",
                $"assets/{item.Id}/profile"));
        }

        diagnostics.AddRange(AssetProcessingProfileValidator.Validate(profile));

        var slug = string.Concat(item.ExternalId.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        var destination = $"assets/imported/{slug}";
        var cacheKey = CreateCacheKey(profile);
        var tasks = CreateTasks(profile, destination, cacheKey);
        var plan = diagnostics.Count == 0
            ? new AssetImportPlan(
                AssetImportPlan.CurrentSchemaVersion,
                item.Id,
                item.RelativeModelPath!,
                destination,
                item.ContentSha256,
                item.LicenseSpdx,
                item.Provenance,
                profile,
                cacheKey,
                tasks,
                true)
            : null;
        return new AssetImportPlanResult(plan, diagnostics);
    }

    private static AssetProcessingProfile InferProfile(AssetCatalogItem item)
    {
        var sourceFormat = Path.GetExtension(item.RelativeModelPath ?? string.Empty).TrimStart('.').ToLowerInvariant();
        var isTexture = sourceFormat is "png" or "tif" or "tiff" or "exr" or "ktx2";
        var outputBudget = item.TotalBytes switch
        {
            <= 0 => 1,
            > long.MaxValue / 2 => long.MaxValue,
            _ => item.TotalBytes * 2,
        };
        return new AssetProcessingProfile(
            item.Id,
            AssetProcessingProfile.CurrentSchemaVersion,
            isTexture ? AssetProcessingKind.Texture : AssetProcessingKind.Model,
            sourceFormat,
            isTexture ? "ktx2" : "glb",
            item.LicenseSpdx,
            item.ContentSha256,
            item.Provenance,
            outputBudget,
            texture: isTexture
                ? new(TextureColorSpace.Srgb, TextureCompressionProfile.Universal, 4096, 10.24m,
                    new Dictionary<PbrTextureChannel, string> { [PbrTextureChannel.BaseColor] = item.RelativeModelPath! })
                : null,
            model: isTexture
                ? null
                : new(false, true, true, [], [1m, 0.5m, 0.25m]));
    }

    private static string CreateCacheKey(AssetProcessingProfile profile)
    {
        var texture = profile.Texture is null
            ? string.Empty
            : $"{profile.Texture.ColorSpace}|{profile.Texture.Compression}|{profile.Texture.MaximumDimension}|{FormatDecimal(profile.Texture.TexelDensity)}|" +
              string.Join(',', profile.Texture.Channels.Select(item => $"{item.Key}:{item.Value}"));
        var model = profile.Model is null
            ? string.Empty
            : $"{profile.Model.ImportRig}|{profile.Model.ImportAnimations}|{profile.Model.GenerateCollisions}|" +
              $"{string.Join(',', profile.Model.Sockets)}|{string.Join(',', profile.Model.LodRatios.Select(FormatDecimal))}";
        var canonical = $"{profile.SchemaVersion}|{profile.AssetId}|{profile.Kind}|{profile.SourceFormat}|" +
            $"{profile.CanonicalFormat}|{profile.ContentSha256}|{profile.LicenseSpdx}|{profile.Provenance}|" +
            $"{profile.MaximumOutputBytes}|{texture}|{model}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static IReadOnlyList<AssetBuildTask> CreateTasks(
        AssetProcessingProfile profile,
        string destination,
        string cacheKey)
    {
        var tasks = new List<AssetBuildTask>();
        if (profile.Kind == AssetProcessingKind.Texture)
        {
            tasks.Add(new("texture.process", $"{destination}/texture.ktx2", cacheKey, []));
            return tasks;
        }

        const string normalize = "model.normalize";
        tasks.Add(new(normalize, $"{destination}/model.glb", cacheKey, []));
        if (profile.Model!.ImportAnimations)
        {
            tasks.Add(new("model.animations", $"{destination}/animations.index.json", cacheKey, [normalize]));
        }

        if (profile.Model.GenerateCollisions)
        {
            tasks.Add(new("model.collisions", $"{destination}/collisions.glb", cacheKey, [normalize]));
        }

        foreach (var ratio in profile.Model.LodRatios.Skip(1))
        {
            var suffix = FormatDecimal(ratio);
            tasks.Add(new($"model.lod.{suffix}", $"{destination}/lod-{suffix}.glb", cacheKey, [normalize]));
        }

        if (profile.Model.Sockets.Count > 0)
        {
            tasks.Add(new("model.sockets", $"{destination}/sockets.json", cacheKey, [normalize]));
        }

        return tasks;
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record AssetImportPlan(
    int SchemaVersion,
    AssetCatalogItemId AssetId,
    string SourceRelativePath,
    string DestinationRelativeDirectory,
    string ContentSha256,
    string LicenseSpdx,
    string Provenance,
    AssetProcessingProfile Profile,
    string CacheKey,
    IReadOnlyList<AssetBuildTask> Tasks,
    bool DryRun)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AssetBuildTask(
    string Name,
    string OutputRelativePath,
    string CacheKey,
    IReadOnlyList<string> Dependencies);

public sealed record AssetImportPlanResult(
    AssetImportPlan? Plan,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Plan is not null && Diagnostics.Count == 0;
}
