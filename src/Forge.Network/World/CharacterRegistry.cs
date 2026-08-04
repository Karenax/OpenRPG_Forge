namespace Forge.Network.World;

public sealed class CharacterRegistry
{
    private readonly Dictionary<Guid, NetworkCharacter> _characters = [];

    public WorldDecision Register(NetworkCharacter character, Guid actorAccountId)
    {
        if (character.CharacterId == Guid.Empty || character.OwnerAccountId == Guid.Empty || string.IsNullOrWhiteSpace(character.Name))
            return Reject("NET_CHARACTER_INVALID", character.CharacterId);
        if (character.OwnerAccountId != actorAccountId)
            return Reject("NET_CHARACTER_OWNER_MISMATCH", character.CharacterId);
        if (string.IsNullOrWhiteSpace(character.ContentFingerprint))
            return Reject("NET_CHARACTER_CONTENT_REQUIRED", character.CharacterId);
        if (_characters.ContainsKey(character.CharacterId))
            return Reject("NET_CHARACTER_DUPLICATE", character.CharacterId);
        _characters.Add(character.CharacterId, character with { Revision = 1 });
        return Accept("NET_CHARACTER_REGISTERED", character.CharacterId, 1);
    }

    public NetworkCharacter? Find(Guid id) => _characters.GetValueOrDefault(id);
    public IReadOnlyList<NetworkCharacter> ForOwner(Guid ownerAccountId) => _characters.Values.Where(x => x.OwnerAccountId == ownerAccountId).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.CharacterId).ToArray();

    private static WorldDecision Accept(string code, Guid id, long revision) => new(true, code, revision, $"accepted:{code}:character={id}:revision={revision}");
    private static WorldDecision Reject(string code, Guid id) => new(false, code, 0, $"rejected:{code}:character={id}");
}
