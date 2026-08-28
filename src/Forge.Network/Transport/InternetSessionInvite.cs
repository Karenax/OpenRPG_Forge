using System.Net;
using System.Text.Json;

namespace Forge.Network.Transport;

public sealed record InternetSessionInvite(
    int SchemaVersion,
    string Address,
    int Port,
    string CertificateSha256,
    string AuthenticationToken)
{
    public const int CurrentSchemaVersion = 1;
    public const string Prefix = "openrpg2:";

    public string Encode()
    {
        Validate(this);
        var json = JsonSerializer.SerializeToUtf8Bytes(this, Options);
        return Prefix + Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out InternetSessionInvite invite)
    {
        invite = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        var encoded = value[Prefix.Length..].Replace('-', '+').Replace('_', '/');
        encoded += new string('=', (4 - encoded.Length % 4) % 4);
        try
        {
            var payload = Convert.FromBase64String(encoded);
            if (payload.Length is < 1 or > 768) return false;
            var decoded = JsonSerializer.Deserialize<InternetSessionInvite>(payload, Options);
            if (decoded is null) return false;
            Validate(decoded);
            invite = decoded;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static void Validate(InternetSessionInvite invite)
    {
        if (invite.SchemaVersion != CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(invite.Address) || invite.Address.Length > 253 ||
            invite.Address.Any(char.IsWhiteSpace) ||
            (!IPAddress.TryParse(invite.Address, out _) &&
             Uri.CheckHostName(invite.Address) != UriHostNameType.Dns) ||
            invite.Port is < 1024 or > 65_535 ||
            invite.CertificateSha256 is not { Length: 64 } ||
            invite.CertificateSha256.Any(character => !Uri.IsHexDigit(character)) ||
            invite.AuthenticationToken is not { Length: >= 24 and <= 256 } ||
            invite.AuthenticationToken.Any(char.IsWhiteSpace))
            throw new ArgumentException("NET_SESSION_INVITE_INVALID", nameof(invite));
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
