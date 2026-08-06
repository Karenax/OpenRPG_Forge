using System.Globalization;
using System.Text;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public sealed class LoreRetrievalIndex
{
    private readonly LoreDocument[] _documents;

    private LoreRetrievalIndex(IReadOnlyList<LoreDocument> documents)
    {
        _documents = documents.ToArray();
    }

    public int Count => _documents.Length;

    public static LoreRetrievalIndex Build(LoreIndexDefinition index, ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(project);
        var diagnostics = LoreIndexProjectValidator.Validate(index, project);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException($"Lore index is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}", nameof(index));
        }
        var sources = LoreProjectSourceCatalog.Build(project);
        var documents = index.Entries.Select(entry =>
        {
            var sourceContent = entry.SourceIds
                .Select(sourceId => sources.TryGet(sourceId, out var source) ? source.Content : string.Empty);
            var searchText = string.Join(' ',
                entry.Key,
                entry.Title,
                entry.Content,
                string.Join(' ', entry.Tags),
                string.Join(' ', sourceContent));
            return new LoreDocument(entry, Normalize(searchText));
        }).ToArray();
        return new LoreRetrievalIndex(documents);
    }

    public IReadOnlyList<LoreRetrievalResult> Query(string text, int limit = 8)
    {
        if (limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        var normalized = Normalize(text ?? string.Empty);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _documents
            .Select(document => new { Document = document, Score = Score(document, normalized, tokens) })
            .Where(item => tokens.Length == 0 || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Document.Entry.Kind)
            .ThenBy(item => item.Document.Entry.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(item => new LoreRetrievalResult(
                item.Document.Entry.Id,
                item.Document.Entry.Kind,
                item.Document.Entry.Key,
                item.Document.Entry.Title,
                item.Document.Entry.Content,
                item.Document.Entry.SourceIds,
                item.Document.Entry.RelatedEntryIds,
                item.Document.Entry.Tags,
                item.Document.Entry.Provenance,
                item.Score))
            .ToArray();
    }

    private static int Score(LoreDocument document, string text, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return 1;
        if (tokens.Any(token => !document.SearchText.Contains(token, StringComparison.Ordinal))) return 0;
        var title = Normalize(document.Entry.Title);
        var key = Normalize(document.Entry.Key);
        var tags = Normalize(string.Join(' ', document.Entry.Tags));
        var score = title == text || key == text ? 160 : 0;
        if (title.StartsWith(text, StringComparison.Ordinal) || key.StartsWith(text, StringComparison.Ordinal)) score += 90;
        score += tokens.Sum(token => title.Contains(token, StringComparison.Ordinal) ? 30 : 0);
        score += tokens.Sum(token => tags.Contains(token, StringComparison.Ordinal) ? 20 : 0);
        score += tokens.Sum(token => document.SearchText.Contains(token, StringComparison.Ordinal) ? 5 : 0);
        return score;
    }

    private static string Normalize(string value)
    {
        var source = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record LoreDocument(LoreEntryDefinition Entry, string SearchText);
}
