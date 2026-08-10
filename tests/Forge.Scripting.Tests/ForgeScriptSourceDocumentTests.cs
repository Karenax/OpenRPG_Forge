namespace Forge.Scripting.Tests;

public sealed class ForgeScriptSourceDocumentTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "openrpg-forge-script-source-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Canonical_source_validates_typed_multi_handler_entries_and_deterministic_dispatch()
    {
        var contract = new ForgeScriptEventContract(Guid.NewGuid(), 1, "test:event", "creature.damaged", "int", true);
        var high = Binding("OnDamagedHigh", 100, "is_enraged");
        var low = Binding("OnDamagedLow", 20, null);
        var document = Document(
            "void OnDamagedLow(const int &in e) {}\n" +
            "void OnDamagedHigh(const int &in e) {}\n" +
            "void main() { say(\"ready\"); }\n",
            [low, high]);

        var diagnostics = ForgeScriptSourceValidator.Validate(document, [contract]);
        var resolved = ForgeScriptSourceValidator.ResolveHandlers(document, "creature.combat",
            "creature.damaged", ForgeScriptEventAuthority.Server, condition => condition == "is_enraged");

        Assert.Empty(diagnostics);
        Assert.Equal([high.Id, low.Id], resolved.Select(item => item.Id));
        Assert.Equal("void OnDamagedHigh(const int &in e)",
            ForgeScriptSourceValidator.EntryDeclaration("OnDamagedHigh", "int"));
    }

    [Fact]
    public void Validation_reports_stable_paths_and_only_required_missing_sources()
    {
        var contract = new ForgeScriptEventContract(Guid.NewGuid(), 1, "test:event", "creature.damaged", "int", true);
        var broken = Document("void main() {}", [Binding("MissingHandler", 0, null)]) with
        {
            RelativePath = "../unsafe.as",
        };

        var diagnostics = ForgeScriptSourceValidator.Validate(broken, [contract]);
        var references = ForgeScriptSourceValidator.ValidateRequirements(
        [
            new(Guid.NewGuid(), "scripts/optional.as", false),
            new(Guid.NewGuid(), "scripts/required.as", true),
        ], [Document("void main() {}", [])]);

        Assert.Contains(diagnostics, item => item.Code == "script.source.path_invalid" &&
                                             item.Path == "script/relativePath");
        Assert.Contains(diagnostics, item => item.Code == "script.binding.entry_missing" &&
                                             item.Path == "script/eventBindings/0/handler");
        Assert.Single(references);
        Assert.Equal("script.source.required_missing", references[0].Code);
        Assert.Contains("required.as", references[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_and_metadata_round_trip_exactly_while_bytecode_stays_outside_the_contract()
    {
        var source = "void main()\r\n{\r\n    say(\"exact\");\r\n}\r\n";
        var document = Document(source, []) with
        {
            Distribution = ForgeScriptSourceDistributionPolicy.SourceAndBytecode,
            RequiredAtRuntime = true,
        };
        var store = new ForgeScriptSourceStore(_workspace);

        store.Save(document);
        var restored = store.Load(document.RelativePath);

        Assert.Equal(document.Id, restored.Id);
        Assert.Equal(document.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(document.Provenance, restored.Provenance);
        Assert.Equal(document.RelativePath, restored.RelativePath);
        Assert.Equal(document.ApiVersion, restored.ApiVersion);
        Assert.Equal(document.Distribution, restored.Distribution);
        Assert.Equal(document.RequiredAtRuntime, restored.RequiredAtRuntime);
        Assert.Equal(document.Dependencies, restored.Dependencies);
        Assert.Equal(document.EventBindings, restored.EventBindings);
        Assert.Equal(source, restored.Source);
        Assert.True(File.Exists(Path.Combine(_workspace, "scripts", "combat.as")));
        Assert.True(File.Exists(Path.Combine(_workspace, "scripts", "combat.as.meta.json")));
        Assert.Empty(Directory.GetFiles(_workspace, "*.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public void Command_history_previews_applies_and_round_trips_source_mutations()
    {
        var initial = Document("void main() {}", []);
        var history = new ForgeScriptSourceHistory(initial, new NativeForgeScriptRuntime(), []);
        var changed = initial with { Source = "void main() { say(\"changed\"); }" };
        var command = new ForgeScriptSourceCommand(Guid.NewGuid(), 0, changed, "author-edit");

        var preview = history.Preview(command);
        var applied = history.Apply(command);
        var undone = history.Undo();
        var redone = history.Redo();

        Assert.True(preview.Succeeded);
        Assert.Equal(0, preview.Revision);
        Assert.True(applied.Succeeded);
        Assert.Equal(changed.Source, applied.Document.Source);
        Assert.Equal(initial.Source, undone.Document.Source);
        Assert.Equal(changed.Source, redone.Document.Source);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
    }

    private static ForgeScriptSourceDocument Document(string source, IReadOnlyList<ForgeScriptEventBinding> bindings) =>
        new(Guid.Parse("12000000-0000-4000-8000-000000000001"), 1, "test:source", "scripts/combat.as",
            source, "1.0.0", ForgeScriptSourceDistributionPolicy.SourceOnly, false, [], bindings);

    private static ForgeScriptEventBinding Binding(string handler, int priority, string? condition) =>
        new(Guid.NewGuid(), 1, "test:binding", "creature.combat", "creature.damaged", handler, "int",
            priority, condition, ForgeScriptEventAuthority.Server, true);
}
