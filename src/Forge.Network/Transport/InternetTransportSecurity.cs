using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Forge.Network.Transport;

public sealed record InternetAccountTokenHash(Guid AccountId, string TokenSha256);

public sealed class InternetServerSecurityOptions
{
    private readonly IReadOnlyDictionary<Guid, byte[]> _tokenHashes;

    public InternetServerSecurityOptions(
        X509Certificate2 serverCertificate,
        IReadOnlyList<InternetAccountTokenHash> accounts)
    {
        ArgumentNullException.ThrowIfNull(serverCertificate);
        ArgumentNullException.ThrowIfNull(accounts);
        if (!serverCertificate.HasPrivateKey)
            throw new ArgumentException("NET_TLS_CERTIFICATE_PRIVATE_KEY_REQUIRED", nameof(serverCertificate));
        if (serverCertificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow ||
            serverCertificate.NotBefore.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("NET_TLS_CERTIFICATE_VALIDITY_INVALID", nameof(serverCertificate));
        if (accounts.Count == 0 ||
            accounts.Any(account => account is null) ||
            accounts.Count(item => item.AccountId == Guid.Empty) > 1 ||
            accounts.Select(item => item.AccountId).Distinct().Count() != accounts.Count)
            throw new ArgumentException("NET_AUTH_ACCOUNT_REGISTRY_INVALID", nameof(accounts));

        var hashes = new Dictionary<Guid, byte[]>();
        foreach (var account in accounts)
        {
            if (!TryDecodeSha256(account.TokenSha256, out var hash))
                throw new ArgumentException("NET_AUTH_TOKEN_HASH_INVALID", nameof(accounts));
            hashes.Add(account.AccountId, hash);
        }

        ServerCertificate = serverCertificate;
        CertificateSha256 = InternetTransportSecurity.CertificateSha256(serverCertificate);
        _tokenHashes = hashes;
    }

    public X509Certificate2 ServerCertificate { get; }

    public string CertificateSha256 { get; }

    public SslProtocols EnabledProtocols => SslProtocols.Tls12 | SslProtocols.Tls13;

    public string? Authenticate(Guid accountId, string? token)
    {
        if (accountId == Guid.Empty ||
            (!_tokenHashes.TryGetValue(accountId, out var expected) &&
             !_tokenHashes.TryGetValue(Guid.Empty, out expected)))
            return "NET_AUTH_INVALID_CREDENTIALS";
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 24 or > 256)
            return "NET_AUTH_INVALID_CREDENTIALS";
        var actual = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(expected, actual)
            ? null
            : "NET_AUTH_INVALID_CREDENTIALS";
    }

    private static bool TryDecodeSha256(string value, out byte[] hash)
    {
        hash = [];
        if (value is not { Length: 64 }) return false;
        try
        {
            hash = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class InternetTransportSecurity
{
    public static string TokenSha256(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (token.Length is < 24 or > 256) throw new ArgumentException("NET_AUTH_TOKEN_INVALID", nameof(token));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
    }

    public static string CertificateSha256(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
    }

    public static X509Certificate2 CreateSelfSignedCertificate(
        string commonName,
        TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);
        if (commonName.Trim().Length > 120 || commonName.Any(character => char.IsControl(character)))
            throw new ArgumentException("NET_TLS_CERTIFICATE_NAME_INVALID", nameof(commonName));
        var effectiveLifetime = lifetime ?? TimeSpan.FromDays(30);
        if (effectiveLifetime < TimeSpan.FromHours(1) || effectiveLifetime > TimeSpan.FromDays(398))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            $"CN={commonName.Trim().Replace(",", "_", StringComparison.Ordinal)}",
            key,
            HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var now = DateTimeOffset.UtcNow;
        using var generated = request.CreateSelfSigned(now.AddMinutes(-5), now.Add(effectiveLifetime));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }
}
