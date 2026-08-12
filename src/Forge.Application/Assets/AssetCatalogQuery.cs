using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetCatalogQuery(
    string? Text = null,
    string? Source = null,
    string? Category = null,
    string? LicenseSpdx = null,
    AssetAvailability? Availability = null,
    int MaximumResults = 300,
    bool? Installed = null,
    string? Tag = null,
    double? MaximumWidth = null,
    double? MaximumHeight = null,
    double? MaximumDepth = null,
    string? Style = null,
    string? Skeleton = null,
    string? Material = null,
    long? MaximumTriangleCount = null,
    string? Author = null,
    string? Compatibility = null,
    string? Variant = null);
