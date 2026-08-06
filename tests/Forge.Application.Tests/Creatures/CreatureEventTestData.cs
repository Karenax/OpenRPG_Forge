using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;

namespace Forge.Application.Tests.Creatures;

internal static class CreatureEventTestData
{
    internal static readonly Guid CustomSchemaId = Guid.Parse("a1530000-0000-4000-8000-000000000010");

    internal static CreatureEventProfile Complete() => new(
        Guid.Parse("a1530000-0000-4000-8000-000000000001"), 1, "author:test", true,
        new(CreatureCombatBoundaryMode.TurnEnded,
            Binding("a1530000-0000-4000-8000-000000000002", "creature.turn_ended", "OnTurnEnded")),
        new(
            Binding("a1530000-0000-4000-8000-000000000003", "creature.inventory_accessed", "OnInventoryAccessed"),
            Binding("a1530000-0000-4000-8000-000000000004", "creature.item_transferred", "OnItemTransferred")),
        new(Binding("a1530000-0000-4000-8000-000000000005", "creature.rest_completed", "OnRestCompleted")),
        new(true, 2_000, 8, true,
            Binding("a1530000-0000-4000-8000-000000000006", "creature.heartbeat", "OnHeartbeat")),
        [new(CustomSchemaId, "crypt", "braziers_lit", 2,
            new Dictionary<string, CreatureCustomFieldType>
            {
                ["count"] = CreatureCustomFieldType.Integer,
                ["source"] = CreatureCustomFieldType.EntityId,
            }, true, Binding("a1530000-0000-4000-8000-000000000011",
                "custom.crypt.braziers_lit", "OnBraziersLit"))],
        new Dictionary<string, string> { ["telemetry.channel"] = "creature-events" });

    private static EventBinding Binding(string id, string eventName, string handler) => new(
        Guid.Parse(id), 1, "author:test", "1.0", eventName, handler, EventAuthorityScope.Server);
}
