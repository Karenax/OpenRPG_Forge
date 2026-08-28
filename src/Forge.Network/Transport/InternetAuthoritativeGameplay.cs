using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Network.Transport;

internal sealed record InternetGameplayDecision(bool Accepted, string Code, long Revision);

internal sealed class InternetAuthoritativeGameplay
{
    private const int MaximumHealth = 100;
    private const int AttackDamage = 30;
    private const int HealingPotionAmount = 25;
    private const double MaximumAttackDistance = 6;
    private const string HealingPotion = "healing-potion";
    private const string TrialQuest = "network-trial";

    private readonly Guid _sessionId;
    private readonly Dictionary<Guid, Guid> _accountsByConnection = [];
    private readonly Dictionary<Guid, PlayerState> _playersByAccount = [];
    private readonly HashSet<Guid> _processedIntents = [];
    private long _revision;
    private string _eventCode = "NET_GAMEPLAY_READY";

    internal InternetAuthoritativeGameplay(Guid sessionId)
    {
        if (sessionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(sessionId));
        _sessionId = sessionId;
    }

    internal InternetGameplayDecision Connect(Guid connectionId, Guid accountId)
    {
        if (connectionId == Guid.Empty || accountId == Guid.Empty)
            return Reject("NET_GAMEPLAY_IDENTITY_INVALID");
        if (_accountsByConnection.ContainsKey(connectionId))
            return Reject("NET_GAMEPLAY_CONNECTION_EXISTS");
        if (_accountsByConnection.Values.Contains(accountId))
            return Reject("NET_ACCOUNT_ALREADY_CONNECTED");

        _accountsByConnection.Add(connectionId, accountId);
        if (!_playersByAccount.ContainsKey(accountId))
            _playersByAccount.Add(accountId, PlayerState.Create(accountId));
        _revision++;
        _eventCode = "NET_GAMEPLAY_CONNECTED";
        return Accept(_eventCode);
    }

    internal void Disconnect(Guid connectionId)
    {
        if (!_accountsByConnection.Remove(connectionId)) return;
        _revision++;
        _eventCode = "NET_GAMEPLAY_DISCONNECTED";
    }

    internal InternetGameplayDecision Apply(
        Guid connectionId,
        InternetGameplayIntent? intent,
        Func<Guid, InternetPlayerPose?> resolvePose)
    {
        ArgumentNullException.ThrowIfNull(resolvePose);
        if (!_accountsByConnection.TryGetValue(connectionId, out var accountId))
            return Reject("NET_GAMEPLAY_CONNECTION_UNKNOWN");
        if (intent is null || intent.IntentId == Guid.Empty || string.IsNullOrWhiteSpace(intent.Kind) ||
            intent.Kind.Length > 64 || intent.Payload is null || intent.Payload.Count > 16 ||
            intent.Payload.Any(item => item.Key.Length is < 1 or > 64 || item.Value.Length > 256))
            return Reject("NET_GAMEPLAY_INTENT_INVALID");
        if (!_processedIntents.Add(intent.IntentId)) return Reject("NET_GAMEPLAY_INTENT_REPLAYED");
        if (intent.ExpectedRevision != _revision) return Reject("NET_GAMEPLAY_REVISION_STALE");

        var player = _playersByAccount[accountId];
        var code = intent.Kind switch
        {
            "combat.attack" => ApplyAttack(connectionId, player, intent.Payload, resolvePose),
            "inventory.use" => ApplyInventoryUse(player, intent.Payload),
            "quest.advance" => ApplyQuestAdvance(player, intent.Payload),
            "save.checkpoint" => ApplyCheckpoint(player, intent.Payload),
            _ => "NET_GAMEPLAY_INTENT_FORBIDDEN",
        };
        if (!code.EndsWith("_APPLIED", StringComparison.Ordinal)) return Reject(code);

        _revision++;
        _eventCode = code;
        return Accept(code);
    }

    internal InternetGameplaySnapshot Snapshot(Guid connectionId)
    {
        if (!_accountsByConnection.TryGetValue(connectionId, out var accountId))
            throw new InvalidOperationException("NET_GAMEPLAY_CONNECTION_UNKNOWN");
        var player = _playersByAccount[accountId];
        var combatants = _accountsByConnection
            .Select(item => (item.Key, Player: _playersByAccount[item.Value]))
            .OrderBy(item => item.Key)
            .Select(item => new InternetPublicCombatantState(
                item.Key,
                item.Player.Health,
                MaximumHealth,
                item.Player.Health == 0))
            .ToArray();
        return new InternetGameplaySnapshot(
            _sessionId,
            _revision,
            _eventCode,
            combatants,
            new InternetPrivateGameplayState(
                accountId,
                new Dictionary<string, int>(player.Inventory, StringComparer.Ordinal),
                new Dictionary<string, string>(player.Quests, StringComparer.Ordinal),
                player.AttacksLanded,
                player.SaveRevision,
                player.CheckpointSha256));
    }

    private string ApplyAttack(
        Guid attackerConnectionId,
        PlayerState attacker,
        IReadOnlyDictionary<string, string> payload,
        Func<Guid, InternetPlayerPose?> resolvePose)
    {
        if (!TryOnly(payload, "targetConnectionId", out var targetValue) ||
            !Guid.TryParse(targetValue, out var targetConnectionId) ||
            targetConnectionId == Guid.Empty || targetConnectionId == attackerConnectionId ||
            !_accountsByConnection.TryGetValue(targetConnectionId, out var targetAccountId))
            return "NET_COMBAT_TARGET_INVALID";
        if (attacker.Health == 0) return "NET_COMBAT_ATTACKER_DEFEATED";
        var target = _playersByAccount[targetAccountId];
        if (target.Health == 0) return "NET_COMBAT_TARGET_DEFEATED";
        var attackerPose = resolvePose(attackerConnectionId);
        var targetPose = resolvePose(targetConnectionId);
        if (attackerPose is null || targetPose is null ||
            !string.Equals(attackerPose.AreaId, targetPose.AreaId, StringComparison.Ordinal))
            return "NET_COMBAT_SPATIAL_STATE_REQUIRED";
        var distanceSquared = SquaredDistance(attackerPose, targetPose);
        if (distanceSquared > MaximumAttackDistance * MaximumAttackDistance)
            return "NET_COMBAT_TARGET_OUT_OF_RANGE";

        target.Health = Math.Max(0, target.Health - AttackDamage);
        attacker.AttacksLanded++;
        return "NET_COMBAT_ATTACK_APPLIED";
    }

    private static string ApplyInventoryUse(
        PlayerState player,
        IReadOnlyDictionary<string, string> payload)
    {
        if (!TryOnly(payload, "item", out var item) || !string.Equals(item, HealingPotion, StringComparison.Ordinal))
            return "NET_INVENTORY_ITEM_INVALID";
        if (player.Health == 0 || player.Health == MaximumHealth)
            return "NET_INVENTORY_USE_NOT_APPLICABLE";
        if (!player.Inventory.TryGetValue(HealingPotion, out var quantity) || quantity < 1)
            return "NET_INVENTORY_ITEM_UNAVAILABLE";
        player.Inventory[HealingPotion] = quantity - 1;
        player.Health = Math.Min(MaximumHealth, player.Health + HealingPotionAmount);
        return "NET_INVENTORY_USE_APPLIED";
    }

    private static string ApplyQuestAdvance(
        PlayerState player,
        IReadOnlyDictionary<string, string> payload)
    {
        if (!TryOnly(payload, "action", out var action)) return "NET_QUEST_ACTION_INVALID";
        var current = player.Quests[TrialQuest];
        if (string.Equals(action, "begin", StringComparison.Ordinal) && current == "available")
        {
            player.Quests[TrialQuest] = "active";
            return "NET_QUEST_ADVANCE_APPLIED";
        }
        if (string.Equals(action, "claim", StringComparison.Ordinal) && current == "active" &&
            player.AttacksLanded > 0)
        {
            player.Quests[TrialQuest] = "completed";
            return "NET_QUEST_ADVANCE_APPLIED";
        }
        return "NET_QUEST_TRANSITION_REJECTED";
    }

    private static string ApplyCheckpoint(
        PlayerState player,
        IReadOnlyDictionary<string, string> payload)
    {
        if (payload.Count != 0) return "NET_SAVE_PAYLOAD_FORBIDDEN";
        player.SaveRevision++;
        player.CheckpointSha256 = CheckpointSha256(player);
        return "NET_SAVE_CHECKPOINT_APPLIED";
    }

    private static string CheckpointSha256(PlayerState player)
    {
        var canonical = string.Join('|',
            player.AccountId.ToString("D"),
            player.Health.ToString(CultureInfo.InvariantCulture),
            player.AttacksLanded.ToString(CultureInfo.InvariantCulture),
            player.SaveRevision.ToString(CultureInfo.InvariantCulture),
            string.Join(',', player.Inventory.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}")),
            string.Join(',', player.Quests.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool TryOnly(
        IReadOnlyDictionary<string, string> payload,
        string key,
        out string value)
    {
        value = string.Empty;
        return payload.Count == 1 && payload.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    private static double SquaredDistance(InternetPlayerPose left, InternetPlayerPose right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;
        return (x * x) + (y * y) + (z * z);
    }

    private InternetGameplayDecision Accept(string code) => new(true, code, _revision);

    private InternetGameplayDecision Reject(string code) => new(false, code, _revision);

    private sealed class PlayerState
    {
        private PlayerState(Guid accountId)
        {
            AccountId = accountId;
            Inventory = new Dictionary<string, int>(StringComparer.Ordinal) { [HealingPotion] = 2 };
            Quests = new Dictionary<string, string>(StringComparer.Ordinal) { [TrialQuest] = "available" };
        }

        internal Guid AccountId { get; }
        internal int Health { get; set; } = MaximumHealth;
        internal Dictionary<string, int> Inventory { get; }
        internal Dictionary<string, string> Quests { get; }
        internal int AttacksLanded { get; set; }
        internal long SaveRevision { get; set; }
        internal string? CheckpointSha256 { get; set; }

        internal static PlayerState Create(Guid accountId) => new(accountId);
    }
}
