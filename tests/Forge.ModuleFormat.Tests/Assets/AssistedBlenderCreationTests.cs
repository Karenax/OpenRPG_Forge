using System.Text.Json;
using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssistedBlenderCreationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-assisted-blender", Guid.NewGuid().ToString("N"));

    public AssistedBlenderCreationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void GeneratesCanonicalTypedRecipeConfigurationAndFiveTests()
    {
        var proposal = AssistedBlenderCreationService.Generate(Brief(), _root);

        Assert.True(proposal.CanApply);
        Assert.Empty(proposal.Diagnostics);
        Assert.Equal(5, proposal.Tests.Count);
        Assert.All(proposal.Tests, item => Assert.True(item.Passed));
        Assert.Contains(AssetExternalOperation.CreatePrimitives, proposal.Configuration.Operations);
        Assert.Equal("assets/generated/forge-pillar/model.glb", proposal.Definition.OutputRelativePath);
        using var recipe = JsonDocument.Parse(proposal.RecipeJson);
        Assert.Equal("cylinder", recipe.RootElement.GetProperty("primitives")[0].GetProperty("kind").GetString());
        Assert.Equal("cube", recipe.RootElement.GetProperty("uvProjection").GetString());
    }

    [Fact]
    public void ApplyRequiresApprovalPermissionAndExactPreviewThenPersistsAndRestoresBatch()
    {
        var handler = new ApplyAssistedBlenderCreationHandler(new FixedClock());
        var state = new AssetExternalEditState();
        var proposal = AssistedBlenderCreationService.Generate(Brief(), _root);
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedBlenderCreationCommand Command(AssistedBlenderCreationProposal value,
            AssistantPermissionLevel permission, bool dryRun) => new(CommandId.From(Guid.NewGuid()), transaction,
            plan, state.Revision, value, permission, dryRun);

        Assert.Equal(ApplyAssistedBlenderCreationHandler.ApprovalRequiredCode,
            Assert.Single(handler.Handle(_root, state,
                Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan, true)).Diagnostics));
        var approved = proposal.WithApproval(true, "author-approved:test:g12-j");
        Assert.Equal(ApplyAssistedBlenderCreationHandler.PermissionDeniedCode,
            Assert.Single(handler.Handle(_root, state, Command(approved, AssistantPermissionLevel.Propose, true)).Diagnostics));
        Assert.True(handler.Handle(_root, state,
            Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, true)).Succeeded);
        var applied = handler.Handle(_root, state,
            Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, false));
        var history = new AssistedBlenderCreationCommandHistory(state);
        history.Record(applied);

        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(AssistedBlenderCreationService.ResolvePath(_root, approved.RecipeRelativePath)));
        Assert.True(File.Exists(Path.Combine(_root, "assets", "external-edits.json")));
        Assert.True(File.Exists(Path.Combine(_root, "assets", "roundtrip",
            $"{approved.Configuration.AssetId}.worker-request.json")));
        Assert.Contains("\"create_primitives\"", File.ReadAllText(Path.Combine(_root, "assets", "roundtrip",
            $"{approved.Configuration.AssetId}.worker-request.json")), StringComparison.Ordinal);
        Assert.True(history.Undo(_root));
        Assert.False(File.Exists(AssistedBlenderCreationService.ResolvePath(_root, approved.RecipeRelativePath)));
        Assert.Empty(history.State.Configurations);
        Assert.True(history.Redo(_root));
        Assert.True(File.Exists(AssistedBlenderCreationService.ResolvePath(_root, approved.RecipeRelativePath)));
        Assert.Single(history.State.Configurations);
    }

    [Fact]
    public void RejectsUnsafeOutputDuplicatePrimitiveAndTamperedPlan()
    {
        var invalid = new AssistedBlenderCreationDefinition(1, Guid.NewGuid(), "pillar", "Pillar", "author:test",
            "CC0-1.0", [Primitive(), Primitive()], Material(), BlenderUvProjection.Cube, [0.5m], true,
            "../model.glb");
        var codes = AssistedBlenderCreationValidator.Validate(invalid).Select(item => item.Code).ToArray();
        Assert.Contains(AssistedBlenderCreationValidator.PrimitiveInvalidCode, codes);
        Assert.Contains(AssistedBlenderCreationValidator.PathUnsafeCode, codes);

        var handler = new ApplyAssistedBlenderCreationHandler(new FixedClock());
        var state = new AssetExternalEditState();
        var approved = AssistedBlenderCreationService.Generate(Brief(), _root)
            .WithApproval(true, "author-approved:test:g12-j");
        var command = new ApplyAssistedBlenderCreationCommand(CommandId.From(Guid.NewGuid()), Guid.NewGuid(),
            Guid.NewGuid(), state.Revision, approved, AssistantPermissionLevel.ApplyApprovedPlan, true);
        Assert.True(handler.Handle(_root, state, command).Succeeded);
        var tampered = approved with { SemanticDiff = approved.SemanticDiff + "|tampered" };
        Assert.Equal(ApplyAssistedBlenderCreationHandler.PlanNotPreviewedCode,
            Assert.Single(handler.Handle(_root, state, command with
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

    private static AssistedBlenderCreationBrief Brief() => new(
        Guid.Parse("b1310000-0000-4000-8000-000000000001"), "forge-pillar", "Forge Pillar",
        [Primitive()], Material(), BlenderUvProjection.Cube, [0.5m, 0.25m], true,
        ["lore:forge-architecture"], ["style:readable", "silhouette:strong"], "author:test", 20260806);

    private static BlenderPrimitiveDefinition Primitive() => new(
        Guid.Parse("b1310000-0000-4000-8000-000000000002"), "pillar", BlenderPrimitiveKind.Cylinder,
        new(0, 0, 1), new(0, 0, 0), new(1, 1, 2));

    private static BlenderMaterialDefinition Material() =>
        new("forge_stone", new BlenderColor(0.32m, 0.24m, 0.18m, 1), 0.05m, 0.72m);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-08-06T00:00:00Z");
    }
}
