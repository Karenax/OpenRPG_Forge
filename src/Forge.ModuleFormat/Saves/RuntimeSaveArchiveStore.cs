using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Gameplay;

namespace Forge.ModuleFormat.Saves;

public sealed record RuntimeModuleDependency(string ModuleId, string ContentSha256);

public sealed record RuntimeSaveArchiveManifest(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slot,
    long Revision,
    DateTimeOffset SavedAtUtc,
    string FormatVersion,
    string ModuleId,
    string ModuleContentSha256,
    string PayloadSha256,
    int PayloadBytes,
    IReadOnlyList<string> ChunkSha256,
    IReadOnlyList<RuntimeModuleDependency> Dependencies);

public sealed record RuntimeSaveArchiveResult(
    bool Succeeded,
    RuntimeSaveDocument? Save,
    RuntimeSaveArchiveManifest? Manifest,
    IReadOnlyList<RuntimeSaveDiagnostic> Diagnostics);

public static class RuntimeSaveArchiveStore
{
    public const string PolicyInvalidCode = "save.archive.policy_invalid";
    public const string SlotInvalidCode = "save.archive.slot_invalid";
    public const string ManifestMissingCode = "save.archive.manifest_missing";
    public const string ManifestInvalidCode = "save.archive.manifest_invalid";
    public const string ChunkMissingCode = "save.archive.chunk_missing";
    public const string ChecksumMismatchCode = "save.archive.checksum_mismatch";
    public const string DependencyMismatchCode = "save.archive.dependency_mismatch";
    public const string WriteFailedCode = "save.archive.write_failed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
    };

    public static RuntimeSaveArchiveResult Save(string root, string slot, long revision,
        RuntimeSaveDocument save, RuntimeSavePolicy policy, IReadOnlyList<RuntimeModuleDependency> dependencies,
        DateTimeOffset savedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(dependencies);
        if (!ValidPolicy(policy)) return Failure(PolicyInvalidCode, "Archive policy is invalid.", "policy");
        if (!ValidSlot(slot) || revision < 1) return Failure(SlotInvalidCode, "Slot and revision must be safe and positive.", "slot");
        var diagnostics = RuntimeSaveValidator.Validate(save);
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);
        if (!ValidDependencies(dependencies))
            return Failure(DependencyMismatchCode, "Module dependencies require unique UUIDs and SHA-256 hashes.", "dependencies");
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var chunkRoot = Path.Combine(fullRoot, "chunks");
            var slotRoot = Path.Combine(fullRoot, "slots", slot);
            Directory.CreateDirectory(chunkRoot);
            Directory.CreateDirectory(slotRoot);
            var payload = JsonSerializer.SerializeToUtf8Bytes(save with { SavedAtUtc = savedAtUtc }, CompactJsonOptions);
            var payloadHash = Hash(payload);
            var chunks = Chunk(payload, policy.ChunkBytes).ToArray();
            var chunkHashes = new List<string>(chunks.Length);
            foreach (var chunk in chunks)
            {
                var hash = Hash(chunk);
                chunkHashes.Add(hash);
                var path = Path.Combine(chunkRoot, $"{hash}.bin");
                if (!File.Exists(path)) WriteAtomic(path, chunk);
                else if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(path)), Convert.FromHexString(hash)))
                    return Failure(ChecksumMismatchCode, $"Existing chunk '{hash}' is corrupt.", path);
            }
            var manifest = new RuntimeSaveArchiveManifest(Guid.NewGuid(), 1, policy.Provenance.Trim(), slot,
                revision, savedAtUtc, save.FormatVersion, save.ModuleId, save.ModuleContentSha256, payloadHash,
                payload.Length, chunkHashes, dependencies.OrderBy(item => item.ModuleId, StringComparer.Ordinal).ToArray());
            var manifestPath = ManifestPath(slotRoot, revision);
            WriteAtomic(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
            Retain(slotRoot, policy.RollbackSlots + 1);
            return new(true, save with { SavedAtUtc = savedAtUtc }, manifest, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            return Failure(WriteFailedCode, exception.Message, root);
        }
    }

    public static RuntimeSaveArchiveResult Load(string root, string slot, int rollbackSteps,
        string expectedModuleId, string expectedContentSha256,
        IReadOnlyList<RuntimeModuleDependency> availableDependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentSha256);
        ArgumentNullException.ThrowIfNull(availableDependencies);
        if (!ValidSlot(slot) || rollbackSteps < 0)
            return Failure(SlotInvalidCode, "Slot and rollback offset are invalid.", "slot");
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var slotRoot = Path.Combine(fullRoot, "slots", slot);
            var paths = Directory.Exists(slotRoot)
                ? Directory.GetFiles(slotRoot, "*.orpsnapshot").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray()
                : [];
            if (rollbackSteps >= paths.Length)
                return Failure(ManifestMissingCode, "No save manifest exists for this slot and rollback offset.", slotRoot);
            RuntimeSaveArchiveManifest? manifest;
            try { manifest = JsonSerializer.Deserialize<RuntimeSaveArchiveManifest>(File.ReadAllText(paths[rollbackSteps]), JsonOptions); }
            catch (JsonException exception) { return Failure(ManifestInvalidCode, exception.Message, paths[rollbackSteps]); }
            if (manifest is null || manifest.Id == Guid.Empty || manifest.SchemaVersion != 1 ||
                !string.Equals(manifest.Slot, slot, StringComparison.Ordinal) || manifest.ChunkSha256.Count == 0 ||
                manifest.PayloadBytes < 1 || !HashValue(manifest.PayloadSha256))
                return Failure(ManifestInvalidCode, "Save manifest identity or payload metadata is invalid.", paths[rollbackSteps]);
            if (!string.Equals(manifest.ModuleId, expectedModuleId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.ModuleContentSha256, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
                return Failure(RuntimeSaveStore.ContentMismatchCode, "Save module identity or content fingerprint differs.", paths[rollbackSteps]);
            if (!DependenciesAvailable(manifest.Dependencies, availableDependencies))
                return Failure(DependencyMismatchCode, "A required module dependency is unavailable or has another hash.", "dependencies");
            using var payload = new MemoryStream(manifest.PayloadBytes);
            foreach (var hash in manifest.ChunkSha256)
            {
                if (!HashValue(hash)) return Failure(ManifestInvalidCode, "Manifest contains an invalid chunk hash.", paths[rollbackSteps]);
                var chunkPath = Path.Combine(fullRoot, "chunks", $"{hash}.bin");
                if (!File.Exists(chunkPath)) return Failure(ChunkMissingCode, $"Chunk '{hash}' is missing.", chunkPath);
                var bytes = File.ReadAllBytes(chunkPath);
                if (!string.Equals(Hash(bytes), hash, StringComparison.Ordinal))
                    return Failure(ChecksumMismatchCode, $"Chunk '{hash}' failed SHA-256 verification.", chunkPath);
                payload.Write(bytes);
            }
            var bytesAll = payload.ToArray();
            if (bytesAll.Length != manifest.PayloadBytes ||
                !string.Equals(Hash(bytesAll), manifest.PayloadSha256, StringComparison.Ordinal))
                return Failure(ChecksumMismatchCode, "Reconstructed save payload failed SHA-256 verification.", paths[rollbackSteps]);
            var save = JsonSerializer.Deserialize<RuntimeSaveDocument>(bytesAll, CompactJsonOptions);
            if (save is null) return Failure(ManifestInvalidCode, "Save payload is empty.", paths[rollbackSteps]);
            save = RuntimeSaveStore.Migrate(save);
            var diagnostics = RuntimeSaveValidator.Validate(save);
            return diagnostics.Count == 0 ? new(true, save, manifest, []) : new(false, null, manifest, diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            return Failure(ManifestInvalidCode, exception.Message, root);
        }
    }

    public static bool ShouldAutosave(RuntimeSavePolicy policy, DateTimeOffset? previous,
        DateTimeOffset now) => policy.AutosaveIntervalSeconds > 0 &&
        (previous is null || now - previous.Value >= TimeSpan.FromSeconds(policy.AutosaveIntervalSeconds));

    private static bool ValidPolicy(RuntimeSavePolicy policy) => policy.Id != Guid.Empty && policy.SchemaVersion == 1 &&
        !string.IsNullOrWhiteSpace(policy.Provenance) && policy.ChunkBytes is >= 4096 and <= 4_194_304 &&
        policy.AutosaveIntervalSeconds is >= 0 and <= 86_400 && policy.RollbackSlots is >= 0 and <= 100 &&
        string.Equals(policy.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase);
    private static bool ValidSlot(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    private static bool ValidDependencies(IReadOnlyList<RuntimeModuleDependency> dependencies) =>
        dependencies.All(item => Guid.TryParse(item.ModuleId, out var id) && id != Guid.Empty && HashValue(item.ContentSha256)) &&
        dependencies.Select(item => item.ModuleId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == dependencies.Count;
    private static bool DependenciesAvailable(IReadOnlyList<RuntimeModuleDependency> required,
        IReadOnlyList<RuntimeModuleDependency> available) => required.All(item => available.Any(candidate =>
        string.Equals(candidate.ModuleId, item.ModuleId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(candidate.ContentSha256, item.ContentSha256, StringComparison.OrdinalIgnoreCase)));
    private static bool HashValue(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static IEnumerable<byte[]> Chunk(byte[] bytes, int size)
    {
        for (var offset = 0; offset < bytes.Length; offset += size)
            yield return bytes.AsSpan(offset, Math.Min(size, bytes.Length - offset)).ToArray();
    }
    private static string ManifestPath(string slotRoot, long revision) =>
        Path.Combine(slotRoot, $"{revision:D20}.orpsnapshot");
    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Retain(string slotRoot, int count)
    {
        foreach (var stale in Directory.GetFiles(slotRoot, "*.orpsnapshot")
                     .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(count))
            File.Delete(stale);
    }
    private static RuntimeSaveArchiveResult Failure(string code, string message, string path) =>
        new(false, null, null, [new(code, message, path)]);
}
