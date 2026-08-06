using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public static class LoreContextAssembler
{
    public static LoreContextPacket Assemble(
        LoreIndexDefinition index,
        ProjectDefinition project,
        string query,
        int maximumEntries = 8)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(project);
        if (!index.Enabled || index.Entries.Count == 0)
        {
            return Packet(query, string.Empty, [], 0, false);
        }
        var hits = LoreRetrievalIndex.Build(index, project).Query(query, maximumEntries);
        var builder = new StringBuilder();
        var included = new List<LoreRetrievalResult>();
        var truncated = false;
        foreach (var hit in hits)
        {
            var block = $"[{hit.Kind}:{hit.Key}] sources={string.Join(',', hit.SourceIds)}\n{hit.Content}\n";
            if (builder.Length + block.Length > index.MaximumContextCharacters)
            {
                truncated = true;
                break;
            }
            builder.Append(block);
            included.Add(hit);
        }
        var sourceIds = included.SelectMany(hit => hit.SourceIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return Packet(query, builder.ToString().TrimEnd(), sourceIds, included.Count, truncated);
    }

    private static LoreContextPacket Packet(
        string query,
        string content,
        IReadOnlyList<string> sourceIds,
        int entryCount,
        bool truncated)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new LoreContextPacket((query ?? string.Empty).Trim(), content, hash, sourceIds, entryCount, truncated);
    }
}
