namespace Forge.ModuleFormat.Tests.RuntimePresentation;

public sealed class DialogueVoiceSurfaceBoundaryTests
{
    [Fact]
    public void StudioOffersHumanVoiceImportAndRuntimeUsesTheDedicatedPlayer()
    {
        var root = FindRepositoryRoot();
        var studio = File.ReadAllText(Path.Combine(root, "apps", "Forge.Studio", "Presentation",
            "StudioNarrativeWorkbench.DialogueVoice.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "apps", "Forge.Studio", "Session",
            "StudioSessionController.DialogueVoice.cs"));
        var runtime = File.ReadAllText(Path.Combine(root, "apps", "Forge.Runtime", "Runtime",
            "RuntimeDialogueVoicePlayer.cs"));
        var narrative = File.ReadAllText(Path.Combine(root, "apps", "Forge.Runtime", "Runtime",
            "GameRoot.NarrativePersistence.cs"));
        var gameRoot = File.ReadAllText(Path.Combine(root, "apps", "Forge.Runtime", "Runtime",
            "GameRoot.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "apps", "Forge.Runtime", "Runtime",
            "GameRoot.Smoke.DialogueVoice.cs"));

        Assert.Contains("FileDialog", studio, StringComparison.Ordinal);
        Assert.Contains("Prévisualiser la voix", studio, StringComparison.Ordinal);
        Assert.Contains("Importer et lier", studio, StringComparison.Ordinal);
        Assert.Contains("Sous-titre de secours", studio, StringComparison.Ordinal);
        Assert.Contains("\"voice.duration\", \"Durée (ms)\", \"1000\"", studio,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"voice.duration\", \"Durée (ms)\", \"0\"", studio,
            StringComparison.Ordinal);
        Assert.Contains("PreviewDialogueVoiceImport", controller, StringComparison.Ordinal);
        Assert.Contains("\"neutral\", \"neutral\", 1000,", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("\"neutral\", \"neutral\", 0,", controller, StringComparison.Ordinal);
        Assert.Contains("ApplyApprovedDialogueVoiceImport", controller, StringComparison.Ordinal);
        Assert.Contains("UndoDialogueVoiceImport", controller, StringComparison.Ordinal);
        Assert.Contains("RedoDialogueVoiceImport", controller, StringComparison.Ordinal);
        Assert.Contains("AudioStreamPlayer", runtime, StringComparison.Ordinal);
        Assert.Contains("var verifiedBytes = verified.VerifiedBytes!;", runtime, StringComparison.Ordinal);
        Assert.Contains("AudioStreamMP3.LoadFromBuffer(verifiedBytes", runtime, StringComparison.Ordinal);
        Assert.Contains("AudioStreamWav.LoadFromBuffer(verifiedBytes", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadFromFile", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Func<string, AudioStream?> loadAudioStream", runtime, StringComparison.Ordinal);
        Assert.Contains("event=started", runtime, StringComparison.Ordinal);
        Assert.Contains("event=completed", runtime, StringComparison.Ordinal);
        Assert.Contains("event=interrupted", runtime, StringComparison.Ordinal);
        Assert.Contains("event=fallback", runtime, StringComparison.Ordinal);
        Assert.Contains("_dialogueVoicePlayer?.Play", narrative, StringComparison.Ordinal);
        Assert.Contains("_dialogueVoicePlayer?.Stop", narrative, StringComparison.Ordinal);
        Assert.Contains("--dialogue-voice-completion-smoke-test", gameRoot, StringComparison.Ordinal);
        Assert.Contains("RunDialogueVoiceCompletionSmokeTestAsync", gameRoot, StringComparison.Ordinal);
        Assert.Contains("OPENRPG_FORGE_RUNTIME_DIALOGUE_VOICE_COMPLETION_PASS", smoke,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StudioPreventsASecondUnresolvedVoiceImportAndExercisesTheLifecycle()
    {
        var root = FindRepositoryRoot();
        var studio = File.ReadAllText(Path.Combine(root, "apps", "Forge.Studio", "Presentation",
            "StudioNarrativeWorkbench.DialogueVoice.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "apps", "Forge.Studio", "Session",
            "StudioSessionController.DialogueVoice.cs"));

        Assert.Contains("_appliedDialogueVoiceImport is { Persisted: false }", controller,
            StringComparison.Ordinal);
        Assert.Contains("secondPreviewBlocked", studio, StringComparison.Ordinal);
        Assert.Contains("reverted.DeletionSkipped", controller, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "PROJECT_CONTEXT.md"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException();
    }
}
