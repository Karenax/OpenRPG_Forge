[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$ProtocolPath,

    [string]$GrassworksExternalRoot,

    [string]$ForgeExternalRoot,

    [switch]$VerifyExternal,

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:ExpectedProtocolSha256 = 'cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d'

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
        '{0} properties differ: missing=[{1}] extra=[{2}].' -f $Label, ($missing -join ','), ($extra -join ',')
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
    $items = @($Actual)
    Assert-Condition -Condition ($items.Count -eq $Expected.Count) -Message "$Label item count differs."
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        Assert-NonEmptyString -Value $items[$index] -Label "$Label[$index]"
        Assert-Condition -Condition ([string]$items[$index] -ceq $Expected[$index]) -Message "$Label differs at index $index."
    }
}

function Assert-ExactBoolean {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][bool]$Expected,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [bool] -and [bool]$Value -eq $Expected) -Message "$Label must be $($Expected.ToString().ToLowerInvariant())."
}

function Assert-HexDigest {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][ValidateSet(40, 64)][int]$Length,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Value -is [string] -and ([string]$Value -cmatch "^[0-9a-f]{$Length}$")) -Message "$Label must be a lowercase $Length-character hexadecimal digest."
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

    try {
        $document = [Text.Json.JsonDocument]::Parse($Text)
        try {
            Assert-JsonElementHasNoDuplicateProperties -Element $document.RootElement -Path '$'
        }
        finally {
            $document.Dispose()
        }
        return ($Text | ConvertFrom-Json -Depth 64 -DateKind String)
    }
    catch {
        throw "$Label is not valid strict JSON: $($_.Exception.Message)"
    }
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
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "$Label Git command failed."
    return ($output -join "`n").Trim()
}

function Assert-GitCommitAndTree {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Commit,
        [Parameter(Mandatory = $true)][string]$Tree,
        [Parameter(Mandatory = $true)][string]$Label
    )

    & git -C $Repository rev-parse --verify "$Commit`^{commit}" 2>$null | Out-Null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "$Label commit does not exist: $Commit"
    $actualTree = (Get-GitText -Repository $Repository -Arguments @('rev-parse', "$Commit`^{tree}") -Label "$Label tree").ToLowerInvariant()
    Assert-Condition -Condition ($actualTree -ceq $Tree) -Message "$Label tree mismatch."
    & git -C $Repository merge-base --is-ancestor $Commit HEAD 2>$null
    Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "$Label commit is not an ancestor of HEAD."
}

function Test-PortableRelativePath {
    param([AllowNull()][object]$Value)

    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $false
    }
    $path = [string]$Value
    if ([IO.Path]::IsPathRooted($path) -or $path.Contains('\') -or $path.Contains(':') -or $path.StartsWith('/') -or $path.EndsWith('/')) {
        return $false
    }
    foreach ($component in $path.Split('/')) {
        if ([string]::IsNullOrWhiteSpace($component) -or $component -in @('.', '..')) {
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

    Assert-Condition -Condition (Test-PortableRelativePath -Value $RelativePath) -Message "$Label must use a portable relative path."
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    $nativeRelative = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootFull $nativeRelative))
    Assert-Condition -Condition $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -Message "$Label escapes its declared root."
    return $candidate
}

function Test-RootsOverlap {
    param(
        [Parameter(Mandatory = $true)][string]$First,
        [Parameter(Mandatory = $true)][string]$Second
    )

    $firstFull = [IO.Path]::GetFullPath($First).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $secondFull = [IO.Path]::GetFullPath($Second).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $firstPrefix = $firstFull + [IO.Path]::DirectorySeparatorChar
    $secondPrefix = $secondFull + [IO.Path]::DirectorySeparatorChar
    return $firstFull.Equals($secondFull, [StringComparison]::OrdinalIgnoreCase) -or
        $firstFull.StartsWith($secondPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $secondFull.StartsWith($firstPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePointBetween {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $current = Get-Item -LiteralPath $Path -Force
    while ($null -ne $current) {
        Assert-Condition -Condition (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "$Label traverses reparse point '$($current.FullName)'."
        $currentFull = [IO.Path]::GetFullPath($current.FullName).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        if ($currentFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
            return
        }
        $current = if ($current -is [IO.FileInfo]) { $current.Directory } else { $current.Parent }
    }
    throw "$Label did not resolve back to its declared root."
}

function Assert-FileSizeAndHash {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][long]$ExpectedBytes,
        [Parameter(Mandatory = $true)][string]$ExpectedSha256,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition (Test-Path -LiteralPath $Path -PathType Leaf) -Message "$Label is missing: $Path"
    $item = Get-Item -LiteralPath $Path -Force
    Assert-Condition -Condition ([long]$item.Length -eq $ExpectedBytes) -Message "$Label byte length mismatch."
    $actualHash = Get-LowerSha256 -Path $Path
    Assert-Condition -Condition ($actualHash -ceq $ExpectedSha256) -Message "$Label SHA-256 mismatch."
}

function Assert-ImageArtifact {
    param(
        [Parameter(Mandatory = $true)][object]$Artifact,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $path = Resolve-SafeChildPath -Root $Root -RelativePath ([string]$Artifact.relative_path) -Label "artifact[$($Artifact.id)]"
    Assert-FileSizeAndHash -Path $path -ExpectedBytes ([long]$Artifact.bytes) -ExpectedSha256 ([string]$Artifact.sha256) -Label "artifact[$($Artifact.id)]"
    Assert-NoReparsePointBetween -Path $path -Root $Root -Label "artifact[$($Artifact.id)]"

    $stream = [IO.File]::OpenRead($path)
    try {
        $signature = [byte[]]::new(8)
        $read = $stream.Read($signature, 0, $signature.Length)
        Assert-Condition -Condition ($read -eq 8) -Message "artifact[$($Artifact.id)] is too short for an image signature."
    }
    finally {
        $stream.Dispose()
    }

    if ($Artifact.media_type -ceq 'image/jpeg') {
        Assert-Condition -Condition ($signature[0] -eq 0xff -and $signature[1] -eq 0xd8 -and $signature[2] -eq 0xff) -Message "artifact[$($Artifact.id)] does not have a JPEG signature."
    }
    elseif ($Artifact.media_type -ceq 'image/png') {
        $png = @(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)
        for ($index = 0; $index -lt $png.Count; $index++) {
            Assert-Condition -Condition ($signature[$index] -eq $png[$index]) -Message "artifact[$($Artifact.id)] does not have a PNG signature."
        }
    }
    else {
        throw "artifact[$($Artifact.id)] has an unsupported media type."
    }

    $image = [Drawing.Image]::FromFile($path)
    try {
        Assert-Condition -Condition ($image.Width -eq [int]$Artifact.width -and $image.Height -eq [int]$Artifact.height) -Message "artifact[$($Artifact.id)] image dimensions mismatch."
    }
    finally {
        $image.Dispose()
    }
}

function New-OrdinalMap {
    return [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
}

function Assert-Protocol {
    param(
        [Parameter(Mandatory = $true)][object]$Protocol,
        [Parameter(Mandatory = $true)][string]$Repository
    )

    Assert-ExactProperties -Value $Protocol -Expected @(
        'schema', 'schema_version', 'protocol_id', 'status', 'route', 'prepared_from',
        'reference_identity', 'roots', 'artifacts', 'comparison_panels', 'context_bindings',
        'review_axes', 'review_contract', 'boundaries', 'qualification'
    ) -Label 'protocol'
    Assert-Condition -Condition ($Protocol.schema -ceq 'forge.gate1-r1-051-grassworks-semantic-protocol') -Message 'Unexpected protocol schema.'
    Assert-Condition -Condition (($Protocol.schema_version -is [long] -or $Protocol.schema_version -is [int]) -and [long]$Protocol.schema_version -eq 1) -Message 'Unexpected protocol schema version.'
    Assert-Condition -Condition ($Protocol.protocol_id -ceq 'r1-051-grassworks-forge-semantic-v1') -Message 'Unexpected protocol id.'
    Assert-Condition -Condition ($Protocol.status -ceq 'technically_ready_for_future_human_package_integration') -Message 'Protocol status cannot claim human qualification.'
    Assert-Condition -Condition ($Protocol.route -ceq 'R1-051') -Message 'Protocol must remain scoped to R1-051.'

    Assert-ExactProperties -Value $Protocol.prepared_from -Expected @('branch', 'base_commit', 'base_tree') -Label 'protocol.prepared_from'
    Assert-Condition -Condition ($Protocol.prepared_from.branch -ceq 'Rust') -Message 'Protocol must be prepared from Rust.'
    Assert-HexDigest -Value $Protocol.prepared_from.base_commit -Length 40 -Label 'protocol.prepared_from.base_commit'
    Assert-HexDigest -Value $Protocol.prepared_from.base_tree -Length 40 -Label 'protocol.prepared_from.base_tree'
    Assert-Condition -Condition ($Protocol.prepared_from.base_commit -ceq '48038854b828003dd514562e34d5930e614422b4') -Message 'Protocol base commit changed without a version bump.'
    Assert-Condition -Condition ($Protocol.prepared_from.base_tree -ceq 'f3fc51386cd1e46d241342d791d501953e4f384d') -Message 'Protocol base tree changed without a version bump.'
    Assert-GitCommitAndTree -Repository $Repository -Commit $Protocol.prepared_from.base_commit -Tree $Protocol.prepared_from.base_tree -Label 'Protocol base'

    Assert-ExactProperties -Value $Protocol.reference_identity -Expected @(
        'name', 'author', 'historical_post_id', 'historical_post_url', 'interactive_demo_url',
        'user_reference_observed_at_utc', 'state_model', 'public_commit_or_version_identified'
    ) -Label 'protocol.reference_identity'
    Assert-Condition -Condition ($Protocol.reference_identity.name -ceq 'Three.js Grassworks') -Message 'Reference name mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.author -ceq 'Bilal Khan') -Message 'Reference author mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.historical_post_id -ceq '2093382978105966827') -Message 'Historical post id mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.historical_post_url -ceq 'https://x.com/Bk23544/status/2093382978105966827') -Message 'Historical post URL mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.interactive_demo_url -ceq 'https://grassworks.techredux.co/demo') -Message 'Interactive demo URL mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.user_reference_observed_at_utc -ceq '2026-08-29T20:18:42Z') -Message 'Historical user observation timestamp mismatch.'
    Assert-Condition -Condition ($Protocol.reference_identity.state_model -ceq 'historical_identity_with_current_mutable_visual_samples') -Message 'Reference state model mismatch.'
    Assert-ExactBoolean -Value $Protocol.reference_identity.public_commit_or_version_identified -Expected $false -Label 'protocol.reference_identity.public_commit_or_version_identified'

    Assert-JsonArray -Value $Protocol.roots -Label 'protocol.roots'
    $expectedRoots = @{
        grassworks_external_current_20260903 = @('external_reference_observation', 'GrassworksExternalRoot', $false, $true, $false)
        forge_external_visual_125ba0b = @('local_forge_evidence', 'ForgeExternalRoot', $false, $true, $true)
    }
    $rootMap = New-OrdinalMap
    foreach ($root in @($Protocol.roots)) {
        Assert-ExactProperties -Value $root -Expected @('id', 'kind', 'required_parameter', 'tracked_in_forge', 'review_in_place_only', 'redistribution_authorized') -Label 'protocol.roots[]'
        Assert-NonEmptyString -Value $root.id -Label 'protocol.roots[].id'
        Assert-Condition -Condition $expectedRoots.ContainsKey([string]$root.id) -Message "Unknown root '$($root.id)'."
        Assert-Condition -Condition $rootMap.TryAdd([string]$root.id, $root) -Message "Duplicate root '$($root.id)'."
        $expected = $expectedRoots[[string]$root.id]
        Assert-Condition -Condition ($root.kind -ceq $expected[0] -and $root.required_parameter -ceq $expected[1]) -Message "Root metadata mismatch for '$($root.id)'."
        Assert-ExactBoolean -Value $root.tracked_in_forge -Expected ([bool]$expected[2]) -Label "root[$($root.id)].tracked_in_forge"
        Assert-ExactBoolean -Value $root.review_in_place_only -Expected ([bool]$expected[3]) -Label "root[$($root.id)].review_in_place_only"
        Assert-ExactBoolean -Value $root.redistribution_authorized -Expected ([bool]$expected[4]) -Label "root[$($root.id)].redistribution_authorized"
    }
    Assert-Condition -Condition ($rootMap.Count -eq 2) -Message 'Protocol must declare exactly two roots.'

    $expectedArtifacts = @{
        grassworks_high_sunny_current = @('grassworks_external_current_20260903', 'grassworks-high-sunny-1920x1080.jpg', 'image/jpeg', 456471L, '96e5e96777ed63f778db989f0e77cb641618c0fe4bf7f401c9f9d1f1a73848d5', 1920, 1080, 'current_mutable_live_reference', $true)
        grassworks_high_rain_current = @('grassworks_external_current_20260903', 'grassworks-high-rain-1920x1080.jpg', 'image/jpeg', 379020L, '17703bcc42db55ea49b1caea5d804de9aee7f39c7470363fe8a470cc5a97f0d0', 1920, 1080, 'current_mutable_live_reference', $true)
        grassworks_historical_post_current_render = @('grassworks_external_current_20260903', 'x-post-2093382978105966827-with-video-frame-1905x1072.jpg', 'image/jpeg', 128035L, '43be0cebbaf0bd7d58dbbe08de02c7b60932383133580146f95b1d75db7d5a3c', 1905, 1072, 'historical_identity_context_current_render_not_source_media', $false)
        forge_human_clear_125ba0b = @('forge_external_visual_125ba0b', 'captures/human-clear.png', 'image/png', 2083983L, 'b144fe5b4b428f25467ddcb9e6c4060c8e027eb9ef81755aa710c2e3e62d7b42', 1920, 1080, 'fixed_forge_capture_125ba0b', $true)
        forge_distant_clear_125ba0b = @('forge_external_visual_125ba0b', 'captures/distant-clear.png', 'image/png', 2030378L, 'df3d5693d0dcd3120a4407cd17283dbe06dfb8fd5da9ec6c6185fd6e47185e8e', 1920, 1080, 'fixed_forge_capture_125ba0b', $true)
        forge_water_clear_125ba0b = @('forge_external_visual_125ba0b', 'captures/water-clear.png', 'image/png', 2138344L, 'ed03bfee3eaaecaf1af27cbe03e9af4cb4ad116157d99bfe37b8b6fc5f2b0cbc', 1920, 1080, 'fixed_forge_capture_125ba0b', $true)
        forge_water_rain_125ba0b = @('forge_external_visual_125ba0b', 'captures/water-rain.png', 'image/png', 2177341L, '5a97f46289469c0f33a7c011b73eb59e209c4400b7068b323279667d2b284506', 1920, 1080, 'fixed_forge_capture_125ba0b', $true)
    }
    Assert-JsonArray -Value $Protocol.artifacts -Label 'protocol.artifacts'
    $artifactMap = New-OrdinalMap
    foreach ($artifact in @($Protocol.artifacts)) {
        Assert-ExactProperties -Value $artifact -Expected @('id', 'root_id', 'relative_path', 'media_type', 'bytes', 'sha256', 'width', 'height', 'observation_role', 'eligible_for_axis_review') -Label 'protocol.artifacts[]'
        Assert-NonEmptyString -Value $artifact.id -Label 'protocol.artifacts[].id'
        Assert-Condition -Condition $expectedArtifacts.ContainsKey([string]$artifact.id) -Message "Unknown artifact '$($artifact.id)'."
        Assert-Condition -Condition $artifactMap.TryAdd([string]$artifact.id, $artifact) -Message "Duplicate artifact '$($artifact.id)'."
        $expected = $expectedArtifacts[[string]$artifact.id]
        Assert-Condition -Condition ($artifact.root_id -ceq $expected[0]) -Message "Artifact root mismatch for '$($artifact.id)'."
        Assert-Condition -Condition (Test-PortableRelativePath -Value $artifact.relative_path) -Message "Artifact '$($artifact.id)' path is not portable and relative."
        Assert-Condition -Condition ($artifact.relative_path -ceq $expected[1]) -Message "Artifact path mismatch for '$($artifact.id)'."
        Assert-Condition -Condition ($artifact.media_type -ceq $expected[2]) -Message "Artifact media type mismatch for '$($artifact.id)'."
        Assert-Condition -Condition ([long]$artifact.bytes -eq [long]$expected[3]) -Message "Artifact byte length mismatch for '$($artifact.id)'."
        Assert-HexDigest -Value $artifact.sha256 -Length 64 -Label "artifact[$($artifact.id)].sha256"
        Assert-Condition -Condition ($artifact.sha256 -ceq $expected[4]) -Message "Artifact hash mismatch for '$($artifact.id)'."
        Assert-Condition -Condition ([int]$artifact.width -eq [int]$expected[5] -and [int]$artifact.height -eq [int]$expected[6]) -Message "Artifact dimensions mismatch for '$($artifact.id)'."
        Assert-Condition -Condition ($artifact.observation_role -ceq $expected[7]) -Message "Artifact role mismatch for '$($artifact.id)'."
        Assert-ExactBoolean -Value $artifact.eligible_for_axis_review -Expected ([bool]$expected[8]) -Label "artifact[$($artifact.id)].eligible_for_axis_review"
        Assert-Condition -Condition $rootMap.ContainsKey([string]$artifact.root_id) -Message "Artifact '$($artifact.id)' references an unknown root."
    }
    Assert-Condition -Condition ($artifactMap.Count -eq 7) -Message 'Protocol must declare exactly seven artifacts.'

    $expectedPanels = @{
        sunny_human_height = @('human_height_ground_cover', @('grassworks_high_sunny_current'), @('forge_human_clear_125ba0b'), @('density', 'naturalness', 'depth', 'lighting', 'finish'))
        sunny_landscape_depth = @('landscape_depth_and_composition', @('grassworks_high_sunny_current'), @('forge_distant_clear_125ba0b'), @('density', 'naturalness', 'depth', 'lighting', 'finish'))
        rain_atmosphere_and_water = @('rain_weather_water_and_banks', @('grassworks_high_rain_current'), @('forge_water_rain_125ba0b'), @('naturalness', 'depth', 'lighting', 'finish'))
        clear_to_rain_response = @('semantic_weather_response_not_temporal_sync', @('grassworks_high_sunny_current', 'grassworks_high_rain_current'), @('forge_water_clear_125ba0b', 'forge_water_rain_125ba0b'), @('naturalness', 'lighting', 'finish'))
    }
    Assert-JsonArray -Value $Protocol.comparison_panels -Label 'protocol.comparison_panels'
    $panelMap = New-OrdinalMap
    foreach ($panel in @($Protocol.comparison_panels)) {
        Assert-ExactProperties -Value $panel -Expected @('id', 'framing_class', 'grassworks_artifact_ids', 'forge_artifact_ids', 'review_axes', 'semantic_equivalence_only', 'direct_pixel_comparison_allowed', 'human_axis_verdict_required') -Label 'protocol.comparison_panels[]'
        Assert-Condition -Condition $expectedPanels.ContainsKey([string]$panel.id) -Message "Unknown comparison panel '$($panel.id)'."
        Assert-Condition -Condition $panelMap.TryAdd([string]$panel.id, $panel) -Message "Duplicate comparison panel '$($panel.id)'."
        $expected = $expectedPanels[[string]$panel.id]
        Assert-Condition -Condition ($panel.framing_class -ceq $expected[0]) -Message "Framing class mismatch for panel '$($panel.id)'."
        Assert-ExactStringArray -Actual $panel.grassworks_artifact_ids -Expected ([string[]]$expected[1]) -Label "panel[$($panel.id)].grassworks_artifact_ids"
        Assert-ExactStringArray -Actual $panel.forge_artifact_ids -Expected ([string[]]$expected[2]) -Label "panel[$($panel.id)].forge_artifact_ids"
        Assert-ExactStringArray -Actual $panel.review_axes -Expected ([string[]]$expected[3]) -Label "panel[$($panel.id)].review_axes"
        Assert-ExactBoolean -Value $panel.semantic_equivalence_only -Expected $true -Label "panel[$($panel.id)].semantic_equivalence_only"
        Assert-ExactBoolean -Value $panel.direct_pixel_comparison_allowed -Expected $false -Label "panel[$($panel.id)].direct_pixel_comparison_allowed"
        Assert-ExactBoolean -Value $panel.human_axis_verdict_required -Expected $true -Label "panel[$($panel.id)].human_axis_verdict_required"
        foreach ($artifactId in @($panel.grassworks_artifact_ids) + @($panel.forge_artifact_ids)) {
            Assert-Condition -Condition $artifactMap.ContainsKey([string]$artifactId) -Message "Panel '$($panel.id)' references unknown artifact '$artifactId'."
            Assert-ExactBoolean -Value $artifactMap[[string]$artifactId].eligible_for_axis_review -Expected $true -Label "panel[$($panel.id)].artifact[$artifactId].eligible_for_axis_review"
        }
    }
    Assert-Condition -Condition ($panelMap.Count -eq 4) -Message 'Protocol must declare exactly four comparison panels.'

    Assert-JsonArray -Value $Protocol.context_bindings -Label 'protocol.context_bindings'
    Assert-Condition -Condition (@($Protocol.context_bindings).Count -eq 1) -Message 'Protocol must declare exactly one context binding.'
    $context = @($Protocol.context_bindings)[0]
    Assert-ExactProperties -Value $context -Expected @('id', 'artifact_id', 'purpose', 'axis_verdict_eligible', 'cannot_substitute_for') -Label 'protocol.context_bindings[0]'
    Assert-Condition -Condition ($context.id -ceq 'historical_post_identity_only') -Message 'Context binding id mismatch.'
    Assert-Condition -Condition ($context.artifact_id -ceq 'grassworks_historical_post_current_render') -Message 'Context artifact mismatch.'
    Assert-Condition -Condition ($context.purpose -ceq 'bind_current_render_to_historical_post_identity') -Message 'Context purpose mismatch.'
    Assert-ExactBoolean -Value $context.axis_verdict_eligible -Expected $false -Label 'protocol.context_bindings[0].axis_verdict_eligible'
    Assert-ExactStringArray -Actual $context.cannot_substitute_for -Expected @('contemporaneous_2026_08_29_capture', 'source_video_bytes', 'source_video_sha256') -Label 'protocol.context_bindings[0].cannot_substitute_for'

    $expectedAxes = @{
        density = @('densité', @('sunny_human_height', 'sunny_landscape_depth'))
        naturalness = @('naturel', @('sunny_human_height', 'sunny_landscape_depth', 'rain_atmosphere_and_water', 'clear_to_rain_response'))
        depth = @('profondeur', @('sunny_human_height', 'sunny_landscape_depth', 'rain_atmosphere_and_water'))
        lighting = @('lumière', @('sunny_human_height', 'sunny_landscape_depth', 'rain_atmosphere_and_water', 'clear_to_rain_response'))
        finish = @('finition', @('sunny_human_height', 'sunny_landscape_depth', 'rain_atmosphere_and_water', 'clear_to_rain_response'))
    }
    Assert-JsonArray -Value $Protocol.review_axes -Label 'protocol.review_axes'
    Assert-ExactStringArray -Actual @($Protocol.review_axes | ForEach-Object { [string]$_.id }) -Expected @('density', 'naturalness', 'depth', 'lighting', 'finish') -Label 'protocol.review_axes ids'
    foreach ($axis in @($Protocol.review_axes)) {
        Assert-ExactProperties -Value $axis -Expected @('id', 'label_fr', 'required_panel_ids', 'question_fr') -Label 'protocol.review_axes[]'
        Assert-Condition -Condition $expectedAxes.ContainsKey([string]$axis.id) -Message "Unknown review axis '$($axis.id)'."
        $expected = $expectedAxes[[string]$axis.id]
        Assert-Condition -Condition ($axis.label_fr -ceq $expected[0]) -Message "French label mismatch for axis '$($axis.id)'."
        Assert-ExactStringArray -Actual $axis.required_panel_ids -Expected ([string[]]$expected[1]) -Label "axis[$($axis.id)].required_panel_ids"
        Assert-NonEmptyString -Value $axis.question_fr -Label "axis[$($axis.id)].question_fr"
        Assert-Condition -Condition (-not ([string]$axis.question_fr -match '(?i)\b(?:todo|tbd|placeholder)\b')) -Message "Axis '$($axis.id)' question contains a placeholder."
        foreach ($panelId in @($axis.required_panel_ids)) {
            Assert-Condition -Condition $panelMap.ContainsKey([string]$panelId) -Message "Axis '$($axis.id)' references unknown panel '$panelId'."
            Assert-Condition -Condition (@($panelMap[[string]$panelId].review_axes) -ccontains [string]$axis.id) -Message "Axis '$($axis.id)' is absent from required panel '$panelId'."
        }
    }

    Assert-ExactProperties -Value $Protocol.review_contract -Expected @(
        'reviewer_kind', 'allowed_verdicts', 'observation_per_axis_required',
        'blocking_issue_required_for', 'external_hash_verification_required',
        'protocol_sha256_binding_required_for_future_receipt', 'source_limitations_acknowledgement_required',
        'protocol_may_close_r1_051', 'protocol_may_authorize_gate1_transition', 'current_human_verdict'
    ) -Label 'protocol.review_contract'
    Assert-Condition -Condition ($Protocol.review_contract.reviewer_kind -ceq 'human_owner') -Message 'Reviewer kind must remain human_owner.'
    Assert-ExactStringArray -Actual $Protocol.review_contract.allowed_verdicts -Expected @('accepted', 'rejected', 'inconclusive') -Label 'protocol.review_contract.allowed_verdicts'
    Assert-ExactStringArray -Actual $Protocol.review_contract.blocking_issue_required_for -Expected @('rejected', 'inconclusive') -Label 'protocol.review_contract.blocking_issue_required_for'
    Assert-ExactBoolean -Value $Protocol.review_contract.observation_per_axis_required -Expected $true -Label 'protocol.review_contract.observation_per_axis_required'
    Assert-ExactBoolean -Value $Protocol.review_contract.external_hash_verification_required -Expected $true -Label 'protocol.review_contract.external_hash_verification_required'
    Assert-ExactBoolean -Value $Protocol.review_contract.protocol_sha256_binding_required_for_future_receipt -Expected $true -Label 'protocol.review_contract.protocol_sha256_binding_required_for_future_receipt'
    Assert-ExactBoolean -Value $Protocol.review_contract.source_limitations_acknowledgement_required -Expected $true -Label 'protocol.review_contract.source_limitations_acknowledgement_required'
    Assert-ExactBoolean -Value $Protocol.review_contract.protocol_may_close_r1_051 -Expected $false -Label 'protocol.review_contract.protocol_may_close_r1_051'
    Assert-ExactBoolean -Value $Protocol.review_contract.protocol_may_authorize_gate1_transition -Expected $false -Label 'protocol.review_contract.protocol_may_authorize_gate1_transition'
    Assert-Condition -Condition ($null -eq $Protocol.review_contract.current_human_verdict) -Message 'Protocol must not contain a human verdict.'

    Assert-ExactProperties -Value $Protocol.boundaries -Expected @(
        'historical_post_rendered_at_protocol_capture_time', 'contemporaneous_2026_08_29_capture_available',
        'source_video_bytes_available', 'source_video_sha256_available', 'live_demo_mutable',
        'public_version_or_commit_available', 'direct_pixel_comparison_allowed',
        'grassworks_media_copied_into_forge', 'grassworks_assets_copied_into_forge',
        'grassworks_code_or_bundles_retained', 'cadle_assets_present', 'human_review_performed'
    ) -Label 'protocol.boundaries'
    Assert-ExactBoolean -Value $Protocol.boundaries.historical_post_rendered_at_protocol_capture_time -Expected $true -Label 'protocol.boundaries.historical_post_rendered_at_protocol_capture_time'
    foreach ($name in @('contemporaneous_2026_08_29_capture_available', 'source_video_bytes_available', 'source_video_sha256_available', 'public_version_or_commit_available', 'direct_pixel_comparison_allowed', 'grassworks_media_copied_into_forge', 'grassworks_assets_copied_into_forge', 'grassworks_code_or_bundles_retained', 'cadle_assets_present', 'human_review_performed')) {
        Assert-ExactBoolean -Value $Protocol.boundaries.$name -Expected $false -Label "protocol.boundaries.$name"
    }
    Assert-ExactBoolean -Value $Protocol.boundaries.live_demo_mutable -Expected $true -Label 'protocol.boundaries.live_demo_mutable'

    Assert-ExactProperties -Value $Protocol.qualification -Expected @('machine_protocol_defined', 'external_artifact_hashes_required', 'technically_ready_for_future_human_package_integration', 'reference_snapshot_reproducible', 'human_review_performed', 'r1_051_closed', 'gate1_transition_authorized') -Label 'protocol.qualification'
    foreach ($name in @('machine_protocol_defined', 'external_artifact_hashes_required', 'technically_ready_for_future_human_package_integration')) {
        Assert-ExactBoolean -Value $Protocol.qualification.$name -Expected $true -Label "protocol.qualification.$name"
    }
    foreach ($name in @('reference_snapshot_reproducible', 'human_review_performed', 'r1_051_closed', 'gate1_transition_authorized')) {
        Assert-ExactBoolean -Value $Protocol.qualification.$name -Expected $false -Label "protocol.qualification.$name"
    }

    return [pscustomobject]@{
        RootMap = $rootMap
        ArtifactMap = $artifactMap
        PanelMap = $panelMap
    }
}

function Assert-ExternalEvidence {
    param(
        [Parameter(Mandatory = $true)][object]$Protocol,
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$GrassworksRoot,
        [Parameter(Mandatory = $true)][string]$ForgeRoot
    )

    $repositoryFull = [IO.Path]::GetFullPath($Repository)
    $grassworksFull = [IO.Path]::GetFullPath($GrassworksRoot)
    $forgeFull = [IO.Path]::GetFullPath($ForgeRoot)
    Assert-Condition -Condition (Test-Path -LiteralPath $grassworksFull -PathType Container) -Message "GrassworksExternalRoot is missing: $grassworksFull"
    Assert-Condition -Condition (Test-Path -LiteralPath $forgeFull -PathType Container) -Message "ForgeExternalRoot is missing: $forgeFull"
    Assert-Condition -Condition (-not (Test-RootsOverlap -First $repositoryFull -Second $grassworksFull)) -Message 'GrassworksExternalRoot must remain outside and disjoint from RepositoryRoot.'
    Assert-Condition -Condition (-not (Test-RootsOverlap -First $repositoryFull -Second $forgeFull)) -Message 'ForgeExternalRoot must remain outside and disjoint from RepositoryRoot.'
    Assert-Condition -Condition (-not (Test-RootsOverlap -First $grassworksFull -Second $forgeFull)) -Message 'GrassworksExternalRoot and ForgeExternalRoot must be disjoint.'
    Assert-NoReparsePointBetween -Path $grassworksFull -Root $grassworksFull -Label 'GrassworksExternalRoot'
    Assert-NoReparsePointBetween -Path $forgeFull -Root $forgeFull -Label 'ForgeExternalRoot'

    $resolvedRoots = @{
        grassworks_external_current_20260903 = $grassworksFull
        forge_external_visual_125ba0b = $forgeFull
    }
    $grassworksCount = 0
    $forgeCount = 0
    foreach ($artifact in @($Protocol.artifacts)) {
        Assert-ImageArtifact -Artifact $artifact -Root $resolvedRoots[[string]$artifact.root_id]
        if ($artifact.root_id -ceq 'grassworks_external_current_20260903') {
            $grassworksCount++
        }
        else {
            $forgeCount++
        }
    }
    Assert-Condition -Condition ($grassworksCount -eq 3 -and $forgeCount -eq 4) -Message 'External artifact counts differ from the protocol.'

    $grassworksHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($artifact in @($Protocol.artifacts | Where-Object { $_.root_id -ceq 'grassworks_external_current_20260903' })) {
        [void]$grassworksHashes.Add([string]$artifact.sha256)
    }
    $trackedFiles = @((Get-GitText -Repository $repositoryFull -Arguments @('ls-files') -Label 'Tracked file inventory') -split "`n")
    foreach ($relative in $trackedFiles) {
        if ([string]::IsNullOrWhiteSpace($relative)) {
            continue
        }
        $tracked = Join-Path $repositoryFull $relative
        if (Test-Path -LiteralPath $tracked -PathType Leaf) {
            $trackedHash = Get-LowerSha256 -Path $tracked
            Assert-Condition -Condition (-not $grassworksHashes.Contains($trackedHash)) -Message "Tracked Forge file duplicates Grassworks media bytes: $relative"
        }
    }

    return [pscustomobject]@{
        GrassworksArtifacts = $grassworksCount
        ForgeArtifacts = $forgeCount
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

function Invoke-ProtocolSelfTest {
    param(
        [Parameter(Mandatory = $true)][object]$Protocol,
        [Parameter(Mandatory = $true)][string]$Repository
    )

    [void](Assert-Protocol -Protocol $Protocol -Repository $Repository)
    $cases = 1

    $mutations = @(
        @('schema version', { param($value) $value.schema_version = 2 }),
        @('qualified status', { param($value) $value.status = 'qualified' }),
        @('wrong route', { param($value) $value.route = 'R1-052' }),
        @('wrong base commit', { param($value) $value.prepared_from.base_commit = '0' * 40 }),
        @('tracked external root', { param($value) $value.roots[0].tracked_in_forge = $true }),
        @('absolute artifact path', { param($value) $value.artifacts[0].relative_path = 'C:/evidence/sample.jpg' }),
        @('artifact traversal', { param($value) $value.artifacts[0].relative_path = '../sample.jpg' }),
        @('artifact hash', { param($value) $value.artifacts[0].sha256 = '0' * 64 }),
        @('post eligible for verdict', { param($value) $value.artifacts[2].eligible_for_axis_review = $true }),
        @('direct pixel comparison', { param($value) $value.comparison_panels[0].direct_pixel_comparison_allowed = $true }),
        @('unknown panel artifact', { param($value) $value.comparison_panels[0].forge_artifact_ids[0] = 'unknown' }),
        @('missing review axis', { param($value) $value.review_axes = @($value.review_axes | Select-Object -First 4) }),
        @('context verdict eligible', { param($value) $value.context_bindings[0].axis_verdict_eligible = $true }),
        @('invented human verdict', { param($value) $value.review_contract.current_human_verdict = 'accepted' }),
        @('source media claimed', { param($value) $value.boundaries.source_video_bytes_available = $true }),
        @('human review claimed', { param($value) $value.boundaries.human_review_performed = $true }),
        @('route closed', { param($value) $value.qualification.r1_051_closed = $true }),
        @('Gate 1 authorized', { param($value) $value.qualification.gate1_transition_authorized = $true })
    )
    foreach ($mutation in $mutations) {
        $candidate = Copy-JsonObject -Value $Protocol
        & $mutation[1] $candidate
        Assert-Throws -Label ([string]$mutation[0]) -Action { Assert-Protocol -Protocol $candidate -Repository $Repository }
        $cases++
    }

    $duplicateArtifact = Copy-JsonObject -Value $Protocol
    $duplicateArtifact.artifacts = @($duplicateArtifact.artifacts) + @((Copy-JsonObject -Value $duplicateArtifact.artifacts[0]))
    Assert-Throws -Label 'duplicate artifact' -Action { Assert-Protocol -Protocol $duplicateArtifact -Repository $Repository }
    $cases++

    Assert-Throws -Label 'duplicate JSON property' -Action { ConvertFrom-StrictJson -Text '{"schema":1,"schema":2}' -Label 'duplicate self-test' }
    $cases++
    Assert-Throws -Label 'direct traversal resolver' -Action { Resolve-SafeChildPath -Root $Repository -RelativePath '../escape.jpg' -Label 'self-test traversal' }
    $cases++
    Assert-Throws -Label 'external root overlaps repository' -Action {
        Assert-Condition -Condition (-not (Test-RootsOverlap -First $Repository -Second $Repository)) -Message 'Synthetic overlap must be rejected.'
    }
    $cases++

    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $tempRoot = Join-Path $tempBase ('forge-r1-051-selftest-' + [IO.Path]::GetRandomFileName())
    [void][IO.Directory]::CreateDirectory($tempRoot)
    try {
        $probe = Join-Path $tempRoot 'probe.bin'
        [IO.File]::WriteAllBytes($probe, [byte[]](1, 2, 3, 4))
        $probeHash = Get-LowerSha256 -Path $probe
        Assert-FileSizeAndHash -Path $probe -ExpectedBytes 4 -ExpectedSha256 $probeHash -Label 'self-test hash probe'
        [IO.File]::WriteAllBytes($probe, [byte[]](1, 2, 3, 5))
        Assert-Throws -Label 'tampered external bytes' -Action { Assert-FileSizeAndHash -Path $probe -ExpectedBytes 4 -ExpectedSha256 $probeHash -Label 'self-test tampered hash probe' }
        $cases++
    }
    finally {
        $resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
        Assert-Condition -Condition $resolvedTempRoot.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -Message 'Self-test cleanup target escaped the system temp directory.'
        if (Test-Path -LiteralPath $resolvedTempRoot) {
            Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
        }
    }

    Write-Output "FORGE_GATE1_R1_051_PROTOCOL_SELF_TEST_PASS cases=$cases synthetic_only=true human_review_recorded=false routes_closed=0"
}

$repositoryFull = [IO.Path]::GetFullPath($RepositoryRoot)
Assert-Condition -Condition (Test-Path -LiteralPath (Join-Path $repositoryFull '.git')) -Message "RepositoryRoot is not a Git worktree: $repositoryFull"
if ([string]::IsNullOrWhiteSpace($ProtocolPath)) {
    $ProtocolPath = Join-Path $repositoryFull 'docs/evidence/gate1/r1-051-grassworks-forge-semantic-protocol.v1.json'
}
$protocolFull = if ([IO.Path]::IsPathRooted($ProtocolPath)) {
    [IO.Path]::GetFullPath($ProtocolPath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryFull $ProtocolPath))
}
Assert-Condition -Condition (Test-Path -LiteralPath $protocolFull -PathType Leaf) -Message "R1-051 protocol is missing: $protocolFull"

$repositoryPrefix = $repositoryFull.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
Assert-Condition -Condition $protocolFull.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -Message 'ProtocolPath must be located inside RepositoryRoot.'

$protocolText = Get-Content -Raw -LiteralPath $protocolFull
$protocol = ConvertFrom-StrictJson -Text $protocolText -Label 'R1-051 protocol'
$protocolHash = Get-LowerSha256 -Path $protocolFull
Assert-Condition -Condition ($protocolHash -ceq $script:ExpectedProtocolSha256) -Message 'R1-051 protocol differs from the validator-pinned SHA-256.'
$state = Assert-Protocol -Protocol $protocol -Repository $repositoryFull

Write-Output (
    'FORGE_GATE1_R1_051_PROTOCOL_PASS sha256={0} artifacts={1} panels={2} axes=5 human_review=false r1_051_closed=false gate1_transition=false' -f
    $protocolHash, $state.ArtifactMap.Count, $state.PanelMap.Count
)

if ($VerifyExternal) {
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($GrassworksExternalRoot)) -Message '-VerifyExternal requires -GrassworksExternalRoot.'
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($ForgeExternalRoot)) -Message '-VerifyExternal requires -ForgeExternalRoot.'
    Add-Type -AssemblyName System.Drawing
    $external = Assert-ExternalEvidence -Protocol $protocol -Repository $repositoryFull -GrassworksRoot $GrassworksExternalRoot -ForgeRoot $ForgeExternalRoot
    Write-Output (
        'FORGE_GATE1_R1_051_EXTERNAL_PASS grassworks={0} forge={1} media_copied_into_forge=0 cadle_assets=0' -f
        $external.GrassworksArtifacts, $external.ForgeArtifacts
    )
}
elseif (-not [string]::IsNullOrWhiteSpace($GrassworksExternalRoot) -or -not [string]::IsNullOrWhiteSpace($ForgeExternalRoot)) {
    throw 'External roots require -VerifyExternal.'
}

if ($SelfTest) {
    Invoke-ProtocolSelfTest -Protocol $protocol -Repository $repositoryFull
}
