using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Domain.Areas;
using Forge.Domain.Validation;
using Forge.Scripting;

namespace Forge.Application.Areas;

public sealed record AreaUserEventRuntimeResult(
    bool Succeeded,
    bool Ignored,
    bool DryRun,
    AreaUserEvent? Binding,
    string CanonicalPayload,
    IReadOnlyList<EventDelivery> Deliveries,
    ForgeScriptCompileResult? Compilation,
    ForgeScriptExecutionResult? Execution,
    bool Executed,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static partial class AreaUserEventRuntimeService
{
    public const string MissingRequiredCode = "runtime.area_user_event.required_missing";
    public const string PayloadInvalidCode = "runtime.area_user_event.payload_invalid";
    public const string PayloadQuotaCode = "runtime.area_user_event.payload_quota_exceeded";
    public const string ApiMismatchCode = "runtime.area_user_event.api_mismatch";
    public const string SourceMissingCode = "runtime.area_user_event.source_missing";
    public const string HandlerMissingCode = "runtime.area_user_event.handler_missing";
    public const string CompileFailedCode = "runtime.area_user_event.compile_failed";
    public const string ExecutionFailedCode = "runtime.area_user_event.execution_failed";

    private const int MaximumPayloadBytes = 4_096;

    public static AreaUserEventRuntimeResult Dispatch(
        AreaDefinition area,
        string namespacedEvent,
        IReadOnlyDictionary<string, object?> payload,
        IForgeScriptRuntime runtime,
        Func<string, string?> resolveSource,
        bool requiredAtRuntime = false,
        bool dryRun = false,
        Guid? correlationId = null,
        Func<ForgeScriptArtifact, bool>? execute = null,
        ForgeScriptQuotas? quotas = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespacedEvent);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(resolveSource);
        var root = $"areas/{area.Id}/authoring/userEvents";
        var binding = area.Authoring.UserEvents.SingleOrDefault(item =>
            string.Equals(item.NamespacedName, namespacedEvent, StringComparison.Ordinal));
        if (binding is null)
        {
            return requiredAtRuntime
                ? Failure(null, dryRun, MissingRequiredCode,
                    $"Required area user event '{namespacedEvent}' is not configured.", root)
                : new(true, true, dryRun, null, string.Empty, [], null, null, false, []);
        }

        var bindingPath = $"{root}/{binding.Id:D}";
        var contractDiagnostics = AreaUserEventValidator.Validate(area, binding, requireExecutable: true);
        if (contractDiagnostics.Count > 0)
            return new(false, false, dryRun, binding, string.Empty, [], null, null, false,
                contractDiagnostics);
        if (!string.Equals(binding.ForgeScriptApiVersion, runtime.ApiVersion, StringComparison.Ordinal))
            return Failure(binding, dryRun, ApiMismatchCode,
                $"Area event requires ForgeScript API '{binding.ForgeScriptApiVersion}' but runtime is '{runtime.ApiVersion}'.",
                $"{bindingPath}/forgeScriptApiVersion");

        IReadOnlyList<EventDelivery> deliveries;
        string canonicalPayload;
        try
        {
            var tools = new ForgeScriptRuntimeTools();
            tools.Register(new Forge.Scripting.CustomEventSchema(binding.NamespacedName, binding.ApiVersion,
                binding.PayloadFields.ToDictionary(item => item.Key, item => ResolvePayloadType(item.Value),
                    StringComparer.Ordinal)));
            foreach (var subscription in binding.Subscriptions)
                tools.Subscribe(binding.NamespacedName, subscription, binding.ApiVersion);
            deliveries = tools.Publish(new Forge.Scripting.CustomEvent(binding.NamespacedName, binding.ApiVersion,
                payload, "server", correlationId ?? Guid.NewGuid()));
            canonicalPayload = SerializeCanonicalPayload(payload);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(binding, dryRun, PayloadInvalidCode, exception.Message,
                $"{bindingPath}/payloadFields");
        }
        catch (NotSupportedException exception)
        {
            return Failure(binding, dryRun, PayloadInvalidCode, exception.Message,
                $"{bindingPath}/payloadFields");
        }

        if (Encoding.UTF8.GetByteCount(canonicalPayload) > MaximumPayloadBytes)
            return Failure(binding, dryRun, PayloadQuotaCode,
                $"Canonical event payload exceeds {MaximumPayloadBytes} UTF-8 bytes.",
                $"{bindingPath}/payloadFields");

        string? source;
        try
        {
            source = resolveSource(binding.ScriptPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            source = null;
        }
        if (string.IsNullOrWhiteSpace(source))
            return binding.Required || requiredAtRuntime
                ? Failure(binding, dryRun, SourceMissingCode,
                    $"Required ForgeScript source '{binding.ScriptPath}' is unavailable.",
                    $"{bindingPath}/scriptPath")
                : new(true, true, dryRun, binding, canonicalPayload, deliveries, null, null, false, []);

        foreach (var subscription in binding.Subscriptions)
        {
            var present = string.Equals(subscription, "main", StringComparison.Ordinal)
                ? MainDeclaration().IsMatch(source)
                : TypedHandlerDeclaration(subscription).IsMatch(source);
            if (!present)
                return Failure(binding, dryRun, HandlerMissingCode,
                    string.Equals(subscription, "main", StringComparison.Ordinal)
                        ? "Subscription 'main' requires a void main() entry."
                        : $"Subscription '{subscription}' requires void {subscription}(CustomEventRef event_ref).",
                    $"{bindingPath}/subscriptions/{subscription}");
        }

        var entrySource = BuildEntrySource(source, binding, canonicalPayload);
        var compilation = runtime.Compile(entrySource);
        if (!compilation.Success || compilation.Artifact is null)
            return new(false, false, dryRun, binding, canonicalPayload, deliveries, compilation, null, false,
                [new(CompileFailedCode,
                    compilation.Diagnostics.Count == 0
                        ? "ForgeScript compilation failed."
                        : string.Join("; ", compilation.Diagnostics.Select(item => item.Message)),
                    $"{bindingPath}/scriptPath")]);
        if (dryRun)
            return new(true, false, true, binding, canonicalPayload, deliveries, compilation, null, false, []);

        ForgeScriptExecutionResult? execution = null;
        var executed = execute is not null
            ? execute(compilation.Artifact)
            : (execution = runtime.Execute(compilation.Artifact, quotas)).Status ==
              ForgeScriptExecutionStatus.Completed;
        return executed
            ? new(true, false, false, binding, canonicalPayload, deliveries, compilation, execution, true, [])
            : new(false, false, false, binding, canonicalPayload, deliveries, compilation, execution, false,
                [new(ExecutionFailedCode,
                    execution is null || execution.Diagnostics.Count == 0
                        ? "ForgeScript event execution failed."
                        : string.Join("; ", execution.Diagnostics.Select(item => item.Message)),
                    $"{bindingPath}/scriptPath")]);
    }

    public static string BuildEntrySource(string source, AreaUserEvent binding, string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        var hasMain = MainDeclaration().IsMatch(source);
        var body = hasMain
            ? MainDeclaration().Replace(source, "void __forge_area_user_event_main(", 1)
            : source;
        var calls = binding.Subscriptions.Select(subscription =>
            string.Equals(subscription, "main", StringComparison.Ordinal)
                ? "    __forge_area_user_event_main();"
                : $"    {subscription}(__forge_area_user_event);");
        return body + Environment.NewLine +
               "void main()" + Environment.NewLine + "{" + Environment.NewLine +
               $"    CustomEventRef __forge_area_user_event = custom_event(\"{Escape(binding.NamespacedName)}\", " +
               $"{binding.ApiVersion}, \"{Escape(canonicalPayload)}\");" + Environment.NewLine +
               "    event_publish(__forge_area_user_event);" + Environment.NewLine +
               string.Join(Environment.NewLine, calls) + Environment.NewLine + "}" + Environment.NewLine;
    }

    private static AreaUserEventRuntimeResult Failure(
        AreaUserEvent? binding,
        bool dryRun,
        string code,
        string message,
        string path) =>
        new(false, false, dryRun, binding, string.Empty, [], null, null, false,
            [new(code, message, path)]);

    private static Type ResolvePayloadType(string type) => type switch
    {
        "bool" => typeof(bool),
        "int" => typeof(int),
        "long" => typeof(long),
        "float" => typeof(double),
        "string" => typeof(string),
        "guid" => typeof(Guid),
        _ => throw new NotSupportedException($"Area event payload type '{type}' is unsupported."),
    };

    private static string SerializeCanonicalPayload(IReadOnlyDictionary<string, object?> payload) =>
        JsonSerializer.Serialize(payload.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    [GeneratedRegex(@"\bvoid\s+main\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MainDeclaration();

    private static Regex TypedHandlerDeclaration(string handler) => new(
        @"\bvoid\s+" + Regex.Escape(handler) + @"\s*\(\s*CustomEventRef(?:\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\)",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
