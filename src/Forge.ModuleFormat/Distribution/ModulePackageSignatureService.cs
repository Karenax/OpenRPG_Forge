using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Packages;

namespace Forge.ModuleFormat.Distribution;

public static class ModulePackageSignatureService
{
    public const string CurrentFormatVersion = "1.0.0";
    public const string Algorithm = "ECDSA-P256-SHA256";
    public const string InvalidFormatCode = "distribution.signature.format_invalid";
    public const string PackageHashMismatchCode = "distribution.signature.package_hash_mismatch";
    public const string SignatureInvalidCode = "distribution.signature.invalid";
    public const string KeyUntrustedCode = "distribution.signature.key_untrusted";
    public const string KeyRevokedCode = "distribution.signature.key_revoked";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static ModulePackageSignature Sign(
        string packagePath,
        string privateKeyPemPath,
        string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPemPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var package = RequireFile(packagePath, "Package", new ModulePackageLimits().MaximumTotalBytes);
        var keyPath = RequireFile(privateKeyPemPath, "Private signing key", 64 * 1024);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        key.ImportFromPem(File.ReadAllText(keyPath));
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var packageHash = SHA256.HashData(File.ReadAllBytes(package));
        var signature = key.SignHash(packageHash, DSASignatureFormat.Rfc3279DerSequence);
        var document = new ModulePackageSignature(
            CurrentFormatVersion,
            Algorithm,
            Convert.ToHexString(packageHash).ToLowerInvariant(),
            Sha256(publicKey),
            Convert.ToBase64String(publicKey),
            Convert.ToBase64String(signature));
        WriteAtomically(destinationPath, JsonSerializer.Serialize(document, Options) + Environment.NewLine);
        return document;
    }

    public static string GenerateKeyPair(string privateKeyPemPath, string publicKeyPemPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPemPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPemPath);
        var privateKeyPath = Path.GetFullPath(privateKeyPemPath);
        var publicKeyPath = Path.GetFullPath(publicKeyPemPath);
        if (File.Exists(privateKeyPath) || File.Exists(publicKeyPath))
        {
            throw new IOException("Signing key generation refuses to overwrite an existing key file.");
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        WriteAtomically(privateKeyPath, key.ExportPkcs8PrivateKeyPem() + Environment.NewLine);
        WriteAtomically(publicKeyPath, key.ExportSubjectPublicKeyInfoPem() + Environment.NewLine);
        return Sha256(key.ExportSubjectPublicKeyInfo());
    }

    public static ModulePackageSignatureVerification Verify(
        string packagePath,
        string signaturePath,
        ModuleSigningConfiguration policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(signaturePath);
        ArgumentNullException.ThrowIfNull(policy);
        var package = RequireFile(packagePath, "Package", new ModulePackageLimits().MaximumTotalBytes);
        var signatureFile = RequireFile(signaturePath, "Signature", 64 * 1024);
        ModulePackageSignature document;
        try
        {
            document = JsonSerializer.Deserialize<ModulePackageSignature>(File.ReadAllText(signatureFile), Options)
                ?? throw new JsonException("Signature document is empty.");
        }
        catch (JsonException exception)
        {
            return Failure(InvalidFormatCode, exception.Message);
        }

        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal) ||
            !string.Equals(document.Algorithm, Algorithm, StringComparison.Ordinal) ||
            !IsSha256(document.PackageSha256) ||
            !IsSha256(document.KeyFingerprint))
        {
            return Failure(InvalidFormatCode, "Signature metadata is unsupported or malformed.");
        }

        var packageHash = SHA256.HashData(File.ReadAllBytes(package));
        var packageHashText = Convert.ToHexString(packageHash).ToLowerInvariant();
        if (!string.Equals(packageHashText, document.PackageSha256, StringComparison.Ordinal))
        {
            return new ModulePackageSignatureVerification(
                false,
                false,
                false,
                packageHashText,
                document.KeyFingerprint,
                PackageHashMismatchCode,
                "Package content does not match the signed SHA-256 digest.");
        }

        byte[] publicKey;
        byte[] signature;
        try
        {
            publicKey = Convert.FromBase64String(document.PublicKey);
            signature = Convert.FromBase64String(document.Signature);
            if (publicKey.Length > 4 * 1024 || signature.Length > 4 * 1024)
            {
                return Failure(InvalidFormatCode, "Signature key or payload exceeds the supported size.", packageHashText, document.KeyFingerprint);
            }
        }
        catch (FormatException exception)
        {
            return Failure(InvalidFormatCode, exception.Message, packageHashText, document.KeyFingerprint);
        }

        var fingerprint = Sha256(publicKey);
        if (!string.Equals(fingerprint, document.KeyFingerprint, StringComparison.Ordinal))
        {
            return Failure(InvalidFormatCode, "Embedded public key fingerprint does not match the document.", packageHashText, fingerprint);
        }

        var revoked = policy.RevokedKeyFingerprints.Contains(fingerprint, StringComparer.Ordinal);
        if (revoked)
        {
            return new ModulePackageSignatureVerification(
                false,
                false,
                true,
                packageHashText,
                fingerprint,
                KeyRevokedCode,
                "Package signature uses a revoked key.");
        }

        bool cryptographicallyValid;
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out var consumed);
            cryptographicallyValid = consumed == publicKey.Length &&
                verifier.VerifyHash(packageHash, signature, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException exception)
        {
            return Failure(SignatureInvalidCode, exception.Message, packageHashText, fingerprint);
        }

        if (!cryptographicallyValid)
        {
            return Failure(SignatureInvalidCode, "Package signature is cryptographically invalid.", packageHashText, fingerprint);
        }

        var trusted = policy.TrustedKeyFingerprints.Contains(fingerprint, StringComparer.Ordinal);
        if (policy.Mode == ModuleSigningMode.Required && !trusted)
        {
            return new ModulePackageSignatureVerification(
                false,
                false,
                false,
                packageHashText,
                fingerprint,
                KeyUntrustedCode,
                "Package signature key is not trusted by the release policy.");
        }

        return new ModulePackageSignatureVerification(
            true,
            trusted,
            false,
            packageHashText,
            fingerprint,
            null,
            null);
    }

    public static string GetPublicKeyFingerprint(string privateKeyPemPath)
    {
        var keyPath = RequireFile(privateKeyPemPath, "Private signing key", 64 * 1024);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        key.ImportFromPem(File.ReadAllText(keyPath));
        return Sha256(key.ExportSubjectPublicKeyInfo());
    }

    private static string RequireFile(string path, string label, long maximumBytes)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{label} file does not exist.", fullPath);
        }

        if (new FileInfo(fullPath).Length > maximumBytes)
        {
            throw new InvalidDataException($"{label} exceeds the supported size limit.");
        }

        return fullPath;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit) && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static ModulePackageSignatureVerification Failure(
        string code,
        string message,
        string packageHash = "",
        string fingerprint = "") =>
        new(false, false, false, packageHash, fingerprint, code, message);

    private static void WriteAtomically(string destinationPath, string content)
    {
        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Signature destination has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content);
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
}
