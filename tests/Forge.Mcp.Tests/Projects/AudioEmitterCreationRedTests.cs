using System.Security.Cryptography;
using Forge.Application.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class AudioEmitterCreationRedTests
{
    [Fact]
    public void VersionedBuiltinCreatesFromEmptyCatalogWithFullControlsAndRevert()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetAudioEmitters();
        Assert.Empty(current.Catalog.Definitions);
        var source = workspace.Project.GetBuiltinAudioResources().Resources.Single(item =>
            item.ResourcePath == ProceduralAmbientAudioService.StreamResource);
        var transform = new TransformValue(
            new Vector3Value(14, 2.5f, -7),
            new QuaternionValue(0, .38268343f, 0, .9238795f),
            new Vector3Value(1.25f, 1.25f, 1.25f));
        var request = new AudioEmitterCreatePlanRequest(
            AudioEmitterCreatePlanRequest.CurrentSchemaVersion,
            current.Revision,
            current.AreaId,
            "Ruisseau nord",
            source.ResourcePath,
            transform,
            2,
            38,
            .42f,
            AudioLoopMode.Loop,
            ["weather==rain", "chapter==2"],
            false,
            source.LicenseSpdx,
            source.ContentSha256,
            source.MemoryBytes,
            source.MimeType,
            source.ResourcePath,
            "mcp:audio-authoring",
            source.PackageManifest);
        var stale = workspace.Project.PlanAudioEmitter(request);
        var planned = workspace.Project.PlanAudioEmitter(request);

        Assert.True(planned.Succeeded);
        Assert.NotNull(planned.Diff);
        Assert.Equal(current.Revision, workspace.Project.GetCurrentRevision());
        var applied = workspace.Project.ApplyAudioEmitterPlan(planned.PlanId!);
        Assert.True(applied.Applied);
        Assert.False(workspace.Project.ApplyAudioEmitterPlan(stale.PlanId!).Succeeded);

        var reopened = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.Single()
            .Environment.Audio.Emitters;
        var definition = Assert.Single(reopened.Definitions);
        var instance = Assert.Single(reopened.Instances);
        Assert.Equal(source.ResourcePath, definition.ClipSet.Clips.Single().ResourcePath);
        Assert.Equal(source.ContentSha256, definition.ClipSet.Clips.Single().ContentSha256);
        Assert.Equal(AudioLoopMode.Loop, definition.Loop.Mode);
        Assert.Equal(38, definition.Spatial.OuterRadius);
        Assert.Equal(transform, instance.Transform);
        Assert.Equal(.42f, instance.Control!.Volume);
        Assert.Equal(["weather==rain", "chapter==2"], instance.Control.Conditions);
        Assert.False(instance.Control.Muted);

        var reverted = workspace.Project.RevertAudioEmitterPlan(planned.PlanId!);
        Assert.True(reverted.Reverted);
        Assert.Empty(ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas.Single()
            .Environment.Audio.Emitters.Definitions);
    }

    [Fact]
    public void GovernedWorkspaceResourceRequiresSafePathLicenseSizeAndHash()
    {
        using var workspace = new TestWorkspace();
        var audioDirectory = Path.Combine(workspace.RootDirectory, "audio");
        Directory.CreateDirectory(audioDirectory);
        var bytes = new byte[] { 1, 4, 9, 16, 25, 36 };
        File.WriteAllBytes(Path.Combine(audioDirectory, "bell.ogg"), bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var current = workspace.Project.GetAudioEmitters();
        var request = new AudioEmitterCreatePlanRequest(
            1, current.Revision, current.AreaId, "Cloche", "audio/bell.ogg",
            TransformValue.Identity, 1, 20, 1, AudioLoopMode.OneShot, [], false,
            "CC0-1.0", hash, bytes.Length, "audio/ogg", string.Empty,
            "asset-vault:audio", "assets/audio-manifest.json");

        Assert.True(workspace.Project.PlanAudioEmitter(request).Succeeded);
        var traversal = workspace.Project.PlanAudioEmitter(request with { ResourcePath = "../bell.ogg" });
        var badHash = workspace.Project.PlanAudioEmitter(request with { ContentSha256 = new string('0', 64) });
        var badLicense = workspace.Project.PlanAudioEmitter(request with { LicenseSpdx = "UNLICENSED" });

        Assert.Contains(traversal.Diagnostics, item => item.Code == "audio.source.path_unsafe");
        Assert.Contains(badHash.Diagnostics, item => item.Code == "audio.source.hash_mismatch");
        Assert.Contains(badLicense.Diagnostics, item => item.Code == "audio.source.license_invalid");
    }
}
