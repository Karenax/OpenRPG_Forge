using Forge.Domain.Lifecycle;

namespace Forge.Application.Lifecycle;

public sealed record RestTransitionResult(bool Accepted, string? DiagnosticCode,
    IReadOnlyList<RestLifecycleEvent> Events, RestSession? Session);
