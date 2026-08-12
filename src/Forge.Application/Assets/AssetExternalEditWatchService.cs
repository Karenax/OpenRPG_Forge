using System.Security.Cryptography;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.Application.Assets;

public sealed record AssetExternalEditSnapshot(
    string ContentSha256,
    long Bytes,
    DateTimeOffset LastWriteUtc,
    string Extension);

public sealed record AssetExternalEditMetadataChange(
    string Field,
    string Before,
    string After);

public sealed record AssetExternalReimportPlan(
    AssetExternalEditId ConfigurationId,
    string SourceRelativePath,
    string OutputRelativePath,
    AssetExternalTool Tool,
    IReadOnlyList<AssetExternalOperation> Operations,
    IReadOnlyList<AssetTargetPlatform> TargetPlatforms,
    IReadOnlyList<AssetExternalEditMetadataChange> MetadataChanges,
    bool DryRun,
    string ModificationProvenance,
    string LicenseSpdx);

public sealed class AssetExternalEditWatchService : IDisposable
{
    public const string SourceMissingCode = "asset.external_edit.watch_source_missing";
    public const string SourcePathInvalidCode = "asset.external_edit.watch_source_path_invalid";

    private readonly AssetExternalEditConfiguration _configuration;
    private readonly string _sourcePath;
    private readonly FileSystemWatcher? _watcher;
    private AssetExternalEditSnapshot _baseline;
    private bool _pending;

    private AssetExternalEditWatchService(
        AssetExternalEditConfiguration configuration,
        string sourcePath,
        AssetExternalEditSnapshot baseline,
        FileSystemWatcher? watcher)
    {
        _configuration = configuration;
        _sourcePath = sourcePath;
        _baseline = baseline;
        _watcher = watcher;
        if (_watcher is null)
        {
            return;
        }

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public bool HasPendingChange => _pending;

    public static AssetExternalEditWatchOpenResult Open(
        string sourceRoot,
        AssetExternalEditConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentNullException.ThrowIfNull(configuration);
        var diagnostic = AssetExternalEditValidator.Validate(configuration).FirstOrDefault();
        if (diagnostic is not null)
        {
            return new(null, [diagnostic]);
        }

        var root = Path.GetFullPath(sourceRoot);
        var sourcePath = Path.GetFullPath(Path.Combine(root, configuration.SourceRelativePath));
        if (!sourcePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return new(null, [new DomainDiagnostic(
                SourcePathInvalidCode,
                "External edit watch source must remain inside its configured root.",
                $"assets/external-edits/{configuration.Id}/sourceRelativePath")]);
        }

        if (!File.Exists(sourcePath))
        {
            return new(null, [new DomainDiagnostic(
                SourceMissingCode,
                "External edit watch source does not exist.",
                $"assets/external-edits/{configuration.Id}/sourceRelativePath")]);
        }

        var watcher = configuration.WatchForChanges
            ? new FileSystemWatcher(Path.GetDirectoryName(sourcePath)!, Path.GetFileName(sourcePath))
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            }
            : null;
        return new(new AssetExternalEditWatchService(configuration, sourcePath, Capture(sourcePath), watcher), []);
    }

    public AssetExternalReimportPlan? Poll(bool dryRun = true)
    {
        var current = Capture(_sourcePath);
        var changes = Diff(_baseline, current);
        _pending = _pending || changes.Count > 0;
        if (!_pending || changes.Count == 0)
        {
            return null;
        }

        return new(
            _configuration.Id,
            _configuration.SourceRelativePath,
            _configuration.OutputRelativePath,
            _configuration.Tool,
            _configuration.Operations,
            _configuration.TargetPlatforms,
            changes,
            dryRun,
            _configuration.ModificationProvenance,
            _configuration.LicenseSpdx);
    }

    public void Acknowledge()
    {
        _baseline = Capture(_sourcePath);
        _pending = false;
    }

    public void Dispose()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnChanged;
        _watcher.Created -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs args) => _pending = true;

    private void OnRenamed(object sender, RenamedEventArgs args) => _pending = true;

    private static AssetExternalEditSnapshot Capture(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var info = new FileInfo(path);
        return new(hash, info.Length, info.LastWriteTimeUtc, info.Extension.ToLowerInvariant());
    }

    private static IReadOnlyList<AssetExternalEditMetadataChange> Diff(
        AssetExternalEditSnapshot baseline,
        AssetExternalEditSnapshot current)
    {
        var changes = new List<AssetExternalEditMetadataChange>();
        Add("contentSha256", baseline.ContentSha256, current.ContentSha256);
        Add("bytes", baseline.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            current.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("extension", baseline.Extension, current.Extension);
        return changes;

        void Add(string field, string before, string after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add(new(field, before, after));
            }
        }
    }
}

public sealed record AssetExternalEditWatchOpenResult(
    AssetExternalEditWatchService? Service,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Service is not null && Diagnostics.Count == 0;
}
