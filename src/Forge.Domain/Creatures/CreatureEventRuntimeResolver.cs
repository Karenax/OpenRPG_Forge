using Forge.Domain.Lifecycle;

namespace Forge.Domain.Creatures;

public sealed record CreatureEventRuntimeState(long HeartbeatAccumulatorMilliseconds, long HeartbeatSequence)
{
    public static CreatureEventRuntimeState Empty { get; } = new(0, 0);
}

public sealed record CreatureEventDispatch(
    Guid BindingId,
    string EventName,
    string Handler,
    EventAuthorityScope Authority,
    IReadOnlyDictionary<string, string> Payload,
    int BudgetMilliseconds = 0);

public sealed record CreatureEventAdvanceResult(
    CreatureEventRuntimeState State,
    IReadOnlyList<CreatureEventDispatch> Dispatches,
    IReadOnlyList<CreatureEventDiagnostic> Diagnostics,
    bool IgnoredOptionalProfile)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureEventRuntimeResolver
{
    public const int MaximumCatchUpDispatches = 4;

    public static CreatureEventAdvanceResult AdvanceHeartbeat(
        CreatureEventProfile? profile,
        CreatureEventRuntimeState state,
        long deltaMilliseconds,
        bool insideInterest,
        string creatureId,
        bool required = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (deltaMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        var diagnostics = ValidateRuntime(profile, required);
        if (profile is null)
            return new(state, [], diagnostics, !required);
        if (diagnostics.Count > 0 || profile.Heartbeat is not { Enabled: true } heartbeat ||
            !insideInterest && heartbeat.SuspendOutsideInterest)
            return new(state, [], diagnostics, false);

        var accumulated = checked(state.HeartbeatAccumulatorMilliseconds + deltaMilliseconds);
        var due = accumulated / heartbeat.IntervalMilliseconds;
        var emitted = Math.Min(due, MaximumCatchUpDispatches);
        var dispatches = new List<CreatureEventDispatch>();
        for (var index = 0; index < emitted; index++)
        {
            var sequence = state.HeartbeatSequence + index + 1;
            dispatches.Add(Dispatch(heartbeat.Binding, new Dictionary<string, string>
            {
                ["creatureId"] = creatureId,
                ["sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["intervalMilliseconds"] = heartbeat.IntervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }, heartbeat.BudgetMilliseconds));
        }
        if (due > MaximumCatchUpDispatches)
            diagnostics.Add(new("creature.events.heartbeat_backlog_truncated", "creatureEvents/heartbeat",
                $"Dropped {due - MaximumCatchUpDispatches} overdue creature heartbeat dispatch(es)."));
        accumulated = due > MaximumCatchUpDispatches
            ? accumulated % heartbeat.IntervalMilliseconds
            : accumulated - emitted * heartbeat.IntervalMilliseconds;
        return new(new(accumulated, state.HeartbeatSequence + emitted), dispatches, diagnostics, false);
    }

    public static CreatureEventDispatch? CombatBoundary(
        CreatureEventProfile? profile,
        CreatureCombatBoundaryMode mode,
        string creatureId,
        long sequence,
        string combatMode)
    {
        if (profile?.CombatBoundary is not { } configured || configured.Mode != mode) return null;
        EnsureValid(profile);
        return Dispatch(configured.Binding, new Dictionary<string, string>
        {
            ["creatureId"] = creatureId,
            ["sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["combatMode"] = combatMode,
            ["boundary"] = mode.ToString(),
        });
    }

    public static CreatureEventDispatch? InventoryAccessed(
        CreatureEventProfile? profile,
        string creatureId,
        string actorId,
        string cause,
        string result)
    {
        if (profile?.Inventory is not { } inventory) return null;
        EnsureValid(profile);
        return Dispatch(inventory.Accessed, CommonInventoryPayload(creatureId, actorId, cause, result));
    }

    public static CreatureEventDispatch? ItemTransferred(
        CreatureEventProfile? profile,
        string creatureId,
        string actorId,
        string itemId,
        int quantity,
        string cause,
        string result)
    {
        if (profile?.Inventory is not { } inventory) return null;
        EnsureValid(profile);
        var payload = CommonInventoryPayload(creatureId, actorId, cause, result);
        payload["itemId"] = itemId;
        payload["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Dispatch(inventory.Transferred, payload);
    }

    public static CreatureEventDispatch? RestCompleted(
        CreatureEventProfile? profile,
        string creatureId,
        int durationSeconds,
        IReadOnlyDictionary<string, double> restoredResources,
        IReadOnlyList<string> interruptions)
    {
        if (profile?.Rest is not { } rest) return null;
        EnsureValid(profile);
        return Dispatch(rest.Completed, new Dictionary<string, string>
        {
            ["creatureId"] = creatureId,
            ["durationSeconds"] = durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["restoredResources"] = string.Join(',', restoredResources.OrderBy(item => item.Key)
                .Select(item => $"{item.Key}:{item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}")),
            ["interruptions"] = string.Join(',', interruptions.Order(StringComparer.Ordinal)),
        });
    }

    public static CreatureEventDispatch Custom(
        CreatureEventProfile profile,
        Guid schemaId,
        IReadOnlyDictionary<string, string> payload)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(payload);
        EnsureValid(profile);
        var custom = profile.CustomEvents.SingleOrDefault(item => item.SchemaId == schemaId)
            ?? throw new KeyNotFoundException("CREATURE_CUSTOM_EVENT_SCHEMA_MISSING");
        if (payload.Count != custom.Fields.Count || custom.Fields.Keys.Any(item => !payload.ContainsKey(item)) ||
            !payload.All(item => ValidateCustomValue(custom.Fields[item.Key], item.Value)))
            throw new ArgumentException("CREATURE_CUSTOM_EVENT_PAYLOAD_INVALID", nameof(payload));
        return Dispatch(custom.Binding, payload.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
    }

    private static List<CreatureEventDiagnostic> ValidateRuntime(CreatureEventProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? [new(CreatureEventValidator.MissingRequiredCode, "creatureEvents", "Required creature events are absent.")]
                : [];
        return CreatureEventValidator.Validate(profile).ToList();
    }

    private static void EnsureValid(CreatureEventProfile profile)
    {
        var diagnostics = CreatureEventValidator.Validate(profile);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(profile));
    }

    private static Dictionary<string, string> CommonInventoryPayload(
        string creatureId, string actorId, string cause, string result) => new(StringComparer.Ordinal)
        {
            ["creatureId"] = creatureId,
            ["actorId"] = actorId,
            ["cause"] = cause,
            ["result"] = result,
        };

    private static CreatureEventDispatch Dispatch(
        EventBinding binding,
        IReadOnlyDictionary<string, string> payload,
        int budgetMilliseconds = 0) =>
        new(binding.Id, binding.EventName, binding.Handler, binding.Authority, payload, budgetMilliseconds);

    private static bool ValidateCustomValue(CreatureCustomFieldType type, string value) => type switch
    {
        CreatureCustomFieldType.String => true,
        CreatureCustomFieldType.Integer => long.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out _),
        CreatureCustomFieldType.Number => double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) && double.IsFinite(number),
        CreatureCustomFieldType.Boolean => bool.TryParse(value, out _),
        CreatureCustomFieldType.EntityId or CreatureCustomFieldType.ItemId =>
            CreaturePresentationProfile.IsCanonicalIdentifier(value),
        _ => false,
    };
}
