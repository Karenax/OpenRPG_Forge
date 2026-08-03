using Forge.Domain.Lifecycle;

namespace Forge.Application.Lifecycle;

public sealed class CustomWorldEventBus
{
    private readonly Dictionary<Guid, CustomEventSchema> _schemas = [];
    private readonly Dictionary<Guid, List<Action<CustomWorldEvent>>> _subscribers = [];

    public void Register(CustomEventSchema schema)
    {
        if (schema.Id == Guid.Empty || schema.SchemaVersion != 1 || schema.PayloadVersion < 1 ||
            string.IsNullOrWhiteSpace(schema.Provenance) || !ValidSegment(schema.Namespace) || !ValidSegment(schema.Name))
            throw new InvalidOperationException("PROJECT_CUSTOM_EVENT_SCHEMA_INVALID");
        _schemas[schema.Id] = schema;
    }

    public void Subscribe(Guid schemaId, Action<CustomWorldEvent> observer)
    {
        if (!_schemas.ContainsKey(schemaId)) throw new InvalidOperationException("PROJECT_CUSTOM_EVENT_SCHEMA_MISSING");
        if (!_subscribers.TryGetValue(schemaId, out var observers)) _subscribers[schemaId] = observers = [];
        observers.Add(observer);
    }

    public CustomWorldEvent Publish(Guid schemaId, int payloadVersion, IReadOnlyDictionary<string, string> payload, DateTimeOffset occurredAt)
    {
        if (!_schemas.TryGetValue(schemaId, out var schema)) throw new InvalidOperationException("PROJECT_CUSTOM_EVENT_SCHEMA_MISSING");
        if (payloadVersion != schema.PayloadVersion || schema.Fields.Keys.Any(field => !payload.ContainsKey(field)) || payload.Keys.Any(field => !schema.Fields.ContainsKey(field)))
            throw new InvalidOperationException("PROJECT_CUSTOM_EVENT_PAYLOAD_INVALID");
        var @event = new CustomWorldEvent(Guid.NewGuid(), schemaId, $"{schema.Namespace}.{schema.Name}", payloadVersion, payload, occurredAt);
        if (_subscribers.TryGetValue(schemaId, out var observers)) foreach (var observer in observers) observer(@event);
        return @event;
    }

    public static WorldClockProfile ValidateClock(WorldClockProfile profile)
    {
        var dayMinutes = profile.HoursPerDay * profile.MinutesPerHour;
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance) ||
            profile.DaysPerMonth < 1 || profile.MonthsPerYear < 1 || profile.HoursPerDay < 1 || profile.MinutesPerHour < 1 || profile.Speed < 0 ||
            profile.AstronomicalMinutes.Values.Any(value => value < 0 || value >= dayMinutes))
            throw new InvalidOperationException("PROJECT_WORLD_CLOCK_INVALID");
        return profile;
    }

    public static long LocalMinute(WorldClockProfile profile, string areaId, long worldMinute) =>
        worldMinute + (profile.AreaTimeZoneOffsets.TryGetValue(areaId, out var offset) ? offset : 0);

    public static IReadOnlyList<WorldAstronomicalEventKind> CrossedAstronomicalEvents(WorldClockProfile profile, long before, long after, string areaId)
    {
        ValidateClock(profile);
        var dayMinutes = profile.HoursPerDay * profile.MinutesPerHour;
        var localBefore = LocalMinute(profile, areaId, before);
        var localAfter = LocalMinute(profile, areaId, after);
        return profile.AstronomicalMinutes.Where(pair => Crossed(localBefore, localAfter, pair.Value, dayMinutes)).Select(pair => pair.Key).ToArray();
    }

    private static bool Crossed(long before, long after, int minute, int dayMinutes) =>
        after > before && Enumerable.Range((int)Math.Floor((double)before / dayMinutes), (int)(Math.Floor((double)after / dayMinutes) - Math.Floor((double)before / dayMinutes) + 1))
            .Any(day => day * dayMinutes + minute > before && day * dayMinutes + minute <= after);

    private static bool ValidSegment(string value) => !string.IsNullOrWhiteSpace(value) && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
}
