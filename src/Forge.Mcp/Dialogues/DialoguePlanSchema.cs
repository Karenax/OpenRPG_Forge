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
              "role": { "type": ["string", "null"], "enum": [null, "SpeakerLine", "PlayerChoice", "Narration", "System", "ActionOnly", "Subgraph", "End"] },
              "participantId": { "type": ["string", "null"], "format": "uuid" },
              "subgraphKey": { "type": ["string", "null"] },
              "variants": {
                "type": ["array", "null"],
                "items": {
                  "type": "object",
                  "required": ["locale", "gender", "pluralCategory", "text"],
                  "properties": {
                    "locale": { "type": "string", "minLength": 2 },
                    "gender": { "type": "string", "minLength": 1 },
                    "pluralCategory": { "enum": ["Unspecified", "Zero", "One", "Two", "Few", "Many", "Other"] },
                    "text": { "type": "string", "minLength": 1 }
                  }
                }
              },
              "onEnterActions": {
                "type": ["array", "null"],
                "items": {
                  "type": "object",
                  "required": ["kind", "key", "value"],
                  "properties": {
                    "kind": { "enum": ["SetVariable", "StartQuest", "AdvanceQuest", "CompleteQuest", "GiveItem", "RemoveItem", "UnlockEntity", "SetRelation", "PlayAnimation", "PlayAudio", "StartSequence", "RunScript"] },
                    "key": { "type": "string", "minLength": 1 },
                    "value": { "type": "string", "minLength": 1 },
                    "required": { "type": "boolean" }
                  }
                }
              },
              "choices": {
                "type": ["array", "null"],
                "items": {
                  "type": "object",
                  "properties": {
                    "edgeKind": { "type": ["string", "null"], "enum": [null, "Flow", "Reference"] },
                    "priority": { "type": "integer", "minimum": 0 },
                    "conditionCombination": { "type": ["string", "null"], "enum": [null, "All", "Any"] },
                    "variants": { "type": ["array", "null"] },
                    "conditions": {
                      "type": ["array", "null"],
                      "items": {
                        "type": "object",
                        "required": ["kind", "key", "expectedValue"],
                        "properties": {
                          "kind": { "enum": ["VariableEquals", "HasItem", "QuestStateEquals", "RelationEquals", "EventRaised"] },
                          "key": { "type": "string", "minLength": 1 },
                          "expectedValue": { "type": "string", "minLength": 1 },
                          "negated": { "type": "boolean" },
                          "eventBinding": { "type": ["string", "null"] },
                          "forgeScriptApiVersion": { "const": 1 }
                        }
                      }
                    },
                    "actions": { "type": ["array", "null"] }
                  }
                }
              }
            }
          }
        }
      }
    }
    """;
}
