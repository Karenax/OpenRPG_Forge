namespace Forge.Domain.Content;

public sealed record ContentWorkspaceConfigurationId
{
    public Guid Value { get; }

    private ContentWorkspaceConfigurationId(Guid value) => Value = value;

    public static ContentWorkspaceConfigurationId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A content workspace configuration ID cannot be empty.", nameof(value))
            : new ContentWorkspaceConfigurationId(value);

    public static ContentWorkspaceConfigurationId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid content workspace configuration ID.");

    public override string ToString() => Value.ToString("D");
}
