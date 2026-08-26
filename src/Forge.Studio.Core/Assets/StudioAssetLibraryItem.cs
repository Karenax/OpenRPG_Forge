namespace Forge.Studio.Core.Assets;

public sealed record StudioAssetVariant(
    string Format,
    string RelativePath,
    bool AvailableLocally,
    long FileSize);

public sealed record StudioAssetLibraryItem(
    string ExternalId,
    string DisplayName,
    string Source,
    string? PackId,
    string? PackName,
    string Kind,
    string Format,
    string RelativeModelPath,
    string? RelativePreviewPath,
    string SourcePage,
    string LicenseSpdx,
    string LicenseUrl,
    bool LicenseVerified,
    string Author,
    bool AttributionRequired,
    string AttributionText,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Categories,
    IReadOnlyList<StudioAssetVariant> Variants,
    long FileSize,
    bool AvailableLocally)
{
    public const long MaximumInlinePreviewBytes = 128L * 1024L * 1024L;

    public bool CanInstall =>
        AvailableLocally &&
        LicenseVerified &&
        (string.Equals(Format, "glb", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Format, "gltf", StringComparison.OrdinalIgnoreCase));

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(RelativePreviewPath);

    public bool CanPreview3D => Variants.Any(variant =>
        variant.AvailableLocally &&
        variant.FileSize <= MaximumInlinePreviewBytes &&
        variant.Format is "glb" or "gltf");

    public string SearchText { get; } = string.Join(' ',
        new[]
        {
            ExternalId,
            DisplayName,
            Source,
            PackId ?? string.Empty,
            PackName ?? string.Empty,
            Kind,
            Format,
        }.Concat(Tags).Concat(Categories)).ToLowerInvariant();
}

public sealed record StudioAssetLibraryIndex(
    string RepositoryRoot,
    string VaultRoot,
    IReadOnlyList<StudioAssetLibraryItem> Items,
    int CatalogEntryCount)
{
    public int LocalModelCount => Items.Count(item => item.AvailableLocally);

    public int InstallableModelCount => Items.Count(item => item.CanInstall);

    public int ThumbnailCount => Items.Count(item => item.HasThumbnail);

    public int Preview3DCount => Items.Count(item => item.CanPreview3D);

    public int MissingPreviewCount => Items.Count(item => !item.HasThumbnail && !item.CanPreview3D);

    public IReadOnlyList<string> Sources => Items
        .Select(item => item.Source)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<string> Formats => Items
        .Select(item => item.Format)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
