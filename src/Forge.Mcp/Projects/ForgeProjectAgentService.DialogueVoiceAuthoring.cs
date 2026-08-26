using System.Security.Cryptography;
using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Mcp.Dialogues;
using Forge.ModuleFormat.Audio;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    private readonly Dictionary<string, StoredDialogueVoiceImportPlan> _dialogueVoiceImportPlans =
        new(StringComparer.Ordinal);

    public DialogueVoiceImportPlanResult PlanDialogueVoiceImport(DialogueVoiceImportPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
                return VoiceFailure(request.BaseRevision, "dialogue.voice.plan.schema_unsupported",
                    "Dialogue voice import schema must be 1.");
            try
            {
                var dialogueId = DialogueId.Parse(request.DialogueId);
                var nodeId = DialogueNodeId.Parse(request.NodeId);
                var participantId = BlueprintId.Parse(request.ParticipantBlueprintId);
                var extension = Path.GetExtension(request.SourceRelativePath).ToLowerInvariant();
                var authoring = new DialogueVoiceCueAuthoringRequest(dialogueId, nodeId, participantId, string.Empty,
                    request.Locale, request.Take, request.SubtitleKey, request.Subtitle,
                    request.LipSyncProfileId, request.Emotion, request.DurationMilliseconds, request.LicenseSpdx,
                    request.ExpectedSha256, request.AllowSubtitleFallback, request.Provenance);
                var destination = DialogueVoiceCueAuthoringService.DestinationRelativePath(authoring, extension);
                authoring = authoring with { Resource = destination };
                var file = GovernedDialogueVoiceImportService.Plan(_session.RootDirectory,
                    new GovernedDialogueVoiceImportRequest(1, request.SourceRoot, request.SourceRelativePath,
                        destination, request.ExpectedSha256, request.MimeType, request.LicenseSpdx,
                        request.Provenance));
                if (!file.Succeeded)
                    return VoiceFailure(request.BaseRevision, file.Diagnostics.Select(Map).ToArray());
                var authored = DialogueVoiceCueAuthoringService.Build(_history.State.Project, authoring);
                if (!authored.Succeeded)
                    return VoiceFailure(request.BaseRevision, authored.Diagnostics.Select(Map).ToArray());
                var command = new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision), dialogueId, authored.Dialogue!.Presentation, true);
                var preview = _dialoguePresentationHandler.Handle(_history.State, command);
                if (!preview.Succeeded)
                    return VoiceFailure(request.BaseRevision, preview.Diagnostics.Select(Map).ToArray());

                var planId = Guid.NewGuid().ToString("D");
                var plan = new StoredDialogueVoiceImportPlan(planId, command, authored.Cue!, file.Plan!,
                    preview.Patch!, preview.Inverse!,
                    [new("planned", request.BaseRevision, _clock.UtcNow, true)]);
                _dialogueVoiceImportPlans.Add(planId, plan);
                return VoiceResult(plan);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return VoiceFailure(request.BaseRevision, "dialogue.voice.plan.request_invalid", exception.Message);
            }
        }
    }

    public DialogueVoiceImportPlanResult ApplyDialogueVoiceImportPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialogueVoiceImportPlans.TryGetValue(planId, out var plan))
                return VoiceFailure(_history.State.Revision.Value, "dialogue.voice.plan.missing",
                    "Dialogue voice import plan does not exist.");
            if (plan.Applied && !plan.Reverted)
            {
                var path = Forge.Application.Gameplay.RuntimeModuleAssetPathResolver.ResolveVoiceAudio(
                    _session.RootDirectory, plan.FilePlan.DestinationRelativePath);
                if (path is not null && string.Equals(TryHashDialogueVoiceFile(path), plan.FilePlan.ContentSha256,
                        StringComparison.OrdinalIgnoreCase) && HasExactCurrentCue(plan))
                    return VoiceResult(plan, idempotent: true);
                return VoiceFailure(plan.Command.BaseRevision.Value, "dialogue.voice.plan.materialization_changed",
                    "The applied voice materialization is missing or changed.");
            }
            if (plan.Reverted)
                return VoiceFailure(plan.Command.BaseRevision.Value, "dialogue.voice.plan.reverted",
                    "A reverted dialogue voice plan cannot be applied again; create a new plan.");

            var appliedCommand = plan.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
            var commandResult = _dialoguePresentationHandler.Handle(_history.State, appliedCommand);
            if (!commandResult.Succeeded)
                return VoiceFailure(plan.Command.BaseRevision.Value, commandResult.Diagnostics.Select(Map).ToArray());
            var fileResult = GovernedDialogueVoiceImportService.Apply(_session.RootDirectory, plan.FilePlan);
            if (!fileResult.Succeeded)
                return VoiceFailure(plan.Command.BaseRevision.Value, fileResult.Diagnostics.Select(Map).ToArray());
            var saved = _session.Save(commandResult.State.Project);
            if (!saved.Succeeded)
            {
                var compensation = GovernedDialogueVoiceImportService.Revert(
                    _session.RootDirectory, plan.FilePlan, fileResult);
                var diagnostics = saved.Diagnostics.Select(Map).ToList();
                if (!compensation.Succeeded)
                    diagnostics.AddRange(compensation.Diagnostics.Select(Map));
                return VoiceFailure(plan.Command.BaseRevision.Value, diagnostics);
            }
            _history.Record(commandResult);
            plan.Applied = true;
            plan.FileApply = fileResult;
            plan.AppliedCommandId = commandResult.Audit.CommandId.Value;
            plan.Audit.Add(new("applied", _history.State.Revision.Value, _clock.UtcNow, true));
            return VoiceResult(plan);
        }
    }

    public DialogueVoiceImportPlanResult RevertDialogueVoiceImportPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialogueVoiceImportPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted)
                return VoiceFailure(_history.State.Revision.Value, "dialogue.voice.plan.not_applied",
                    "Dialogue voice import plan is not currently applied.");
            if (_history.Journal.LastOrDefault(entry => entry.IsApplied)?.Id != plan.AppliedCommandId)
                return VoiceFailure(plan.Command.BaseRevision.Value, "dialogue.voice.plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            var undone = _history.Undo();
            if (!undone.Succeeded)
                return VoiceFailure(plan.Command.BaseRevision.Value, "dialogue.voice.plan.inverse_unavailable",
                    "Dialogue voice inverse is unavailable.");
            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                var compensation = _history.Redo();
                var diagnostics = saved.Diagnostics.Select(Map).ToList();
                if (!compensation.Succeeded)
                    diagnostics.Add(new AgentDiagnostic("dialogue.voice.plan.compensation_failed",
                        "Saving the inverse failed and the in-memory dialogue could not be restored."));
                return VoiceFailure(plan.Command.BaseRevision.Value, diagnostics);
            }
            var fileRevert = GovernedDialogueVoiceImportService.Revert(
                _session.RootDirectory, plan.FilePlan, plan.FileApply!);
            if (!fileRevert.Succeeded)
            {
                var redone = _history.Redo();
                var diagnostics = fileRevert.Diagnostics.Select(Map).ToList();
                if (!redone.Succeeded)
                    diagnostics.Add(new AgentDiagnostic("dialogue.voice.plan.compensation_failed",
                        "Voice blob retention failed and the dialogue could not be restored in memory."));
                else
                {
                    var resaved = _session.Save(redone.State.Project);
                    if (!resaved.Succeeded) diagnostics.AddRange(resaved.Diagnostics.Select(Map));
                }
                return VoiceFailure(plan.Command.BaseRevision.Value, diagnostics);
            }
            plan.Reverted = true;
            plan.Audit.Add(new("reverted", _history.State.Revision.Value, _clock.UtcNow, true));
            return VoiceResult(plan);
        }
    }

    private DialogueVoiceImportPlanResult VoiceResult(StoredDialogueVoiceImportPlan plan, bool idempotent = false) =>
        new(true, plan.Id, plan.Command.BaseRevision.Value, plan.FilePlan.DestinationRelativePath,
            plan.FilePlan.ContentSha256, plan.FilePlan.SourceBytes, plan.FilePlan.Request.MimeType,
            plan.FilePlan.Reused, plan.Applied, plan.Reverted, idempotent, plan.Patch, plan.Inverse, [],
            plan.Audit.ToArray());

    private static DialogueVoiceImportPlanResult VoiceFailure(long revision, string code, string message) =>
        VoiceFailure(revision, [new AgentDiagnostic(code, message)]);

    private static DialogueVoiceImportPlanResult VoiceFailure(long revision, IReadOnlyList<AgentDiagnostic> diagnostics) =>
        new(false, null, revision, null, null, 0, null, false, false, false, false, null, null,
            diagnostics, []);

    private static string? TryHashDialogueVoiceFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          CryptographicException)
        {
            return null;
        }
    }

    private bool HasExactCurrentCue(StoredDialogueVoiceImportPlan plan)
    {
        var dialogue = _history.State.Project.Dialogues.SingleOrDefault(item => item.Id == plan.Command.DialogueId);
        if (dialogue?.Presentation is null) return false;
        var matches = dialogue.Presentation.VoiceCues.Where(item => item.Id == plan.Cue.Id).ToArray();
        return matches.Length == 1 && matches[0] == plan.Cue;
    }

    private sealed class StoredDialogueVoiceImportPlan(
        string id,
        SetDialoguePresentationCommand command,
        DialogueVoiceCue cue,
        GovernedDialogueVoiceImportPlan filePlan,
        ProjectPatch patch,
        ProjectPatch inverse,
        List<DialogueVoiceImportAuditEntry> audit)
    {
        internal string Id { get; } = id;
        internal SetDialoguePresentationCommand Command { get; } = command;
        internal DialogueVoiceCue Cue { get; } = cue;
        internal GovernedDialogueVoiceImportPlan FilePlan { get; } = filePlan;
        internal ProjectPatch Patch { get; } = patch;
        internal ProjectPatch Inverse { get; } = inverse;
        internal List<DialogueVoiceImportAuditEntry> Audit { get; } = audit;
        internal bool Applied { get; set; }
        internal bool Reverted { get; set; }
        internal Guid? AppliedCommandId { get; set; }
        internal GovernedDialogueVoiceImportApplyResult? FileApply { get; set; }
    }
}
