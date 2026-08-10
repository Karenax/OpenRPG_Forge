using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Rules;

public sealed record RulesetPluginPackage
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string Version { get; }
    public string ContentPackageId { get; }
    public IReadOnlyList<string> Attributes { get; }
    public IReadOnlyList<string> Effects { get; }
    public IReadOnlyList<string> Abilities { get; }
    public IReadOnlyList<string> Inventories { get; }
    public IReadOnlyList<string> ProgressionTracks { get; }
    public IReadOnlyList<string> UiPanels { get; }
    public string Provenance { get; }

    public RulesetPluginPackage(Guid id, int schemaVersion, string key, string version, string contentPackageId,
        IReadOnlyList<string>? attributes, IReadOnlyList<string>? effects, IReadOnlyList<string>? abilities,
        IReadOnlyList<string>? inventories, IReadOnlyList<string>? progressionTracks,
        IReadOnlyList<string>? uiPanels, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "Ruleset plugin identity or schema is invalid.");
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPackageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        Id = id;
        SchemaVersion = schemaVersion;
        Key = NormalizeKey(key);
        Version = version.Trim();
        ContentPackageId = NormalizeKey(contentPackageId);
        Attributes = NormalizeRegistry(attributes);
        Effects = NormalizeRegistry(effects);
        Abilities = NormalizeRegistry(abilities);
        Inventories = NormalizeRegistry(inventories);
        ProgressionTracks = NormalizeRegistry(progressionTracks);
        UiPanels = NormalizeRegistry(uiPanels);
        Provenance = provenance.Trim();
    }

    internal static string NormalizeKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 96 ||
            !normalized.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'))
            throw new ArgumentException($"Ruleset identifier '{value}' is not canonical.");
        return normalized;
    }

    private static IReadOnlyList<string> NormalizeRegistry(IReadOnlyList<string>? values) => (values ?? [])
        .Select(NormalizeKey).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
}

public sealed record RulesetPluginCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public string ActivePackageKey { get; }
    public IReadOnlyList<RulesetPluginPackage> Packages { get; }
    public string Provenance { get; }

    public RulesetPluginCatalog(Guid id, int schemaVersion, bool enabled, bool requiredAtRuntime,
        string activePackageKey, IReadOnlyList<RulesetPluginPackage>? packages, string provenance)
    {
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "Ruleset plugin catalog identity or schema is invalid.");
        ArgumentNullException.ThrowIfNull(activePackageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        ActivePackageKey = string.IsNullOrWhiteSpace(activePackageKey)
            ? string.Empty
            : RulesetPluginPackage.NormalizeKey(activePackageKey);
        Packages = (packages ?? []).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        Provenance = provenance.Trim();
    }

    public static RulesetPluginCatalog CreateDefault(Guid ownerId)
    {
        var package = new RulesetPluginPackage(StableId(ownerId, "ruleset-package:forge-generic"), 1,
            "forge.generic", "1.0.0", "content.core",
            ["ability", "armor_class", "health", "level"], ["damage", "healing", "status"],
            ["attack", "interact", "use_item"], ["equipment", "inventory"], ["character_level"],
            ["character_sheet", "inventory", "progression"], "migration-default");
        return new RulesetPluginCatalog(StableId(ownerId, "ruleset-plugin-catalog"), 1, true, true,
            package.Key, [package], "migration-default");
    }

    private static Guid StableId(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ownerId:D}:{discriminator}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
