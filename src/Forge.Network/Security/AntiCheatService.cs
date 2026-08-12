using System.Security.Cryptography;
using Forge.Network.Replication;

namespace Forge.Network.Security;

public sealed record AntiCheatPolicy(Guid Id, int SchemaVersion, float MaximumUnitsPerSecond,
    int MaximumIntentsPerSecond, int AnomalyThreshold, bool RequireSignedContent,
    IReadOnlyList<string> TrustedSignerFingerprints, bool AuthoritativeRandom,
    bool TrustClientSaves, int RandomSeed);

public sealed record NetworkJoinProof(string ContentSha256, string SignerFingerprint, bool SignatureValid);
public sealed record AntiCheatDecision(bool Allowed, string Code, int AnomalyScore, bool Quarantined,
    string AuditEntry);

public sealed class AntiCheatService
{
    private readonly AntiCheatPolicy _policy;
    private readonly IntentGuard _guard;
    private readonly Random _random;

    public AntiCheatService(AntiCheatPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _guard = new(policy.MaximumUnitsPerSecond, policy.MaximumIntentsPerSecond);
        _random = new(policy.RandomSeed);
    }

    public AntiCheatDecision ValidateJoin(Guid connectionId, NetworkJoinProof proof)
    {
        if (connectionId == Guid.Empty || !IsSha256(proof.ContentSha256))
            return Reject(connectionId, "NET_ANTICHEAT_JOIN_IDENTITY_INVALID", 10);
        if (_policy.RequireSignedContent && (!proof.SignatureValid ||
            !_policy.TrustedSignerFingerprints.Contains(proof.SignerFingerprint, StringComparer.OrdinalIgnoreCase)))
            return Reject(connectionId, "NET_ANTICHEAT_CONTENT_SIGNATURE_REJECTED", 25);
        return Allow(connectionId, "NET_ANTICHEAT_JOIN_ALLOWED", 0);
    }

    public AntiCheatDecision ValidateIntent(MovementIntent intent, long serverRevision, DateTimeOffset receivedAt)
    {
        var decision = _guard.Validate(intent, serverRevision, receivedAt);
        return decision.Allowed
            ? Allow(intent.ConnectionId, "NET_ANTICHEAT_INTENT_ALLOWED", decision.ViolationScore)
            : new(false, decision.Code, decision.ViolationScore,
                decision.ViolationScore >= _policy.AnomalyThreshold,
                $"rejected:{decision.Code}:connection={intent.ConnectionId:D}:score={decision.ViolationScore}");
    }

    public AntiCheatDecision ValidateClientSave(Guid connectionId, ReadOnlySpan<byte> payload) =>
        !_policy.TrustClientSaves && payload.Length > 0
            ? Reject(connectionId, "NET_ANTICHEAT_CLIENT_SAVE_REJECTED", 10)
            : Allow(connectionId, "NET_ANTICHEAT_CLIENT_SAVE_IGNORED", 0);

    public int NextAuthoritativeRandom(int minimumInclusive, int maximumExclusive)
    {
        if (!_policy.AuthoritativeRandom) throw new InvalidOperationException("NET_ANTICHEAT_RNG_NOT_AUTHORITATIVE");
        return _random.Next(minimumInclusive, maximumExclusive);
    }

    private AntiCheatDecision Reject(Guid connectionId, string code, int score) =>
        new(false, code, score, score >= _policy.AnomalyThreshold,
            $"rejected:{code}:connection={connectionId:D}:score={score}");

    private static AntiCheatDecision Allow(Guid connectionId, string code, int score) =>
        new(true, code, score, false, $"accepted:{code}:connection={connectionId:D}:score={score}");

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}
