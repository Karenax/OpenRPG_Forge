using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueProductionProfileTests
{
    [Fact]
    public void R070Canonical_profile_validates_all_five_capabilities()
    {
        var dialogue = CreateProductionDialogue();
        Assert.Empty(DialogueProductionProfileValidator.Validate(dialogue));
        Assert.Empty(DialogueValidator.Validate(dialogue));
        Assert.Equal(4, dialogue.Production!.Consequences.Select(item => item.Kind).Distinct().Count());
        Assert.True(DialogueProductionRuntimeService.TestSubgraph(dialogue.Production,
            dialogue.Production.Subgraphs.Single().Id, dialogue.Production.Subgraphs.Single().Tests.Single().Id).Passed);
    }

    [Fact]
    public void R070Impact_coverage_collaboration_and_ai_are_deterministic_and_previewed()
    {
        var dialogue = CreateProductionDialogue();
        var profile = dialogue.Production!;
        var impact = DialogueProductionRuntimeService.AnalyzeImpact(profile,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["quest:crypt"] = ["journal:crypt", "ending:embers"],
            });
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(dialogue);
        var nodeLock = profile.Collaboration.Locks.Single();
        var conflict = profile.Collaboration.Conflicts.Single();
        var merge = DialogueProductionRuntimeService.ResolveConflict(conflict);
        var preview = DialogueProductionRuntimeService.PreviewAi(profile.AiPlans.Single());
        var applied = DialogueProductionRuntimeService.ApplyAi(profile, preview.PlanId);

        Assert.Contains("ending:embers", impact.Single(item =>
            item.Consequence.Kind == DialogueConsequenceTargetKind.Quest).AffectedResources);
        Assert.Equal(2d / 3d, coverage.Ratio);
        Assert.Equal(2, coverage.Paths.Count);
        Assert.True(DialogueProductionRuntimeService.CanEdit(nodeLock, "author", DateTimeOffset.UnixEpoch));
        Assert.False(DialogueProductionRuntimeService.CanEdit(nodeLock, "reviewer", DateTimeOffset.UnixEpoch));
        Assert.True(merge.CanApply);
        Assert.Contains("resolution:Manual", merge.SemanticDiff);
        Assert.Empty(preview.Lint);
        Assert.True(applied.AiPlans.Single().Applied);
        Assert.Throws<InvalidOperationException>(() => DialogueProductionRuntimeService.ApplyAi(applied, preview.PlanId));
    }

    [Fact]
    public void R070Invalid_profile_reports_stable_localized_diagnostics()
    {
        var dialogue = CreateProductionDialogue();
        var profile = dialogue.Production!;
        var invalid = profile with
        {
            Subgraphs = [profile.Subgraphs.Single() with { Version = 0 }],
            Consequences = [profile.Consequences[0] with { TargetId = "" }],
            Coverage = profile.Coverage with { MaximumDepth = 0 },
            Collaboration = profile.Collaboration with
            {
                Revisions = [profile.Collaboration.Revisions.Single() with { CanonicalJson = "not-json" }],
            },
            AiPlans = [profile.AiPlans.Single() with { ContextCitations = [] }],
        };
        var diagnostics = DialogueProductionProfileValidator.Validate(dialogue.WithProduction(invalid));
        Assert.Contains(diagnostics, item => item.Code == DialogueProductionProfileValidator.SubgraphCode &&
            item.Path.EndsWith("/subgraphs", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueProductionProfileValidator.ConsequenceCode);
        Assert.Contains(diagnostics, item => item.Code == DialogueProductionProfileValidator.CoverageCode);
        Assert.Contains(diagnostics, item => item.Code == DialogueProductionProfileValidator.CollaborationCode);
        Assert.Contains(diagnostics, item => item.Code == DialogueProductionProfileValidator.AiCode);
    }

    [Fact]
    public void R070Optional_absence_is_safe_and_required_absence_is_diagnostic()
    {
        var optional = DialogueAuthoringProfileTests.CreateDialogue();
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["production.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring, optional.Experience);
        Assert.Empty(DialogueProductionProfileValidator.Validate(optional));
        Assert.Equal(DialogueProductionProfileValidator.RequiredMissingCode,
            Assert.Single(DialogueProductionProfileValidator.Validate(required)).Code);
    }

    internal static DialogueDefinition CreateProductionDialogue()
    {
        var source = DialogueAuthoringProfileTests.CreateDialogue();
        var subgraphNodeId = source.Nodes[1].Id;
        var nodes = source.Nodes.Select(item => item.Id == subgraphNodeId
            ? new DialogueNode(item.Id, item.SpeakerBlueprintId, item.Text, item.OnEnterActions, item.Choices,
                DialogueNodeRole.Subgraph, item.ParticipantId, "embers")
            : item).ToArray();
        var dialogue = new DialogueDefinition(source.Id, source.Slug, source.DisplayName, source.EntryNodeId, nodes,
            source.Presentation, source.SchemaVersion, "author:r070", source.Metadata, source.Participants,
            source.EntryPoints);
        var p = "author:r070";
        var contractId = Guid.Parse("d0700000-0000-4000-8000-000000000101");
        var input = new DialogueSubgraphParameter(Guid.Parse("d0700000-0000-4000-8000-000000000102"), 1, p,
            "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var output = new DialogueSubgraphParameter(Guid.Parse("d0700000-0000-4000-8000-000000000103"), 1, p,
            "accepted", DialogueTextTokenType.Boolean, DialogueSubgraphParameterDirection.Output, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0700000-0000-4000-8000-000000000104"), 1, p,
            "trust accepted", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string> { ["accepted"] = "true" });
        var contract = new DialogueSubgraphContract(contractId, 1, p, "embers", 2, [input, output],
            ["party:trust"], [test]);
        var invocation = new DialogueSubgraphInvocation(Guid.Parse("d0700000-0000-4000-8000-000000000105"),
            1, p, subgraphNodeId, contractId,
            new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string> { ["accepted"] = "memory:accepted" });
        var consequences = Enum.GetValues<DialogueConsequenceTargetKind>().Select((kind, index) =>
            new DialogueTypedConsequence(Guid.Parse($"d0700000-0000-4000-8000-0000000002{index:00}"), 1, p,
                source.EntryNodeId, kind, kind switch
                {
                    DialogueConsequenceTargetKind.Quest => "quest:crypt",
                    DialogueConsequenceTargetKind.Faction => "faction:wardens",
                    DialogueConsequenceTargetKind.Item => "item:ash-key",
                    _ => "world:embers",
                }, kind == DialogueConsequenceTargetKind.Faction ? "reputation" : "set", "+1",
                $"Impact {kind}")).ToArray();
        var evidence = new DialogueCoverageEvidence(Guid.Parse("d0700000-0000-4000-8000-000000000301"), 1, p,
            "test:embers", DialogueCoverageEvidenceKind.Test, [source.EntryNodeId, subgraphNodeId],
            new Dictionary<string, string> { ["quest:crypt"] = "active" });
        var coverage = new DialogueCoveragePolicy(Guid.Parse("d0700000-0000-4000-8000-000000000302"), 1, p,
            64, new Dictionary<string, string> { ["quest:crypt"] = "active" }, [evidence]);
        var now = DateTimeOffset.UnixEpoch;
        var revision = new DialogueNodeRevision(Guid.Parse("d0700000-0000-4000-8000-000000000401"), 1, p,
            subgraphNodeId, 2, "author", "{\"text\":\"ours\"}", now);
        var nodeLock = new DialogueNodeLock(Guid.Parse("d0700000-0000-4000-8000-000000000402"), 1, p,
            subgraphNodeId, "author", now.AddMinutes(5));
        var comment = new DialogueNodeComment(Guid.Parse("d0700000-0000-4000-8000-000000000403"), 1, p,
            subgraphNodeId, "reviewer", "Clarifier la conséquence.", false, now);
        var conflict = new DialogueSemanticConflict(Guid.Parse("d0700000-0000-4000-8000-000000000404"), 1, p,
            subgraphNodeId, "text", "{\"text\":\"base\"}", "{\"text\":\"ours\"}",
            "{\"text\":\"theirs\"}", DialogueConflictResolution.Manual, "{\"text\":\"merged\"}", true);
        var collaboration = new DialogueCollaborationProfile(
            Guid.Parse("d0700000-0000-4000-8000-000000000405"), 1, p, [revision], [nodeLock], [comment],
            [conflict]);
        var ai = new DialogueAiPlan(Guid.Parse("d0700000-0000-4000-8000-000000000501"), 1, p,
            Guid.Parse("d0700000-0000-4000-8000-000000000502"),
            Guid.Parse("d0700000-0000-4000-8000-000000000503"), DialogueAiAuthorizationLevel.AuthorApply,
            "Une promesse sous la crypte.", ["offre", "choix"], ["style:embers"],
            ["PROJECT_CONTEXT.md"], "add-subgraph:embers|nodes:1", [], true, false);
        return dialogue.WithProduction(new DialogueProductionProfile(
            Guid.Parse("d0700000-0000-4000-8000-000000000010"), 1, p, true, true,
            [contract], [invocation], consequences, coverage, collaboration, [ai]));
    }
}
