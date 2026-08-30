using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistedAreaPlanWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 5, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AssistedAreaArchetype.Crypt, "door")]
    [InlineData(AssistedAreaArchetype.Village, "path")]
    public async Task OfflineProviderProposesDeterministicConnectedPlanWithoutGeometry(
        AssistedAreaArchetype archetype,
        string traversal)
    {
        var project = Project();
        var brief = Brief(project, archetype);
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var provider = new DeterministicAssistantProvider(profile.ActiveProvider);

        var first = await AssistedAreaPlanProposalService.ProposeAsync(
            brief, project, LoreIndexDefinition.CreateDefault(), provider, profile.ActiveProvider, false, false);
        var second = await AssistedAreaPlanProposalService.ProposeAsync(
            brief, project, LoreIndexDefinition.CreateDefault(), provider, profile.ActiveProvider, false, false);

        Assert.True(first.Succeeded);
        Assert.NotNull(first.Plan);
        Assert.Equal(8, first.Plan.Nodes.Count);
        Assert.Equal(first.Plan.Nodes.Select(item => (item.Id, item.Kind, item.X, item.Z)),
            second.Plan!.Nodes.Select(item => (item.Id, item.Kind, item.X, item.Z)));
        Assert.Contains(first.Plan.Links, item => item.Traversal.Contains(traversal, StringComparison.Ordinal));
        Assert.False(first.Plan.ApprovedForConstruction);
        Assert.Equal(0, first.EstimatedCostUsd);
        Assert.Contains("geometry-applied:false", first.SemanticDiff, StringComparison.Ordinal);
        Assert.Equal(64, first.PromptSha256.Length);
        Assert.Contains($"map-contract-{MapAuthoringContract.ContentSha256}", first.Plan.Provenance,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelProviderReceivesVersionedContractAndItsTopologyBecomesCanonicalPlan()
    {
        var project = Project();
        var brief = Brief(project, AssistedAreaArchetype.Crypt);
        var providerDefinition = ModelProviderDefinition();
        var provider = new RecordingProvider(ProviderTopology(brief));

        var first = await AssistedAreaPlanProposalService.ProposeAsync(
            brief,
            project,
            LoreIndexDefinition.CreateDefault(),
            provider,
            providerDefinition,
            networkAuthorized: true,
            projectContentAuthorized: true);
        var second = await AssistedAreaPlanProposalService.ProposeAsync(
            brief,
            project,
            LoreIndexDefinition.CreateDefault(),
            provider,
            providerDefinition,
            networkAuthorized: true,
            projectContentAuthorized: true);

        Assert.True(first.Succeeded);
        Assert.NotNull(first.Plan);
        Assert.Equal(AssistedAreaPlanProposalService.PromptVersion, provider.LastRequest!.Prompt.PromptVersion);
        Assert.Contains(MapAuthoringContract.ContentSha256, provider.LastRequest.Prompt.SystemMessage,
            StringComparison.Ordinal);
        Assert.Contains("# Contrat de création de maps par l'IA", provider.LastRequest.Prompt.SystemMessage,
            StringComparison.Ordinal);
        Assert.Contains("Zone de plan", provider.LastRequest.Prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("Kit pierre", provider.LastRequest.Prompt.UserMessage, StringComparison.Ordinal);
        Assert.Contains("AI objectif central", first.Plan.Nodes.Select(node => node.Label));
        Assert.Contains(first.Plan.Nodes, node => node.X == 4 && node.Z == 10);
        Assert.Equal(
            first.Plan.Nodes.Select(node => node.Id),
            second.Plan!.Nodes.Select(node => node.Id));
        Assert.Contains($"map-contract-{MapAuthoringContract.ContentSha256}", first.Plan.Provenance,
            StringComparison.Ordinal);
        Assert.Equal("model-test", first.Plan.ProviderKey);
    }

    [Fact]
    public async Task InvalidModelTopologyIsRejectedWithoutDeterministicFallback()
    {
        var project = Project();
        var brief = Brief(project, AssistedAreaArchetype.Village);
        var provider = new RecordingProvider("""{"summary":"incomplete","nodes":[],"links":[]}""");

        var proposal = await AssistedAreaPlanProposalService.ProposeAsync(
            brief,
            project,
            LoreIndexDefinition.CreateDefault(),
            provider,
            ModelProviderDefinition(),
            networkAuthorized: true,
            projectContentAuthorized: false);

        Assert.False(proposal.Succeeded);
        Assert.Null(proposal.Plan);
        Assert.Equal("no-change", proposal.SemanticDiff);
        Assert.Contains(proposal.Diagnostics,
            diagnostic => diagnostic.Code == AssistedAreaPlanProviderResponseGenerator.ResponseInvalidCode);
        Assert.Contains("[project content withheld]", provider.LastRequest!.Prompt.UserMessage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Zone de plan", provider.LastRequest.Prompt.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Kit pierre", provider.LastRequest.Prompt.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MapAuthoringContractIsEmbeddedVersionedAndHasStableSha256()
    {
        Assert.Equal("2.2.0", MapAuthoringContract.Version);
        Assert.Contains("## Cycle gouverné", MapAuthoringContract.Content, StringComparison.Ordinal);
        Assert.Contains("## Placement extérieur canonique", MapAuthoringContract.Content, StringComparison.Ordinal);
        Assert.Equal(64, MapAuthoringContract.ContentSha256.Length);
        Assert.All(MapAuthoringContract.ContentSha256,
            character => Assert.True(char.IsAsciiHexDigit(character) && !char.IsUpper(character)));
    }

    [Fact]
    public async Task CommandRequiresDryRunAndExplicitApprovalPermissionThenSupportsHistory()
    {
        var project = Project();
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var proposal = await AssistedAreaPlanProposalService.ProposeAsync(
            Brief(project, AssistedAreaArchetype.Crypt), project, LoreIndexDefinition.CreateDefault(),
            new DeterministicAssistantProvider(profile.ActiveProvider), profile.ActiveProvider, false, false);
        var plan = proposal.Plan!;
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-c");
        var history = new AssistedAreaPlanCommandHistory();
        var handler = new SetAssistedAreaPlansHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var previewPlan = Guid.NewGuid();
        SetAssistedAreaPlansCommand Command(bool dryRun, AssistedAreaPlanCatalog value, AssistantPermissionLevel permission) =>
            new(CommandId.From(Guid.NewGuid()), transaction, previewPlan, history.State.Revision, value, permission, dryRun);

        var refused = handler.Handle(history.State, Command(false, catalog, AssistantPermissionLevel.Configure), project);
        var preview = handler.Handle(history.State, Command(true, catalog, AssistantPermissionLevel.Configure), project);
        var applied = handler.Handle(history.State, Command(false, catalog, AssistantPermissionLevel.Configure), project);

        Assert.Equal(SetAssistedAreaPlansHandler.PlanNotPreviewedCode, refused.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        history.Record(applied);
        Assert.Single(history.State.Catalog.Plans);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Catalog.Plans);
        Assert.True(history.Redo());

        var approved = new AssistedAreaPlanCatalog(catalog.Id, 1,
            [plan.WithApproval(true, "author-approved:g12-c")], "test:g12-c");
        var denied = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), history.State.Revision,
                approved, AssistantPermissionLevel.Configure, true), project);
        Assert.Equal(SetAssistedAreaPlansHandler.PermissionDeniedCode, denied.Diagnostics[0].Code);
    }

    [Fact]
    public async Task MissingAreaBlueprintAndLoreReferencesHaveStableDiagnostics()
    {
        var project = Project();
        var proposal = AssistedAreaPlanGenerator.Generate(
            Brief(project, AssistedAreaArchetype.Crypt) with
            {
                AreaId = Guid.NewGuid(),
                KitBlueprintIds = [Guid.NewGuid()],
                LoreSourceIds = ["quest:00000000-0000-4000-8000-000000000099"],
            },
            project,
            "offline-ci",
            0,
            "test",
            new string('a', 64),
            1,
            1);

        Assert.False(proposal.Succeeded);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.AreaMissingCode);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.BlueprintMissingCode);
        Assert.Contains(proposal.Diagnostics, item => item.Code == AssistedAreaPlanProjectValidator.LoreSourceMissingCode);
        await Task.CompletedTask;
    }

    private static AssistedAreaPlanBrief Brief(ProjectDefinition project, AssistedAreaArchetype archetype) => new(
        Guid.Parse("b1240000-0000-4000-8000-000000000010"),
        project.Areas[0].Id.Value,
        archetype == AssistedAreaArchetype.Crypt ? "Crypte du Serment" : "Village des Braises",
        archetype,
        20260805,
        64,
        64,
        8,
        [project.Blueprints[0].Id.Value],
        ["Preserve a navigable route from entry to exit."],
        ["Reach the central objective and return safely."],
        [$"project:{project.Id}"],
        "test:g12-c");

    private static AssistantProviderDefinition ModelProviderDefinition() => new(
        Guid.Parse("b1240000-0000-4000-8000-000000000030"),
        AssistantProviderDefinition.CurrentSchemaVersion,
        "model-test",
        "Model test",
        AssistantProviderKind.OpenAiCompatible,
        "https://example.invalid/v1",
        "test-model",
        string.Empty,
        enabled: true,
        localOnly: false,
        maximumInputTokens: 32_768,
        maximumOutputTokens: 4_096,
        inputCostPerMillionTokens: 0,
        outputCostPerMillionTokens: 0,
        provenance: "test:g12-c-contract");

    private static string ProviderTopology(AssistedAreaPlanBrief brief)
    {
        var blueprintId = brief.KitBlueprintIds[0].ToString("D");
        var nodes = Enumerable.Range(0, brief.NodeCount).Select(index => new
        {
            key = $"node-{index}",
            kind = index switch
            {
                0 => "Entry",
                var last when last == brief.NodeCount - 1 => "Exit",
                4 => "Objective",
                _ => "Exploration",
            },
            label = index == 4 ? "AI objectif central" : $"AI espace {index}",
            x = -24f + (index * 7f),
            z = 10f,
            width = 4f,
            depth = 4f,
            contentBlueprintIds = index is 0 || index == brief.NodeCount - 1
                ? Array.Empty<string>()
                : [blueprintId],
            tags = new[] { "ai-node" },
        }).ToArray();
        var links = Enumerable.Range(0, brief.NodeCount - 1).Select(index => new
        {
            from = $"node-{index}",
            to = $"node-{index + 1}",
            traversal = "door",
            required = true,
        }).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            summary = "Topologie de test produite par le modèle.",
            nodes,
            links,
        });
    }

    private sealed class RecordingProvider(string content) : IAssistantProvider
    {
        public AssistantProviderRequest? LastRequest { get; private set; }

        public Task<AssistantProviderResponse> GenerateAsync(
            AssistantProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(new AssistantProviderResponse(
                "model-test",
                "test-model",
                content,
                500,
                250,
                0.01m,
                "test:model-provider"));
        }
    }

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("b1240000-0000-4000-8000-000000000020")),
            "plan-test",
            "Zone de plan",
            [],
            AreaKind.Exterior,
            new AreaDimensions(96, 96));
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b1240000-0000-4000-8000-000000000021")),
            "kit-stone",
            "Kit pierre",
            BlueprintKind.Placeable);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1240000-0000-4000-8000-000000000022")),
            "Projet de plan",
            [blueprint],
            [area]);
    }
}
