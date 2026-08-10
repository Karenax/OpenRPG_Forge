using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Navigation;

public sealed record AnchorCatalogHandle(AreaId AreaId, Guid CatalogId, int SchemaVersion);

public sealed record AnchorScriptSnapshot(
    Guid InstanceId,
    Guid DefinitionId,
    string Slug,
    string Label,
    AnchorKind Kind,
    AnchorScope Scope,
    TransformValue Transform,
    IReadOnlyList<string> Tags,
    bool RequiredAtRuntime,
    bool IsSpawn,
    bool IsPortal);

/// <summary>Typed, bounded boundary exposed to ForgeScript adapters.</summary>
public sealed class AnchorScriptService
{
    private readonly ProjectDefinition _project;
    private readonly AreaDefinition _area;

    public AnchorScriptService(ProjectDefinition project, AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);
        _area = project.Areas.SingleOrDefault(item => item.Id == areaId)
            ?? throw new ArgumentException("ANCHOR_AREA_MISSING", nameof(areaId));
        var diagnostics = AnchorCatalogValidator.Validate(project, _area);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(project));
        _project = project;
    }

    public AnchorCatalogHandle Handle => new(_area.Id, _area.Navigation.Anchors.Id,
        _area.Navigation.Anchors.SchemaVersion);

    public IReadOnlyList<AnchorScriptSnapshot> Query(
        AnchorCatalogHandle handle,
        string? kind = null,
        string? tag = null,
        int limit = 100)
    {
        Ensure(handle);
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        AnchorKind? parsedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<AnchorKind>(kind, true, out var value))
                throw new ArgumentException("ANCHOR_KIND_INVALID", nameof(kind));
            parsedKind = value;
        }
        var definitions = _area.Navigation.Anchors.Definitions.ToDictionary(item => item.Id);
        return _area.Navigation.Anchors.Instances
            .Where(item => definitions.ContainsKey(item.DefinitionId))
            .Select(item => (Instance: item, Definition: definitions[item.DefinitionId]))
            .Where(item => parsedKind is null || item.Definition.Kind == parsedKind)
            .Where(item => string.IsNullOrWhiteSpace(tag) || item.Definition.Tags.Contains(tag.Trim()))
            .OrderBy(item => item.Instance.DisplayOrder).ThenBy(item => item.Instance.Id).Take(limit)
            .Select(item => new AnchorScriptSnapshot(item.Instance.Id, item.Definition.Id,
                item.Definition.Slug, item.Definition.Label.Fallback, item.Definition.Kind,
                item.Definition.Scope, item.Instance.Transform, item.Definition.Tags,
                item.Definition.RequiredAtRuntime, item.Instance.Spawn is not null,
                item.Instance.Portal is not null)).ToArray();
    }

    public SpawnAnchorResolution ResolveSpawn(AnchorCatalogHandle handle, string profile, string team,
        IReadOnlySet<Guid>? blockedInstanceIds = null)
    {
        Ensure(handle);
        return AnchorRuntimeService.ResolveSpawn(_area, profile, team, blockedInstanceIds);
    }

    public PortalTravelResolution ResolvePortal(AnchorCatalogHandle handle, Guid sourceAnchorInstanceId,
        Vector3Value incomingVelocity, float incomingYawDegrees)
    {
        Ensure(handle);
        return AnchorRuntimeService.ResolvePortal(_project, _area.Id, sourceAnchorInstanceId,
            incomingVelocity, incomingYawDegrees);
    }

    private void Ensure(AnchorCatalogHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var catalog = _area.Navigation.Anchors;
        if (handle.AreaId != _area.Id || handle.CatalogId != catalog.Id ||
            handle.SchemaVersion != catalog.SchemaVersion)
            throw new InvalidOperationException("ANCHOR_CATALOG_HANDLE_STALE");
    }
}
