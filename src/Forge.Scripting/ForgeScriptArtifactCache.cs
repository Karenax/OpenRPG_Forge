using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Forge.Scripting;

public sealed class ForgeScriptArtifactCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    private readonly IForgeScriptRuntime _runtime;
    private readonly string _cacheRoot;

    public ForgeScriptArtifactCache(IForgeScriptRuntime runtime, string cacheRoot)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        _runtime = runtime;
        _cacheRoot = Path.GetFullPath(cacheRoot);
    }

    public ForgeScriptCacheResult Compile(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var sourceHash = Sha256(Encoding.UTF8.GetBytes(source));
        var cacheKey = ComputeKey(sourceHash, _runtime.ApiVersion, _runtime.VirtualMachineVersion);
        if (TryRead(cacheKey, source, sourceHash, out var cachedArtifact, out var cachedMetadata))
        {
            return new ForgeScriptCacheResult(
                new ForgeScriptCompileResult(true, cachedArtifact, []),
                true,
                cacheKey,
                cachedMetadata);
        }

        var compilation = _runtime.Compile(source);
        if (!compilation.Success || compilation.Artifact is null)
        {
            return new ForgeScriptCacheResult(compilation, false, cacheKey, null);
        }

        var metadata = new ForgeScriptCacheMetadata(
            StableId(cacheKey),
            ForgeScriptCacheMetadata.CurrentSchemaVersion,
            cacheKey,
            sourceHash,
            Sha256(compilation.Artifact.Bytecode),
            _runtime.ApiVersion,
            _runtime.VirtualMachineVersion,
            "forgescript-cache");
        Write(cacheKey, compilation.Artifact.Bytecode, metadata);
        return new ForgeScriptCacheResult(compilation, false, cacheKey, metadata);
    }

    public static string ComputeKey(string sourceSha256, string apiVersion, string compilerVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerVersion);
        return Sha256(Encoding.UTF8.GetBytes($"{sourceSha256}\n{apiVersion}\n{compilerVersion}"));
    }

    private bool TryRead(
        string cacheKey,
        string source,
        string sourceHash,
        out ForgeScriptArtifact? artifact,
        out ForgeScriptCacheMetadata? metadata)
    {
        artifact = null;
        metadata = null;
        var metadataPath = MetadataPath(cacheKey);
        var bytecodePath = BytecodePath(cacheKey);
        if (!File.Exists(metadataPath) || !File.Exists(bytecodePath))
        {
            return false;
        }

        try
        {
            var candidate = JsonSerializer.Deserialize<ForgeScriptCacheMetadata>(
                File.ReadAllText(metadataPath),
                JsonOptions);
            var bytecode = File.ReadAllBytes(bytecodePath);
            if (candidate is null ||
                candidate.SchemaVersion != ForgeScriptCacheMetadata.CurrentSchemaVersion ||
                !string.Equals(candidate.Id, StableId(cacheKey), StringComparison.Ordinal) ||
                !string.Equals(candidate.CacheKey, cacheKey, StringComparison.Ordinal) ||
                !string.Equals(candidate.SourceSha256, sourceHash, StringComparison.Ordinal) ||
                !string.Equals(candidate.BytecodeSha256, Sha256(bytecode), StringComparison.Ordinal) ||
                !string.Equals(candidate.ApiVersion, _runtime.ApiVersion, StringComparison.Ordinal) ||
                !string.Equals(candidate.CompilerVersion, _runtime.VirtualMachineVersion, StringComparison.Ordinal) ||
                !string.Equals(candidate.Provenance, "forgescript-cache", StringComparison.Ordinal))
            {
                return false;
            }

            artifact = new ForgeScriptArtifact(
                source,
                bytecode,
                candidate.CompilerVersion,
                candidate.ApiVersion);
            metadata = candidate;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private void Write(string cacheKey, byte[] bytecode, ForgeScriptCacheMetadata metadata)
    {
        Directory.CreateDirectory(_cacheRoot);
        WriteAtomically(BytecodePath(cacheKey), bytecode);
        WriteAtomically(
            MetadataPath(cacheKey),
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata, JsonOptions)));
    }

    private string MetadataPath(string cacheKey) => Path.Combine(_cacheRoot, $"{cacheKey}.json");

    private string BytecodePath(string cacheKey) => Path.Combine(_cacheRoot, $"{cacheKey}.bin");

    private static void WriteAtomically(string destination, byte[] content)
    {
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string StableId(string cacheKey)
    {
        var bytes = Convert.FromHexString(cacheKey)[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes).ToString("D");
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
