namespace Forge.Domain.Content;

public sealed record ContentWorkspaceConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public ContentWorkspaceConfigurationId Id { get; }

    public int SchemaVersion { get; }

    public ContentBatchConfiguration Batch { get; }

    public IReadOnlyList<DataTableDefinition> DataTables { get; }

    public ContentMergePolicy MergePolicy { get; }

    public string Provenance { get; }

    public ContentWorkspaceConfiguration(
        ContentWorkspaceConfigurationId id,
        ContentBatchConfiguration batch,
        IReadOnlyList<DataTableDefinition> dataTables,
        ContentMergePolicy mergePolicy,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(dataTables);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), $"Content workspace schema '{schemaVersion}' is not supported.");
        }

        var materializedTables = dataTables.OrderBy(table => table.Slug, StringComparer.Ordinal).ToArray();
        if (materializedTables.Select(table => table.Id).Distinct().Count() != materializedTables.Length ||
            materializedTables.Select(table => table.Slug).Distinct(StringComparer.Ordinal).Count() != materializedTables.Length)
        {
            throw new ArgumentException("Data table IDs and slugs must be unique.", nameof(dataTables));
        }

        var ids = materializedTables.Select(table => table.Id).ToHashSet();
        foreach (var table in materializedTables)
        {
            if (table.OverridesTableId is not null && !ids.Contains(table.OverridesTableId))
            {
                throw new ArgumentException($"Data table '{table.Slug}' overrides missing table '{table.OverridesTableId}'.", nameof(dataTables));
            }

            foreach (var column in table.Columns.Where(column => column.Kind == DataColumnKind.Reference))
            {
                if (column.ReferenceTableId is null || !ids.Contains(column.ReferenceTableId))
                {
                    throw new ArgumentException($"Column '{table.Slug}.{column.Key}' references a missing table.", nameof(dataTables));
                }

                var targetRows = materializedTables.Single(candidate => candidate.Id == column.ReferenceTableId)
                    .Rows.Select(row => row.Id.Value.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingReference = table.Rows
                    .Select(row => row.Values.GetValueOrDefault(column.Key))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !targetRows.Contains(value));
                if (missingReference is not null)
                {
                    throw new ArgumentException(
                        $"Column '{table.Slug}.{column.Key}' references missing row '{missingReference}'.", nameof(dataTables));
                }
            }
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Batch = batch;
        DataTables = materializedTables;
        MergePolicy = mergePolicy;
        Provenance = provenance.Trim();
    }

    public static ContentWorkspaceConfiguration CreateDefault(Guid moduleId) =>
        new(ContentWorkspaceConfigurationId.From(moduleId), ContentBatchConfiguration.CreateDefault(), [],
            ContentMergePolicy.PreserveBoth, "project-author");
}
