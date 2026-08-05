namespace Forge.Domain.Gameplay;

public enum CombatModeKind
{
    RealTime,
    RealTimeWithPause,
    TurnBased,
    Custom,
}

public sealed record CombatModeProviderDefinition
{
    public Guid Id { get; }
    public string ProviderId { get; }
    public string DisplayName { get; }
    public CombatModeKind Kind { get; }
    public string CustomProvider { get; }
    public IReadOnlyList<string> EventContracts { get; }
    public bool ServerAuthoritative { get; }

    public CombatModeProviderDefinition(Guid id, string providerId, string displayName, CombatModeKind kind,
        string customProvider, IReadOnlyList<string> eventContracts, bool serverAuthoritative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(eventContracts);
        var events = eventContracts.Select(Normalize).Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || (kind == CombatModeKind.Custom && string.IsNullOrWhiteSpace(customProvider)) ||
            events.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Combat mode provider is invalid.");
        }

        Id = id;
        ProviderId = Normalize(providerId);
        DisplayName = displayName.Trim();
        Kind = kind;
        CustomProvider = (customProvider ?? string.Empty).Trim();
        EventContracts = events;
        ServerAuthoritative = serverAuthoritative;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public sealed record CombatModeProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string DefaultProviderId { get; }
    public IReadOnlyList<CombatModeProviderDefinition> Providers { get; }
    public string Provenance { get; }

    public CombatModeProfile(Guid id, int schemaVersion, string defaultProviderId,
        IReadOnlyList<CombatModeProviderDefinition> providers, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultProviderId);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = providers.OrderBy(value => value.ProviderId, StringComparer.Ordinal).ToArray();
        var normalizedDefault = defaultProviderId.Trim().ToLowerInvariant();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(value => value.Id).Distinct().Count() != values.Length ||
            values.Select(value => value.ProviderId).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.All(value => value.ProviderId != normalizedDefault))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Combat mode profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        DefaultProviderId = normalizedDefault;
        Providers = values;
        Provenance = provenance.Trim();
    }

    public CombatModeProviderDefinition DefaultProvider =>
        Providers.Single(provider => provider.ProviderId == DefaultProviderId);
}
