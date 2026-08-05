using Forge.Domain.Areas;
using Forge.Domain.Sequences;
using Forge.Domain.Rules;
using System.Text.Json.Serialization;

namespace Forge.Domain.RuntimePolicies;

public sealed record AreaRuntimePolicyProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<HeartbeatScheduleDefinition> Heartbeats { get; }
    public AreaRestPolicy Rest { get; }
    public AreaCinematicProfile Cinematics { get; }
    public AreaRulesProfile Rules { get; }
    public string Provenance { get; }

    public AreaRuntimePolicyProfile(Guid id, int schemaVersion,
        IReadOnlyList<HeartbeatScheduleDefinition> heartbeats, AreaRestPolicy rest, string provenance)
        : this(id, schemaVersion, heartbeats, rest, provenance, null, null)
    {
    }

    [JsonConstructor]
    public AreaRuntimePolicyProfile(Guid id, int schemaVersion,
        IReadOnlyList<HeartbeatScheduleDefinition> heartbeats, AreaRestPolicy rest, string provenance,
        AreaCinematicProfile? cinematics, AreaRulesProfile? rules = null)
    {
        ArgumentNullException.ThrowIfNull(heartbeats);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = heartbeats.OrderBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            values.Any(item => item.Scope != HeartbeatScope.Area))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area runtime policy profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Heartbeats = values;
        Rest = rest;
        Cinematics = cinematics ?? AreaCinematicProfile.CreateDefault(AreaId.From(id));
        Rules = rules ?? AreaRulesProfile.CreateDefault(AreaId.From(id));
        Provenance = provenance.Trim();
    }

    public static AreaRuntimePolicyProfile CreateDefault(AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        var rest = new AreaRestPolicy(RuntimePolicyIdFactory.Create(areaId.Value, "rest"),
            AreaRestPolicy.CurrentSchemaVersion, RestPermissionMode.Allowed, string.Empty, string.Empty, 0, 0, [],
            "migration-default");
        return new AreaRuntimePolicyProfile(areaId.Value, CurrentSchemaVersion, [], rest, "migration-default",
            AreaCinematicProfile.CreateDefault(areaId), AreaRulesProfile.CreateDefault(areaId));
    }

    public static AreaRuntimePolicyProfile CloneForArea(AreaRuntimePolicyProfile source, AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(areaId);
        var heartbeats = source.Heartbeats.Select((item, index) => new HeartbeatScheduleDefinition(
            RuntimePolicyIdFactory.Create(areaId.Value, $"heartbeat:{index}"), item.SchemaVersion, item.Name,
            HeartbeatScope.Area, item.Enabled, item.IntervalMilliseconds, item.BudgetMilliseconds,
            item.SuspendWhenInactive, item.ForgeScriptApiVersion, item.Handler, "area-clone")).ToArray();
        var volumes = source.Rest.Volumes.Select((item, index) => new RestPolicyVolume(
            RuntimePolicyIdFactory.Create(areaId.Value, $"rest-volume:{index}"), item.Name, item.Center, item.Size,
            item.Mode, item.DenialReason, item.Condition, item.CooldownSeconds, item.RequiredSecureRadius,
            item.Priority, "area-clone")).ToArray();
        var rest = new AreaRestPolicy(RuntimePolicyIdFactory.Create(areaId.Value, "rest"), source.Rest.SchemaVersion,
            source.Rest.Mode, source.Rest.DenialReason, source.Rest.Condition, source.Rest.CooldownSeconds,
            source.Rest.RequiredSecureRadius, volumes, "area-clone");
        var anchors = source.Cinematics.CameraAnchors.Select((item, index) => new CameraAnchorDefinition(
            RuntimePolicyIdFactory.Create(areaId.Value, $"camera-anchor:{index}"), item.Name, item.Transform,
            item.LookAt, item.Framing, item.FieldOfViewDegrees, item.DefaultDurationMilliseconds,
            item.SafeAreaMargin, item.Tags, "area-clone")).ToArray();
        var cinematics = new AreaCinematicProfile(areaId.Value, source.Cinematics.SchemaVersion, anchors,
            "area-clone");
        var perception = new EnvironmentPerceptionProfile(areaId.Value, source.Rules.Perception.SchemaVersion,
            source.Rules.Perception.RulesetId, source.Rules.Perception.Modifiers.Select((item, index) =>
                new EnvironmentPerceptionModifier(RuntimePolicyIdFactory.Create(areaId.Value, $"rules:sense:{index}"),
                    item.SenseId, item.Multiplier, item.AdditiveModifier, item.ConditionTag)).ToArray(), "area-clone");
        var sourceCombat = source.Rules.Combat;
        var combat = new AreaCombatPolicy(RuntimePolicyIdFactory.Create(areaId.Value, "rules:combat"),
            sourceCombat.SchemaVersion, sourceCombat.PvpMode, sourceCombat.FriendlyFire, sourceCombat.CrimeEnabled,
            sourceCombat.AggroEnabled, sourceCombat.EnvironmentalDamageEnabled, sourceCombat.ServerAuthoritative,
            "area-clone");
        var rules = new AreaRulesProfile(areaId.Value, source.Rules.SchemaVersion, perception, combat, "area-clone");
        return new AreaRuntimePolicyProfile(areaId.Value, source.SchemaVersion, heartbeats, rest, "area-clone",
            cinematics, rules);
    }
}
