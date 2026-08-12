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
        },
        "experience": {
          "type": ["object", "null"],
          "additionalProperties": false,
          "required": ["experienceId", "enabled", "requiredAtRuntime", "lifecycle", "search", "review", "memorySlots", "skillChecks"],
          "properties": {
            "experienceId": { "type": "string", "format": "uuid" },
            "enabled": { "type": "boolean" },
            "requiredAtRuntime": { "type": "boolean" },
            "lifecycle": {
              "type": "object",
              "additionalProperties": false,
              "required": ["lifecycleId", "completedActions", "abortedActions", "rollbackOnAbort", "createResumeState"],
              "properties": {
                "lifecycleId": { "type": "string", "format": "uuid" },
                "completedActions": { "type": "array", "items": { "type": "object", "required": ["kind", "key", "value"], "properties": { "kind": { "enum": ["SetVariable", "StartQuest", "AdvanceQuest", "CompleteQuest", "GiveItem", "RemoveItem", "UnlockEntity", "SetRelation", "PlayAnimation", "PlayAudio", "StartSequence", "RunScript"] }, "key": { "type": "string", "minLength": 1 }, "value": { "type": "string", "minLength": 1 }, "required": { "type": "boolean" } } } },
                "abortedActions": { "type": "array", "items": { "type": "object", "required": ["kind", "key", "value"], "properties": { "kind": { "enum": ["SetVariable", "StartQuest", "AdvanceQuest", "CompleteQuest", "GiveItem", "RemoveItem", "UnlockEntity", "SetRelation", "PlayAnimation", "PlayAudio", "StartSequence", "RunScript"] }, "key": { "type": "string", "minLength": 1 }, "value": { "type": "string", "minLength": 1 }, "required": { "type": "boolean" } } } },
                "rollbackOnAbort": { "type": "boolean" },
                "createResumeState": { "type": "boolean" }
              }
            },
            "search": {
              "type": "object",
              "additionalProperties": false,
              "required": ["searchId", "pattern", "useRegex", "ignoreCase", "speakerRoles", "conditionKinds", "actionKinds", "replacementPreview"],
              "properties": {
                "searchId": { "type": "string", "format": "uuid" },
                "pattern": { "type": "string", "minLength": 1, "maxLength": 512 },
                "useRegex": { "type": "boolean" },
                "ignoreCase": { "type": "boolean" },
                "speakerRoles": { "type": "array", "items": { "type": "string", "minLength": 1 } },
                "conditionKinds": { "type": "array", "items": { "enum": ["VariableEquals", "HasItem", "QuestStateEquals", "RelationEquals", "EventRaised"] } },
                "actionKinds": { "type": "array", "items": { "enum": ["SetVariable", "StartQuest", "AdvanceQuest", "CompleteQuest", "GiveItem", "RemoveItem", "UnlockEntity", "SetRelation", "PlayAnimation", "PlayAudio", "StartSequence", "RunScript"] } },
                "replacementPreview": { "type": "string" }
              }
            },
            "review": {
              "type": "object",
              "additionalProperties": false,
              "required": ["reviewId", "badges", "speakerRoles", "reviewMode"],
              "properties": {
                "reviewId": { "type": "string", "format": "uuid" },
                "badges": { "type": "array", "items": { "enum": ["Condition", "Action", "Voice", "Localization", "Error", "Link"] } },
                "speakerRoles": { "type": "array", "items": { "type": "string", "minLength": 1 } },
                "reviewMode": { "type": "boolean" }
              }
            },
            "memorySlots": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["slotId", "scope", "scopeId", "name", "type", "duration"],
                "properties": {
                  "slotId": { "type": "string", "format": "uuid" },
                  "scope": { "enum": ["Dialogue", "Participant", "Player", "Party", "World"] },
                  "scopeId": { "type": "string", "minLength": 1 },
                  "name": { "type": "string", "minLength": 1 },
                  "type": { "enum": ["Text", "Integer", "Decimal", "Boolean", "EntityName", "LocalizedText"] },
                  "duration": { "enum": ["Node", "Session", "Area", "Campaign", "Permanent"] },
                  "defaultValue": { "type": ["string", "null"] }
                }
              }
            },
            "skillChecks": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["checkId", "nodeId", "skillId", "difficulty", "advantage", "visibility", "successNodeId", "failureNodeId"],
                "properties": {
                  "checkId": { "type": "string", "format": "uuid" },
                  "nodeId": { "type": "string", "format": "uuid" },
                  "skillId": { "type": "string", "minLength": 1 },
                  "difficulty": { "type": "number", "minimum": 0 },
                  "opponentId": { "type": ["string", "null"] },
                  "advantage": { "type": "boolean" },
                  "visibility": { "enum": ["Hidden", "DifficultyOnly", "ResultOnly", "FullExplanation"] },
                  "successNodeId": { "type": "string", "format": "uuid" },
                  "failureNodeId": { "type": "string", "format": "uuid" }
                }
              }
            }
          }
        },
        "production": {
          "type": ["object", "null"],
          "additionalProperties": false,
          "required": ["productionId", "enabled", "requiredAtRuntime", "subgraphs", "invocations", "consequences", "coverage", "collaboration", "aiPlans"],
          "properties": {
            "productionId": { "type": "string", "format": "uuid" },
            "enabled": { "type": "boolean" },
            "requiredAtRuntime": { "type": "boolean" },
            "subgraphs": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["contractId", "key", "version", "parameters", "requiredVariables", "tests"],
                "properties": {
                  "contractId": { "type": "string", "format": "uuid" },
                  "key": { "type": "string", "minLength": 1 },
                  "version": { "type": "integer", "minimum": 1 },
                  "parameters": { "type": "array", "items": { "type": "object", "required": ["parameterId", "name", "type", "direction", "required"], "properties": { "parameterId": { "type": "string", "format": "uuid" }, "name": { "type": "string", "minLength": 1 }, "type": { "enum": ["Text", "Integer", "Decimal", "Boolean", "EntityName", "LocalizedText"] }, "direction": { "enum": ["Input", "Output"] }, "required": { "type": "boolean" }, "defaultValue": { "type": ["string", "null"] } } } },
                  "requiredVariables": { "type": "array", "items": { "type": "string", "pattern": "^[^:]+:.+$" } },
                  "tests": { "type": "array", "minItems": 1, "items": { "type": "object", "required": ["testId", "name", "inputs", "expectedOutputs"], "properties": { "testId": { "type": "string", "format": "uuid" }, "name": { "type": "string", "minLength": 1 }, "inputs": { "type": "object", "additionalProperties": { "type": "string" } }, "expectedOutputs": { "type": "object", "additionalProperties": { "type": "string" } } } } }
                }
              }
            },
            "invocations": { "type": "array", "items": { "type": "object", "required": ["invocationId", "nodeId", "contractId", "inputBindings", "outputBindings"], "properties": { "invocationId": { "type": "string", "format": "uuid" }, "nodeId": { "type": "string", "format": "uuid" }, "contractId": { "type": "string", "format": "uuid" }, "inputBindings": { "type": "object", "additionalProperties": { "type": "string" } }, "outputBindings": { "type": "object", "additionalProperties": { "type": "string" } } } } },
            "consequences": { "type": "array", "items": { "type": "object", "required": ["consequenceId", "nodeId", "kind", "targetId", "operation", "value", "explanation"], "properties": { "consequenceId": { "type": "string", "format": "uuid" }, "nodeId": { "type": "string", "format": "uuid" }, "kind": { "enum": ["Quest", "Faction", "Item", "World"] }, "targetId": { "type": "string", "minLength": 1 }, "operation": { "type": "string", "minLength": 1 }, "value": { "type": "string", "minLength": 1 }, "explanation": { "type": "string", "minLength": 1 } } } },
            "coverage": { "type": "object", "required": ["coverageId", "maximumDepth", "requiredStates", "evidence"], "properties": { "coverageId": { "type": "string", "format": "uuid" }, "maximumDepth": { "type": "integer", "minimum": 1, "maximum": 256 }, "requiredStates": { "type": "object", "additionalProperties": { "type": "string" } }, "evidence": { "type": "array", "items": { "type": "object", "required": ["evidenceId", "name", "kind", "visitedNodeIds", "state"], "properties": { "evidenceId": { "type": "string", "format": "uuid" }, "name": { "type": "string", "minLength": 1 }, "kind": { "enum": ["Test", "Session"] }, "visitedNodeIds": { "type": "array", "minItems": 1, "items": { "type": "string", "format": "uuid" } }, "state": { "type": "object", "additionalProperties": { "type": "string" } } } } } } },
            "collaboration": { "type": "object", "required": ["collaborationId", "revisions", "locks", "comments", "conflicts"], "properties": { "collaborationId": { "type": "string", "format": "uuid" }, "revisions": { "type": "array" }, "locks": { "type": "array" }, "comments": { "type": "array" }, "conflicts": { "type": "array" } } },
            "aiPlans": { "type": "array", "items": { "type": "object", "required": ["aiId", "transactionId", "planId", "authorization", "synopsis", "requiredBeats", "styleRules", "contextCitations", "semanticDiff", "lint", "previewOnly", "applied"], "properties": { "aiId": { "type": "string", "format": "uuid" }, "transactionId": { "type": "string", "format": "uuid" }, "planId": { "type": "string", "format": "uuid" }, "authorization": { "enum": ["SuggestOnly", "Draft", "AuthorApply"] }, "synopsis": { "type": "string", "minLength": 1 }, "requiredBeats": { "type": "array", "minItems": 1, "items": { "type": "string", "minLength": 1 } }, "styleRules": { "type": "array", "minItems": 1, "items": { "type": "string", "minLength": 1 } }, "contextCitations": { "type": "array", "minItems": 1, "items": { "type": "string", "minLength": 1 } }, "semanticDiff": { "type": "string", "minLength": 1 }, "lint": { "type": "array", "items": { "type": "string" } }, "previewOnly": { "type": "boolean" }, "applied": { "type": "boolean" } } } }
          }
        },
        "authoring": {
          "type": ["object", "null"],
          "additionalProperties": false,
          "required": ["authoringId", "enabled", "requiredAtRuntime", "participants", "nodes"],
          "properties": {
            "authoringId": { "type": "string", "format": "uuid" },
            "enabled": { "type": "boolean" },
            "requiredAtRuntime": { "type": "boolean" },
            "participants": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["bindingId", "role", "kind", "value"],
                "properties": {
                  "bindingId": { "type": "string", "format": "uuid" },
                  "role": { "type": "string", "minLength": 1 },
                  "kind": { "enum": ["Role", "Query", "Tag", "Entity"] },
                  "value": { "type": "string", "minLength": 1 },
                  "fallbackRole": { "type": ["string", "null"] }
                }
              }
            },
            "nodes": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["authoringNodeId", "nodeId", "speakerRole", "evaluationMode", "tokens", "choiceEvaluations", "wizardEntries", "notes"],
                "properties": {
                  "authoringNodeId": { "type": "string", "format": "uuid" },
                  "nodeId": { "type": "string", "format": "uuid" },
                  "speakerRole": { "type": "string", "minLength": 1 },
                  "evaluationMode": { "enum": ["FirstValid", "AllValid", "Weighted", "RandomDeterministic", "Manual"] },
                  "tokens": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "required": ["tokenId", "name", "type", "required"],
                      "properties": {
                        "tokenId": { "type": "string", "format": "uuid" },
                        "name": { "type": "string", "minLength": 1 },
                        "type": { "enum": ["Text", "Integer", "Decimal", "Boolean", "EntityName", "LocalizedText"] },
                        "required": { "type": "boolean" },
                        "defaultValue": { "type": ["string", "null"] }
                      }
                    }
                  },
                  "choiceEvaluations": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "required": ["evaluationId", "choiceId", "weight"],
                      "properties": {
                        "evaluationId": { "type": "string", "format": "uuid" },
                        "choiceId": { "type": "string", "format": "uuid" },
                        "weight": { "type": "number", "minimum": 0 }
                      }
                    }
                  },
                  "wizardEntries": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "required": ["entryId", "kind", "targetId", "sourceDescription"],
                      "properties": {
                        "entryId": { "type": "string", "format": "uuid" },
                        "kind": { "enum": ["Condition", "Action"] },
                        "targetId": { "type": "string", "format": "uuid" },
                        "sourceDescription": { "type": "string", "pattern": "^builder:" }
                      }
                    }
                  },
                  "notes": {
                    "type": "object",
                    "required": ["notesId", "intent", "emotion", "source", "status", "todos", "authorMention"],
                    "properties": {
                      "notesId": { "type": "string", "format": "uuid" },
                      "intent": { "type": "string", "minLength": 1 },
                      "emotion": { "type": "string", "minLength": 1 },
                      "source": { "type": "string", "minLength": 1 },
                      "status": { "enum": ["Draft", "Review", "Approved", "Deprecated"] },
                      "todos": { "type": "array", "items": { "type": "string", "minLength": 1 } },
                      "authorMention": { "type": "string", "minLength": 1 }
                    }
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
