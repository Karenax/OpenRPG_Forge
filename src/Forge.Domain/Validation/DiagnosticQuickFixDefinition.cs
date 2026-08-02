using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Domain.Validation;

public sealed record DiagnosticQuickFixDefinition
{
    public const int CurrentSchemaVersion = 1;

    public DiagnosticQuickFixId Id { get; }

    public int SchemaVersion { get; }

    public string Label { get; }

    public string Description { get; }

    public DiagnosticQuickFixKind Kind { get; }

    public AreaId AreaId { get; }

    public EntityId? EntityId { get; }

    public string Provenance { get; }

    public DiagnosticQuickFixDefinition(
        DiagnosticQuickFixId id,
        string label,
        string description,
        DiagnosticQuickFixKind kind,
        AreaId areaId,
        EntityId? entityId,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Diagnostic quick-fix schema '{schemaVersion}' is not supported.");
        }

        if (kind == DiagnosticQuickFixKind.ClampEntityToArea && entityId is null)
        {
            throw new ArgumentException("The clamp quick fix requires an entity ID.", nameof(entityId));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Label = label.Trim();
        Description = description.Trim();
        Kind = kind;
        AreaId = areaId;
        EntityId = entityId;
        Provenance = provenance.Trim();
    }
}
