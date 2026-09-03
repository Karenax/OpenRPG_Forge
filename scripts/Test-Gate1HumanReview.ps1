[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$PackagePath,

    [string]$ReviewPath,

    [string]$ForgeExternalRoot,

    [string]$GrassworksExternalRoot,

    [string]$CadleReferenceRoot,

    [switch]$VerifyExternal,

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:ExpectedPackageSha256 = "1d996c54af7652997357c64dd726a304c830d8b6ec8a05913e917ca26cfec725"

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [Management.Automation.PSCustomObject]) -Message "$Label must be a JSON object."
    $actual = @($Value.PSObject.Properties.Name)
    $missing = @($Expected | Where-Object { $_ -cnotin $actual })
    $extra = @($actual | Where-Object { $_ -cnotin $Expected })
    Assert-Condition -Condition ($missing.Count -eq 0 -and $extra.Count -eq 0 -and $actual.Count -eq $Expected.Count) -Message (
        "{0} properties differ: missing=[{1}] extra=[{2}]." -f $Label, ($missing -join ","), ($extra -join ",")
    )
}

function Assert-NonEmptyString {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [string] -and -not [string]::IsNullOrWhiteSpace([string]$Value)) -Message "$Label must be a non-empty string."
}

function Assert-JsonArray {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [Array]) -Message "$Label must be a JSON array."
}

function Assert-ExactStringArray {
    param(
        [AllowNull()][AllowEmptyCollection()][object]$Actual,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-JsonArray -Value $Actual -Label $Label
    $actualItems = @($Actual)
    Assert-Condition -Condition ($actualItems.Count -eq $Expected.Count) -Message "$Label item count differs."
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        Assert-NonEmptyString -Value $actualItems[$index] -Label "$Label[$index]"
        Assert-Condition -Condition ([string]$actualItems[$index] -ceq $Expected[$index]) -Message "$Label differs at index $index."
    }
}

function Assert-HexDigest {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][ValidateSet(40, 64)][int]$Length,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [string] -and ([string]$Value -cmatch "^[0-9a-f]{$Length}$")) -Message "$Label must be a lowercase $Length-character hexadecimal digest."
}

function Test-PortableRelativePath {
    param([AllowNull()][object]$Value)

    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $false
    }
    $path = [string]$Value
    if ([IO.Path]::IsPathRooted($path) -or $path.Contains("\") -or $path.Contains(":") -or $path.StartsWith("/") -or $path.EndsWith("/")) {
        return $false
    }
    foreach ($component in $path.Split("/")) {
        if ([string]::IsNullOrWhiteSpace($component) -or $component -in @(".", "..")) {
            return $false
        }
    }
    return $true
}

function Resolve-SafeChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootFull = [IO.Path]::GetFullPath($Root)
    $rootPrefix = $rootFull.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $nativeRelative = $RelativePath.Replace("/", [IO.Path]::DirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootFull $nativeRelative))
    Assert-Condition -Condition $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -Message "$Label escapes its declared root."
    return $candidate
}

function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Get-GitText {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $output = @(& git -C $Repository @Arguments 2>$null | ForEach-Object { "$_" })
    $exitCode = $LASTEXITCODE
    Assert-Condition -Condition ($exitCode -eq 0) -Message "$Label Git command failed."
    return ($output -join "`n").Trim()
}

function Assert-DisjointRoots {
    param(
        [Parameter(Mandatory = $true)][string]$First,
        [Parameter(Mandatory = $true)][string]$Second,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $firstFull = [IO.Path]::GetFullPath($First).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $secondFull = [IO.Path]::GetFullPath($Second).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $firstPrefix = $firstFull + [IO.Path]::DirectorySeparatorChar
    $secondPrefix = $secondFull + [IO.Path]::DirectorySeparatorChar
    $overlap = $firstFull.Equals($secondFull, [StringComparison]::OrdinalIgnoreCase) -or
        $firstFull.StartsWith($secondPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $secondFull.StartsWith($firstPrefix, [StringComparison]::OrdinalIgnoreCase)
    Assert-Condition -Condition (-not $overlap) -Message "$Label roots must be disjoint."
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        Assert-Condition -Condition (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "$Label traverses reparse point '$($item.FullName)'."
        $item = if ($item -is [IO.FileInfo]) { $item.Directory } else { $item.Parent }
    }
}

function Assert-JsonElementHasNoDuplicateProperties {
    param(
        [Parameter(Mandatory = $true)][Text.Json.JsonElement]$Element,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            Assert-Condition -Condition $seen.Add($property.Name) -Message "Duplicate JSON property '$($property.Name)' at $Path."
            Assert-JsonElementHasNoDuplicateProperties -Element $property.Value -Path "$Path.$($property.Name)"
        }
    }
    elseif ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-JsonElementHasNoDuplicateProperties -Element $item -Path "$Path[$index]"
            $index++
        }
    }
}

function ConvertFrom-StrictJson {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $document = [Text.Json.JsonDocument]::Parse($Text)
    try {
        Assert-JsonElementHasNoDuplicateProperties -Element $document.RootElement -Path '$'
    }
    finally {
        $document.Dispose()
    }
    try {
        return ($Text | ConvertFrom-Json -Depth 64 -DateKind String)
    }
    catch {
        throw "$Label is not valid JSON: $($_.Exception.Message)"
    }
}

function Assert-GitObjectAndAncestry {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Commit,
        [Parameter(Mandatory = $true)][string]$Label
    )

    & git -C $Repository rev-parse --verify "$Commit`^{commit}" 2>$null | Out-Null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "$Label commit does not exist in this repository: $Commit"
    & git -C $Repository merge-base --is-ancestor $Commit HEAD 2>$null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "$Label commit is not an ancestor of HEAD: $Commit"
}

function Assert-Package {
    param(
        [Parameter(Mandatory = $true)][object]$Package,
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][bool]$CheckExternal,
        [AllowEmptyString()][string]$ForgeRoot,
        [AllowEmptyString()][string]$GrassworksRoot,
        [AllowEmptyString()][string]$CadleRoot
    )

    Assert-ExactProperties -Value $Package -Expected @(
        "schema", "schema_version", "status", "scope", "prepared_from", "qualified_software_baseline",
        "evidence_sets",
        "gate1_transition_authorized", "cadle_policy", "roots", "artifacts", "routes",
        "excluded_technical_routes", "review_receipt_contract", "summary"
    ) -Label "package"
    Assert-Condition -Condition ($Package.schema -ceq "forge.gate1-human-review-package") -Message "Unexpected package schema."
    Assert-Condition -Condition ([int]$Package.schema_version -eq 1) -Message "Unexpected package schema version."
    Assert-Condition -Condition ($Package.status -ceq "ready_for_partial_human_review") -Message "Unexpected package status."
    Assert-Condition -Condition ($Package.scope -ceq "human_visual_review_only") -Message "Package scope must remain human_visual_review_only."
    Assert-Condition -Condition ($Package.gate1_transition_authorized -is [bool] -and -not $Package.gate1_transition_authorized) -Message "The package cannot authorize a Gate 1 transition."

    Assert-ExactProperties -Value $Package.prepared_from -Expected @("branch", "commit") -Label "package.prepared_from"
    Assert-Condition -Condition ($Package.prepared_from.branch -ceq "Rust") -Message "The package must be prepared from Rust."
    Assert-HexDigest -Value $Package.prepared_from.commit -Length 40 -Label "package.prepared_from.commit"
    Assert-Condition -Condition ($Package.prepared_from.commit -ceq "73db783975251cf7b4439dce9a12328a4c55c5ec") -Message "The package must bind the commit that introduced the R1-051 protocol."
    Assert-GitObjectAndAncestry -Repository $Repository -Commit $Package.prepared_from.commit -Label "Prepared-from"

    Assert-ExactProperties -Value $Package.qualified_software_baseline -Expected @("commit", "tree") -Label "package.qualified_software_baseline"
    Assert-HexDigest -Value $Package.qualified_software_baseline.commit -Length 40 -Label "package.qualified_software_baseline.commit"
    Assert-HexDigest -Value $Package.qualified_software_baseline.tree -Length 40 -Label "package.qualified_software_baseline.tree"
    Assert-GitObjectAndAncestry -Repository $Repository -Commit $Package.qualified_software_baseline.commit -Label "Qualified software baseline"
    $actualTree = (Get-GitText -Repository $Repository -Arguments @("rev-parse", "$($Package.qualified_software_baseline.commit)^{tree}") -Label "Qualified software baseline tree").ToLowerInvariant()
    Assert-Condition -Condition ($actualTree -ceq $Package.qualified_software_baseline.tree) -Message "Qualified software baseline tree mismatch."
    & git -C $Repository merge-base --is-ancestor $Package.qualified_software_baseline.commit $Package.prepared_from.commit 2>$null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Qualified software baseline must be an ancestor of prepared_from."
    $branch = Get-GitText -Repository $Repository -Arguments @("branch", "--show-current") -Label "Current branch"
    if ([string]::IsNullOrEmpty($branch)) {
        & git -C $Repository rev-parse --verify "refs/heads/Rust^{commit}" 2>$null | Out-Null
        Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Detached validation requires a local Rust branch."
        & git -C $Repository merge-base --is-ancestor $Package.prepared_from.commit refs/heads/Rust 2>$null
        Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Detached validation HEAD is not based on the Rust package snapshot."
        & git -C $Repository merge-base --is-ancestor HEAD refs/heads/Rust 2>$null
        $headIsBehindRust = $LASTEXITCODE -eq 0
        & git -C $Repository merge-base --is-ancestor refs/heads/Rust HEAD 2>$null
        $headIsAheadOfRust = $LASTEXITCODE -eq 0
        Assert-Condition -Condition ($headIsBehindRust -or $headIsAheadOfRust) -Message "Detached validation HEAD must be on the direct Rust lineage."
    }
    else {
        Assert-Condition -Condition ($branch -ceq "Rust") -Message "Human-review package validation must run on branch Rust or on a detached Rust worktree."
    }

    Assert-ExactProperties -Value $Package.cadle_policy -Expected @("assets_embedded", "captures_tracked", "review_in_place_only", "redistribution_authorized") -Label "package.cadle_policy"
    Assert-Condition -Condition ($Package.cadle_policy.assets_embedded -is [bool] -and -not $Package.cadle_policy.assets_embedded) -Message "Cadle assets must not be embedded."
    Assert-Condition -Condition ($Package.cadle_policy.captures_tracked -is [bool] -and -not $Package.cadle_policy.captures_tracked) -Message "Cadle captures must not be tracked."
    Assert-Condition -Condition ($Package.cadle_policy.review_in_place_only -is [bool] -and $Package.cadle_policy.review_in_place_only) -Message "Cadle review must remain in place."
    Assert-Condition -Condition ($Package.cadle_policy.redistribution_authorized -is [bool] -and -not $Package.cadle_policy.redistribution_authorized) -Message "Cadle redistribution must remain unauthorized."

    $expectedRoots = @{
        repository = @("tracked_forge_evidence", "RepositoryRoot", $true, $true)
        forge_external_water_125ba0b = @("local_forge_evidence", "ForgeExternalRoot", $false, $true)
        grassworks_external_current_20260903 = @("external_reference_observation", "GrassworksExternalRoot", $false, $false)
        cadle_gate_c_overlay = @("local_cadle_reference", "CadleReferenceRoot", $false, $false)
    }
    $rootMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($root in @($Package.roots)) {
        Assert-ExactProperties -Value $root -Expected @("id", "kind", "required_parameter", "tracked_in_forge", "redistributable") -Label "package.roots[]"
        Assert-NonEmptyString -Value $root.id -Label "root.id"
        Assert-Condition -Condition $expectedRoots.ContainsKey($root.id) -Message "Unknown evidence root: $($root.id)"
        Assert-Condition -Condition $rootMap.TryAdd($root.id, $root) -Message "Duplicate evidence root: $($root.id)"
        $expected = $expectedRoots[$root.id]
        Assert-Condition -Condition ($root.kind -ceq $expected[0]) -Message "Root kind mismatch for $($root.id)."
        Assert-Condition -Condition ($root.required_parameter -ceq $expected[1]) -Message "Root parameter mismatch for $($root.id)."
        Assert-Condition -Condition ($root.tracked_in_forge -is [bool] -and $root.tracked_in_forge -eq $expected[2]) -Message "tracked_in_forge mismatch for $($root.id)."
        Assert-Condition -Condition ($root.redistributable -is [bool] -and $root.redistributable -eq $expected[3]) -Message "redistributable mismatch for $($root.id)."
    }
    Assert-Condition -Condition ($rootMap.Count -eq $expectedRoots.Count) -Message "The package must declare exactly four evidence roots."

    if ($CheckExternal) {
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($ForgeRoot)) -Message "-ForgeExternalRoot is required with -VerifyExternal."
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($GrassworksRoot)) -Message "-GrassworksExternalRoot is required with -VerifyExternal."
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($CadleRoot)) -Message "-CadleReferenceRoot is required with -VerifyExternal."
        Assert-Condition -Condition (Test-Path -LiteralPath $ForgeRoot -PathType Container) -Message "Forge external evidence root is missing: $ForgeRoot"
        Assert-Condition -Condition (Test-Path -LiteralPath $GrassworksRoot -PathType Container) -Message "Grassworks external observation root is missing: $GrassworksRoot"
        Assert-Condition -Condition (Test-Path -LiteralPath $CadleRoot -PathType Container) -Message "Cadle reference root is missing: $CadleRoot"
        Assert-DisjointRoots -First $Repository -Second $ForgeRoot -Label "Repository/Forge external evidence"
        Assert-DisjointRoots -First $Repository -Second $GrassworksRoot -Label "Repository/Grassworks external observation"
        Assert-DisjointRoots -First $Repository -Second $CadleRoot -Label "Repository/Cadle reference"
        Assert-DisjointRoots -First $ForgeRoot -Second $GrassworksRoot -Label "Forge external/Grassworks external"
        Assert-DisjointRoots -First $ForgeRoot -Second $CadleRoot -Label "Forge external/Cadle reference"
        Assert-DisjointRoots -First $GrassworksRoot -Second $CadleRoot -Label "Grassworks external/Cadle reference"
        Assert-NotReparsePoint -Path $ForgeRoot -Label "Forge external evidence root"
        Assert-NotReparsePoint -Path $GrassworksRoot -Label "Grassworks external observation root"
        Assert-NotReparsePoint -Path $CadleRoot -Label "Cadle reference root"
    }

    $requiredArtifactIds = @(
        "browser_temporal_frame_a", "browser_temporal_frame_b", "browser_temporal_ledger",
        "quality_low_clear", "quality_high_clear", "quality_low_clear_metrics", "quality_high_clear_metrics",
        "cadle_baseline_metadata", "native_human_clear", "native_distant_clear", "native_water_clear",
        "native_water_rain", "native_water_restored", "native_movement_video", "native_visual_ledger",
        "native_visual_validation", "native_video_probe", "cadle_vale_ground", "cadle_forest_ground",
        "cadle_water_golden_shore", "cadle_forge_ab_protocol_json", "cadle_forge_ab_review_guide",
        "grassworks_semantic_protocol_json", "grassworks_semantic_review_guide",
        "grassworks_high_sunny_current", "grassworks_high_rain_current", "grassworks_historical_post_current_render"
    )
    $artifactMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $rootCounts = @{
        repository = 0
        forge_external_water_125ba0b = 0
        grassworks_external_current_20260903 = 0
        cadle_gate_c_overlay = 0
    }
    foreach ($artifact in @($Package.artifacts)) {
        Assert-ExactProperties -Value $artifact -Expected @("id", "root", "path", "bytes", "sha256", "media_type", "redistribution", "notes") -Label "package.artifacts[]"
        Assert-NonEmptyString -Value $artifact.id -Label "artifact.id"
        Assert-Condition -Condition $artifactMap.TryAdd($artifact.id, $artifact) -Message "Duplicate artifact id: $($artifact.id)"
        Assert-Condition -Condition $rootMap.ContainsKey($artifact.root) -Message "Unknown artifact root for $($artifact.id): $($artifact.root)"
        Assert-Condition -Condition (Test-PortableRelativePath -Value $artifact.path) -Message "Artifact path is not portable for $($artifact.id): $($artifact.path)"
        Assert-Condition -Condition ($artifact.bytes -is [long] -or $artifact.bytes -is [int]) -Message "Artifact byte count must be an integer for $($artifact.id)."
        Assert-Condition -Condition ([long]$artifact.bytes -gt 0) -Message "Artifact byte count must be positive for $($artifact.id)."
        Assert-HexDigest -Value $artifact.sha256 -Length 64 -Label "artifact[$($artifact.id)].sha256"
        Assert-Condition -Condition (@("image/png", "image/jpeg", "video/mp4", "application/json", "text/markdown") -ccontains [string]$artifact.media_type) -Message "Unsupported media type for $($artifact.id)."
        Assert-NonEmptyString -Value $artifact.notes -Label "artifact[$($artifact.id)].notes"
        $rootCounts[$artifact.root]++

        if ($artifact.root -ceq "repository") {
            Assert-Condition -Condition (@("tracked_forge_evidence", "tracked_metadata_only") -ccontains [string]$artifact.redistribution) -Message "Tracked artifact redistribution label is invalid for $($artifact.id)."
            $filePath = Resolve-SafeChildPath -Root $Repository -RelativePath $artifact.path -Label "artifact[$($artifact.id)]"
            Assert-Condition -Condition (Test-Path -LiteralPath $filePath -PathType Leaf) -Message "Tracked artifact is missing: $($artifact.path)"
            & git -C $Repository ls-files --error-unmatch -- $artifact.path 2>$null | Out-Null
            Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Repository artifact is not tracked: $($artifact.path)"
            $file = Get-Item -LiteralPath $filePath
            Assert-Condition -Condition ($file.Length -eq [long]$artifact.bytes) -Message "Tracked artifact byte count mismatch: $($artifact.id)"
            Assert-Condition -Condition ((Get-LowerSha256 -Path $filePath) -ceq $artifact.sha256) -Message "Tracked artifact hash mismatch: $($artifact.id)"
        }
        elseif ($artifact.root -ceq "forge_external_water_125ba0b") {
            Assert-Condition -Condition ($artifact.redistribution -ceq "local_forge_evidence_not_embedded") -Message "Forge external artifact label is invalid for $($artifact.id)."
            if ($CheckExternal) {
                $filePath = Resolve-SafeChildPath -Root $ForgeRoot -RelativePath $artifact.path -Label "artifact[$($artifact.id)]"
                Assert-Condition -Condition (Test-Path -LiteralPath $filePath -PathType Leaf) -Message "Forge external artifact is missing: $($artifact.path)"
                Assert-NotReparsePoint -Path $filePath -Label "Forge external artifact $($artifact.id)"
                $file = Get-Item -LiteralPath $filePath
                Assert-Condition -Condition ($file.Length -eq [long]$artifact.bytes) -Message "Forge external artifact byte count mismatch: $($artifact.id)"
                Assert-Condition -Condition ((Get-LowerSha256 -Path $filePath) -ceq $artifact.sha256) -Message "Forge external artifact hash mismatch: $($artifact.id)"
            }
        }
        elseif ($artifact.root -ceq "grassworks_external_current_20260903") {
            Assert-Condition -Condition ($artifact.redistribution -ceq "local_only_grassworks_observation_not_embedded") -Message "Grassworks external artifact label is invalid for $($artifact.id)."
            Assert-Condition -Condition ($artifact.media_type -ceq "image/jpeg") -Message "Grassworks observations in this package must be JPEG images only."
            if ($CheckExternal) {
                $filePath = Resolve-SafeChildPath -Root $GrassworksRoot -RelativePath $artifact.path -Label "artifact[$($artifact.id)]"
                Assert-Condition -Condition (Test-Path -LiteralPath $filePath -PathType Leaf) -Message "Grassworks external observation is missing: $($artifact.path)"
                Assert-NotReparsePoint -Path $filePath -Label "Grassworks external observation $($artifact.id)"
                $file = Get-Item -LiteralPath $filePath
                Assert-Condition -Condition ($file.Length -eq [long]$artifact.bytes) -Message "Grassworks external artifact byte count mismatch: $($artifact.id)"
                Assert-Condition -Condition ((Get-LowerSha256 -Path $filePath) -ceq $artifact.sha256) -Message "Grassworks external artifact hash mismatch: $($artifact.id)"
            }
        }
        else {
            Assert-Condition -Condition ($artifact.redistribution -ceq "local_only_excluded_cadle_media") -Message "Cadle artifact label is invalid for $($artifact.id)."
            Assert-Condition -Condition ($artifact.media_type -ceq "image/png") -Message "Cadle references in this package must be PNG images only."
            if ($CheckExternal) {
                $filePath = Resolve-SafeChildPath -Root $CadleRoot -RelativePath $artifact.path -Label "artifact[$($artifact.id)]"
                Assert-Condition -Condition (Test-Path -LiteralPath $filePath -PathType Leaf) -Message "Cadle local reference is missing: $($artifact.path)"
                Assert-NotReparsePoint -Path $filePath -Label "Cadle reference $($artifact.id)"
                $file = Get-Item -LiteralPath $filePath
                Assert-Condition -Condition ($file.Length -eq [long]$artifact.bytes) -Message "Cadle reference byte count mismatch: $($artifact.id)"
                Assert-Condition -Condition ((Get-LowerSha256 -Path $filePath) -ceq $artifact.sha256) -Message "Cadle reference hash mismatch: $($artifact.id)"
            }
        }
    }
    Assert-Condition -Condition ($artifactMap.Count -eq 27) -Message "The package must contain exactly 27 artifact references."
    foreach ($id in $requiredArtifactIds) {
        Assert-Condition -Condition $artifactMap.ContainsKey($id) -Message "Required artifact is missing: $id"
    }
    Assert-Condition -Condition ($rootCounts.repository -eq 12) -Message "The package must contain exactly twelve tracked Forge artifacts."
    Assert-Condition -Condition ($rootCounts.forge_external_water_125ba0b -eq 9) -Message "The package must contain exactly nine local Forge artifacts."
    Assert-Condition -Condition ($rootCounts.grassworks_external_current_20260903 -eq 3) -Message "The package must contain exactly three in-place Grassworks observations."
    Assert-Condition -Condition ($rootCounts.cadle_gate_c_overlay -eq 3) -Message "The package must contain exactly three in-place Cadle references."

    $expectedEvidenceSets = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $expectedEvidenceSets.Add("browser_temporal_4e476b31", [pscustomobject]@{
        System = "forge"
        Commit = "4e476b31ee33533b6f4023804d8ecd1be02f9c55"
        Tree = "cc6efae0289b10dcf12f97e6bb4d97b3ead29f90"
        ArtifactIds = @("browser_temporal_frame_a", "browser_temporal_frame_b", "browser_temporal_ledger")
    })
    $expectedEvidenceSets.Add("quality_matrix_5eadec7f", [pscustomobject]@{
        System = "forge"
        Commit = "5eadec7f6f9f9b5b6734ce62ea3518f0297496f9"
        Tree = "a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1"
        ArtifactIds = @("quality_low_clear", "quality_high_clear", "quality_low_clear_metrics", "quality_high_clear_metrics")
    })
    $expectedEvidenceSets.Add("native_water_125ba0b", [pscustomobject]@{
        System = "forge"
        Commit = "125ba0b8bb1fe04b2b34cae5abc41c26d1919b00"
        Tree = "6d4639c824ccfa81c3d5ed911903cec2bb3c5074"
        ArtifactIds = @(
            "native_human_clear", "native_distant_clear", "native_water_clear", "native_water_rain",
            "native_water_restored", "native_movement_video", "native_visual_ledger",
            "native_visual_validation", "native_video_probe"
        )
    })
    $expectedEvidenceSets.Add("cadle_snapshot_ea0da625", [pscustomobject]@{
        System = "cadle"
        Commit = "ea0da62549980816a6a27e08b75808b1f56d70e7"
        Tree = "f0d52e434454fe302052d22eafde203bc361cc1f"
        ArtifactIds = @("cadle_baseline_metadata", "cadle_vale_ground", "cadle_forest_ground", "cadle_water_golden_shore")
    })
    $expectedEvidenceSets.Add("cadle_forge_ab_protocol_1bd5292", [pscustomobject]@{
        System = "forge"
        Commit = "1bd52926405c5a0a2ae8e4c1f257507b8cb1b4af"
        Tree = "d0135ec9a4d19a15378c07adcc3763c9b223b432"
        ArtifactIds = @("cadle_forge_ab_protocol_json", "cadle_forge_ab_review_guide")
    })
    $expectedEvidenceSets.Add("grassworks_forge_semantic_protocol_73db783", [pscustomobject]@{
        System = "forge"
        Commit = "73db783975251cf7b4439dce9a12328a4c55c5ec"
        Tree = "466cb51b12203e2358aec7ebcc9051eb58a1234e"
        ArtifactIds = @(
            "grassworks_semantic_protocol_json", "grassworks_semantic_review_guide",
            "grassworks_high_sunny_current", "grassworks_high_rain_current", "grassworks_historical_post_current_render"
        )
    })
    $evidenceSetMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $artifactEvidenceSetMap = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($evidenceSet in @($Package.evidence_sets)) {
        Assert-ExactProperties -Value $evidenceSet -Expected @(
            "id", "system", "commit", "tree", "exact_to_qualified_software_baseline", "artifact_ids"
        ) -Label "package.evidence_sets[]"
        Assert-Condition -Condition $expectedEvidenceSets.ContainsKey($evidenceSet.id) -Message "Unknown evidence set: $($evidenceSet.id)"
        Assert-Condition -Condition $evidenceSetMap.TryAdd($evidenceSet.id, $evidenceSet) -Message "Duplicate evidence set: $($evidenceSet.id)"
        $expectedSet = $expectedEvidenceSets[$evidenceSet.id]
        Assert-Condition -Condition ($evidenceSet.system -ceq $expectedSet.System) -Message "Evidence system mismatch for $($evidenceSet.id)."
        Assert-Condition -Condition ($evidenceSet.commit -ceq $expectedSet.Commit) -Message "Evidence commit mismatch for $($evidenceSet.id)."
        Assert-Condition -Condition ($evidenceSet.tree -ceq $expectedSet.Tree) -Message "Evidence tree mismatch for $($evidenceSet.id)."
        $expectedExactBaselineIdentity = $evidenceSet.id -ceq "browser_temporal_4e476b31"
        Assert-Condition -Condition (
            $evidenceSet.exact_to_qualified_software_baseline -is [bool] -and
            $evidenceSet.exact_to_qualified_software_baseline -eq $expectedExactBaselineIdentity
        ) -Message "Exact qualified-baseline identity mismatch for $($evidenceSet.id)."
        $evidenceArtifactIds = @($evidenceSet.artifact_ids)
        Assert-ExactStringArray -Actual $evidenceSet.artifact_ids -Expected $expectedSet.ArtifactIds -Label "evidence_set[$($evidenceSet.id)].artifact_ids"
        foreach ($artifactId in $evidenceArtifactIds) {
            Assert-Condition -Condition $artifactMap.ContainsKey($artifactId) -Message "Unknown artifact '$artifactId' in evidence set $($evidenceSet.id)."
            Assert-Condition -Condition $artifactEvidenceSetMap.TryAdd($artifactId, $evidenceSet.id) -Message "Artifact '$artifactId' belongs to more than one evidence set."
        }
        if ($evidenceSet.system -ceq "forge") {
            Assert-GitObjectAndAncestry -Repository $Repository -Commit $evidenceSet.commit -Label "Evidence set $($evidenceSet.id)"
            $evidenceTree = (Get-GitText -Repository $Repository -Arguments @("rev-parse", "$($evidenceSet.commit)^{tree}") -Label "Evidence set tree $($evidenceSet.id)").ToLowerInvariant()
            Assert-Condition -Condition ($evidenceTree -ceq $evidenceSet.tree) -Message "Repository tree mismatch for evidence set $($evidenceSet.id)."
        }
    }
    Assert-Condition -Condition ($evidenceSetMap.Count -eq 6) -Message "The package must declare exactly six evidence sets."
    Assert-Condition -Condition ($artifactEvidenceSetMap.Count -eq 27) -Message "Every artifact must belong to exactly one evidence set."

    $exactBaselineVisualArtifactCount = 0
    foreach ($evidenceSet in @($Package.evidence_sets)) {
        if ($evidenceSet.exact_to_qualified_software_baseline) {
            foreach ($artifactId in @($evidenceSet.artifact_ids)) {
                if (@("image/png", "video/mp4") -ccontains [string]$artifactMap[$artifactId].media_type) {
                    $exactBaselineVisualArtifactCount++
                }
            }
        }
    }
    Assert-Condition -Condition ($exactBaselineVisualArtifactCount -eq 2) -Message "Exactly two visual artifacts must match the qualified software baseline."

    foreach ($artifactId in $requiredArtifactIds) {
        $artifact = $artifactMap[$artifactId]
        if ($artifact.root -ceq "repository") {
            & git -C $Repository diff --quiet --no-ext-diff $Package.prepared_from.commit -- $artifact.path 2>$null
            Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Tracked artifact differs from the package preparation snapshot: $artifactId"
        }
    }

    $cadleLockRelative = "upstream/cadle.lock.json"
    $cadleLockPath = Join-Path $Repository $cadleLockRelative
    Assert-Condition -Condition (Test-Path -LiteralPath $cadleLockPath -PathType Leaf) -Message "Pinned Cadle lock is missing."
    & git -C $Repository ls-files --error-unmatch -- $cadleLockRelative 2>$null | Out-Null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Pinned Cadle lock is not tracked."
    & git -C $Repository diff --quiet --no-ext-diff $Package.prepared_from.commit -- $cadleLockRelative 2>$null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Pinned Cadle lock differs from the package preparation snapshot."
    Assert-Condition -Condition ((Get-LowerSha256 -Path $cadleLockPath) -ceq "6e0608b93da299285304996a7d5769de2dce1581ed86f3e86c0b51a40b546630") -Message "Pinned Cadle lock hash mismatch."
    $cadleLock = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $cadleLockPath) -Label "Pinned Cadle lock"
    $cadleEvidenceSet = $evidenceSetMap["cadle_snapshot_ea0da625"]
    Assert-Condition -Condition ($cadleLock.repository.commit -ceq $cadleEvidenceSet.commit) -Message "Cadle evidence commit differs from upstream/cadle.lock.json."
    Assert-Condition -Condition ($cadleLock.repository.tree -ceq $cadleEvidenceSet.tree) -Message "Cadle evidence tree differs from upstream/cadle.lock.json."

    $abProtocolArtifact = $artifactMap["cadle_forge_ab_protocol_json"]
    $abProtocolPath = Resolve-SafeChildPath -Root $Repository -RelativePath $abProtocolArtifact.path -Label "R1-052 A/B protocol"
    $abProtocol = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $abProtocolPath) -Label "R1-052 A/B protocol"
    Assert-ExactProperties -Value $abProtocol -Expected @(
        "schema", "schema_version", "status", "scope", "qualified_software_baseline", "evidence",
        "common_environment", "comparison_pairs", "review_axes", "cost_context", "review_contract",
        "cadle_media_policy", "qualification"
    ) -Label "R1-052 A/B protocol"
    Assert-Condition -Condition ($abProtocol.schema -ceq "forge.gate1-cadle-forge-ab-protocol") -Message "Unexpected R1-052 A/B protocol schema."
    Assert-Condition -Condition ([int]$abProtocol.schema_version -eq 1) -Message "Unexpected R1-052 A/B protocol schema version."
    Assert-Condition -Condition ($abProtocol.status -ceq "ready_for_human_review") -Message "R1-052 A/B protocol is not ready for human review."
    Assert-Condition -Condition ($abProtocol.scope -ceq "r1-052_visual_impact_and_cost_context") -Message "Unexpected R1-052 A/B protocol scope."
    Assert-Condition -Condition ($abProtocol.qualified_software_baseline.commit -ceq $Package.qualified_software_baseline.commit) -Message "R1-052 protocol baseline commit mismatch."
    Assert-Condition -Condition ($abProtocol.qualified_software_baseline.tree -ceq $Package.qualified_software_baseline.tree) -Message "R1-052 protocol baseline tree mismatch."

    $nativeEvidenceSet = $evidenceSetMap["native_water_125ba0b"]
    Assert-Condition -Condition ($abProtocol.evidence.forge.evidence_set_id -ceq $nativeEvidenceSet.id) -Message "R1-052 Forge evidence-set id mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.forge.commit -ceq $nativeEvidenceSet.commit) -Message "R1-052 Forge evidence commit mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.forge.tree -ceq $nativeEvidenceSet.tree) -Message "R1-052 Forge evidence tree mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.cadle.evidence_set_id -ceq $cadleEvidenceSet.id) -Message "R1-052 Cadle evidence-set id mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.cadle.commit -ceq $cadleEvidenceSet.commit) -Message "R1-052 Cadle evidence commit mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.cadle.tree -ceq $cadleEvidenceSet.tree) -Message "R1-052 Cadle evidence tree mismatch."
    Assert-Condition -Condition ((@($abProtocol.evidence.forge.capture_resolution_px) -join "x") -ceq "1920x1080") -Message "R1-052 Forge capture resolution mismatch."
    Assert-Condition -Condition ((@($abProtocol.evidence.cadle.capture_resolution_px) -join "x") -ceq "1920x1080") -Message "R1-052 Cadle capture resolution mismatch."
    Assert-Condition -Condition ($abProtocol.evidence.forge.quality -ceq "high" -and $abProtocol.evidence.cadle.quality -ceq "high") -Message "R1-052 evidence must use the High profile."
    Assert-Condition -Condition ($abProtocol.common_environment.same_physical_machine -is [bool] -and $abProtocol.common_environment.same_physical_machine) -Message "R1-052 protocol must bind one physical machine."
    Assert-Condition -Condition ((@($abProtocol.common_environment.resolution_px) -join "x") -ceq "1920x1080") -Message "R1-052 common resolution mismatch."
    Assert-Condition -Condition ($abProtocol.common_environment.quality -ceq "high") -Message "R1-052 common quality mismatch."
    Assert-ExactStringArray -Actual $abProtocol.review_axes -Expected @("terrain", "vegetation", "water", "atmosphere", "readability", "game_feel") -Label "R1-052 review_axes"

    $expectedPairs = @{
        "terrain_and_macro_composition" = @("native_distant_clear", "cadle_vale_ground")
        "vegetation_at_human_height" = @("native_human_clear", "cadle_forest_ground")
        "water_from_shore_context" = @("native_water_clear", "cadle_water_golden_shore")
    }
    $seenPairs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($pair in @($abProtocol.comparison_pairs)) {
        Assert-Condition -Condition ($expectedPairs.Keys -ccontains [string]$pair.id) -Message "Unknown R1-052 comparison pair: $($pair.id)"
        Assert-Condition -Condition $seenPairs.Add([string]$pair.id) -Message "Duplicate R1-052 comparison pair: $($pair.id)"
        Assert-Condition -Condition ($pair.forge_artifact_id -ceq $expectedPairs[$pair.id][0]) -Message "R1-052 Forge artifact mismatch for $($pair.id)."
        Assert-Condition -Condition ($pair.cadle_artifact_id -ceq $expectedPairs[$pair.id][1]) -Message "R1-052 Cadle artifact mismatch for $($pair.id)."
        Assert-Condition -Condition ($artifactMap.ContainsKey([string]$pair.forge_artifact_id) -and $artifactMap.ContainsKey([string]$pair.cadle_artifact_id)) -Message "R1-052 comparison pair references an unknown artifact."
        Assert-Condition -Condition ($pair.camera_equivalence -ceq "semantic_only_not_geometric") -Message "R1-052 comparison must remain semantic, not geometric."
        Assert-Condition -Condition ($pair.direct_pixel_comparison_allowed -is [bool] -and -not $pair.direct_pixel_comparison_allowed) -Message "R1-052 direct pixel comparison must remain forbidden."
    }
    Assert-Condition -Condition ($seenPairs.Count -eq 3) -Message "R1-052 protocol must declare exactly three comparison pairs."

    $reviewedRuntimePaths = @($abProtocol.evidence.forge.visual_delta_review.reviewed_runtime_paths)
    $patchIdOutput = @(
        & git -C $Repository diff "$($abProtocol.evidence.forge.visual_delta_review.range)" -- @reviewedRuntimePaths |
            & git -C $Repository patch-id --stable
    )
    Assert-Condition -Condition ($LASTEXITCODE -eq 0 -and $patchIdOutput.Count -eq 1) -Message "R1-052 stable patch-id recomputation failed."
    $actualPatchId = ([string]$patchIdOutput[0] -split "\s+")[0]
    Assert-Condition -Condition ($actualPatchId -ceq $abProtocol.evidence.forge.visual_delta_review.stable_patch_id) -Message "R1-052 stable patch-id mismatch."
    Assert-Condition -Condition ($abProtocol.cost_context.comparison_rule -ceq "context_only_no_raw_fps_duel") -Message "R1-052 raw FPS comparison must remain forbidden."
    Assert-Condition -Condition ($abProtocol.review_contract.human_review_required -is [bool] -and $abProtocol.review_contract.human_review_required) -Message "R1-052 must require human review."
    Assert-Condition -Condition ($abProtocol.review_contract.machine_protocol_closes_route -is [bool] -and -not $abProtocol.review_contract.machine_protocol_closes_route) -Message "R1-052 machine protocol cannot close the route."
    Assert-Condition -Condition ($abProtocol.cadle_media_policy.review_in_place_only -and -not $abProtocol.cadle_media_policy.copied_into_forge -and -not $abProtocol.cadle_media_policy.tracked_in_forge -and -not $abProtocol.cadle_media_policy.published) -Message "R1-052 Cadle media policy mismatch."
    Assert-Condition -Condition (-not $abProtocol.qualification.human_review_performed -and -not $abProtocol.qualification.r1_052_qualified -and -not $abProtocol.qualification.gate1_transition_authorized) -Message "R1-052 protocol must not claim human review, qualification or Gate 1 transition."

    $semanticProtocolArtifact = $artifactMap["grassworks_semantic_protocol_json"]
    Assert-Condition -Condition ($semanticProtocolArtifact.sha256 -ceq "cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d") -Message "R1-051 protocol package hash mismatch."
    $semanticProtocolPath = Resolve-SafeChildPath -Root $Repository -RelativePath $semanticProtocolArtifact.path -Label "R1-051 semantic protocol"
    $semanticProtocol = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $semanticProtocolPath) -Label "R1-051 semantic protocol"
    Assert-ExactProperties -Value $semanticProtocol -Expected @(
        "schema", "schema_version", "protocol_id", "status", "route", "prepared_from", "reference_identity",
        "roots", "artifacts", "comparison_panels", "context_bindings", "review_axes", "review_contract",
        "boundaries", "qualification"
    ) -Label "R1-051 semantic protocol"
    Assert-Condition -Condition ($semanticProtocol.schema -ceq "forge.gate1-r1-051-grassworks-semantic-protocol") -Message "Unexpected R1-051 semantic protocol schema."
    Assert-Condition -Condition ([int]$semanticProtocol.schema_version -eq 1) -Message "Unexpected R1-051 semantic protocol schema version."
    Assert-Condition -Condition ($semanticProtocol.protocol_id -ceq "r1-051-grassworks-forge-semantic-v1") -Message "Unexpected R1-051 semantic protocol id."
    Assert-Condition -Condition ($semanticProtocol.status -ceq "technically_ready_for_future_human_package_integration") -Message "R1-051 semantic protocol is not technically ready for package integration."
    Assert-Condition -Condition ($semanticProtocol.route -ceq "R1-051") -Message "R1-051 semantic protocol route mismatch."
    $semanticEvidenceSet = $evidenceSetMap["grassworks_forge_semantic_protocol_73db783"]
    Assert-Condition -Condition ($semanticEvidenceSet.commit -ceq "73db783975251cf7b4439dce9a12328a4c55c5ec") -Message "R1-051 semantic evidence commit mismatch."
    Assert-Condition -Condition ($semanticEvidenceSet.tree -ceq "466cb51b12203e2358aec7ebcc9051eb58a1234e") -Message "R1-051 semantic evidence tree mismatch."

    $semanticArtifactMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($artifact in @($semanticProtocol.artifacts)) {
        Assert-Condition -Condition $semanticArtifactMap.TryAdd($artifact.id, $artifact) -Message "Duplicate R1-051 protocol artifact: $($artifact.id)"
    }
    Assert-Condition -Condition ($semanticArtifactMap.Count -eq 7) -Message "R1-051 semantic protocol must bind exactly seven visual artifacts."
    $semanticArtifactBindings = @{
        grassworks_high_sunny_current = "grassworks_high_sunny_current"
        grassworks_high_rain_current = "grassworks_high_rain_current"
        grassworks_historical_post_current_render = "grassworks_historical_post_current_render"
        native_human_clear = "forge_human_clear_125ba0b"
        native_distant_clear = "forge_distant_clear_125ba0b"
        native_water_clear = "forge_water_clear_125ba0b"
        native_water_rain = "forge_water_rain_125ba0b"
    }
    foreach ($packageArtifactId in $semanticArtifactBindings.Keys) {
        $protocolArtifact = $semanticArtifactMap[$semanticArtifactBindings[$packageArtifactId]]
        $packageArtifact = $artifactMap[$packageArtifactId]
        Assert-Condition -Condition ($protocolArtifact.relative_path -ceq $packageArtifact.path) -Message "R1-051 protocol path mismatch for $packageArtifactId."
        Assert-Condition -Condition ([long]$protocolArtifact.bytes -eq [long]$packageArtifact.bytes) -Message "R1-051 protocol byte count mismatch for $packageArtifactId."
        Assert-Condition -Condition ($protocolArtifact.sha256 -ceq $packageArtifact.sha256) -Message "R1-051 protocol hash mismatch for $packageArtifactId."
        Assert-Condition -Condition ($protocolArtifact.media_type -ceq $packageArtifact.media_type) -Message "R1-051 protocol media type mismatch for $packageArtifactId."
    }
    $semanticAxisIds = @($semanticProtocol.review_axes | ForEach-Object { [string]$_.id })
    Assert-ExactStringArray -Actual $semanticAxisIds -Expected @("density", "naturalness", "depth", "lighting", "finish") -Label "R1-051 review axes"
    $semanticPanels = @($semanticProtocol.comparison_panels)
    Assert-Condition -Condition ($semanticPanels.Count -eq 4) -Message "R1-051 semantic protocol must define exactly four panels."
    foreach ($panel in $semanticPanels) {
        Assert-Condition -Condition ($panel.semantic_equivalence_only -is [bool] -and $panel.semantic_equivalence_only) -Message "R1-051 panels must remain semantic only."
        Assert-Condition -Condition ($panel.direct_pixel_comparison_allowed -is [bool] -and -not $panel.direct_pixel_comparison_allowed) -Message "R1-051 direct pixel comparison must remain forbidden."
        Assert-Condition -Condition ($panel.human_axis_verdict_required -is [bool] -and $panel.human_axis_verdict_required) -Message "R1-051 panels must require human axis verdicts."
    }
    $historicalContext = @($semanticProtocol.context_bindings | Where-Object { $_.id -ceq "historical_post_identity_only" })
    Assert-Condition -Condition ($historicalContext.Count -eq 1 -and -not $historicalContext[0].axis_verdict_eligible) -Message "R1-051 current historical-post render must remain identity-only."
    Assert-Condition -Condition (
        $semanticProtocol.boundaries.historical_post_rendered_at_protocol_capture_time -and
        -not $semanticProtocol.boundaries.contemporaneous_2026_08_29_capture_available -and
        -not $semanticProtocol.boundaries.source_video_bytes_available -and
        -not $semanticProtocol.boundaries.source_video_sha256_available -and
        $semanticProtocol.boundaries.live_demo_mutable -and
        -not $semanticProtocol.boundaries.public_version_or_commit_available -and
        -not $semanticProtocol.boundaries.direct_pixel_comparison_allowed -and
        -not $semanticProtocol.boundaries.grassworks_media_copied_into_forge -and
        -not $semanticProtocol.boundaries.grassworks_assets_copied_into_forge -and
        -not $semanticProtocol.boundaries.grassworks_code_or_bundles_retained -and
        -not $semanticProtocol.boundaries.cadle_assets_present -and
        -not $semanticProtocol.boundaries.human_review_performed
    ) -Message "R1-051 semantic source boundaries mismatch."
    Assert-Condition -Condition (
        $semanticProtocol.review_contract.reviewer_kind -ceq "human_owner" -and
        $semanticProtocol.review_contract.observation_per_axis_required -and
        $semanticProtocol.review_contract.external_hash_verification_required -and
        $semanticProtocol.review_contract.source_limitations_acknowledgement_required -and
        -not $semanticProtocol.review_contract.protocol_may_close_r1_051 -and
        -not $semanticProtocol.review_contract.protocol_may_authorize_gate1_transition -and
        $null -eq $semanticProtocol.review_contract.current_human_verdict
    ) -Message "R1-051 human review contract mismatch."
    Assert-Condition -Condition (
        $semanticProtocol.qualification.machine_protocol_defined -and
        $semanticProtocol.qualification.external_artifact_hashes_required -and
        $semanticProtocol.qualification.technically_ready_for_future_human_package_integration -and
        -not $semanticProtocol.qualification.reference_snapshot_reproducible -and
        -not $semanticProtocol.qualification.human_review_performed -and
        -not $semanticProtocol.qualification.r1_051_closed -and
        -not $semanticProtocol.qualification.gate1_transition_authorized
    ) -Message "R1-051 semantic protocol must not claim a human verdict, route closure or Gate 1 transition."

    $browserLedgerArtifact = $artifactMap["browser_temporal_ledger"]
    $browserLedgerPath = Resolve-SafeChildPath -Root $Repository -RelativePath $browserLedgerArtifact.path -Label "browser temporal ledger"
    $browserLedger = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $browserLedgerPath) -Label "Browser temporal ledger"
    $browserEvidenceSet = $evidenceSetMap["browser_temporal_4e476b31"]
    Assert-Condition -Condition ($browserLedger.git.commit -ceq $browserEvidenceSet.commit) -Message "Browser ledger source commit mismatch."
    Assert-Condition -Condition ($browserLedger.git.tree -ceq $browserEvidenceSet.tree) -Message "Browser ledger source tree mismatch."
    foreach ($binding in @(
        @("browser_temporal_frame_a", "canvas_frame_a"),
        @("browser_temporal_frame_b", "canvas_frame_b")
    )) {
        $artifact = $artifactMap[$binding[0]]
        $ledgerArtifact = $browserLedger.artifacts.($binding[1])
        Assert-Condition -Condition ([long]$ledgerArtifact.bytes -eq [long]$artifact.bytes) -Message "Browser ledger byte count mismatch for $($binding[0])."
        Assert-Condition -Condition ($ledgerArtifact.sha256 -ceq $artifact.sha256) -Message "Browser ledger hash mismatch for $($binding[0])."
    }

    $qualityManifestRelative = "docs/evidence/gate1/native/5eadec7f6f9f9b5b6734ce62ea3518f0297496f9/quality-matrix/artifact-manifest.json"
    $qualityManifestPath = Resolve-SafeChildPath -Root $Repository -RelativePath $qualityManifestRelative -Label "quality artifact manifest"
    Assert-Condition -Condition (Test-Path -LiteralPath $qualityManifestPath -PathType Leaf) -Message "Quality artifact manifest is missing."
    & git -C $Repository ls-files --error-unmatch -- $qualityManifestRelative 2>$null | Out-Null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Quality artifact manifest is not tracked."
    & git -C $Repository diff --quiet --no-ext-diff $Package.prepared_from.commit -- $qualityManifestRelative 2>$null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Quality artifact manifest differs from the package preparation snapshot."
    Assert-Condition -Condition ((Get-LowerSha256 -Path $qualityManifestPath) -ceq "732f53829c2ea6795127049427646b5f54c2ef3023684260d2d1d51b636c3dcc") -Message "Quality artifact manifest hash mismatch."
    $qualityManifest = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $qualityManifestPath) -Label "Quality artifact manifest"
    $qualityEvidenceSet = $evidenceSetMap["quality_matrix_5eadec7f"]
    Assert-Condition -Condition ($qualityManifest.source_commit -ceq $qualityEvidenceSet.commit) -Message "Quality manifest source commit mismatch."
    Assert-Condition -Condition ($qualityManifest.source_tree -ceq $qualityEvidenceSet.tree) -Message "Quality manifest source tree mismatch."
    $qualityFileMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($file in @($qualityManifest.files)) {
        Assert-Condition -Condition $qualityFileMap.TryAdd($file.file, $file) -Message "Duplicate quality manifest file: $($file.file)"
    }
    foreach ($artifactId in @("quality_low_clear", "quality_high_clear", "quality_low_clear_metrics", "quality_high_clear_metrics")) {
        $artifact = $artifactMap[$artifactId]
        $fileName = [IO.Path]::GetFileName($artifact.path)
        Assert-Condition -Condition $qualityFileMap.ContainsKey($fileName) -Message "Quality manifest does not bind $artifactId."
        $manifestEntry = $qualityFileMap[$fileName]
        Assert-Condition -Condition ([long]$manifestEntry.bytes -eq [long]$artifact.bytes) -Message "Quality manifest byte count mismatch for $artifactId."
        Assert-Condition -Condition ($manifestEntry.sha256 -ceq $artifact.sha256) -Message "Quality manifest hash mismatch for $artifactId."
    }

    if ($CheckExternal) {
        $nativeLedgerArtifact = $artifactMap["native_visual_ledger"]
        $nativeLedgerPath = Resolve-SafeChildPath -Root $ForgeRoot -RelativePath $nativeLedgerArtifact.path -Label "native visual ledger"
        $nativeLedger = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $nativeLedgerPath) -Label "Native visual ledger"
        Assert-Condition -Condition ($nativeLedger.build_source_commit -ceq $evidenceSetMap["native_water_125ba0b"].commit) -Message "Native ledger source commit does not match its evidence set."
        $nativeValidationArtifact = $artifactMap["native_visual_validation"]
        $nativeValidationPath = Resolve-SafeChildPath -Root $ForgeRoot -RelativePath $nativeValidationArtifact.path -Label "native visual validation"
        $nativeValidation = ConvertFrom-StrictJson -Text (Get-Content -Raw -LiteralPath $nativeValidationPath) -Label "Native visual validation"
        $nativeEvidenceSet = $evidenceSetMap["native_water_125ba0b"]
        Assert-Condition -Condition ($nativeValidation.repository_commit -ceq $nativeEvidenceSet.commit) -Message "Native validation source commit mismatch."
        Assert-Condition -Condition ($nativeValidation.repository_tree -ceq $nativeEvidenceSet.tree) -Message "Native validation source tree mismatch."
        Assert-Condition -Condition ($nativeValidation.ledger_sha256 -ceq $nativeLedgerArtifact.sha256) -Message "Native validation does not bind the packaged ledger."
        $nativeCaptureMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        foreach ($capture in @($nativeValidation.captures)) {
            Assert-Condition -Condition $nativeCaptureMap.TryAdd($capture.relative_path, $capture) -Message "Duplicate native validation capture: $($capture.relative_path)"
        }
        foreach ($artifactId in @("native_human_clear", "native_distant_clear", "native_water_clear", "native_water_rain", "native_water_restored")) {
            $artifact = $artifactMap[$artifactId]
            Assert-Condition -Condition $nativeCaptureMap.ContainsKey($artifact.path) -Message "Native validation does not bind $artifactId."
            $capture = $nativeCaptureMap[$artifact.path]
            Assert-Condition -Condition ([long]$capture.bytes -eq [long]$artifact.bytes) -Message "Native validation byte count mismatch for $artifactId."
            Assert-Condition -Condition ($capture.sha256 -ceq $artifact.sha256) -Message "Native validation hash mismatch for $artifactId."
        }
        $nativeVideoArtifact = $artifactMap["native_movement_video"]
        Assert-Condition -Condition ($nativeValidation.video.relative_path -ceq $nativeVideoArtifact.path) -Message "Native validation video path mismatch."
        Assert-Condition -Condition ([long]$nativeValidation.video.bytes -eq [long]$nativeVideoArtifact.bytes) -Message "Native validation video byte count mismatch."
        Assert-Condition -Condition ($nativeValidation.video.sha256 -ceq $nativeVideoArtifact.sha256) -Message "Native validation video hash mismatch."

        $cadleTop = Get-GitText -Repository $CadleRoot -Arguments @("rev-parse", "--show-toplevel") -Label "Cadle top-level"
        Assert-Condition -Condition (
            [IO.Path]::GetFullPath($cadleTop).TrimEnd([IO.Path]::DirectorySeparatorChar) -ceq
            [IO.Path]::GetFullPath($CadleRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
        ) -Message "CadleReferenceRoot must be the exact Git top-level."
        $cadleHead = (Get-GitText -Repository $CadleRoot -Arguments @("rev-parse", "HEAD") -Label "Cadle HEAD").ToLowerInvariant()
        $cadleTree = (Get-GitText -Repository $CadleRoot -Arguments @("rev-parse", "HEAD^{tree}") -Label "Cadle tree").ToLowerInvariant()
        Assert-Condition -Condition ($cadleHead -ceq $cadleEvidenceSet.commit) -Message "Cadle reference commit mismatch."
        Assert-Condition -Condition ($cadleTree -ceq $cadleEvidenceSet.tree) -Message "Cadle reference tree mismatch."
        $cadleOrigin = Get-GitText -Repository $CadleRoot -Arguments @("remote", "get-url", "origin") -Label "Cadle origin"
        Assert-Condition -Condition ($cadleOrigin -ceq $cadleLock.repository.url) -Message "Cadle reference origin mismatch."
        & git -C $CadleRoot symbolic-ref -q HEAD 2>$null | Out-Null
        Assert-Condition -Condition ($LASTEXITCODE -eq 1) -Message "Cadle reference must remain detached."
        $cadleStatus = @(& git -C $CadleRoot status --porcelain=v1 2>$null | ForEach-Object { "$_" })
        Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Cadle status query failed."
        Assert-Condition -Condition ($cadleStatus.Count -eq 1 -and $cadleStatus[0] -ceq " M package-lock.json") -Message "Cadle reference has changes outside the single allowed package-lock overlay."
        $cadleOverlayHash = Get-LowerSha256 -Path (Join-Path $CadleRoot "package-lock.json")
        $allowedOverlayHashes = @(
            [string]$cadleLock.qualification.overlaySha256,
            [string]$cadleLock.qualification.generationEvidence.rawWindowsCrLfSha256
        )
        Assert-Condition -Condition ($allowedOverlayHashes -ccontains $cadleOverlayHash) -Message "Cadle package-lock overlay hash mismatch."
    }

    $expectedReadiness = @{
        "R1-014" = "ready_for_human_review"
        "R1-023" = "requires_gpu_observation"
        "R1-025" = "requires_gpu_observation"
        "R1-026" = "requires_gpu_observation"
        "R1-046" = "ready_for_human_review"
        "R1-047" = "ready_for_human_review"
        "R1-048" = "requires_presentmon_pass_and_realtime_review"
        "R1-049" = "ready_for_human_review"
        "R1-050" = "ready_for_human_review"
        "R1-051" = "ready_for_human_review"
        "R1-052" = "ready_for_human_review"
    }
    $expectedRouteEvidenceSets = @{
        "R1-014" = @("browser_temporal_4e476b31")
        "R1-023" = @("native_water_125ba0b", "quality_matrix_5eadec7f")
        "R1-025" = @("native_water_125ba0b")
        "R1-026" = @("quality_matrix_5eadec7f", "native_water_125ba0b")
        "R1-046" = @("native_water_125ba0b")
        "R1-047" = @("native_water_125ba0b")
        "R1-048" = @("native_water_125ba0b")
        "R1-049" = @("native_water_125ba0b", "quality_matrix_5eadec7f")
        "R1-050" = @("native_water_125ba0b", "quality_matrix_5eadec7f")
        "R1-051" = @("native_water_125ba0b", "grassworks_forge_semantic_protocol_73db783")
        "R1-052" = @("native_water_125ba0b", "cadle_snapshot_ea0da625", "cadle_forge_ab_protocol_1bd5292")
    }
    $expectedRouteArtifacts = @{
        "R1-014" = @("browser_temporal_frame_a", "browser_temporal_frame_b", "browser_temporal_ledger")
        "R1-023" = @("native_water_clear", "native_water_rain", "native_water_restored", "quality_low_clear", "quality_high_clear", "native_visual_ledger", "native_visual_validation")
        "R1-025" = @("native_water_clear", "native_water_rain", "native_water_restored", "native_visual_ledger", "native_visual_validation")
        "R1-026" = @("quality_low_clear", "quality_high_clear", "quality_low_clear_metrics", "quality_high_clear_metrics", "native_water_clear", "native_water_rain", "native_visual_ledger", "native_visual_validation")
        "R1-046" = @("native_human_clear", "native_visual_ledger", "native_visual_validation")
        "R1-047" = @("native_distant_clear", "native_visual_ledger", "native_visual_validation")
        "R1-048" = @("native_movement_video", "native_video_probe", "native_visual_ledger", "native_visual_validation")
        "R1-049" = @("native_human_clear", "native_distant_clear", "native_water_clear", "native_water_rain", "quality_low_clear", "quality_high_clear")
        "R1-050" = @("native_human_clear", "native_distant_clear", "native_water_clear", "quality_high_clear")
        "R1-051" = @(
            "grassworks_semantic_protocol_json", "grassworks_semantic_review_guide",
            "grassworks_high_sunny_current", "grassworks_high_rain_current", "grassworks_historical_post_current_render",
            "native_human_clear", "native_distant_clear", "native_water_clear", "native_water_rain"
        )
        "R1-052" = @("native_human_clear", "native_distant_clear", "native_water_clear", "cadle_vale_ground", "cadle_forest_ground", "cadle_water_golden_shore", "cadle_baseline_metadata", "cadle_forge_ab_protocol_json", "cadle_forge_ab_review_guide")
    }
    $routeMap = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($route in @($Package.routes)) {
        Assert-ExactProperties -Value $route -Expected @("route", "review_readiness", "evidence_set_ids", "artifact_ids", "acceptance_criteria", "remaining_blockers") -Label "package.routes[]"
        Assert-NonEmptyString -Value $route.route -Label "package.routes[].route"
        Assert-Condition -Condition ($expectedReadiness.Keys -ccontains [string]$route.route) -Message "Unknown human-review route: $($route.route)"
        Assert-Condition -Condition $routeMap.TryAdd($route.route, $route) -Message "Duplicate human-review route: $($route.route)"
        Assert-Condition -Condition ($route.review_readiness -ceq $expectedReadiness[$route.route]) -Message "Review readiness mismatch for $($route.route)."

        $routeEvidenceSetIds = @($route.evidence_set_ids)
        Assert-ExactStringArray -Actual $route.evidence_set_ids -Expected $expectedRouteEvidenceSets[$route.route] -Label "route[$($route.route)].evidence_set_ids"
        foreach ($evidenceSetId in $routeEvidenceSetIds) {
            Assert-Condition -Condition $evidenceSetMap.ContainsKey($evidenceSetId) -Message "Unknown evidence set '$evidenceSetId' for $($route.route)."
        }

        $routeArtifactIds = @($route.artifact_ids)
        Assert-ExactStringArray -Actual $route.artifact_ids -Expected $expectedRouteArtifacts[$route.route] -Label "route[$($route.route)].artifact_ids"
        Assert-Condition -Condition ($routeArtifactIds.Count -gt 0) -Message "Route $($route.route) must cite at least one package artifact."
        $seenRouteArtifacts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($artifactId in $routeArtifactIds) {
            Assert-NonEmptyString -Value $artifactId -Label "route[$($route.route)].artifact_ids[]"
            Assert-Condition -Condition $seenRouteArtifacts.Add($artifactId) -Message "Duplicate artifact '$artifactId' in route $($route.route)."
            Assert-Condition -Condition $artifactMap.ContainsKey($artifactId) -Message "Unknown artifact '$artifactId' in route $($route.route)."
        }

        $criteria = @($route.acceptance_criteria)
        Assert-Condition -Condition ($criteria.Count -gt 0) -Message "Route $($route.route) must define acceptance criteria."
        foreach ($criterion in $criteria) {
            Assert-NonEmptyString -Value $criterion -Label "route[$($route.route)].acceptance_criteria[]"
        }
        $blockers = @($route.remaining_blockers)
        if ($route.review_readiness -ceq "ready_for_human_review") {
            Assert-Condition -Condition ($blockers.Count -eq 0) -Message "Review-ready route $($route.route) must not claim a remaining review blocker."
        }
        else {
            Assert-Condition -Condition ($blockers.Count -gt 0) -Message "Non-ready route $($route.route) must state at least one blocker."
        }
        foreach ($blocker in $blockers) {
            Assert-NonEmptyString -Value $blocker -Label "route[$($route.route)].remaining_blockers[]"
        }
    }
    Assert-Condition -Condition ($routeMap.Count -eq $expectedReadiness.Count) -Message "The package must contain exactly eleven human-review routes."

    $excluded = @($Package.excluded_technical_routes)
    Assert-Condition -Condition ($excluded.Count -eq 2) -Message "Exactly two technical-only routes must be excluded from this package."
    $excludedIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($route in $excluded) {
        Assert-ExactProperties -Value $route -Expected @("route", "reason") -Label "package.excluded_technical_routes[]"
        Assert-Condition -Condition (@("R1-037", "R1-041") -ccontains [string]$route.route) -Message "Unexpected technical route exclusion: $($route.route)"
        Assert-Condition -Condition $excludedIds.Add($route.route) -Message "Duplicate technical route exclusion: $($route.route)"
        Assert-NonEmptyString -Value $route.reason -Label "excluded[$($route.route)].reason"
    }

    Assert-ExactProperties -Value $Package.review_receipt_contract -Expected @("schema", "schema_version", "scope", "attestation", "allowed_verdicts", "gate1_transition_authorized") -Label "package.review_receipt_contract"
    Assert-Condition -Condition ($Package.review_receipt_contract.schema -ceq "forge.gate1-human-review-receipt") -Message "Unexpected review receipt schema."
    Assert-Condition -Condition ([int]$Package.review_receipt_contract.schema_version -eq 1) -Message "Unexpected review receipt schema version."
    Assert-Condition -Condition ($Package.review_receipt_contract.scope -ceq "human_visual_review_only") -Message "Unexpected review receipt scope."
    Assert-Condition -Condition ($Package.review_receipt_contract.attestation -ceq "review_performed_by_named_human_owner") -Message "Unexpected review attestation contract."
    Assert-Condition -Condition ((@($Package.review_receipt_contract.allowed_verdicts) -join ",") -ceq "accepted,rejected,inconclusive") -Message "Unexpected allowed review verdicts."
    Assert-Condition -Condition ($Package.review_receipt_contract.gate1_transition_authorized -is [bool] -and -not $Package.review_receipt_contract.gate1_transition_authorized) -Message "Review receipts cannot authorize Gate 1 transition."

    Assert-ExactProperties -Value $Package.summary -Expected @("artifact_count", "evidence_sets", "exact_baseline_visual_artifacts", "human_routes", "fully_review_ready_routes", "cadle_assets_embedded", "gate1_routes_closed") -Label "package.summary"
    Assert-Condition -Condition ([int]$Package.summary.artifact_count -eq 27) -Message "Package artifact summary mismatch."
    Assert-Condition -Condition ([int]$Package.summary.evidence_sets -eq 6) -Message "Package evidence-set summary mismatch."
    Assert-Condition -Condition ([int]$Package.summary.exact_baseline_visual_artifacts -eq $exactBaselineVisualArtifactCount) -Message "Package exact-baseline visual artifact summary mismatch."
    Assert-Condition -Condition ([int]$Package.summary.human_routes -eq 11) -Message "Package route summary mismatch."
    $readyRouteCount = @($routeMap.Values | Where-Object { $_.review_readiness -ceq "ready_for_human_review" }).Count
    Assert-Condition -Condition ($readyRouteCount -eq 7) -Message "Exactly seven routes must be ready for human review."
    Assert-Condition -Condition ([int]$Package.summary.fully_review_ready_routes -eq $readyRouteCount) -Message "Package ready-route summary mismatch."
    Assert-Condition -Condition ([int]$Package.summary.cadle_assets_embedded -eq 0) -Message "Package must embed zero Cadle assets."
    Assert-Condition -Condition ([int]$Package.summary.gate1_routes_closed -eq 0) -Message "Preparing the package must close zero Gate 1 routes."

    return [pscustomobject]@{
        ArtifactMap = $artifactMap
        EvidenceSetMap = $evidenceSetMap
        RouteMap = $routeMap
    }
}

function Assert-ReviewReceipt {
    param(
        [Parameter(Mandatory = $true)][object]$Receipt,
        [Parameter(Mandatory = $true)][object]$Package,
        [Parameter(Mandatory = $true)][object]$PackageState,
        [Parameter(Mandatory = $true)][string]$PackageHash,
        [Parameter(Mandatory = $true)][bool]$ExternalVerified
    )

    Assert-ExactProperties -Value $Receipt -Expected @(
        "schema", "schema_version", "scope", "package_sha256", "qualified_software_baseline_commit",
        "reviewer", "reviewed_at_utc", "attestation", "gate1_transition_authorized", "routes"
    ) -Label "review receipt"
    Assert-Condition -Condition ($Receipt.schema -ceq $Package.review_receipt_contract.schema) -Message "Review receipt schema mismatch."
    Assert-Condition -Condition ($Receipt.schema_version -is [long] -or $Receipt.schema_version -is [int]) -Message "Review receipt schema version must be an integer."
    Assert-Condition -Condition ([long]$Receipt.schema_version -eq [long]$Package.review_receipt_contract.schema_version) -Message "Review receipt schema version mismatch."
    Assert-Condition -Condition ($Receipt.scope -ceq $Package.review_receipt_contract.scope) -Message "Review receipt scope mismatch."
    Assert-HexDigest -Value $Receipt.package_sha256 -Length 64 -Label "review.package_sha256"
    Assert-Condition -Condition ($Receipt.package_sha256 -ceq $PackageHash) -Message "Review receipt is not bound to the current package bytes."
    Assert-Condition -Condition ($Receipt.qualified_software_baseline_commit -ceq $Package.qualified_software_baseline.commit) -Message "Review receipt qualified software baseline mismatch."
    Assert-Condition -Condition ($Receipt.attestation -ceq $Package.review_receipt_contract.attestation) -Message "Review receipt attestation mismatch."
    Assert-Condition -Condition ($Receipt.gate1_transition_authorized -is [bool] -and -not $Receipt.gate1_transition_authorized) -Message "A human review receipt cannot authorize Gate 1 transition."
    Assert-Condition -Condition $ExternalVerified -Message "A human review receipt requires -VerifyExternal and verified external evidence roots."

    Assert-ExactProperties -Value $Receipt.reviewer -Expected @("kind", "identifier") -Label "review.reviewer"
    Assert-Condition -Condition ($Receipt.reviewer.kind -ceq "human_owner") -Message "Reviewer kind must be human_owner."
    Assert-NonEmptyString -Value $Receipt.reviewer.identifier -Label "review.reviewer.identifier"
    $reviewerIdentifier = ([string]$Receipt.reviewer.identifier).Trim()
    Assert-Condition -Condition ($reviewerIdentifier -ceq [string]$Receipt.reviewer.identifier) -Message "Reviewer identifier cannot contain surrounding whitespace."
    $placeholderPattern = '^(?i:todo|tbd|replace.*|unknown|n/?a|anonymous|human_owner)$'
    Assert-Condition -Condition (-not ($reviewerIdentifier -match $placeholderPattern)) -Message "Reviewer identifier is a placeholder."

    Assert-NonEmptyString -Value $Receipt.reviewed_at_utc -Label "review.reviewed_at_utc"
    Assert-Condition -Condition ([string]$Receipt.reviewed_at_utc -cmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$') -Message "Review timestamp must use exact UTC second precision (YYYY-MM-DDTHH:MM:SSZ)."
    $timestamp = [DateTimeOffset]::MinValue
    $timestampStyles = [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
    $timestampValid = [DateTimeOffset]::TryParseExact(
        [string]$Receipt.reviewed_at_utc,
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        [Globalization.CultureInfo]::InvariantCulture,
        $timestampStyles,
        [ref]$timestamp
    )
    Assert-Condition -Condition ($timestampValid -and $timestamp.Offset -eq [TimeSpan]::Zero) -Message "Review timestamp is not a valid UTC timestamp."
    Assert-Condition -Condition ($timestamp -le [DateTimeOffset]::UtcNow.AddMinutes(5)) -Message "Review timestamp cannot be in the future."

    Assert-JsonArray -Value $Receipt.routes -Label "review.routes"
    $reviewRoutes = @($Receipt.routes)
    Assert-Condition -Condition ($reviewRoutes.Count -gt 0) -Message "Review receipt must contain at least one route verdict."
    $seenRoutes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $accepted = 0
    foreach ($route in $reviewRoutes) {
        Assert-ExactProperties -Value $route -Expected @("route", "verdict", "evidence_set_ids", "artifact_ids", "observations", "blocking_issues") -Label "review.routes[]"
        Assert-NonEmptyString -Value $route.route -Label "review.routes[].route"
        Assert-NonEmptyString -Value $route.verdict -Label "review.route[$($route.route)].verdict"
        Assert-Condition -Condition $PackageState.RouteMap.ContainsKey($route.route) -Message "Unknown reviewed route: $($route.route)"
        Assert-Condition -Condition $seenRoutes.Add($route.route) -Message "Duplicate reviewed route: $($route.route)"
        Assert-Condition -Condition (@($Package.review_receipt_contract.allowed_verdicts) -ccontains [string]$route.verdict) -Message "Unknown review verdict for $($route.route): $($route.verdict)"
        if ($route.verdict -ceq "accepted") {
            $accepted++
            Assert-Condition -Condition ($PackageState.RouteMap[$route.route].review_readiness -ceq "ready_for_human_review") -Message "Only a review-ready route can receive an accepted verdict."
        }

        $expectedEvidenceSetIds = @($PackageState.RouteMap[$route.route].evidence_set_ids)
        Assert-ExactStringArray -Actual $route.evidence_set_ids -Expected $expectedEvidenceSetIds -Label "review.route[$($route.route)].evidence_set_ids"

        $allowedArtifactIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($artifactId in @($PackageState.RouteMap[$route.route].artifact_ids)) {
            [void]$allowedArtifactIds.Add($artifactId)
        }
        Assert-JsonArray -Value $route.artifact_ids -Label "review.route[$($route.route)].artifact_ids"
        $receiptArtifacts = @($route.artifact_ids)
        if ($route.verdict -cne "inconclusive") {
            Assert-Condition -Condition ($receiptArtifacts.Count -gt 0) -Message "A decisive review of $($route.route) must cite at least one package artifact."
        }
        $seenArtifacts = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($artifactId in $receiptArtifacts) {
            Assert-NonEmptyString -Value $artifactId -Label "review.route[$($route.route)].artifact_ids[]"
            Assert-Condition -Condition $seenArtifacts.Add($artifactId) -Message "Duplicate reviewed artifact '$artifactId' for $($route.route)."
            Assert-Condition -Condition $allowedArtifactIds.Contains($artifactId) -Message "Artifact '$artifactId' is not registered for $($route.route)."
        }
        if ($route.verdict -ceq "accepted") {
            Assert-Condition -Condition ($receiptArtifacts.Count -eq $allowedArtifactIds.Count) -Message "Accepted review of $($route.route) must cite every registered artifact."
            foreach ($artifactId in $allowedArtifactIds) {
                Assert-Condition -Condition $seenArtifacts.Contains($artifactId) -Message "Accepted review of $($route.route) is missing artifact '$artifactId'."
            }
        }

        Assert-JsonArray -Value $route.observations -Label "review.route[$($route.route)].observations"
        $observations = @($route.observations)
        Assert-Condition -Condition ($observations.Count -gt 0) -Message "Review of $($route.route) must contain a concrete observation."
        foreach ($observation in $observations) {
            Assert-NonEmptyString -Value $observation -Label "review.route[$($route.route)].observations[]"
            Assert-Condition -Condition (-not ([string]$observation -match '(?i)\b(?:todo|tbd|placeholder)\b')) -Message "Review observation for $($route.route) contains a placeholder."
        }
        if ($route.route -ceq "R1-051") {
            foreach ($label in @("density:", "naturalness:", "depth:", "lighting:", "finish:", "source_limitations:")) {
                $matchingObservations = @($observations | Where-Object {
                    $candidate = [string]$_
                    $candidate.StartsWith($label, [StringComparison]::Ordinal) -and
                        -not [string]::IsNullOrWhiteSpace($candidate.Substring($label.Length))
                })
                Assert-Condition -Condition ($matchingObservations.Count -gt 0) -Message "R1-051 review requires concrete non-blank text after prefix '$label'."
            }
        }
        Assert-JsonArray -Value $route.blocking_issues -Label "review.route[$($route.route)].blocking_issues"
        $blockingIssues = @($route.blocking_issues)
        if (@("rejected", "inconclusive") -ccontains [string]$route.verdict) {
            Assert-Condition -Condition ($blockingIssues.Count -gt 0) -Message "$($route.verdict) review of $($route.route) must state a blocking issue."
        }
        else {
            Assert-Condition -Condition ($blockingIssues.Count -eq 0) -Message "Accepted review of $($route.route) cannot contain a blocking issue."
        }
        foreach ($issue in $blockingIssues) {
            Assert-NonEmptyString -Value $issue -Label "review.route[$($route.route)].blocking_issues[]"
            Assert-Condition -Condition (-not ([string]$issue -match '(?i)\b(?:todo|tbd|placeholder)\b')) -Message "Review blocker for $($route.route) contains a placeholder."
        }
    }

    return [pscustomobject]@{
        Routes = $reviewRoutes.Count
        Accepted = $accepted
    }
}

function Copy-JsonObject {
    param([Parameter(Mandatory = $true)][object]$Value)

    return (($Value | ConvertTo-Json -Depth 64 -Compress) | ConvertFrom-Json -Depth 64 -DateKind String)
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $threw = $false
    try {
        & $Action | Out-Null
    }
    catch {
        $threw = $true
    }
    Assert-Condition -Condition $threw -Message "Self-test expected rejection: $Label"
}

function Invoke-ReceiptSelfTest {
    param(
        [Parameter(Mandatory = $true)][object]$Package,
        [Parameter(Mandatory = $true)][object]$PackageState,
        [Parameter(Mandatory = $true)][string]$PackageHash
    )

    $valid = [pscustomobject]@{
        schema = "forge.gate1-human-review-receipt"
        schema_version = 1
        scope = "human_visual_review_only"
        package_sha256 = $PackageHash
        qualified_software_baseline_commit = $Package.qualified_software_baseline.commit
        reviewer = [pscustomobject]@{
            kind = "human_owner"
            identifier = "synthetic-self-test-reviewer"
        }
        reviewed_at_utc = "2026-09-03T00:00:00Z"
        attestation = "review_performed_by_named_human_owner"
        gate1_transition_authorized = $false
        routes = @(
            [pscustomobject]@{
                route = "R1-014"
                verdict = "accepted"
                evidence_set_ids = @("browser_temporal_4e476b31")
                artifact_ids = @("browser_temporal_frame_a", "browser_temporal_frame_b", "browser_temporal_ledger")
                observations = @("Synthetic contract exercise only; no real human verdict is recorded.")
                blocking_issues = @()
            }
        )
    }
    [void](Assert-ReviewReceipt -Receipt $valid -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true)

    $validR1051 = Copy-JsonObject -Value $valid
    $validR1051.routes[0].route = "R1-051"
    $validR1051.routes[0].evidence_set_ids = @($PackageState.RouteMap["R1-051"].evidence_set_ids)
    $validR1051.routes[0].artifact_ids = @($PackageState.RouteMap["R1-051"].artifact_ids)
    $validR1051.routes[0].observations = @(
        "density: Synthetic contract coverage only.",
        "naturalness: Synthetic contract coverage only.",
        "depth: Synthetic contract coverage only.",
        "lighting: Synthetic contract coverage only.",
        "finish: Synthetic contract coverage only.",
        "source_limitations: Synthetic acknowledgement only; no real human verdict is recorded."
    )
    [void](Assert-ReviewReceipt -Receipt $validR1051 -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true)

    $r1051MissingAxis = Copy-JsonObject -Value $validR1051
    $r1051MissingAxis.routes[0].observations = @($r1051MissingAxis.routes[0].observations | Where-Object { -not ([string]$_).StartsWith("density:", [StringComparison]::Ordinal) })
    Assert-Throws -Label "R1-051 missing density observation" -Action { Assert-ReviewReceipt -Receipt $r1051MissingAxis -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $r1051MissingSourceLimits = Copy-JsonObject -Value $validR1051
    $r1051MissingSourceLimits.routes[0].observations = @($r1051MissingSourceLimits.routes[0].observations | Where-Object { -not ([string]$_).StartsWith("source_limitations:", [StringComparison]::Ordinal) })
    Assert-Throws -Label "R1-051 missing source-limitations acknowledgement" -Action { Assert-ReviewReceipt -Receipt $r1051MissingSourceLimits -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $r1051EmptyAxis = Copy-JsonObject -Value $validR1051
    $r1051EmptyAxis.routes[0].observations = @($r1051EmptyAxis.routes[0].observations | ForEach-Object {
        if (([string]$_).StartsWith("density:", [StringComparison]::Ordinal)) { "density:" } else { $_ }
    })
    Assert-Throws -Label "R1-051 empty density observation" -Action { Assert-ReviewReceipt -Receipt $r1051EmptyAxis -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $r1051WhitespaceSourceLimits = Copy-JsonObject -Value $validR1051
    $r1051WhitespaceSourceLimits.routes[0].observations = @($r1051WhitespaceSourceLimits.routes[0].observations | ForEach-Object {
        if (([string]$_).StartsWith("source_limitations:", [StringComparison]::Ordinal)) { "source_limitations:   " } else { $_ }
    })
    Assert-Throws -Label "R1-051 whitespace-only source-limitations acknowledgement" -Action { Assert-ReviewReceipt -Receipt $r1051WhitespaceSourceLimits -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $wrongHash = Copy-JsonObject -Value $valid
    $wrongHash.package_sha256 = "0" * 64
    Assert-Throws -Label "wrong package hash" -Action { Assert-ReviewReceipt -Receipt $wrongHash -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $blankReviewer = Copy-JsonObject -Value $valid
    $blankReviewer.reviewer.identifier = " TODO "
    Assert-Throws -Label "placeholder reviewer" -Action { Assert-ReviewReceipt -Receipt $blankReviewer -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $duplicateRoute = Copy-JsonObject -Value $valid
    $duplicateRoute.routes = @($duplicateRoute.routes[0], (Copy-JsonObject -Value $duplicateRoute.routes[0]))
    Assert-Throws -Label "duplicate route" -Action { Assert-ReviewReceipt -Receipt $duplicateRoute -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $unknownVerdict = Copy-JsonObject -Value $valid
    $unknownVerdict.routes[0].verdict = "pass"
    Assert-Throws -Label "unknown verdict" -Action { Assert-ReviewReceipt -Receipt $unknownVerdict -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $transitionAttempt = Copy-JsonObject -Value $valid
    $transitionAttempt.gate1_transition_authorized = $true
    Assert-Throws -Label "transition authorization" -Action { Assert-ReviewReceipt -Receipt $transitionAttempt -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $inconclusiveWithoutIssue = Copy-JsonObject -Value $valid
    $inconclusiveWithoutIssue.routes[0].verdict = "inconclusive"
    $inconclusiveWithoutIssue.routes[0].blocking_issues = @()
    Assert-Throws -Label "inconclusive without issue" -Action { Assert-ReviewReceipt -Receipt $inconclusiveWithoutIssue -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $unknownArtifact = Copy-JsonObject -Value $valid
    $unknownArtifact.routes[0].artifact_ids = @("not-in-package")
    Assert-Throws -Label "unknown artifact" -Action { Assert-ReviewReceipt -Receipt $unknownArtifact -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $extraProperty = Copy-JsonObject -Value $valid
    $extraProperty | Add-Member -NotePropertyName gate1_qualified -NotePropertyValue $true
    Assert-Throws -Label "extra qualification property" -Action { Assert-ReviewReceipt -Receipt $extraProperty -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $caseVariantVerdict = Copy-JsonObject -Value $valid
    $caseVariantVerdict.routes[0].verdict = "Accepted"
    Assert-Throws -Label "case-variant verdict" -Action { Assert-ReviewReceipt -Receipt $caseVariantVerdict -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $nonReadyAccepted = Copy-JsonObject -Value $valid
    $nonReadyAccepted.routes[0].route = "R1-023"
    $nonReadyAccepted.routes[0].evidence_set_ids = @($PackageState.RouteMap["R1-023"].evidence_set_ids)
    $nonReadyAccepted.routes[0].artifact_ids = @($PackageState.RouteMap["R1-023"].artifact_ids)
    Assert-Throws -Label "accepted non-ready route" -Action { Assert-ReviewReceipt -Receipt $nonReadyAccepted -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $acceptedWithBlocker = Copy-JsonObject -Value $valid
    $acceptedWithBlocker.routes[0].blocking_issues = @("Synthetic contradictory blocker.")
    Assert-Throws -Label "accepted route with blocker" -Action { Assert-ReviewReceipt -Receipt $acceptedWithBlocker -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $acceptedPartialEvidence = Copy-JsonObject -Value $valid
    $acceptedPartialEvidence.routes[0].artifact_ids = @("browser_temporal_frame_a")
    Assert-Throws -Label "accepted route with incomplete evidence" -Action { Assert-ReviewReceipt -Receipt $acceptedPartialEvidence -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    Assert-Throws -Label "receipt without external verification" -Action { Assert-ReviewReceipt -Receipt $valid -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $false }

    $placeholderObservation = Copy-JsonObject -Value $valid
    $placeholderObservation.routes[0].observations = @("TODO")
    Assert-Throws -Label "placeholder observation" -Action { Assert-ReviewReceipt -Receipt $placeholderObservation -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    $collapsedEvidenceSet = Copy-JsonObject -Value $valid
    $collapsedEvidenceSet.routes[0].route = "R1-049"
    $collapsedEvidenceSet.routes[0].evidence_set_ids = @("native_water_125ba0b,quality_matrix_5eadec7f")
    $collapsedEvidenceSet.routes[0].artifact_ids = @($PackageState.RouteMap["R1-049"].artifact_ids)
    Assert-Throws -Label "collapsed evidence-set array" -Action { Assert-ReviewReceipt -Receipt $collapsedEvidenceSet -Package $Package -PackageState $PackageState -PackageHash $PackageHash -ExternalVerified $true }

    Write-Output "FORGE_GATE1_HUMAN_REVIEW_SELF_TEST_PASS cases=21 synthetic_only=true human_review_recorded=false"
}

$repositoryFull = [IO.Path]::GetFullPath($RepositoryRoot)
Assert-Condition -Condition (Test-Path -LiteralPath (Join-Path $repositoryFull ".git")) -Message "RepositoryRoot is not a Git worktree: $repositoryFull"
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $repositoryFull "docs/evidence/gate1/gate1-human-review-package.v1.json"
}
$packageFull = if ([IO.Path]::IsPathRooted($PackagePath)) {
    [IO.Path]::GetFullPath($PackagePath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryFull $PackagePath))
}
Assert-Condition -Condition (Test-Path -LiteralPath $packageFull -PathType Leaf) -Message "Human-review package is missing: $packageFull"

$packageText = Get-Content -Raw -LiteralPath $packageFull
$package = ConvertFrom-StrictJson -Text $packageText -Label "Human-review package"
$packageHash = Get-LowerSha256 -Path $packageFull
Assert-Condition -Condition ($packageHash -ceq $script:ExpectedPackageSha256) -Message "Human-review package differs from the validator-pinned SHA-256."
if (-not [string]::IsNullOrWhiteSpace($ReviewPath)) {
    Assert-Condition -Condition $VerifyExternal.IsPresent -Message "-ReviewPath requires -VerifyExternal and all three external evidence roots."
}
$packageState = Assert-Package -Package $package -Repository $repositoryFull -CheckExternal $VerifyExternal.IsPresent -ForgeRoot $ForgeExternalRoot -GrassworksRoot $GrassworksExternalRoot -CadleRoot $CadleReferenceRoot

Write-Output (
    "FORGE_GATE1_HUMAN_REVIEW_PACKAGE_PASS artifacts=27 evidence_sets=6 exact_baseline_visual_artifacts=2 human_routes=11 review_ready=7 external_verified={0} cadle_assets_embedded=0 routes_closed=0 sha256={1}" -f $VerifyExternal.IsPresent.ToString().ToLowerInvariant(), $packageHash
)

if ($SelfTest) {
    Invoke-ReceiptSelfTest -Package $package -PackageState $packageState -PackageHash $packageHash
}

if (-not [string]::IsNullOrWhiteSpace($ReviewPath)) {
    $reviewFull = if ([IO.Path]::IsPathRooted($ReviewPath)) {
        [IO.Path]::GetFullPath($ReviewPath)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repositoryFull $ReviewPath))
    }
    Assert-Condition -Condition (Test-Path -LiteralPath $reviewFull -PathType Leaf) -Message "Review receipt is missing: $reviewFull"
    $reviewText = Get-Content -Raw -LiteralPath $reviewFull
    $review = ConvertFrom-StrictJson -Text $reviewText -Label "Human-review receipt"
    $reviewResult = Assert-ReviewReceipt -Receipt $review -Package $package -PackageState $packageState -PackageHash $packageHash -ExternalVerified $VerifyExternal.IsPresent
    Write-Output (
        "FORGE_GATE1_HUMAN_REVIEW_RECEIPT_PASS routes={0} accepted={1} scope=human_visual_review_only closes_gate=false" -f $reviewResult.Routes, $reviewResult.Accepted
    )
}
