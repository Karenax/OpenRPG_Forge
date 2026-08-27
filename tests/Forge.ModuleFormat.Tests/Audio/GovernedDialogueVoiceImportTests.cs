using System.Security.Cryptography;
using Forge.ModuleFormat.Audio;

namespace Forge.ModuleFormat.Tests.Audio;

public sealed class GovernedDialogueVoiceImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-voice-import", Guid.NewGuid().ToString("N"));
    private string SourceRoot => Path.Combine(_root, "source");
    private string WorkspaceRoot => Path.Combine(_root, "workspace");

    [Fact]
    public void ValidMp3PlansAppliesIdempotentlyAndRevertsWithoutWritingDuringDryRun()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
        var bytes = FakeMp3("governed voice take");
        File.WriteAllBytes(Path.Combine(SourceRoot, "take.mp3"), bytes);
        var hash = Hash(bytes);
        const string destination = "audio/dialogues/dialogue/node/take-0123456789abcdef.mp3";

        var planned = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            new GovernedDialogueVoiceImportRequest(1, SourceRoot, "take.mp3", destination, hash,
                "audio/mpeg", "CC0-1.0", "author:test"));

        Assert.True(planned.Succeeded, Describe(planned.Diagnostics));
        Assert.Equal(destination, planned.Plan!.DestinationRelativePath);
        Assert.Equal(hash, planned.Plan.ContentSha256);
        Assert.False(File.Exists(Path.Combine(WorkspaceRoot, destination.Replace('/', Path.DirectorySeparatorChar))));

        var applied = GovernedDialogueVoiceImportService.Apply(WorkspaceRoot, planned.Plan);
        var repeated = GovernedDialogueVoiceImportService.Apply(WorkspaceRoot, planned.Plan);

        Assert.True(applied.Succeeded, Describe(applied.Diagnostics));
        Assert.True(applied.Created);
        Assert.True(repeated.Succeeded, Describe(repeated.Diagnostics));
        Assert.True(repeated.Reused);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(WorkspaceRoot,
            destination.Replace('/', Path.DirectorySeparatorChar))));

        var reverted = GovernedDialogueVoiceImportService.Revert(WorkspaceRoot, planned.Plan, applied);

        Assert.True(reverted.Succeeded, Describe(reverted.Diagnostics));
        Assert.True(reverted.DeletionSkipped);
        Assert.Contains(reverted.Diagnostics,
            item => item.Code == GovernedDialogueVoiceImportService.BlobRetainedCode);
        Assert.True(File.Exists(Path.Combine(WorkspaceRoot,
            destination.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void HashMimeExtensionCollisionAndEscapingLinkAreRejectedWithoutWrites()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
        var bytes = FakeMp3("voice");
        File.WriteAllBytes(Path.Combine(SourceRoot, "take.mp3"), bytes);
        var hash = Hash(bytes);
        const string destination = "audio/dialogues/d/n/take.mp3";

        var badHash = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request(destination, new string('0', 64), "audio/mpeg"));
        var badMime = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request(destination, hash, "audio/wav"));
        File.WriteAllBytes(Path.Combine(SourceRoot, "take.txt"), bytes);
        var badExtension = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request(destination, hash, "audio/mpeg") with { SourceRelativePath = "take.txt" });

        Assert.Contains(badHash.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.HashMismatchCode);
        Assert.Contains(badMime.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.MimeInvalidCode);
        Assert.Contains(badExtension.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.ExtensionUnsupportedCode);
        Assert.False(Directory.Exists(Path.Combine(WorkspaceRoot, "audio")));

        var destinationPath = Path.Combine(WorkspaceRoot, destination.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllBytes(destinationPath, FakeMp3("different"));
        var collision = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request(destination, hash, "audio/mpeg"));
        Assert.Contains(collision.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.CollisionCode);

        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "outside.mp3"), bytes);
        var link = Path.Combine(SourceRoot, "linked.mp3");
        try
        {
            File.CreateSymbolicLink(link, Path.Combine(outside, "outside.mp3"));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        var escaped = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request("audio/dialogues/d/n/linked.mp3", hash, "audio/mpeg") with
            { SourceRelativePath = "linked.mp3" });
        Assert.Contains(escaped.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.SourcePathUnsafeCode);
    }

    [Fact]
    public void TruncatedMp3AndWavContainersAreRejected()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
        var truncatedMp3 = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00 };
        File.WriteAllBytes(Path.Combine(SourceRoot, "truncated.mp3"), truncatedMp3);
        var truncatedWav = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x04, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
        };
        File.WriteAllBytes(Path.Combine(SourceRoot, "truncated.wav"), truncatedWav);

        var mp3 = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request("audio/dialogues/blobs/sha256/aa/" + Hash(truncatedMp3) + ".mp3",
                Hash(truncatedMp3), "audio/mpeg") with
            {
                SourceRelativePath = "truncated.mp3",
            });
        var wav = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request("audio/dialogues/blobs/sha256/bb/" + Hash(truncatedWav) + ".wav",
                Hash(truncatedWav), "audio/wav") with
            {
                SourceRelativePath = "truncated.wav",
            });

        Assert.Contains(mp3.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.MimeInvalidCode);
        Assert.Contains(wav.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.MimeInvalidCode);
    }

    [Fact]
    public void UnknownBananaLicenseIsRejected()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
        var bytes = FakeMp3("licensed voice");
        File.WriteAllBytes(Path.Combine(SourceRoot, "take.mp3"), bytes);

        var result = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request("audio/dialogues/blobs/sha256/cc/" + Hash(bytes) + ".mp3",
                Hash(bytes), "audio/mpeg") with
            {
                LicenseSpdx = "banana",
            });

        Assert.Contains(result.Diagnostics, item => item.Code == GovernedDialogueVoiceImportService.LicenseInvalidCode);
    }

    [Fact]
    public void ForgedPlanDestinationIsRejectedWithoutWritingOutsidePlannedBlob()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(WorkspaceRoot);
        var bytes = FakeMp3("immutable plan");
        File.WriteAllBytes(Path.Combine(SourceRoot, "take.mp3"), bytes);
        var hash = Hash(bytes);
        var plannedDestination = $"audio/dialogues/blobs/sha256/{hash[..2]}/{hash}.mp3";
        var planned = GovernedDialogueVoiceImportService.Plan(WorkspaceRoot,
            Request(plannedDestination, hash, "audio/mpeg"));
        Assert.True(planned.Succeeded, Describe(planned.Diagnostics));
        var forged = planned.Plan! with { DestinationRelativePath = "audio/dialogues/forged.mp3" };

        var result = GovernedDialogueVoiceImportService.Apply(WorkspaceRoot, forged);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics,
            item => item.Code == GovernedDialogueVoiceImportService.PlanMismatchCode);
        Assert.False(File.Exists(Path.Combine(WorkspaceRoot, "audio", "dialogues", "forged.mp3")));
        Assert.False(File.Exists(Path.Combine(WorkspaceRoot,
            plannedDestination.Replace('/', Path.DirectorySeparatorChar))));
    }

    private GovernedDialogueVoiceImportRequest Request(string destination, string hash, string mime) =>
        new(1, SourceRoot, "take.mp3", destination, hash, mime, "CC0-1.0", "author:test");

    private static byte[] FakeMp3(string payload)
    {
        var frame = new byte[417];
        frame[0] = 0xff;
        frame[1] = 0xfb;
        frame[2] = 0x90;
        frame[3] = 0x00;
        var text = System.Text.Encoding.UTF8.GetBytes(payload);
        text.AsSpan(0, Math.Min(text.Length, frame.Length - 4)).CopyTo(frame.AsSpan(4));
        return frame;
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Describe(IEnumerable<Forge.Domain.Validation.DomainDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Cleanup is best effort and does not weaken assertions.
        }
    }
}
