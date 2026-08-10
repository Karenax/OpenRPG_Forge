using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;

namespace Forge.ModuleFormat.Tests.Serialization;

internal static class TestProjectFactory
{
    internal static ProjectDefinition CreateValid()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc"));
        var strike = new AbilityDefinition(
            AbilityId.From(Guid.Parse("4ad1063b-ef78-4b4a-95de-ea763711978e")),
            "ember-strike",
            "Ember strike",
            1,
            0.5,
            AbilityTargeting.SingleEnemy,
            [new AbilityEffect(AbilityEffectKind.Damage, 2)],
            new AbilityVisualCue("cue.ember-strike"));
        var blueprint = new BlueprintDefinition(
            blueprintId,
            "ash-warden",
            "Ash Warden",
            BlueprintKind.Creature,
            new CombatProfile(
                new CombatStats(8, 3, 2, 1, "crypt", "hostile"),
                [strike],
                [new LootDrop("warden-ember", 1)]));
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56")),
            blueprintId,
            new TransformValue(
                new Vector3Value(2, 0, -4),
                QuaternionValue.Identity,
                Vector3Value.One),
            ["boss", "hostile"],
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [
                    new EntityPropertyOverride("displayName", EntityPropertyValueKind.String, "Gardien cendré"),
                    new EntityPropertyOverride("hostile", EntityPropertyValueKind.Boolean, "true"),
                    new EntityPropertyOverride("hitPoints", EntityPropertyValueKind.Integer, "80"),
                ]));
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt-of-ashes",
            "Crypt of Ashes",
            [entity]);
        var nodeId = DialogueNodeId.From(Guid.Parse("3f92330b-c513-437e-9444-6e0dbbe8e555"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("6393019c-06c3-466c-ab93-9b4093368f36")),
            "ash-warden-warning",
            "Ash Warden warning",
            nodeId,
            [new DialogueNode(
                nodeId,
                blueprintId,
                new LocalizedText("dialogue.warden.warning", "You shall not pass."),
                choices: [new DialogueChoice(
                    DialogueChoiceId.From(Guid.Parse("02c677c4-d1c1-4eeb-93c8-e4478d07fb97")),
                    new LocalizedText("dialogue.warden.leave", "We shall see."))])]);
        var initialQuestStateId = QuestStateId.From(Guid.Parse("0296868c-3db3-4694-9e5c-f0991ba7714d"));
        var completedQuestStateId = QuestStateId.From(Guid.Parse("d3b755c4-b331-4010-b7e7-7c8dbb50ff73"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("43c53222-479e-4b90-b870-516998287380")),
            "warden-quest",
            "Warden quest",
            initialQuestStateId,
            [
                new QuestStateDefinition(
                    initialQuestStateId,
                    "Active",
                    new QuestJournalEntry("quest.warden.active", "Defeat the Ash Warden.")),
                new QuestStateDefinition(
                    completedQuestStateId,
                    "Completed",
                    new QuestJournalEntry("quest.warden.completed", "The Warden is defeated."),
                    isTerminal: true),
            ],
            [new QuestTransition(
                QuestTransitionId.From(Guid.Parse("1e855df6-e5b7-49fa-aa6f-b865e75505d4")),
                initialQuestStateId,
                completedQuestStateId)]);

        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "OpenRPG Forge test project",
            [blueprint],
            [area],
            [dialogue],
            [quest]);
    }

    internal static ProjectDefinition CreateNavigationValid()
    {
        var project = CreateValid();
        var spawnBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("24d7fb76-9bbb-4ccc-89fc-82b3a9396048")),
            "player-start",
            "Player start",
            BlueprintKind.PlayerSpawn);
        var spawn = new EntityInstance(
            EntityId.From(Guid.Parse("c7c935f6-f248-419e-93d4-280e63d769c2")),
            spawnBlueprint.Id,
            new TransformValue(new Vector3Value(0, 0, 0), QuaternionValue.Identity, Vector3Value.One),
            ["player-start"]);
        return new ProjectDefinition(project.Id, project.Title, project.Module,
            [.. project.Blueprints, spawnBlueprint],
            [project.Areas[0].WithEntities([.. project.Areas[0].Entities, spawn])],
            project.Dialogues, project.Quests);
    }
}
