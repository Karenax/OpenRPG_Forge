using Forge.Domain.Validation;

namespace Forge.Application.Lifecycle;

public sealed record HeartbeatAdvanceResult(IReadOnlyList<HeartbeatDispatch> Dispatches,
    IReadOnlyList<DomainDiagnostic> Diagnostics, long ElapsedMilliseconds);
