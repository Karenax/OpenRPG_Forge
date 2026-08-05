using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Domain.RuntimeExtensions;

namespace Forge.ModuleFormat.Diagnostics;

public interface ICrashDumpAdapter
{
    string Name { get; }

    byte[] Capture();
}

public sealed record CrashArtifactRequest(Guid ReproductionId, DateTimeOffset OccurredAt,
    string ApplicationVersion, string PackageFingerprint, IReadOnlyList<string> LogLines,
    string OutputDirectory);

public sealed record CrashArtifactResult(string ArtifactPath, string Sha256, long Bytes,
    Guid ReproductionId, int SanitizedLogLines, bool DumpIncluded, string DumpAdapter);

public sealed class CrashArtifactBuilder(CrashArtifactPolicy policy, ICrashDumpAdapter? dumpAdapter = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public CrashArtifactResult Capture(CrashArtifactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReproductionId == Guid.Empty || string.IsNullOrWhiteSpace(request.ApplicationVersion) ||
            request.PackageFingerprint.Length != 64 || request.PackageFingerprint.Any(character => !Uri.IsHexDigit(character)) ||
            !Path.IsPathFullyQualified(request.OutputDirectory))
            throw new ArgumentException("Crash artifacts require reproduction identity, version, package SHA-256 and an absolute output directory.");
        ArgumentNullException.ThrowIfNull(request.LogLines);

        var lines = policy.CaptureSanitizedLogs
            ? request.LogLines.Take(policy.MaxLogLines).Select(Sanitize).ToArray()
            : [];
        var dump = policy.DumpAdapter == CrashDumpAdapterKind.None || dumpAdapter is null
            ? null : dumpAdapter.Capture();
        if (dump?.LongLength >= policy.MaxArtifactBytes)
            throw new InvalidDataException("The crash dump exceeds the artifact byte quota.");

        var manifest = new CrashArtifactManifest(
            1,
            policy.Id,
            request.ReproductionId,
            request.OccurredAt.ToUniversalTime(),
            policy.IncludeVersion ? request.ApplicationVersion.Trim() : null,
            policy.IncludePackageFingerprint ? request.PackageFingerprint.ToLowerInvariant() : null,
            policy.IncludeReproductionId,
            lines.Length,
            dump is not null,
            dump is null ? "none" : dumpAdapter!.Name,
            policy.Provenance);
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);

        Directory.CreateDirectory(request.OutputDirectory);
        var fileName = $"openrpg-forge-crash-{request.ReproductionId:D}.zip";
        var destination = Path.Combine(Path.GetFullPath(request.OutputDirectory), fileName);
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    Write(archive, "manifest.json", Encoding.UTF8.GetBytes(manifestJson), request.OccurredAt);
                    if (lines.Length > 0)
                        Write(archive, "sanitized.log", Encoding.UTF8.GetBytes(string.Join('\n', lines)), request.OccurredAt);
                    if (dump is not null) Write(archive, "diagnostic.dump", dump, request.OccurredAt);
                }
                if (stream.Length > policy.MaxArtifactBytes)
                    throw new InvalidDataException("The crash artifact exceeds its byte quota.");
            }
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        var bytes = new FileInfo(destination).Length;
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(destination)));
        return new CrashArtifactResult(destination, hash, bytes, request.ReproductionId, lines.Length,
            dump is not null, dump is null ? "none" : dumpAdapter!.Name);
    }

    private string Sanitize(string line)
    {
        var result = line ?? string.Empty;
        foreach (var key in policy.SensitiveKeys)
        {
            var pattern = $"(?i)([\\\"']?{Regex.Escape(key)}[\\\"']?\\s*[:=]\\s*)([\\\"'][^\\\"']*[\\\"']|[^,;\\s]+)";
            result = Regex.Replace(result, pattern, "$1[REDACTED]", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }
        return result.Length <= 8_192 ? result : result[..8_192];
    }

    private static void Write(ZipArchive archive, string path, byte[] content, DateTimeOffset occurredAt)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.SmallestSize);
        var utc = occurredAt.ToUniversalTime();
        entry.LastWriteTime = utc.Year < 1980 ? new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero) : utc;
        using var stream = entry.Open();
        stream.Write(content);
    }

    private sealed record CrashArtifactManifest(int SchemaVersion, Guid PolicyId, Guid ReproductionId,
        DateTimeOffset OccurredAt, string? ApplicationVersion, string? PackageFingerprint,
        bool IncludesReproductionId, int SanitizedLogLines, bool DumpIncluded, string DumpAdapter,
        string Provenance);
}
