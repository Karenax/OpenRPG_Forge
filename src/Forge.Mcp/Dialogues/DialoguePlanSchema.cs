namespace Forge.Mcp.Dialogues;

public static class DialoguePlanSchema
{
    public const int CurrentVersion = 1;

    public const string Json = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "$id": "https://openrpg-forge.local/schemas/dialogue-plan-v1.json",
      "title": "OpenRPG Forge dialogue mutation plan",
      "type": "object",
      "additionalProperties": false,
      "required": ["schemaVersion", "baseRevision", "dialogueId", "slug", "displayName", "entryNodeId", "nodes"],
      "properties": {
        "schemaVersion": { "const": 1 },
        "baseRevision": { "type": "integer", "minimum": 0 },
        "dialogueId": { "type": "string", "format": "uuid" },
        "slug": { "type": "string", "minLength": 1 },
        "displayName": { "type": "string", "minLength": 1 },
        "entryNodeId": { "type": "string", "format": "uuid" },
        "nodes": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "object",
            "required": ["nodeId", "localizationKey", "fallbackText"],
            "properties": {
              "nodeId": { "type": "string", "format": "uuid" },
              "speakerBlueprintId": { "type": ["string", "null"], "format": "uuid" },
              "localizationKey": { "type": "string", "minLength": 1 },
              "fallbackText": { "type": "string", "minLength": 1 },
              "onEnterActions": { "type": ["array", "null"] },
              "choices": { "type": ["array", "null"] }
            }
          }
        }
      }
    }
    """;
}
