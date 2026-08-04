namespace Forge.Network.Sessions;

public static class NetworkValidation
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(DedicatedServerConfiguration config)
    {
        var diagnostics = new List<NetworkDiagnostic>();
        if (config.ServerId == Guid.Empty) diagnostics.Add(new("NET_SERVER_ID_REQUIRED", "/network/serverId", "A stable server UUID is required."));
        if (config.SchemaVersion != 1) diagnostics.Add(new("NET_SCHEMA_UNSUPPORTED", "/network/schemaVersion", "Only network schema v1 is supported."));
        if (string.IsNullOrWhiteSpace(config.Name)) diagnostics.Add(new("NET_SERVER_NAME_REQUIRED", "/network/name", "Server name is required."));
        if (config.Port is < 1024 or > 65535) diagnostics.Add(new("NET_PORT_INVALID", "/network/port", "Port must be between 1024 and 65535."));
        if (config.Capacity is < 1 or > 64) diagnostics.Add(new("NET_CAPACITY_INVALID", "/network/capacity", "Capacity must be between 1 and 64."));
        if (string.IsNullOrWhiteSpace(config.ModuleFingerprint)) diagnostics.Add(new("NET_FINGERPRINT_REQUIRED", "/network/moduleFingerprint", "Module fingerprint is required."));
        if (string.IsNullOrWhiteSpace(config.ProtocolVersion)) diagnostics.Add(new("NET_PROTOCOL_REQUIRED", "/network/protocolVersion", "Protocol version is required."));
        return diagnostics;
    }

    public static IReadOnlyList<NetworkDiagnostic> Validate(CharacterDraft draft)
    {
        var diagnostics = new List<NetworkDiagnostic>();
        if (draft.CharacterId == Guid.Empty) diagnostics.Add(new("NET_CHARACTER_ID_REQUIRED", "/character/id", "A stable character UUID is required."));
        if (string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Length > 40) diagnostics.Add(new("NET_CHARACTER_NAME_INVALID", "/character/name", "Name must contain 1 to 40 characters."));
        if (string.IsNullOrWhiteSpace(draft.AncestryId)) diagnostics.Add(new("NET_CHARACTER_ANCESTRY_REQUIRED", "/character/ancestryId", "Ancestry is required."));
        if (string.IsNullOrWhiteSpace(draft.ClassId)) diagnostics.Add(new("NET_CHARACTER_CLASS_REQUIRED", "/character/classId", "Class is required."));
        if (draft.Attributes.Count == 0 || draft.Attributes.Any(pair => pair.Value is < 1 or > 30)) diagnostics.Add(new("NET_CHARACTER_ATTRIBUTES_INVALID", "/character/attributes", "Attributes must be between 1 and 30."));
        return diagnostics;
    }
}
