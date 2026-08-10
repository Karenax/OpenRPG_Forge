namespace Forge.Application.Creator;

public sealed record CreatorDiagnosticQuery(
    string? Text = null,
    CreatorDiagnosticCategory? Category = null,
    CreatorDiagnosticSeverity MinimumSeverity = CreatorDiagnosticSeverity.Information,
    string? CorrelationId = null);

public static class CreatorDiagnosticConsole
{
    public static IReadOnlyList<CreatorDiagnostic> Filter(
        IReadOnlyList<CreatorDiagnostic> diagnostics,
        CreatorDiagnosticQuery query)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(query);
        var text = query.Text?.Trim();
        var correlation = query.CorrelationId?.Trim();
        return diagnostics
            .Where(item => item.Severity >= query.MinimumSeverity)
            .Where(item => query.Category is null || item.Category == query.Category)
            .Where(item => string.IsNullOrWhiteSpace(correlation) ||
                string.Equals(item.CorrelationId, correlation, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(text) || Matches(item, text))
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Matches(CreatorDiagnostic item, string text) =>
        item.Code.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        item.Message.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        item.DocumentPath.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        item.Property.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        item.Source.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        (item.CorrelationId?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
        item.Category.ToString().Contains(text, StringComparison.OrdinalIgnoreCase);
}
