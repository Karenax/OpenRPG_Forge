using Forge.Network.Operations;

namespace Forge.LiveDirector.Economy;

public sealed class LiveEconomyService(AuthorizationService authorization, long absoluteAdjustmentCap, long walletCap)
{
    public sealed record Request(Guid CommandId, Guid WalletAccountId, long Amount, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, long PreviousBalance, long Balance, string SemanticDiff, string AuditEntry);

    private readonly Dictionary<Guid, long> _wallets = [];
    private readonly HashSet<Guid> _processedCommands = [];
    private long _revision;

    public long Balance(Guid accountId) => _wallets.GetValueOrDefault(accountId);

    public Result Adjust(AuthorizationSubject actor, Request request, bool apply)
    {
        var previous = Balance(request.WalletAccountId);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_ECONOMY_PERMISSION_DENIED", request, previous);
        if (request.CommandId == Guid.Empty || request.WalletAccountId == Guid.Empty || request.Amount == 0 || string.IsNullOrWhiteSpace(request.Reason))
            return Reject("LIVE_ECONOMY_REQUEST_INVALID", request, previous);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_ECONOMY_REVISION_STALE", request, previous);
        if (_processedCommands.Contains(request.CommandId)) return Reject("LIVE_ECONOMY_COMMAND_REPLAYED", request, previous);
        if (Math.Abs((decimal)request.Amount) > absoluteAdjustmentCap) return Reject("LIVE_ECONOMY_ADJUSTMENT_CAP", request, previous);
        var candidate = (decimal)previous + request.Amount;
        if (candidate < 0 || candidate > walletCap) return Reject("LIVE_ECONOMY_WALLET_RANGE", request, previous);
        var balance = (long)candidate;
        var diff = $"wallet.adjust:account={request.WalletAccountId}:amount={request.Amount}:balance={previous}->{balance}:reason={request.Reason.Trim()}";
        if (!apply) return new(false, "LIVE_ECONOMY_PREVIEW", _revision, previous, balance, diff, $"previewed:{diff}:actor={actor.AccountId}");
        _wallets[request.WalletAccountId] = balance;
        _processedCommands.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_ECONOMY_APPLIED", _revision, previous, balance, diff, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private Result Reject(string code, Request request, long previous) => new(false, code, _revision, previous, previous, "no changes", $"rejected:{code}:command={request.CommandId}");
}
