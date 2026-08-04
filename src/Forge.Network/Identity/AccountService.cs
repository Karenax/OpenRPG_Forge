using System.Security.Cryptography;

namespace Forge.Network.Identity;

public sealed record Account(Guid AccountId, string Login, byte[] PasswordSalt, byte[] PasswordHash, int Iterations, bool Disabled, long Revision);
public sealed record AuthenticationDecision(bool Authenticated, string Code, Guid? AccountId, string AuditEntry);

public sealed class AccountService
{
    private const int CurrentIterations = 120_000;
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public Account Register(string login, string password)
    {
        var normalized = NormalizeLogin(login);
        if (password.Length < 10 || password.Length > 256) throw new ArgumentException("NET_PASSWORD_POLICY_FAILED", nameof(password));
        if (_accounts.ContainsKey(normalized)) throw new InvalidOperationException("NET_ACCOUNT_DUPLICATE");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, 32);
        var account = new Account(Guid.NewGuid(), normalized, salt, hash, CurrentIterations, false, 1);
        _accounts.Add(normalized, account);
        return account;
    }

    public AuthenticationDecision Authenticate(string login, string password)
    {
        var normalized = NormalizeLogin(login);
        if (!_accounts.TryGetValue(normalized, out var account)) return Reject("NET_AUTH_INVALID_CREDENTIALS", normalized);
        if (account.Disabled) return Reject("NET_ACCOUNT_DISABLED", normalized);
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, account.PasswordSalt, account.Iterations, HashAlgorithmName.SHA256, account.PasswordHash.Length);
        if (!CryptographicOperations.FixedTimeEquals(candidate, account.PasswordHash)) return Reject("NET_AUTH_INVALID_CREDENTIALS", normalized);
        return new(true, "NET_AUTHENTICATED", account.AccountId, $"accepted:NET_AUTHENTICATED:account={account.AccountId}");
    }

    private static string NormalizeLogin(string login)
    {
        var normalized = login.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 64 || normalized.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException("NET_LOGIN_INVALID", nameof(login));
        return normalized;
    }

    private static AuthenticationDecision Reject(string code, string login) => new(false, code, null, $"rejected:{code}:login={login}");
}
