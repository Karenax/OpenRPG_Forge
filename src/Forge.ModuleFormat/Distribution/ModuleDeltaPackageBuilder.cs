using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Packages;

namespace Forge.ModuleFormat.Distribution;

public static class ModuleDeltaPackageBuilder
{
    public const string CurrentFormatVersion = "1.0.0";
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static ModuleDeltaBuildResult Create(
        string basePackagePath,
        string targetPackagePath,
        string destinationPath,
        ModuleDeltaConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException("Delta updates are disabled by the module release configuration.");
        }

        var basePackage = RequireFile(basePackagePath, "Base package");
        var targetPackage = RequireFile(targetPackagePath, "Target package");
        var limits = new ModulePackageLimits();
        if (new FileInfo(basePackage).Length > limits.MaximumTotalBytes ||
            new FileInfo(targetPackage).Length > limits.MaximumTotalBytes)
        {
            throw new InvalidDataException("Delta source packages exceed the module package size limit.");
        }
        var destination = Path.GetFullPath(destinationPath);
        if (!string.Equals(Path.GetExtension(destination), ".orpdelta", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Delta destination must use the .orpdelta extension.", nameof(destinationPath));
        }

        var baseBytes = File.ReadAllBytes(basePackage);
        var targetBytes = File.ReadAllBytes(targetPackage);
        var baseChunks = ReadChunks(baseBytes, configuration.ChunkSizeBytes)
            .GroupBy(chunk => chunk.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Content, StringComparer.Ordinal);
        var targetChunks = ReadChunks(targetBytes, configuration.ChunkSizeBytes).ToArray();
        var includedHashes = new HashSet<string>(StringComparer.Ordinal);
        var chunks = targetChunks.Select(chunk =>
        {
            var included = !configuration.DeduplicateByHash || !baseChunks.ContainsKey(chunk.Sha256);
            if (included)
            {
                includedHashes.Add(chunk.Sha256);
            }

            return new ModuleDeltaChunk(chunk.Index, chunk.Sha256, chunk.Content.Length, included);
        }).ToArray();
        var manifest = new ModuleDeltaManifest(
            CurrentFormatVersion,
            Sha256(baseBytes),
            Sha256(targetBytes),
            targetBytes.LongLength,
            configuration.ChunkSizeBytes,
            configuration.AllowResume,
            configuration.DeduplicateByHash,
            configuration.AllowRollback,
            configuration.RetainedVersions,
            chunks);
        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Delta destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporary = Path.Combine(destinationDirectory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                WriteEntry(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, Options) + Environment.NewLine));
                foreach (var chunk in targetChunks
                             .Where(chunk => includedHashes.Contains(chunk.Sha256))
                             .GroupBy(chunk => chunk.Sha256, StringComparer.Ordinal)
                             .Select(group => group.First())
                             .OrderBy(chunk => chunk.Sha256, StringComparer.Ordinal))
                {
                    WriteEntry(archive, $"chunks/{chunk.Sha256}.bin", chunk.Content);
                }
            }

            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return new ModuleDeltaBuildResult(
            destination,
            manifest,
            chunks.Count(chunk => chunk.Included),
            chunks.Count(chunk => !chunk.Included),
            targetChunks.Where(chunk => includedHashes.Contains(chunk.Sha256))
                .GroupBy(chunk => chunk.Sha256, StringComparer.Ordinal)
                .Sum(group => (long)group.First().Content.Length));
    }

    public static ModuleDeltaManifest Apply(
        string basePackagePath,
        string deltaPath,
        string destinationPath,
        string? resumeCachePath = null)
    {
        var basePackage = RequireFile(basePackagePath, "Base package");
        var delta = RequireFile(deltaPath, "Delta package");
        var destination = Path.GetFullPath(destinationPath);
        var baseBytes = File.ReadAllBytes(basePackage);
        using var archive = ZipFile.OpenRead(delta);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Delta package does not contain manifest.json.");
        ModuleDeltaManifest manifest;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true, leaveOpen: false))
        {
            manifest = JsonSerializer.Deserialize<ModuleDeltaManifest>(reader.ReadToEnd(), Options)
                ?? throw new InvalidDataException("Delta manifest is empty.");
        }

        ValidateManifest(manifest);
        if (!string.Equals(Sha256(baseBytes), manifest.BasePackageSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Base package SHA-256 does not match the delta manifest.");
        }

        var baseChunks = ReadChunks(baseBytes, manifest.ChunkSizeBytes)
            .GroupBy(chunk => chunk.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Content, StringComparer.Ordinal);
        var cacheRoot = string.IsNullOrWhiteSpace(resumeCachePath) ? null : Path.GetFullPath(resumeCachePath);
        if (cacheRoot is not null && !manifest.AllowResume)
        {
            throw new InvalidOperationException("This delta manifest does not allow resume caches.");
        }

        if (cacheRoot is not null)
        {
            Directory.CreateDirectory(cacheRoot);
        }

        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Reconstructed package destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporary = Path.Combine(destinationDirectory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = File.Create(temporary))
            {
                foreach (var chunk in manifest.Chunks.OrderBy(chunk => chunk.Index))
                {
                    var content = ResolveChunk(archive, chunk, baseChunks, cacheRoot);
                    output.Write(content);
                }
            }

            var rebuilt = File.ReadAllBytes(temporary);
            if (rebuilt.LongLength != manifest.TargetBytes ||
                !string.Equals(Sha256(rebuilt), manifest.TargetPackageSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Reconstructed package does not match the target hash and length.");
            }

            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return manifest;
    }

    public static void Rollback(string verifiedBasePackagePath, ModuleDeltaManifest manifest, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!manifest.AllowRollback)
        {
            throw new InvalidOperationException("This delta manifest does not allow rollback.");
        }

        var basePackage = RequireFile(verifiedBasePackagePath, "Rollback package");
        var bytes = File.ReadAllBytes(basePackage);
        if (!string.Equals(Sha256(bytes), manifest.BasePackageSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Rollback package does not match the retained base hash.");
        }

        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Rollback destination has no parent directory."));
        File.Copy(basePackage, destination, true);
    }

    private static byte[] ResolveChunk(
        ZipArchive archive,
        ModuleDeltaChunk chunk,
        IReadOnlyDictionary<string, byte[]> baseChunks,
        string? cacheRoot)
    {
        byte[]? content = null;
        var cachePath = cacheRoot is null ? null : Path.Combine(cacheRoot, $"{chunk.Sha256}.bin");
        if (cachePath is not null && File.Exists(cachePath))
        {
            content = File.ReadAllBytes(cachePath);
        }

        if (content is null && baseChunks.TryGetValue(chunk.Sha256, out var baseContent))
        {
            content = baseContent;
        }

        if (content is null)
        {
            var entry = archive.GetEntry($"chunks/{chunk.Sha256}.bin")
                ?? throw new InvalidDataException($"Delta chunk '{chunk.Sha256}' is missing.");
            if (entry.Length != chunk.Length || entry.Length > ModuleDeltaConfiguration.MaximumChunkSizeBytes)
            {
                throw new InvalidDataException($"Delta chunk '{chunk.Sha256}' declares an invalid length.");
            }
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            content = buffer.ToArray();
        }

        if (content.Length != chunk.Length || !string.Equals(Sha256(content), chunk.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Delta chunk '{chunk.Sha256}' failed integrity validation.");
        }

        if (cachePath is not null && !File.Exists(cachePath))
        {
            File.WriteAllBytes(cachePath, content);
        }

        return content;
    }

    private static IReadOnlyList<(int Index, string Sha256, byte[] Content)> ReadChunks(byte[] content, int chunkSize)
    {
        var chunks = new List<(int Index, string Sha256, byte[] Content)>();
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, content.Length - offset);
            var chunk = content.AsSpan(offset, length).ToArray();
            chunks.Add((chunks.Count, Sha256(chunk), chunk));
        }

        if (content.Length == 0)
        {
            chunks.Add((0, Sha256([]), []));
        }

        return chunks;
    }

    private static void ValidateManifest(ModuleDeltaManifest manifest)
    {
        if (!string.Equals(manifest.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal) ||
            manifest.ChunkSizeBytes < ModuleDeltaConfiguration.MinimumChunkSizeBytes ||
            manifest.ChunkSizeBytes > ModuleDeltaConfiguration.MaximumChunkSizeBytes ||
            (manifest.ChunkSizeBytes & (manifest.ChunkSizeBytes - 1)) != 0 ||
            manifest.TargetBytes < 0 || manifest.TargetBytes > new ModulePackageLimits().MaximumTotalBytes ||
            manifest.Chunks.Count is 0 or > 8_192 ||
            manifest.Chunks.Select(chunk => chunk.Index).Distinct().Count() != manifest.Chunks.Count ||
            manifest.Chunks.OrderBy(chunk => chunk.Index).Select(chunk => chunk.Index)
                .Where((index, position) => index != position).Any() ||
            manifest.Chunks.Sum(chunk => (long)chunk.Length) != manifest.TargetBytes ||
            manifest.Chunks.Any(chunk => chunk.Length < 0 || chunk.Sha256.Length != 64 || !chunk.Sha256.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("Delta manifest is malformed or unsupported.");
        }
    }

    private static string RequireFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{label} does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
