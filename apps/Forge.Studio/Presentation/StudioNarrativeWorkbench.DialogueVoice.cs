using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNarrativeWorkbenchUx5B
{
    private OptionButton _voiceNode = null!;
    private LineEdit _voiceSourcePath = null!;
    private Label _voiceHash = null!;
    private Label _voiceDestination = null!;
    private Label _voiceRuntime = null!;


    private void AddDialogueVoiceImportSurface()
    {
        var scroll = (ScrollContainer)_tabs.GetChild(0);
        var column = (VBoxContainer)scroll.GetChild(0);
        column.AddChild(StudioStyle.Label("Prise vocale gouvernée", 16, StudioStyle.Text));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 7);
        column.AddChild(grid);

        grid.AddChild(FieldLabel("Nœud parlant"));
        _voiceNode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_voiceNode);
        grid.AddChild(_voiceNode);

        grid.AddChild(FieldLabel("Fichier vocal local"));
        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _voiceSourcePath = new LineEdit { Editable = false, PlaceholderText = "MP3 ou WAV local" };
        _voiceSourcePath.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        StudioStyle.ApplyLineEdit(_voiceSourcePath);
        var choose = StudioStyle.Button("Choisir…");
        choose.Pressed += OpenDialogueVoiceFileDialog;
        sourceRow.AddChild(_voiceSourcePath);
        sourceRow.AddChild(choose);
        grid.AddChild(sourceRow);

        AddLine(grid, "voice.locale", "Locale", "fr-FR");
        AddLine(grid, "voice.take", "Prise", "take-01");
        AddLine(grid, "voice.subtitle", "Sous-titre", "");
        AddLine(grid, "voice.emotion", "Émotion", "neutral");
        AddLine(grid, "voice.lipsync", "Profil lipsync", "neutral");
        AddLine(grid, "voice.duration", "Durée (ms)", "1000");
        AddLine(grid, "voice.license", "Licence SPDX", "CC0-1.0");
        AddLine(grid, "voice.provenance", "Provenance", "studio:dialogue-voice-import");
        AddCheck(grid, "voice.fallback", "Sous-titre de secours", true);

        grid.AddChild(FieldLabel("SHA-256"));
        _voiceHash = StudioStyle.Label("Calculé pendant la prévisualisation", 11, StudioStyle.Muted, wrap: true);
        grid.AddChild(_voiceHash);
        grid.AddChild(FieldLabel("Destination relative"));
        _voiceDestination = StudioStyle.Label("Aucune écriture avant application", 11, StudioStyle.Muted, wrap: true);
        grid.AddChild(_voiceDestination);
        grid.AddChild(FieldLabel("Comportement runtime"));
        _voiceRuntime = StudioStyle.Label("Sous-titres conservés; voix non configurée.", 11, StudioStyle.Warning, wrap: true);
        grid.AddChild(_voiceRuntime);

        grid.AddChild(new Control());
        var actions = new HBoxContainer();
        var preview = StudioStyle.Button("Prévisualiser la voix", primary: true);
        var apply = StudioStyle.Button("Importer et lier");
        var undo = StudioStyle.Button("Annuler import vocal");
        var redo = StudioStyle.Button("Rétablir import vocal");
        preview.Pressed += PreviewDialogueVoiceImportFromSurface;
        apply.Pressed += ApplyDialogueVoiceImportFromSurface;
        undo.Pressed += () => ShowVoiceAction(_session.UndoDialogueVoiceImport());
        redo.Pressed += () => ShowVoiceAction(_session.RedoDialogueVoiceImport());
        actions.AddChild(preview);
        actions.AddChild(apply);
        actions.AddChild(undo);
        actions.AddChild(redo);
        grid.AddChild(actions);

    }

    private void OpenDialogueVoiceFileDialog()
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Title = "Choisir une prise vocale locale",
            Filters = ["*.mp3 ; Audio MP3", "*.wav ; Audio WAV"],
            UseNativeDialog = true,
        };
        dialog.FileSelected += path =>
        {
            _voiceSourcePath.Text = path;
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCenteredRatio(.72f);
    }

    private void RefreshDialogueVoiceNodes()
    {
        _voiceNode.Clear();
        _voiceNode.AddItem("Sélectionner un nœud parlant…");
        _voiceNode.SetItemMetadata(0, string.Empty);
        var selector = _selectors[StudioNarrativeSectionUx5B.Dialogue];
        if (selector.Selected <= 0) return;
        var dialogueId = Guid.Parse(selector.GetItemMetadata(selector.Selected).AsString());
        foreach (var choice in _session.DialogueVoiceNodeChoices(dialogueId))
        {
            _voiceNode.AddItem(choice.Label);
            _voiceNode.SetItemMetadata(_voiceNode.ItemCount - 1, choice.Id.ToString("D"));
        }
        _voiceNode.Select(0);
    }

    private void FocusDialogueVoiceEvidence()
    {
        var scroll = (ScrollContainer)_tabs.GetChild((int)StudioNarrativeSectionUx5B.Dialogue);
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, int.MaxValue);
    }

    private void PreviewDialogueVoiceImportFromSurface()
    {
        try
        {
            var selector = _selectors[StudioNarrativeSectionUx5B.Dialogue];
            if (selector.Selected <= 0 || _voiceNode.Selected <= 0 || string.IsNullOrWhiteSpace(_voiceSourcePath.Text))
            {
                _voiceRuntime.Text = "Choisissez explicitement un dialogue, un nœud et un fichier local.";
                return;
            }
            var dialogueId = Guid.Parse(selector.GetItemMetadata(selector.Selected).AsString());
            var nodeId = Guid.Parse(_voiceNode.GetItemMetadata(_voiceNode.Selected).AsString());
            var draft = _session.CreateDialogueVoiceDraft(dialogueId, nodeId, _voiceSourcePath.Text) with
            {
                Locale = _lines["voice.locale"].Text,
                Take = _lines["voice.take"].Text,
                Subtitle = _lines["voice.subtitle"].Text,
                Emotion = _lines["voice.emotion"].Text,
                LipSyncProfileId = _lines["voice.lipsync"].Text,
                DurationMilliseconds = int.TryParse(_lines["voice.duration"].Text, out var duration) ? duration : -1,
                LicenseSpdx = _lines["voice.license"].Text,
                Provenance = _lines["voice.provenance"].Text,
                AllowSubtitleFallback = _checks["voice.fallback"].ButtonPressed,
            };
            if (string.IsNullOrWhiteSpace(draft.Subtitle))
            {
                draft = draft with
                {
                    Subtitle = _session.Project!.Dialogues.Single(item => item.Id.Value == dialogueId)
                        .Nodes.Single(item => item.Id.Value == nodeId).Text.Fallback,
                };
            }
            var result = _session.PreviewDialogueVoiceImport(draft);
            _voiceHash.Text = result.Succeeded ? result.ContentSha256 : "Non validé";
            _voiceDestination.Text = result.Succeeded ? result.DestinationRelativePath : "Aucune écriture";
            _voiceRuntime.Text = result.Succeeded
                ? $"{result.RuntimeBehavior}\nDiff : {result.Diff}\nInverse : {result.Inverse}"
                : string.Join("\n", result.Diagnostics);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _voiceRuntime.Text = exception.Message;
        }
    }

    private void ApplyDialogueVoiceImportFromSurface() =>
        ShowVoiceAction(_session.ApplyApprovedDialogueVoiceImport());

    private void ShowVoiceAction(StudioActionResult result)
    {
        _voiceRuntime.Text = result.Message;
        StatusChanged?.Invoke(result.Message, result.Succeeded);
        if (result.Succeeded) NarrativeChanged?.Invoke();
    }

    private static (bool Succeeded, Guid DialogueId, Guid NodeId, string Hash, string Destination)
        RunDialogueVoiceTransactionEvidence(StudioSessionController editing)
    {
        if (editing.Session is null || editing.Project is null)
            return (false, Guid.Empty, Guid.Empty, string.Empty, string.Empty);
        var dialogue = editing.Project.Dialogues.FirstOrDefault(item =>
            item.Nodes.Any(node => node.SpeakerBlueprintId is not null));
        var node = dialogue?.Nodes.FirstOrDefault(item => item.SpeakerBlueprintId is not null);
        if (dialogue is null || node is null)
            return (false, Guid.Empty, Guid.Empty, string.Empty, string.Empty);
        var sourceRoot = Path.Combine(editing.Session.RootDirectory, ".studio-voice-evidence");
        Directory.CreateDirectory(sourceRoot);
        var source = Path.Combine(sourceRoot, "voice.mp3");
        File.WriteAllBytes(source, BuildDialogueVoiceEvidenceMp3());
        GD.Print("OPENRPG_FORGE_STUDIO_UX5B_TRANSACTION_STEP phase=voice_source_ready");
        var draft = editing.CreateDialogueVoiceDraft(dialogue.Id.Value, node.Id.Value, source);
        var preview = editing.PreviewDialogueVoiceImport(draft);
        GD.Print($"OPENRPG_FORGE_STUDIO_UX5B_TRANSACTION_STEP phase=voice_preview succeeded={preview.Succeeded.ToString().ToLowerInvariant()}");
        if (!preview.Succeeded || File.Exists(Path.Combine(editing.Session.RootDirectory,
                preview.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar))))
            return (false, Guid.Empty, Guid.Empty, string.Empty, string.Empty);
        var applied = editing.ApplyApprovedDialogueVoiceImport();
        GD.Print($"OPENRPG_FORGE_STUDIO_UX5B_TRANSACTION_STEP phase=voice_apply succeeded={applied.Succeeded.ToString().ToLowerInvariant()}");
        var secondPreview = editing.PreviewDialogueVoiceImport(draft with { Take = "take-02" });
        var secondPreviewBlocked = !secondPreview.Succeeded && secondPreview.Diagnostics.Any(item =>
            item.Contains("Enregistrez ou annulez l'import vocal courant", StringComparison.Ordinal));
        var undo = editing.UndoDialogueVoiceImport();
        GD.Print($"OPENRPG_FORGE_STUDIO_UX5B_TRANSACTION_STEP phase=voice_undo succeeded={undo.Succeeded.ToString().ToLowerInvariant()}");
        var destination = Path.Combine(editing.Session.RootDirectory,
            preview.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var undone = undo.Succeeded && File.Exists(destination) &&
                     editing.Project.Dialogues.Single(item => item.Id == dialogue.Id).Presentation?.VoiceCues
                         .All(item => item.ContentSha256 != preview.ContentSha256) != false;
        var redo = editing.RedoDialogueVoiceImport();
        GD.Print($"OPENRPG_FORGE_STUDIO_UX5B_TRANSACTION_STEP phase=voice_redo succeeded={redo.Succeeded.ToString().ToLowerInvariant()}");
        var redone = redo.Succeeded && File.Exists(destination) &&
                     editing.Project.Dialogues.Single(item => item.Id == dialogue.Id).Presentation!.VoiceCues
                         .Any(item => item.ContentSha256 == preview.ContentSha256);
        return (applied.Succeeded && secondPreviewBlocked && undone && redone, dialogue.Id.Value, node.Id.Value,
            preview.ContentSha256, preview.DestinationRelativePath);
    }

    private static byte[] BuildDialogueVoiceEvidenceMp3()
    {
        var frame = new byte[417];
        frame[0] = 0xff;
        frame[1] = 0xfb;
        frame[2] = 0x90;
        frame[3] = 0x00;
        return frame;
    }
}
