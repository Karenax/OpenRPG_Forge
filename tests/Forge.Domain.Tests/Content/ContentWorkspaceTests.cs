using Forge.Domain.Content;

namespace Forge.Domain.Tests.Content;

public sealed class ContentWorkspaceTests
{
    [Fact]
    public void TablesValidateTypedCellsReferencesAndOverrides()
    {
        var itemTableId = DataTableId.From(Guid.Parse("e1000000-0000-4000-8000-000000000001"));
        var rowId = DataRowId.From(Guid.Parse("e1000000-0000-4000-8000-000000000002"));
        var itemTable = new DataTableDefinition(
            itemTableId,
            "items",
            "Items",
            [new DataColumnDefinition(
                DataColumnId.From(Guid.Parse("e1000000-0000-4000-8000-000000000003")),
                "name", "Name", DataColumnKind.Text, required: true)],
            [new DataRowDefinition(rowId, new Dictionary<string, string> { ["name"] = "Forge sword" })],
            licenseSpdx: "CC0-1.0",
            contentHash: new string('a', 64));
        var lootTable = new DataTableDefinition(
            DataTableId.From(Guid.Parse("e1000000-0000-4000-8000-000000000004")),
            "loot",
            "Loot",
            [
                new DataColumnDefinition(
                    DataColumnId.From(Guid.Parse("e1000000-0000-4000-8000-000000000005")),
                    "item", "Item", DataColumnKind.Reference, required: true, itemTableId),
                new DataColumnDefinition(
                    DataColumnId.From(Guid.Parse("e1000000-0000-4000-8000-000000000006")),
                    "weight", "Weight", DataColumnKind.Decimal, required: true),
            ],
            [new DataRowDefinition(
                DataRowId.From(Guid.Parse("e1000000-0000-4000-8000-000000000007")),
                new Dictionary<string, string>
                {
                    ["item"] = rowId.ToString(),
                    ["weight"] = "1.25",
                })],
            licenseSpdx: "CC0-1.0",
            contentHash: new string('b', 64));

        var workspace = new ContentWorkspaceConfiguration(
            ContentWorkspaceConfigurationId.From(Guid.Parse("e1000000-0000-4000-8000-000000000008")),
            ContentBatchConfiguration.CreateDefault(),
            [itemTable, lootTable],
            ContentMergePolicy.PreserveBoth,
            "test");

        Assert.Equal(2, workspace.DataTables.Count);
        Assert.Equal(DataColumnKind.Reference, workspace.DataTables.Single(table => table.Slug == "loot").Columns[0].Kind);
        Assert.Throws<ArgumentException>(() => new ContentWorkspaceConfiguration(
            workspace.Id,
            workspace.Batch,
            [lootTable],
            workspace.MergePolicy,
            workspace.Provenance));
    }

    [Fact]
    public void ThreeWayMergePreservesBothConflictingVariantsWithStableIds()
    {
        var tableId = DataTableId.From(Guid.Parse("e1100000-0000-4000-8000-000000000001"));
        var rowId = DataRowId.From(Guid.Parse("e1100000-0000-4000-8000-000000000002"));
        var column = new DataColumnDefinition(
            DataColumnId.From(Guid.Parse("e1100000-0000-4000-8000-000000000003")),
            "name", "Name", DataColumnKind.Text, required: true);
        DataTableDefinition Table(string value, string provenance) => new(
            tableId,
            "encounters",
            "Encounters",
            [column],
            [new DataRowDefinition(rowId, new Dictionary<string, string> { ["name"] = value }, provenance)]);

        var first = DataTableThreeWayMerger.Merge(
            Table("Base", "base"), Table("Ours", "ours"), Table("Theirs", "theirs"), ContentMergePolicy.PreserveBoth);
        var second = DataTableThreeWayMerger.Merge(
            Table("Base", "base"), Table("Ours", "ours"), Table("Theirs", "theirs"), ContentMergePolicy.PreserveBoth);

        Assert.Single(first.Conflicts);
        Assert.Equal(2, first.Merged.Rows.Count);
        Assert.Contains(first.Merged.Rows, row => row.Values["name"] == "Ours");
        Assert.Contains(first.Merged.Rows, row => row.Values["name"] == "Theirs");
        Assert.Equal(first.Merged.Rows.Select(row => row.Id), second.Merged.Rows.Select(row => row.Id));
        Assert.Equal(first.Conflicts[0].ConflictId, second.Conflicts[0].ConflictId);
    }
}
