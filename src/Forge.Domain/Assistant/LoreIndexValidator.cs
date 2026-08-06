using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public static partial class LoreIndexValidator
{
    public const string SchemaUnsupportedCode = "assistant.lore.schema_unsupported";
    public const string IdentityInvalidCode = "assistant.lore.identity_invalid";
    public const string EntryInvalidCode = "assistant.lore.entry_invalid";
    public const string SourceInvalidCode = "assistant.lore.source_invalid";
    public const string ReferenceInvalidCode = "assistant.lore.reference_invalid";
    public const string GovernanceMissingCode = "assistant.lore.governance_missing";

    private static readonly Regex SafeKey = SafeKeyRegex();
    private static readonly Regex SafeSourceId = SafeSourceIdRegex();

    public static IReadOnlyList<DomainDiagnostic> Validate(LoreIndexDefinition index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var diagnostics = new List<DomainDiagnostic>();
        if (index.SchemaVersion != LoreIndexDefinition.CurrentSchemaVersion)
        {
            diagnostics.Add(new(SchemaUnsupportedCode, "Lore index schema is unsupported.", "assistant/lore/schemaVersion"));
        }
        if (index.Id == Guid.Empty || index.MaximumContextCharacters is < 512 or > 100_000)
        {
            diagnostics.Add(new(IdentityInvalidCode, "Lore index UUID or context budget is invalid.", "assistant/lore/id"));
        }
        if (string.IsNullOrWhiteSpace(index.Provenance))
        {
            diagnostics.Add(new(GovernanceMissingCode, "Lore index provenance is required.", "assistant/lore/provenance"));
        }
        if (index.Entries.Count > 10_000 ||
            index.Entries.GroupBy(entry => entry.Id).Any(group => group.Count() > 1) ||
            index.Entries.GroupBy(entry => entry.Key, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            diagnostics.Add(new(EntryInvalidCode, "Lore entries require unique bounded UUIDs and keys.", "assistant/lore/entries"));
        }
        var ids = index.Entries.Select(entry => entry.Id).ToHashSet();
        foreach (var entry in index.Entries)
        {
            var path = $"assistant/lore/entries/{entry.Id:D}";
            if (entry.Id == Guid.Empty ||
                entry.SchemaVersion != LoreEntryDefinition.CurrentSchemaVersion ||
                !SafeKey.IsMatch(entry.Key) ||
                string.IsNullOrWhiteSpace(entry.Title) || entry.Title.Length > 200 ||
                string.IsNullOrWhiteSpace(entry.Content) || entry.Content.Length > 50_000 ||
                entry.Tags.Count > 32 || entry.Tags.Any(tag => !SafeKey.IsMatch(tag)) ||
                string.IsNullOrWhiteSpace(entry.Provenance))
            {
                diagnostics.Add(new(EntryInvalidCode, "Lore entry identity, content, tags or provenance are invalid.", path));
            }
            if (entry.SourceIds.Count is < 1 or > 32 || entry.SourceIds.Any(source => !SafeSourceId.IsMatch(source)))
            {
                diagnostics.Add(new(SourceInvalidCode, "Lore entries require one to thirty-two stable source IDs.", $"{path}/sourceIds"));
            }
            if (entry.RelatedEntryIds.Contains(entry.Id) || entry.RelatedEntryIds.Any(related => !ids.Contains(related)))
            {
                diagnostics.Add(new(ReferenceInvalidCode, "Lore entry relationships must resolve and cannot target themselves.", $"{path}/relatedEntryIds"));
            }
        }
        return diagnostics;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,95}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeKeyRegex();

    [GeneratedRegex("^(project|module|blueprint|dialogue|dialogue-node|quest|quest-state|manual):[a-z0-9._:-]{2,160}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSourceIdRegex();
}
