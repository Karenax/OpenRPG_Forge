using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;

namespace Forge.Application.Tests.Dialogues;

public sealed class DialogueGraphScriptServiceTests
{
    [Fact]
    public void Typed_handle_reads_graph_and_rejects_stale_identity()
    {
        var entry = DialogueNodeId.Parse("d0051000-0000-4000-8000-000000000001");
        var end = DialogueNodeId.Parse("d0051000-0000-4000-8000-000000000002");
        var graph = new DialogueDefinition(DialogueId.Parse("d0051000-0000-4000-8000-000000000010"),
            "script", "Script", entry,
            [
                new(entry, null, new("dialogue.entry", "Entrée"), choices:
                [new(DialogueChoiceId.Parse("d0051000-0000-4000-8000-000000000020"),
                    new LocalizedText("dialogue.link", "Lien",
                        [LocalizedTextVariant.Create("dialogue.link", "en", "Link")]),
                    [new(DialogueConditionKind.HasItem, "key", "true")],
                    [new(DialogueActionKind.PlayAudio, "link", "play")],
                    end, DialogueEdgeKind.Reference)]),
                new(end, null, new("dialogue.end", "Fin"), role: DialogueNodeRole.End),
            ]);
        var service = new DialogueGraphScriptService(graph);

        var snapshot = service.Read(service.Handle);

        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(1, snapshot.EdgeCount);
        Assert.Equal(1, snapshot.ReferenceEdgeCount);
        Assert.Equal(1, snapshot.LocalizationVariantCount);
        Assert.Equal(1, snapshot.ConditionCount);
        Assert.Equal(1, snapshot.ActionCount);
        Assert.Equal(entry, service.ReadNode(service.Handle, entry).Id);
        Assert.Throws<InvalidOperationException>(() => service.Read(
            new DialogueGraphHandle(DialogueId.Parse("d0051000-0000-4000-8000-000000000099"), 1)));
    }

    [Fact]
    public void R068Typed_handle_reads_authoring_without_exposing_storage_or_engine()
    {
        var entry = DialogueNodeId.Parse("d0684000-0000-4000-8000-000000000001");
        var graph = new DialogueDefinition(DialogueId.Parse("d0684000-0000-4000-8000-000000000010"),
            "r068-script", "R068 script", entry,
            [new(entry, null, new("dialogue.r068", "Bonjour."), role: DialogueNodeRole.End)],
            provenance: "author:r068");
        var profile = DialogueAuthoringProfile.CreateDefault(graph, "author:r068") with
        {
            RequiredAtRuntime = true,
        };
        var service = new DialogueGraphScriptService(graph.WithAuthoring(profile));

        var snapshot = service.ReadAuthoring(service.Handle);
        var node = service.ReadNodeAuthoring(service.Handle, entry);

        Assert.True(snapshot.Enabled);
        Assert.True(snapshot.RequiredAtRuntime);
        Assert.Single(snapshot.EvaluationModes);
        Assert.Equal(DialogueChoiceEvaluationMode.FirstValid, Assert.Single(snapshot.EvaluationModes));
        Assert.Equal(entry, node!.NodeId);
        Assert.Equal("narrator", node.SpeakerRole);
    }

    [Fact]
    public void R069Typed_handle_reads_experience_and_skill_check_without_storage_or_engine()
    {
        var entry = DialogueNodeId.Parse("d0694000-0000-4000-8000-000000000001");
        var success = DialogueNodeId.Parse("d0694000-0000-4000-8000-000000000002");
        var failure = DialogueNodeId.Parse("d0694000-0000-4000-8000-000000000003");
        var graph = new DialogueDefinition(DialogueId.Parse("d0694000-0000-4000-8000-000000000010"),
            "r069-script", "R069 script", entry,
            [
                new(entry, null, new("dialogue.r069", "Bonjour."), choices:
                [
                    new(DialogueChoiceId.Parse("d0694000-0000-4000-8000-000000000011"),
                        new("dialogue.r069.success", "Réussite"), targetNodeId: success),
                    new(DialogueChoiceId.Parse("d0694000-0000-4000-8000-000000000012"),
                        new("dialogue.r069.failure", "Échec"), targetNodeId: failure),
                ]),
                new(success, null, new("dialogue.r069.end.success", "Fin"), role: DialogueNodeRole.End),
                new(failure, null, new("dialogue.r069.end.failure", "Fin"), role: DialogueNodeRole.End),
            ], provenance: "author:r069");
        var profile = DialogueExperienceProfile.CreateDefault(graph, "author:r069") with
        {
            RequiredAtRuntime = true,
            MemorySlots =
            [new(Guid.NewGuid(), 1, "author:r069", DialogueMemoryScope.Player, "player:current", "trust",
                DialogueTextTokenType.Integer, DialogueMemoryDuration.Campaign, "0")],
            SkillChecks =
            [new(Guid.NewGuid(), 1, "author:r069", entry, "persuasion", 12, "elyra", true,
                DialogueSkillCheckVisibility.FullExplanation, success, failure)],
        };
        var service = new DialogueGraphScriptService(graph.WithExperience(profile));

        var snapshot = service.ReadExperience(service.Handle);
        var check = service.ReadSkillCheck(service.Handle, entry);

        Assert.True(snapshot.Enabled);
        Assert.True(snapshot.RequiredAtRuntime);
        Assert.Equal(6, snapshot.ReviewBadges.Count);
        Assert.Equal(1, snapshot.MemorySlotCount);
        Assert.Equal(1, snapshot.SkillCheckCount);
        Assert.Equal("persuasion", check!.SkillId);
    }

    [Fact]
    public void R070Typed_handle_reads_production_contract_coverage_and_consequences_without_storage_or_engine()
    {
        var node = DialogueNodeId.Parse("d0704000-0000-4000-8000-000000000001");
        var graph = new DialogueDefinition(DialogueId.Parse("d0704000-0000-4000-8000-000000000010"),
            "r070-script", "R070 script", node,
            [new(node, null, new("dialogue.r070", "Bonjour."), role: DialogueNodeRole.Subgraph,
                subgraphKey: "embers")], provenance: "author:r070");
        var p = "author:r070";
        var contractId = Guid.Parse("d0704000-0000-4000-8000-000000000101");
        var parameter = new DialogueSubgraphParameter(Guid.Parse("d0704000-0000-4000-8000-000000000102"),
            1, p, "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0704000-0000-4000-8000-000000000103"), 1, p,
            "trust", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string>());
        var contract = new DialogueSubgraphContract(contractId, 1, p, "embers", 1, [parameter], [], [test]);
        var invocation = new DialogueSubgraphInvocation(Guid.Parse("d0704000-0000-4000-8000-000000000104"),
            1, p, node, contractId, new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string>());
        var consequence = new DialogueTypedConsequence(
            Guid.Parse("d0704000-0000-4000-8000-000000000105"), 1, p, node,
            DialogueConsequenceTargetKind.Quest, "quest:crypt", "advance", "2", "Advance quest");
        var evidence = new DialogueCoverageEvidence(Guid.Parse("d0704000-0000-4000-8000-000000000106"),
            1, p, "test:trust", DialogueCoverageEvidenceKind.Test, [node],
            new Dictionary<string, string>());
        var profile = new DialogueProductionProfile(Guid.Parse("d0704000-0000-4000-8000-000000000107"),
            1, p, true, true, [contract], [invocation], [consequence],
            new DialogueCoveragePolicy(Guid.Parse("d0704000-0000-4000-8000-000000000108"), 1, p, 64,
                new Dictionary<string, string>(), [evidence]),
            new DialogueCollaborationProfile(Guid.Parse("d0704000-0000-4000-8000-000000000109"), 1, p,
                [], [], [], []), []);
        var service = new DialogueGraphScriptService(graph.WithProduction(profile));

        var snapshot = service.ReadProduction(service.Handle);

        Assert.True(snapshot.Enabled);
        Assert.True(snapshot.RequiredAtRuntime);
        Assert.Equal(1, snapshot.SubgraphCount);
        Assert.Equal(1, snapshot.CoverageRatio);
        Assert.Equal("embers", service.ReadSubgraph(service.Handle, "embers")!.Key);
        Assert.Equal("quest:crypt", Assert.Single(service.ReadConsequences(service.Handle, node)).TargetId);
    }
}
