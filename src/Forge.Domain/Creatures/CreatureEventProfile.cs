using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public enum CreatureCombatBoundaryMode
{
    CombatTickCompleted,
    TurnEnded,
}

public enum CreatureCustomFieldType
{
    String,
    Integer,
    Number,
    Boolean,
    EntityId,
    ItemId,
}

public sealed record CreatureEventProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool RequiredAtRuntime { get; }
    public CreatureCombatBoundaryBinding? CombatBoundary { get; }
    public CreatureInventoryEventBindings? Inventory { get; }
    public CreatureRestEventBinding? Rest { get; }
    public CreatureHeartbeatBinding? Heartbeat { get; }
    public IReadOnlyList<CreatureCustomEventBinding> CustomEvents { get; }
    public IReadOnlyDictionary<string, string> Overrides { get; }
    public CreatureDamageDeathBindings? DamageDeath { get; }

    public CreatureEventProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool requiredAtRuntime,
        CreatureCombatBoundaryBinding? combatBoundary,
        CreatureInventoryEventBindings? inventory,
        CreatureRestEventBinding? rest,
        CreatureHeartbeatBinding? heartbeat,
        IReadOnlyList<CreatureCustomEventBinding>? customEvents,
        IReadOnlyDictionary<string, string>? overrides,
        CreatureDamageDeathBindings? damageDeath = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        RequiredAtRuntime = requiredAtRuntime;
        CombatBoundary = combatBoundary?.Normalize();
        Inventory = inventory?.Normalize();
        Rest = rest?.Normalize();
        Heartbeat = heartbeat?.Normalize();
        CustomEvents = (customEvents ?? []).Select(item => item.Normalize())
            .OrderBy(item => item.QualifiedName, StringComparer.Ordinal).ToArray();
        Overrides = (overrides ?? new Dictionary<string, string>())
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
        DamageDeath = damageDeath?.Normalize();
    }

    public static CreatureEventProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        requiredAtRuntime: false,
        new(CreatureCombatBoundaryMode.CombatTickCompleted,
            Binding(Guid.NewGuid(), "creature.combat_tick_completed", "OnCombatTickCompleted", provenance)),
        new(
            Binding(Guid.NewGuid(), "creature.inventory_accessed", "OnInventoryAccessed", provenance),
            Binding(Guid.NewGuid(), "creature.item_transferred", "OnItemTransferred", provenance)),
        new(Binding(Guid.NewGuid(), "creature.rest_completed", "OnRestCompleted", provenance)),
        new(true, 2_000, 8, true,
            Binding(Guid.NewGuid(), "creature.heartbeat", "OnHeartbeat", provenance)),
        [],
        new Dictionary<string, string>(),
        new(
            Binding(Guid.NewGuid(), "creature.before_damage", "BeforeDamage", provenance),
            Binding(Guid.NewGuid(), "creature.damage_applied", "OnDamaged", provenance),
            Binding(Guid.NewGuid(), "entity.died", "OnDeath", provenance),
            4,
            0.5));

    private static EventBinding Binding(Guid id, string eventName, string handler, string provenance) =>
        new(id, 1, provenance, "1.0", eventName, handler, EventAuthorityScope.Server);

    internal static EventBinding NormalizeBinding(EventBinding binding) => binding with
    {
        Provenance = binding.Provenance?.Trim() ?? string.Empty,
        ForgeScriptApiVersion = binding.ForgeScriptApiVersion?.Trim() ?? string.Empty,
        EventName = binding.EventName?.Trim().ToLowerInvariant() ?? string.Empty,
        Handler = binding.Handler?.Trim() ?? string.Empty,
    };

    public sealed record CreatureCombatBoundaryBinding(CreatureCombatBoundaryMode Mode, EventBinding Binding)
    {
        internal CreatureCombatBoundaryBinding Normalize() => this with { Binding = NormalizeBinding(Binding) };
    }

    public sealed record CreatureInventoryEventBindings(EventBinding Accessed, EventBinding Transferred)
    {
        internal CreatureInventoryEventBindings Normalize() => this with
        {
            Accessed = NormalizeBinding(Accessed),
            Transferred = NormalizeBinding(Transferred),
        };
    }

    public sealed record CreatureRestEventBinding(EventBinding Completed)
    {
        internal CreatureRestEventBinding Normalize() => this with { Completed = NormalizeBinding(Completed) };
    }

    public sealed record CreatureHeartbeatBinding(
        bool Enabled,
        int IntervalMilliseconds,
        int BudgetMilliseconds,
        bool SuspendOutsideInterest,
        EventBinding Binding)
    {
        internal CreatureHeartbeatBinding Normalize() => this with { Binding = NormalizeBinding(Binding) };
    }

    public sealed record CreatureDamageDeathBindings(
        EventBinding BeforeDamage,
        EventBinding DamageApplied,
        EventBinding Died,
        int BeforeDamageBudgetMilliseconds,
        double MaximumAdjustmentRatio)
    {
        internal CreatureDamageDeathBindings Normalize() => this with
        {
            BeforeDamage = NormalizeBinding(BeforeDamage),
            DamageApplied = NormalizeBinding(DamageApplied),
            Died = NormalizeBinding(Died),
        };
    }

    public sealed record CreatureCustomEventBinding(
        Guid SchemaId,
        string Namespace,
        string Name,
        int PayloadVersion,
        IReadOnlyDictionary<string, CreatureCustomFieldType> Fields,
        bool Required,
        EventBinding Binding)
    {
        public string QualifiedName => $"{Namespace}.{Name}";

        internal CreatureCustomEventBinding Normalize()
        {
            var eventNamespace = CreaturePresentationProfile.NormalizeIdentifier(Namespace);
            var eventName = CreaturePresentationProfile.NormalizeIdentifier(Name);
            return this with
            {
                Namespace = eventNamespace,
                Name = eventName,
                Fields = (Fields ?? new Dictionary<string, CreatureCustomFieldType>())
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        item => CreaturePresentationProfile.NormalizeIdentifier(item.Key),
                        item => item.Value,
                        StringComparer.Ordinal),
                Binding = NormalizeBinding(Binding) with { EventName = $"custom.{eventNamespace}.{eventName}" },
            };
        }
    }
}
