using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;

namespace Forge.Application.Assistant;

public sealed class SetLoreIndexHandler
{
    public const string RevisionConflictCode = "assistant.lore.revision_conflict";
    public const string TransactionRequiredCode = "assistant.lore.transaction_required";
    public const string PlanRequiredCode = "assistant.lore.plan_required";
    public const string PlanNotPreviewedCode = "assistant.lore.plan_not_previewed";
    public const string PermissionDeniedCode = "assistant.lore.permission_denied";
    private const string CommandName = "set_lore_index";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (LoreIndexRevision Revision, Guid TransactionId, string Fingerprint)> _previewedPlans = [];

    public SetLoreIndexHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public LoreIndexCommandResult Handle(
        LoreIndexState state,
        SetLoreIndexCommand command,
        ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(project);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "Lore index changed before this command was applied.");
        }
        if (command.TransactionId == Guid.Empty)
        {
            return Failure(state, command, TransactionRequiredCode, "Lore index configuration requires a transaction ID.");
        }
        if (command.PlanId == Guid.Empty)
        {
            return Failure(state, command, PlanRequiredCode, "Lore index configuration requires a preview plan ID.");
        }
        if (command.Permission < AssistantPermissionLevel.Configure)
        {
            return Failure(state, command, PermissionDeniedCode, "Lore index configuration permission is insufficient.");
        }
        var diagnostics = LoreIndexProjectValidator.Validate(command.Index, project)
            .Select(item => new CommandDiagnostic(item.Code, item.Message))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new LoreIndexCommandResult(
                false,
                state,
                command.BaseRevision,
                null,
                command.TransactionId,
                command.PlanId,
                diagnostics,
                null,
                null,
                Audit(command, false));
        }

        var patch = new ProjectPatch(
            "replace",
            "assistant/lore-index",
            $"Set governed lore index with {command.Index.Entries.Count} entry or entries.");
        var inverse = new ProjectPatch(
            "replace",
            "assistant/lore-index",
            $"Restore governed lore index with {state.Index.Entries.Count} entry or entries.");
        var fingerprint = Fingerprint(command.Index);
        if (command.DryRun)
        {
            lock (_previewedPlans)
            {
                if (_previewedPlans.Count >= 128) _previewedPlans.Clear();
                _previewedPlans[command.PlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new LoreIndexCommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                command.TransactionId,
                command.PlanId,
                [],
                patch,
                inverse,
                Audit(command, false));
        }

        lock (_previewedPlans)
        {
            if (!_previewedPlans.TryGetValue(command.PlanId, out var preview) ||
                preview.Revision != command.BaseRevision ||
                preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Failure(
                    state,
                    command,
                    PlanNotPreviewedCode,
                    "Lore index configuration must match its successful dry-run transaction and current revision.");
            }
            _previewedPlans.Remove(command.PlanId);
        }

        var revision = state.Revision.Next();
        return new LoreIndexCommandResult(
            true,
            new LoreIndexState(command.Index, revision),
            command.BaseRevision,
            revision,
            command.TransactionId,
            command.PlanId,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private LoreIndexCommandResult Failure(
        LoreIndexState state,
        SetLoreIndexCommand command,
        string code,
        string message) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            command.TransactionId,
            command.PlanId,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            Audit(command, false));

    private AuditEntry Audit(SetLoreIndexCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static string Fingerprint(LoreIndexDefinition index)
    {
        var value = new StringBuilder()
            .Append(index.Id).Append('|').Append(index.SchemaVersion).Append('|')
            .Append(index.Enabled).Append('|').Append(index.MaximumContextCharacters).Append('|')
            .Append(index.Provenance);
        foreach (var entry in index.Entries)
        {
            value.Append("|E|").Append(entry.Id).Append('|').Append(entry.SchemaVersion).Append('|')
                .Append(entry.Kind).Append('|').Append(entry.Key).Append('|').Append(entry.Title).Append('|')
                .Append(entry.Content).Append('|').Append(string.Join(',', entry.SourceIds)).Append('|')
                .Append(string.Join(',', entry.RelatedEntryIds)).Append('|').Append(string.Join(',', entry.Tags))
                .Append('|').Append(entry.Provenance);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }
}
