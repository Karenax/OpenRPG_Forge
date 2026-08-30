using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public static class AssetPackageManifestValidator
{
    public const string SchemaUnsupportedCode = "asset.manifest.schema_unsupported";
    public const string GovernanceMissingCode = "asset.manifest.governance_missing";
    public const string ChunkInvalidCode = "asset.manifest.chunk_invalid";
    public const string ChunkDuplicateCode = "asset.manifest.chunk_duplicate";
    public const string ReferenceMissingCode = "asset.manifest.reference_missing";
    public const string PlatformBudgetInvalidCode = "asset.manifest.platform_budget_invalid";
    public const string PlatformBudgetExceededCode = "asset.manifest.platform_budget_exceeded";
    public const string CollisionInvalidCode = "asset.manifest.collision_invalid";
    public const string NavigationInvalidCode = "asset.manifest.navigation_invalid";
    public const string FontInvalidCode = "asset.manifest.font_invalid";
    public const string AnimationInvalidCode = "asset.manifest.animation_invalid";
    public const string VfxInvalidCode = "asset.manifest.vfx_invalid";
    public const string LodInvalidCode = "asset.manifest.lod_invalid";
    public const string ModelMetadataInvalidCode = "asset.manifest.model_metadata_invalid";
    public const string IntelligenceInvalidCode = "asset.manifest.intelligence_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssetPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var root = $"assets/manifests/{manifest.Id}";
        var diagnostics = new List<DomainDiagnostic>();
        if (manifest.SchemaVersion != AssetPackageManifest.CurrentSchemaVersion)
        {
            diagnostics.Add(new(
                SchemaUnsupportedCode,
                "Asset package manifest schema is unsupported.",
                $"{root}/schemaVersion"));
        }

        if (string.IsNullOrWhiteSpace(manifest.ModificationProvenance) ||
            string.IsNullOrWhiteSpace(manifest.LicenseSpdx) ||
            !IsSha256(manifest.SourceContentSha256))
        {
            diagnostics.Add(new(
                GovernanceMissingCode,
                "Modification provenance, SPDX license and source SHA-256 are required.",
                $"{root}/governance"));
        }

        var chunks = manifest.Chunks ?? [];
        var chunkIds = chunks.Select(chunk => chunk.Id).ToHashSet();
        if (chunks.Any(chunk => chunk.Id == Guid.Empty ||
                !AssetCatalogValidator.IsSafeRelativePath(chunk.RelativePath) ||
                !IsSha256(chunk.ContentSha256) ||
                chunk.UncompressedBytes <= 0 || chunk.StoredBytes <= 0 ||
                chunk.StoredBytes > chunk.UncompressedBytes ||
                chunk.Platforms is null || chunk.Platforms.Count == 0 ||
                chunk.Platforms.Distinct().Count() != chunk.Platforms.Count))
        {
            diagnostics.Add(new(
                ChunkInvalidCode,
                "Manifest chunks require safe paths, hashes, sizes and unique platform variants.",
                $"{root}/chunks"));
        }

        if (chunks.Select(chunk => chunk.Id).Distinct().Count() != chunks.Count ||
            chunks.Select(chunk => chunk.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != chunks.Count)
        {
            diagnostics.Add(new(
                ChunkDuplicateCode,
                "Manifest chunk IDs and paths must be unique.",
                $"{root}/chunks"));
        }

        ValidatePlatformBudgets(manifest, chunks, root, diagnostics);
        ValidateCollision(manifest.Collision, chunkIds, root, diagnostics);
        ValidateNavigation(manifest.Navigation, chunks, chunkIds, root, diagnostics);
        ValidateFont(manifest.Font, chunks, chunkIds, root, diagnostics);
        ValidateAnimation(manifest.Animation, chunkIds, root, diagnostics);
        ValidateVfx(manifest.Vfx, chunkIds, root, diagnostics);
        ValidateLod(manifest.Lod, chunkIds, root, diagnostics);
        ValidateModelMetadata(manifest.ModelMetadata, chunkIds, root, diagnostics);
        if (manifest.Intelligence is not null)
        {
            var intelligenceDiagnostics = AssetIntelligenceProfileValidator.Validate(
                manifest.Intelligence,
                $"{root}/intelligence");
            if (intelligenceDiagnostics.Count > 0)
            {
                diagnostics.Add(new(
                    IntelligenceInvalidCode,
                    intelligenceDiagnostics[0].Message,
                    intelligenceDiagnostics[0].Path));
            }
            else if (manifest.Intelligence.ExternalId.Length == 0)
            {
                diagnostics.Add(new(
                    IntelligenceInvalidCode,
                    "Asset intelligence must identify its catalog asset.",
                    $"{root}/intelligence/externalId"));
            }
        }
        return diagnostics;
    }

    private static void ValidatePlatformBudgets(
        AssetPackageManifest manifest,
        IReadOnlyList<AssetManifestChunk> chunks,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (manifest.PlatformBudgets is null || manifest.PlatformBudgets.Count == 0 ||
            manifest.PlatformBudgets.Any(item => item.Value <= 0))
        {
            diagnostics.Add(new(
                PlatformBudgetInvalidCode,
                "At least one positive platform budget is required.",
                $"{root}/platformBudgets"));
            return;
        }

        foreach (var budget in manifest.PlatformBudgets.OrderBy(item => item.Key))
        {
            var storedBytes = chunks
                .Where(chunk => chunk.Platforms.Contains(AssetTargetPlatform.Any) ||
                    chunk.Platforms.Contains(budget.Key))
                .Sum(chunk => chunk.StoredBytes);
            if (storedBytes > budget.Value)
            {
                diagnostics.Add(new(
                    PlatformBudgetExceededCode,
                    $"Stored asset chunks exceed the {budget.Key} budget ({storedBytes} > {budget.Value}).",
                    $"{root}/platformBudgets/{budget.Key}"));
            }
        }
    }

    private static void ValidateCollision(
        AssetCollisionManifest? collision,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (collision is null)
        {
            return;
        }

        if (collision.MaterialTags is null || collision.MaterialTags.Count == 0 ||
            collision.MaterialTags.Any(string.IsNullOrWhiteSpace) ||
            collision.MaterialTags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != collision.MaterialTags.Count ||
            collision.CookPlatforms is null || collision.CookPlatforms.Count == 0 ||
            collision.CookPlatforms.Distinct().Count() != collision.CookPlatforms.Count)
        {
            diagnostics.Add(new(
                CollisionInvalidCode,
                "Collision material tags and cook platforms must be non-empty and unique.",
                $"{root}/collision"));
        }

        ValidateReference(collision.Required, collision.ChunkId, chunkIds, $"{root}/collision/chunkId", diagnostics);
    }

    private static void ValidateNavigation(
        AssetNavigationManifest? navigation,
        IReadOnlyList<AssetManifestChunk> chunks,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (navigation is null)
        {
            return;
        }

        var referenced = navigation.ChunkId is { } id
            ? chunks.SingleOrDefault(chunk => chunk.Id == id)
            : null;
        if (navigation.MaterialTags is null || navigation.MaterialTags.Count == 0 ||
            navigation.MaterialTags.Any(string.IsNullOrWhiteSpace) ||
            navigation.MaterialTags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != navigation.MaterialTags.Count ||
            navigation.CookPlatforms is null || navigation.CookPlatforms.Count == 0 ||
            navigation.CookPlatforms.Distinct().Count() != navigation.CookPlatforms.Count ||
            referenced is not null && referenced.Role != AssetChunkRole.Navigation)
        {
            diagnostics.Add(new(
                NavigationInvalidCode,
                "Navigation requires a dedicated chunk, unique material tags and unique cook platforms.",
                $"{root}/navigation"));
        }

        ValidateReference(navigation.Required, navigation.ChunkId, chunkIds, $"{root}/navigation/chunkId", diagnostics);
    }

    private static void ValidateFont(
        AssetFontManifest? font,
        IReadOnlyList<AssetManifestChunk> chunks,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (font is null)
        {
            return;
        }

        var primary = font.PrimaryChunkId is { } primaryId
            ? chunks.SingleOrDefault(chunk => chunk.Id == primaryId)
            : null;
        var fallbacks = font.Fallbacks ?? [];
        var referencedFontIds = fallbacks.Select(fallback => fallback.ChunkId)
            .Append(font.PrimaryChunkId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();
        var invalid = string.IsNullOrWhiteSpace(font.PrimaryFamily) ||
            primary is not null && primary.Role != AssetChunkRole.Font ||
            fallbacks.Any(fallback => string.IsNullOrWhiteSpace(fallback.Family) || fallback.ChunkId == Guid.Empty) ||
            fallbacks.Select(fallback => fallback.Family).Append(font.PrimaryFamily)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != fallbacks.Count + 1 ||
            referencedFontIds.Distinct().Count() != referencedFontIds.Length ||
            referencedFontIds.Any(id => chunks.SingleOrDefault(chunk => chunk.Id == id)?.Role != AssetChunkRole.Font) ||
            font.ShapingScripts is null || font.ShapingScripts.Count == 0 ||
            font.ShapingScripts.Any(script => script.Length != 4 || !script.All(char.IsAsciiLetter)) ||
            font.ShapingScripts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != font.ShapingScripts.Count ||
            font.LocalizationTestLocales is null || font.LocalizationTestLocales.Count == 0 ||
            font.LocalizationTestLocales.Any(locale => string.IsNullOrWhiteSpace(locale) || locale.Contains(' ')) ||
            font.LocalizationTestLocales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != font.LocalizationTestLocales.Count ||
            font.DpiBuckets is null || font.DpiBuckets.Count == 0 ||
            font.DpiBuckets.Any(dpi => dpi is < 72 or > 768) ||
            !font.DpiBuckets.SequenceEqual(font.DpiBuckets.Order()) ||
            font.DpiBuckets.Distinct().Count() != font.DpiBuckets.Count ||
            font.MinimumTextScale is < 0.5m or > 3m ||
            font.MaximumTextScale is < 0.5m or > 3m ||
            font.MinimumTextScale > font.MaximumTextScale;
        if (invalid)
        {
            diagnostics.Add(new(
                FontInvalidCode,
                "Font assets require dedicated chunks, unique fallback families, shaping scripts, locales, DPI buckets and text-scale bounds.",
                $"{root}/font"));
        }

        ValidateReference(font.Required, font.PrimaryChunkId, chunkIds, $"{root}/font/primaryChunkId", diagnostics);
        foreach (var fallback in fallbacks)
        {
            ValidateReference(true, fallback.ChunkId, chunkIds, $"{root}/font/fallbacks/{fallback.Family}", diagnostics);
        }
    }

    private static void ValidateAnimation(
        AssetAnimationManifest? animation,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (animation is null)
        {
            return;
        }

        var clips = animation.Clips ?? [];
        if (string.IsNullOrWhiteSpace(animation.Rig) || clips.Count == 0 ||
            clips.Select(clip => clip.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != clips.Count ||
            clips.Any(clip => string.IsNullOrWhiteSpace(clip.Name) || clip.DurationSeconds <= 0 ||
                clip.Events is null || clip.Events.Any(string.IsNullOrWhiteSpace) ||
                clip.Events.Distinct(StringComparer.Ordinal).Count() != clip.Events.Count) ||
            (!string.IsNullOrWhiteSpace(animation.RetargetMap) &&
                !AssetCatalogValidator.IsSafeRelativePath(animation.RetargetMap)))
        {
            diagnostics.Add(new(
                AnimationInvalidCode,
                "Animation rig, clips, events or retarget map are invalid.",
                $"{root}/animation"));
        }

        ValidateReference(animation.Required, animation.ChunkId, chunkIds, $"{root}/animation/chunkId", diagnostics);
    }

    private static void ValidateVfx(
        AssetVfxManifest? vfx,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (vfx is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(vfx.Provider) || vfx.MaximumParticles <= 0 ||
            vfx.MaximumDrawCalls <= 0 || vfx.CullDistance <= 0)
        {
            diagnostics.Add(new(
                VfxInvalidCode,
                "VFX provider, particle budget, draw-call budget and cull distance are required.",
                $"{root}/vfx"));
        }

        ValidateReference(vfx.Required, vfx.ChunkId, chunkIds, $"{root}/vfx/chunkId", diagnostics);
    }

    private static void ValidateLod(
        AssetLodManifest? lod,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (lod is null)
        {
            return;
        }

        var levels = lod.Levels ?? [];
        var expectedLevels = Enumerable.Range(0, levels.Count).ToArray();
        if (lod.CrossFadeSeconds is < 0 or > 5 || levels.Count == 0 ||
            !levels.Select(level => level.Level).SequenceEqual(expectedLevels) ||
            levels.Any(level => level.ScreenRatio is <= 0 or > 1 || level.MaximumTriangles <= 0) ||
            !levels.Select(level => level.ScreenRatio).SequenceEqual(
                levels.Select(level => level.ScreenRatio).OrderDescending()) ||
            levels.Select(level => level.ScreenRatio).Distinct().Count() != levels.Count)
        {
            diagnostics.Add(new(
                LodInvalidCode,
                "LOD levels must be contiguous with descending unique screen ratios and valid budgets.",
                $"{root}/lod"));
        }

        foreach (var level in levels)
        {
            ValidateReference(lod.Required, level.ChunkId, chunkIds, $"{root}/lod/levels/{level.Level}/chunkId", diagnostics);
        }
    }

    private static void ValidateModelMetadata(
        AssetModelMetadata? metadata,
        IReadOnlySet<Guid> chunkIds,
        string root,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (metadata is null)
        {
            return;
        }

        var dimensions = metadata.Dimensions;
        var metrics = metadata.Metrics;
        var variants = metadata.Variants ?? [];
        var compatibility = metadata.Compatibility;
        var invalid = dimensions is null ||
            dimensions.ExtentXMeters is <= 0 or > 1000 ||
            dimensions.ExtentYMeters is <= 0 or > 1000 ||
            dimensions.ExtentZMeters is <= 0 or > 1000 ||
            metrics is null || metrics.BaseMeshCount <= 0 || metrics.VertexCount <= 0 || metrics.TriangleCount <= 0 ||
            metrics.MaterialCount < 0 || metrics.TextureCount < 0 || metrics.LodMeshCount < 0 ||
            metrics.CollisionMeshCount < 0 || metrics.QualityScore is < 0 or > 100 ||
            metadata.Styles is null || metadata.Styles.Count == 0 ||
            metadata.Styles.Any(string.IsNullOrWhiteSpace) ||
            metadata.Styles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != metadata.Styles.Count ||
            variants.Count == 0 || variants.Any(variant =>
                string.IsNullOrWhiteSpace(variant.Key) || string.IsNullOrWhiteSpace(variant.DisplayName) ||
                variant.ModelChunkId == Guid.Empty || !chunkIds.Contains(variant.ModelChunkId) ||
                variant.Platforms is null || variant.Platforms.Count == 0 ||
                variant.Platforms.Distinct().Count() != variant.Platforms.Count ||
                variant.Tags is null || variant.Tags.Any(string.IsNullOrWhiteSpace)) ||
            variants.Select(variant => variant.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variants.Count ||
            compatibility is null || string.IsNullOrWhiteSpace(compatibility.ShaderFamily) ||
            compatibility.MetersPerUnit is < 0.001m or > 1000m ||
            compatibility.TargetTexelDensity is < 16 or > 8192 ||
            compatibility.Platforms is null || compatibility.Platforms.Count == 0 ||
            compatibility.Platforms.Distinct().Count() != compatibility.Platforms.Count ||
            !AssetCatalogValidator.IsSafeRelativePath(metadata.PreviewRelativePath) ||
            !IsSha256(metadata.PreviewContentSha256) || metadata.PreviewBytes <= 0 ||
            !AssetCatalogValidator.IsSafeRelativePath(metadata.QualityReportRelativePath) ||
            !IsSha256(metadata.QualityReportContentSha256);
        if (invalid)
        {
            diagnostics.Add(new(
                ModelMetadataInvalidCode,
                "Model metadata requires valid dimensions, metrics, styles, variants, compatibility and governed sidecars.",
                $"{root}/modelMetadata"));
        }
    }

    private static void ValidateReference(
        bool required,
        Guid? chunkId,
        IReadOnlySet<Guid> chunkIds,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (chunkId is { } id && id != Guid.Empty && chunkIds.Contains(id))
        {
            return;
        }

        if (required)
        {
            diagnostics.Add(new(
                ReferenceMissingCode,
                "A required asset extension does not resolve to a manifest chunk.",
                path));
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
