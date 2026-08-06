using System.Text.Json;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssistedModel3DGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-assisted-model3d", Guid.NewGuid().ToString("N"));

    public AssistedModel3DGenerationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task OfflineProposalStagesNothingUntilHumanApproval()
    {
        var proposal = await AssistedModel3DGenerationService.ProposeAsync(
            Brief(), _root, new DeterministicModel3DGenerationProvider(), networkAuthorized: false);

        Assert.True(proposal.Succeeded);
        Assert.False(proposal.CanApply);
        Assert.Empty(proposal.Diagnostics);
        Assert.Equal(5, proposal.Tests.Count);
        Assert.False(File.Exists(Path.Combine(_root, "assets", "staging", "ai-3d", "forge-idol", "forge-idol.obj")));
        Assert.Equal("CC0-1.0", proposal.Receipt.LicenseSpdx);
        using var receipt = JsonDocument.Parse(proposal.ReceiptJson);
        Assert.False(receipt.RootElement.GetProperty("humanAccepted").GetBoolean());

        var approved = proposal.WithApproval(true, "author:test:g12-k");
        Assert.True(approved.CanApply);
        Assert.True(approved.Receipt.HumanAccepted);
        Assert.Contains("author:test:g12-k", approved.ReceiptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImageInputIsReadFromConfinedWorkspaceAndHashedIntoReceipt()
    {
        var relative = "assets/references/idol.png";
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(path, bytes);
        var brief = Brief() with { InputKind = Model3DInputKind.Image, ImageSourceRelativePath = relative };

        var proposal = await AssistedModel3DGenerationService.ProposeAsync(
            brief, _root, new DeterministicModel3DGenerationProvider(), false);

        Assert.True(proposal.Succeeded);
        Assert.Equal(relative, proposal.Receipt.ImageSourceRelativePath);
        Assert.Equal(AssistedModel3DGenerationService.Sha256(bytes), proposal.Receipt.ImageSourceSha256);
        Assert.False(File.Exists(Path.Combine(_root, "assets", "staging", "ai-3d", "forge-idol", "forge-idol.obj")));
    }

    [Fact]
    public async Task ApplyRequiresPreviewThenPersistsFourFilesAndSupportsUndoRedo()
    {
        var proposal = (await AssistedModel3DGenerationService.ProposeAsync(
            Brief(), _root, new DeterministicModel3DGenerationProvider(), false))
            .WithApproval(true, "author:test:g12-k");
        var handler = new ApplyAssistedModel3DGenerationHandler(new FixedClock());
        var state = new AssetExternalEditState();
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedModel3DGenerationCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, plan, state.Revision, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        Assert.True(handler.Handle(_root, state, Command(true)).Succeeded);
        var applied = handler.Handle(_root, state, Command(false));
        var history = new AssistedModel3DGenerationCommandHistory(state);
        history.Record(applied);

        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(4, applied.Patches.Count);
        Assert.All(applied.Patches, patch => Assert.True(File.Exists(Path.Combine(_root,
            patch.RelativePath.Replace('/', Path.DirectorySeparatorChar)))));
        var request = File.ReadAllText(Path.Combine(_root, "assets", "roundtrip",
            $"{proposal.Configuration.AssetId}.worker-request.json"));
        Assert.Contains("assets/staging/ai-3d/forge-idol/forge-idol.obj", request, StringComparison.Ordinal);
        using (var requestDocument = System.Text.Json.JsonDocument.Parse(request))
        {
            Assert.DoesNotContain(requestDocument.RootElement.GetProperty("operations").EnumerateArray(),
                item => item.GetString() == "create_primitives");
            Assert.Contains(requestDocument.RootElement.GetProperty("securityPolicy")
                    .GetProperty("allowedBlenderOperations").EnumerateArray(),
                item => item.GetString() == "create_primitives");
        }
        Assert.True(history.Undo(_root));
        Assert.All(applied.Patches, patch => Assert.False(File.Exists(Path.Combine(_root,
            patch.RelativePath.Replace('/', Path.DirectorySeparatorChar)))));
        Assert.True(history.Redo(_root));
        Assert.All(applied.Patches, patch => Assert.True(File.Exists(Path.Combine(_root,
            patch.RelativePath.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Fact]
    public async Task ApprovalPermissionAndTamperedPlanAreBlocked()
    {
        var proposal = await AssistedModel3DGenerationService.ProposeAsync(
            Brief(), _root, new DeterministicModel3DGenerationProvider(), false);
        var handler = new ApplyAssistedModel3DGenerationHandler(new FixedClock());
        var state = new AssetExternalEditState();
        var baseCommand = new ApplyAssistedModel3DGenerationCommand(CommandId.From(Guid.NewGuid()), Guid.NewGuid(),
            Guid.NewGuid(), state.Revision, proposal, AssistantPermissionLevel.ApplyApprovedPlan, true);

        Assert.Equal(ApplyAssistedModel3DGenerationHandler.ApprovalRequiredCode,
            Assert.Single(handler.Handle(_root, state, baseCommand).Diagnostics));
        var approved = proposal.WithApproval(true, "author:test:g12-k");
        var alteredSource = approved with { SourceObj = approved.SourceObj + "# altered\n" };
        Assert.Equal(ApplyAssistedModel3DGenerationHandler.ProposalInvalidCode,
            Assert.Single(handler.Handle(_root, state, baseCommand with { Proposal = alteredSource }).Diagnostics));
        Assert.Equal(ApplyAssistedModel3DGenerationHandler.PermissionDeniedCode,
            Assert.Single(handler.Handle(_root, state, baseCommand with
            {
                Proposal = approved,
                Permission = AssistantPermissionLevel.Propose,
            }).Diagnostics));
        var previewCommand = baseCommand with { Proposal = approved };
        Assert.True(handler.Handle(_root, state, previewCommand).Succeeded);
        var tampered = approved with { SemanticDiff = approved.SemanticDiff + "|tampered" };
        Assert.Equal(ApplyAssistedModel3DGenerationHandler.PlanNotPreviewedCode,
            Assert.Single(handler.Handle(_root, state, previewCommand with
            {
                CommandId = CommandId.From(Guid.NewGuid()),
                Proposal = tampered,
                DryRun = false,
            }).Diagnostics));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static AssistedModel3DGenerationBrief Brief() => new(
        Guid.Parse("b1330000-0000-4000-8000-000000000030"), "forge-idol", "Forge Idol",
        Model3DInputKind.Text, "A compact weathered stone idol", null, Model3DProviderDefinition.CreateOffline(),
        new Model3DTopologyPolicy(10_000, [0.5m, 0.25m], true),
        ["lore:forge-idol"], ["style:readable", "silhouette:strong"], "author:test", 20260806);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-08-06T00:00:00Z");
    }
}
