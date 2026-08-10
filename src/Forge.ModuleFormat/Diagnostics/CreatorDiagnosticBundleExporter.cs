using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Creator;

namespace Forge.ModuleFormat.Diagnostics;

public sealed record CreatorDiagnosticBundleResult(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string RelativePath,
    string Sha256,
    int DiagnosticCount,
    bool IncludesRuntimeLog,
    bool Reused)
{
    public const int CurrentSchemaVersion = 1;
}

public static partial class CreatorDiagnosticBundleExporter
{
    public const string DefaultRelativeDirectory = ".forge/diagnostics";
    public const string UnsafePathCode = "creator.diagnostics.export_path_unsafe";

    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static CreatorDiagnosticBundleResult Export(
        string workspaceRoot,
        IReadOnlyList<CreatorDiagnostic> diagnostics,
        string? runtimeLog,
        string relativeDirectory = DefaultRelativeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!IsSafeRelativePath(relativeDirectory))
            throw new ArgumentException(UnsafePathCode, nameof(relativeDirectory));

        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var sanitizedLog = Sanitize(runtimeLog);
        var ordered = diagnostics
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var diagnosticsJson = Serialize(ordered.Select(item => new
        {
            id = item.Id,
            schemaVersion = item.SchemaVersion,
            provenance = item.Provenance,
            severity = item.Severity.ToString().ToLowerInvariant(),
            category = item.Category.ToString().ToLowerInvariant(),
            item.Code,
            item.Message,
            item.DocumentPath,
            item.SubjectUuid,
            item.Property,
            item.Source,
            item.CorrelationId,
            target = item.Target is null ? null : new
            {
                kind = item.Target.Kind.ToString().ToLowerInvariant(),
                item.Target.Id,
                item.Target.DisplayName,
                item.Target.DocumentPath,
            },
        }));
        var logBytes = Encoding.UTF8.GetBytes(sanitizedLog);
        var diagnosticBytes = Encoding.UTF8.GetBytes(diagnosticsJson);
        var identityBytes = SHA256.HashData(Combine(diagnosticBytes, logBytes));
        var identity = StableGuid(identityBytes);
        var provenance = "openrpg-forge:creator-diagnostic-bundle";
        var manifestJson = Serialize(new
        {
            id = identity,
            schemaVersion = CreatorDiagnosticBundleResult.CurrentSchemaVersion,
            provenance,
            diagnosticCount = ordered.Length,
            includesRuntimeLog = logBytes.Length > 0,
            entries = new[]
            {
                Entry("diagnostics.json", diagnosticBytes),
                Entry("runtime.log", logBytes),
            },
        });
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        var fileName = $"diagnostics-{identity:N}.zip";
        var destination = Path.Combine(directory, fileName);
        var temporary = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                Write(archive, "manifest.json", manifestBytes);
                Write(archive, "diagnostics.json", diagnosticBytes);
                if (logBytes.Length > 0) Write(archive, "runtime.log", logBytes);
            }

            var sha256 = HashFile(temporary);
            var reused = File.Exists(destination) && string.Equals(HashFile(destination), sha256, StringComparison.Ordinal);
            if (reused) File.Delete(temporary);
            else File.Move(temporary, destination, true);
            return new(identity, CreatorDiagnosticBundleResult.CurrentSchemaVersion, provenance,
                Path.GetRelativePath(root, destination).Replace('\\', '/'), sha256, ordered.Length,
                logBytes.Length > 0, reused);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string Sanitize(string? runtimeLog)
    {
        if (string.IsNullOrWhiteSpace(runtimeLog)) return string.Empty;
        var normalized = runtimeLog.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        normalized = SecretPattern().Replace(normalized, match => $"{match.Groups[1].Value}=[REDACTED]");
        return PlayerPattern().Replace(normalized, match => $"{match.Groups[1].Value}=[PLAYER_REDACTED]") + "\n";
    }

    private static object Entry(string path, byte[] content) => new
    {
        path,
        bytes = content.Length,
        sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
    };

    private static void Write(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    }) + "\n";

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length + 1];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        result[first.Length] = 0;
        Buffer.BlockCopy(second, 0, result, first.Length + 1, second.Length);
        return result;
    }

    private static Guid StableGuid(byte[] hash)
    {
        var bytes = hash[..16].ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool IsSafeRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");

    [GeneratedRegex("(?im)\\b(password|secret|token|authorization|cookie)\\s*[:=]\\s*[^\\s]+")]
    private static partial Regex SecretPattern();

    [GeneratedRegex("(?im)\\b(player(?:id)?|account(?:id)?|user(?:id)?)\\s*[:=]\\s*[^\\s]+")]
    private static partial Regex PlayerPattern();
}
