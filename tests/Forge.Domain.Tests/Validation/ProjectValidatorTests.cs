using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Validation;

public sealed class ProjectValidatorTests
{
    [Fact]
    public void Validate_ReportsMissingBlueprintReference()
    {
        var missingBlueprintId = BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc"));
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56")),
            missingBlueprintId,
            TransformValue.Identity);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt",
            "Crypt",
            [entity]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Test project",
            [],
            [area]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project));

        Assert.Equal(ProjectValidator.MissingBlueprintCode, diagnostic.Code);
        Assert.Contains(missingBlueprintId.ToString(), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReportsBlueprintInstanceVersionMismatchWithLocalizedPath()
    {
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b0300000-0000-4000-8000-000000000010")),
            "versioned-pillar",
            "Versioned pillar",
            BlueprintKind.Placeable,
            authoring: new BlueprintAuthoringMetadata(
                1, "1.4.0", "project.versioned.pillar",
                "content/blueprints/versioned-pillar.blueprint.json"));
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("b0300000-0000-4000-8000-000000000011")),
            blueprint.Id,
            TransformValue.Identity,
            blueprintVersionRequirement: ">=2.0.0");
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("b0300000-0000-4000-8000-000000000012")),
            "versioned-area",
            "Versioned area",
            [entity]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("b0300000-0000-4000-8000-000000000013")),
            "Versioned project",
            [blueprint],
            [area]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project));

        Assert.Equal(ProjectValidator.BlueprintVersionMismatchCode, diagnostic.Code);
        Assert.Equal($"areas/{area.Id}/entities/0/blueprintVersionRequirement", diagnostic.Path);
    }

    [Fact]
    public void Validate_ReportsMissingModuleEntryArea()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt",
            "Crypt",
            []);
        var missingAreaId = AreaId.From(Guid.Parse("ea40d07f-3e30-46a5-a39d-f4f14a25d23a"));
        var module = new ModuleDefinition(
            ModuleId.From(Guid.Parse("0a8d1557-b84b-424b-8857-e8f0a7ac2765")),
            "test-module",
            "Test module",
            string.Empty,
            missingAreaId);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Test project",
            module,
            [],
            [area]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project));

        Assert.Equal(ProjectValidator.MissingEntryAreaCode, diagnostic.Code);
        Assert.Contains(missingAreaId.ToString(), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReportsMissingDialogueSpeakerBlueprint()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt",
            "Crypt",
            []);
        var nodeId = DialogueNodeId.From(Guid.Parse("de3ddd6a-d9ff-454c-ad6a-07b44f00e409"));
        var missingSpeaker = BlueprintId.From(Guid.Parse("92959760-6633-437c-bd2b-e308f79b1358"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("74b0fe1f-341b-45f0-a0e0-728106311cd1")),
            "missing-speaker",
            "Missing speaker",
            nodeId,
            [new DialogueNode(
                nodeId,
                missingSpeaker,
                new LocalizedText("dialogue.missing", "Missing"))]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Test project",
            [],
            [area],
            [dialogue]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project));

        Assert.Equal(ProjectValidator.MissingDialogueSpeakerBlueprintCode, diagnostic.Code);
    }

    [Fact]
    public void Validate_ReportsLocalizedBrokenPortalReferences()
    {
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc")),
            "portal-anchor",
            "Portal anchor",
            BlueprintKind.Trigger);
        var source = new EntityInstance(
            EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56")),
            blueprint.Id,
            TransformValue.Identity);
        var missingDestinationArea = AreaId.From(Guid.Parse("ea40d07f-3e30-46a5-a39d-f4f14a25d23a"));
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("9d9620d8-15b4-4be1-932a-30c77ed46cff")),
            "Broken portal",
            source.Id,
            missingDestinationArea,
            EntityId.From(Guid.Parse("47996244-97c1-4824-82a3-40922e577f53")),
            false,
            false);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt",
            "Crypt",
            [source],
            portals: [portal]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("69ddeadc-4cc1-42d0-aed6-9d99244479cf")),
            "Test project",
            [blueprint],
            [area]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project));

        Assert.Equal(ProjectValidator.MissingPortalDestinationAreaCode, diagnostic.Code);
        Assert.Equal($"areas/{area.Id}/portals/0/destinationAreaId", diagnostic.Path);
    }

    [Fact]
    public void Validate_ReportsCrossAreaPatrolWithoutMatchingSourcePortal()
    {
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "portal-anchor",
            "Portal anchor", BlueprintKind.Trigger);
        var sourceId = AreaId.From(Guid.NewGuid());
        var destinationId = AreaId.From(Guid.NewGuid());
        var sourceEntity = new EntityInstance(EntityId.From(Guid.NewGuid()), blueprint.Id, TransformValue.Identity);
        var destinationEntity = new EntityInstance(EntityId.From(Guid.NewGuid()), blueprint.Id,
            TransformValue.Identity);
        var declaredPortal = new PortalLink(PortalId.From(Guid.NewGuid()), "Passage", sourceEntity.Id,
            destinationId, destinationEntity.Id, false, true);
        var defaults = AreaRuntimePolicyProfile.CreateDefault(sourceId);
        var route = new CrossAreaPatrolRoute(Guid.NewGuid(), "guard",
            [new CrossAreaPatrolSegment(Guid.NewGuid(), sourceId, destinationId, PortalId.From(Guid.NewGuid()),
                Guid.NewGuid(), true, PatrolOwnershipAuthority.Server)], false, "test");
        var experience = new AreaExperienceProfile(sourceId.Value, 1, [], [], [], [route],
            defaults.Experience.Loading, "test");
        var policies = new AreaRuntimePolicyProfile(defaults.Id, defaults.SchemaVersion, defaults.Heartbeats,
            defaults.Rest, "test", defaults.Cinematics, defaults.Rules, experience);
        var source = new AreaDefinition(sourceId, "source", "Source", [sourceEntity], portals: [declaredPortal],
            runtimePolicies: policies);
        var destination = new AreaDefinition(destinationId, "destination", "Destination", [destinationEntity]);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Test project", [blueprint],
            [source, destination]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project), item =>
            item.Code == ProjectValidator.CrossAreaPatrolReferenceCode);

        Assert.Contains(route.Id.ToString(), diagnostic.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReportsUnresolvedTypedQuestJournalReference()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "crypt", "Crypt", []);
        var stateId = QuestStateId.From(Guid.NewGuid());
        var state = new QuestStateDefinition(stateId, 1, "test:r007", "terminal", "Terminal",
            new QuestJournalEntry(
                new LocalizedText("quest.test.summary", "Summary"),
                new LocalizedText("quest.test.details", "Details"),
                contextLinks: [new QuestJournalLink(QuestJournalLinkKind.Location, "missing-area")],
                provenance: "test:r007"), true, 1, 0, QuestTerminalKind.Completed, string.Empty,
            QuestReactivationRule.Never);
        var quest = new QuestDefinition(QuestId.From(Guid.NewGuid()), "test-quest", "Test quest", stateId,
            [state]);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Test project", [], [area],
            quests: [quest]);

        var diagnostic = Assert.Single(ProjectValidator.Validate(project), item =>
            item.Code == ProjectValidator.MissingQuestJournalReferenceCode);

        Assert.EndsWith("/referenceId", diagnostic.Path, StringComparison.Ordinal);
    }
}
