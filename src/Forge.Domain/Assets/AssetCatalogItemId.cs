using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Assets;

public sealed record AssetCatalogItemId
{
    public Guid Value { get; }

    private AssetCatalogItemId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An asset catalog item ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static AssetCatalogItemId From(Guid value) => new(value);

    public static AssetCatalogItemId Parse(string value) => From(Guid.Parse(value));

    public static AssetCatalogItemId ForExternalId(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:asset:{externalId.Trim().ToLowerInvariant()}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return From(new Guid(bytes[..16]));
    }

    public override string ToString() => Value.ToString("D");
}
