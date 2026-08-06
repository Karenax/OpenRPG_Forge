using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record Model3DProviderRequest(
    Guid RequestId,
    Model3DInputKind InputKind,
    string Prompt,
    byte[]? ImageContent,
    string? ImageSha256,
    int Seed,
    int MaximumTriangles,
    bool NetworkAuthorized);
