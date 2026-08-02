using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Creator;

public sealed record ApplyDiagnosticQuickFixCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    DiagnosticId DiagnosticId,
    DiagnosticQuickFixId QuickFixId,
    bool DryRun);
