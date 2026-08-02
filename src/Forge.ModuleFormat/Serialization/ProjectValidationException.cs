using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Serialization;

public sealed class ProjectValidationException : Exception
{
    public IReadOnlyList<DomainDiagnostic> Diagnostics { get; }

    public ProjectValidationException(IReadOnlyList<DomainDiagnostic> diagnostics)
        : base("The project document contains invalid domain references.")
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = diagnostics.ToArray();
    }
}
