[CmdletBinding()]
param(
    [ValidateSet("Audit", "SelfTest", "IsolatedCheck")]
    [string]$Mode = "Audit",

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [ValidateSet("None", "Targeted", "Workspace")]
    [string]$CargoCheck = "None",

    [string]$CargoTargetDirectory,

    [string]$CadleReferenceRoot,

    [string]$OutputPath,

    [switch]$RequireCleanTree,

    [switch]$RequireAssetManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:KnownSpdxLicenses = @(
    "Apache-2.0",
    "BSD-2-Clause",
    "BSD-3-Clause",
    "CC0-1.0",
    "ISC",
    "MIT",
    "MPL-2.0",
    "Zlib"
)

function Invoke-CapturedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [string]$WorkingDirectory,

        [int[]]$AllowedExitCodes = @(0)
    )

    $previousLocation = $null
    try {
        if ($WorkingDirectory) {
            $previousLocation = Get-Location
            Set-Location -LiteralPath $WorkingDirectory
        }

        $commandOutput = @(& $FilePath @ArgumentList 2>&1 | ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
        if ($AllowedExitCodes -notcontains $exitCode) {
            $renderedArguments = $ArgumentList -join " "
            $renderedOutput = ($commandOutput | Select-Object -Last 40) -join [Environment]::NewLine
            throw "Command failed ($exitCode): $FilePath $renderedArguments`n$renderedOutput"
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = $commandOutput
        }
    }
    finally {
        if ($null -ne $previousLocation) {
            Set-Location -LiteralPath $previousLocation
        }
    }
}

function Get-NormalizedRoot {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
}

function Get-GitResult {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    return Invoke-CapturedCommand -FilePath "git" -ArgumentList (@("-C", $Root) + $Arguments) -AllowedExitCodes $AllowedExitCodes
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-PortableRelativePath {
    param([AllowEmptyString()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }
    if ([IO.Path]::IsPathRooted($Path) -or $Path.Contains("\") -or $Path.Contains(":")) {
        return $false
    }
    if ($Path.StartsWith("/") -or $Path.EndsWith("/")) {
        return $false
    }

    foreach ($component in $Path.Split("/")) {
        if ([string]::IsNullOrWhiteSpace($component) -or $component -eq "." -or $component -eq "..") {
            return $false
        }
    }

    return $true
}

function Get-TrackedFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    $result = Get-GitResult -Root $Root -Arguments @("ls-files")
    return @($result.Output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

function Get-OptionalHead {
    param([Parameter(Mandatory = $true)][string]$Root)

    $result = Get-GitResult -Root $Root -Arguments @("rev-parse", "--verify", "HEAD") -AllowedExitCodes @(0, 128)
    if ($result.ExitCode -eq 0) {
        return ($result.Output -join "").Trim()
    }
    return $null
}

function Get-TextIfSupported {
    param([Parameter(Mandatory = $true)][string]$Path)

    $supportedExtensions = @(
        ".css", ".gltf", ".html", ".js", ".json", ".md", ".ron", ".rs",
        ".toml", ".ts", ".tsx", ".txt", ".wgsl", ".yaml", ".yml"
    )
    if ($supportedExtensions -notcontains [IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        return $null
    }

    return [IO.File]::ReadAllText($Path)
}

function Add-Issue {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $Issues.Add($Message)
}

function Test-CanonicalIdentityBoundary {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$TrackedFiles,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues
    )

    $canonicalFiles = @($TrackedFiles | Where-Object {
        $_ -like "examples/visual_valley/*" -or
        $_ -like "assets/visual_valley/*" -or
        $_ -like "crates/forge_engine/src/shaders/visual_valley*"
    })
    $patterns = [ordered]@{
        "cadle" = "(?i)(^|[^a-z0-9])cadle([^a-z0-9]|$)"
        "humpalumps" = "(?i)(^|[^a-z0-9])humpalumps([^a-z0-9]|$)"
        "the-vale" = "(?i)(^|[^a-z0-9])the[ _-]+vale([^a-z0-9]|$)"
        "three-js" = "(?i)(^|[^a-z0-9])three(?:\.js)?([^a-z0-9]|$)"
        "whisperwood" = "(?i)(^|[^a-z0-9])whisperwood([^a-z0-9]|$)"
    }

    foreach ($relativePath in $canonicalFiles) {
        $fullPath = Join-Path $Root ($relativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
        foreach ($entry in $patterns.GetEnumerator()) {
            if ($relativePath -match $entry.Value) {
                Add-Issue -Issues $Issues -Message "Forbidden upstream identity '$($entry.Key)' occurs in tracked path '$relativePath'."
            }
        }

        $text = Get-TextIfSupported -Path $fullPath
        if ($null -eq $text) {
            continue
        }
        foreach ($entry in $patterns.GetEnumerator()) {
            if ($text -match $entry.Value) {
                Add-Issue -Issues $Issues -Message "Forbidden upstream identity '$($entry.Key)' occurs in canonical content '$relativePath'."
            }
        }
    }

    return $canonicalFiles.Count
}

function Test-ProductDependencyBoundary {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$TrackedFiles,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues
    )

    $dependencyFiles = @($TrackedFiles | Where-Object {
        $leaf = [IO.Path]::GetFileName($_).ToLowerInvariant()
        $extension = [IO.Path]::GetExtension($_).ToLowerInvariant()
        $inProductTree = $_ -like "apps/*" -or $_ -like "crates/*" -or $_ -like "tools/*" -or $_ -like ".cargo/*"
        $_ -eq "Cargo.toml" -or $_ -eq "Cargo.lock" -or
        ($inProductTree -and (
            $leaf -in @("build.rs", "cargo.toml", "index.html", "package.json", "package-lock.json", "trunk.toml") -or
            $extension -in @(".html", ".js", ".json", ".rs", ".toml", ".ts", ".tsx")
        ))
    })
    $patterns = [ordered]@{
        "cadle-path" = '(?i)upstream[\\/]+cadle(?:[\\/]|$)'
        "cadle-url" = '(?i)github\.com[\\/]humpalumps[\\/]cadle(?:\.git)?(?:[\\/#]|$)'
        "cadle-package" = '(?im)^\s*cadle\s*='
        "three-package" = '(?im)(?:^\s*three\s*=|"three"\s*:)'
        "postprocessing-package" = '(?im)(?:^\s*postprocessing\s*=|"postprocessing"\s*:)'
        "vite-package" = '(?im)(?:^\s*vite\s*=|"vite"\s*:)'
    }

    foreach ($relativePath in $dependencyFiles) {
        $fullPath = Join-Path $Root ($relativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
        $text = Get-TextIfSupported -Path $fullPath
        if ($null -eq $text) {
            continue
        }
        foreach ($entry in $patterns.GetEnumerator()) {
            if ($text -match $entry.Value) {
                Add-Issue -Issues $Issues -Message "Forbidden product dependency '$($entry.Key)' occurs in '$relativePath'."
            }
        }
    }

    return $dependencyFiles.Count
}

function Test-AssetManifests {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$TrackedFiles,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues
    )

    $manifestPaths = @($TrackedFiles | Where-Object {
        $_ -like "assets/visual_valley/*/subset.manifest.json"
    } | Sort-Object)
    $assetFiles = @($TrackedFiles | Where-Object { $_ -like "assets/visual_valley/*" })
    $claimedAssetFiles = @{}
    $seenSubsetIds = @{}
    $licenseIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $validatedFileCount = 0
    $validatedModelCount = 0

    foreach ($manifestPath in $manifestPaths) {
        $manifestFullPath = Join-Path $Root ($manifestPath -replace "/", [IO.Path]::DirectorySeparatorChar)
        $manifestTrackedDirectory = $manifestPath.Substring(0, $manifestPath.LastIndexOf("/"))
        $manifestDirectory = Split-Path -Parent $manifestFullPath
        try {
            $manifest = [IO.File]::ReadAllText($manifestFullPath) | ConvertFrom-Json -Depth 64
            if ($manifest.schema -ne "forge.asset-subset-manifest" -or [int]$manifest.schema_version -ne 1) {
                throw "unsupported schema '$($manifest.schema)' version '$($manifest.schema_version)'"
            }
            if ([string]::IsNullOrWhiteSpace([string]$manifest.subset_id)) {
                throw "subset_id is empty"
            }
            if ($seenSubsetIds.ContainsKey([string]$manifest.subset_id)) {
                throw "duplicate subset_id '$($manifest.subset_id)'"
            }
            $seenSubsetIds[[string]$manifest.subset_id] = $manifestPath

            foreach ($requiredSourceField in @("pack_id", "name", "version", "author", "source_page", "distribution_page", "archive_url", "retrieved_at", "archive_sha256", "archive_bytes", "normalized_metadata_sha256")) {
                $sourceProperty = $manifest.source.PSObject.Properties[$requiredSourceField]
                if ($null -eq $sourceProperty -or [string]::IsNullOrWhiteSpace([string]$sourceProperty.Value)) {
                    throw "source.$requiredSourceField is missing or empty"
                }
            }
            foreach ($urlField in @("source_page", "distribution_page", "archive_url")) {
                $uri = $null
                if (-not [Uri]::TryCreate([string]$manifest.source.$urlField, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne "https") {
                    throw "source.$urlField must be an absolute HTTPS URL"
                }
            }
            $retrievedAt = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse([string]$manifest.source.retrieved_at, [ref]$retrievedAt)) {
                throw "source.retrieved_at is not a valid timestamp"
            }
            foreach ($hashField in @("archive_sha256", "normalized_metadata_sha256")) {
                if ([string]$manifest.source.$hashField -cnotmatch "^[0-9a-f]{64}$") {
                    throw "source.$hashField must be a lowercase SHA-256"
                }
            }
            if ([long]$manifest.source.archive_bytes -le 0) {
                throw "source.archive_bytes must be positive"
            }

            if ($manifest.license.verified -ne $true) {
                throw "license.verified must be true"
            }
            $spdx = [string]$manifest.license.spdx
            if ($script:KnownSpdxLicenses -notcontains $spdx) {
                throw "license.spdx '$spdx' is not in the audited allow-list"
            }
            $null = $licenseIds.Add($spdx)
            $licenseUri = $null
            if (-not [Uri]::TryCreate([string]$manifest.license.url, [UriKind]::Absolute, [ref]$licenseUri) -or $licenseUri.Scheme -ne "https") {
                throw "license.url must be an absolute HTTPS URL"
            }
            if (-not (Test-PortableRelativePath -Path ([string]$manifest.license.evidence_path))) {
                throw "license.evidence_path is not a portable relative path"
            }
            if ([string]$manifest.license.evidence_sha256 -cnotmatch "^[0-9a-f]{64}$") {
                throw "license.evidence_sha256 must be a lowercase SHA-256"
            }

            $fileEntries = @($manifest.files)
            if ($fileEntries.Count -eq 0) {
                throw "files must contain at least one entry"
            }
            $declaredPaths = @{}
            foreach ($fileEntry in $fileEntries) {
                $relativeAssetPath = [string]$fileEntry.path
                if (-not (Test-PortableRelativePath -Path $relativeAssetPath)) {
                    throw "files.path '$relativeAssetPath' is not a portable relative path"
                }
                if ($declaredPaths.ContainsKey($relativeAssetPath)) {
                    throw "duplicate files.path '$relativeAssetPath'"
                }
                $declaredPaths[$relativeAssetPath] = $fileEntry
                if (-not (Test-PortableRelativePath -Path ([string]$fileEntry.source_path))) {
                    throw "files.source_path '$($fileEntry.source_path)' is not a portable relative path"
                }
                if ([string]::IsNullOrWhiteSpace([string]$fileEntry.transformation)) {
                    throw "files.transformation is empty for '$relativeAssetPath'"
                }
                if ([string]$fileEntry.sha256 -cnotmatch "^[0-9a-f]{64}$") {
                    throw "files.sha256 is not a lowercase SHA-256 for '$relativeAssetPath'"
                }

                $fullAssetPath = [IO.Path]::GetFullPath((Join-Path $manifestDirectory ($relativeAssetPath -replace "/", [IO.Path]::DirectorySeparatorChar)))
                $directoryPrefix = [IO.Path]::GetFullPath($manifestDirectory).TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
                if (-not $fullAssetPath.StartsWith($directoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "files.path escapes the subset directory: '$relativeAssetPath'"
                }
                if (-not (Test-Path -LiteralPath $fullAssetPath -PathType Leaf)) {
                    throw "declared file is missing: '$relativeAssetPath'"
                }
                $actualBytes = (Get-Item -LiteralPath $fullAssetPath).Length
                if ([long]$fileEntry.bytes -ne $actualBytes) {
                    throw "byte count mismatch for '$relativeAssetPath': declared $($fileEntry.bytes), actual $actualBytes"
                }
                $actualHash = Get-Sha256 -Path $fullAssetPath
                if ([string]$fileEntry.sha256 -cne $actualHash) {
                    throw "SHA-256 mismatch for '$relativeAssetPath': declared $($fileEntry.sha256), actual $actualHash"
                }

                $trackedAssetPath = "$manifestTrackedDirectory/$relativeAssetPath"
                if ($TrackedFiles -cnotcontains $trackedAssetPath) {
                    throw "declared file is not tracked: '$trackedAssetPath'"
                }
                if ($claimedAssetFiles.ContainsKey($trackedAssetPath)) {
                    throw "asset is claimed by multiple manifests: '$trackedAssetPath'"
                }
                $claimedAssetFiles[$trackedAssetPath] = $manifestPath
                $validatedFileCount++
            }

            $licenseEvidencePath = [string]$manifest.license.evidence_path
            if (-not $declaredPaths.ContainsKey($licenseEvidencePath)) {
                throw "license evidence '$licenseEvidencePath' is not declared in files"
            }
            $licenseEvidenceFullPath = Join-Path $manifestDirectory ($licenseEvidencePath -replace "/", [IO.Path]::DirectorySeparatorChar)
            $licenseEvidenceHash = Get-Sha256 -Path $licenseEvidenceFullPath
            if ([string]$manifest.license.evidence_sha256 -cne $licenseEvidenceHash) {
                throw "license evidence SHA-256 mismatch: declared $($manifest.license.evidence_sha256), actual $licenseEvidenceHash"
            }
            if ([string]$declaredPaths[$licenseEvidencePath].sha256 -cne $licenseEvidenceHash) {
                throw "license evidence files.sha256 differs from license.evidence_sha256"
            }

            $modelEntries = @($manifest.models)
            if ($modelEntries.Count -eq 0) {
                throw "models must contain at least one entry"
            }
            $seenModelIds = @{}
            foreach ($modelEntry in $modelEntries) {
                if ([string]::IsNullOrWhiteSpace([string]$modelEntry.id) -or $seenModelIds.ContainsKey([string]$modelEntry.id)) {
                    throw "model id is empty or duplicated: '$($modelEntry.id)'"
                }
                $seenModelIds[[string]$modelEntry.id] = $true
                if ([string]::IsNullOrWhiteSpace([string]$modelEntry.kind)) {
                    throw "model kind is empty for '$($modelEntry.id)'"
                }
                $scenePath = [string]$modelEntry.scene_path
                if (-not (Test-PortableRelativePath -Path $scenePath) -or -not $scenePath.StartsWith("visual_valley/", [StringComparison]::Ordinal)) {
                    throw "model scene_path must be under visual_valley/: '$scenePath'"
                }
                $expectedTrackedScene = "assets/$scenePath"
                if (-not $claimedAssetFiles.ContainsKey($expectedTrackedScene)) {
                    throw "model scene_path is not declared as a file: '$expectedTrackedScene'"
                }
                $sceneRelativePath = $expectedTrackedScene.Substring($manifestTrackedDirectory.Length + 1)
                $sceneFileEntry = $declaredPaths[$sceneRelativePath]
                if ([string]$sceneFileEntry.source_path -cne [string]$modelEntry.source_path) {
                    throw "model source_path differs from its file entry for '$($modelEntry.id)'"
                }
                $baseScale = @($modelEntry.base_scale)
                $invalidScaleComponents = @($baseScale | Where-Object {
                    $value = [double]$_
                    -not [double]::IsFinite($value) -or $value -le 0
                })
                if (($baseScale.Count -ne 1 -and $baseScale.Count -ne 3) -or $invalidScaleComponents.Count -ne 0) {
                    throw "model base_scale must be one positive finite number or three positive finite numbers for '$($modelEntry.id)'"
                }
                $validatedModelCount++
            }

            foreach ($trackedAsset in @($assetFiles | Where-Object {
                $_ -like "$manifestTrackedDirectory/*" -and $_ -cne $manifestPath
            })) {
                if (-not $claimedAssetFiles.ContainsKey($trackedAsset)) {
                    throw "tracked subset file is absent from manifest.files: '$trackedAsset'"
                }
            }
        }
        catch {
            Add-Issue -Issues $Issues -Message "Invalid asset manifest '$manifestPath': $($_.Exception.Message)"
        }
    }

    foreach ($assetFile in $assetFiles) {
        if ($manifestPaths -ccontains $assetFile) {
            continue
        }
        if (-not $claimedAssetFiles.ContainsKey($assetFile)) {
            Add-Issue -Issues $Issues -Message "Tracked Visual Valley asset has no validated subset manifest: '$assetFile'."
        }
    }

    return [pscustomobject][ordered]@{
        manifests = $manifestPaths.Count
        files = $validatedFileCount
        models = $validatedModelCount
        licenses = @($licenseIds | Sort-Object)
    }
}

function Test-PinnedCadleReference {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string[]]$TrackedFiles,
        [Parameter(Mandatory = $true)][string]$CadleRoot,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues
    )

    $lockPath = Join-Path $Root "upstream/cadle.lock.json"
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf) -or $TrackedFiles -cnotcontains "upstream/cadle.lock.json") {
        Add-Issue -Issues $Issues -Message "Pinned Cadle comparison requires tracked upstream/cadle.lock.json."
        return [pscustomobject][ordered]@{ status = "FAIL"; files_hashed = 0; exact_matches = 0; normalized_line_matches = 0 }
    }

    try {
        $lock = [IO.File]::ReadAllText($lockPath) | ConvertFrom-Json -Depth 32
        $normalizedCadleRoot = Get-NormalizedRoot -Path $CadleRoot
        $reportedCadleRoot = ((Get-GitResult -Root $normalizedCadleRoot -Arguments @("rev-parse", "--show-toplevel")).Output -join "").Trim()
        if ((Get-NormalizedRoot -Path $reportedCadleRoot) -cne $normalizedCadleRoot) {
            throw "CadleReferenceRoot must be the exact Git top-level"
        }
        $cadleHead = ((Get-GitResult -Root $normalizedCadleRoot -Arguments @("rev-parse", "HEAD")).Output -join "").Trim()
        $cadleTree = ((Get-GitResult -Root $normalizedCadleRoot -Arguments @("rev-parse", "HEAD^{tree}")).Output -join "").Trim()
        if ($cadleHead -cne [string]$lock.repository.commit -or $cadleTree -cne [string]$lock.repository.tree) {
            throw "Cadle reference does not match the pinned commit/tree in upstream/cadle.lock.json"
        }
        $cadleStatus = (Get-GitResult -Root $normalizedCadleRoot -Arguments @("status", "--porcelain=v1")).Output
        if (@($cadleStatus).Count -ne 0) {
            throw "Cadle reference must be clean"
        }

        $cadleFiles = @(Get-TrackedFiles -Root $normalizedCadleRoot)
        $cadleHashes = @{}
        $cadleNormalizedLines = @{}
        foreach ($cadleRelativePath in $cadleFiles) {
            $cadleFullPath = Join-Path $normalizedCadleRoot ($cadleRelativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $cadleFullPath -PathType Leaf)) {
                throw "Pinned Cadle tracked file is missing: '$cadleRelativePath'"
            }
            $cadleHash = Get-Sha256 -Path $cadleFullPath
            if (-not $cadleHashes.ContainsKey($cadleHash)) {
                $cadleHashes[$cadleHash] = $cadleRelativePath
            }

            if ((Get-Item -LiteralPath $cadleFullPath).Length -gt 2MB) {
                continue
            }
            $cadleText = Get-TextIfSupported -Path $cadleFullPath
            if ($null -eq $cadleText -or $cadleRelativePath -match "(?i)(^|/)(license|copying|notice)[^/]*$") {
                continue
            }
            foreach ($line in ($cadleText -split "`r?`n")) {
                $normalizedLine = ([regex]::Replace($line.Trim(), "\s+", " "))
                if ($normalizedLine.Length -ge 40 -and -not $cadleNormalizedLines.ContainsKey($normalizedLine)) {
                    $cadleNormalizedLines[$normalizedLine] = $cadleRelativePath
                }
            }
        }

        $comparisonFiles = @($TrackedFiles | Where-Object {
            $_ -like "examples/visual_valley/*" -or
            $_ -like "assets/visual_valley/*" -or
            $_ -like "crates/forge_engine/src/shaders/visual_valley*"
        })
        $exactMatches = 0
        $normalizedLineMatches = 0
        foreach ($forgeRelativePath in $comparisonFiles) {
            $forgeFullPath = Join-Path $Root ($forgeRelativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
            if ($forgeRelativePath -notmatch "(?i)(^|/)(license|copying|notice)[^/]*$" -and $forgeRelativePath -notlike "*/subset.manifest.json") {
                $forgeHash = Get-Sha256 -Path $forgeFullPath
                if ($cadleHashes.ContainsKey($forgeHash)) {
                    $exactMatches++
                    Add-Issue -Issues $Issues -Message "Exact file copy matches pinned Cadle: Forge '$forgeRelativePath', Cadle '$($cadleHashes[$forgeHash])', SHA-256 $forgeHash."
                }
            }

            if ($forgeRelativePath -like "assets/visual_valley/*") {
                continue
            }
            $forgeText = Get-TextIfSupported -Path $forgeFullPath
            if ($null -eq $forgeText) {
                continue
            }
            foreach ($line in ($forgeText -split "`r?`n")) {
                $normalizedLine = ([regex]::Replace($line.Trim(), "\s+", " "))
                if ($normalizedLine.Length -ge 40 -and $cadleNormalizedLines.ContainsKey($normalizedLine)) {
                    $normalizedLineMatches++
                    $lineHashBytes = [Text.Encoding]::UTF8.GetBytes($normalizedLine)
                    $lineHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($lineHashBytes)).ToLowerInvariant()
                    Add-Issue -Issues $Issues -Message "Normalized line copy matches pinned Cadle: Forge '$forgeRelativePath', Cadle '$($cadleNormalizedLines[$normalizedLine])', line SHA-256 $lineHash."
                }
            }
        }

        return [pscustomobject][ordered]@{
            status = if ($exactMatches -eq 0 -and $normalizedLineMatches -eq 0) { "PASS" } else { "FAIL" }
            commit = $cadleHead
            tree = $cadleTree
            files_hashed = $cadleFiles.Count
            forge_files_compared = $comparisonFiles.Count
            exact_matches = $exactMatches
            normalized_line_matches = $normalizedLineMatches
        }
    }
    catch {
        Add-Issue -Issues $Issues -Message "Pinned Cadle comparison failed: $($_.Exception.Message)"
        return [pscustomobject][ordered]@{ status = "FAIL"; files_hashed = 0; exact_matches = 0; normalized_line_matches = 0 }
    }
}

function Invoke-RepositoryAudit {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [string]$CadleRoot,
        [switch]$RequireClean,
        [switch]$RequireAssets
    )

    $normalizedRoot = Get-NormalizedRoot -Path $Root
    $reportedRoot = ((Get-GitResult -Root $normalizedRoot -Arguments @("rev-parse", "--show-toplevel")).Output -join "").Trim()
    if ((Get-NormalizedRoot -Path $reportedRoot) -cne $normalizedRoot) {
        throw "RepositoryRoot must be the exact Git top-level. Expected '$reportedRoot', got '$normalizedRoot'."
    }
    if ($RequireClean) {
        $status = (Get-GitResult -Root $normalizedRoot -Arguments @("status", "--porcelain=v1")).Output
        if (@($status).Count -ne 0) {
            throw "Repository must be clean for this audit; git status reported: $($status -join '; ')"
        }
    }

    $trackedFiles = @(Get-TrackedFiles -Root $normalizedRoot)
    $issues = [Collections.Generic.List[string]]::new()
    $trackedCadlePayload = @($trackedFiles | Where-Object { $_ -like "upstream/cadle/*" })
    foreach ($path in $trackedCadlePayload) {
        Add-Issue -Issues $issues -Message "Cadle payload is tracked in the product repository: '$path'."
    }

    $dependencyFileCount = Test-ProductDependencyBoundary -Root $normalizedRoot -TrackedFiles $trackedFiles -Issues $issues
    $canonicalFileCount = Test-CanonicalIdentityBoundary -Root $normalizedRoot -TrackedFiles $trackedFiles -Issues $issues
    $assetSummary = Test-AssetManifests -Root $normalizedRoot -TrackedFiles $trackedFiles -Issues $issues
    if ($RequireAssets -and $assetSummary.manifests -eq 0) {
        Add-Issue -Issues $issues -Message "At least one validated Visual Valley asset subset manifest is required."
    }
    $cadleComparison = if ($CadleRoot) {
        Test-PinnedCadleReference -Root $normalizedRoot -TrackedFiles $trackedFiles -CadleRoot $CadleRoot -Issues $issues
    }
    else {
        [pscustomobject][ordered]@{
            status = "NOT_RUN"
            files_hashed = 0
            exact_matches = 0
            normalized_line_matches = 0
        }
    }

    $declaredReuseMarkers = @()
    foreach ($relativePath in @($trackedFiles | Where-Object {
        $_ -like "examples/visual_valley/*" -or $_ -like "assets/visual_valley/*" -or $_ -like "crates/forge_engine/src/shaders/visual_valley*"
    })) {
        $fullPath = Join-Path $normalizedRoot ($relativePath -replace "/", [IO.Path]::DirectorySeparatorChar)
        $text = Get-TextIfSupported -Path $fullPath
        if ($null -ne $text -and $text -match "(?i)(?:SPDX-License-Identifier\s*:\s*LicenseRef-Cadle|derived\s+from\s+Cadle)") {
            $declaredReuseMarkers += $relativePath
        }
    }
    foreach ($path in $declaredReuseMarkers) {
        Add-Issue -Issues $issues -Message "Declared Cadle-derived product content requires an explicit, separately reviewed reuse record: '$path'."
    }

    if ($issues.Count -ne 0) {
        throw "Gate 1 provenance audit failed with $($issues.Count) issue(s):`n- $($issues -join "`n- ")"
    }

    $head = Get-OptionalHead -Root $normalizedRoot
    $tree = $null
    if ($null -ne $head) {
        $tree = ((Get-GitResult -Root $normalizedRoot -Arguments @("rev-parse", "HEAD^{tree}")).Output -join "").Trim()
    }
    return [pscustomobject][ordered]@{
        schema = "forge.gate1-provenance-audit"
        schema_version = 1
        generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
        repository = [pscustomobject][ordered]@{
            head = $head
            tree = $tree
            tracked_files = $trackedFiles.Count
        }
        boundaries = [pscustomobject][ordered]@{
            tracked_cadle_payload_files = 0
            declared_cadle_reuse_markers = 0
            dependency_files_scanned = $dependencyFileCount
            canonical_files_scanned = $canonicalFileCount
        }
        assets = $assetSummary
        cadle_reference_comparison = $cadleComparison
        result = "PASS"
    }
}

function Write-AuditResult {
    param(
        [Parameter(Mandatory = $true)]$Result,
        [string]$Path,
        [Parameter(Mandatory = $true)][string]$Marker
    )

    if ($Path) {
        $absolutePath = [IO.Path]::GetFullPath($Path)
        $parent = Split-Path -Parent $absolutePath
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            $null = New-Item -ItemType Directory -Path $parent -Force
        }
        [IO.File]::WriteAllText($absolutePath, ($Result | ConvertTo-Json -Depth 64) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
        Write-Host "$Marker output=$absolutePath"
    }
    else {
        Write-Host $Marker
    }
}

function Remove-ProvenanceTempDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd("\", "/")
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
    $leaf = Split-Path -Leaf $fullPath
    if (-not $fullPath.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not $leaf.StartsWith("forge-gate1-provenance-", [StringComparison]::Ordinal)) {
        throw "Refusing to remove unsafe temporary path '$fullPath'."
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

function New-SyntheticFixture {
    param([Parameter(Mandatory = $true)][string]$Root)

    $assetDirectory = Join-Path $Root "assets/visual_valley/test-pack"
    $exampleDirectory = Join-Path $Root "examples/visual_valley"
    $shaderDirectory = Join-Path $Root "crates/forge_engine/src/shaders"
    foreach ($directory in @($assetDirectory, $exampleDirectory, $shaderDirectory)) {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }

    [IO.File]::WriteAllText((Join-Path $Root "Cargo.toml"), "[workspace]`nmembers = []`nresolver = `"2`"`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $exampleDirectory "environment.v1.json"), "{`"schema`":`"forge.visual-valley.environment`"}`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $shaderDirectory "visual_valley_test.wgsl"), "// independent test shader`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $assetDirectory "License.txt"), "CC0 1.0 Universal`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllBytes((Join-Path $assetDirectory "model.bin"), [byte[]](1, 2, 3, 4))
    [IO.File]::WriteAllText((Join-Path $assetDirectory "model.gltf"), "{`"asset`":{`"version`":`"2.0`"},`"buffers`": [{`"uri`":`"model.bin`",`"byteLength`":4}]}`n", [Text.UTF8Encoding]::new($false))

    $licenseHash = Get-Sha256 -Path (Join-Path $assetDirectory "License.txt")
    $binHash = Get-Sha256 -Path (Join-Path $assetDirectory "model.bin")
    $gltfHash = Get-Sha256 -Path (Join-Path $assetDirectory "model.gltf")
    $manifest = [ordered]@{
        schema = "forge.asset-subset-manifest"
        schema_version = 1
        subset_id = "synthetic-cc0-test-pack"
        source = [ordered]@{
            pack_id = "synthetic"
            name = "Synthetic test pack"
            version = "1.0"
            author = "Forge test suite"
            source_page = "https://example.invalid/source"
            distribution_page = "https://example.invalid/distribution"
            archive_url = "https://example.invalid/archive.zip"
            retrieved_at = "2026-09-01T00:00:00Z"
            archive_sha256 = "a" * 64
            archive_bytes = 4
            normalized_metadata_sha256 = "b" * 64
        }
        license = [ordered]@{
            spdx = "CC0-1.0"
            url = "https://creativecommons.org/publicdomain/zero/1.0/"
            verified = $true
            attribution_required = $false
            evidence_path = "License.txt"
            evidence_sha256 = $licenseHash
        }
        models = @(
            [ordered]@{
                id = "synthetic-model"
                kind = "test"
                scene_path = "visual_valley/test-pack/model.gltf"
                source_path = "source/model.gltf"
                base_scale = @(1.0, 1.0, 1.0)
            }
        )
        files = @(
            [ordered]@{ path = "License.txt"; source_path = "License.txt"; bytes = (Get-Item (Join-Path $assetDirectory "License.txt")).Length; sha256 = $licenseHash; transformation = "verbatim" },
            [ordered]@{ path = "model.bin"; source_path = "source/model.bin"; bytes = 4; sha256 = $binHash; transformation = "verbatim" },
            [ordered]@{ path = "model.gltf"; source_path = "source/model.gltf"; bytes = (Get-Item (Join-Path $assetDirectory "model.gltf")).Length; sha256 = $gltfHash; transformation = "verbatim" }
        )
    }
    [IO.File]::WriteAllText(
        (Join-Path $assetDirectory "subset.manifest.json"),
        ($manifest | ConvertTo-Json -Depth 16) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false)
    )

    $null = Invoke-CapturedCommand -FilePath "git" -ArgumentList @("init", "--quiet", "--initial-branch=proof", $Root)
    $null = Get-GitResult -Root $Root -Arguments @("add", "-A")
}

function Assert-ExpectedFailure {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Case
    )

    $failedAsExpected = $false
    try {
        $null = & $Action
    }
    catch {
        if ($_.Exception.Message -notmatch $Pattern) {
            throw "Self-test '$Case' failed for an unexpected reason: $($_.Exception.Message)"
        }
        $failedAsExpected = $true
    }
    if (-not $failedAsExpected) {
        throw "Self-test '$Case' did not fail as expected."
    }
}

function Invoke-SelfTest {
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("forge-gate1-provenance-selftest-" + [Guid]::NewGuid().ToString("N"))
    $forgeRoot = Join-Path $tempRoot "forge"
    $cadleRoot = Join-Path $tempRoot "reference"
    $null = New-Item -ItemType Directory -Path $forgeRoot -Force
    $null = New-Item -ItemType Directory -Path $cadleRoot -Force
    try {
        New-SyntheticFixture -Root $forgeRoot
        $positive = Invoke-RepositoryAudit -Root $forgeRoot
        if ($positive.result -ne "PASS" -or $positive.assets.manifests -ne 1 -or $positive.assets.files -ne 3 -or $positive.assets.models -ne 1) {
            throw "Positive self-test returned an unexpected ledger."
        }

        $modelPath = Join-Path $forgeRoot "assets/visual_valley/test-pack/model.bin"
        $modelBytes = [IO.File]::ReadAllBytes($modelPath)
        [IO.File]::WriteAllBytes($modelPath, [byte[]](9, 9, 9, 9))
        Assert-ExpectedFailure -Case "tampered hash" -Pattern "SHA-256 mismatch" -Action { Invoke-RepositoryAudit -Root $forgeRoot }
        [IO.File]::WriteAllBytes($modelPath, $modelBytes)

        $orphanPath = Join-Path $forgeRoot "assets/visual_valley/test-pack/orphan.bin"
        [IO.File]::WriteAllBytes($orphanPath, [byte[]](5))
        $null = Get-GitResult -Root $forgeRoot -Arguments @("add", "-A")
        Assert-ExpectedFailure -Case "orphan asset" -Pattern "absent from manifest|no validated subset manifest" -Action { Invoke-RepositoryAudit -Root $forgeRoot }
        Remove-Item -LiteralPath $orphanPath -Force
        $null = Get-GitResult -Root $forgeRoot -Arguments @("add", "-A")

        $environmentPath = Join-Path $forgeRoot "examples/visual_valley/environment.v1.json"
        $environmentBytes = [IO.File]::ReadAllBytes($environmentPath)
        [IO.File]::WriteAllText($environmentPath, "{`"world`":`"Whisperwood`"}`n", [Text.UTF8Encoding]::new($false))
        Assert-ExpectedFailure -Case "upstream identity" -Pattern "Forbidden upstream identity" -Action { Invoke-RepositoryAudit -Root $forgeRoot }
        [IO.File]::WriteAllBytes($environmentPath, $environmentBytes)

        $cargoPath = Join-Path $forgeRoot "Cargo.toml"
        $cargoBytes = [IO.File]::ReadAllBytes($cargoPath)
        [IO.File]::AppendAllText($cargoPath, "cadle = { path = `"upstream/cadle`" }`n", [Text.UTF8Encoding]::new($false))
        Assert-ExpectedFailure -Case "Cadle dependency" -Pattern "Forbidden product dependency" -Action { Invoke-RepositoryAudit -Root $forgeRoot }
        [IO.File]::WriteAllBytes($cargoPath, $cargoBytes)

        $referenceContent = "// synthetic independent reference line whose normalized length exceeds forty characters`n"
        $referenceSourceDirectory = Join-Path $cadleRoot "src"
        $null = New-Item -ItemType Directory -Path $referenceSourceDirectory -Force
        [IO.File]::WriteAllText((Join-Path $referenceSourceDirectory "World.js"), $referenceContent, [Text.UTF8Encoding]::new($false))
        $null = Invoke-CapturedCommand -FilePath "git" -ArgumentList @("init", "--quiet", "--initial-branch=main", $cadleRoot)
        $null = Get-GitResult -Root $cadleRoot -Arguments @("add", "-A")
        $null = Get-GitResult -Root $cadleRoot -Arguments @(
            "-c", "user.name=Forge Gate 1 Test",
            "-c", "user.email=gate1-test@invalid.example",
            "commit", "--quiet", "-m", "Synthetic pinned reference"
        )
        $referenceCommit = ((Get-GitResult -Root $cadleRoot -Arguments @("rev-parse", "HEAD")).Output -join "").Trim()
        $referenceTree = ((Get-GitResult -Root $cadleRoot -Arguments @("rev-parse", "HEAD^{tree}")).Output -join "").Trim()
        $upstreamDirectory = Join-Path $forgeRoot "upstream"
        $null = New-Item -ItemType Directory -Path $upstreamDirectory -Force
        $syntheticLock = [ordered]@{
            schemaVersion = 2
            repository = [ordered]@{
                url = "https://example.invalid/reference.git"
                branch = "main"
                commit = $referenceCommit
                tree = $referenceTree
            }
        }
        [IO.File]::WriteAllText(
            (Join-Path $upstreamDirectory "cadle.lock.json"),
            ($syntheticLock | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
            [Text.UTF8Encoding]::new($false)
        )
        $null = Get-GitResult -Root $forgeRoot -Arguments @("add", "-A")
        $referencePositive = Invoke-RepositoryAudit -Root $forgeRoot -CadleRoot $cadleRoot
        if ($referencePositive.cadle_reference_comparison.status -ne "PASS" -or $referencePositive.cadle_reference_comparison.files_hashed -ne 1) {
            throw "Pinned-reference positive self-test returned an unexpected ledger."
        }

        [IO.File]::WriteAllText($environmentPath, $referenceContent, [Text.UTF8Encoding]::new($false))
        Assert-ExpectedFailure -Case "exact pinned-reference copy" -Pattern "Exact file copy matches pinned Cadle|Normalized line copy matches pinned Cadle" -Action {
            Invoke-RepositoryAudit -Root $forgeRoot -CadleRoot $cadleRoot
        }
        [IO.File]::WriteAllBytes($environmentPath, $environmentBytes)

        Write-Host "FORGE_GATE1_PROVENANCE_SELF_TEST_PASS cases=7"
    }
    finally {
        Remove-ProvenanceTempDirectory -Path $tempRoot
    }
}

function Invoke-CargoQualification {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][ValidateSet("None", "Targeted", "Workspace")][string]$Level,
        [string]$TargetDirectory
    )

    if ($Level -eq "None") {
        return [pscustomobject][ordered]@{ level = "None"; command = $null; result = "SKIPPED" }
    }
    $arguments = if ($Level -eq "Targeted") {
        @("check", "--locked", "-j", "1", "-p", "forge_core", "-p", "forge_project", "-p", "forge_world")
    }
    else {
        @("check", "--workspace", "--all-targets", "--all-features", "--locked", "-j", "1")
    }

    $previousTarget = [Environment]::GetEnvironmentVariable("CARGO_TARGET_DIR", "Process")
    try {
        if ($TargetDirectory) {
            [Environment]::SetEnvironmentVariable("CARGO_TARGET_DIR", [IO.Path]::GetFullPath($TargetDirectory), "Process")
        }
        $null = Invoke-CapturedCommand -FilePath "cargo" -ArgumentList $arguments -WorkingDirectory $Root
    }
    finally {
        [Environment]::SetEnvironmentVariable("CARGO_TARGET_DIR", $previousTarget, "Process")
    }

    return [pscustomobject][ordered]@{
        level = $Level
        command = "cargo $($arguments -join ' ')"
        result = "PASS"
    }
}

function Invoke-IsolatedCheck {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][ValidateSet("None", "Targeted", "Workspace")][string]$CargoLevel,
        [string]$TargetDirectory,
        [string]$CadleRoot,
        [switch]$RequireAssets,
        [string]$LedgerPath
    )

    $normalizedRoot = Get-NormalizedRoot -Path $Root
    $sourceAudit = Invoke-RepositoryAudit -Root $normalizedRoot -CadleRoot $CadleRoot -RequireClean -RequireAssets:$RequireAssets
    if ($null -eq $sourceAudit.repository.head) {
        throw "IsolatedCheck requires a committed source HEAD."
    }

    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("forge-gate1-provenance-isolated-" + [Guid]::NewGuid().ToString("N"))
    $checkout = Join-Path $tempRoot "checkout"
    $archive = Join-Path $tempRoot "source.tar"
    $null = New-Item -ItemType Directory -Path $checkout -Force
    try {
        $null = Get-GitResult -Root $normalizedRoot -Arguments @("archive", "--format=tar", "--output=$archive", "HEAD")
        $null = Invoke-CapturedCommand -FilePath "tar" -ArgumentList @("-xf", $archive, "-C", $checkout)
        Remove-Item -LiteralPath $archive -Force

        $null = Invoke-CapturedCommand -FilePath "git" -ArgumentList @("init", "--quiet", "--initial-branch=proof", $checkout)
        $null = Get-GitResult -Root $checkout -Arguments @("add", "-A")
        $null = Get-GitResult -Root $checkout -Arguments @(
            "-c", "user.name=Forge Gate 1 Proof",
            "-c", "user.email=gate1-proof@invalid.example",
            "commit", "--quiet", "-m", "Gate 1 isolated proof"
        )

        $remotes = (Get-GitResult -Root $checkout -Arguments @("remote", "-v")).Output
        if (@($remotes).Count -ne 0) {
            throw "Isolated checkout unexpectedly contains a Git remote: $($remotes -join '; ')"
        }
        $refs = (Get-GitResult -Root $checkout -Arguments @("show-ref")).Output
        if (($refs -join "`n") -match "(?i)cadle") {
            throw "Isolated checkout unexpectedly contains a Cadle ref."
        }
        if (Test-Path -LiteralPath (Join-Path $checkout "upstream/cadle")) {
            throw "Isolated checkout unexpectedly contains upstream/cadle."
        }

        $isolatedAudit = Invoke-RepositoryAudit -Root $checkout -RequireClean -RequireAssets:$RequireAssets
        $cargoResult = Invoke-CargoQualification -Root $checkout -Level $CargoLevel -TargetDirectory $TargetDirectory
        $result = [pscustomobject][ordered]@{
            schema = "forge.gate1-isolated-independence-check"
            schema_version = 1
            generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
            source = [pscustomobject][ordered]@{
                commit = $sourceAudit.repository.head
                tree = $sourceAudit.repository.tree
            }
            pinned_cadle_comparison = $sourceAudit.cadle_reference_comparison
            isolation = [pscustomobject][ordered]@{
                remotes = 0
                cadle_refs = 0
                upstream_cadle_directory = $false
                clean_tree = $true
            }
            audit = $isolatedAudit
            cargo = $cargoResult
            result = "PASS"
        }
        Write-AuditResult -Result $result -Path $LedgerPath -Marker "FORGE_GATE1_ISOLATED_CHECK_PASS cargo=$CargoLevel remotes=0 cadle_refs=0"
    }
    finally {
        Remove-ProvenanceTempDirectory -Path $tempRoot
    }
}

switch ($Mode) {
    "Audit" {
        $audit = Invoke-RepositoryAudit -Root $RepositoryRoot -CadleRoot $CadleReferenceRoot -RequireClean:$RequireCleanTree -RequireAssets:$RequireAssetManifest
        Write-AuditResult -Result $audit -Path $OutputPath -Marker "FORGE_GATE1_PROVENANCE_AUDIT_PASS manifests=$($audit.assets.manifests) files=$($audit.assets.files) models=$($audit.assets.models)"
    }
    "SelfTest" {
        Invoke-SelfTest
    }
    "IsolatedCheck" {
        Invoke-IsolatedCheck -Root $RepositoryRoot -CargoLevel $CargoCheck -TargetDirectory $CargoTargetDirectory -CadleRoot $CadleReferenceRoot -RequireAssets:$RequireAssetManifest -LedgerPath $OutputPath
    }
}
