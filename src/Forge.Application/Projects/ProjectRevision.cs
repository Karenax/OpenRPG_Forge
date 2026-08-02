namespace Forge.Application.Projects;

public readonly record struct ProjectRevision
{
    public long Value { get; }

    public ProjectRevision(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A project revision cannot be negative.");
        }

        Value = value;
    }

    public ProjectRevision Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
