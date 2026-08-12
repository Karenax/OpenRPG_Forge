using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Domain.Lifecycle;

public sealed record ProjectWorldProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Enabled { get; }
    public bool RequiredAtRuntime { get; }
    public IReadOnlyList<CustomEventSchema> CustomEvents { get; }
    public WorldClockProfile Clock { get; }

    public ProjectWorldProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        bool enabled,
        bool requiredAtRuntime,
        IReadOnlyList<CustomEventSchema> customEvents,
        WorldClockProfile clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(customEvents);
        ArgumentNullException.ThrowIfNull(clock);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Enabled = enabled;
        RequiredAtRuntime = requiredAtRuntime;
        CustomEvents = customEvents.OrderBy(item => item.Namespace, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Clock = clock;
    }

    public static ProjectWorldProfile CreateDefault(Guid moduleId)
    {
        var clockId = RuntimePolicyIdFactory.Create(moduleId, "world-clock");
        return new ProjectWorldProfile(
            moduleId,
            CurrentSchemaVersion,
            "migration-default:r052",
            true,
            false,
            [],
            new WorldClockProfile(
                clockId,
                WorldClockProfile.CurrentSchemaVersion,
                "migration-default:r052",
                28,
                12,
                24,
                60,
                1,
                false,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal),
                new Dictionary<WorldAstronomicalEventKind, int>
                {
                    [WorldAstronomicalEventKind.Dawn] = 360,
                    [WorldAstronomicalEventKind.Sunrise] = 420,
                    [WorldAstronomicalEventKind.Sunset] = 1_080,
                    [WorldAstronomicalEventKind.Dusk] = 1_140,
                    [WorldAstronomicalEventKind.Midnight] = 0,
                }));
    }
}

public static class ProjectWorldProfileValidator
{
    public const string IdentityInvalidCode = "project.world.identity_invalid";
    public const string SchemaUnsupportedCode = "project.world.schema_unsupported";
    public const string EventIdentityInvalidCode = "project.world.event_identity_invalid";
    public const string EventDuplicateCode = "project.world.event_duplicate";
    public const string EventContractInvalidCode = "project.world.event_contract_invalid";
    public const string ClockInvalidCode = "project.world.clock_invalid";
    public const string AreaReferenceMissingCode = "project.world.area_reference_missing";

    private static readonly HashSet<string> SupportedFieldTypes =
        new(["string", "int", "float", "bool", "guid"], StringComparer.Ordinal);

    public static IReadOnlyList<DomainDiagnostic> Validate(
        ProjectWorldProfile profile,
        Guid moduleId,
        IReadOnlySet<string>? areaIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        const string root = "module/runtimePolicies/world";
        if (profile.Id == Guid.Empty || profile.Id != moduleId)
            diagnostics.Add(new(IdentityInvalidCode, "World profile identity must match the module UUID.", $"{root}/id"));
        if (profile.SchemaVersion != ProjectWorldProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "World profile schema must be version 1.", $"{root}/schemaVersion"));

        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schema in profile.CustomEvents)
        {
            var path = $"{root}/customEvents/{schema.Id:D}";
            if (schema.Id == Guid.Empty || schema.SchemaVersion != CustomEventSchema.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(schema.Provenance))
                diagnostics.Add(new(EventIdentityInvalidCode,
                    "Custom event requires a stable UUID, schema v1 and provenance.", path));
            var qualified = $"{schema.Namespace}.{schema.Name}";
            if (!ids.Add(schema.Id) || !names.Add(qualified))
                diagnostics.Add(new(EventDuplicateCode, "Custom event UUIDs and qualified names must be unique.", path));
            if (!ValidSegment(schema.Namespace) || !ValidSegment(schema.Name) || schema.PayloadVersion < 1 ||
                schema.Fields.Count == 0 || schema.Fields.Keys.Any(key => !ValidSegment(key)) ||
                schema.Fields.Values.Any(value => !SupportedFieldTypes.Contains(value)) ||
                string.IsNullOrWhiteSpace(schema.ForgeScriptApiVersion) || !ValidHandler(schema.Handler) ||
                schema.Authority != EventAuthorityScope.Server)
                diagnostics.Add(new(EventContractInvalidCode,
                    "Custom event namespace, payload schema, ForgeScript binding or authority is invalid.", path));
        }

        var clock = profile.Clock;
        var clockPath = $"{root}/clock";
        var dayMinutes = (long)clock.HoursPerDay * clock.MinutesPerHour;
        if (clock.Id == Guid.Empty || clock.SchemaVersion != WorldClockProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(clock.Provenance) || clock.DaysPerMonth < 1 || clock.DaysPerMonth > 366 ||
            clock.MonthsPerYear < 1 || clock.MonthsPerYear > 100 || clock.HoursPerDay < 1 ||
            clock.HoursPerDay > 100 || clock.MinutesPerHour < 1 || clock.MinutesPerHour > 1_000 ||
            !double.IsFinite(clock.Speed) || clock.Speed < 0 || clock.Speed > 10_000 || clock.InitialMinute < 0 ||
            clock.AstronomicalMinutes.Values.Any(value => value < 0 || value >= dayMinutes) ||
            clock.AreaTimeZoneOffsets.Values.Any(value => Math.Abs((long)value) >= dayMinutes))
            diagnostics.Add(new(ClockInvalidCode,
                "World clock calendar, speed, initial time, zone offsets or astronomical events are invalid.",
                clockPath));
        if (areaIds is not null)
        {
            foreach (var areaId in clock.AreaTimeZoneOffsets.Keys.Where(key => !areaIds.Contains(key)))
                diagnostics.Add(new(AreaReferenceMissingCode,
                    $"World clock area '{areaId}' does not exist.", $"{clockPath}/areaTimeZoneOffsets/{areaId}"));
        }

        return diagnostics;
    }

    private static bool ValidSegment(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');

    private static bool ValidHandler(string value) =>
        !string.IsNullOrWhiteSpace(value) && (char.IsLetter(value[0]) || value[0] == '_') &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');
}

public sealed record ProjectWorldRuntimeHandle(Guid Id, int SchemaVersion, string Provenance, ProjectWorldProfile Profile);

public sealed record ProjectWorldRuntimeResolution(ProjectWorldRuntimeHandle? Handle, DomainDiagnostic? Diagnostic)
{
    public bool Succeeded => Diagnostic is null;
}

public static class ProjectWorldRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.project_world.required_missing";

    public static ProjectWorldRuntimeResolution Resolve(ProjectWorldProfile? profile, Guid moduleId, bool required)
    {
        if (profile is null || !profile.Enabled)
            return required
                ? new(null, new DomainDiagnostic(RequiredMissingCode,
                    "Required project world configuration is unavailable.", "module/runtimePolicies/world"))
                : new(null, null);
        var diagnostic = ProjectWorldProfileValidator.Validate(profile, moduleId).FirstOrDefault();
        return diagnostic is null
            ? new(new ProjectWorldRuntimeHandle(profile.Id, profile.SchemaVersion, profile.Provenance, profile), null)
            : new(null, diagnostic);
    }
}
