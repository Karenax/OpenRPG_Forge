using System.Text;
using System.Text.Json;
using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Workspaces;

public sealed record RecoverySessionMarker(
    Guid Id,
    int SchemaVersion,
    Guid ProjectId,
    DateTimeOffset StartedAt,
    int ProcessId,
    bool CleanShutdown,
    DateTimeOffset? ClosedAt,
    string Provenance);

public sealed record RecoverySessionInspection(
    RecoverySessionMarker? Previous,
    RecoverySessionMarker Current,
    bool PreviousSessionWasUnclean,
    string Code,
    string Detail);

public sealed class RecoverySessionStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "session.json";

    public RecoverySessionInspection Begin(string workspaceRoot, ProjectId projectId, DateTimeOffset startedAt,
        int processId, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));

        var previous = TryRead(workspaceRoot, out var readCode, out var readDetail);
        var interrupted = readCode == "recovery.session.marker_corrupt" ||
                          previous is { CleanShutdown: false } && previous.ProjectId == projectId.Value;
        var current = new RecoverySessionMarker(Guid.NewGuid(), CurrentSchemaVersion, projectId.Value,
            startedAt.ToUniversalTime(), processId, false, null, provenance.Trim());
        Write(workspaceRoot, current);
        var code = interrupted ? "recovery.session.interrupted" : readCode;
        var detail = interrupted
            ? "The previous editor session did not record a clean shutdown."
            : readDetail;
        return new RecoverySessionInspection(previous, current, interrupted, code, detail);
    }

    public RecoverySessionMarker? Read(string workspaceRoot) =>
        TryRead(workspaceRoot, out _, out _);

    public bool End(string workspaceRoot, Guid sessionId, DateTimeOffset closedAt)
    {
        if (sessionId == Guid.Empty) return false;
        var current = TryRead(workspaceRoot, out _, out _);
        if (current is null || current.Id != sessionId) return false;
        Write(workspaceRoot, current with
        {
            CleanShutdown = true,
            ClosedAt = closedAt.ToUniversalTime(),
        });
        return true;
    }

    private static RecoverySessionMarker? TryRead(string workspaceRoot, out string code, out string detail)
    {
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
        {
            code = "recovery.session.first_open";
            detail = "No previous editor session marker exists.";
            return null;
        }
        try
        {
            var marker = JsonSerializer.Deserialize<RecoverySessionMarker>(File.ReadAllText(path), Options)
                         ?? throw new JsonException("Recovery session marker is empty.");
            if (marker.Id == Guid.Empty || marker.SchemaVersion != CurrentSchemaVersion ||
                marker.ProjectId == Guid.Empty || marker.ProcessId <= 0 || string.IsNullOrWhiteSpace(marker.Provenance))
                throw new JsonException("Recovery session marker fields are invalid.");
            code = marker.CleanShutdown ? "recovery.session.clean" : "recovery.session.open";
            detail = marker.CleanShutdown
                ? "The previous editor session ended cleanly."
                : "The previous editor session is still marked as open.";
            return marker;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            code = "recovery.session.marker_corrupt";
            detail = exception.Message;
            return null;
        }
    }

    private static void Write(string workspaceRoot, RecoverySessionMarker marker)
    {
        var path = ResolvePath(workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(marker, Options), new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), RecoverySnapshotStore.RelativeDirectory, FileName);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
