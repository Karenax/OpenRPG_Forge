using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetCatalogQuery(
    string? Text = null,
    string? Source = null,
    string? Category = null,
    string? LicenseSpdx = null,
    AssetAvailability? Availability = null,
    int MaximumResults = 300,
    bool? Installed = null);
