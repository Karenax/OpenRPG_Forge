namespace Forge.Domain.Sequences;

public sealed record StartupFlowDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public StartupFlowKind Kind { get; }
    public Guid? ReferenceId { get; }
    public string ResourcePath { get; }
    public StartupFlowKind FallbackKind { get; }
    public Guid? FallbackReferenceId { get; }
    public string FallbackResourcePath { get; }
    public bool Skippable { get; }
    public string SkipLabel { get; }
    public string Provenance { get; }

    public StartupFlowDefinition(Guid id, int schemaVersion, StartupFlowKind kind, Guid? referenceId,
        string resourcePath, StartupFlowKind fallbackKind, Guid? fallbackReferenceId, string fallbackResourcePath,
        bool skippable, string skipLabel, string provenance)
    {
        ArgumentNullException.ThrowIfNull(resourcePath);
        ArgumentNullException.ThrowIfNull(fallbackResourcePath);
        ArgumentNullException.ThrowIfNull(skipLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || referenceId == Guid.Empty ||
            fallbackReferenceId == Guid.Empty ||
            kind == StartupFlowKind.Sequence && referenceId is null ||
            kind is StartupFlowKind.Video or StartupFlowKind.Scene && string.IsNullOrWhiteSpace(resourcePath) ||
            fallbackKind == StartupFlowKind.Sequence && fallbackReferenceId is null ||
            fallbackKind is StartupFlowKind.Video or StartupFlowKind.Scene &&
            string.IsNullOrWhiteSpace(fallbackResourcePath) || skippable && string.IsNullOrWhiteSpace(skipLabel))
        {
            throw new ArgumentException("Startup flow is invalid.", nameof(kind));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Kind = kind;
        ReferenceId = referenceId;
        ResourcePath = resourcePath.Trim().Replace('\\', '/');
        FallbackKind = fallbackKind;
        FallbackReferenceId = fallbackReferenceId;
        FallbackResourcePath = fallbackResourcePath.Trim().Replace('\\', '/');
        Skippable = skippable;
        SkipLabel = skipLabel.Trim();
        Provenance = provenance.Trim();
    }
}
