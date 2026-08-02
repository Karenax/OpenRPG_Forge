using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.ModuleFormat.Serialization;

internal sealed class AreaIdJsonConverter : JsonConverter<AreaId>
{
    public override AreaId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        AreaId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, AreaId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An area ID cannot be null.")
            : throw new JsonException("An area ID must be a string.");
}

internal sealed class PortalIdJsonConverter : JsonConverter<PortalId>
{
    public override PortalId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        PortalId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, PortalId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A portal ID cannot be null.")
            : throw new JsonException("A portal ID must be a string.");
}

internal sealed class BlueprintIdJsonConverter : JsonConverter<BlueprintId>
{
    public override BlueprintId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BlueprintId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, BlueprintId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A blueprint ID cannot be null.")
            : throw new JsonException("A blueprint ID must be a string.");
}

internal sealed class AbilityIdJsonConverter : JsonConverter<AbilityId>
{
    public override AbilityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        AbilityId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, AbilityId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An ability ID cannot be null.")
            : throw new JsonException("An ability ID must be a string.");
}

internal sealed class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        EntityId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An entity ID cannot be null.")
            : throw new JsonException("An entity ID must be a string.");
}

internal sealed class DialogueIdJsonConverter : JsonConverter<DialogueId>
{
    public override DialogueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DialogueId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DialogueId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A dialogue ID cannot be null.")
            : throw new JsonException("A dialogue ID must be a string.");
}

internal sealed class DialogueNodeIdJsonConverter : JsonConverter<DialogueNodeId>
{
    public override DialogueNodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DialogueNodeId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DialogueNodeId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A dialogue node ID cannot be null.")
            : throw new JsonException("A dialogue node ID must be a string.");
}

internal sealed class DialogueChoiceIdJsonConverter : JsonConverter<DialogueChoiceId>
{
    public override DialogueChoiceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DialogueChoiceId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DialogueChoiceId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A dialogue choice ID cannot be null.")
            : throw new JsonException("A dialogue choice ID must be a string.");
}

internal sealed class ProjectIdJsonConverter : JsonConverter<ProjectId>
{
    public override ProjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ProjectId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, ProjectId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A project ID cannot be null.")
            : throw new JsonException("A project ID must be a string.");
}

internal sealed class ModuleIdJsonConverter : JsonConverter<ModuleId>
{
    public override ModuleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ModuleId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, ModuleId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A module ID cannot be null.")
            : throw new JsonException("A module ID must be a string.");
}

internal sealed class ProjectMetadataIdJsonConverter : JsonConverter<ProjectMetadataId>
{
    public override ProjectMetadataId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ProjectMetadataId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, ProjectMetadataId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A project metadata ID cannot be null.")
            : throw new JsonException("A project metadata ID must be a string.");
}

internal sealed class ProjectVariableIdJsonConverter : JsonConverter<ProjectVariableId>
{
    public override ProjectVariableId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ProjectVariableId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, ProjectVariableId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A project variable ID cannot be null.")
            : throw new JsonException("A project variable ID must be a string.");
}

internal sealed class QuestIdJsonConverter : JsonConverter<QuestId>
{
    public override QuestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        QuestId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, QuestId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A quest ID cannot be null.")
            : throw new JsonException("A quest ID must be a string.");
}

internal sealed class QuestStateIdJsonConverter : JsonConverter<QuestStateId>
{
    public override QuestStateId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        QuestStateId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, QuestStateId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A quest state ID cannot be null.")
            : throw new JsonException("A quest state ID must be a string.");
}

internal sealed class QuestTransitionIdJsonConverter : JsonConverter<QuestTransitionId>
{
    public override QuestTransitionId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => QuestTransitionId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, QuestTransitionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A quest transition ID cannot be null.")
            : throw new JsonException("A quest transition ID must be a string.");
}
