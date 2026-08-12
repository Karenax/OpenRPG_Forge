using System.Text.Json;
using Forge.Domain.Quests;

namespace Forge.ModuleFormat.Serialization;

/// <summary>Deterministic serializer for the quest quality extension inside a validated quest aggregate.</summary>
public static class QuestQualityProfileJsonSerializer
{
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    public static string Serialize(QuestQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    public static QuestQualityProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<QuestQualityProfile>(json, Options)
               ?? throw new JsonException("The quest quality profile is empty.");
    }
}
