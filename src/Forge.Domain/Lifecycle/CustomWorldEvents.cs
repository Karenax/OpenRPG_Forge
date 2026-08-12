namespace Forge.Domain.Lifecycle;

public enum WorldAstronomicalEventKind { Dawn, Sunrise, Sunset, Dusk, Moonrise, Midnight }

public sealed record CustomEventSchema(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Namespace,
    string Name,
    int PayloadVersion,
    IReadOnlyDictionary<string, string> Fields,
    bool RequiredExtension,
    string ForgeScriptApiVersion = "1.0",
    string Handler = "OnCustomWorldEvent",
    EventAuthorityScope Authority = EventAuthorityScope.Server)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record CustomWorldEvent(
    Guid Id,
    Guid SchemaId,
    string QualifiedName,
    int PayloadVersion,
    IReadOnlyDictionary<string, string> Payload,
    DateTimeOffset OccurredAt);

public sealed record WorldClockProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    int DaysPerMonth,
    int MonthsPerYear,
    int HoursPerDay,
    int MinutesPerHour,
    double Speed,
    bool Paused,
    long InitialMinute,
    IReadOnlyDictionary<string, int> AreaTimeZoneOffsets,
    IReadOnlyDictionary<WorldAstronomicalEventKind, int> AstronomicalMinutes)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record WorldClockSnapshot(long TotalMinutes, bool Paused, long Revision, string Provenance);

public sealed record WorldClockAdvanceResult(
    WorldClockSnapshot Snapshot,
    IReadOnlyList<WorldAstronomicalEventKind> CrossedEvents);
