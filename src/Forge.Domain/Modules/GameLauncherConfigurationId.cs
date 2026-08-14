namespace Forge.Domain.Modules;

public sealed record GameLauncherConfigurationId
{
    public Guid Value { get; }

    private GameLauncherConfigurationId(Guid value)
    {
        Value = value;
    }

    public static GameLauncherConfigurationId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A game launcher configuration ID cannot be empty.", nameof(value));
        }

        return new GameLauncherConfigurationId(value);
    }

    public static GameLauncherConfigurationId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid game launcher configuration ID.");
    }

    public override string ToString() => Value.ToString("D");
}
