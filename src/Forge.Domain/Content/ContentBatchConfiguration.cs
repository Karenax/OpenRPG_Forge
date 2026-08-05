namespace Forge.Domain.Content;

public sealed record ContentBatchConfiguration
{
    public string Glob { get; }

    public IReadOnlyList<string> Extensions { get; }

    public ContentBatchCollisionPolicy CollisionPolicy { get; }

    public string Provenance { get; }

    public string LicenseSpdx { get; }

    public ContentBatchConfiguration(
        string glob,
        IReadOnlyList<string> extensions,
        ContentBatchCollisionPolicy collisionPolicy,
        string provenance,
        string licenseSpdx)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(glob);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        if (Path.IsPathRooted(glob) || glob.Contains("..", StringComparison.Ordinal) || glob.Contains('\\'))
        {
            throw new ArgumentException("The content glob must be a safe forward-slash relative expression.", nameof(glob));
        }

        var normalizedExtensions = extensions
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedExtensions.Length == 0)
        {
            throw new ArgumentException("At least one content extension is required.", nameof(extensions));
        }

        Glob = glob.Trim();
        Extensions = normalizedExtensions;
        CollisionPolicy = collisionPolicy;
        Provenance = provenance.Trim();
        LicenseSpdx = licenseSpdx.Trim();
    }

    public static ContentBatchConfiguration CreateDefault() =>
        new("*", [".json", ".forge", ".glb", ".png", ".ogg"], ContentBatchCollisionPolicy.Fail,
            "project-author", "UNLICENSED");

    private static string NormalizeExtension(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (!normalized.StartsWith(".", StringComparison.Ordinal) || normalized.Length < 2 ||
            normalized.Skip(1).Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException($"Content extension '{value}' is invalid.", nameof(value));
        }

        return normalized;
    }
}
