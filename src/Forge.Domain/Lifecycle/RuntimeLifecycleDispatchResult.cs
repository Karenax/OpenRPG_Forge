using Forge.Domain.Validation;

namespace Forge.Domain.Lifecycle;

public sealed record RuntimeLifecycleDispatchResult(
    bool Succeeded,
    IReadOnlyList<RuntimeLifecycleEvent> Events,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    bool Applied);
