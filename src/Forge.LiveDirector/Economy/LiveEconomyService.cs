using Forge.Network.Operations;

namespace Forge.LiveDirector.Economy;

public sealed class LiveEconomyService(AuthorizationService authorization, long absoluteAdjustmentCap, long walletCap)
{
    public sealed record Request(Guid CommandId, Guid WalletAccountId, long Amount, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, long PreviousBalance, long Balance, string SemanticDiff, string AuditEntry);

    private readonly Dictionary<Guid, long> _wallets = [];
    private readonly HashSet<Guid> _processedCommands = [];
    private readonly Dictionary<Guid, Compensation> _compensations = [];
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
        _compensations[request.CommandId] = new(request.WalletAccountId, previous, balance);
        return new(true, "LIVE_ECONOMY_APPLIED", _revision, previous, balance, diff, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public Result Compensate(AuthorizationSubject actor, Guid commandId, long expectedRevision,
        bool apply)
    {
        if (!_compensations.TryGetValue(commandId, out var compensation))
            return RejectCompensation("LIVE_ECONOMY_COMPENSATION_UNKNOWN", commandId);
        if (expectedRevision != _revision)
            return RejectCompensation("LIVE_ECONOMY_COMPENSATION_REVISION_STALE", commandId);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return RejectCompensation("LIVE_ECONOMY_PERMISSION_DENIED", commandId);
        if (Balance(compensation.AccountId) != compensation.AppliedBalance)
            return RejectCompensation("LIVE_ECONOMY_COMPENSATION_CONFLICT", commandId);
        var diff = $"wallet.restore:account={compensation.AccountId}:balance={compensation.AppliedBalance}->{compensation.PreviousBalance}";
        if (!apply)
            return new(false, "LIVE_ECONOMY_COMPENSATION_PREVIEW", _revision,
                compensation.AppliedBalance, compensation.PreviousBalance, diff,
                $"previewed:{diff}:actor={actor.AccountId}");
        _wallets[compensation.AccountId] = compensation.PreviousBalance;
        _compensations.Remove(commandId);
        _revision++;
        return new(true, "LIVE_ECONOMY_COMPENSATED", _revision,
            compensation.AppliedBalance, compensation.PreviousBalance, diff,
            $"compensated:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public IReadOnlyDictionary<Guid, long> ExportWallets() =>
        new SortedDictionary<Guid, long>(_wallets);

    public void ImportWallets(IReadOnlyDictionary<Guid, long> wallets)
    {
        ArgumentNullException.ThrowIfNull(wallets);
        if (wallets.Any(item => item.Key == Guid.Empty || item.Value < 0 ||
                                item.Value > walletCap))
            throw new InvalidDataException("LIVE_ECONOMY_SNAPSHOT_INVALID");
        _wallets.Clear();
        foreach (var item in wallets.OrderBy(item => item.Key)) _wallets[item.Key] = item.Value;
    }

    private Result Reject(string code, Request request, long previous) => new(false, code, _revision, previous, previous, "no changes", $"rejected:{code}:command={request.CommandId}");

    private Result RejectCompensation(string code, Guid commandId) => new(false, code,
        _revision, 0, 0, "no changes", $"rejected:{code}:command={commandId}");

    private sealed record Compensation(Guid AccountId, long PreviousBalance,
        long AppliedBalance);
}
