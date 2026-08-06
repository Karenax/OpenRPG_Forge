using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureEventProfileTests
{
    [Fact]
    public void CompleteProfileValidatesWithStableTypedBindings()
    {
        var profile = Complete();

        var diagnostics = CreatureEventValidator.Validate(profile, "blueprints/warden/creatureEvents");

        Assert.Empty(diagnostics);
        Assert.Equal(6, CreatureEventValidator.EnumerateBindings(profile).Count);
        Assert.Equal("crypt.braziers_lit", Assert.Single(profile.CustomEvents).QualifiedName);
        Assert.All(CreatureEventValidator.EnumerateBindings(profile), binding =>
            Assert.Equal(EventAuthorityScope.Server, binding.Authority));
    }

    [Fact]
    public void HeartbeatIsBudgetedSuspendedAndBacklogBounded()
    {
        var profile = Complete();
        var suspended = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, CreatureEventRuntimeState.Empty,
            10_000, false, "ash-warden");
        var active = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, suspended.State,
            10_000, true, "ash-warden");

        Assert.Empty(suspended.Dispatches);
        Assert.Equal(CreatureEventRuntimeResolver.MaximumCatchUpDispatches, active.Dispatches.Count);
        Assert.All(active.Dispatches, dispatch => Assert.Equal(8, dispatch.BudgetMilliseconds));
        Assert.Contains(active.Diagnostics, item => item.Code == "creature.events.heartbeat_backlog_truncated");
        Assert.Equal(4, active.State.HeartbeatSequence);
    }

    [Fact]
    public void RuntimeBuildsExplicitCombatInventoryRestAndCustomPayloads()
    {
        var profile = Complete();
        var combat = CreatureEventRuntimeResolver.CombatBoundary(profile, CreatureCombatBoundaryMode.TurnEnded,
            "ash-warden", 3, "turn-based");
        var accessed = CreatureEventRuntimeResolver.InventoryAccessed(profile, "ash-warden", "hero", "pickpocket", "noticed");
        var transferred = CreatureEventRuntimeResolver.ItemTransferred(profile, "ash-warden", "hero",
            "ember-key", 1, "corpse-loot", "accepted");
        var rest = CreatureEventRuntimeResolver.RestCompleted(profile, "ash-warden", 28_800,
            new Dictionary<string, double> { ["health"] = 12, ["focus"] = 4 }, ["ambush"]);
        var custom = CreatureEventRuntimeResolver.Custom(profile, CustomSchemaId,
            new Dictionary<string, string> { ["count"] = "3", ["source"] = "ash-warden" });

        Assert.Equal("creature.turn_ended", combat!.EventName);
        Assert.Equal("turn-based", combat.Payload["combatMode"]);
        Assert.Equal("pickpocket", accessed!.Payload["cause"]);
        Assert.Equal("ember-key", transferred!.Payload["itemId"]);
        Assert.Contains("health:12", rest!.Payload["restoredResources"], StringComparison.Ordinal);
        Assert.Equal("custom.crypt.braziers_lit", custom.EventName);
        Assert.Throws<ArgumentException>(() => CreatureEventRuntimeResolver.Custom(profile, CustomSchemaId,
            new Dictionary<string, string> { ["count"] = "invalid", ["source"] = "ash-warden" }));
    }

    [Fact]
    public void InvalidAndMissingRequiredProfilesReturnLocalizedDiagnostics()
    {
        var invalidBinding = new EventBinding(Guid.Empty, 2, "", "9.0", "wrong", "bad handler!",
            EventAuthorityScope.Local);
        var invalid = new CreatureEventProfile(Guid.Empty, 9, "", true,
            new(CreatureCombatBoundaryMode.TurnEnded, invalidBinding), null, null,
            new(true, 100, 0, true, invalidBinding),
            [new(Guid.Empty, "bad namespace", "", 0,
                new Dictionary<string, CreatureCustomFieldType> { ["bad field"] = (CreatureCustomFieldType)999 },
                true, invalidBinding)],
            new Dictionary<string, string> { ["bad key"] = "" });

        var diagnostics = CreatureEventValidator.Validate(invalid, "blueprints/test/creatureEvents");
        var missing = CreatureEventRuntimeResolver.AdvanceHeartbeat(null, CreatureEventRuntimeState.Empty,
            1_000, true, "test", required: true);
        var optional = CreatureEventRuntimeResolver.AdvanceHeartbeat(null, CreatureEventRuntimeState.Empty,
            1_000, true, "test");

        Assert.Contains(diagnostics, item => item.Code == "creature.events.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.events.binding_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.events.heartbeat_interval_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.events.custom_schema_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.events.custom_fields_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.events.override_invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/test/creatureEvents", item.Path));
        Assert.Equal(CreatureEventValidator.MissingRequiredCode, Assert.Single(missing.Diagnostics).Code);
        Assert.True(optional.IgnoredOptionalProfile);
    }

    private static readonly Guid CustomSchemaId = Guid.Parse("a1500000-0000-4000-8000-000000000010");

    internal static CreatureEventProfile Complete() => new(
        Guid.Parse("a1500000-0000-4000-8000-000000000001"), 1, "author:test", true,
        new(CreatureCombatBoundaryMode.TurnEnded,
            Binding("a1500000-0000-4000-8000-000000000002", "creature.turn_ended", "OnTurnEnded")),
        new(
            Binding("a1500000-0000-4000-8000-000000000003", "creature.inventory_accessed", "OnInventoryAccessed"),
            Binding("a1500000-0000-4000-8000-000000000004", "creature.item_transferred", "OnItemTransferred")),
        new(Binding("a1500000-0000-4000-8000-000000000005", "creature.rest_completed", "OnRestCompleted")),
        new(true, 2_000, 8, true,
            Binding("a1500000-0000-4000-8000-000000000006", "creature.heartbeat", "OnHeartbeat")),
        [new(CustomSchemaId, "crypt", "braziers_lit", 2,
            new Dictionary<string, CreatureCustomFieldType>
            {
                ["count"] = CreatureCustomFieldType.Integer,
                ["source"] = CreatureCustomFieldType.EntityId,
            }, true, Binding("a1500000-0000-4000-8000-000000000011",
                "custom.crypt.braziers_lit", "OnBraziersLit"))],
        new Dictionary<string, string> { ["telemetry.channel"] = "creature-events" });

    private static EventBinding Binding(string id, string eventName, string handler) => new(
        Guid.Parse(id), 1, "author:test", "1.0", eventName, handler, EventAuthorityScope.Server);
}
