using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssistedForgeScriptWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-script-tests", Guid.NewGuid().ToString("N"));
    private readonly NativeForgeScriptRuntime _runtime = new();

    public AssistedForgeScriptWorkflowTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void GeneratorRetrievesApiCompilesRunsTestsAndIsDeterministic()
    {
        var brief = Brief();

        var first = AssistedForgeScriptCreationService.Generate(brief, _root, _runtime);
        var second = AssistedForgeScriptCreationService.Generate(brief, _root, _runtime);

        Assert.True(first.Succeeded, string.Join(" | ", first.Tests.Select(item => $"{item.Id}:{item.Passed}:{item.Detail}")) +
                                     " diagnostics=" + string.Join(" | ", first.Diagnostics.Select(item => item.Message)));
        Assert.True(first.CanApply);
        Assert.Equal("1.0.0", first.ForgeScriptApiVersion);
        Assert.Equal("2.38.0", first.VirtualMachineVersion);
        Assert.DoesNotContain(first.Diagnostics, item => item.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, first.Tests.Count);
        Assert.All(first.Tests, test => Assert.True(test.Passed));
        Assert.Equal(first.Source, second.Source);
        Assert.Equal(first.SourceSha256, second.SourceSha256);
        Assert.Contains("say(\"Une braise répond.\")", first.Source, StringComparison.Ordinal);
        Assert.Contains("add-script:scripts/g12g-test.forge", first.SemanticDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedPassingSourceRequiresExactDryRunAndSupportsUndoRedoRoundTrip()
    {
        var proposal = AssistedForgeScriptCreationService.Generate(Brief(), _root, _runtime)
            .WithApproval(true, "author-approved:test:g12-g");
        var handler = new ApplyAssistedForgeScriptHandler();
        var history = new AssistedForgeScriptCommandHistory();
        var transactionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        ApplyAssistedForgeScriptCommand Command(bool dryRun) => new(
            Guid.NewGuid(), transactionId, planId, 7, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var withoutPreview = handler.Handle(_root, 7, Command(false));
        var preview = handler.Handle(_root, 7, Command(true));
        var path = AssistedForgeScriptCreationService.ResolvePath(_root, proposal.RelativePath);

        Assert.Contains(ApplyAssistedForgeScriptHandler.PlanNotPreviewedCode, withoutPreview.Diagnostics);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.False(File.Exists(path));
        var applied = handler.Handle(_root, 7, Command(false));
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(proposal.Source, File.ReadAllText(path));
        history.Record(applied);
        Assert.True(history.Undo(_root));
        Assert.False(File.Exists(path));
        Assert.True(history.Redo(_root));
        Assert.Equal(proposal.Source, File.ReadAllText(path));
    }

    [Fact]
    public void InvalidSourceUnsafePathDraftAndInsufficientPermissionAreRejected()
    {
        Assert.Throws<ArgumentException>(() => AssistedForgeScriptCreationService.Generate(
            Brief() with { RelativePath = "../escape.forge" }, _root, _runtime));
        var invalid = AssistedForgeScriptCreationService.Generate(
            Brief() with { SourceOverride = "void main( {" }, _root, _runtime);
        Assert.False(invalid.CanApply);
        Assert.NotEmpty(invalid.Diagnostics);
        var proposal = AssistedForgeScriptCreationService.Generate(Brief(), _root, _runtime);
        var handler = new ApplyAssistedForgeScriptHandler();
        ApplyAssistedForgeScriptCommand Command(AssistedForgeScriptProposal value, AssistantPermissionLevel permission) =>
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, value, permission, true);

        var draft = handler.Handle(_root, 0, Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan));
        var insufficient = handler.Handle(_root, 0, Command(
            proposal.WithApproval(true, "author-approved:test:g12-g"), AssistantPermissionLevel.Configure));

        Assert.Contains(ApplyAssistedForgeScriptHandler.ApprovalRequiredCode, draft.Diagnostics);
        Assert.Contains(ApplyAssistedForgeScriptHandler.PermissionDeniedCode, insufficient.Diagnostics);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }

    private static AssistedForgeScriptBrief Brief() => new(
        Guid.Parse("b1280000-0000-4000-8000-000000000001"),
        "scripts/g12g-test.forge",
        "OnUse",
        "Informer le joueur qu'une braise répond et enregistrer l'interaction.",
        "Une braise répond.",
        "assistant.script.used",
        "true",
        ["lore:crypt-of-ashes"],
        ["style:brief"],
        "@author",
        20260806);
}
