using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record CreatorExperienceProductionProfile
{
    public const int CurrentSchemaVersion = 1;

    public sealed record WaterQualitySettings(Guid Id, bool Refraction, bool ScreenSpaceReflections,
        bool DepthColor, string FallbackQuality);

    public sealed record DeviceQuerySettings(Guid Id, bool Enabled, IReadOnlyList<string> AllowedQueries,
        bool RequireConsent, bool RedactStableIdentifiers);

    public sealed record ResourceCommandSettings(Guid Id, bool Enabled, int MaximumResults,
        long MaximumBytes, IReadOnlyList<string> AllowedNamespaces);

    public sealed record DynamicAreaSettings(Guid Id, bool Enabled, int MaximumInstances,
        int LifetimeSeconds, bool RequireServerAuthority, bool ResolveDependencies);

    public sealed record PartyControlSettings(Guid Id, bool Enabled, int MaximumControlledMembers,
        bool AllowDirectSwitch, IReadOnlyList<string> Commands);

    public sealed record ProgressionTrackSettings(Guid Id, int MaximumTracks, bool DataDriven,
        bool SupportLargeSetUi);

    public sealed record RepositoryMetadataSettings(Guid Id, string Channel, string Compatibility,
        bool RequireSignature, bool RequireChangeNotes);

    public sealed record ObservabilitySettings(Guid Id, bool Enabled, bool TracyCompatible,
        bool OpenTelemetryCompatible, int MaximumCaptureSeconds, bool IncludeScripts);

    public sealed record GraphicsQualitySettings(Guid Id, string AntiAliasing, int AnisotropicLevel,
        string Upscaling, bool CapabilityFallback);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public WaterQualitySettings Water { get; }
    public DeviceQuerySettings DeviceQueries { get; }
    public ResourceCommandSettings ResourceCommands { get; }
    public DynamicAreaSettings DynamicAreas { get; }
    public PartyControlSettings PartyControl { get; }
    public ProgressionTrackSettings ProgressionTracks { get; }
    public RepositoryMetadataSettings RepositoryMetadata { get; }
    public ObservabilitySettings Observability { get; }
    public GraphicsQualitySettings Graphics { get; }
    public string Provenance { get; }

    public CreatorExperienceProductionProfile(Guid id, int schemaVersion, WaterQualitySettings water,
        DeviceQuerySettings deviceQueries, ResourceCommandSettings resourceCommands,
        DynamicAreaSettings dynamicAreas, PartyControlSettings partyControl,
        ProgressionTrackSettings progressionTracks, RepositoryMetadataSettings repositoryMetadata,
        ObservabilitySettings observability, GraphicsQualitySettings graphics, string provenance)
    {
        ArgumentNullException.ThrowIfNull(water);
        ArgumentNullException.ThrowIfNull(deviceQueries);
        ArgumentNullException.ThrowIfNull(resourceCommands);
        ArgumentNullException.ThrowIfNull(dynamicAreas);
        ArgumentNullException.ThrowIfNull(partyControl);
        ArgumentNullException.ThrowIfNull(progressionTracks);
        ArgumentNullException.ThrowIfNull(repositoryMetadata);
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(graphics);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || water.Id == Guid.Empty ||
            water.FallbackQuality is not ("low" or "medium" or "high"))
            throw new ArgumentOutOfRangeException(nameof(id), "Creator experience requires schema v1 and a water fallback.");
        if (deviceQueries.Id == Guid.Empty || deviceQueries.AllowedQueries.Any(string.IsNullOrWhiteSpace) ||
            deviceQueries.Enabled && (!deviceQueries.RequireConsent || !deviceQueries.RedactStableIdentifiers))
            throw new ArgumentException("Device queries require consent, redaction and an allowlist.", nameof(deviceQueries));
        if (resourceCommands.Id == Guid.Empty || resourceCommands.MaximumResults is < 1 or > 10_000 ||
            resourceCommands.MaximumBytes is < 1_024 or > 1_073_741_824 ||
            resourceCommands.AllowedNamespaces.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Resource commands require bounded results, bytes and namespaces.", nameof(resourceCommands));
        if (dynamicAreas.Id == Guid.Empty || dynamicAreas.MaximumInstances is < 1 or > 1_024 ||
            dynamicAreas.LifetimeSeconds is < 1 or > 604_800 || dynamicAreas.Enabled && !dynamicAreas.RequireServerAuthority)
            throw new ArgumentException("Dynamic areas require server authority and bounded lifetime.", nameof(dynamicAreas));
        if (partyControl.Id == Guid.Empty || partyControl.MaximumControlledMembers is < 1 or > 64 ||
            partyControl.Commands.Any(string.IsNullOrWhiteSpace) || progressionTracks.Id == Guid.Empty ||
            progressionTracks.MaximumTracks is < 8 or > 256 || !progressionTracks.DataDriven)
            throw new ArgumentException("Party control and progression tracks require bounded data-driven settings.");
        if (repositoryMetadata.Id == Guid.Empty || string.IsNullOrWhiteSpace(repositoryMetadata.Channel) ||
            string.IsNullOrWhiteSpace(repositoryMetadata.Compatibility) || observability.Id == Guid.Empty ||
            observability.MaximumCaptureSeconds is < 1 or > 3_600)
            throw new ArgumentException("Repository metadata and observability settings are invalid.");
        if (graphics.Id == Guid.Empty || graphics.AntiAliasing is not ("disabled" or "fxaa" or "msaa2" or "msaa4" or "msaa8") ||
            graphics.AnisotropicLevel is not (1 or 2 or 4 or 8 or 16) ||
            graphics.Upscaling is not ("disabled" or "fsr" or "native"))
            throw new ArgumentException("Graphics quality requires supported AA, anisotropy and upscaling.", nameof(graphics));
        var ids = new[] { id, water.Id, deviceQueries.Id, resourceCommands.Id, dynamicAreas.Id, partyControl.Id,
            progressionTracks.Id, repositoryMetadata.Id, observability.Id, graphics.Id };
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Creator experience UUIDs must be unique.");

        Id = id;
        SchemaVersion = schemaVersion;
        Water = water with { FallbackQuality = water.FallbackQuality.Trim().ToLowerInvariant() };
        DeviceQueries = deviceQueries with { AllowedQueries = NormalizeMany(deviceQueries.AllowedQueries) };
        ResourceCommands = resourceCommands with { AllowedNamespaces = NormalizeMany(resourceCommands.AllowedNamespaces) };
        DynamicAreas = dynamicAreas;
        PartyControl = partyControl with { Commands = NormalizeMany(partyControl.Commands) };
        ProgressionTracks = progressionTracks;
        RepositoryMetadata = repositoryMetadata with
        {
            Channel = Normalize(repositoryMetadata.Channel),
            Compatibility = repositoryMetadata.Compatibility.Trim(),
        };
        Observability = observability;
        Graphics = graphics with
        {
            AntiAliasing = graphics.AntiAliasing.Trim().ToLowerInvariant(),
            Upscaling = graphics.Upscaling.Trim().ToLowerInvariant(),
        };
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static CreatorExperienceProductionProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"post-launch:creator:{key}");
        return new CreatorExperienceProductionProfile(Stable("root"), 1,
            new WaterQualitySettings(Stable("water"), true, true, true, "medium"),
            new DeviceQuerySettings(Stable("device-query"), false, ["input-family", "graphics-tier"], true, true),
            new ResourceCommandSettings(Stable("resource-command"), true, 256, 64 * 1024 * 1024,
                ["module", "content", "runtime"]),
            new DynamicAreaSettings(Stable("dynamic-area"), false, 16, 3_600, true, true),
            new PartyControlSettings(Stable("party-control"), true, 8, true, ["follow", "hold", "focus", "regroup"]),
            new ProgressionTrackSettings(Stable("progression"), 64, true, true),
            new RepositoryMetadataSettings(Stable("repository"), "stable", "1.x", true, true),
            new ObservabilitySettings(Stable("observability"), false, true, true, 60, true),
            new GraphicsQualitySettings(Stable("graphics"), "msaa4", 8, "native", true),
            "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => [Id, Water.Id, DeviceQueries.Id, ResourceCommands.Id, DynamicAreas.Id,
        PartyControl.Id, ProgressionTracks.Id, RepositoryMetadata.Id, Observability.Id, Graphics.Id];

    private static string Normalize(string value) => DataDrivenRuleSchema.NormalizeKey(value, nameof(value));
    private static IReadOnlyList<string> NormalizeMany(IReadOnlyList<string> values) => values.Select(Normalize)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
