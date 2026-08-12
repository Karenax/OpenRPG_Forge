using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;

namespace Forge.Network.Replication;

public sealed record WeatherReplicationEnvelope(
    int SchemaVersion,
    AreaId AreaId,
    Guid StateId,
    int Revision,
    long ServerRevision,
    double ElapsedSeconds,
    Guid PreviousStateId,
    float BlendSeconds,
    string AudioCue,
    string GameplayEffect,
    string Sha256)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record WeatherReplicationApplyResult(bool Applied, string Code,
    WeatherReplicationEnvelope? State);

public static class WeatherReplicationService
{
    public static WeatherReplicationEnvelope Publish(AreaId areaId, Guid stateId, int revision,
        long serverRevision, double elapsedSeconds, Guid previousStateId, float blendSeconds,
        string audioCue, string gameplayEffect, bool hasAuthority)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(audioCue);
        ArgumentNullException.ThrowIfNull(gameplayEffect);
        if (!hasAuthority) throw new UnauthorizedAccessException("NET_WEATHER_AUTHORITY_DENIED");
        if (stateId == Guid.Empty || revision < 0 || serverRevision < 0 || elapsedSeconds < 0 ||
            !double.IsFinite(elapsedSeconds) || blendSeconds is < 0 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(revision));
        var hash = Hash(areaId, stateId, revision, serverRevision, elapsedSeconds, previousStateId,
            blendSeconds, audioCue, gameplayEffect);
        return new(WeatherReplicationEnvelope.CurrentSchemaVersion, areaId, stateId, revision,
            serverRevision, elapsedSeconds, previousStateId, blendSeconds, audioCue.Trim(),
            gameplayEffect.Trim(), hash);
    }

    public static WeatherReplicationApplyResult Apply(WeatherReplicationEnvelope envelope,
        WeatherReplicationEnvelope? current)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != WeatherReplicationEnvelope.CurrentSchemaVersion ||
            !string.Equals(envelope.Sha256, Hash(envelope.AreaId, envelope.StateId, envelope.Revision,
                envelope.ServerRevision, envelope.ElapsedSeconds, envelope.PreviousStateId,
                envelope.BlendSeconds, envelope.AudioCue, envelope.GameplayEffect), StringComparison.Ordinal))
            return new(false, "NET_WEATHER_ENVELOPE_INVALID", current);
        if (current is not null && envelope.ServerRevision <= current.ServerRevision)
            return new(false, "NET_WEATHER_REVISION_STALE", current);
        return new(true, "NET_WEATHER_APPLIED", envelope);
    }

    private static string Hash(AreaId areaId, Guid stateId, int revision, long serverRevision,
        double elapsedSeconds, Guid previousStateId, float blendSeconds, string audioCue, string gameplayEffect)
    {
        var payload = FormattableString.Invariant(
            $"1|{areaId}|{stateId:D}|{revision}|{serverRevision}|{elapsedSeconds:R}|{previousStateId:D}|{blendSeconds:R}|{audioCue.Trim()}|{gameplayEffect.Trim()}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
