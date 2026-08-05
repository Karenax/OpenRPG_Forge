using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.ModuleFormat.Serialization;

internal sealed class ArtisticStyleProfileIdJsonConverter : JsonConverter<ArtisticStyleProfileId>
{
    public override ArtisticStyleProfileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ArtisticStyleProfileId.Parse(reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An artistic style profile ID cannot be null.")
            : throw new JsonException("An artistic style profile ID must be a string."));

    public override void Write(Utf8JsonWriter writer, ArtisticStyleProfileId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class ContentWorkspaceConfigurationIdJsonConverter : JsonConverter<ContentWorkspaceConfigurationId>
{
    public override ContentWorkspaceConfigurationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ContentWorkspaceConfigurationId.Parse(ReadString(ref reader, "A content workspace configuration ID"));

    public override void Write(Utf8JsonWriter writer, ContentWorkspaceConfigurationId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader, string label) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException($"{label} cannot be null.")
            : throw new JsonException($"{label} must be a string.");
}

internal sealed class DataTableIdJsonConverter : JsonConverter<DataTableId>
{
    public override DataTableId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DataTableId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DataTableId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() ?? throw new JsonException("A data table ID cannot be null.")
        : throw new JsonException("A data table ID must be a string.");
}

internal sealed class DataColumnIdJsonConverter : JsonConverter<DataColumnId>
{
    public override DataColumnId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DataColumnId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DataColumnId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() ?? throw new JsonException("A data column ID cannot be null.")
        : throw new JsonException("A data column ID must be a string.");
}

internal sealed class DataRowIdJsonConverter : JsonConverter<DataRowId>
{
    public override DataRowId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DataRowId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, DataRowId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() ?? throw new JsonException("A data row ID cannot be null.")
        : throw new JsonException("A data row ID must be a string.");
}

internal sealed class AssetCatalogItemIdJsonConverter : JsonConverter<AssetCatalogItemId>
{
    public override AssetCatalogItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        AssetCatalogItemId.Parse(reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An asset catalog item ID cannot be null.")
            : throw new JsonException("An asset catalog item ID must be a string."));

    public override void Write(Utf8JsonWriter writer, AssetCatalogItemId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class AssetPackageManifestIdJsonConverter : JsonConverter<AssetPackageManifestId>
{
    public override AssetPackageManifestId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        AssetPackageManifestId.Parse(reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An asset package manifest ID cannot be null.")
            : throw new JsonException("An asset package manifest ID must be a string."));

    public override void Write(
        Utf8JsonWriter writer,
        AssetPackageManifestId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class AssetExternalEditIdJsonConverter : JsonConverter<AssetExternalEditId>
{
    public override AssetExternalEditId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => AssetExternalEditId.Parse(ReadString(ref reader));

    public override void Write(
        Utf8JsonWriter writer,
        AssetExternalEditId value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An external edit ID cannot be null.")
            : throw new JsonException("An external edit ID must be a string.");
}

internal sealed class LocalizationCatalogIdJsonConverter : JsonConverter<LocalizationCatalogId>
{
    public override LocalizationCatalogId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LocalizationCatalogId.Parse(reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A localization catalog ID cannot be null.")
            : throw new JsonException("A localization catalog ID must be a string."));

    public override void Write(Utf8JsonWriter writer, LocalizationCatalogId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class EditorCameraProfileIdJsonConverter : JsonConverter<EditorCameraProfileId>
{
    public override EditorCameraProfileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        EditorCameraProfileId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, EditorCameraProfileId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An editor camera profile ID cannot be null.")
            : throw new JsonException("An editor camera profile ID must be a string.");
}

internal sealed class EditorCameraBookmarkIdJsonConverter : JsonConverter<EditorCameraBookmarkId>
{
    public override EditorCameraBookmarkId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        EditorCameraBookmarkId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, EditorCameraBookmarkId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An editor camera bookmark ID cannot be null.")
            : throw new JsonException("An editor camera bookmark ID must be a string.");
}

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

internal sealed class ModuleDistributionConfigurationIdJsonConverter :
    JsonConverter<ModuleDistributionConfigurationId>
{
    public override ModuleDistributionConfigurationId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ModuleDistributionConfigurationId.Parse(ReadString(ref reader));

    public override void Write(
        Utf8JsonWriter writer,
        ModuleDistributionConfigurationId value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A module distribution configuration ID cannot be null.")
            : throw new JsonException("A module distribution configuration ID must be a string.");
}

internal sealed class ModuleReleaseConfigurationIdJsonConverter : JsonConverter<ModuleReleaseConfigurationId>
{
    public override ModuleReleaseConfigurationId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ModuleReleaseConfigurationId.Parse(ReadString(ref reader));

    public override void Write(
        Utf8JsonWriter writer,
        ModuleReleaseConfigurationId value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A module release configuration ID cannot be null.")
            : throw new JsonException("A module release configuration ID must be a string.");
}

internal sealed class ModuleTargetProfileIdJsonConverter : JsonConverter<ModuleTargetProfileId>
{
    public override ModuleTargetProfileId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ModuleTargetProfileId.Parse(ReadString(ref reader));

    public override void Write(
        Utf8JsonWriter writer,
        ModuleTargetProfileId value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A module target profile ID cannot be null.")
            : throw new JsonException("A module target profile ID must be a string.");
}

internal sealed class ThirdPartyDependencyIdJsonConverter : JsonConverter<ThirdPartyDependencyId>
{
    public override ThirdPartyDependencyId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => ThirdPartyDependencyId.Parse(ReadString(ref reader));

    public override void Write(
        Utf8JsonWriter writer,
        ThirdPartyDependencyId value,
        JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A third-party dependency ID cannot be null.")
            : throw new JsonException("A third-party dependency ID must be a string.");
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

internal sealed class AreaNavigationDefinitionIdJsonConverter : JsonConverter<AreaNavigationDefinitionId>
{
    public override AreaNavigationDefinitionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        AreaNavigationDefinitionId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, AreaNavigationDefinitionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An area navigation definition ID cannot be null.")
            : throw new JsonException("An area navigation definition ID must be a string.");
}

internal sealed class NavigationAgentProfileIdJsonConverter : JsonConverter<NavigationAgentProfileId>
{
    public override NavigationAgentProfileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        NavigationAgentProfileId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, NavigationAgentProfileId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A navigation agent profile ID cannot be null.")
            : throw new JsonException("A navigation agent profile ID must be a string.");
}

internal sealed class OffMeshNavigationLinkIdJsonConverter : JsonConverter<OffMeshNavigationLinkId>
{
    public override OffMeshNavigationLinkId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        OffMeshNavigationLinkId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, OffMeshNavigationLinkId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("An off-mesh navigation link ID cannot be null.")
            : throw new JsonException("An off-mesh navigation link ID must be a string.");
}

internal sealed class NavigationSurfaceIdJsonConverter : JsonConverter<NavigationSurfaceId>
{
    public override NavigationSurfaceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        NavigationSurfaceId.Parse(ReadString(ref reader));

    public override void Write(Utf8JsonWriter writer, NavigationSurfaceId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static string ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("A navigation surface ID cannot be null.")
            : throw new JsonException("A navigation surface ID must be a string.");
}
