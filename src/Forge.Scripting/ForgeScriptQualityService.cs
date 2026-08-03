using System.Text.Json;

namespace Forge.Scripting;

public sealed record ScriptFixture(string Id, int Seed, IReadOnlyDictionary<string, object?> Entities, IReadOnlyDictionary<string, object?> Services);
public sealed record ScriptAssertion(string Name, Func<TestWorld, bool> Predicate, string Failure);
public sealed record ScriptTestCase(string Id, ScriptFixture Fixture, Action<TestWorld> Arrange, Action<TestWorld> Act, IReadOnlyList<ScriptAssertion> Assertions);
public sealed record ScriptTestResult(string Id, bool Passed, IReadOnlyList<string> Failures, IReadOnlyList<string> Events, int Seed);
public sealed class TestWorld(ScriptFixture fixture)
{
    public ScriptFixture Fixture { get; } = fixture;
    public Dictionary<string, object?> State { get; } = fixture.Entities.ToDictionary();
    public List<string> Events { get; } = [];
    public Random Random { get; } = new(fixture.Seed);
}
public sealed record ProfileSample(string ScriptId, string Function, long Instructions, long AllocatedBytes, TimeSpan Cpu, IReadOnlyList<string> CallPath);
public sealed record ProfileReport(IReadOnlyList<ProfileSample> Hotspots, long TotalInstructions, long TotalAllocatedBytes, TimeSpan TotalCpu, IReadOnlyList<string> Alerts);
public sealed record ApiRegistration(string Name, string Signature, string Summary, string ApiVersion, IReadOnlySet<string> Availability, string Example, string McpResource);
public sealed record GeneratedApiDocument(string ApiVersion, string Markdown, IReadOnlyList<string> McpResources);
public sealed record StaticDiagnostic(string Code, string Severity, int Line, string Message, string Suggestion);
public sealed record ScriptSource(string Path, string Text, string Authority, IReadOnlySet<string> NullableSymbols);
public sealed record PersistentComponent(string Name, int SchemaVersion, Type ValueType, Func<int, JsonElement, object?> Migrate);
public sealed record ScriptStateEnvelope(int SchemaVersion, IReadOnlyDictionary<string, JsonElement> Components);

public sealed class ForgeScriptQualityService
{
    private readonly Dictionary<string, PersistentComponent> _persistent = [];

    public static ScriptTestResult Run(ScriptTestCase test)
    {
        var world = new TestWorld(test.Fixture); test.Arrange(world); test.Act(world); var failures = test.Assertions.Where(assertion => !assertion.Predicate(world)).Select(assertion => $"{assertion.Name}:{assertion.Failure}").ToArray(); return new(test.Id, failures.Length == 0, failures, world.Events, test.Fixture.Seed);
    }
    public static string ToCiReport(IReadOnlyList<ScriptTestResult> results) => JsonSerializer.Serialize(new { schemaVersion = 1, passed = results.Count(item => item.Passed), failed = results.Count(item => !item.Passed), tests = results });

    public static ProfileReport Profile(IReadOnlyList<ProfileSample> samples, long instructionThreshold, long allocationThreshold, TimeSpan cpuThreshold)
    {
        var instructions = samples.Sum(sample => sample.Instructions); var allocations = samples.Sum(sample => sample.AllocatedBytes); var cpu = TimeSpan.FromTicks(samples.Sum(sample => sample.Cpu.Ticks)); var alerts = new List<string>(); if (instructions > instructionThreshold) alerts.Add($"SCRIPT_PROFILE_INSTRUCTIONS:{instructions}"); if (allocations > allocationThreshold) alerts.Add($"SCRIPT_PROFILE_ALLOCATIONS:{allocations}"); if (cpu > cpuThreshold) alerts.Add($"SCRIPT_PROFILE_CPU:{cpu.TotalMilliseconds:0.###}"); return new(samples.OrderByDescending(sample => sample.Cpu).ThenByDescending(sample => sample.Instructions).ToArray(), instructions, allocations, cpu, alerts);
    }

    public static GeneratedApiDocument GenerateDocumentation(IEnumerable<ApiRegistration> registrations, string apiVersion)
    {
        var selected = registrations.Where(item => item.ApiVersion == apiVersion).OrderBy(item => item.Name).ToArray(); var lines = new List<string> { $"# ForgeScript API {apiVersion}", "" }; foreach (var item in selected) { lines.Add($"## {item.Name}"); lines.Add(""); lines.Add($"`{item.Signature}` — {item.Summary}"); lines.Add($"Availability: {string.Join(", ", item.Availability.Order())}"); lines.Add($"Example: `{item.Example}`"); lines.Add(""); }
        return new(apiVersion, string.Join(Environment.NewLine, lines), selected.Select(item => item.McpResource).Distinct().Order().ToArray());
    }

    public static IReadOnlyList<StaticDiagnostic> Analyze(ScriptSource source)
    {
        var diagnostics = new List<StaticDiagnostic>(); var lines = source.Text.Replace("\r\n", "\n").Split('\n'); for (var index = 0; index < lines.Length; index++) { var line = lines[index]; if (line.Contains("OnHeartbeat", StringComparison.Ordinal) && line.Contains("FindAll", StringComparison.Ordinal)) diagnostics.Add(new("SCR-LINT-EXPENSIVE-HEARTBEAT", "warning", index + 1, "Requête globale coûteuse dans un heartbeat.", "Mettre en cache ou utiliser un événement.")); if (line.Contains("UnsafeQuery(", StringComparison.Ordinal)) diagnostics.Add(new("SCR-LINT-UNSAFE-QUERY", "error", index + 1, "Requête non bornée.", "Utiliser Query(limit, scope).")); if (line.Contains("DeprecatedApi(", StringComparison.Ordinal)) diagnostics.Add(new("SCR-LINT-DEPRECATED", "warning", index + 1, "API dépréciée.", "Consulter l'API générée.")); if (source.Authority != "server" && line.Contains("ServerOnly(", StringComparison.Ordinal)) diagnostics.Add(new("SCR-LINT-AUTHORITY", "error", index + 1, "Violation d'autorité.", "Déplacer l'appel dans un handler serveur.")); foreach (var symbol in source.NullableSymbols) if (line.Contains($"{symbol}.", StringComparison.Ordinal) && !line.Contains($"{symbol} != null", StringComparison.Ordinal)) diagnostics.Add(new("SCR-LINT-NULL", "warning", index + 1, $"'{symbol}' peut être null.", "Ajouter une validation explicite.")); }
        return diagnostics;
    }

    public void RegisterPersistent(PersistentComponent component) { if (string.IsNullOrWhiteSpace(component.Name) || component.SchemaVersion <= 0) throw new InvalidOperationException("SCRIPT_STATE_REGISTRATION_INVALID"); _persistent[component.Name] = component; }
    public string SerializeState(IReadOnlyDictionary<string, object?> state)
    {
        var registered = state.Where(pair => _persistent.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value, _persistent[pair.Key].ValueType)); return JsonSerializer.Serialize(new ScriptStateEnvelope(1, registered));
    }
    public IReadOnlyDictionary<string, object?> DeserializeState(string json)
    {
        var envelope = JsonSerializer.Deserialize<ScriptStateEnvelope>(json) ?? throw new InvalidOperationException("SCRIPT_STATE_INVALID"); if (envelope.SchemaVersion != 1) throw new InvalidOperationException("SCRIPT_STATE_VERSION_UNSUPPORTED"); var restored = new Dictionary<string, object?>(); foreach (var pair in envelope.Components) { if (!_persistent.TryGetValue(pair.Key, out var registration)) throw new InvalidOperationException($"SCRIPT_STATE_COMPONENT_UNREGISTERED:{pair.Key}"); restored[pair.Key] = registration.Migrate(registration.SchemaVersion, pair.Value); }
        return restored;
    }
}
