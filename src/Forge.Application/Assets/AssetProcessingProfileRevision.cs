namespace Forge.Application.Assets;

public readonly record struct AssetProcessingProfileRevision
{
    public long Value { get; }

    public AssetProcessingProfileRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "An asset processing profile revision cannot be negative.");
        }

        Value = value;
    }

    public AssetProcessingProfileRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
