using System.Text;
using System.Text.Json;

namespace Forge.Scripting;

public sealed record ScriptFixture(
    string Id,
    int Seed,
    IReadOnlyDictionary<string, object?> Entities,
    IReadOnlyDictionary<string, object?> Services);

public sealed record ScriptAssertion(string Name, Func<TestWorld, bool> Predicate, string Failure);

public sealed record ScriptTestCase(
    string Id,
    ScriptFixture Fixture,
    Action<TestWorld> Arrange,
    Action<TestWorld> Act,
    IReadOnlyList<ScriptAssertion> Assertions);

public sealed record ScriptTestResult(
    string Id,
    bool Passed,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Events,
    int Seed);

public sealed class TestWorld(ScriptFixture fixture)
{
    public ScriptFixture Fixture { get; } = fixture ?? throw new ArgumentNullException(nameof(fixture));
    public Dictionary<string, object?> State { get; } = fixture.Entities.ToDictionary(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, object?> Services { get; } = fixture.Services.ToDictionary(StringComparer.Ordinal);
    public List<string> Events { get; } = [];
    public Random Random { get; } = new(fixture.Seed);
}

public sealed record ProfileSample(
    string ScriptId,
    string Function,
    long Instructions,
    long AllocatedBytes,
    TimeSpan Cpu,
    IReadOnlyList<string> CallPath);

public sealed record ProfileReport(
    IReadOnlyList<ProfileSample> Hotspots,
    long TotalInstructions,
    long TotalAllocatedBytes,
    TimeSpan TotalCpu,
    IReadOnlyList<string> Alerts)
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CallGraph { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}

public sealed record ApiRegistration(
    string Name,
    string Signature,
    string Summary,
    string ApiVersion,
    IReadOnlySet<string> Availability,
    string Example,
    string McpResource);

public sealed record GeneratedApiDocument(
    string ApiVersion,
    string Markdown,
    IReadOnlyList<string> McpResources);

public sealed record StaticDiagnostic(string Code, string Severity, int Line, string Message, string Suggestion)
{
    public string Path { get; init; } = "script/source";
}

public sealed record ScriptSource(
    string Path,
    string Text,
    string Authority,
    IReadOnlySet<string> NullableSymbols);

public sealed record PersistentComponent(
    string Name,
    int SchemaVersion,
    Type ValueType,
    Func<int, JsonElement, object?> Migrate)
{
    public Func<object?, bool> Validate { get; init; } = _ => true;
}

public sealed record PersistedScriptComponent(int SchemaVersion, JsonElement Value);

public sealed record ScriptStateEnvelope(
    int SchemaVersion,
    IReadOnlyDictionary<string, PersistedScriptComponent> Components);

public sealed record ForgeScriptUnitTestSettings(Guid Id, int DeterministicSeed, int MaximumCases);
public sealed record ForgeScriptProfilerSettings(
    Guid Id,
    long InstructionThreshold,
    long AllocationThresholdBytes,
    int CpuThresholdMilliseconds);
public sealed record ForgeScriptDocumentationSettings(
    Guid Id,
    string OutputRelativePath,
    IReadOnlyList<string> Availability);
public sealed record ForgeScriptStaticAnalysisSettings(
    Guid Id,
    IReadOnlyList<string> EnabledRules,
    bool TreatWarningsAsErrors);
public sealed record ForgeScriptStateSettings(
    Guid Id,
    int EnvelopeSchemaVersion,
    int MaximumBytes,
    IReadOnlyDictionary<string, int> RegisteredComponents);

public sealed record ForgeScriptQualityProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    string EventBinding,
    bool RequiredAtRuntime,
    ForgeScriptUnitTestSettings UnitTests,
    ForgeScriptProfilerSettings Profiler,
    ForgeScriptDocumentationSettings Documentation,
    ForgeScriptStaticAnalysisSettings StaticAnalysis,
    ForgeScriptStateSettings State)
{
    public const int CurrentSchemaVersion = 1;

    public static ForgeScriptQualityProfile Default { get; } = new(
        Guid.Parse("c7100000-0000-4000-8000-000000000100"),
        CurrentSchemaVersion,
        "forge:r100",
        "1.0.0",
        "module.script.quality",
        false,
        new(Guid.Parse("c7100000-0000-4000-8000-000000000101"), 2026, 128),
        new(Guid.Parse("c7100000-0000-4000-8000-000000000102"), 10_000, 8 * 1024 * 1024, 100),
        new(Guid.Parse("c7100000-0000-4000-8000-000000000103"),
            "docs/generated/forgescript-api.md",
            ["server", "client", "rpg-kit"]),
        new(Guid.Parse("c7100000-0000-4000-8000-000000000104"),
            [.. ForgeScriptQualityProfileValidator.SupportedRules], false),
        new(Guid.Parse("c7100000-0000-4000-8000-000000000105"), 1, 1024 * 1024,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["quest"] = 1, ["score"] = 1 }));
}

public sealed record ForgeScriptQualityDiagnostic(
    string Code,
    string Path,
    Guid? SubjectId,
    string Severity,
    string Message);

public static class ForgeScriptQualityProfileValidator
{
    public static IReadOnlyList<string> SupportedRules { get; } =
    [
        "expensive-heartbeat",
        "unsafe-query",
        "nullable-access",
        "deprecated-api",
        "authority-violation",
    ];

    public static IReadOnlyList<ForgeScriptQualityDiagnostic> Validate(ForgeScriptQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<ForgeScriptQualityDiagnostic>();
        Add(profile.Id == Guid.Empty, "script.quality.uuid_required", "script/quality/id", profile.Id,
            "A stable profile UUID is required.");
        Add(profile.SchemaVersion != ForgeScriptQualityProfile.CurrentSchemaVersion,
            "script.quality.schema_unsupported", "script/quality/schemaVersion", profile.Id,
            "ForgeScript quality schema v1 is required.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "script.quality.provenance_required",
            "script/quality/provenance", profile.Id, "Modification provenance is required.");
        Add(!ForgeScriptApiCompatibility.IsSupported(profile.ApiVersion), "script.quality.api_unsupported",
            "script/quality/apiVersion", profile.Id, "The ForgeScript API version is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.EventBinding), "script.quality.event_binding_required",
            "script/quality/eventBinding", profile.Id, "A quality event binding is required.");

        var features = new[]
        {
            profile.UnitTests.Id,
            profile.Profiler.Id,
            profile.Documentation.Id,
            profile.StaticAnalysis.Id,
            profile.State.Id,
        };
        Add(features.Any(id => id == Guid.Empty), "script.quality.feature_uuid_required",
            "script/quality/features", profile.Id, "Every quality feature requires a stable UUID.");
        Add(features.Where(id => id != Guid.Empty).Distinct().Count() != features.Count(id => id != Guid.Empty),
            "script.quality.feature_uuid_duplicate", "script/quality/features", profile.Id,
            "Quality feature UUIDs must be unique.");
        Add(profile.UnitTests.DeterministicSeed == 0 || profile.UnitTests.MaximumCases is < 1 or > 10_000,
            "script.quality.tests_budget_invalid", "script/quality/unitTests", profile.UnitTests.Id,
            "Test seed must be non-zero and maximum cases must be between 1 and 10000.");
        Add(profile.Profiler.InstructionThreshold < 1 || profile.Profiler.AllocationThresholdBytes < 1 ||
            profile.Profiler.CpuThresholdMilliseconds < 1,
            "script.quality.profiler_threshold_invalid", "script/quality/profiler", profile.Profiler.Id,
            "Profiler instruction, allocation and CPU thresholds must be positive.");
        Add(!SafeGeneratedDocumentationPath(profile.Documentation.OutputRelativePath),
            "script.quality.documentation_path_invalid", "script/quality/documentation/outputRelativePath",
            profile.Documentation.Id, "Generated documentation must target a relative docs/generated Markdown path.");
        Add(profile.Documentation.Availability.Count == 0 || profile.Documentation.Availability.Any(value =>
                value is not ("server" or "client" or "rpg-kit")),
            "script.quality.documentation_availability_invalid", "script/quality/documentation/availability",
            profile.Documentation.Id, "Availability must use server, client or rpg-kit.");
        Add(profile.StaticAnalysis.EnabledRules.Count == 0 || profile.StaticAnalysis.EnabledRules.Any(rule =>
                !SupportedRules.Contains(rule, StringComparer.Ordinal)),
            "script.quality.static_rule_invalid", "script/quality/staticAnalysis/enabledRules",
            profile.StaticAnalysis.Id, "Static analysis rules must be selected from the versioned rule set.");
        Add(profile.State.EnvelopeSchemaVersion != 1 || profile.State.MaximumBytes is < 1024 or > 64 * 1024 * 1024,
            "script.quality.state_budget_invalid", "script/quality/state", profile.State.Id,
            "State envelope schema must be 1 and maximum bytes must be between 1 KiB and 64 MiB.");
        Add(profile.State.RegisteredComponents.Count == 0 || profile.State.RegisteredComponents.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1),
            "script.quality.state_component_invalid", "script/quality/state/registeredComponents",
            profile.State.Id, "Registered state components require a name and positive schema version.");
        return diagnostics;

        void Add(bool condition, string code, string path, Guid subjectId, string message)
        {
            if (condition) diagnostics.Add(new(code, path, subjectId == Guid.Empty ? null : subjectId, "error", message));
        }
    }

    private static bool SafeGeneratedDocumentationPath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathRooted(path) &&
        !path.Contains("..", StringComparison.Ordinal) &&
        path.Replace('\\', '/').StartsWith("docs/generated/", StringComparison.Ordinal) &&
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
}

public sealed record ForgeScriptQualityCommand(
    Guid TransactionId,
    long ExpectedRevision,
    ForgeScriptQualityProfile Profile,
    string Reason);

public sealed record ForgeScriptQualityMutation(
    bool Succeeded,
    ForgeScriptQualityProfile Profile,
    long Revision,
    string SemanticDiff,
    IReadOnlyList<ForgeScriptQualityDiagnostic> Diagnostics);

public sealed class ForgeScriptQualityHistory(ForgeScriptQualityProfile initial)
{
    private readonly Stack<ForgeScriptQualityProfile> _undo = [];
    private readonly Stack<ForgeScriptQualityProfile> _redo = [];
    public ForgeScriptQualityProfile Profile { get; private set; } = initial ?? throw new ArgumentNullException(nameof(initial));
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptQualityMutation Preview(ForgeScriptQualityCommand command) => Evaluate(command);

    public ForgeScriptQualityMutation Apply(ForgeScriptQualityCommand command)
    {
        var result = Evaluate(command);
        if (!result.Succeeded) return result;
        _undo.Push(Profile);
        _redo.Clear();
        Profile = command.Profile;
        Revision++;
        return result with { Profile = Profile, Revision = Revision };
    }

    public ForgeScriptQualityMutation Undo()
    {
        if (!CanUndo) return Failure("script.quality.undo_empty", "script/quality/history", "Nothing to undo.");
        _redo.Push(Profile);
        Profile = _undo.Pop();
        Revision++;
        return new(true, Profile, Revision, "undo:script-quality", []);
    }

    public ForgeScriptQualityMutation Redo()
    {
        if (!CanRedo) return Failure("script.quality.redo_empty", "script/quality/history", "Nothing to redo.");
        _undo.Push(Profile);
        Profile = _redo.Pop();
        Revision++;
        return new(true, Profile, Revision, "redo:script-quality", []);
    }

    private ForgeScriptQualityMutation Evaluate(ForgeScriptQualityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TransactionId == Guid.Empty || command.ExpectedRevision != Revision ||
            string.IsNullOrWhiteSpace(command.Reason))
            return Failure("script.quality.command_invalid", "script/quality/command",
                "Transaction identity, current revision and reason are required.");
        var diagnostics = ForgeScriptQualityProfileValidator.Validate(command.Profile);
        return diagnostics.Count > 0
            ? new(false, Profile, Revision, "no-change", diagnostics)
            : new(true, command.Profile, Revision,
                $"quality:{Profile.Id:D}|tests:{Profile.UnitTests.MaximumCases}->{command.Profile.UnitTests.MaximumCases}|state:{Profile.State.MaximumBytes}->{command.Profile.State.MaximumBytes}", []);
    }

    private ForgeScriptQualityMutation Failure(string code, string path, string message) =>
        new(false, Profile, Revision, "no-change", [new(code, path, Profile.Id, "error", message)]);
}

public sealed class ForgeScriptQualityStore(string root)
{
    public const string RelativePath = "forge-script-quality.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));

    public void Save(ForgeScriptQualityProfile profile)
    {
        var diagnostics = ForgeScriptQualityProfileValidator.Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(string.Join(';', diagnostics.Select(item => item.Code)));
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, RelativePath);
        var temporary = destination + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, JsonOptions));
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public ForgeScriptQualityProfile Load()
    {
        var profile = JsonSerializer.Deserialize<ForgeScriptQualityProfile>(
                          File.ReadAllText(Path.Combine(_root, RelativePath)), JsonOptions)
                      ?? throw new InvalidOperationException("script.quality.document_invalid");
        var diagnostics = ForgeScriptQualityProfileValidator.Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(string.Join(';', diagnostics.Select(item => item.Code)));
        return profile;
    }
}

public sealed class ForgeScriptQualityService
{
    private readonly Dictionary<string, PersistentComponent> _persistent = new(StringComparer.Ordinal);
    private readonly int _maximumStateBytes;

    public ForgeScriptQualityService(int maximumStateBytes = 1024 * 1024)
    {
        if (maximumStateBytes is < 1024 or > 64 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        _maximumStateBytes = maximumStateBytes;
    }

    public static ScriptTestResult Run(ScriptTestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        if (string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.Fixture.Id) ||
            test.Fixture.Seed == 0 || test.Assertions.Count == 0 ||
            test.Assertions.Any(assertion => string.IsNullOrWhiteSpace(assertion.Name)))
            throw new InvalidOperationException("SCRIPT_TEST_CONFIGURATION_INVALID");
        var world = new TestWorld(test.Fixture);
        try
        {
            test.Arrange(world);
            test.Act(world);
        }
        catch (Exception exception)
        {
            return new(test.Id, false, [$"execution:{exception.GetType().Name}:{exception.Message}"],
                world.Events.ToArray(), test.Fixture.Seed);
        }
        var failures = test.Assertions
            .Where(assertion => !assertion.Predicate(world))
            .Select(assertion => $"{assertion.Name}:{assertion.Failure}")
            .ToArray();
        return new(test.Id, failures.Length == 0, failures, world.Events.ToArray(), test.Fixture.Seed);
    }

    public static IReadOnlyList<ScriptTestResult> RunSuite(
        IReadOnlyList<ScriptTestCase> tests,
        int maximumCases)
    {
        ArgumentNullException.ThrowIfNull(tests);
        if (maximumCases < 1 || tests.Count > maximumCases ||
            tests.Select(test => test.Id).Distinct(StringComparer.Ordinal).Count() != tests.Count)
            throw new InvalidOperationException("SCRIPT_TEST_SUITE_INVALID_OR_QUOTA_EXCEEDED");
        return tests.Select(Run).ToArray();
    }

    public static string ToCiReport(IReadOnlyList<ScriptTestResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            passed = results.Count(item => item.Passed),
            failed = results.Count(item => !item.Passed),
            deterministicSeeds = results.Select(item => item.Seed).Distinct().Order().ToArray(),
            tests = results,
        });
    }

    public static ProfileReport Profile(
        IReadOnlyList<ProfileSample> samples,
        long instructionThreshold,
        long allocationThreshold,
        TimeSpan cpuThreshold)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (instructionThreshold < 1 || allocationThreshold < 1 || cpuThreshold <= TimeSpan.Zero ||
            samples.Any(sample => string.IsNullOrWhiteSpace(sample.ScriptId) ||
                                  string.IsNullOrWhiteSpace(sample.Function) || sample.Instructions < 0 ||
                                  sample.AllocatedBytes < 0 || sample.Cpu < TimeSpan.Zero))
            throw new InvalidOperationException("SCRIPT_PROFILE_CONFIGURATION_INVALID");
        var aggregated = samples
            .GroupBy(sample => (sample.ScriptId, sample.Function))
            .Select(group => new ProfileSample(group.Key.ScriptId, group.Key.Function,
                group.Sum(sample => sample.Instructions), group.Sum(sample => sample.AllocatedBytes),
                TimeSpan.FromTicks(group.Sum(sample => sample.Cpu.Ticks)),
                group.SelectMany(sample => sample.CallPath).Distinct(StringComparer.Ordinal).ToArray()))
            .OrderByDescending(sample => sample.Cpu)
            .ThenByDescending(sample => sample.Instructions)
            .ThenBy(sample => sample.ScriptId, StringComparer.Ordinal)
            .ThenBy(sample => sample.Function, StringComparer.Ordinal)
            .ToArray();
        var instructions = aggregated.Sum(sample => sample.Instructions);
        var allocations = aggregated.Sum(sample => sample.AllocatedBytes);
        var cpu = TimeSpan.FromTicks(aggregated.Sum(sample => sample.Cpu.Ticks));
        var alerts = aggregated.SelectMany(sample => new[]
            {
                sample.Instructions > instructionThreshold
                    ? $"SCRIPT_PROFILE_INSTRUCTIONS:{sample.ScriptId}:{sample.Function}:{sample.Instructions}"
                    : null,
                sample.AllocatedBytes > allocationThreshold
                    ? $"SCRIPT_PROFILE_ALLOCATIONS:{sample.ScriptId}:{sample.Function}:{sample.AllocatedBytes}"
                    : null,
                sample.Cpu > cpuThreshold
                    ? $"SCRIPT_PROFILE_CPU:{sample.ScriptId}:{sample.Function}:{sample.Cpu.TotalMilliseconds:0.###}"
                    : null,
            })
            .Where(alert => alert is not null)
            .Cast<string>()
            .ToArray();
        return new(aggregated, instructions, allocations, cpu, alerts)
        {
            CallGraph = aggregated.ToDictionary(
                sample => $"{sample.ScriptId}::{sample.Function}",
                sample => (IReadOnlyList<string>)sample.CallPath,
                StringComparer.Ordinal),
        };
    }

    public static GeneratedApiDocument GenerateDocumentation(
        IEnumerable<ApiRegistration> registrations,
        string apiVersion)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);
        var selected = registrations
            .Where(item => item.ApiVersion == apiVersion)
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var lines = new List<string>
        {
            $"# ForgeScript API {apiVersion}",
            "",
            "> Generated from the versioned API registration catalog. Do not edit manually.",
            "",
        };
        foreach (var item in selected)
        {
            lines.Add($"## {item.Name}");
            lines.Add("");
            lines.Add($"`{item.Signature}` — {item.Summary}");
            lines.Add($"Availability: {string.Join(", ", item.Availability.Order(StringComparer.Ordinal))}");
            lines.Add($"MCP resource: `{item.McpResource}`");
            lines.Add($"Example: `{item.Example}`");
            lines.Add("");
        }
        return new(apiVersion, string.Join(Environment.NewLine, lines),
            selected.Select(item => item.McpResource).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<StaticDiagnostic> Analyze(
        ScriptSource source,
        ForgeScriptStaticAnalysisSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Path) || string.IsNullOrWhiteSpace(source.Authority))
            throw new InvalidOperationException("SCRIPT_STATIC_SOURCE_INVALID");
        var enabled = settings?.EnabledRules ?? ForgeScriptQualityProfileValidator.SupportedRules;
        var diagnostics = new List<StaticDiagnostic>();
        var lines = source.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            Add(enabled.Contains("expensive-heartbeat") && line.Contains("OnHeartbeat", StringComparison.Ordinal) &&
                line.Contains("FindAll", StringComparison.Ordinal), "SCR-LINT-EXPENSIVE-HEARTBEAT", "warning",
                "Global query in a heartbeat.", "Cache the result or use an event.");
            Add(enabled.Contains("unsafe-query") && line.Contains("UnsafeQuery(", StringComparison.Ordinal),
                "SCR-LINT-UNSAFE-QUERY", "error", "Unbounded query.", "Use Query(limit, scope).");
            Add(enabled.Contains("deprecated-api") && line.Contains("DeprecatedApi(", StringComparison.Ordinal),
                "SCR-LINT-DEPRECATED", "warning", "Deprecated API.", "Use the generated API reference.");
            Add(enabled.Contains("authority-violation") && source.Authority != "server" &&
                line.Contains("ServerOnly(", StringComparison.Ordinal), "SCR-LINT-AUTHORITY", "error",
                "Authority violation.", "Move the call to a server handler.");
            if (enabled.Contains("nullable-access"))
                foreach (var symbol in source.NullableSymbols.Order(StringComparer.Ordinal))
                    Add(line.Contains($"{symbol}.", StringComparison.Ordinal) &&
                        !line.Contains($"{symbol} != null", StringComparison.Ordinal) &&
                        !line.Contains($"is_valid({symbol})", StringComparison.Ordinal),
                        "SCR-LINT-NULL", "warning", $"'{symbol}' may be null or invalid.",
                        "Add an explicit null or validity check.");

            void Add(bool condition, string code, string severity, string message, string suggestion)
            {
                if (!condition) return;
                diagnostics.Add(new(code,
                    settings?.TreatWarningsAsErrors == true && severity == "warning" ? "error" : severity,
                    index + 1, message, suggestion)
                { Path = $"{source.Path}:{index + 1}" });
            }
        }
        return diagnostics;
    }

    public void RegisterPersistent(PersistentComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (string.IsNullOrWhiteSpace(component.Name) || component.SchemaVersion <= 0 ||
            component.ValueType is null || component.Migrate is null || _persistent.ContainsKey(component.Name))
            throw new InvalidOperationException("SCRIPT_STATE_REGISTRATION_INVALID");
        _persistent.Add(component.Name, component);
    }

    public string SerializeState(IReadOnlyDictionary<string, object?> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var registered = new Dictionary<string, PersistedScriptComponent>(StringComparer.Ordinal);
        foreach (var pair in state.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!_persistent.TryGetValue(pair.Key, out var component)) continue;
            if (!component.Validate(pair.Value))
                throw new InvalidOperationException($"SCRIPT_STATE_COMPONENT_INVALID:{pair.Key}");
            registered[pair.Key] = new(component.SchemaVersion,
                JsonSerializer.SerializeToElement(pair.Value, component.ValueType));
        }
        var json = JsonSerializer.Serialize(new ScriptStateEnvelope(1, registered));
        if (Encoding.UTF8.GetByteCount(json) > _maximumStateBytes)
            throw new InvalidOperationException("SCRIPT_STATE_QUOTA_EXCEEDED");
        return json;
    }

    public IReadOnlyDictionary<string, object?> DeserializeState(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > _maximumStateBytes)
            throw new InvalidOperationException("SCRIPT_STATE_QUOTA_EXCEEDED");
        ScriptStateEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ScriptStateEnvelope>(json)
                       ?? throw new InvalidOperationException("SCRIPT_STATE_INVALID");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("SCRIPT_STATE_INVALID", exception);
        }
        if (envelope.SchemaVersion != 1) throw new InvalidOperationException("SCRIPT_STATE_VERSION_UNSUPPORTED");
        var restored = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in envelope.Components.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!_persistent.TryGetValue(pair.Key, out var registration))
                throw new InvalidOperationException($"SCRIPT_STATE_COMPONENT_UNREGISTERED:{pair.Key}");
            if (pair.Value.SchemaVersion < 1 || pair.Value.SchemaVersion > registration.SchemaVersion)
                throw new InvalidOperationException($"SCRIPT_STATE_COMPONENT_VERSION_UNSUPPORTED:{pair.Key}");
            var value = registration.Migrate(pair.Value.SchemaVersion, pair.Value.Value);
            if (!registration.Validate(value))
                throw new InvalidOperationException($"SCRIPT_STATE_COMPONENT_INVALID:{pair.Key}");
            restored[pair.Key] = value;
        }
        return restored;
    }
}
