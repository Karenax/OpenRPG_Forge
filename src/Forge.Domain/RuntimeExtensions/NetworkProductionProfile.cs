using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record NetworkProductionProfile
{
    public const int CurrentSchemaVersion = 1;

    public sealed record VoiceSettings(Guid Id, bool Enabled, string Codec, int MaximumBitrateKbps,
        bool Positional, bool PushToTalk, bool ModerationEnabled);

    public sealed record ShardDefinition(Guid Id, string Key, IReadOnlyList<Guid> AreaIds,
        int MaximumPlayers, string Region);

    public sealed record HotReloadSettings(Guid Id, bool Enabled, bool AtomicSwap, bool RequireRollback,
        int MaximumPackageMegabytes, int DrainTimeoutSeconds);

    public sealed record ServerExtensionSettings(Guid Id, bool Enabled, bool RequireSignature,
        bool IsolateProcesses, IReadOnlyList<string> AllowedCapabilities, int CpuBudgetMilliseconds);

    public sealed record ProtocolCompatibilitySettings(Guid Id, string ProtocolFamily,
        int MinimumVersion, int MaximumVersion, IReadOnlyList<string> Platforms,
        bool RejectUnknownFields);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public VoiceSettings Voice { get; }
    public IReadOnlyList<ShardDefinition> Shards { get; }
    public HotReloadSettings HotReload { get; }
    public ServerExtensionSettings ServerExtensions { get; }
    public ProtocolCompatibilitySettings Protocol { get; }
    public string Provenance { get; }

    public NetworkProductionProfile(Guid id, int schemaVersion, VoiceSettings voice,
        IReadOnlyList<ShardDefinition> shards, HotReloadSettings hotReload,
        ServerExtensionSettings serverExtensions, ProtocolCompatibilitySettings protocol, string provenance)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(shards);
        ArgumentNullException.ThrowIfNull(hotReload);
        ArgumentNullException.ThrowIfNull(serverExtensions);
        ArgumentNullException.ThrowIfNull(protocol);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || voice.Id == Guid.Empty ||
            voice.Codec is not ("opus" or "pcm") || voice.MaximumBitrateKbps is < 8 or > 512)
            throw new ArgumentOutOfRangeException(nameof(id), "Network production requires schema v1 and a supported voice profile.");
        var orderedShards = shards.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        if (orderedShards.Any(item => item.Id == Guid.Empty || item.MaximumPlayers is < 1 or > 100_000 ||
                string.IsNullOrWhiteSpace(item.Region) || item.AreaIds.Count == 0 ||
                item.AreaIds.Any(areaId => areaId == Guid.Empty) || item.AreaIds.Distinct().Count() != item.AreaIds.Count) ||
            orderedShards.Select(item => item.Id).Distinct().Count() != orderedShards.Length ||
            orderedShards.Select(item => Normalize(item.Key)).Distinct(StringComparer.Ordinal).Count() != orderedShards.Length)
            throw new ArgumentException("Shards require unique identities, areas, regions and bounded capacities.", nameof(shards));
        if (hotReload.Id == Guid.Empty || hotReload.MaximumPackageMegabytes is < 1 or > 65_536 ||
            hotReload.DrainTimeoutSeconds is < 1 or > 3_600 || hotReload.Enabled && (!hotReload.AtomicSwap || !hotReload.RequireRollback))
            throw new ArgumentException("Server hot reload must be atomic, reversible and bounded.", nameof(hotReload));
        if (serverExtensions.Id == Guid.Empty || serverExtensions.CpuBudgetMilliseconds is < 1 or > 10_000 ||
            serverExtensions.Enabled && (!serverExtensions.RequireSignature || !serverExtensions.IsolateProcesses) ||
            serverExtensions.AllowedCapabilities.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Server extensions must be signed, isolated and quota-bound.", nameof(serverExtensions));
        if (protocol.Id == Guid.Empty || string.IsNullOrWhiteSpace(protocol.ProtocolFamily) ||
            protocol.MinimumVersion < 1 || protocol.MaximumVersion < protocol.MinimumVersion ||
            protocol.Platforms.Count == 0 || protocol.Platforms.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Protocol compatibility requires a version range and platforms.", nameof(protocol));
        var ids = orderedShards.Select(item => item.Id).Append(id).Append(voice.Id).Append(hotReload.Id)
            .Append(serverExtensions.Id).Append(protocol.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Network production UUIDs must be unique.");

        Id = id;
        SchemaVersion = schemaVersion;
        Voice = voice with { Codec = voice.Codec.Trim().ToLowerInvariant() };
        Shards = orderedShards.Select(item => item with
        {
            Key = Normalize(item.Key),
            AreaIds = item.AreaIds.Order().ToArray(),
            Region = Normalize(item.Region),
        }).ToArray();
        HotReload = hotReload;
        ServerExtensions = serverExtensions with
        {
            AllowedCapabilities = serverExtensions.AllowedCapabilities.Select(Normalize)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
        Protocol = protocol with
        {
            ProtocolFamily = Normalize(protocol.ProtocolFamily),
            Platforms = protocol.Platforms.Select(Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static NetworkProductionProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"post-launch:network:{key}");
        return new NetworkProductionProfile(Stable("root"), 1,
            new VoiceSettings(Stable("voice"), false, "opus", 64, true, true, true), [],
            new HotReloadSettings(Stable("hot-reload"), false, true, true, 512, 60),
            new ServerExtensionSettings(Stable("extensions"), false, true, true, [], 50),
            new ProtocolCompatibilitySettings(Stable("protocol"), "openrpg-net", 1, 1,
                ["windows", "linux", "macos"], true), "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => Shards.Select(item => item.Id).Append(Id).Append(Voice.Id)
        .Append(HotReload.Id).Append(ServerExtensions.Id).Append(Protocol.Id).ToArray();

    private static string Normalize(string value) => DataDrivenRuleSchema.NormalizeKey(value, nameof(value));
}
