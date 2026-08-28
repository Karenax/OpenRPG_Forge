using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Network.Transport;

public sealed record InternetAccountTokenRegistry(
    int SchemaVersion,
    IReadOnlyList<InternetAccountTokenHash> Accounts)
{
    public const int CurrentSchemaVersion = 1;
}

public static class InternetServerSecurityStore
{
    private const long MaximumCertificateBytes = 1024 * 1024;
    private const long MaximumRegistryBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static X509Certificate2 LoadCertificate(
        string certificatePath,
        string passwordEnvironmentVariable)
    {
        var path = ValidateFile(certificatePath, MaximumCertificateBytes, ".pfx", ".p12");
        if (string.IsNullOrWhiteSpace(passwordEnvironmentVariable) ||
            passwordEnvironmentVariable.Length > 128 ||
            passwordEnvironmentVariable.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '_')))
            throw new InvalidDataException("NET_TLS_PASSWORD_ENV_INVALID");
        var password = Environment.GetEnvironmentVariable(passwordEnvironmentVariable)
            ?? throw new InvalidDataException("NET_TLS_PASSWORD_ENV_MISSING");
        if (password.Length is < 12 or > 256) throw new InvalidDataException("NET_TLS_PASSWORD_INVALID");
        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                path,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("NET_TLS_CERTIFICATE_LOAD_FAILED", exception);
        }
    }

    public static IReadOnlyList<InternetAccountTokenHash> LoadAccountRegistry(string registryPath)
    {
        var path = ValidateFile(registryPath, MaximumRegistryBytes, ".json");
        try
        {
            var registry = JsonSerializer.Deserialize<InternetAccountTokenRegistry>(
                    File.ReadAllText(path, System.Text.Encoding.UTF8), Options)
                ?? throw new InvalidDataException("NET_AUTH_REGISTRY_EMPTY");
            if (registry.SchemaVersion != InternetAccountTokenRegistry.CurrentSchemaVersion ||
                registry.Accounts is null || registry.Accounts.Count is < 1 or > 64)
                throw new InvalidDataException("NET_AUTH_REGISTRY_INVALID");
            return registry.Accounts;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("NET_AUTH_REGISTRY_INVALID", exception);
        }
    }

    private static string ValidateFile(string path, long maximumBytes, params string[] extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("NET_SECURITY_PATH_NOT_ABSOLUTE");
        var fullPath = Path.GetFullPath(path);
        if (!extensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("NET_SECURITY_FILE_EXTENSION_INVALID");
        var file = new FileInfo(fullPath);
        if (!file.Exists || file.Length is < 1 || file.Length > maximumBytes ||
            file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("NET_SECURITY_FILE_INVALID");
        return fullPath;
    }
}
