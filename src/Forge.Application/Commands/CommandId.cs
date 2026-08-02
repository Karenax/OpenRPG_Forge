namespace Forge.Application.Commands;

public sealed record CommandId
{
    public Guid Value { get; }

    private CommandId(Guid value)
    {
        Value = value;
    }

    public static CommandId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A command ID cannot be empty.", nameof(value));
        }

        return new CommandId(value);
    }

    public override string ToString() => Value.ToString("D");
}
