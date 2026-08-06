namespace Forge.Domain.Assets;

public enum AssetVaultFileRole
{
    Model,
    Preview,
}

public sealed record AssetVaultInstalledFile
{
    public AssetVaultFileRole Role { get; }

    public string ContentSha256 { get; }

    public long Bytes { get; }

    public string SourceRelativePath { get; }

    public string OriginalFileName { get; }

    public AssetVaultInstalledFile(
        AssetVaultFileRole role,
        string contentSha256,
        long bytes,
        string sourceRelativePath,
        string originalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        var normalizedHash = contentSha256.Trim().ToLowerInvariant();
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Asset Vault files require a valid SHA-256 hash.", nameof(contentSha256));
        }

        if (!AssetCatalogValidator.IsSafeRelativePath(sourceRelativePath))
        {
            throw new ArgumentException("Asset Vault source paths must be safe and relative.", nameof(sourceRelativePath));
        }

        if (!string.Equals(Path.GetFileName(originalFileName), originalFileName, StringComparison.Ordinal) ||
            originalFileName is "." or "..")
        {
            throw new ArgumentException("Asset Vault file names cannot contain a directory.", nameof(originalFileName));
        }

        Role = role;
        ContentSha256 = normalizedHash;
        Bytes = bytes;
        SourceRelativePath = sourceRelativePath.Replace('\\', '/').Trim();
        OriginalFileName = originalFileName.Trim();
    }
}
