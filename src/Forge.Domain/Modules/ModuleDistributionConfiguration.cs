using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Modules;

public sealed record ModuleDistributionConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public ModuleDistributionConfigurationId Id { get; }

    public int SchemaVersion { get; }

    public IReadOnlyList<ModuleTargetProfile> TargetProfiles { get; }

    public IReadOnlyList<ThirdPartyDependencyApproval> Dependencies { get; }

    public ModuleReleaseConfiguration Release { get; }

    public GameLauncherConfiguration Launcher { get; }

    public string Provenance { get; }

    public ModuleDistributionConfiguration(
        ModuleDistributionConfigurationId id,
        IReadOnlyList<ModuleTargetProfile> targetProfiles,
        IReadOnlyList<ThirdPartyDependencyApproval>? dependencies,
        string provenance,
        ModuleReleaseConfiguration? release = null,
        int schemaVersion = CurrentSchemaVersion,
        GameLauncherConfiguration? launcher = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(targetProfiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Module distribution schema '{schemaVersion}' is not supported.");
        }

        var profiles = targetProfiles.OrderBy(profile => profile.Target).ToArray();
        var expectedTargets = Enum.GetValues<ModuleTargetKind>();
        if (profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Length)
        {
            throw new ArgumentException("Module target profile IDs must be unique.", nameof(targetProfiles));
        }

        if (profiles.Select(profile => profile.Target).Distinct().Count() != profiles.Length ||
            expectedTargets.Any(target => profiles.All(profile => profile.Target != target)))
        {
            throw new ArgumentException(
                "Module distribution requires exactly one Editor, Client, DedicatedServer, Test and Shipping profile.",
                nameof(targetProfiles));
        }

        var approvals = (dependencies ?? []).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (approvals.Select(item => item.Id).Distinct().Count() != approvals.Length)
        {
            throw new ArgumentException("Third-party dependency IDs must be unique.", nameof(dependencies));
        }

        if (approvals.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != approvals.Length)
        {
            throw new ArgumentException("Third-party dependency names must be unique.", nameof(dependencies));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        TargetProfiles = profiles;
        Dependencies = approvals;
        Release = release ?? ModuleReleaseConfiguration.CreateDefault(id);
        Launcher = launcher ?? GameLauncherConfiguration.CreateDisabled(id);
        Provenance = provenance.Trim();
    }

    public ModuleTargetProfile GetProfile(ModuleTargetKind target) =>
        TargetProfiles.Single(profile => profile.Target == target);

    public static ModuleDistributionConfiguration CreateDefault(ModuleId moduleId, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        var profiles = Enum.GetValues<ModuleTargetKind>()
            .Select(target => CreateDefaultProfile(moduleId, target))
            .ToArray();
        return new ModuleDistributionConfiguration(
            ModuleDistributionConfigurationId.From(CreateStableGuid(moduleId, "distribution")),
            profiles,
            [],
            "migration-default",
            ModuleReleaseConfiguration.CreateDefault(
                ModuleDistributionConfigurationId.From(CreateStableGuid(moduleId, "distribution"))),
            launcher: GameLauncherConfiguration.CreateDefault(moduleId, displayName ?? "Jeu OpenRPG"));
    }

    private static ModuleTargetProfile CreateDefaultProfile(ModuleId moduleId, ModuleTargetKind target)
    {
        var authoring = target is ModuleTargetKind.Editor or ModuleTargetKind.Test;
        var distributable = target is ModuleTargetKind.Client or ModuleTargetKind.DedicatedServer or
            ModuleTargetKind.Shipping;
        return new ModuleTargetProfile(
            ModuleTargetProfileId.From(CreateStableGuid(moduleId, $"target:{target}")),
            target,
            enabled: true,
            includeEditorData: authoring,
            includeDebugSymbols: authoring,
            optimizeContent: distributable,
            requireApprovedLicenses: distributable,
            outputName: target.ToString().ToLowerInvariant(),
            secretsProfile: target.ToString().ToLowerInvariant(),
            provenance: "migration-default");
    }

    private static Guid CreateStableGuid(ModuleId moduleId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{moduleId}:{discriminator}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
