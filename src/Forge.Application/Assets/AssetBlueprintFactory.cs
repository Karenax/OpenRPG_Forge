using System.Security.Cryptography;
using System.Text;
using Forge.Application.Projects;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;

namespace Forge.Application.Assets;

public static class AssetBlueprintFactory
{
    public const string AssetIdModificationPrefix = "visual.asset=";
    public const string ManifestIdModificationPrefix = "asset.manifest=";

    public static BlueprintDefinition Create(AssetCatalogItem item, AssetPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(manifest);
        if (item.Id != manifest.AssetId)
        {
            throw new ArgumentException("Asset item and package manifest IDs do not match.", nameof(manifest));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:asset-blueprint:{item.Id}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        var slug = SlugGenerator.FromDisplayName($"asset-{item.Source}-{item.SourceAssetId}", "asset-model");
        return new BlueprintDefinition(
            BlueprintId.From(new Guid(bytes[..16])),
            slug,
            item.DisplayName,
            ResolveKind(item.Intelligence.SemanticType),
            paletteOrigin: BlueprintPaletteOrigin.ProjectContent,
            isFavorite: true,
            provenance: new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                $"asset://catalog/{item.ExternalId}",
                item.Author,
                item.LicenseSpdx,
                manifest.Chunks.Single(chunk => chunk.Role == AssetChunkRole.Model).ContentSha256,
                modifications:
                [
                    $"{AssetIdModificationPrefix}{item.Id}",
                    $"{ManifestIdModificationPrefix}{manifest.Id}",
                ]));
    }

    public static bool TryGetAssetId(BlueprintDefinition blueprint, out AssetCatalogItemId? assetId)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        var values = blueprint.Provenance.Modifications.Where(candidate =>
            candidate.StartsWith(AssetIdModificationPrefix, StringComparison.Ordinal)).ToArray();
        if (values.Length != 1)
        {
            assetId = null;
            return false;
        }

        try
        {
            assetId = AssetCatalogItemId.Parse(values[0][AssetIdModificationPrefix.Length..]);
            return true;
        }
        catch (FormatException)
        {
            assetId = null;
            return false;
        }
    }

    private static BlueprintKind ResolveKind(string semanticType) => semanticType.Trim().ToLowerInvariant() switch
    {
        "door" => BlueprintKind.Door,
        "character" or "creature" => BlueprintKind.Creature,
        "item" or "weapon" => BlueprintKind.Item,
        "spawn" or "player-spawn" => BlueprintKind.PlayerSpawn,
        "trigger" => BlueprintKind.Trigger,
        _ => BlueprintKind.Placeable,
    };
}
