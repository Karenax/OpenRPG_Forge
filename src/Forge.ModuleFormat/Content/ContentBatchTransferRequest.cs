using Forge.Domain.Content;

namespace Forge.ModuleFormat.Content;

public sealed record ContentBatchTransferRequest
{
    public ContentBatchOperation Operation { get; }

    public string SourceRoot { get; }

    public string DestinationRoot { get; }

    public string Glob { get; }

    public IReadOnlyList<string> Extensions { get; }

    public ContentBatchCollisionPolicy CollisionPolicy { get; }

    public string Provenance { get; }

    public string LicenseSpdx { get; }

    public bool DryRun { get; }

    public ContentBatchTransferRequest(
        ContentBatchOperation operation,
        string sourceRoot,
        string destinationRoot,
        string glob,
        IReadOnlyList<string> extensions,
        ContentBatchCollisionPolicy collisionPolicy,
        string provenance,
        string licenseSpdx,
        bool dryRun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        var configuration = new ContentBatchConfiguration(glob, extensions, collisionPolicy, provenance, licenseSpdx);
        Operation = operation;
        SourceRoot = Path.GetFullPath(sourceRoot);
        DestinationRoot = Path.GetFullPath(destinationRoot);
        Glob = configuration.Glob;
        Extensions = configuration.Extensions;
        CollisionPolicy = configuration.CollisionPolicy;
        Provenance = configuration.Provenance;
        LicenseSpdx = configuration.LicenseSpdx;
        DryRun = dryRun;
    }
}
