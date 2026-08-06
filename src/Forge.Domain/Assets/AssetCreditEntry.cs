namespace Forge.Domain.Assets;

public sealed record AssetCreditEntry(
    string Source,
    string Author,
    string LicenseSpdx,
    string LicenseUrl,
    bool AttributionRequired,
    string AttributionText,
    IReadOnlyList<string> AssetExternalIds,
    IReadOnlyList<string> SourcePages);
