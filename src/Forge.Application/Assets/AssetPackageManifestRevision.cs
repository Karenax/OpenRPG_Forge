namespace Forge.Application.Assets;

public readonly record struct AssetPackageManifestRevision(long Value)
{
    public AssetPackageManifestRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
