using System.Text.RegularExpressions;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Validation;

namespace Forge.Domain.Projects;

public enum ProjectTemplateKind
{
    Rpg,
    CoreMenu,
    CoreSimulation,
}

public enum ProjectRenderPreset
{
    Balanced,
    Quality,
    Performance,
}

public enum ProjectNetworkMode
{
    Offline,
    PeerToPeer,
    DedicatedServer,
}

public sealed record ProjectTechnicalIdentity(
    ProjectId ProjectId,
    string Slug,
    string PackageNamespace,
    string FormatCompatibility);

public sealed record PlayerStartProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Key,
    AreaId AreaId,
    EntityId SpawnEntityId,
    Guid? WaypointId,
    float OrientationDegrees,
    string GameMode,
    string EntryClass,
    string PlayerSelectionRule,
    int Priority,
    bool RequiredAtRuntime)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record ProjectFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ProjectTemplateKind Template,
    IReadOnlyList<string> Kits,
    IReadOnlyList<ModuleDistributionPlatform> TargetPlatforms,
    string RulesProvider,
    ProjectRenderPreset RenderPreset,
    ProjectNetworkMode NetworkMode,
    bool RequiresInitialArea,
    ProjectTechnicalIdentity Identity,
    IReadOnlyList<PlayerStartProfile> PlayerStarts,
    bool RequiredAtRuntime)
{
    public const int CurrentSchemaVersion = 1;

    public static ProjectFoundationProfile CreateRpgDefault(
        Guid id,
        ProjectId projectId,
        string slug,
        AreaId areaId,
        EntityId spawnEntityId,
        string? packageNamespace = null) => new(
        id,
        CurrentSchemaVersion,
        "openrpg-forge:project-wizard",
        ProjectTemplateKind.Rpg,
        ["forge-core", "forge-rpg-kit"],
        [ModuleDistributionPlatform.WindowsX64],
        "forge.generic",
        ProjectRenderPreset.Balanced,
        ProjectNetworkMode.Offline,
        true,
        new(projectId, slug, string.IsNullOrWhiteSpace(packageNamespace) ? $"module.{slug}" : packageNamespace,
            "1.x"),
        [new PlayerStartProfile(
            StableGuid(id, "player-start:default"),
            PlayerStartProfile.CurrentSchemaVersion,
            "openrpg-forge:project-wizard",
            "default",
            areaId,
            spawnEntityId,
            null,
            0,
            "campaign",
            "adventurer",
            "default",
            100,
            true)],
        true);

    public static ProjectFoundationProfile CreateCoreDefault(
        Guid id,
        ProjectId projectId,
        string slug,
        ProjectTemplateKind template,
        string? packageNamespace = null) => new(
        id,
        CurrentSchemaVersion,
        "openrpg-forge:project-wizard",
        template,
        ["forge-core"],
        [ModuleDistributionPlatform.WindowsX64],
        "forge.core",
        ProjectRenderPreset.Balanced,
        ProjectNetworkMode.Offline,
        false,
        new(projectId, slug, string.IsNullOrWhiteSpace(packageNamespace) ? $"module.{slug}" : packageNamespace,
            "1.x"),
        [],
        true);

    private static Guid StableGuid(Guid id, string discriminator)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"openrpg-forge:{id:D}:{discriminator}"));
        Span<byte> value = stackalloc byte[16];
        bytes.AsSpan(0, value.Length).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }
}

public static partial class ProjectFoundationValidator
{
    public const string IdentityCode = "project.foundation.identity_invalid";
    public const string SchemaCode = "project.foundation.schema_unsupported";
    public const string ChoicesCode = "project.foundation.choices_invalid";
    public const string InitialAreaCode = "project.foundation.initial_area_invalid";
    public const string PlayerStartCode = "project.foundation.player_start_invalid";
    public const string PlayerStartReferenceCode = "project.foundation.player_start_reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        ProjectFoundationProfile profile,
        ProjectDefinition? project = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var root = $"module/metadata/foundation/{profile.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance) ||
            profile.Identity.ProjectId is null || string.IsNullOrWhiteSpace(profile.Identity.Slug) ||
            !SlugPattern().IsMatch(profile.Identity.Slug) ||
            !NamespacePattern().IsMatch(profile.Identity.PackageNamespace) ||
            !FormatPattern().IsMatch(profile.Identity.FormatCompatibility))
            diagnostics.Add(new(IdentityCode,
                "Foundation UUID, provenance, project UUID, slug, package namespace and format compatibility are required.",
                $"{root}/identity"));
        if (profile.SchemaVersion != ProjectFoundationProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaCode, "Project foundation schema must be 1.", $"{root}/schemaVersion"));
        if (!Enum.IsDefined(profile.Template) || !Enum.IsDefined(profile.RenderPreset) ||
            !Enum.IsDefined(profile.NetworkMode) || profile.Kits.Count is < 1 or > 16 ||
            profile.Kits.Any(item => !KeyPattern().IsMatch(item)) ||
            profile.Kits.Distinct(StringComparer.Ordinal).Count() != profile.Kits.Count ||
            profile.TargetPlatforms.Count == 0 || profile.TargetPlatforms.Any(item => !Enum.IsDefined(item)) ||
            profile.TargetPlatforms.Distinct().Count() != profile.TargetPlatforms.Count ||
            !KeyPattern().IsMatch(profile.RulesProvider))
            diagnostics.Add(new(ChoicesCode,
                "Template, unique kits, target platforms, rules, rendering and network choices must be valid and bounded.",
                $"{root}/choices"));
        if (profile.Template == ProjectTemplateKind.Rpg &&
            (!profile.RequiresInitialArea || profile.PlayerStarts.Count == 0))
            diagnostics.Add(new(InitialAreaCode,
                "The RPG template requires an initial area and at least one player start profile.",
                $"{root}/requiresInitialArea"));
        var starts = profile.PlayerStarts.ToArray();
        if (starts.Select(item => item.Id).Distinct().Count() != starts.Length ||
            starts.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != starts.Length ||
            starts.Select(item => item.PlayerSelectionRule).Distinct(StringComparer.Ordinal).Count() != starts.Length ||
            starts.Any(item => item.Id == Guid.Empty || item.SchemaVersion != PlayerStartProfile.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Provenance) || !KeyPattern().IsMatch(item.Key) ||
                !float.IsFinite(item.OrientationDegrees) || item.OrientationDegrees is < 0 or >= 360 ||
                !KeyPattern().IsMatch(item.GameMode) || !KeyPattern().IsMatch(item.EntryClass) ||
                !SelectionPattern().IsMatch(item.PlayerSelectionRule) || item.Priority is < -10_000 or > 10_000))
            diagnostics.Add(new(PlayerStartCode,
                "Player starts require unique UUIDs, keys and selection rules plus bounded orientation and typed choices.",
                $"{root}/playerStarts"));
        if (starts.Length > 0 && !starts.Any(item => item.PlayerSelectionRule == "default"))
            diagnostics.Add(new(PlayerStartCode, "Player start profiles require one deterministic default rule.",
                $"{root}/playerStarts"));
        if (project is null) return diagnostics;
        if (profile.Identity.ProjectId != project.Id ||
            !string.Equals(profile.Identity.Slug, project.Module.Slug, StringComparison.Ordinal))
            diagnostics.Add(new(IdentityCode, "Foundation project UUID and slug must match the immutable project identity.",
                $"{root}/identity"));
        if (profile.RequiresInitialArea && project.Areas.Count == 0)
            diagnostics.Add(new(InitialAreaCode, "The selected project target requires an initial area.",
                "module/entryAreaId"));
        foreach (var start in starts)
        {
            var area = project.Areas.SingleOrDefault(item => item.Id == start.AreaId);
            if (area is null)
            {
                diagnostics.Add(new(PlayerStartReferenceCode,
                    $"Player start '{start.Key}' references a missing area.",
                    $"{root}/playerStarts/{start.Id:D}/areaId"));
                continue;
            }
            var entity = area.Entities.SingleOrDefault(item => item.Id == start.SpawnEntityId);
            var blueprint = entity is null
                ? null
                : project.Blueprints.SingleOrDefault(item => item.Id == entity.BlueprintId);
            if (entity is null || blueprint?.Kind != BlueprintKind.PlayerSpawn)
                diagnostics.Add(new(PlayerStartReferenceCode,
                    $"Player start '{start.Key}' must reference a PlayerSpawn entity in its area.",
                    $"{root}/playerStarts/{start.Id:D}/spawnEntityId"));
            if (start.WaypointId is { } waypoint &&
                !area.Navigation.Anchors.Instances.Any(item => item.Id == waypoint))
                diagnostics.Add(new(PlayerStartReferenceCode,
                    $"Player start '{start.Key}' references a missing waypoint anchor.",
                    $"{root}/playerStarts/{start.Id:D}/waypointId"));
        }
        return diagnostics;
    }

    [GeneratedRegex("^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^(default|player:[a-z0-9._-]+|tag:[a-z0-9._-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SelectionPattern();

    [GeneratedRegex("^[0-9]+(?:\\.[0-9x*]+){0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatPattern();
}

public sealed record PlayerStartSelectionContext(string PlayerId, string GameMode, string EntryClass,
    IReadOnlySet<string> Tags);

public sealed record PlayerStartSelectionResult(
    bool Succeeded,
    PlayerStartProfile? Profile,
    DomainDiagnostic? Diagnostic);

public static class PlayerStartProfileResolver
{
    public static PlayerStartSelectionResult Resolve(
        ProjectFoundationProfile? foundation,
        PlayerStartSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (foundation is null)
            return new(true, null, null);
        var match = foundation.PlayerStarts.Where(item =>
                string.Equals(item.GameMode, context.GameMode, StringComparison.Ordinal) &&
                (string.Equals(item.EntryClass, context.EntryClass, StringComparison.Ordinal) || item.EntryClass == "any") &&
                Matches(item.PlayerSelectionRule, context))
            .OrderByDescending(item => item.Priority).ThenBy(item => item.Id).FirstOrDefault();
        if (match is not null) return new(true, match, null);
        var required = foundation.RequiredAtRuntime || foundation.PlayerStarts.Any(item => item.RequiredAtRuntime);
        return required
            ? new(false, null, new DomainDiagnostic(ProjectFoundationValidator.PlayerStartReferenceCode,
                "No required player start profile matches the player context.", "module/metadata/foundation/playerStarts"))
            : new(true, null, null);
    }

    private static bool Matches(string rule, PlayerStartSelectionContext context) =>
        rule == "default" ||
        rule.StartsWith("player:", StringComparison.Ordinal) &&
        string.Equals(rule[7..], context.PlayerId, StringComparison.Ordinal) ||
        rule.StartsWith("tag:", StringComparison.Ordinal) && context.Tags.Contains(rule[4..]);
}
