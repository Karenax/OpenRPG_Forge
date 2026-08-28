using System.Text;
using System.Text.Json;

namespace Forge.ModuleFormat.Workspaces;

internal static class ProjectWorkspaceTransaction
{
    private const int CurrentSchemaVersion = 1;
    private const string TransactionsRelativePath = ".forge/project-transactions";
    private const string JournalFileName = "journal.json";
    private const string CommittedMarkerFileName = "committed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly AsyncLocal<Action<int>?> TestWriteProbeSlot = new();

    internal static Action<int>? TestWriteProbe
    {
        get => TestWriteProbeSlot.Value;
        set => TestWriteProbeSlot.Value = value;
    }

    internal static bool HasPending(string rootDirectory)
    {
        var transactionsRoot = TransactionsRoot(rootDirectory);
        return Directory.Exists(transactionsRoot) &&
               Directory.EnumerateDirectories(transactionsRoot).Any();
    }

    internal static void Commit(string rootDirectory, IReadOnlyDictionary<string, string> documents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0) return;

        var canonicalRoot = CanonicalRoot(rootDirectory);
        var transactionsRoot = TransactionsRoot(canonicalRoot);
        Directory.CreateDirectory(transactionsRoot);
        var transactionDirectory = Path.Combine(transactionsRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(transactionDirectory);

        var entries = new List<TransactionEntry>(documents.Count);
        var index = 0;
        foreach (var document in documents.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var targetPath = ResolveContainedPath(canonicalRoot, document.Key);
            var stagedFile = $"{index:D4}.new";
            var backupFile = $"{index:D4}.bak";
            var stagedPath = Path.Combine(transactionDirectory, stagedFile);
            var backupPath = Path.Combine(transactionDirectory, backupFile);
            var existed = File.Exists(targetPath);
            if (existed)
            {
                WriteAllBytesDurably(backupPath, File.ReadAllBytes(targetPath));
            }

            WriteAllTextDurably(stagedPath, document.Value);
            entries.Add(new TransactionEntry(
                NormalizeRelativePath(document.Key),
                stagedFile,
                backupFile,
                existed));
            index++;
        }

        var journal = new TransactionJournal(CurrentSchemaVersion, entries);
        WriteAllTextDurably(
            Path.Combine(transactionDirectory, JournalFileName),
            JsonSerializer.Serialize(journal, JsonOptions));

        try
        {
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                var targetPath = ResolveContainedPath(canonicalRoot, entry.TargetRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Move(
                    Path.Combine(transactionDirectory, entry.StagedFile),
                    targetPath,
                    overwrite: true);
                TestWriteProbe?.Invoke(entryIndex + 1);
            }

            WriteAllTextDurably(Path.Combine(transactionDirectory, CommittedMarkerFileName), "committed\n");
            TryDeleteTransactionDirectory(transactionDirectory, transactionsRoot);
        }
        catch (Exception exception) when (exception is not ProjectWorkspaceSimulatedCrashException)
        {
            RollBack(canonicalRoot, transactionDirectory, journal);
            throw;
        }
    }

    internal static void RecoverPending(string rootDirectory)
    {
        var canonicalRoot = CanonicalRoot(rootDirectory);
        var transactionsRoot = TransactionsRoot(canonicalRoot);
        if (!Directory.Exists(transactionsRoot)) return;

        foreach (var transactionDirectory in Directory.EnumerateDirectories(transactionsRoot).Order(StringComparer.Ordinal))
        {
            var journalPath = Path.Combine(transactionDirectory, JournalFileName);
            if (!File.Exists(journalPath))
            {
                TryDeleteTransactionDirectory(transactionDirectory, transactionsRoot);
                continue;
            }

            var journal = JsonSerializer.Deserialize<TransactionJournal>(
                File.ReadAllText(journalPath),
                JsonOptions);
            if (journal is null || journal.SchemaVersion != CurrentSchemaVersion || journal.Entries.Count == 0)
            {
                throw new JsonException($"Invalid Forge workspace transaction journal: {journalPath}");
            }

            if (!File.Exists(Path.Combine(transactionDirectory, CommittedMarkerFileName)))
            {
                RollBack(canonicalRoot, transactionDirectory, journal);
            }
            else
            {
                TryDeleteTransactionDirectory(transactionDirectory, transactionsRoot);
            }
        }
    }

    private static void RollBack(
        string canonicalRoot,
        string transactionDirectory,
        TransactionJournal journal)
    {
        foreach (var entry in journal.Entries.Reverse())
        {
            var targetPath = ResolveContainedPath(canonicalRoot, entry.TargetRelativePath);
            if (entry.Existed)
            {
                var backupPath = ResolveTransactionFile(transactionDirectory, entry.BackupFile);
                if (!File.Exists(backupPath))
                {
                    throw new IOException($"Workspace transaction backup is missing: {backupPath}");
                }

                WriteAllBytesAtomically(targetPath, File.ReadAllBytes(backupPath));
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        TryDeleteTransactionDirectory(transactionDirectory, TransactionsRoot(canonicalRoot));
    }

    private static string ResolveContainedPath(string canonicalRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new IOException("A workspace transaction path must be relative.");
        }

        var candidate = Path.GetFullPath(Path.Combine(
            canonicalRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"Workspace transaction path escaped the project: {relativePath}");
        }

        return candidate;
    }

    private static string ResolveTransactionFile(string transactionDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new IOException("A workspace transaction file name is unsafe.");
        }

        return Path.Combine(transactionDirectory, fileName);
    }

    private static string CanonicalRoot(string rootDirectory) =>
        Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
        Path.DirectorySeparatorChar;

    private static string TransactionsRoot(string rootDirectory) =>
        Path.Combine(
            rootDirectory,
            TransactionsRelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/');

    private static void WriteAllTextDurably(string path, string content) =>
        WriteAllBytesDurably(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));

    private static void WriteAllBytesDurably(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteAllBytesAtomically(string targetPath, byte[] content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("A workspace transaction target has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            WriteAllBytesDurably(temporaryPath, content);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void TryDeleteTransactionDirectory(string transactionDirectory, string transactionsRoot)
    {
        var canonicalTransactionsRoot = CanonicalRoot(transactionsRoot);
        var canonicalTransactionDirectory = Path.GetFullPath(transactionDirectory);
        if (!canonicalTransactionDirectory.StartsWith(canonicalTransactionsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Refusing to remove a workspace transaction outside its transaction root.");
        }

        try
        {
            Directory.Delete(canonicalTransactionDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A committed journal is safe to finalize on the next workspace open.
        }
        catch (UnauthorizedAccessException)
        {
            // A committed journal is safe to finalize on the next workspace open.
        }
    }

    private sealed record TransactionJournal(int SchemaVersion, IReadOnlyList<TransactionEntry> Entries);

    private sealed record TransactionEntry(
        string TargetRelativePath,
        string StagedFile,
        string BackupFile,
        bool Existed);
}

internal sealed class ProjectWorkspaceSimulatedCrashException : Exception
{
    internal ProjectWorkspaceSimulatedCrashException(string message)
        : base(message)
    {
    }
}
