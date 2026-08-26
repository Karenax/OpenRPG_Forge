using System.Security.Cryptography;
using Forge.Mcp.Dialogues;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class DialogueVoiceImportAgentServiceTests : IDisposable
{
    private readonly TestWorkspace _workspace = new();
    private readonly string _sourceRoot = Path.Combine(Path.GetTempPath(), "openrpg-mcp-voice", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PlanApplyReapplyReopenAndRevertUseTheCanonicalDialogueCommand()
    {
        Directory.CreateDirectory(_sourceRoot);
        var bytes = ValidMp3();
        File.WriteAllBytes(Path.Combine(_sourceRoot, "voice.mp3"), bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var session = ProjectSession.Open(_workspace.RootDirectory).Session!;
        var dialogue = session.Project.Dialogues.First();
        var node = dialogue.Nodes.First(item => item.SpeakerBlueprintId is not null);
        var request = new DialogueVoiceImportPlanRequest(
            1, 0, dialogue.Id.ToString(), node.Id.ToString(), node.SpeakerBlueprintId!.Value.ToString(),
            _sourceRoot, "voice.mp3", hash, "audio/mpeg", "fr-FR", "take-01",
            node.Text.Key, node.Text.Fallback, "neutral", "warm", 1200,
            "CC0-1.0", true, "author:test");

        var planned = _workspace.Project.PlanDialogueVoiceImport(request);

        Assert.True(planned.Succeeded, Describe(planned.Diagnostics));
        Assert.NotNull(planned.PlanId);
        Assert.NotNull(planned.SemanticDiff);
        Assert.NotNull(planned.Inverse);
        Assert.Equal($"audio/dialogues/blobs/sha256/{hash[..2]}/{hash}.mp3",
            planned.DestinationRelativePath);
        Assert.False(File.Exists(Path.Combine(_workspace.RootDirectory,
            planned.DestinationRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        var applied = _workspace.Project.ApplyDialogueVoiceImportPlan(planned.PlanId!);
        var repeated = _workspace.Project.ApplyDialogueVoiceImportPlan(planned.PlanId!);
        var reopened = ProjectSession.Open(_workspace.RootDirectory).Session!;
        var cue = reopened.Project.Dialogues.Single(item => item.Id == dialogue.Id).Presentation!.VoiceCues
            .Single(item => item.NodeId == node.Id && item.Take == "take-01");

        Assert.True(applied.Succeeded, Describe(applied.Diagnostics));
        Assert.True(repeated.Succeeded, Describe(repeated.Diagnostics));
        Assert.True(repeated.Idempotent);
        Assert.Equal(planned.DestinationRelativePath, cue.Resource);
        Assert.Equal(hash, cue.ContentSha256);
        Assert.Equal("CC0-1.0", cue.LicenseSpdx);
        Assert.True(File.Exists(Path.Combine(_workspace.RootDirectory,
            cue.Resource!.Replace('/', Path.DirectorySeparatorChar))));

        var reverted = _workspace.Project.RevertDialogueVoiceImportPlan(planned.PlanId!);
        var afterRevert = ProjectSession.Open(_workspace.RootDirectory).Session!;

        Assert.True(reverted.Succeeded, Describe(reverted.Diagnostics));
        Assert.DoesNotContain(afterRevert.Project.Dialogues.Single(item => item.Id == dialogue.Id)
            .Presentation?.VoiceCues ?? [], item => item.Take == "take-01" && item.ContentSha256 == hash);
        Assert.True(File.Exists(Path.Combine(_workspace.RootDirectory,
            planned.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void ReapplyDetectsWhenCanonicalMaterializationWasReplacedByAnotherVoicePlan()
    {
        Directory.CreateDirectory(_sourceRoot);
        var firstBytes = ValidMp3(0x11);
        var secondBytes = ValidMp3(0x22);
        File.WriteAllBytes(Path.Combine(_sourceRoot, "voice-a.mp3"), firstBytes);
        File.WriteAllBytes(Path.Combine(_sourceRoot, "voice-b.mp3"), secondBytes);
        var session = ProjectSession.Open(_workspace.RootDirectory).Session!;
        var dialogue = session.Project.Dialogues.First();
        var node = dialogue.Nodes.First(item => item.SpeakerBlueprintId is not null);
        var request = new DialogueVoiceImportPlanRequest(
            1, 0, dialogue.Id.ToString(), node.Id.ToString(), node.SpeakerBlueprintId!.Value.ToString(),
            _sourceRoot, "voice-a.mp3", Hash(firstBytes), "audio/mpeg", "fr-FR", "take-01",
            node.Text.Key, node.Text.Fallback, "neutral", "warm", 1200,
            "CC0-1.0", true, "author:test");

        var first = _workspace.Project.PlanDialogueVoiceImport(request);
        Assert.True(first.Succeeded, Describe(first.Diagnostics));
        Assert.True(_workspace.Project.ApplyDialogueVoiceImportPlan(first.PlanId!).Succeeded);
        var second = _workspace.Project.PlanDialogueVoiceImport(request with
        {
            BaseRevision = _workspace.Project.GetCurrentRevision(),
            SourceRelativePath = "voice-b.mp3",
            ExpectedSha256 = Hash(secondBytes),
        });
        Assert.True(second.Succeeded, Describe(second.Diagnostics));
        Assert.True(_workspace.Project.ApplyDialogueVoiceImportPlan(second.PlanId!).Succeeded);

        var repeated = _workspace.Project.ApplyDialogueVoiceImportPlan(first.PlanId!);

        Assert.False(repeated.Succeeded);
        Assert.Contains(repeated.Diagnostics,
            item => item.Code == "dialogue.voice.plan.materialization_changed");
    }

    private static byte[] ValidMp3(byte marker = 0)
    {
        var frame = new byte[417];
        frame[0] = 0xff;
        frame[1] = 0xfb;
        frame[2] = 0x90;
        frame[3] = 0x00;
        frame[4] = marker;
        return frame;
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Describe(IEnumerable<Forge.Mcp.AgentDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    public void Dispose()
    {
        _workspace.Dispose();
        if (Directory.Exists(_sourceRoot)) Directory.Delete(_sourceRoot, recursive: true);
    }
}
