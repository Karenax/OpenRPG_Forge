using Forge.Application.Commands;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class EditorCommandJournalStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-journal-tests", Guid.NewGuid().ToString("N"));

    public EditorCommandJournalStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void SaveLoadRoundTripsSerializableAtomicCommandEnvelope()
    {
        var entry = Entry();

        EditorCommandJournalStore.Save(_root, [entry]);
        var restored = EditorCommandJournalStore.Load(_root);

        Assert.Equal(entry, Assert.Single(restored));
        Assert.True(File.Exists(Path.Combine(_root, EditorCommandJournalStore.RelativePath)));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, ".forge"), "*.tmp"));
    }

    [Fact]
    public void InvalidEnvelopeIsRejectedBeforeWriting()
    {
        var invalid = Entry() with { Id = Guid.Empty };

        Assert.Throws<InvalidDataException>(() => EditorCommandJournalStore.Save(_root, [invalid]));
        Assert.False(File.Exists(Path.Combine(_root, EditorCommandJournalStore.RelativePath)));
    }

    private static CommandHistoryJournalEntry Entry() => new(
        Guid.Parse("d3300000-0000-4000-8000-000000000010"),
        CommandHistoryJournalEntry.CurrentSchemaVersion, "journal-test", "PlaceEntity",
        new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero), 4, 5,
        new ProjectPatch("add", "areas/test/entities/test", "Add entity"),
        new ProjectPatch("remove", "areas/test/entities/test", "Remove entity"), true);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
