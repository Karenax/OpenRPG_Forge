using System.Text;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssistedCanonicalForgeScriptWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-script-canonical-tests",
        Guid.NewGuid().ToString("N"));
    private readonly NativeForgeScriptRuntime _runtime = new();

    public AssistedCanonicalForgeScriptWorkflowTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ApprovedProposalUsesCanonicalSourceStoreAndRestoresExactSourceMetadataPair()
    {
        var proposal = AssistedForgeScriptCreationService.Generate(Brief(), _root, _runtime)
            .WithApproval(true, "author-approved:test:ux6-q");
        var document = Document(proposal);
        var handler = new ApplyAssistedCanonicalForgeScriptHandler();
        var history = new AssistedCanonicalForgeScriptCommandHistory();
        var transactionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        ApplyAssistedCanonicalForgeScriptCommand Command(bool dryRun) => new(
            Guid.NewGuid(), transactionId, planId, 11, proposal, document,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var withoutPreview = handler.Handle(_root, 11, Command(false));
        var preview = handler.Handle(_root, 11, Command(true));

        Assert.Contains(ApplyAssistedCanonicalForgeScriptHandler.PlanNotPreviewedCode,
            withoutPreview.Diagnostics);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.False(File.Exists(Path.Combine(_root, "scripts", "ux6-q-assisted.as")));

        var applied = handler.Handle(_root, 11, Command(false));
        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics));
        Assert.True(applied.Applied);
        var loaded = new ForgeScriptSourceStore(_root).Load(document.RelativePath);
        Assert.Equal(document.Id, loaded.Id);
        Assert.Equal(document.Provenance, loaded.Provenance);
        Assert.Equal(document.RelativePath, loaded.RelativePath);
        Assert.Equal(document.Source, loaded.Source);
        Assert.Equal(document.ApiVersion, loaded.ApiVersion);
        Assert.Equal(document.Distribution, loaded.Distribution);
        Assert.Equal(document.RequiredAtRuntime, loaded.RequiredAtRuntime);
        Assert.Equal(document.Dependencies, loaded.Dependencies);
        Assert.Equal(document.EventBindings, loaded.EventBindings);
        history.Record(applied);
        var appliedSource = File.ReadAllBytes(Path.Combine(_root, "scripts", "ux6-q-assisted.as"));
        var appliedMetadata = File.ReadAllBytes(Path.Combine(_root, "scripts", "ux6-q-assisted.as.meta.json"));

        Assert.True(history.Undo(_root));
        Assert.False(File.Exists(Path.Combine(_root, "scripts", "ux6-q-assisted.as")));
        Assert.False(File.Exists(Path.Combine(_root, "scripts", "ux6-q-assisted.as.meta.json")));
        Assert.True(history.Redo(_root));
        Assert.Equal(appliedSource, File.ReadAllBytes(Path.Combine(_root, "scripts", "ux6-q-assisted.as")));
        Assert.Equal(appliedMetadata,
            File.ReadAllBytes(Path.Combine(_root, "scripts", "ux6-q-assisted.as.meta.json")));
    }

    [Fact]
    public void CanonicalPlanRejectsPermissionStaleSourceAndMetadataMutation()
    {
        var initial = new ForgeScriptSourceDocument(
            Brief().ScriptId, 1, "author-approved:existing", Brief().RelativePath,
            "void main()\n{\n    say(\"Avant\");\n}\n", _runtime.ApiVersion,
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [], []);
        new ForgeScriptSourceStore(_root).Save(initial);
        var brief = Brief() with { SourceOverride = "void main()\n{\n    say(\"Après\");\n}\n" };
        var proposal = AssistedForgeScriptCreationService.Generate(brief, _root, _runtime)
            .WithApproval(true, "author-approved:test:ux6-q");
        var document = Document(proposal);
        var handler = new ApplyAssistedCanonicalForgeScriptHandler();
        var transactionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        ApplyAssistedCanonicalForgeScriptCommand Command(bool dryRun, AssistantPermissionLevel permission) => new(
            Guid.NewGuid(), transactionId, planId, 4, proposal, document, permission, dryRun);

        var denied = handler.Handle(_root, 4, Command(true, AssistantPermissionLevel.Configure));
        Assert.Contains(ApplyAssistedCanonicalForgeScriptHandler.PermissionDeniedCode, denied.Diagnostics);
        Assert.True(handler.Handle(_root, 4,
            Command(true, AssistantPermissionLevel.ApplyApprovedPlan)).Succeeded);
        File.AppendAllText(Path.Combine(_root, "scripts", "ux6-q-assisted.as.meta.json"), " ", Encoding.UTF8);
        var staleMetadata = handler.Handle(_root, 4,
            Command(false, AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.Contains(ApplyAssistedCanonicalForgeScriptHandler.PlanNotPreviewedCode, staleMetadata.Diagnostics);

        var staleBrief = brief with { SourceOverride = "void main()\n{\n    say(\"Encore\");\n}\n" };
        var staleProposal = AssistedForgeScriptCreationService.Generate(staleBrief, _root, _runtime)
            .WithApproval(true, "author-approved:test:ux6-q");
        File.AppendAllText(Path.Combine(_root, "scripts", "ux6-q-assisted.as"), "// external\n", Encoding.UTF8);
        var staleDocument = Document(staleProposal);
        var sourceChanged = handler.Handle(_root, 4, new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4,
            staleProposal, staleDocument, AssistantPermissionLevel.ApplyApprovedPlan, true));
        Assert.Contains(ApplyAssistedCanonicalForgeScriptHandler.SourceChangedCode, sourceChanged.Diagnostics);
    }

    [Fact]
    public void GeneratorAcceptsCanonicalAsButRejectsOtherExtensions()
    {
        var canonical = AssistedForgeScriptCreationService.Generate(Brief(), _root, _runtime);

        Assert.True(canonical.CanApply);
        Assert.EndsWith(".as", canonical.RelativePath, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => AssistedForgeScriptCreationService.Generate(
            Brief() with { RelativePath = "scripts/unsafe.txt" }, _root, _runtime));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }

    private static AssistedForgeScriptBrief Brief() => new(
        Guid.Parse("72000000-0000-4000-8000-000000000018"),
        "scripts/ux6-q-assisted.as",
        "OnUse",
        "Créer une interaction locale déterministe.",
        "Une étincelle répond.",
        "assistant.script.used",
        "true",
        ["lore:studio-ux6-q"],
        ["style:brief", "sandbox:no-external-io"],
        "@studio-author",
        20260821);

    private static ForgeScriptSourceDocument Document(AssistedForgeScriptProposal proposal) => new(
        proposal.ScriptId,
        ForgeScriptSourceDocument.CurrentSchemaVersion,
        proposal.Provenance,
        proposal.RelativePath,
        proposal.Source,
        proposal.ForgeScriptApiVersion,
        ForgeScriptSourceDistributionPolicy.SourceAndBytecode,
        false,
        [],
        []);
}
