namespace Forge.Scripting;

public sealed record ForgeScriptMockWorld(
    IReadOnlyDictionary<string, object?> Entities,
    IReadOnlyDictionary<string, object?> Services,
    IReadOnlySet<string> AllowedEventKinds);

public sealed record ForgeScriptSandboxTestCase(
    string Id,
    string SourcePath,
    string Source,
    ForgeScriptQuotas Quotas,
    ForgeScriptMockWorld MockWorld,
    IReadOnlySet<string> ExpectedEventKinds);

public sealed record ForgeScriptSandboxTestResult(
    string Id,
    bool Compiled,
    ForgeScriptExecutionStatus Status,
    bool Passed,
    double EventCoverage,
    IReadOnlyList<string> CoveredEventKinds,
    IReadOnlyList<string> MockWorldCalls,
    IReadOnlyList<string> Failures,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);

public sealed record ForgeScriptSandboxSuiteReport(
    bool Passed,
    int Compiled,
    int Executed,
    double Coverage,
    IReadOnlyList<ForgeScriptSandboxTestResult> Tests);

public sealed class ForgeScriptSandboxTestService(IForgeScriptRuntime runtime)
{
    private readonly IForgeScriptRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public ForgeScriptSandboxTestResult Run(ForgeScriptSandboxTestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        if (string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.SourcePath) ||
            Path.IsPathRooted(test.SourcePath) || test.SourcePath.Replace('\\', '/').Split('/').Any(segment => segment is "." or ".."))
            throw new ArgumentException("Script sandbox test identity and source path must be safe.", nameof(test));

        var compilation = _runtime.Compile(test.Source);
        if (!compilation.Success || compilation.Artifact is null)
            return new(test.Id, false, ForgeScriptExecutionStatus.Failed, false, 0, [], [],
                ["compile-failed"], compilation.Diagnostics);

        var execution = _runtime.Execute(compilation.Artifact, test.Quotas);
        var covered = execution.Events.Select(item => item.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var calls = execution.Events.Select(item => $"{item.Kind}:{item.Value}").ToArray();
        var failures = new List<string>();
        if (execution.Status != ForgeScriptExecutionStatus.Completed)
            failures.Add($"execution-status:{execution.Status}");
        foreach (var kind in covered.Where(kind => test.MockWorld.AllowedEventKinds.Count > 0 &&
                     !test.MockWorld.AllowedEventKinds.Contains(kind)))
            failures.Add($"mock-world-event-forbidden:{kind}");
        foreach (var expected in test.ExpectedEventKinds.Where(expected => !covered.Contains(expected, StringComparer.Ordinal)))
            failures.Add($"event-not-covered:{expected}");
        var coverage = test.ExpectedEventKinds.Count == 0 ? 1 :
            (double)test.ExpectedEventKinds.Count(covered.Contains) / test.ExpectedEventKinds.Count;
        return new(test.Id, true, execution.Status, failures.Count == 0, coverage, covered, calls,
            failures, execution.Diagnostics);
    }

    public ForgeScriptSandboxSuiteReport RunSuite(IEnumerable<ForgeScriptSandboxTestCase> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        var results = tests.OrderBy(test => test.Id, StringComparer.Ordinal).Select(Run).ToArray();
        return new(results.All(result => result.Passed), results.Count(result => result.Compiled),
            results.Count(result => result.Status == ForgeScriptExecutionStatus.Completed),
            results.Length == 0 ? 1 : results.Average(result => result.EventCoverage), results);
    }
}
