[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistributionDirectory,
    [Parameter(Mandatory)]
    [string]$Product,
    [string]$Version = '0.1.0-dev'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/distribution'))
if (-not [System.IO.Path]::IsPathRooted($DistributionDirectory)) {
    $DistributionDirectory = Join-Path $repositoryRoot $DistributionDirectory
}
$distributionPath = [System.IO.Path]::GetFullPath($DistributionDirectory)
$allowedPrefix = $distributionRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $distributionPath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The distribution must be below $distributionRoot."
}
if (-not (Test-Path -LiteralPath $distributionPath -PathType Container)) {
    throw "Distribution directory missing: $distributionPath"
}

function New-DeterministicGuid {
    param([Parameter(Mandatory)][string]$Value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    }
    finally {
        $sha.Dispose()
    }
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($bytes, $guidBytes, 16)
    $guidBytes[7] = ($guidBytes[7] -band 0x0f) -bor 0x50
    $guidBytes[8] = ($guidBytes[8] -band 0x3f) -bor 0x80
    return [Guid]::new($guidBytes)
}

function Read-NuGetMetadata {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ResolvedVersion
    )
    $packageRoot = Join-Path (Join-Path $env:USERPROFILE '.nuget/packages') `
        (Join-Path $Name.ToLowerInvariant() $ResolvedVersion.ToLowerInvariant())
    $nuspec = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nuspec' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $nuspec) {
        throw "NuGet metadata is unavailable for $Name $ResolvedVersion. Restore the solution first."
    }
    [xml]$document = Get-Content -LiteralPath $nuspec.FullName -Raw
    $metadata = $document.package.metadata
    $license = [string]$metadata.license
    if ([string]::IsNullOrWhiteSpace($license)) {
        throw "NuGet package $Name $ResolvedVersion has no declared license."
    }
    $nupkg = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nupkg' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    return [ordered]@{
        License = $license.Trim()
        Authors = ([string]$metadata.authors).Trim()
        ProjectUrl = ([string]$metadata.projectUrl).Trim()
        PackagePath = if ($null -eq $nupkg) { $null } else { $nupkg.FullName }
    }
}

$packageEntries = @{}
$dependencyEdges = @{}
$lockFiles = @('apps', 'src') | ForEach-Object {
    $sourceRoot = Join-Path $repositoryRoot $_
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter 'packages.lock.json' -File -ErrorAction Stop |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
}
foreach ($lockFile in $lockFiles) {
    $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json
    foreach ($target in $lock.dependencies.PSObject.Properties) {
        foreach ($package in $target.Value.PSObject.Properties) {
            $resolvedProperty = $package.Value.PSObject.Properties['resolved']
            if ($null -eq $resolvedProperty) { continue }
            $resolvedVersion = [string]$resolvedProperty.Value
            if ([string]::IsNullOrWhiteSpace($resolvedVersion)) { continue }
            $key = "$($package.Name.ToLowerInvariant())|$($resolvedVersion.ToLowerInvariant())"
            if (-not $packageEntries.ContainsKey($key)) {
                $packageEntries[$key] = [ordered]@{
                    Name = $package.Name
                    Version = $resolvedVersion
                    Type = [string]$package.Value.PSObject.Properties['type'].Value
                }
            }
            if (-not $dependencyEdges.ContainsKey($key)) {
                $dependencyEdges[$key] = New-Object System.Collections.Generic.HashSet[string]
            }
            $dependencyProperty = $package.Value.PSObject.Properties['dependencies']
            if ($null -ne $dependencyProperty -and $null -ne $dependencyProperty.Value) {
                foreach ($dependency in $dependencyProperty.Value.PSObject.Properties) {
                    $dependencyKey = "$($dependency.Name.ToLowerInvariant())|$(([string]$dependency.Value).ToLowerInvariant())"
                    [void]$dependencyEdges[$key].Add($dependencyKey)
                }
            }
        }
    }
}

$components = New-Object System.Collections.Generic.List[object]
$licenseRows = New-Object System.Collections.Generic.List[object]
foreach ($entry in @($packageEntries.Values | Sort-Object Name, Version)) {
    $metadata = Read-NuGetMetadata -Name $entry.Name -ResolvedVersion $entry.Version
    $bomRef = "pkg:nuget/$([Uri]::EscapeDataString($entry.Name))@$([Uri]::EscapeDataString($entry.Version))"
    $component = [ordered]@{
        type = 'library'
        'bom-ref' = $bomRef
        group = 'NuGet'
        name = $entry.Name
        version = $entry.Version
        scope = 'required'
        licenses = @([ordered]@{ license = [ordered]@{ id = $metadata.License } })
        purl = $bomRef
        properties = @(
            [ordered]@{ name = 'openrpg-forge:dependency-kind'; value = $entry.Type },
            [ordered]@{ name = 'openrpg-forge:authors'; value = $metadata.Authors }
        )
    }
    if (-not [string]::IsNullOrWhiteSpace($metadata.ProjectUrl)) {
        $component.externalReferences = @([ordered]@{ type = 'website'; url = $metadata.ProjectUrl })
    }
    if ($null -ne $metadata.PackagePath) {
        $component.hashes = @([ordered]@{
            alg = 'SHA-256'
            content = (Get-FileHash -LiteralPath $metadata.PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }
    $components.Add($component)
    $licenseRows.Add([pscustomobject]@{
        Component = $entry.Name
        Version = $entry.Version
        License = $metadata.License
        Authors = $metadata.Authors
        Source = $metadata.ProjectUrl
        Kind = 'NuGet'
    })
}

$manualComponents = @(
    [ordered]@{ Name='Godot Engine .NET'; Version='4.7.1'; License='MIT'; Authors='Godot Engine contributors'; Source='https://godotengine.org/'; Purl='pkg:generic/godot-engine@4.7.1' },
    [ordered]@{ Name='AngelScript'; Version='2.38.0'; License='Zlib'; Authors='Andreas Jonsson and contributors'; Source='https://www.angelcode.com/angelscript/'; Purl='pkg:generic/angelscript@2.38.0' },
    [ordered]@{ Name='.NET Runtime'; Version='10.0.7'; License='MIT'; Authors='Microsoft and contributors'; Source='https://dotnet.microsoft.com/'; Purl='pkg:generic/dotnet-runtime@10.0.7' }
)
foreach ($entry in $manualComponents) {
    $components.Add([ordered]@{
        type = 'framework'
        'bom-ref' = $entry.Purl
        name = $entry.Name
        version = $entry.Version
        scope = 'required'
        licenses = @([ordered]@{ license = [ordered]@{ id = $entry.License } })
        purl = $entry.Purl
        externalReferences = @([ordered]@{ type = 'website'; url = $entry.Source })
        properties = @([ordered]@{ name = 'openrpg-forge:authors'; value = $entry.Authors })
    })
    $licenseRows.Add([pscustomobject]@{
        Component = $entry.Name
        Version = $entry.Version
        License = $entry.License
        Authors = $entry.Authors
        Source = $entry.Source
        Kind = 'Runtime'
    })
}

$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceDirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0
$serial = New-DeterministicGuid -Value "$Product|$Version|$sourceRevision"
$dependencies = New-Object System.Collections.Generic.List[object]
foreach ($entry in @($packageEntries.GetEnumerator() | Sort-Object { $_.Value.Name }, { $_.Value.Version })) {
    $sourceRef = "pkg:nuget/$([Uri]::EscapeDataString($entry.Value.Name))@$([Uri]::EscapeDataString($entry.Value.Version))"
    $targets = New-Object System.Collections.Generic.List[string]
    foreach ($dependencyKey in @($dependencyEdges[$entry.Key] | Sort-Object)) {
        if (-not $packageEntries.ContainsKey($dependencyKey)) { continue }
        $dependency = $packageEntries[$dependencyKey]
        $targets.Add("pkg:nuget/$([Uri]::EscapeDataString($dependency.Name))@$([Uri]::EscapeDataString($dependency.Version))")
    }
    $dependencies.Add([ordered]@{ ref = $sourceRef; dependsOn = $targets.ToArray() })
}

$bom = [ordered]@{
    '$schema' = 'https://cyclonedx.org/schema/bom-1.6.schema.json'
    bomFormat = 'CycloneDX'
    specVersion = '1.6'
    serialNumber = "urn:uuid:$serial"
    version = 1
    metadata = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        tools = [ordered]@{ components = @([ordered]@{ type='application'; name='OpenRPG Forge compliance generator'; version='1' }) }
        component = [ordered]@{
            type = 'application'
            'bom-ref' = "pkg:generic/openrpg-forge@$Version"
            name = $Product
            version = $Version
            properties = @(
                [ordered]@{ name='openrpg-forge:source-revision'; value=$sourceRevision },
                [ordered]@{ name='openrpg-forge:source-dirty'; value=$sourceDirty.ToString().ToLowerInvariant() }
            )
        }
    }
    components = $components.ToArray()
    dependencies = $dependencies.ToArray()
}

$compliancePath = Join-Path $distributionPath 'compliance'
New-Item -ItemType Directory -Path $compliancePath -Force | Out-Null
$bom | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $compliancePath 'sbom.cdx.json') -Encoding utf8
$licenseRows | Sort-Object Component,Version | Export-Csv -LiteralPath (Join-Path $compliancePath 'licenses.csv') -NoTypeInformation -Encoding utf8

$notice = New-Object System.Collections.Generic.List[string]
$notice.Add('OPENRPG FORGE - THIRD-PARTY NOTICES')
$notice.Add('')
$notice.Add("Product: $Product $Version")
$notice.Add("Source revision: $sourceRevision")
$notice.Add('')
foreach ($row in @($licenseRows | Sort-Object Component,Version)) {
    $notice.Add("$($row.Component) $($row.Version) | $($row.License) | $($row.Authors)")
    if (-not [string]::IsNullOrWhiteSpace($row.Source)) { $notice.Add("Source: $($row.Source)") }
    $notice.Add('')
}
$notice | Set-Content -LiteralPath (Join-Path $compliancePath 'THIRD-PARTY-NOTICES.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/assets/THIRD_PARTY_ASSET_CREDITS.md') `
    -Destination (Join-Path $compliancePath 'ASSET-CREDITS.md') -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'native/ForgeScript/THIRD_PARTY_NOTICES.md') `
    -Destination (Join-Path $compliancePath 'FORGESCRIPT-NOTICES.md') -Force

$primaryExecutable = Get-ChildItem -LiteralPath $distributionPath -Filter '*.exe' -File |
    Where-Object { $_.Name -notmatch 'uninstall' } | Select-Object -First 1
$signatureStatus = 'No executable found'
if ($null -ne $primaryExecutable) {
    $signatureStatus = [string](Get-AuthenticodeSignature -LiteralPath $primaryExecutable.FullName).Status
}
@(
    'OPENRPG FORGE - SIGNING STATUS',
    '',
    "Product: $Product $Version",
    "Executable status: $signatureStatus",
    '',
    'Public release policy: Authenticode signing is required when a trusted certificate is available.',
    'An unsigned build is a development candidate and may trigger Windows SmartScreen.'
) | Set-Content -LiteralPath (Join-Path $compliancePath 'SIGNING-STATUS.txt') -Encoding utf8

Write-Output "OPENRPG_FORGE_DISTRIBUTION_COMPLIANCE_READY product=$Product components=$($components.Count) licenses=$($licenseRows.Count) signature=$signatureStatus"
