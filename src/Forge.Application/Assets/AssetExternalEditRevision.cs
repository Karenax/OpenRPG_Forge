namespace Forge.Application.Assets;

public readonly record struct AssetExternalEditRevision
{
    public long Value { get; }

    public AssetExternalEditRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "An external edit revision cannot be negative.");
        }

        Value = value;
    }

    public AssetExternalEditRevision Next() => new(checked(Value + 1));
}
