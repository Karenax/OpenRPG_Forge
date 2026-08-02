using Forge.Domain.Blueprints;

namespace Forge.Application.Blueprints;

public sealed record BlueprintReference(
    BlueprintId BlueprintId,
    BlueprintReferenceKind Kind,
    string SourceId,
    string DisplayName,
    string DocumentPath);
