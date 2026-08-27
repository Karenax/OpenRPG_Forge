using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.ModuleFormat.Audio;

namespace Forge.Studio.Session;

internal sealed record StudioDialogueVoiceNodeChoice(Guid Id, string Label);

internal sealed record StudioDialogueVoiceDraft(
    long BaseRevision,
    Guid DialogueId,
    Guid NodeId,
    Guid ParticipantBlueprintId,
    string SourceRoot,
    string SourceRelativePath,
    string MimeType,
    string Locale,
    string Take,
    string SubtitleKey,
    string Subtitle,
    string LipSyncProfileId,
    string Emotion,
    int DurationMilliseconds,
    string LicenseSpdx,
    bool AllowSubtitleFallback,
    string Provenance);

internal sealed record StudioDialogueVoicePreview(
    bool Succeeded,
    string DestinationRelativePath,
    string ContentSha256,
    long SourceBytes,
    bool Reused,
    string Diff,
    string Inverse,
    string RuntimeBehavior,
    IReadOnlyList<string> Diagnostics);

internal sealed partial class StudioSessionController
{
    private readonly SetDialoguePresentationHandler _dialogueVoicePresentationHandler = new(new SystemClock());
    private ApprovedDialogueVoiceImport? _approvedDialogueVoiceImport;
    private AppliedDialogueVoiceImport? _appliedDialogueVoiceImport;
    internal string? DialogueVoiceLifecycleWarning { get; private set; }

    internal IReadOnlyList<StudioDialogueVoiceNodeChoice> DialogueVoiceNodeChoices(Guid dialogueId)
    {
        var dialogue = Project?.Dialogues.SingleOrDefault(item => item.Id.Value == dialogueId);
        if (dialogue is null) return [];
        return dialogue.Nodes.Where(item => item.SpeakerBlueprintId is not null)
            .Select(item => new StudioDialogueVoiceNodeChoice(item.Id.Value,
                $"{item.Text.Fallback} · {Project!.Blueprints.Single(blueprint => blueprint.Id == item.SpeakerBlueprintId).DisplayName}"))
            .ToArray();
    }

    internal StudioDialogueVoiceDraft CreateDialogueVoiceDraft(Guid dialogueId, Guid nodeId, string sourcePath)
    {
        if (History is null) throw new InvalidOperationException("Aucun module n'est ouvert.");
        var dialogue = History.State.Project.Dialogues.Single(item => item.Id.Value == dialogueId);
        var node = dialogue.Nodes.Single(item => item.Id.Value == nodeId);
        var fullPath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(fullPath);
        return new(History.State.Revision.Value, dialogueId, nodeId,
            node.SpeakerBlueprintId?.Value ?? throw new InvalidOperationException("Le nœud sélectionné n'a pas de locuteur."),
            Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath),
            extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "audio/wav" : "audio/mpeg",
            "fr-FR", "take-01", node.Text.Key, node.Text.Fallback, "neutral", "neutral", 1000,
            "CC0-1.0", true, "studio:dialogue-voice-import");
    }

    internal StudioDialogueVoicePreview PreviewDialogueVoiceImport(StudioDialogueVoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (_appliedDialogueVoiceImport is { Persisted: false })
            return VoicePreviewFailure(_appliedDialogueVoiceImport.Undone
                ? "Enregistrez l'annulation ou rétablissez l'import vocal avant de préparer une autre prise."
                : "Enregistrez ou annulez l'import vocal courant avant de préparer une autre prise.");
        _approvedDialogueVoiceImport = null;
        if (History is null || Session is null) return VoicePreviewFailure("Aucun module n'est ouvert.");
        try
        {
            var sourcePath = Path.Combine(draft.SourceRoot, draft.SourceRelativePath);
            var hash = HashFileSha256(sourcePath);
            var authoring = new DialogueVoiceCueAuthoringRequest(DialogueId.From(draft.DialogueId),
                DialogueNodeId.From(draft.NodeId), BlueprintId.From(draft.ParticipantBlueprintId), string.Empty,
                draft.Locale, draft.Take, draft.SubtitleKey, draft.Subtitle, draft.LipSyncProfileId,
                draft.Emotion, draft.DurationMilliseconds, draft.LicenseSpdx, hash,
                draft.AllowSubtitleFallback, draft.Provenance);
            var destination = DialogueVoiceCueAuthoringService.DestinationRelativePath(
                authoring, Path.GetExtension(draft.SourceRelativePath));
            authoring = authoring with { Resource = destination };
            var file = GovernedDialogueVoiceImportService.Plan(Session.RootDirectory,
                new GovernedDialogueVoiceImportRequest(1, draft.SourceRoot, draft.SourceRelativePath,
                    destination, hash, draft.MimeType, draft.LicenseSpdx, draft.Provenance));
            if (!file.Succeeded) return VoicePreviewFailure(file.Diagnostics.Select(item => $"{item.Code}: {item.Message}").ToArray());
            var authored = DialogueVoiceCueAuthoringService.Build(History.State.Project, authoring);
            if (!authored.Succeeded) return VoicePreviewFailure(authored.Diagnostics.Select(item => $"{item.Code}: {item.Message}").ToArray());
            var command = new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()),
                new ProjectRevision(draft.BaseRevision), DialogueId.From(draft.DialogueId),
                authored.Dialogue!.Presentation, true);
            var preview = _dialogueVoicePresentationHandler.Handle(History.State, command);
            if (!preview.Succeeded) return VoicePreviewFailure(preview.Diagnostics.Select(item => $"{item.Code}: {item.Message}").ToArray());
            _approvedDialogueVoiceImport = new(command, file.Plan!, preview.Patch!, preview.Inverse!);
            return new(true, destination, hash, file.Plan!.SourceBytes, file.Plan.Reused,
                preview.Patch!.Summary, preview.Inverse!.Summary,
                draft.AllowSubtitleFallback
                    ? "La voix sera lue; le sous-titre reste visible et sert de secours si la ressource devient invalide."
                    : "La voix est requise; une ressource invalide produira un diagnostic sans masquer l'erreur.", []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return VoicePreviewFailure($"dialogue.voice.studio.preview_failed: {exception.Message}");
        }
    }

    internal StudioActionResult ApplyApprovedDialogueVoiceImport()
    {
        if (History is null || Session is null || _approvedDialogueVoiceImport is null)
            return new(false, "Prévisualisez une prise valide avant de l'importer.");
        var approved = _approvedDialogueVoiceImport;
        var command = approved.Command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        var result = _dialogueVoicePresentationHandler.Handle(History.State, command);
        if (!result.Succeeded)
            return new(false, string.Join(" · ", result.Diagnostics.Select(item => item.Message)));
        var file = GovernedDialogueVoiceImportService.Apply(Session.RootDirectory, approved.FilePlan);
        if (!file.Succeeded)
            return new(false, string.Join(" · ", file.Diagnostics.Select(item => item.Message)));
        History.Record(result);
        _appliedDialogueVoiceImport = new(approved.FilePlan, file, result.Audit.CommandId.Value);
        _approvedDialogueVoiceImport = null;
        InvalidateContentIndexes();
        return new(true, "Prise vocale importée et liée · enregistrer pour persister le dialogue");
    }

    internal StudioActionResult UndoDialogueVoiceImport()
    {
        if (History is null || Session is null || _appliedDialogueVoiceImport is null)
            return new(false, "Aucun import vocal à annuler.");
        if (History.Journal.LastOrDefault(item => item.IsApplied)?.Id != _appliedDialogueVoiceImport.CommandId)
            return new(false, "Annulez d'abord les modifications plus récentes.");
        var undo = History.Undo();
        if (!undo.Succeeded) return new(false, "L'inverse du dialogue n'est plus disponible.");
        var reverted = GovernedDialogueVoiceImportService.Revert(Session.RootDirectory,
            _appliedDialogueVoiceImport.FilePlan, _appliedDialogueVoiceImport.FileApply);
        if (!reverted.Succeeded)
        {
            var compensation = History.Redo();
            var message = string.Join(" · ", reverted.Diagnostics.Select(item => item.Message));
            return compensation.Succeeded
                ? new(false, message)
                : new(false, $"{message} · La compensation du dialogue a échoué; enregistrez sous un autre nom et rouvrez le module.");
        }
        _appliedDialogueVoiceImport.Undone = true;
        InvalidateContentIndexes();
        return new(true, reverted.DeletionSkipped
            ? "Import vocal annulé; le cue est retiré et le blob immuable est conservé sans référence"
            : "Import vocal annulé; le cue est retiré");
    }

    internal StudioActionResult RedoDialogueVoiceImport()
    {
        if (History is null || Session is null || _appliedDialogueVoiceImport is not { Undone: true } applied)
            return new(false, "Aucun import vocal à rétablir.");
        var file = GovernedDialogueVoiceImportService.Apply(Session.RootDirectory, applied.FilePlan);
        if (!file.Succeeded) return new(false, string.Join(" · ", file.Diagnostics.Select(item => item.Message)));
        var redo = History.Redo();
        if (!redo.Succeeded)
        {
            var compensation = GovernedDialogueVoiceImportService.Revert(Session.RootDirectory,
                applied.FilePlan, file);
            return compensation.Succeeded
                ? new(false, "Le dialogue ne peut plus être rétabli; le blob immuable reste sans référence.")
                : new(false, "Le dialogue et la compensation du blob ont échoué; fermez sans enregistrer puis rouvrez le module.");
        }
        applied.FileApply = file;
        applied.Undone = false;
        InvalidateContentIndexes();
        return new(true, "Import vocal rétabli");
    }

    private static StudioDialogueVoicePreview VoicePreviewFailure(params string[] diagnostics) =>
        new(false, string.Empty, string.Empty, 0, false, string.Empty, string.Empty,
            "Le runtime conservera le sous-titre; aucune voix invalide ne sera lue.", diagnostics);

    private static string HashFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    private void DiscardUnsavedDialogueVoiceImport()
    {
        DialogueVoiceLifecycleWarning = null;
        if (Session is not null && _appliedDialogueVoiceImport is { } applied)
        {
            if (!applied.Persisted && !applied.Undone)
            {
                var reverted = GovernedDialogueVoiceImportService.Revert(Session.RootDirectory,
                    applied.FilePlan, applied.FileApply);
                if (!reverted.Succeeded)
                    DialogueVoiceLifecycleWarning = string.Join(" · ",
                        reverted.Diagnostics.Select(item => item.Message));
            }
            else if (applied.Persisted && applied.Undone)
            {
                var restored = GovernedDialogueVoiceImportService.Apply(Session.RootDirectory, applied.FilePlan);
                if (!restored.Succeeded)
                    DialogueVoiceLifecycleWarning = string.Join(" · ",
                        restored.Diagnostics.Select(item => item.Message));
            }
        }
        _approvedDialogueVoiceImport = null;
        _appliedDialogueVoiceImport = null;
    }

    private sealed record ApprovedDialogueVoiceImport(
        SetDialoguePresentationCommand Command,
        GovernedDialogueVoiceImportPlan FilePlan,
        ProjectPatch Patch,
        ProjectPatch Inverse);

    private sealed class AppliedDialogueVoiceImport(
        GovernedDialogueVoiceImportPlan filePlan,
        GovernedDialogueVoiceImportApplyResult fileApply,
        Guid commandId)
    {
        internal GovernedDialogueVoiceImportPlan FilePlan { get; } = filePlan;
        internal GovernedDialogueVoiceImportApplyResult FileApply { get; set; } = fileApply;
        internal Guid CommandId { get; } = commandId;
        internal bool Undone { get; set; }
        internal bool Persisted { get; set; }
    }
}
