namespace Forge.Domain.Assistant;

public sealed record AssistantPromptTestCase
{
    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, string> Variables { get; }
    public IReadOnlyList<string> ExpectedFragments { get; }

    public AssistantPromptTestCase(
        Guid id,
        string name,
        IReadOnlyDictionary<string, string>? variables,
        IReadOnlyList<string>? expectedFragments)
    {
        Id = id;
        Name = (name ?? string.Empty).Trim();
        Variables = (variables ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key.Trim(), item => item.Value ?? string.Empty, StringComparer.Ordinal);
        ExpectedFragments = (expectedFragments ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
