using Forge.Domain.Validation;
using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public static class AreaFoundationValidator
{
    public const string IdentityCode = "area.foundation.identity_invalid";
    public const string SchemaCode = "area.foundation.schema_unsupported";
    public const string CreationCode = "area.foundation.creation_invalid";
    public const string DuplicateCode = "area.foundation.uuid_duplicate";
    public const string ReferenceCode = "area.foundation.reference_missing";
    public const string TileCode = "area.foundation.tile_invalid";
    public const string SocketCode = "area.foundation.socket_invalid";
    public const string LifecycleCode = "area.foundation.lifecycle_invalid";
    public const string GeometryCode = "area.foundation.geometry_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area, AreaFoundationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(profile);
        var root = $"areas/{area.Id}/foundation";
        var diagnostics = new List<DomainDiagnostic>();

        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance) ||
            profile.TileKit.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.TileKit.Provenance))
        {
            diagnostics.Add(new(IdentityCode, "Foundation and tile-kit identity/provenance are required.", root));
        }

        if (profile.SchemaVersion != AreaFoundationProfile.CurrentSchemaVersion ||
            profile.TileKit.SchemaVersion != ModularTileKitProfile.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaCode, "Foundation or tile-kit schema version is unsupported.", root));
        }

        var creation = profile.Creation;
        if (new[] { creation.TemplateKey, creation.KitKey, creation.TerrainProfile, creation.LightingProfile,
                creation.NavigationProfile, creation.StreamingProfile, creation.Biome }
            .Any(string.IsNullOrWhiteSpace) || creation.RuntimeBudgetMegabytes is < 32 or > 65536)
        {
            diagnostics.Add(new(CreationCode, "Creation presets and a 32–65536 MiB runtime budget are required.",
                $"{root}/creation"));
        }

        if (!float.IsFinite(profile.TileKit.GridSizeMeters) || profile.TileKit.GridSizeMeters is <= 0 or > 1024)
        {
            diagnostics.Add(new(TileCode, "Tile grid size must be finite and within 0–1024 metres.",
                $"{root}/tileKit/gridSizeMeters"));
        }

        var identities = new List<Guid> { profile.Id, profile.TileKit.Id };
        identities.AddRange(profile.TileKit.Definitions.Select(item => item.Id));
        identities.AddRange(profile.TileKit.Definitions.SelectMany(item => item.Sockets).Select(item => item.Id));
        identities.AddRange(profile.TileKit.Definitions.SelectMany(item => item.Variants).Select(item => item.Id));
        identities.AddRange(profile.TileKit.ConnectionRules.Select(item => item.Id));
        identities.AddRange(profile.TileKit.Placements.Select(item => item.Id));
        identities.AddRange(profile.TileKit.Groups.Select(item => item.Id));
        identities.AddRange(profile.TileKit.Brushes.Select(item => item.Id));
        identities.AddRange(profile.Volumes.Select(item => item.Id));
        identities.AddRange(profile.Dependencies.Select(item => item.Id));
        identities.AddRange(new[] { profile.Lifecycle.Entered?.Id, profile.Lifecycle.Exited?.Id }
            .Where(item => item.HasValue).Select(item => item!.Value));
        if (identities.Any(item => item == Guid.Empty) || identities.Distinct().Count() != identities.Count)
        {
            diagnostics.Add(new(DuplicateCode, "Foundation child UUIDs must be non-empty and globally unique.", root));
        }

        var definitions = profile.TileKit.Definitions.ToDictionary(item => item.Id);
        var groups = profile.TileKit.Groups.ToDictionary(item => item.Id);
        var placementIds = profile.TileKit.Placements.Select(item => item.Id).ToHashSet();
        var entityIds = area.Entities.Select(item => item.Id).ToHashSet();
        if (profile.TileKit.Placements.Select(item => item.EntityId).Distinct().Count() !=
                profile.TileKit.Placements.Count ||
            profile.TileKit.Placements.Select(item => (item.GridX, item.GridZ)).Distinct().Count() !=
                profile.TileKit.Placements.Count)
        {
            diagnostics.Add(new(TileCode,
                "Tile placements must reference unique entities and occupy unique grid cells.",
                $"{root}/tileKit/placements"));
        }

        foreach (var definition in profile.TileKit.Definitions)
        {
            var path = $"{root}/tileKit/definitions/{definition.Id:D}";
            if (string.IsNullOrWhiteSpace(definition.Key) || definition.BlueprintId is null ||
                definition.Sockets.Select(item => item.Id).Distinct().Count() != definition.Sockets.Count ||
                definition.Variants.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != definition.Variants.Count)
            {
                diagnostics.Add(new(TileCode, "Tile definition key, blueprint, sockets and variants are invalid.", path));
            }

            foreach (var socket in definition.Sockets)
            {
                if (string.IsNullOrWhiteSpace(socket.Name) || string.IsNullOrWhiteSpace(socket.Type) ||
                    !IsFinite(socket.LocalTransform))
                {
                    diagnostics.Add(new(SocketCode, "Tile socket name, type and local transform must be valid.",
                        $"{path}/sockets/{socket.Id:D}"));
                }
            }
        }

        foreach (var placement in profile.TileKit.Placements)
        {
            var path = $"{root}/tileKit/placements/{placement.Id:D}";
            if (!definitions.TryGetValue(placement.DefinitionId, out var definition) ||
                !entityIds.Contains(placement.EntityId))
            {
                diagnostics.Add(new(ReferenceCode, "Tile placement must reference an existing definition and area entity.", path));
                continue;
            }

            var rotationStep = definition.RotationMode switch
            {
                ModularTileRotationMode.Orthogonal => 90f,
                ModularTileRotationMode.Octagonal => 45f,
                _ => 0f,
            };
            if (!float.IsFinite(placement.RotationDegrees) ||
                (rotationStep > 0 && Math.Abs(placement.RotationDegrees / rotationStep -
                    MathF.Round(placement.RotationDegrees / rotationStep)) > 0.0001f) ||
                placement.Mirror == ModularTileMirrorMode.X && !definition.AllowMirrorX ||
                placement.Mirror == ModularTileMirrorMode.Z && !definition.AllowMirrorZ ||
                !string.IsNullOrWhiteSpace(placement.VariantKey) &&
                definition.Variants.All(item => !string.Equals(item.Key, placement.VariantKey, StringComparison.Ordinal)) ||
                placement.GroupId is not null && !groups.ContainsKey(placement.GroupId.Value))
            {
                diagnostics.Add(new(TileCode, "Tile rotation, mirror, variant or group is incompatible with its definition.", path));
            }
        }

        if (profile.TileKit.Groups.Any(group => group.PlacementIds.Any(id => !placementIds.Contains(id))) ||
            profile.TileKit.Brushes.Any(brush => brush.DefinitionIds.Any(id => !definitions.ContainsKey(id))))
        {
            diagnostics.Add(new(ReferenceCode, "Tile groups and brushes must reference existing placements/definitions.",
                $"{root}/tileKit"));
        }

        var socketTypes = profile.TileKit.Definitions.SelectMany(item => item.Sockets)
            .Select(item => item.Type).ToHashSet(StringComparer.Ordinal);
        if (profile.TileKit.ConnectionRules.Any(rule => string.IsNullOrWhiteSpace(rule.FirstSocketType) ||
            string.IsNullOrWhiteSpace(rule.SecondSocketType) || !socketTypes.Contains(rule.FirstSocketType) ||
            !socketTypes.Contains(rule.SecondSocketType)))
        {
            diagnostics.Add(new(SocketCode, "Connection rules must reference declared socket types.",
                $"{root}/tileKit/connectionRules"));
        }

        foreach (var volume in profile.Volumes)
        {
            if (string.IsNullOrWhiteSpace(volume.Name) || string.IsNullOrWhiteSpace(volume.Kind) ||
                string.IsNullOrWhiteSpace(volume.Provenance) || !IsFinite(volume.Minimum) || !IsFinite(volume.Maximum) ||
                volume.Minimum.X >= volume.Maximum.X || volume.Minimum.Y >= volume.Maximum.Y ||
                volume.Minimum.Z >= volume.Maximum.Z)
            {
                diagnostics.Add(new(GeometryCode, "Area volume bounds and metadata must be valid.",
                    $"{root}/volumes/{volume.Id:D}"));
            }
        }


        if (profile.Dependencies.Any(item => item.TargetId == Guid.Empty || string.IsNullOrWhiteSpace(item.Kind) ||
            string.IsNullOrWhiteSpace(item.DocumentPath) || !IsSafeDocumentPath(item.DocumentPath)))
        {
            diagnostics.Add(new(ReferenceCode,
                "Area document dependencies require a target UUID, kind and confined relative document path.",
                $"{root}/dependencies"));
        }

        ValidateBinding(profile.Lifecycle.Entered, "area.entered", $"{root}/lifecycle/entered", diagnostics);
        ValidateBinding(profile.Lifecycle.Exited, "area.exited", $"{root}/lifecycle/exited", diagnostics);
        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateBinding(AreaLifecycleScriptBinding? binding, string expectedEvent, string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (binding is null) return;
        if (binding.Id == Guid.Empty || !string.Equals(binding.EventName, expectedEvent, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(binding.Provenance) || string.IsNullOrWhiteSpace(binding.ForgeScriptApiVersion) ||
            binding.Required && string.IsNullOrWhiteSpace(binding.ScriptPath) ||
            !string.IsNullOrWhiteSpace(binding.ScriptPath) && !IsSafeScriptPath(binding.ScriptPath))
        {
            diagnostics.Add(new(LifecycleCode,
                "Lifecycle binding identity, event, ForgeScript API and relative .forge path must be valid.", path));
        }
    }

    private static bool IsSafeScriptPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("scripts/", StringComparison.Ordinal) &&
               normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("../", StringComparison.Ordinal) && !Path.IsPathRooted(path);
    }

    private static bool IsSafeDocumentPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return !Path.IsPathRooted(path) && !normalized.StartsWith("/", StringComparison.Ordinal) &&
               !normalized.Contains("../", StringComparison.Ordinal) &&
               !normalized.Contains("//", StringComparison.Ordinal);
    }

    private static bool IsFinite(TransformValue transform) =>
        IsFinite(transform.Position) && IsFinite(transform.Scale) &&
        float.IsFinite(transform.Rotation.X) && float.IsFinite(transform.Rotation.Y) &&
        float.IsFinite(transform.Rotation.Z) && float.IsFinite(transform.Rotation.W);

    private static bool IsFinite(Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public sealed record ModularTileConnectionPreview(
    Guid PlacementId,
    int CompatibleSocketCount,
    int OpenSocketCount,
    IReadOnlyList<string> Diagnostics);

public static class ModularTileConnectionPreviewService
{
    public static ModularTileConnectionPreview Preview(ModularTileKitProfile kit, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var placement = kit.Placements.SingleOrDefault(item => item.Id == placementId);
        if (placement is null)
        {
            return new(placementId, 0, 0, ["area.foundation.placement_missing"]);
        }

        var definition = kit.Definitions.SingleOrDefault(item => item.Id == placement.DefinitionId);
        if (definition is null)
        {
            return new(placementId, 0, 0, ["area.foundation.definition_missing"]);
        }

        var rules = kit.ConnectionRules.SelectMany(rule => rule.Bidirectional
                ? new[] { (rule.FirstSocketType, rule.SecondSocketType), (rule.SecondSocketType, rule.FirstSocketType) }
                : new[] { (rule.FirstSocketType, rule.SecondSocketType) })
            .ToHashSet();
        var compatible = 0;
        foreach (var socket in definition.Sockets)
        {
            var hasCompatibleNeighbour = kit.Placements.Where(item => item.Id != placement.Id &&
                    Math.Abs(item.GridX - placement.GridX) + Math.Abs(item.GridZ - placement.GridZ) == 1)
                .Select(item => kit.Definitions.SingleOrDefault(candidate => candidate.Id == item.DefinitionId))
                .Where(item => item is not null)
                .SelectMany(item => item!.Sockets)
                .Any(other => string.Equals(socket.Type, other.Type, StringComparison.Ordinal) ||
                              rules.Contains((socket.Type, other.Type)));
            if (hasCompatibleNeighbour) compatible++;
        }

        return new(placement.Id, compatible, definition.Sockets.Count - compatible, []);
    }
}
