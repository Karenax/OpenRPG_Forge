using System.Security.Cryptography;
using Forge.Application.Areas;
using Forge.ModuleFormat.Audio;
using Forge.ModuleFormat.Tests.Serialization;

namespace Forge.ModuleFormat.Tests.Audio;

public sealed class WorkspaceAudioValidatorTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "openrpg-forge-audio-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Distribution_validation_checks_file_hash_and_declared_memory()
    {
        Directory.CreateDirectory(Path.Combine(_workspace, "audio"));
        var bytes = new byte[] { 1, 3, 5, 7, 9 };
        File.WriteAllBytes(Path.Combine(_workspace, "audio", "bell.ogg"), bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var project = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog("audio/bell.ogg", bytes.Length, hash));

        Assert.Empty(WorkspaceAudioValidator.Validate(_workspace, project));

        var invalid = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog("audio/bell.ogg", bytes.Length + 1, new string('a', 64)));
        var diagnostics = WorkspaceAudioValidator.Validate(_workspace, invalid);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceAudioValidator.ResourceSizeCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceAudioValidator.ResourceHashCode);
    }

    [Fact]
    public void Distribution_validation_reports_missing_resource_with_canonical_path()
    {
        Directory.CreateDirectory(_workspace);
        var project = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog("audio/missing.ogg", 1, new string('a', 64)));

        var diagnostic = Assert.Single(WorkspaceAudioValidator.Validate(_workspace, project),
            item => item.Code == WorkspaceAudioValidator.ResourceMissingCode);
        Assert.EndsWith("/resourcePath", diagnostic.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Versioned_builtin_is_verified_and_unknown_or_forged_builtin_is_rejected()
    {
        Directory.CreateDirectory(_workspace);
        var generated = ProceduralAmbientAudioService.Create(ProceduralAmbientAudioService.StreamResource);
        var project = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog(ProceduralAmbientAudioService.StreamResource,
                generated.Pcm16Mono.LongLength, generated.ContentSha256));
        Assert.Empty(WorkspaceAudioValidator.Validate(_workspace, project));

        var forged = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog(ProceduralAmbientAudioService.StreamResource,
                generated.Pcm16Mono.LongLength, new string('0', 64)));
        Assert.Contains(WorkspaceAudioValidator.Validate(_workspace, forged),
            item => item.Code == WorkspaceAudioValidator.ResourceHashCode);

        var unknown = AudioEmitterPersistenceTests.WithAudio(TestProjectFactory.CreateValid(),
            AudioEmitterPersistenceTests.CreateCatalog("builtin://openrpg/audio/v1/unknown", 1,
                new string('a', 64)));
        Assert.Contains(WorkspaceAudioValidator.Validate(_workspace, unknown),
            item => item.Code == WorkspaceAudioValidator.BuiltinUnsupportedCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
    }
}
