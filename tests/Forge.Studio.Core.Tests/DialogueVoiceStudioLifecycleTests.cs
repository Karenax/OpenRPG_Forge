using Forge.Studio.Session;

namespace Forge.Studio.Core.Tests;

public sealed class DialogueVoiceStudioLifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-studio-voice-lifecycle",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void HumanLockAllowsTwoSavedImportsAndCloseCannotLeaveADanglingCue()
    {
        CopyWorkspace(Path.Combine(FindRepositoryRoot(), "apps", "Forge.Runtime", "content", "crypt-of-ashes"),
            _root);
        var sources = Path.Combine(_root, ".voice-test-sources");
        Directory.CreateDirectory(sources);
        var firstSource = Path.Combine(sources, "voice-01.mp3");
        var secondSource = Path.Combine(sources, "voice-02.mp3");
        var unsavedSource = Path.Combine(sources, "voice-unsaved.mp3");
        File.WriteAllBytes(firstSource, FakeMp3(0x11));
        File.WriteAllBytes(secondSource, FakeMp3(0x22));
        File.WriteAllBytes(unsavedSource, FakeMp3(0x33));
        var editing = new StudioSessionController();
        Assert.True(editing.Open(_root).Succeeded);
        var dialogue = editing.Project!.Dialogues.First(item => item.Nodes.Any(node => node.SpeakerBlueprintId is not null));
        var node = dialogue.Nodes.First(item => item.SpeakerBlueprintId is not null);

        var first = editing.CreateDialogueVoiceDraft(dialogue.Id.Value, node.Id.Value, firstSource) with
        { Take = "take-01" };
        Assert.True(editing.PreviewDialogueVoiceImport(first).Succeeded);
        Assert.True(editing.ApplyApprovedDialogueVoiceImport().Succeeded);

        var locked = editing.CreateDialogueVoiceDraft(dialogue.Id.Value, node.Id.Value, secondSource) with
        { Take = "take-02" };
        var lockedPreview = editing.PreviewDialogueVoiceImport(locked);
        Assert.False(lockedPreview.Succeeded);
        Assert.Contains(lockedPreview.Diagnostics, message =>
            message.Contains("enregistr", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("annul", StringComparison.OrdinalIgnoreCase));

        Assert.True(editing.Save().Succeeded);
        var second = editing.CreateDialogueVoiceDraft(dialogue.Id.Value, node.Id.Value, secondSource) with
        { Take = "take-02" };
        var secondPreview = editing.PreviewDialogueVoiceImport(second);
        Assert.True(secondPreview.Succeeded, string.Join(" | ", secondPreview.Diagnostics));
        Assert.True(editing.ApplyApprovedDialogueVoiceImport().Succeeded);
        Assert.True(editing.UndoDialogueVoiceImport().Succeeded);
        Assert.True(editing.RedoDialogueVoiceImport().Succeeded);
        Assert.True(editing.Save().Succeeded);

        var unsaved = editing.CreateDialogueVoiceDraft(dialogue.Id.Value, node.Id.Value, unsavedSource) with
        { Take = "take-unsaved" };
        Assert.True(editing.PreviewDialogueVoiceImport(unsaved).Succeeded);
        Assert.True(editing.ApplyApprovedDialogueVoiceImport().Succeeded);
        editing.Close();

        var reopened = new StudioSessionController();
        Assert.True(reopened.Open(_root).Succeeded);
        var cues = reopened.Project!.Dialogues.Single(item => item.Id == dialogue.Id).Presentation!.VoiceCues;
        Assert.Contains(cues, cue => cue.Take == "take-01" && ExistingResource(cue.Resource));
        Assert.Contains(cues, cue => cue.Take == "take-02" && ExistingResource(cue.Resource));
        Assert.DoesNotContain(cues, cue => cue.Take == "take-unsaved");
        reopened.Close();

        bool ExistingResource(string? relative) => !string.IsNullOrWhiteSpace(relative) &&
            File.Exists(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static byte[] FakeMp3(byte fill)
    {
        var bytes = Enumerable.Repeat(fill, 417).ToArray();
        bytes[0] = 0xff;
        bytes[1] = 0xfb;
        bytes[2] = 0x90;
        bytes[3] = 0x64;
        return bytes;
    }

    private static void CopyWorkspace(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PROJECT_CONTEXT.md"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test cleanup is best effort; lifecycle assertions run before disposal.
        }
    }
}
