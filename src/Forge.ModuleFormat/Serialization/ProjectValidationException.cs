using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Serialization;

public sealed class ProjectValidationException : Exception
{
    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }

    public ProjectValidationException(IReadOnlyList<DomainDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics))
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = diagnostics.ToArray();
    }

    private static string BuildMessage(IReadOnlyList<DomainDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Count == 0)
        {
            return "The project document contains invalid domain references.";
        }

        return "The project document contains invalid domain references: " +
            string.Join(
                "; ",
                diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} at {diagnostic.Path}: {diagnostic.Message}"));
    }
}
