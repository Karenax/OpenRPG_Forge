using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Packages;

public sealed record ModuleManifestDependency(
    ThirdPartyDependencyId Id,
    string Name,
    string Version,
    string LicenseSpdx,
    string Owner,
    string SourceReference);
