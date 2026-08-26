using System.Security.Cryptography;
using Forge.Application.Areas;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Audio;

public sealed record GovernedAudioSourceMetadata(
    string ResourcePath,
    string LicenseSpdx,
    string ContentSha256,
    long MemoryBytes,
    string MimeType,
    string PackageManifest);

public sealed record GovernedAudioSourceValidationResult(
    GovernedAudioSourceMetadata? Source,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Source is not null && Diagnostics.Count == 0;
}

public static class GovernedAudioSourceValidator
{
    public const string PathUnsafeCode = "audio.source.path_unsafe";
    public const string ResourceMissingCode = "audio.source.missing";
    public const string LicenseInvalidCode = "audio.source.license_invalid";
    public const string HashMismatchCode = "audio.source.hash_mismatch";
    public const string SizeMismatchCode = "audio.source.size_mismatch";
    public const string MimeInvalidCode = "audio.source.mime_invalid";
    public const string ManifestInvalidCode = "audio.source.manifest_invalid";
    public const string BuiltinUnsupportedCode = "audio.source.builtin_unsupported";

    public static GovernedAudioSourceValidationResult Validate(
        string workspaceRoot,
        GovernedAudioSourceMetadata source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<DomainDiagnostic>();
        if (!ValidSpdx(source.LicenseSpdx))
            Add(LicenseInvalidCode, "Audio source requires a valid SPDX license.", "licenseSpdx");
        if (!source.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            Add(MimeInvalidCode, "Audio source MIME type must start with 'audio/'.", "mimeType");
        if (string.IsNullOrWhiteSpace(source.PackageManifest) ||
            !SafeReference(source.PackageManifest))
            Add(ManifestInvalidCode, "Audio source requires a safe package-manifest reference.", "packageManifest");

        if (source.ResourcePath.StartsWith("builtin://", StringComparison.Ordinal))
        {
            if (!ProceduralAmbientAudioService.SupportedResources.Contains(source.ResourcePath, StringComparer.Ordinal))
            {
                Add(BuiltinUnsupportedCode,
                    "New audio authoring accepts only versioned built-in procedural resources.", "resourcePath");
                return new(null, diagnostics);
            }
            var generated = ProceduralAmbientAudioService.Create(source.ResourcePath);
            Verify(source, generated.Pcm16Mono.LongLength, generated.ContentSha256, diagnostics);
            if (!string.Equals(source.PackageManifest, ProceduralAmbientAudioService.PackageManifest,
                    StringComparison.Ordinal) ||
                !string.Equals(source.LicenseSpdx, ProceduralAmbientAudioService.LicenseSpdx,
                    StringComparison.Ordinal))
                Add(ManifestInvalidCode,
                    "Built-in audio must retain its versioned catalog and license receipt.", "packageManifest");
            return diagnostics.Count == 0 ? new(source, []) : new(null, diagnostics);
        }

        if (!SafeRelativePath(source.ResourcePath))
        {
            Add(PathUnsafeCode, "Audio source must be a relative path contained in the workspace.", "resourcePath");
            return new(null, diagnostics);
        }
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(root,
            source.ResourcePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            Add(PathUnsafeCode, "Audio source escapes the workspace.", "resourcePath");
            return new(null, diagnostics);
        }
        if (!File.Exists(absolute))
        {
            Add(ResourceMissingCode, "Audio source does not exist in the workspace.", "resourcePath");
            return new(null, diagnostics);
        }
        var bytes = File.ReadAllBytes(absolute);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Verify(source, bytes.LongLength, hash, diagnostics);
        return diagnostics.Count == 0 ? new(source, []) : new(null, diagnostics);

        void Add(string code, string message, string path) =>
            diagnostics.Add(new DomainDiagnostic(code, message, path));
    }

    private static void Verify(GovernedAudioSourceMetadata source, long actualBytes, string actualHash,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (source.MemoryBytes != actualBytes)
            diagnostics.Add(new(SizeMismatchCode,
                $"Audio source size is {actualBytes} bytes, not {source.MemoryBytes}.", "memoryBytes"));
        if (source.ContentSha256 is not { Length: 64 } ||
            !string.Equals(source.ContentSha256, actualHash, StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new(HashMismatchCode, "Audio source SHA-256 does not match its bytes.",
                "contentSha256"));
    }

    private static bool SafeRelativePath(string value) => !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("://", StringComparison.Ordinal) && !Path.IsPathRooted(value) &&
        !value.Replace('\\', '/').Split('/').Any(segment => segment is ".." or "");

    private static bool SafeReference(string value) =>
        value.StartsWith("builtin://openrpg/audio/v1/", StringComparison.Ordinal) || SafeRelativePath(value);

    private static bool ValidSpdx(string value) => !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "UNLICENSED", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '.' or '+' or '(' or ')' or ' ');
}
