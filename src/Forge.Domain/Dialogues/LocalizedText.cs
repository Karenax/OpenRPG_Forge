namespace Forge.Domain.Dialogues;

public sealed record LocalizedText
{
    public string Key { get; }

    public string Fallback { get; }

    public LocalizedText(string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        Key = key.Trim();
        Fallback = fallback.Trim();
    }
}
