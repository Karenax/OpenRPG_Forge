using System.Security.Cryptography;
using System.Text;

namespace Forge.Network.Content;

public sealed record ContentResource(string Path, long Size, string Sha256);
public sealed record ContentManifest(string ModuleId, string ModuleVersion, string ProtocolVersion, IReadOnlyList<ContentResource> Resources, string Fingerprint);
public sealed record ContentDecision(bool Compatible, string Code, IReadOnlyList<ContentResource> MissingOrChanged,
    string AuditEntry, IReadOnlyList<string>? MismatchDetails = null);

public static class ContentManifestService
{
    public static ContentManifest Create(string moduleId, string moduleVersion, string protocolVersion, IEnumerable<ContentResource> resources)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(moduleVersion) || string.IsNullOrWhiteSpace(protocolVersion))
            throw new ArgumentException("NET_CONTENT_IDENTITY_REQUIRED");
        var ordered = resources.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        if (ordered.Any(x => string.IsNullOrWhiteSpace(x.Path) || x.Size < 0 || !IsSha256(x.Sha256)) || ordered.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("NET_CONTENT_RESOURCE_INVALID", nameof(resources));
        var canonical = string.Join("\n", new[] { moduleId, moduleVersion, protocolVersion }.Concat(ordered.Select(x => $"{x.Path}|{x.Size}|{x.Sha256.ToLowerInvariant()}")));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(moduleId, moduleVersion, protocolVersion, ordered, fingerprint);
    }

    public static ContentDecision Compare(ContentManifest server, ContentManifest client)
    {
        if (server.ProtocolVersion != client.ProtocolVersion)
            return Reject("NET_PROTOCOL_MISMATCH", server.Resources,
                [$"Protocol attendu {server.ProtocolVersion}, reçu {client.ProtocolVersion}."]);
        if (server.ModuleId != client.ModuleId || server.ModuleVersion != client.ModuleVersion)
            return Reject("NET_MODULE_VERSION_MISMATCH", server.Resources,
                [$"Module attendu {server.ModuleId}@{server.ModuleVersion}, reçu {client.ModuleId}@{client.ModuleVersion}."]);
        var clientByPath = client.Resources.ToDictionary(x => x.Path, StringComparer.Ordinal);
        var missing = server.Resources.Where(x => !clientByPath.TryGetValue(x.Path, out var candidate) || candidate.Size != x.Size || !string.Equals(candidate.Sha256, x.Sha256, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (missing.Length != 0 || server.Fingerprint != client.Fingerprint)
            return Reject("NET_CONTENT_SYNC_REQUIRED", missing,
                missing.Select(item => $"Ressource absente ou modifiée : {item.Path} ({item.Size} octets, sha256 {item.Sha256}).").ToArray());
        return new(true, "NET_CONTENT_COMPATIBLE", [],
            $"accepted:NET_CONTENT_COMPATIBLE:fingerprint={server.Fingerprint}", []);
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static ContentDecision Reject(string code, IReadOnlyList<ContentResource> resources,
        IReadOnlyList<string>? details = null) => new(false, code, resources, $"rejected:{code}", details ?? []);
}
