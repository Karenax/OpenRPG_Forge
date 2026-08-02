namespace Forge.Application.Creator;

public readonly record struct EditorCameraRevision
{
    public long Value { get; }

    public EditorCameraRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public EditorCameraRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
