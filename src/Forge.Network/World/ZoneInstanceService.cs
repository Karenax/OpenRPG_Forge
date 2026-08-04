namespace Forge.Network.World;

public sealed class ZoneInstanceService
{
    private readonly Dictionary<Guid, ZoneTemplate> _templates = [];
    private readonly Dictionary<Guid, ZoneInstance> _instances = [];

    public IReadOnlyList<WorldDiagnostic> RegisterTemplate(ZoneTemplate template)
    {
        var diagnostics = Validate(template);
        if (diagnostics.Count == 0) _templates[template.TemplateId] = template;
        return diagnostics;
    }

    public (ZoneInstance? Instance, WorldDecision Decision) Create(Guid templateId, ZoneLifecycle lifecycle, Guid actorPartyId)
    {
        if (!_templates.TryGetValue(templateId, out var template)) return (null, Reject("NET_ZONE_TEMPLATE_UNKNOWN", templateId, 0));
        if (!template.AllowsDynamicCreation) return (null, Reject("NET_ZONE_DYNAMIC_DISABLED", templateId, 0));
        var count = _instances.Values.Count(x => x.TemplateId == templateId);
        if (count >= template.MaxInstances) return (null, Reject("NET_ZONE_INSTANCE_LIMIT", templateId, count));
        if (lifecycle == ZoneLifecycle.Persistent) return (null, Reject("NET_ZONE_DYNAMIC_PERSISTENT_FORBIDDEN", templateId, count));
        var instance = new ZoneInstance(Guid.NewGuid(), templateId, lifecycle, actorPartyId == Guid.Empty ? null : actorPartyId, new HashSet<Guid>(), 1);
        _instances.Add(instance.InstanceId, instance);
        return (instance, Accept("NET_ZONE_INSTANCE_CREATED", instance.InstanceId, 1));
    }

    public WorldDecision Enter(Guid instanceId, Guid characterId, Guid? partyId, long expectedRevision)
    {
        if (!_instances.TryGetValue(instanceId, out var instance)) return Reject("NET_ZONE_INSTANCE_UNKNOWN", instanceId, 0);
        var template = _templates[instance.TemplateId];
        if (instance.Revision != expectedRevision) return Reject("NET_ZONE_REVISION_STALE", instanceId, instance.Revision);
        if (instance.OwnerPartyId is not null && instance.OwnerPartyId != partyId) return Reject("NET_ZONE_PARTY_MISMATCH", instanceId, instance.Revision);
        if (instance.PlayerCharacterIds.Count >= template.MaxPlayersPerInstance) return Reject("NET_ZONE_PLAYER_LIMIT", instanceId, instance.Revision);
        if (instance.PlayerCharacterIds.Contains(characterId)) return Reject("NET_ZONE_CHARACTER_PRESENT", instanceId, instance.Revision);
        _instances[instanceId] = instance with { PlayerCharacterIds = instance.PlayerCharacterIds.Append(characterId).ToHashSet(), Revision = instance.Revision + 1 };
        return Accept("NET_ZONE_ENTERED", instanceId, instance.Revision + 1);
    }

    public ZoneInstance? Find(Guid id) => _instances.GetValueOrDefault(id);

    private static IReadOnlyList<WorldDiagnostic> Validate(ZoneTemplate template)
    {
        var diagnostics = new List<WorldDiagnostic>();
        if (template.TemplateId == Guid.Empty) diagnostics.Add(new("NET_ZONE_TEMPLATE_ID_REQUIRED", "/network/zones/templateId", "Stable template ID required."));
        if (string.IsNullOrWhiteSpace(template.AreaId)) diagnostics.Add(new("NET_ZONE_AREA_REQUIRED", "/network/zones/areaId", "Area ID required."));
        if (template.MaxInstances is < 1 or > 128) diagnostics.Add(new("NET_ZONE_INSTANCE_CAP_INVALID", "/network/zones/maxInstances", "Instance cap must be 1 to 128."));
        if (template.MaxPlayersPerInstance is < 1 or > 64) diagnostics.Add(new("NET_ZONE_PLAYER_CAP_INVALID", "/network/zones/maxPlayersPerInstance", "Player cap must be 1 to 64."));
        return diagnostics;
    }

    private static WorldDecision Accept(string code, Guid id, long revision) => new(true, code, revision, $"accepted:{code}:zone={id}:revision={revision}");
    private static WorldDecision Reject(string code, Guid id, long revision) => new(false, code, revision, $"rejected:{code}:zone={id}:revision={revision}");
}
