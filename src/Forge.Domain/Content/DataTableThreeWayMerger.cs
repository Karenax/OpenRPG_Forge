using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Content;

public static class DataTableThreeWayMerger
{
    public static DataTableMergeResult Merge(
        DataTableDefinition @base,
        DataTableDefinition ours,
        DataTableDefinition theirs,
        ContentMergePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);
        if (@base.Id != ours.Id || @base.Id != theirs.Id)
        {
            throw new ArgumentException("Three-way table merge inputs must share the same stable ID.");
        }

        if (!ours.Columns.SequenceEqual(theirs.Columns) || !@base.Columns.SequenceEqual(ours.Columns))
        {
            throw new ArgumentException("Column schema merges require an explicit migration before row merging.");
        }

        var baseRows = @base.Rows.ToDictionary(row => row.Id);
        var ourRows = ours.Rows.ToDictionary(row => row.Id);
        var theirRows = theirs.Rows.ToDictionary(row => row.Id);
        var rowIds = baseRows.Keys.Concat(ourRows.Keys).Concat(theirRows.Keys).Distinct()
            .OrderBy(id => id.Value).ToArray();
        var merged = new List<DataRowDefinition>();
        var conflicts = new List<DataTableMergeConflict>();
        foreach (var rowId in rowIds)
        {
            baseRows.TryGetValue(rowId, out var baseRow);
            ourRows.TryGetValue(rowId, out var ourRow);
            theirRows.TryGetValue(rowId, out var theirRow);
            var oursChanged = !RowsEqual(baseRow, ourRow);
            var theirsChanged = !RowsEqual(baseRow, theirRow);
            if (!oursChanged && !theirsChanged)
            {
                AddIfPresent(merged, baseRow);
            }
            else if (oursChanged && !theirsChanged)
            {
                AddIfPresent(merged, ourRow);
            }
            else if (!oursChanged && theirsChanged)
            {
                AddIfPresent(merged, theirRow);
            }
            else if (RowsEqual(ourRow, theirRow))
            {
                AddIfPresent(merged, ourRow);
            }
            else
            {
                var conflict = new DataTableMergeConflict(
                    DeterministicGuid($"{@base.Id}:{rowId}:conflict"), rowId, baseRow, ourRow, theirRow,
                    $"Row '{rowId}' changed differently in both variants.");
                conflicts.Add(conflict);
                ResolveConflict(merged, conflict, policy);
            }
        }

        return new DataTableMergeResult(
            new DataTableDefinition(
                ours.Id, ours.Slug, ours.DisplayName, ours.Columns, merged, ours.OverridesTableId,
                $"three-way-merge:{policy}", ours.LicenseSpdx, "unverified", ours.SchemaVersion),
            conflicts);
    }

    private static void ResolveConflict(
        ICollection<DataRowDefinition> merged,
        DataTableMergeConflict conflict,
        ContentMergePolicy policy)
    {
        switch (policy)
        {
            case ContentMergePolicy.PreferTheirs:
                AddIfPresent(merged, conflict.Theirs);
                break;
            case ContentMergePolicy.PreserveBoth:
                AddIfPresent(merged, conflict.Ours);
                if (conflict.Theirs is not null)
                {
                    merged.Add(new DataRowDefinition(
                        DataRowId.From(DeterministicGuid($"{conflict.RowId}:theirs")),
                        conflict.Theirs.Values,
                        $"preserved-variant:{conflict.Theirs.Provenance}"));
                }
                break;
            case ContentMergePolicy.Manual:
            case ContentMergePolicy.PreferOurs:
            default:
                AddIfPresent(merged, conflict.Ours);
                break;
        }
    }

    private static void AddIfPresent(ICollection<DataRowDefinition> rows, DataRowDefinition? row)
    {
        if (row is not null)
        {
            rows.Add(row);
        }
    }

    private static bool RowsEqual(DataRowDefinition? left, DataRowDefinition? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Id != right.Id ||
            !string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) ||
            left.Values.Count != right.Values.Count)
        {
            return false;
        }

        return left.Values.All(pair =>
            right.Values.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
