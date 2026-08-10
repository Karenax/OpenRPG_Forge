using System.Text;
using System.Text.Json;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class AgentAiAuditStore
{
    public const string RelativePath = "assistant/ai-audit.json";
    public const string ReadFailedCode = "assistant.audit.read_failed";
    public const string WriteFailedCode = "assistant.audit.write_failed";
    private const string FormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    public static AgentAiAuditLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path)) return new([], true, []);
        try
        {
            var document = JsonSerializer.Deserialize<AgentAiAuditDocument>(File.ReadAllText(path), Options)
                ?? throw new JsonException("The AI audit document is empty.");
            if (!string.Equals(document.FormatVersion, FormatVersion, StringComparison.Ordinal))
                throw new NotSupportedException($"AI audit format '{document.FormatVersion}' is unsupported.");
            var entries = (document.Entries ?? []).OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
            if (entries.Any(item => item.Id == Guid.Empty || item.TransactionId == Guid.Empty || item.PlanId == Guid.Empty ||
                item.RollbackId == Guid.Empty || item.SchemaVersion != AgentAiAuditEntry.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.ClientId) || string.IsNullOrWhiteSpace(item.UserId) ||
                string.IsNullOrWhiteSpace(item.InputHash) || string.IsNullOrWhiteSpace(item.OutputHash) ||
                string.IsNullOrWhiteSpace(item.Provenance)))
                throw new JsonException("The AI audit contains an invalid entry.");
            return new(entries, false, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new([], false, [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, IReadOnlyList<AgentAiAuditEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(entries);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("The AI audit path has no parent directory.");
            Directory.CreateDirectory(directory);
            var ordered = entries.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
            var json = JsonSerializer.Serialize(new AgentAiAuditDocument(FormatVersion, ordered), Options);
            WriteAtomically(path, json);
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), "assistant", "ai-audit.json");

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record AgentAiAuditDocument(string FormatVersion, IReadOnlyList<AgentAiAuditEntry>? Entries);
}

public sealed record AgentAiAuditLoadResult(
    IReadOnlyList<AgentAiAuditEntry> Entries,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
