using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.ModuleFormat.Quality;

public enum ObservabilityCaptureBackend
{
    OpenTelemetry,
    TracyNative,
}

public sealed record WorkspaceObservabilityCaptureOptions(
    ObservabilityCaptureBackend Backend,
    int RequestedCaptureSeconds,
    bool IncludeScripts,
    bool WriteArtifact);

public sealed record ObservabilityCaptureDiagnostic(
    string Code,
    string Path,
    string Message,
    Guid? RelatedId = null);

public sealed record WorkspaceObservabilityCaptureReport(
    bool Succeeded,
    bool Cancelled,
    ObservabilityCaptureBackend Backend,
    int SpanCount,
    IReadOnlyList<QualityTraceSurface> Surfaces,
    IReadOnlyList<string> Categories,
    string Fingerprint,
    string? ArtifactPath,
    IReadOnlyList<ObservabilityCaptureDiagnostic> Diagnostics);

public sealed class WorkspaceObservabilityCaptureService
{
    public const string OpenTelemetryArtifactRelativePath =
        ".forge/quality/observability/open-telemetry-trace.json";

    private const string ObservabilityPath =
        "module/runtimePolicies/extensions/postLaunch/creatorExperience/observability";

    public WorkspaceObservabilityCaptureReport Run(
        string workspaceRoot,
        ProjectDefinition project,
        WorkspaceObservabilityCaptureOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var profile = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience.Observability;

        if (cancellationToken.IsCancellationRequested)
            return Cancelled(options.Backend, profile.Id);
        if (!profile.Enabled)
            return Failed(options.Backend, "OBSERVABILITY_DISABLED", $"{ObservabilityPath}/enabled",
                "Observability must be enabled before starting a capture.", profile.Id);
        if (options.RequestedCaptureSeconds is < 1 ||
            options.RequestedCaptureSeconds > profile.MaximumCaptureSeconds)
            return Failed(options.Backend, "OBSERVABILITY_DURATION_OUT_OF_RANGE",
                $"{ObservabilityPath}/maximumCaptureSeconds",
                $"Capture duration must be between 1 and {profile.MaximumCaptureSeconds} seconds.", profile.Id);
        if (options.IncludeScripts && !profile.IncludeScripts)
            return Failed(options.Backend, "OBSERVABILITY_SCRIPT_SPANS_DISABLED",
                $"{ObservabilityPath}/includeScripts",
                "Script spans are disabled by the canonical observability profile.", profile.Id);

        if (options.Backend == ObservabilityCaptureBackend.TracyNative)
        {
            if (!profile.TracyCompatible)
                return Failed(options.Backend, "OBSERVABILITY_TRACY_NOT_ALLOWED",
                    $"{ObservabilityPath}/tracyCompatible",
                    "The canonical profile does not allow Tracy captures.", profile.Id);
            return Failed(options.Backend, "OBSERVABILITY_TRACY_RUNTIME_MISSING",
                "runtime/observability/tracy",
                "Native Tracy capture requires a Godot build instrumented with Tracy and a matching capture server.",
                profile.Id);
        }

        if (!profile.OpenTelemetryCompatible)
            return Failed(options.Backend, "OBSERVABILITY_OTEL_NOT_ALLOWED",
                $"{ObservabilityPath}/openTelemetryCompatible",
                "The canonical profile does not allow OpenTelemetry captures.", profile.Id);

        cancellationToken.ThrowIfCancellationRequested();
        var performance = new WorkspacePerformanceLabService().Run(root, project,
            new WorkspacePerformanceLabOptions(WriteArtifacts: false));
        cancellationToken.ThrowIfCancellationRequested();

        var spans = performance.TraceSpans
            .Where(span => options.IncludeScripts || !string.Equals(span.Category, "script", StringComparison.Ordinal))
            .OrderBy(span => span.StartedMicroseconds)
            .ThenBy(span => span.Name, StringComparer.Ordinal)
            .ToArray();
        var surfaces = spans.Select(span => span.Surface).Distinct().Order().ToArray();
        var categories = spans.Select(span => span.Category).Distinct().Order(StringComparer.Ordinal).ToArray();
        var missingSurfaces = Enum.GetValues<QualityTraceSurface>().Except(surfaces).ToArray();
        if (missingSurfaces.Length > 0)
            return Failed(options.Backend, "OBSERVABILITY_SURFACE_MISSING",
                "runtime/observability/surfaces",
                $"Capture is missing required surfaces: {string.Join(", ", missingSurfaces)}.", profile.Id);

        var fingerprint = Fingerprint(profile.Id, options, spans);
        string? artifactPath = null;
        if (options.WriteArtifact)
        {
            cancellationToken.ThrowIfCancellationRequested();
            artifactPath = WriteOpenTelemetryJson(root, project, options, spans, fingerprint);
        }

        return new(true, false, options.Backend, spans.Length, surfaces, categories, fingerprint,
            artifactPath, []);
    }

    private static WorkspaceObservabilityCaptureReport Cancelled(
        ObservabilityCaptureBackend backend,
        Guid profileId) => new(false, true, backend, 0, [], [], string.Empty, null,
        [new("OBSERVABILITY_CAPTURE_CANCELLED", "runtime/observability/capture",
            "Capture was cancelled before producing an artifact.", profileId)]);

    private static WorkspaceObservabilityCaptureReport Failed(
        ObservabilityCaptureBackend backend,
        string code,
        string path,
        string message,
        Guid profileId) => new(false, false, backend, 0, [], [], string.Empty, null,
        [new(code, path, message, profileId)]);

    private static string WriteOpenTelemetryJson(
        string root,
        ProjectDefinition project,
        WorkspaceObservabilityCaptureOptions options,
        IReadOnlyList<QualityTraceSpan> spans,
        string fingerprint)
    {
        var output = Path.GetFullPath(Path.Combine(root,
            OpenTelemetryArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Observability artifact path escapes the workspace.");

        var resourceSpans = spans.GroupBy(span => span.Surface).OrderBy(group => group.Key).Select(group => new
        {
            resource = new
            {
                attributes = new object[]
                {
                    new { key = "service.name", value = "openrpg-forge" },
                    new { key = "service.namespace", value = "forge.studio" },
                    new { key = "openrpg.module", value = project.Module.Slug },
                    new { key = "openrpg.surface", value = group.Key.ToString().ToLowerInvariant() },
                },
            },
            scopeSpans = new[]
            {
                new
                {
                    scope = new { name = "OpenRPG Forge observability", version = "1" },
                    spans = group.Select(span => new
                    {
                        traceId = TraceId(span.Id),
                        spanId = SpanId(span.Id),
                        parentSpanId = span.ParentId is null ? string.Empty : SpanId(span.ParentId.Value),
                        name = span.Name,
                        kind = "SPAN_KIND_INTERNAL",
                        startTimeUnixNano = checked(span.StartedMicroseconds * 1_000L),
                        endTimeUnixNano = checked((span.StartedMicroseconds + span.DurationMicroseconds) * 1_000L),
                        attributes = span.Metadata.Select(item => new { key = item.Key, value = item.Value })
                            .Append(new { key = "openrpg.category", value = span.Category })
                            .OrderBy(item => item.key, StringComparer.Ordinal),
                        status = new { code = span.Failed ? "STATUS_CODE_ERROR" : "STATUS_CODE_OK" },
                    }),
                },
            },
        });
        var document = new
        {
            schemaVersion = 1,
            format = "open-telemetry-json-v1",
            requestedCaptureSeconds = options.RequestedCaptureSeconds,
            includeScripts = options.IncludeScripts,
            fingerprint,
            resourceSpans,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions()) + Environment.NewLine,
                new UTF8Encoding(false));
            File.Move(temporary, output, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return OpenTelemetryArtifactRelativePath;
    }

    private static string Fingerprint(
        Guid profileId,
        WorkspaceObservabilityCaptureOptions options,
        IReadOnlyList<QualityTraceSpan> spans)
    {
        var structural = new
        {
            profileId,
            options.Backend,
            options.RequestedCaptureSeconds,
            options.IncludeScripts,
            spans = spans.Select(span => new
            {
                span.Name,
                span.Surface,
                span.Category,
                span.Failed,
                metadata = span.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal),
            }),
        };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(structural, JsonOptions())));
    }

    private static string TraceId(Guid id)
    {
        var hash = SHA256.HashData(id.ToByteArray());
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    private static string SpanId(Guid id) => Convert.ToHexStringLower(id.ToByteArray().AsSpan(0, 8));

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
