using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Modules;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Distribution;

public static class ModuleReleaseBundleBuilder
{
    public const string CurrentFormatVersion = "1.0.0";
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static ModuleReleaseBundleResult Build(
        string packagePath,
        string destinationPath,
        ModuleTargetKind target,
        ModuleDistributionPlatform platform,
        ModuleReleaseConfiguration configuration,
        string? signaturePath = null,
        string? deltaPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(configuration);
        var package = RequireFile(packagePath, "Module package");
        var destination = Path.GetFullPath(destinationPath);
        if (!string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Release bundle destination must use the .zip extension.", nameof(destinationPath));
        }

        if (!configuration.Platforms.Contains(platform))
        {
            throw new InvalidOperationException($"Platform '{platform}' is not enabled by the release configuration.");
        }

        var packageValidation = ModulePackageValidator.Validate(package);
        if (!packageValidation.IsValid)
        {
            throw new InvalidDataException(
                "Release package is invalid: " + string.Join("; ", packageValidation.Diagnostics.Select(item => item.Message)));
        }

        var signature = string.IsNullOrWhiteSpace(signaturePath) ? null : RequireFile(signaturePath, "Package signature");
        if (configuration.Channel == ModuleReleaseChannel.Stable &&
            configuration.Signing.Mode != ModuleSigningMode.Required)
        {
            throw new InvalidOperationException("Stable release bundles require the Required signing policy.");
        }

        if (configuration.Signing.Mode == ModuleSigningMode.Required && signature is null)
        {
            throw new InvalidOperationException("Release signing policy requires a package signature.");
        }

        if (signature is not null)
        {
            var verification = ModulePackageSignatureService.Verify(package, signature, configuration.Signing);
            if (!verification.IsValid)
            {
                throw new InvalidDataException(
                    $"Package signature is invalid: {verification.DiagnosticCode}: {verification.Message}");
            }
        }

        var delta = string.IsNullOrWhiteSpace(deltaPath) ? null : RequireFile(deltaPath, "Delta package");
        if (delta is not null && !configuration.Delta.Enabled)
        {
            throw new InvalidOperationException("A delta package was supplied while delta updates are disabled.");
        }

        var packageName = Path.GetFileName(package);
        var signatureName = signature is null ? null : Path.GetFileName(signature);
        var deltaName = delta is null ? null : Path.GetFileName(delta);
        var windows = platform == ModuleDistributionPlatform.WindowsX64;
        var manifest = new ModuleReleaseManifest(
            CurrentFormatVersion,
            configuration.Id,
            configuration.Channel,
            target,
            platform,
            packageName,
            Sha256(package),
            new FileInfo(package).Length,
            configuration.Signing.Mode,
            signatureName,
            signature is null ? null : Sha256(signature),
            deltaName,
            delta is null ? null : Sha256(delta),
            delta is not null && configuration.Delta.AllowResume,
            delta is not null && configuration.Delta.AllowRollback,
            windows ? configuration.WindowsPackaging.Mode : null,
            windows && configuration.WindowsPackaging.DetectPrerequisites,
            windows ? configuration.WindowsPackaging.Prerequisites : [],
            windows && configuration.WindowsPackaging.WriteInstallLog,
            windows && configuration.WindowsPackaging.IncludeUninstaller,
            configuration.Provenance);
        var entries = new List<(string Path, byte[] Content)>
        {
            ("release-manifest.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, Options) + Environment.NewLine)),
            ($"packages/{packageName}", File.ReadAllBytes(package)),
            ("README.txt", Encoding.UTF8.GetBytes(BuildReadme(manifest))),
        };
        if (signature is not null)
        {
            entries.Add(($"signatures/{signatureName}", File.ReadAllBytes(signature)));
        }

        if (delta is not null)
        {
            entries.Add(($"deltas/{deltaName}", File.ReadAllBytes(delta)));
        }

        if (windows && configuration.WindowsPackaging.Mode != WindowsPackageMode.PortableZip)
        {
            entries.Add(("install.ps1", Encoding.UTF8.GetBytes(BuildInstallScript(manifest))));
            if (configuration.WindowsPackaging.IncludeUninstaller)
            {
                entries.Add(("uninstall.ps1", Encoding.UTF8.GetBytes(BuildUninstallScript(manifest))));
            }
        }
        else if (!windows)
        {
            entries.Add(("install.sh", Encoding.UTF8.GetBytes(BuildUnixInstallScript(manifest))));
            entries.Add(("uninstall.sh", Encoding.UTF8.GetBytes(BuildUnixUninstallScript(manifest))));
        }

        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Release destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporary = Path.Combine(destinationDirectory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var entry in entries.OrderBy(item => item.Path, StringComparer.Ordinal))
                {
                    WriteEntry(archive, entry.Path, entry.Content);
                }
            }

            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return new ModuleReleaseBundleResult(destination, manifest, entries.Count, new FileInfo(destination).Length);
    }

    private static string BuildReadme(ModuleReleaseManifest manifest) =>
        $"OpenRPG Forge release bundle{Environment.NewLine}" +
        $"Channel: {manifest.Channel}{Environment.NewLine}" +
        $"Target: {manifest.Target}{Environment.NewLine}" +
        $"Platform: {manifest.Platform}{Environment.NewLine}" +
        $"Package SHA-256: {manifest.PackageSha256}{Environment.NewLine}" +
        "Verify release-manifest.json and the optional detached signature before installation." + Environment.NewLine;

    private static string BuildInstallScript(ModuleReleaseManifest manifest)
    {
        var prerequisites = string.Join(", ", manifest.Prerequisites);
        var prerequisiteArray = string.Join(", ", manifest.Prerequisites.Select(QuotePowerShell));
        var prerequisiteCheck = manifest.PrerequisitesDetection
            ? $$"""
                $missing = @()
                foreach ($requirement in @({{prerequisiteArray}})) {
                    switch ($requirement) {
                        'windows-x64' { if (-not [Environment]::Is64BitOperatingSystem) { $missing += $requirement } }
                        'vulkan-or-d3d12' { if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { $missing += $requirement } }
                        default { if ($requirement -like 'dotnet-*' -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += $requirement } }
                    }
                }
                if ($missing.Count -gt 0) { throw "Missing Windows prerequisites: $($missing -join ', ')" }
                Write-Output 'OPENRPG_FORGE_WINDOWS_PREREQUISITES_PASS count={{manifest.Prerequisites.Count}}'
                """
            : "Write-Output 'OPENRPG_FORGE_WINDOWS_PREREQUISITES_SKIPPED'";
        var installLog = manifest.InstallLog
            ? $$"""
                $log = Join-Path $Destination 'openrpg-forge-install.log'
                Add-Content -LiteralPath $log -Value "Installed {{manifest.PackageFile}} channel={{manifest.Channel}} prerequisites={{prerequisites}}"
                """
            : string.Empty;
        return $$"""
            [CmdletBinding()]
            param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'OpenRPG Forge\Modules'))
            $ErrorActionPreference = 'Stop'
            {{prerequisiteCheck}}
            $bundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
            $source = Join-Path $bundleRoot 'packages\{{manifest.PackageFile}}'
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Module package is missing from this release bundle.' }
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
            $target = Join-Path $Destination '{{manifest.PackageFile}}'
            Copy-Item -LiteralPath $source -Destination $target -Force
            $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($hash -ne '{{manifest.PackageSha256}}') { throw 'Installed module hash does not match release-manifest.json.' }
            {{installLog}}
            Write-Output "OPENRPG_FORGE_WINDOWS_INSTALL_PASS package={{manifest.PackageFile}} sha256=$hash"
            """ + Environment.NewLine;
    }

    private static string QuotePowerShell(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string BuildUninstallScript(ModuleReleaseManifest manifest) =>
        $$"""
            [CmdletBinding()]
            param([string]$Destination = (Join-Path $env:LOCALAPPDATA 'OpenRPG Forge\Modules'))
            $ErrorActionPreference = 'Stop'
            $target = Join-Path $Destination '{{manifest.PackageFile}}'
            if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
            $log = Join-Path $Destination 'openrpg-forge-install.log'
            if (Test-Path -LiteralPath $Destination -PathType Container) {
                Add-Content -LiteralPath $log -Value 'Uninstalled {{manifest.PackageFile}}'
            }
            Write-Output 'OPENRPG_FORGE_WINDOWS_UNINSTALL_PASS package={{manifest.PackageFile}}'
            """ + Environment.NewLine;

    private static string BuildUnixInstallScript(ModuleReleaseManifest manifest)
    {
        var expectedKernel = manifest.Platform == ModuleDistributionPlatform.LinuxX64 ? "Linux" : "Darwin";
        var expectedMachine = manifest.Platform == ModuleDistributionPlatform.LinuxX64 ? "x86_64" : "arm64";
        return $$"""
            #!/bin/sh
            set -eu
            destination="${1:-${XDG_DATA_HOME:-$HOME/.local/share}/openrpg-forge/modules}"
            bundle_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
            source_file="$bundle_root/packages/{{manifest.PackageFile}}"
            [ "$(uname -s)" = "{{expectedKernel}}" ] || { echo "ORF-BUILD-016-PLATFORM kernel" >&2; exit 20; }
            [ "$(uname -m)" = "{{expectedMachine}}" ] || { echo "ORF-BUILD-016-PLATFORM architecture" >&2; exit 21; }
            [ -f "$source_file" ] || { echo "ORF-BUILD-016-PACKAGE missing" >&2; exit 22; }
            mkdir -p "$destination"
            target="$destination/{{manifest.PackageFile}}"
            cp "$source_file" "$target"
            if command -v sha256sum >/dev/null 2>&1; then actual=$(sha256sum "$target" | awk '{print $1}'); else actual=$(shasum -a 256 "$target" | awk '{print $1}'); fi
            [ "$actual" = "{{manifest.PackageSha256}}" ] || { echo "ORF-BUILD-016-HASH mismatch" >&2; exit 23; }
            printf '%s\n' "OPENRPG_FORGE_UNIX_INSTALL_PASS platform={{manifest.Platform}} package={{manifest.PackageFile}} sha256=$actual"
            """ + Environment.NewLine;
    }

    private static string BuildUnixUninstallScript(ModuleReleaseManifest manifest) =>
        $$"""
            #!/bin/sh
            set -eu
            destination="${1:-${XDG_DATA_HOME:-$HOME/.local/share}/openrpg-forge/modules}"
            target="$destination/{{manifest.PackageFile}}"
            [ ! -f "$target" ] || rm -- "$target"
            printf '%s\n' 'OPENRPG_FORGE_UNIX_UNINSTALL_PASS platform={{manifest.Platform}} package={{manifest.PackageFile}}'
            """ + Environment.NewLine;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = ProjectJsonSerializer.CreateOptions();
        options.WriteIndented = true;
        return options;
    }

    private static string RequireFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{label} does not exist.", fullPath);
        }

        return fullPath;
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
