using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Serialization;

internal static class AssetGovernanceJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(IReadOnlyList<AssetGovernanceRecord> records)
    {
        var document = new Document(AssetGovernanceRecord.CurrentSchemaVersion,
            records.OrderBy(record => record.AssetId.ToString(), StringComparer.Ordinal)
                .Select(ToDto).ToArray());
        return JsonSerializer.Serialize(document, Options) + "\n";
    }

    public static IReadOnlyList<AssetGovernanceRecord> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("Asset governance document is empty.");
        if (document.SchemaVersion != AssetGovernanceRecord.CurrentSchemaVersion)
            throw new JsonException($"Asset governance schema '{document.SchemaVersion}' is unsupported.");
        return (document.Records ?? []).Select(FromDto)
            .OrderBy(record => record.AssetId.ToString(), StringComparer.Ordinal).ToArray();
    }

    private static RecordDto ToDto(AssetGovernanceRecord record) => new(
        record.SchemaVersion, record.Id.ToString(), record.AssetId.ToString(), record.PackageManifestId.ToString(),
        record.LicenseSpdx, record.ContentSha256, record.Author, record.SourceUrl, record.PermissionEvidence,
        record.Modifications, record.AiGenerated, record.AiModel, record.AiPromptPolicySha256,
        record.PlatformBudgetBytes, record.Quarantine, record.IpBoundary);

    private static AssetGovernanceRecord FromDto(RecordDto dto) => new(
        dto.SchemaVersion,
        AssetGovernanceId.From(Guid.Parse(dto.Id)),
        AssetCatalogItemId.Parse(dto.AssetId),
        AssetPackageManifestId.Parse(dto.PackageManifestId),
        dto.LicenseSpdx,
        dto.ContentSha256,
        dto.Author,
        dto.SourceUrl,
        dto.PermissionEvidence,
        dto.Modifications,
        dto.AiGenerated,
        dto.AiModel,
        dto.AiPromptPolicySha256,
        dto.PlatformBudgetBytes,
        dto.Quarantine ?? throw new JsonException("Quarantine review is required."),
        dto.IpBoundary ?? throw new JsonException("IP boundary is required."));

    private sealed record Document(int SchemaVersion, IReadOnlyList<RecordDto>? Records);
    private sealed record RecordDto(
        int SchemaVersion,
        string Id,
        string AssetId,
        string PackageManifestId,
        string LicenseSpdx,
        string ContentSha256,
        string Author,
        string SourceUrl,
        string PermissionEvidence,
        string Modifications,
        bool AiGenerated,
        string AiModel,
        string AiPromptPolicySha256,
        long PlatformBudgetBytes,
        AssetQuarantineReview? Quarantine,
        AssetIpBoundary? IpBoundary);
}
