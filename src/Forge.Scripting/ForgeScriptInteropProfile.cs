using System.Text.Json;

namespace Forge.Scripting;

public sealed record ForgeScriptInteropProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    string EventBinding,
    bool RequiredAtRuntime,
    int MaximumSources,
    int MaximumSourceBytes,
    int TestInstructionLimit,
    int TestTimeoutMilliseconds,
    int TestRecursionLimit,
    int TestSpawnLimit)
{
    public const int CurrentSchemaVersion = 1;

    public static ForgeScriptInteropProfile Default { get; } = new(
        Guid.Parse("c7110000-0000-4000-8000-000000000100"),
        CurrentSchemaVersion,
        "forge:r101",
        "1.0.0",
        "module.script.interop",
        false,
        512,
        524_288,
        10_000,
        1000,
        32,
        8);

    public ForgeScriptQuotas TestQuotas() => new(
        TestInstructionLimit,
        TestTimeoutMilliseconds,
        TestRecursionLimit,
        TestSpawnLimit);
}

public sealed record ForgeScriptInteropDiagnostic(
    string Code,
    string Path,
    Guid? SubjectId,
    string Severity,
    string Message);

public static class ForgeScriptInteropProfileValidator
{
    public static IReadOnlyList<ForgeScriptInteropDiagnostic> Validate(ForgeScriptInteropProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<ForgeScriptInteropDiagnostic>();
        Add(profile.Id == Guid.Empty, "script.interop.uuid_required", "script/interop/id",
            "A stable interop UUID is required.");
        Add(profile.SchemaVersion != ForgeScriptInteropProfile.CurrentSchemaVersion,
            "script.interop.schema_unsupported", "script/interop/schemaVersion",
            "ForgeScript interop schema v1 is required.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance), "script.interop.provenance_required",
            "script/interop/provenance", "Modification provenance is required.");
        Add(!ForgeScriptApiCompatibility.IsSupported(profile.ApiVersion), "script.interop.api_unsupported",
            "script/interop/apiVersion", "The ForgeScript API version is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.EventBinding), "script.interop.event_binding_required",
            "script/interop/eventBinding", "A canonical event binding is required.");
        Add(profile.MaximumSources is < 1 or > 10_000 || profile.MaximumSourceBytes is < 1024 or > 8 * 1024 * 1024,
            "script.interop.source_quota_invalid", "script/interop/sourceQuota",
            "Source count and byte quotas are outside supported bounds.");
        Add(profile.TestInstructionLimit is < 1 or > 10_000_000 ||
            profile.TestTimeoutMilliseconds is < 1 or > 60_000 ||
            profile.TestRecursionLimit is < 1 or > 1024 || profile.TestSpawnLimit is < 0 or > 100_000,
            "script.interop.test_quota_invalid", "script/interop/testQuotas",
            "Sandbox test quotas are outside supported bounds.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, profile.Id == Guid.Empty ? null : profile.Id,
                "error", message));
        }
    }
}

public sealed record ForgeScriptInteropCommand(
    Guid TransactionId,
    long ExpectedRevision,
    ForgeScriptInteropProfile Profile,
    string Reason);

public sealed record ForgeScriptInteropMutation(
    bool Succeeded,
    ForgeScriptInteropProfile Profile,
    long Revision,
    string SemanticDiff,
    IReadOnlyList<ForgeScriptInteropDiagnostic> Diagnostics);

public sealed class ForgeScriptInteropHistory(ForgeScriptInteropProfile initial)
{
    private readonly Stack<ForgeScriptInteropProfile> _undo = [];
    private readonly Stack<ForgeScriptInteropProfile> _redo = [];
    public ForgeScriptInteropProfile Profile { get; private set; } = initial ?? throw new ArgumentNullException(nameof(initial));
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptInteropMutation Preview(ForgeScriptInteropCommand command) => Evaluate(command);

    public ForgeScriptInteropMutation Apply(ForgeScriptInteropCommand command)
    {
        var result = Evaluate(command);
        if (!result.Succeeded) return result;
        _undo.Push(Profile);
        _redo.Clear();
        Profile = command.Profile;
        Revision++;
        return result with { Profile = Profile, Revision = Revision };
    }

    public ForgeScriptInteropMutation Undo()
    {
        if (!CanUndo) return Failure("script.interop.undo_empty", "script/interop/history", "Nothing to undo.");
        _redo.Push(Profile);
        Profile = _undo.Pop();
        Revision++;
        return new(true, Profile, Revision, "undo:script-interop", []);
    }

    public ForgeScriptInteropMutation Redo()
    {
        if (!CanRedo) return Failure("script.interop.redo_empty", "script/interop/history", "Nothing to redo.");
        _undo.Push(Profile);
        Profile = _redo.Pop();
        Revision++;
        return new(true, Profile, Revision, "redo:script-interop", []);
    }

    private ForgeScriptInteropMutation Evaluate(ForgeScriptInteropCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TransactionId == Guid.Empty || command.ExpectedRevision != Revision ||
            string.IsNullOrWhiteSpace(command.Reason))
            return Failure("script.interop.command_invalid", "script/interop/command",
                "Transaction identity, current revision and reason are required.");
        var diagnostics = ForgeScriptInteropProfileValidator.Validate(command.Profile);
        return diagnostics.Count == 0
            ? new(true, command.Profile, Revision,
                $"interop:{Profile.Id:D}|sources:{Profile.MaximumSources}->{command.Profile.MaximumSources}|bytes:{Profile.MaximumSourceBytes}->{command.Profile.MaximumSourceBytes}", [])
            : new(false, Profile, Revision, "no-change", diagnostics);
    }

    private ForgeScriptInteropMutation Failure(string code, string path, string message) =>
        new(false, Profile, Revision, "no-change", [new(code, path, Profile.Id, "error", message)]);
}

public sealed class ForgeScriptInteropProfileStore(string root)
{
    public const string RelativePath = "forge-script-interop.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));

    public void Save(ForgeScriptInteropProfile profile)
    {
        var diagnostics = ForgeScriptInteropProfileValidator.Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(string.Join(';', diagnostics.Select(item => item.Code)));
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, RelativePath);
        var temporary = destination + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, JsonOptions));
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public ForgeScriptInteropProfile Load()
    {
        var profile = JsonSerializer.Deserialize<ForgeScriptInteropProfile>(
                          File.ReadAllText(Path.Combine(_root, RelativePath)), JsonOptions)
                      ?? throw new InvalidOperationException("script.interop.document_invalid");
        var diagnostics = ForgeScriptInteropProfileValidator.Validate(profile);
        if (diagnostics.Count > 0) throw new InvalidOperationException(string.Join(';', diagnostics.Select(item => item.Code)));
        return profile;
    }
}
