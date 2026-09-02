[CmdletBinding(DefaultParameterSetName = "Paths")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Paths")]
    [ValidateCount(6, 6)]
    [string[]]$SummaryPath,

    [Parameter(Mandatory = $true, ParameterSetName = "Manifest")]
    [ValidateNotNullOrEmpty()]
    [string]$CampaignRoot,

    [Parameter(Mandatory = $true, ParameterSetName = "Manifest")]
    [ValidateNotNullOrEmpty()]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true, ParameterSetName = "Paths")]
    [Parameter(Mandatory = $true, ParameterSetName = "Manifest")]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath,

    [Parameter(Mandatory = $true, ParameterSetName = "SelfTest")]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:ExpectedRoutes = @(
    "auto_no_vsync",
    "fifo_diagnostic",
    "fifo_diagnostic",
    "auto_no_vsync",
    "auto_no_vsync",
    "fifo_diagnostic"
)
$script:ComparisonFields = @(
    "instrumentation",
    "governed_scene",
    "scene_hashes",
    "gpu",
    "driver",
    "operating_system",
    "display"
)

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ($null -eq $InputObject) {
        throw "$Path is null; required property '$Name' is absent."
    }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Path.$Name is absent."
    }
    return $property.Value
}

function Get-OptionalProperty {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $InputObject) {
        return [pscustomobject]@{ Present = $false; Value = $null }
    }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return [pscustomobject]@{ Present = $false; Value = $null }
    }
    return [pscustomobject]@{ Present = $true; Value = $property.Value }
}

function Get-RequiredString {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Path $Path
    Assert-Condition -Condition ($value -is [string] -and -not [string]::IsNullOrWhiteSpace($value)) -Message "$Path.$Name must be a non-empty string."
    return [string]$value
}

function Get-RequiredBoolean {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Path $Path
    Assert-Condition -Condition ($value -is [bool]) -Message "$Path.$Name must be a JSON boolean."
    return [bool]$value
}

function Get-RequiredDouble {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path,
        [double]$Minimum = 0.0
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Path $Path
    Assert-Condition -Condition ($value -is [ValueType] -and $value -isnot [bool]) -Message "$Path.$Name must be numeric."
    $number = [double]$value
    Assert-Condition -Condition ([double]::IsFinite($number) -and $number -ge $Minimum) -Message "$Path.$Name must be finite and at least $Minimum."
    return $number
}

function Get-RequiredUInt64 {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $value = Get-RequiredProperty -InputObject $InputObject -Name $Name -Path $Path
    Assert-Condition -Condition ($value -is [ValueType] -and $value -isnot [bool]) -Message "$Path.$Name must be an unsigned integer."
    try {
        $number = [uint64]$value
    }
    catch {
        throw "$Path.$Name must be an unsigned integer."
    }
    Assert-Condition -Condition ([decimal]$number -eq [decimal]$value) -Message "$Path.$Name must be an unsigned integer."
    return $number
}

function ConvertTo-CanonicalValue {
    param([Parameter(Mandatory = $true)][AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [string] -or $Value -is [ValueType]) {
        return $Value
    }
    if ($Value -is [Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in @($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
            $ordered[$key] = ConvertTo-CanonicalValue -Value $Value[$key]
        }
        return [pscustomobject]$ordered
    }
    if ($Value -is [Collections.IEnumerable]) {
        $items = @()
        foreach ($item in $Value) {
            $items += , (ConvertTo-CanonicalValue -Value $item)
        }
        return , $items
    }

    $properties = @($Value.PSObject.Properties | Where-Object { $_.MemberType -in @("NoteProperty", "Property", "AliasProperty") })
    if ($properties.Count -gt 0) {
        $ordered = [ordered]@{}
        foreach ($property in @($properties | Sort-Object -Property Name -CaseSensitive)) {
            $ordered[$property.Name] = ConvertTo-CanonicalValue -Value $property.Value
        }
        return [pscustomobject]$ordered
    }
    return [string]$Value
}

function ConvertTo-CanonicalJson {
    param([Parameter(Mandatory = $true)][AllowNull()][object]$Value)

    return (ConvertTo-CanonicalValue -Value $Value | ConvertTo-Json -Depth 64 -Compress)
}

function Get-RunMetricStatistics {
    param(
        [Parameter(Mandatory = $true)][double[]]$Values,
        [Parameter(Mandatory = $true)][ValidateSet("higher", "lower")][string]$Better
    )

    Assert-Condition -Condition ($Values.Count -eq 3) -Message "Run-level mode statistics require exactly three values."
    $sorted = [double[]]@($Values | Sort-Object)
    return [pscustomobject][ordered]@{
        unit = "run"
        run_count = 3
        minimum = $sorted[0]
        median = $sorted[1]
        maximum = $sorted[2]
        direction_of_improvement = $Better
        worst = if ($Better -eq "higher") { $sorted[0] } else { $sorted[2] }
    }
}

function New-ComparableMap {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$Instrumentation,
        [Parameter(Mandatory = $true)][AllowNull()][object]$GovernedScene,
        [Parameter(Mandatory = $true)][AllowNull()][object]$SceneHashes,
        [Parameter(Mandatory = $true)][AllowNull()][object]$Gpu,
        [Parameter(Mandatory = $true)][AllowNull()][object]$Driver,
        [Parameter(Mandatory = $true)][AllowNull()][object]$OperatingSystem,
        [Parameter(Mandatory = $true)][AllowNull()][object]$Display
    )

    $map = [ordered]@{}
    foreach ($entry in @(
            @("instrumentation", $Instrumentation),
            @("governed_scene", $GovernedScene),
            @("scene_hashes", $SceneHashes),
            @("gpu", $Gpu),
            @("driver", $Driver),
            @("operating_system", $OperatingSystem),
            @("display", $Display)
        )) {
        if ($null -ne $entry[1]) {
            $map[$entry[0]] = $entry[1]
        }
    }
    return $map
}

function Test-ComparableFieldPresent {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$Map,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Map) {
        return $false
    }
    if ($Map -is [Collections.IDictionary]) {
        return $Map.Contains($Name)
    }
    return $null -ne $Map.PSObject.Properties[$Name]
}

function Get-ComparableFieldValue {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$Map,
        [Parameter(Mandatory = $true)][string]$Name
    )

    Assert-Condition -Condition (Test-ComparableFieldPresent -Map $Map -Name $Name) -Message "Comparable field '$Name' is absent."
    if ($Map -is [Collections.IDictionary]) {
        return $Map[$Name]
    }
    return $Map.PSObject.Properties[$Name].Value
}

function Get-PresentationSignature {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$Signature,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $stable = Get-RequiredBoolean -InputObject $Signature -Name "stable" -Path $Path
    $tuple = [ordered]@{}
    $observedValues = [ordered]@{}
    $singletonTuple = $true
    foreach ($field in @("present_mode_values", "allows_tearing_values", "sync_interval_values", "present_flags_values")) {
        $values = @(Get-RequiredProperty -InputObject $Signature -Name $field -Path $Path)
        Assert-Condition -Condition ($values.Count -gt 0) -Message "$Path.$field must contain at least one observed value."
        $texts = @($values | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive -Unique)
        Assert-Condition -Condition (@($texts | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) -Message "$Path.$field contains an empty value."
        $observedValues[$field] = $texts
        if ($texts.Count -ne 1) {
            $singletonTuple = $false
        }
        else {
            $tuple[$field] = $texts[0]
        }
    }
    $tupleEligible = $stable -and $singletonTuple
    return [pscustomobject][ordered]@{
        stable = $stable
        singleton_tuple = $singletonTuple
        observed_values = [pscustomobject]$observedValues
        tuple = if ($tupleEligible) { [pscustomobject]$tuple } else { $null }
        tuple_key = if ($tupleEligible) { ConvertTo-CanonicalJson -Value ([pscustomobject]$tuple) } else { $null }
    }
}

function Test-HexSha256 {
    param([Parameter(Mandatory = $true)][string]$Value)

    return $Value -cmatch '^[0-9a-f]{64}$'
}

function ConvertFrom-Gate1DiagnosticSummary {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$Summary,
        [Parameter(Mandatory = $true)][int]$Ordinal,
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$SourceSha256
    )

    $rootPath = "summary[$Ordinal]"
    Assert-Condition -Condition ((Get-RequiredString -InputObject $Summary -Name "schema" -Path $rootPath) -eq "forge.gate1-shared-binary-diagnostic") -Message "$rootPath has an unexpected schema."
    Assert-Condition -Condition ((Get-RequiredUInt64 -InputObject $Summary -Name "schema_version" -Path $rootPath) -eq 2) -Message "$rootPath has an unexpected schema version."
    Assert-Condition -Condition ((Get-RequiredBoolean -InputObject $Summary -Name "qualifying" -Path $rootPath) -eq $false) -Message "$rootPath falsely claims qualification."
    $status = Get-RequiredString -InputObject $Summary -Name "status" -Path $rootPath
    Assert-Condition -Condition ($status -in @("diagnostic_thresholds_passed", "diagnostic_thresholds_failed")) -Message "$rootPath.status is outside the diagnostic vocabulary."

    $route = Get-RequiredString -InputObject $Summary -Name "presentation_route" -Path $rootPath
    Assert-Condition -Condition ($route -in @("auto_no_vsync", "fifo_diagnostic")) -Message "$rootPath.presentation_route is invalid."
    $commit = Get-RequiredString -InputObject $Summary -Name "repository_commit" -Path $rootPath
    $tree = Get-RequiredString -InputObject $Summary -Name "repository_tree" -Path $rootPath
    Assert-Condition -Condition ($commit -cmatch '^[0-9a-f]{40}$' -and $tree -cmatch '^[0-9a-f]{40}$') -Message "$rootPath repository commit/tree identities are invalid."

    $acquisition = Get-RequiredProperty -InputObject $Summary -Name "acquisition" -Path $rootPath
    $acquisitionUnchanged = Get-RequiredBoolean -InputObject $acquisition -Name "unchanged_during_run" -Path "$rootPath.acquisition"
    $normalizedAcquisition = [ordered]@{}
    foreach ($name in @("helper", "governed_script")) {
        $descriptor = Get-RequiredProperty -InputObject $acquisition -Name $name -Path "$rootPath.acquisition"
        $descriptorPath = Get-RequiredString -InputObject $descriptor -Name "path" -Path "$rootPath.acquisition.$name"
        $descriptorBytes = Get-RequiredUInt64 -InputObject $descriptor -Name "bytes" -Path "$rootPath.acquisition.$name"
        $descriptorSha256 = Get-RequiredString -InputObject $descriptor -Name "sha256" -Path "$rootPath.acquisition.$name"
        Assert-Condition -Condition ([IO.Path]::IsPathRooted($descriptorPath) -and $descriptorBytes -gt 0 -and (Test-HexSha256 -Value $descriptorSha256)) -Message "$rootPath.acquisition.$name descriptor is invalid."
        $normalizedAcquisition[$name] = [pscustomobject][ordered]@{
            path = [IO.Path]::GetFullPath($descriptorPath)
            bytes = $descriptorBytes
            sha256 = $descriptorSha256
        }
    }

    $runtime = Get-RequiredProperty -InputObject $Summary -Name "runtime" -Path $rootPath
    $binaryBefore = Get-RequiredProperty -InputObject $runtime -Name "binary_before" -Path "$rootPath.runtime"
    $binaryAfter = Get-RequiredProperty -InputObject $runtime -Name "binary_after" -Path "$rootPath.runtime"
    $beforeBytes = Get-RequiredUInt64 -InputObject $binaryBefore -Name "bytes" -Path "$rootPath.runtime.binary_before"
    $afterBytes = Get-RequiredUInt64 -InputObject $binaryAfter -Name "bytes" -Path "$rootPath.runtime.binary_after"
    $beforeSha256 = Get-RequiredString -InputObject $binaryBefore -Name "sha256" -Path "$rootPath.runtime.binary_before"
    $afterSha256 = Get-RequiredString -InputObject $binaryAfter -Name "sha256" -Path "$rootPath.runtime.binary_after"
    Assert-Condition -Condition ($beforeBytes -gt 0 -and $afterBytes -gt 0 -and (Test-HexSha256 -Value $beforeSha256) -and (Test-HexSha256 -Value $afterSha256)) -Message "$rootPath runtime binary descriptors are invalid."

    $instrumentation = Get-RequiredProperty -InputObject $runtime -Name "instrumentation" -Path "$rootPath.runtime"
    $governedScene = Get-RequiredProperty -InputObject $runtime -Name "governed_scene" -Path "$rootPath.runtime"
    $nativeWindow = Get-RequiredProperty -InputObject $runtime -Name "native_window" -Path "$rootPath.runtime"
    $gpu = Get-RequiredProperty -InputObject $runtime -Name "gpu" -Path "$rootPath.runtime"
    $operatingSystem = Get-RequiredProperty -InputObject $runtime -Name "operating_system" -Path "$rootPath.runtime"
    $display = Get-RequiredProperty -InputObject $runtime -Name "display" -Path "$rootPath.runtime"
    $sceneHashesOptional = Get-OptionalProperty -InputObject $governedScene -Name "hashes"
    $driverOptional = Get-OptionalProperty -InputObject $gpu -Name "driver"

    $presentMon = Get-RequiredProperty -InputObject $Summary -Name "presentmon" -Path $rootPath
    $qpcWindow = Get-RequiredProperty -InputObject $presentMon -Name "qpc_window" -Path "$rootPath.presentmon"
    $displayProjection = Get-RequiredProperty -InputObject $presentMon -Name "display_projection" -Path "$rootPath.presentmon"
    $etw = Get-RequiredProperty -InputObject $presentMon -Name "etw" -Path "$rootPath.presentmon"
    $signature = Get-PresentationSignature -Signature (Get-RequiredProperty -InputObject $presentMon -Name "presentation_signature" -Path "$rootPath.presentmon") -Path "$rootPath.presentmon.presentation_signature"

    $infrastructureErrors = [Collections.Generic.List[string]]::new()
    if (-not $acquisitionUnchanged) {
        $infrastructureErrors.Add("acquisition helper or governed script changed during the run")
    }
    $exactRuntimeBoundsObserved = Get-RequiredBoolean -InputObject $qpcWindow -Name "exact_runtime_bounds_observed" -Path "$rootPath.presentmon.qpc_window"
    $frequencyMatchesHost = Get-RequiredBoolean -InputObject $qpcWindow -Name "frequency_matches_host" -Path "$rootPath.presentmon.qpc_window"
    if (-not $exactRuntimeBoundsObserved) {
        $infrastructureErrors.Add("runtime QPC bounds were not observed exactly")
    }
    if (-not $frequencyMatchesHost) {
        $infrastructureErrors.Add("runtime/host QPC frequencies differ")
    }
    $displayCoveragePassed = Get-RequiredBoolean -InputObject $displayProjection -Name "coverage_passed" -Path "$rootPath.presentmon.display_projection"
    $coversStart = Get-RequiredBoolean -InputObject $displayProjection -Name "covers_start" -Path "$rootPath.presentmon.display_projection"
    $coversEnd = Get-RequiredBoolean -InputObject $displayProjection -Name "covers_end" -Path "$rootPath.presentmon.display_projection"
    if (-not $displayCoveragePassed) {
        $infrastructureErrors.Add("display projection coverage failed")
    }
    if (-not $coversStart -or -not $coversEnd) {
        $infrastructureErrors.Add("display projection does not cover both runtime boundaries")
    }
    $maximumGapQpc = Get-RequiredUInt64 -InputObject $displayProjection -Name "maximum_gap_qpc" -Path "$rootPath.presentmon.display_projection"
    $gapToleranceQpc = Get-RequiredUInt64 -InputObject $displayProjection -Name "quantization_tolerance_qpc" -Path "$rootPath.presentmon.display_projection"
    if ($maximumGapQpc -gt $gapToleranceQpc) {
        $infrastructureErrors.Add("display projection gap exceeds quantization tolerance")
    }
    $notDisplayedRows = Get-RequiredUInt64 -InputObject $displayProjection -Name "not_displayed_rows" -Path "$rootPath.presentmon.display_projection"
    $eventsLost = Get-RequiredUInt64 -InputObject $etw -Name "events_lost" -Path "$rootPath.presentmon.etw"
    $buffersLost = Get-RequiredUInt64 -InputObject $etw -Name "buffers_lost" -Path "$rootPath.presentmon.etw"
    if ($eventsLost -ne 0 -or $buffersLost -ne 0) {
        $infrastructureErrors.Add("PresentMon ETW loss is non-zero")
    }
    $nativeStatePassed = Get-RequiredBoolean -InputObject $nativeWindow -Name "state_passed" -Path "$rootPath.runtime.native_window"
    if (-not $nativeStatePassed) {
        $infrastructureErrors.Add("native-window continuity failed")
    }
    $maximumWindowSampleGapMs = Get-RequiredDouble -InputObject $nativeWindow -Name "maximum_sampling_gap_ms" -Path "$rootPath.runtime.native_window"
    if ($maximumWindowSampleGapMs -gt 250.0) {
        $infrastructureErrors.Add("native-window sampling gap exceeds 250 ms")
    }
    if (-not $signature.stable -or -not $signature.singleton_tuple) {
        $infrastructureErrors.Add("presentation signature changed within the run")
    }
    if ($beforeBytes -ne $afterBytes -or -not [string]::Equals($beforeSha256, $afterSha256, [StringComparison]::Ordinal)) {
        $infrastructureErrors.Add("runtime binary bytes/hash changed during the run")
    }
    if (-not (Get-RequiredBoolean -InputObject $runtime -Name "repository_unchanged" -Path "$rootPath.runtime")) {
        $infrastructureErrors.Add("repository changed during the run")
    }
    if (-not (Get-RequiredBoolean -InputObject $runtime -Name "operating_system_stable" -Path "$rootPath.runtime")) {
        $infrastructureErrors.Add("operating-system snapshot changed during the run")
    }
    if (-not (Get-RequiredBoolean -InputObject $runtime -Name "display_stable" -Path "$rootPath.runtime")) {
        $infrastructureErrors.Add("display snapshot changed during the run")
    }

    $metrics = Get-RequiredProperty -InputObject $Summary -Name "display_metrics" -Path $rootPath
    $normalizedMetrics = [pscustomobject][ordered]@{
        sample_count = Get-RequiredUInt64 -InputObject $metrics -Name "sample_count" -Path "$rootPath.display_metrics"
        mean_display_fps = Get-RequiredDouble -InputObject $metrics -Name "mean_display_fps" -Path "$rootPath.display_metrics"
        p95_ms = Get-RequiredDouble -InputObject $metrics -Name "p95_ms" -Path "$rootPath.display_metrics"
        one_percent_low_fps = Get-RequiredDouble -InputObject $metrics -Name "one_percent_low_fps" -Path "$rootPath.display_metrics"
        not_displayed_rows = $notDisplayedRows
        over_33_3_ms_count = Get-RequiredUInt64 -InputObject $metrics -Name "over_33_3_ms_count" -Path "$rootPath.display_metrics"
        maximum_consecutive_over_16_7_ms = Get-RequiredUInt64 -InputObject $metrics -Name "maximum_consecutive_over_16_7_ms" -Path "$rootPath.display_metrics"
    }
    if ($normalizedMetrics.sample_count -lt 120) {
        $infrastructureErrors.Add("display sample count is below 120")
    }
    $displayMetricChecksPassed = (
        $normalizedMetrics.mean_display_fps -ge 60.0 -and
        $normalizedMetrics.p95_ms -le 16.7 -and
        $normalizedMetrics.one_percent_low_fps -ge 45.0 -and
        $normalizedMetrics.over_33_3_ms_count -eq 0 -and
        $normalizedMetrics.maximum_consecutive_over_16_7_ms -le 1
    )
    $preHelperStatusInfrastructurePassed = $infrastructureErrors.Count -eq 0
    $expectedHelperPass = $preHelperStatusInfrastructurePassed -and $displayMetricChecksPassed
    $helperClaimsPass = $status -eq "diagnostic_thresholds_passed"
    $legacyZeroNotDisplayedPolicyMigrated = (
        -not $helperClaimsPass -and
        $expectedHelperPass -and
        $normalizedMetrics.not_displayed_rows -gt 0
    )
    if ($helperClaimsPass -ne $expectedHelperPass -and -not $legacyZeroNotDisplayedPolicyMigrated) {
        $infrastructureErrors.Add("helper status disagrees with recomputed infrastructure and metric checks")
    }

    return [pscustomobject][ordered]@{
        ordinal = $Ordinal
        summary_path = $SourcePath
        summary_sha256 = $SourceSha256
        helper_status = $status
        legacy_zero_not_displayed_policy_migrated = $legacyZeroNotDisplayedPolicyMigrated
        route = $route
        repository_commit = $commit
        repository_tree = $tree
        acquisition = [pscustomobject]$normalizedAcquisition
        runtime_binary_before = [pscustomobject][ordered]@{ bytes = $beforeBytes; sha256 = $beforeSha256 }
        runtime_binary_after = [pscustomobject][ordered]@{ bytes = $afterBytes; sha256 = $afterSha256 }
        comparables = New-ComparableMap `
            -Instrumentation $instrumentation `
            -GovernedScene $governedScene `
            -SceneHashes $(if ($sceneHashesOptional.Present) { $sceneHashesOptional.Value } else { $null }) `
            -Gpu $gpu `
            -Driver $(if ($driverOptional.Present) { $driverOptional.Value } else { $null }) `
            -OperatingSystem $operatingSystem `
            -Display $display
        infrastructure_errors = @($infrastructureErrors)
        qpc_coverage = [pscustomobject][ordered]@{
            exact_runtime_bounds_observed = $exactRuntimeBoundsObserved
            frequency_matches_host = $frequencyMatchesHost
            display_projection_coverage_passed = $displayCoveragePassed
            covers_start = $coversStart
            covers_end = $coversEnd
            maximum_gap_qpc = $maximumGapQpc
            quantization_tolerance_qpc = $gapToleranceQpc
        }
        etw = [pscustomobject][ordered]@{ events_lost = $eventsLost; buffers_lost = $buffersLost }
        native_window = [pscustomobject][ordered]@{ state_passed = $nativeStatePassed; maximum_sampling_gap_ms = $maximumWindowSampleGapMs }
        presentation_signature = $signature
        metrics = $normalizedMetrics
    }
}

function Add-ComparisonIssue {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$Issues,
        [Parameter(Mandatory = $true)][string]$Issue
    )

    if (-not $Issues.Contains($Issue)) {
        $Issues.Add($Issue)
    }
}

function Invoke-Gate1PresentationAnalysis {
    param([Parameter(Mandatory = $true)][object[]]$Runs)

    $infrastructureIssues = [Collections.Generic.List[string]]::new()
    $metricFailures = [Collections.Generic.List[string]]::new()
    Assert-Condition -Condition ($Runs.Count -eq 6) -Message "The A/B campaign analysis requires exactly six run objects."

    for ($index = 0; $index -lt 6; $index++) {
        $run = $Runs[$index]
        $expectedOrdinal = $index + 1
        if ([int]$run.ordinal -ne $expectedOrdinal) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "run ordinal sequence is not exactly 1..6"
        }
        if (-not [string]::Equals([string]$run.route, $script:ExpectedRoutes[$index], [StringComparison]::Ordinal)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "run $expectedOrdinal route is '$($run.route)', expected '$($script:ExpectedRoutes[$index])'"
        }
        foreach ($errorText in @($run.infrastructure_errors)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "run $expectedOrdinal infrastructure: $errorText"
        }
    }

    $anchor = $Runs[0]
    foreach ($run in $Runs) {
        if (-not [string]::Equals([string]$run.repository_commit, [string]$anchor.repository_commit, [StringComparison]::Ordinal)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "repository commit differs across runs"
        }
        if (-not [string]::Equals([string]$run.repository_tree, [string]$anchor.repository_tree, [StringComparison]::Ordinal)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "repository tree differs across runs"
        }
        if ((ConvertTo-CanonicalJson -Value $run.acquisition) -cne (ConvertTo-CanonicalJson -Value $anchor.acquisition)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "acquisition helper or governed script descriptor differs across runs"
        }
        if ([uint64]$run.runtime_binary_before.bytes -ne [uint64]$anchor.runtime_binary_before.bytes -or
            -not [string]::Equals([string]$run.runtime_binary_before.sha256, [string]$anchor.runtime_binary_before.sha256, [StringComparison]::Ordinal)) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "runtime binary bytes/hash differs across runs"
        }
    }

    $comparisonResults = [ordered]@{}
    foreach ($field in $script:ComparisonFields) {
        $presentRuns = @($Runs | Where-Object { Test-ComparableFieldPresent -Map $_.comparables -Name $field })
        if ($presentRuns.Count -eq 0) {
            $comparisonResults[$field] = [pscustomobject][ordered]@{
                availability = "not_available_in_any_summary"
                compared = $false
                matched = $null
            }
            continue
        }
        if ($presentRuns.Count -ne 6) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "$field is present in only $($presentRuns.Count)/6 summaries"
            $comparisonResults[$field] = [pscustomobject][ordered]@{
                availability = "inconsistent"
                compared = $false
                matched = $false
            }
            continue
        }
        $anchorJson = ConvertTo-CanonicalJson -Value (Get-ComparableFieldValue -Map $anchor.comparables -Name $field)
        $matched = @($presentRuns | Where-Object { (ConvertTo-CanonicalJson -Value (Get-ComparableFieldValue -Map $_.comparables -Name $field)) -eq $anchorJson }).Count -eq 6
        if (-not $matched) {
            Add-ComparisonIssue -Issues $infrastructureIssues -Issue "$field differs across runs"
        }
        $comparisonResults[$field] = [pscustomobject][ordered]@{
            availability = "all_six_runs"
            compared = $true
            matched = $matched
            canonical_value_sha256 = $null
        }
    }

    $autoRuns = @($Runs | Where-Object { $_.route -eq "auto_no_vsync" })
    $fifoRuns = @($Runs | Where-Object { $_.route -eq "fifo_diagnostic" })
    if ($autoRuns.Count -ne 3 -or $fifoRuns.Count -ne 3) {
        Add-ComparisonIssue -Issues $infrastructureIssues -Issue "campaign does not contain exactly three AutoNoVsync and three FifoDiagnostic runs"
    }

    $fifoRunsWithTuple = @($fifoRuns | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.presentation_signature.tuple_key) })
    $fifoSignatureKeys = @($fifoRunsWithTuple | ForEach-Object { [string]$_.presentation_signature.tuple_key } | Sort-Object -Unique)
    if ($fifoRunsWithTuple.Count -ne 3 -or $fifoSignatureKeys.Count -ne 1) {
        Add-ComparisonIssue -Issues $infrastructureIssues -Issue "FIFO presentation signature is not stable across all three runs"
    }
    $fifoSignatureKey = if ($fifoRunsWithTuple.Count -eq 3 -and $fifoSignatureKeys.Count -eq 1) { $fifoSignatureKeys[0] } else { $null }
    if ($null -ne $fifoSignatureKey) {
        foreach ($autoRun in $autoRuns) {
            if ([string]::Equals([string]$autoRun.presentation_signature.tuple_key, $fifoSignatureKey, [StringComparison]::Ordinal)) {
                Add-ComparisonIssue -Issues $infrastructureIssues -Issue "FIFO presentation tuple is not distinct from every AutoNoVsync tuple"
            }
        }
    }

    $runLedgers = [Collections.Generic.List[object]]::new()
    foreach ($run in $Runs) {
        $metricChecks = [ordered]@{
            mean_display_fps_at_least_60 = ([double]$run.metrics.mean_display_fps -ge 60.0)
            p95_at_most_16_7_ms = ([double]$run.metrics.p95_ms -le 16.7)
            one_percent_low_at_least_45_fps = ([double]$run.metrics.one_percent_low_fps -ge 45.0)
            no_not_displayed_rows = ([uint64]$run.metrics.not_displayed_rows -eq 0)
            not_displayed_rows_are_diagnostic_when_display_residency_is_continuous = ([bool]$run.qpc_coverage.display_projection_coverage_passed)
            no_displayed_time_over_33_3_ms = ([uint64]$run.metrics.over_33_3_ms_count -eq 0)
            maximum_consecutive_over_16_7_ms_at_most_1 = ([uint64]$run.metrics.maximum_consecutive_over_16_7_ms -le 1)
        }
        $stutterMetricCheckNames = @(
            "mean_display_fps_at_least_60",
            "p95_at_most_16_7_ms",
            "one_percent_low_at_least_45_fps",
            "no_displayed_time_over_33_3_ms",
            "maximum_consecutive_over_16_7_ms_at_most_1"
        )
        $allMetricChecksPass = @($stutterMetricCheckNames | Where-Object { $metricChecks[$_] -eq $false }).Count -eq 0
        $metricAssessment = if ($run.route -eq "fifo_diagnostic") {
            if ($allMetricChecksPass) { "PASS" } else { "FAIL" }
        }
        else {
            "BASELINE_ONLY"
        }
        if ($run.route -eq "fifo_diagnostic" -and -not $allMetricChecksPass) {
            Add-ComparisonIssue -Issues $metricFailures -Issue "FIFO run $($run.ordinal) fails one or more per-run display thresholds"
        }
        $runLedgers.Add([pscustomobject][ordered]@{
                ordinal = $run.ordinal
                route = $run.route
                summary_path = $run.summary_path
                summary_sha256 = $run.summary_sha256
                helper_status = $run.helper_status
                legacy_zero_not_displayed_policy_migrated = [bool]$run.legacy_zero_not_displayed_policy_migrated
                infrastructure_status = if (@($run.infrastructure_errors).Count -eq 0) { "VALID" } else { "INVALID" }
                metric_assessment = $metricAssessment
                metrics = $run.metrics
                metric_checks = [pscustomobject]$metricChecks
                presentation_signature = [pscustomobject][ordered]@{
                    stable_within_run = $run.presentation_signature.stable
                    singleton_tuple = $run.presentation_signature.singleton_tuple
                    observed_values = $run.presentation_signature.observed_values
                    tuple = $run.presentation_signature.tuple
                }
            })
    }

    $pairs = @(
        [pscustomobject]@{ Number = 1; AutoIndex = 0; FifoIndex = 1; Order = "AB" },
        [pscustomobject]@{ Number = 2; AutoIndex = 3; FifoIndex = 2; Order = "BA" },
        [pscustomobject]@{ Number = 3; AutoIndex = 4; FifoIndex = 5; Order = "AB" }
    )
    $pairLedgers = [Collections.Generic.List[object]]::new()
    $strictlyImprovedPairCount = 0
    $strictComparisonCount = 0
    foreach ($pair in $pairs) {
        $auto = $Runs[$pair.AutoIndex]
        $fifo = $Runs[$pair.FifoIndex]
        $p95NonWorse = [double]$fifo.metrics.p95_ms -le [double]$auto.metrics.p95_ms
        $lowNonWorse = [double]$fifo.metrics.one_percent_low_fps -ge [double]$auto.metrics.one_percent_low_fps
        $p95Strict = [double]$fifo.metrics.p95_ms -lt [double]$auto.metrics.p95_ms
        $lowStrict = [double]$fifo.metrics.one_percent_low_fps -gt [double]$auto.metrics.one_percent_low_fps
        if (-not $p95NonWorse -or -not $lowNonWorse) {
            Add-ComparisonIssue -Issues $metricFailures -Issue "pair $($pair.Number) makes FIFO worse for p95 or 1% low"
        }
        if ($p95Strict -or $lowStrict) {
            $strictlyImprovedPairCount++
        }
        $strictComparisonCount += @($p95Strict, $lowStrict | Where-Object { $_ }).Count
        $pairLedgers.Add([pscustomobject][ordered]@{
                pair = $pair.Number
                order = $pair.Order
                auto_run = $auto.ordinal
                fifo_run = $fifo.ordinal
                deltas_fifo_minus_auto = [pscustomobject][ordered]@{
                    mean_display_fps = [double]$fifo.metrics.mean_display_fps - [double]$auto.metrics.mean_display_fps
                    p95_ms = [double]$fifo.metrics.p95_ms - [double]$auto.metrics.p95_ms
                    one_percent_low_fps = [double]$fifo.metrics.one_percent_low_fps - [double]$auto.metrics.one_percent_low_fps
                    not_displayed_rows = [int64]$fifo.metrics.not_displayed_rows - [int64]$auto.metrics.not_displayed_rows
                    over_33_3_ms_count = [int64]$fifo.metrics.over_33_3_ms_count - [int64]$auto.metrics.over_33_3_ms_count
                    maximum_consecutive_over_16_7_ms = [int64]$fifo.metrics.maximum_consecutive_over_16_7_ms - [int64]$auto.metrics.maximum_consecutive_over_16_7_ms
                }
                comparisons = [pscustomobject][ordered]@{
                    fifo_p95_non_worse = $p95NonWorse
                    fifo_one_percent_low_non_worse = $lowNonWorse
                    fifo_p95_strictly_better = $p95Strict
                    fifo_one_percent_low_strictly_better = $lowStrict
                    pair_has_strict_improvement = ($p95Strict -or $lowStrict)
                }
            })
    }
    if ($strictlyImprovedPairCount -lt 2) {
        Add-ComparisonIssue -Issues $metricFailures -Issue "FIFO has strict p95 or 1% low improvement in fewer than two pairs"
    }

    $modeStatistics = [ordered]@{}
    foreach ($mode in @(
            [pscustomobject]@{ Name = "auto_no_vsync"; Runs = $autoRuns },
            [pscustomobject]@{ Name = "fifo_diagnostic"; Runs = $fifoRuns }
        )) {
        if ($mode.Runs.Count -eq 3) {
            $modeStatistics[$mode.Name] = [pscustomobject][ordered]@{
                aggregation_unit = "run"
                frame_pooling = $false
                percentile_averaging_for_verdict = $false
                mean_display_fps = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.mean_display_fps })) -Better "higher"
                p95_ms = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.p95_ms })) -Better "lower"
                one_percent_low_fps = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.one_percent_low_fps })) -Better "higher"
                not_displayed_rows = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.not_displayed_rows })) -Better "lower"
                over_33_3_ms_count = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.over_33_3_ms_count })) -Better "lower"
                maximum_consecutive_over_16_7_ms = Get-RunMetricStatistics -Values ([double[]]@($mode.Runs | ForEach-Object { [double]$_.metrics.maximum_consecutive_over_16_7_ms })) -Better "lower"
            }
        }
    }

    $verdict = if ($infrastructureIssues.Count -gt 0) {
        "INCONCLUSIVE"
    }
    elseif ($metricFailures.Count -gt 0) {
        "FAIL"
    }
    else {
        "PROPOSE"
    }
    return [pscustomobject][ordered]@{
        schema = "forge.gate1-presentation-ab-analysis"
        schema_version = 2
        generated_at_utc = [DateTimeOffset]::UtcNow.ToString("o")
        qualifying = $false
        closes_gate = $false
        verdict = $verdict
        proposal = if ($verdict -eq "PROPOSE") { "PROPOSE_FIFO_AS_CANONICAL_GATE1_PROTOCOL" } else { $null }
        protocol = [pscustomobject][ordered]@{
            exact_run_count = 6
            order = @($script:ExpectedRoutes)
            pairing = @("AB", "BA", "AB")
            aggregation_unit = "run"
            frames_pooled = $false
            p95_values_averaged_for_verdict = $false
            not_displayed_rows_policy = "diagnostic_only_when_display_residency_is_continuous"
            first_fifo_signature_tuple_predefined = $false
        }
        provenance = [pscustomobject][ordered]@{
            repository_commit = $anchor.repository_commit
            repository_tree = $anchor.repository_tree
            runtime_binary = $anchor.runtime_binary_before
        }
        cross_run_comparisons = [pscustomobject]$comparisonResults
        runs = @($runLedgers)
        pairs = @($pairLedgers)
        mode_run_statistics = [pscustomobject]$modeStatistics
        proposal_checks = [pscustomobject][ordered]@{
            infrastructure_valid_for_all_runs = ($infrastructureIssues.Count -eq 0)
            exact_routes_3_plus_3_in_ab_ba_ab_order = ($autoRuns.Count -eq 3 -and $fifoRuns.Count -eq 3 -and @($Runs | ForEach-Object { $_.route }) -join "|" -eq $script:ExpectedRoutes -join "|")
            fifo_signature_stable_3_of_3 = ($fifoRunsWithTuple.Count -eq 3 -and $fifoSignatureKeys.Count -eq 1)
            fifo_signature_distinct_from_every_auto_run = ($null -ne $fifoSignatureKey -and @($autoRuns | Where-Object { $_.presentation_signature.tuple_key -eq $fifoSignatureKey }).Count -eq 0)
            fifo_run_metrics_pass_3_of_3 = (@($runLedgers | Where-Object { $_.route -eq "fifo_diagnostic" -and $_.metric_assessment -eq "PASS" }).Count -eq 3)
            auto_runs_structurally_valid_3_of_3 = ($autoRuns.Count -eq 3 -and @($autoRuns | Where-Object { @($_.infrastructure_errors).Count -eq 0 -and $_.presentation_signature.stable }).Count -eq 3)
            fifo_p95_and_one_percent_low_non_worse_in_every_pair = (@($pairLedgers | Where-Object { $_.comparisons.fifo_p95_non_worse -and $_.comparisons.fifo_one_percent_low_non_worse }).Count -eq 3)
            strict_improvement_in_at_least_two_pairs = ($strictlyImprovedPairCount -ge 2)
            strict_improved_pair_count = $strictlyImprovedPairCount
            strict_metric_comparison_count = $strictComparisonCount
        }
        infrastructure_issues = @($infrastructureIssues)
        metric_failures = @($metricFailures)
        limitations = [pscustomobject][ordered]@{
            diagnostic_only = $true
            gate1_qualification = $false
            human_review_performed = $false
            target_gpu_qualified = $false
            no_gate_closed = $true
            optional_environment_fields_are_compared_only_when_present_in_all_six_summaries = $true
        }
    }
}

function Copy-InMemoryFixture {
    param([Parameter(Mandatory = $true)][object]$Value)

    return ($Value | ConvertTo-Json -Depth 64 | ConvertFrom-Json -Depth 64)
}

function New-InMemoryRunFixture {
    param(
        [Parameter(Mandatory = $true)][int]$Ordinal,
        [Parameter(Mandatory = $true)][string]$Route,
        [Parameter(Mandatory = $true)][double]$P95,
        [Parameter(Mandatory = $true)][double]$OnePercentLow,
        [Parameter(Mandatory = $true)][double]$MeanFps,
        [uint64]$NotDisplayedRows = 0
    )

    $autoTuple = [pscustomobject][ordered]@{
        present_mode_values = "Composed: Flip"
        allows_tearing_values = "0"
        sync_interval_values = "0"
        present_flags_values = "512"
    }
    $fifoTuple = [pscustomobject][ordered]@{
        present_mode_values = "Composed: Flip"
        allows_tearing_values = "0"
        sync_interval_values = "1"
        present_flags_values = "0"
    }
    $tuple = if ($Route -eq "fifo_diagnostic") { $fifoTuple } else { $autoTuple }
    return [pscustomobject][ordered]@{
        ordinal = $Ordinal
        summary_path = "memory://run-$Ordinal/diagnostic-summary.json"
        summary_sha256 = ("{0:x64}" -f $Ordinal)
        helper_status = if ($Route -eq "fifo_diagnostic") { "diagnostic_thresholds_passed" } else { "diagnostic_thresholds_failed" }
        legacy_zero_not_displayed_policy_migrated = $false
        route = $Route
        repository_commit = "1111111111111111111111111111111111111111"
        repository_tree = "2222222222222222222222222222222222222222"
        acquisition = [pscustomobject][ordered]@{
            helper = [pscustomobject]@{ path = "C:\\gate1\\helper.ps1"; bytes = [uint64]4096; sha256 = "5555555555555555555555555555555555555555555555555555555555555555" }
            governed_script = [pscustomobject]@{ path = "C:\\gate1\\governed.ps1"; bytes = [uint64]8192; sha256 = "6666666666666666666666666666666666666666666666666666666666666666" }
        }
        runtime_binary_before = [pscustomobject]@{ bytes = [uint64]123456; sha256 = "3333333333333333333333333333333333333333333333333333333333333333" }
        runtime_binary_after = [pscustomobject]@{ bytes = [uint64]123456; sha256 = "3333333333333333333333333333333333333333333333333333333333333333" }
        comparables = [ordered]@{
            instrumentation = [pscustomobject]@{ display_timing_source = "external_presentmon_displayed_time" }
            governed_scene = [pscustomobject]@{ scene = "visual_valley"; hashes = [pscustomobject]@{ environment = "4444" } }
            scene_hashes = [pscustomobject]@{ environment = "4444" }
            gpu = [pscustomobject]@{ name = "Synthetic GPU"; driver = "1.0"; backend = "Vulkan" }
            driver = "1.0"
            operating_system = [pscustomobject]@{ name = "Windows"; build = "synthetic" }
            display = [pscustomobject]@{ width = 1920; height = 1080; refresh_hz = 144 }
        }
        infrastructure_errors = @()
        qpc_coverage = [pscustomobject]@{ exact_runtime_bounds_observed = $true; frequency_matches_host = $true; display_projection_coverage_passed = $true; covers_start = $true; covers_end = $true; maximum_gap_qpc = 1; quantization_tolerance_qpc = 1 }
        etw = [pscustomobject]@{ events_lost = 0; buffers_lost = 0 }
        native_window = [pscustomobject]@{ state_passed = $true; maximum_sampling_gap_ms = 120.0 }
        presentation_signature = [pscustomobject][ordered]@{
            stable = $true
            singleton_tuple = $true
            observed_values = [pscustomobject][ordered]@{
                present_mode_values = @($tuple.present_mode_values)
                allows_tearing_values = @($tuple.allows_tearing_values)
                sync_interval_values = @($tuple.sync_interval_values)
                present_flags_values = @($tuple.present_flags_values)
            }
            tuple = $tuple
            tuple_key = ConvertTo-CanonicalJson -Value $tuple
        }
        metrics = [pscustomobject][ordered]@{
            sample_count = 1800
            mean_display_fps = $MeanFps
            p95_ms = $P95
            one_percent_low_fps = $OnePercentLow
            not_displayed_rows = $NotDisplayedRows
            over_33_3_ms_count = 0
            maximum_consecutive_over_16_7_ms = 0
        }
    }
}

function New-PositiveInMemoryFixtures {
    return @(
        (New-InMemoryRunFixture -Ordinal 1 -Route "auto_no_vsync" -P95 24.0 -OnePercentLow 30.0 -MeanFps 52.0),
        (New-InMemoryRunFixture -Ordinal 2 -Route "fifo_diagnostic" -P95 13.0 -OnePercentLow 70.0 -MeanFps 72.0 -NotDisplayedRows 4),
        (New-InMemoryRunFixture -Ordinal 3 -Route "fifo_diagnostic" -P95 14.0 -OnePercentLow 68.0 -MeanFps 71.0),
        (New-InMemoryRunFixture -Ordinal 4 -Route "auto_no_vsync" -P95 22.0 -OnePercentLow 35.0 -MeanFps 54.0),
        (New-InMemoryRunFixture -Ordinal 5 -Route "auto_no_vsync" -P95 23.0 -OnePercentLow 32.0 -MeanFps 53.0),
        (New-InMemoryRunFixture -Ordinal 6 -Route "fifo_diagnostic" -P95 14.5 -OnePercentLow 65.0 -MeanFps 70.0)
    )
}

function New-InMemoryDiagnosticSummaryFixture {
    $run = New-InMemoryRunFixture -Ordinal 2 -Route "fifo_diagnostic" -P95 13.0 -OnePercentLow 70.0 -MeanFps 72.0
    return [pscustomobject][ordered]@{
        schema = "forge.gate1-shared-binary-diagnostic"
        schema_version = 2
        status = "diagnostic_thresholds_passed"
        qualifying = $false
        presentation_route = "fifo_diagnostic"
        repository_commit = $run.repository_commit
        repository_tree = $run.repository_tree
        acquisition = [pscustomobject][ordered]@{
            unchanged_during_run = $true
            helper = $run.acquisition.helper
            governed_script = $run.acquisition.governed_script
        }
        runtime = [pscustomobject][ordered]@{
            binary_before = $run.runtime_binary_before
            binary_after = $run.runtime_binary_after
            repository_unchanged = $true
            operating_system_stable = $true
            display_stable = $true
            instrumentation = $run.comparables.instrumentation
            governed_scene = $run.comparables.governed_scene
            native_window = [pscustomobject]@{
                state_passed = $true
                maximum_sampling_gap_ms = 120.0
            }
            gpu = $run.comparables.gpu
            operating_system = $run.comparables.operating_system
            display = $run.comparables.display
        }
        presentmon = [pscustomobject][ordered]@{
            qpc_window = [pscustomobject]@{
                exact_runtime_bounds_observed = $true
                frequency_matches_host = $true
            }
            display_projection = [pscustomobject]@{
                coverage_passed = $true
                covers_start = $true
                covers_end = $true
                maximum_gap_qpc = 1
                quantization_tolerance_qpc = 1
                not_displayed_rows = 4
            }
            etw = [pscustomobject]@{
                events_lost = 0
                buffers_lost = 0
            }
            presentation_signature = [pscustomobject][ordered]@{
                stable = $true
                present_mode_values = @($run.presentation_signature.tuple.present_mode_values)
                allows_tearing_values = @($run.presentation_signature.tuple.allows_tearing_values)
                sync_interval_values = @($run.presentation_signature.tuple.sync_interval_values)
                present_flags_values = @($run.presentation_signature.tuple.present_flags_values)
            }
        }
        display_metrics = [pscustomobject][ordered]@{
            sample_count = $run.metrics.sample_count
            mean_display_fps = $run.metrics.mean_display_fps
            p95_ms = $run.metrics.p95_ms
            one_percent_low_fps = $run.metrics.one_percent_low_fps
            over_33_3_ms_count = $run.metrics.over_33_3_ms_count
            maximum_consecutive_over_16_7_ms = $run.metrics.maximum_consecutive_over_16_7_ms
        }
    }
}

function Invoke-InMemorySelfTest {
    $parsedSummary = ConvertFrom-Gate1DiagnosticSummary `
        -Summary (New-InMemoryDiagnosticSummaryFixture) `
        -Ordinal 2 `
        -SourcePath "memory://summary/diagnostic-summary.json" `
        -SourceSha256 "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    Assert-Condition -Condition ($parsedSummary.route -eq "fifo_diagnostic" -and @($parsedSummary.infrastructure_errors).Count -eq 0) -Message "Schema-v2 diagnostic summary fixture did not normalize cleanly."
    Assert-Condition -Condition ($parsedSummary.metrics.not_displayed_rows -eq 4) -Message "Schema-v2 diagnostic summary fixture lost its diagnostic not-displayed row count."
    Assert-Condition -Condition (-not $parsedSummary.legacy_zero_not_displayed_policy_migrated) -Message "Current-policy PASS fixture was incorrectly marked as a legacy migration."

    $legacySummary = Copy-InMemoryFixture -Value (New-InMemoryDiagnosticSummaryFixture)
    $legacySummary.status = "diagnostic_thresholds_failed"
    $legacyParsed = ConvertFrom-Gate1DiagnosticSummary `
        -Summary $legacySummary `
        -Ordinal 2 `
        -SourcePath "memory://summary/legacy-zero-not-displayed-policy.json" `
        -SourceSha256 "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
    Assert-Condition -Condition (@($legacyParsed.infrastructure_errors).Count -eq 0 -and $legacyParsed.legacy_zero_not_displayed_policy_migrated) -Message "Schema-v2 legacy zero-not-displayed policy fixture was not migrated narrowly."

    $failedWithoutNotDisplayedRows = Copy-InMemoryFixture -Value (New-InMemoryDiagnosticSummaryFixture)
    $failedWithoutNotDisplayedRows.status = "diagnostic_thresholds_failed"
    $failedWithoutNotDisplayedRows.presentmon.display_projection.not_displayed_rows = 0
    $failedWithoutNotDisplayedRowsParsed = ConvertFrom-Gate1DiagnosticSummary `
        -Summary $failedWithoutNotDisplayedRows `
        -Ordinal 2 `
        -SourcePath "memory://summary/failed-without-not-displayed-rows.json" `
        -SourceSha256 "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
    Assert-Condition -Condition (@($failedWithoutNotDisplayedRowsParsed.infrastructure_errors).Count -gt 0 -and -not $failedWithoutNotDisplayedRowsParsed.legacy_zero_not_displayed_policy_migrated) -Message "Schema-v2 failed status without not-displayed rows was incorrectly migrated."

    $passingStatusWithMetricFailure = Copy-InMemoryFixture -Value (New-InMemoryDiagnosticSummaryFixture)
    $passingStatusWithMetricFailure.display_metrics.p95_ms = 17.0
    $passingStatusWithMetricFailureParsed = ConvertFrom-Gate1DiagnosticSummary `
        -Summary $passingStatusWithMetricFailure `
        -Ordinal 2 `
        -SourcePath "memory://summary/pass-with-metric-failure.json" `
        -SourceSha256 "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
    Assert-Condition -Condition (@($passingStatusWithMetricFailureParsed.infrastructure_errors).Count -gt 0 -and -not $passingStatusWithMetricFailureParsed.legacy_zero_not_displayed_policy_migrated) -Message "Schema-v2 passing status with a displayed-metric failure was accepted."

    $coverageSummary = Copy-InMemoryFixture -Value (New-InMemoryDiagnosticSummaryFixture)
    $coverageSummary.presentmon.display_projection.coverage_passed = $false
    $coverageSummary.presentmon.display_projection.covers_end = $false
    $coverageFailure = ConvertFrom-Gate1DiagnosticSummary `
        -Summary $coverageSummary `
        -Ordinal 2 `
        -SourcePath "memory://summary/coverage-failure.json" `
        -SourceSha256 "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
    Assert-Condition -Condition (@($coverageFailure.infrastructure_errors).Count -gt 0 -and -not $coverageFailure.legacy_zero_not_displayed_policy_migrated) -Message "Schema-v2 passing status with broken display-residency coverage was accepted."

    $positive = Invoke-Gate1PresentationAnalysis -Runs (New-PositiveInMemoryFixtures)
    Assert-Condition -Condition ($positive.verdict -eq "PROPOSE" -and $positive.qualifying -eq $false -and $positive.closes_gate -eq $false) -Message "Positive A/B fixture did not produce a non-qualifying PROPOSE verdict."
    Assert-Condition -Condition ($positive.pairs.Count -eq 3 -and $positive.runs.Count -eq 6) -Message "Positive A/B fixture lost run or pair units."
    Assert-Condition -Condition ($positive.mode_run_statistics.fifo_diagnostic.p95_ms.median -eq 14.0) -Message "Run-level median self-test failed."
    Assert-Condition -Condition ($positive.proposal_checks.strict_improved_pair_count -eq 3 -and $positive.proposal_checks.strict_metric_comparison_count -eq 6) -Message "Strict pair/metric comparison counts are incorrect."
    $fifoWithDiagnosticNotDisplayedRows = @($positive.runs | Where-Object { $_.ordinal -eq 2 })[0]
    Assert-Condition -Condition ($fifoWithDiagnosticNotDisplayedRows.metric_assessment -eq "PASS" -and -not $fifoWithDiagnosticNotDisplayedRows.metric_checks.no_not_displayed_rows -and $fifoWithDiagnosticNotDisplayedRows.metric_checks.not_displayed_rows_are_diagnostic_when_display_residency_is_continuous) -Message "A/B self-test treated diagnostic not-displayed rows as a stutter threshold failure."
    Assert-Condition -Condition ($positive.protocol.not_displayed_rows_policy -eq "diagnostic_only_when_display_residency_is_continuous") -Message "A/B self-test lost the not-displayed-row policy."

    $metricFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $metricFail[1].metrics.p95_ms = 17.0
    $metricFailResult = Invoke-Gate1PresentationAnalysis -Runs $metricFail
    Assert-Condition -Condition ($metricFailResult.verdict -eq "FAIL") -Message "FIFO per-run metric failure did not fail the campaign."

    $infrastructureFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $infrastructureFail[2].infrastructure_errors = @("synthetic QPC coverage failure")
    $infrastructureFailResult = Invoke-Gate1PresentationAnalysis -Runs $infrastructureFail
    Assert-Condition -Condition ($infrastructureFailResult.verdict -eq "INCONCLUSIVE") -Message "Infrastructure failure did not make the campaign inconclusive."

    $routeFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $routeFail[0].route = "fifo_diagnostic"
    $routeFailResult = Invoke-Gate1PresentationAnalysis -Runs $routeFail
    Assert-Condition -Condition ($routeFailResult.verdict -eq "INCONCLUSIVE") -Message "Route-order failure did not make the campaign inconclusive."

    $signatureFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $signatureFail[2].presentation_signature.tuple.present_flags_values = "2"
    $signatureFail[2].presentation_signature.tuple_key = ConvertTo-CanonicalJson -Value $signatureFail[2].presentation_signature.tuple
    $signatureFailResult = Invoke-Gate1PresentationAnalysis -Runs $signatureFail
    Assert-Condition -Condition ($signatureFailResult.verdict -eq "INCONCLUSIVE") -Message "Unstable FIFO tuple did not make the campaign inconclusive."

    $withinRunSignatureFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $withinRunSignatureFail[1].presentation_signature.stable = $false
    $withinRunSignatureFail[1].presentation_signature.singleton_tuple = $false
    $withinRunSignatureFail[1].presentation_signature.tuple = $null
    $withinRunSignatureFail[1].presentation_signature.tuple_key = $null
    $withinRunSignatureFail[1].infrastructure_errors = @("presentation signature changed within the run")
    $withinRunSignatureResult = Invoke-Gate1PresentationAnalysis -Runs $withinRunSignatureFail
    Assert-Condition -Condition ($withinRunSignatureResult.verdict -eq "INCONCLUSIVE") -Message "Within-run signature instability did not make the campaign inconclusive."

    $sameTupleFail = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    foreach ($fifoRun in @($sameTupleFail | Where-Object { $_.route -eq "fifo_diagnostic" })) {
        $fifoRun.presentation_signature.tuple = Copy-InMemoryFixture -Value $sameTupleFail[0].presentation_signature.tuple
        $fifoRun.presentation_signature.tuple_key = ConvertTo-CanonicalJson -Value $fifoRun.presentation_signature.tuple
    }
    $sameTupleResult = Invoke-Gate1PresentationAnalysis -Runs $sameTupleFail
    Assert-Condition -Condition ($sameTupleResult.verdict -eq "INCONCLUSIVE") -Message "Identical Auto/FIFO tuples did not make the campaign inconclusive."

    $pairRegression = Copy-InMemoryFixture -Value (New-PositiveInMemoryFixtures)
    $pairRegression[1].metrics.p95_ms = 16.0
    $pairRegression[0].metrics.p95_ms = 15.0
    $pairRegressionResult = Invoke-Gate1PresentationAnalysis -Runs $pairRegression
    Assert-Condition -Condition ($pairRegressionResult.verdict -eq "FAIL") -Message "Pair-level FIFO regression did not fail the campaign."
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $relative = [IO.Path]::GetRelativePath($Root, $Candidate)
    return -not [IO.Path]::IsPathRooted($relative) -and
        $relative -ne ".." -and
        -not $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -and
        -not $relative.StartsWith("../", [StringComparison]::Ordinal)
}

function Resolve-SummaryInputs {
    if ($PSCmdlet.ParameterSetName -eq "Paths") {
        $resolved = [Collections.Generic.List[object]]::new()
        for ($index = 0; $index -lt $SummaryPath.Count; $index++) {
            $path = (Resolve-Path -LiteralPath $SummaryPath[$index]).Path
            Assert-Condition -Condition ([IO.File]::Exists($path)) -Message "Summary path is not a regular file: $path"
            Assert-Condition -Condition ([IO.Path]::GetFileName($path) -ceq "diagnostic-summary.json") -Message "Every input must be named diagnostic-summary.json: $path"
            $resolved.Add([pscustomobject]@{ Ordinal = $index + 1; DeclaredRoute = $script:ExpectedRoutes[$index]; Path = $path })
        }
        return @($resolved)
    }

    $root = (Resolve-Path -LiteralPath $CampaignRoot).Path
    Assert-Condition -Condition ([IO.Directory]::Exists($root)) -Message "CampaignRoot is not a directory: $root"
    $manifest = (Resolve-Path -LiteralPath $ManifestPath).Path
    Assert-Condition -Condition ([IO.File]::Exists($manifest) -and (Test-PathInsideRoot -Root $root -Candidate $manifest)) -Message "ManifestPath must be a regular file inside CampaignRoot."
    $manifestValue = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json -Depth 64
    Assert-Condition -Condition ((Get-RequiredString -InputObject $manifestValue -Name "schema" -Path "manifest") -eq "forge.gate1-presentation-ab-manifest") -Message "Unexpected A/B manifest schema."
    Assert-Condition -Condition ((Get-RequiredUInt64 -InputObject $manifestValue -Name "schema_version" -Path "manifest") -eq 1) -Message "Unexpected A/B manifest schema version."
    $entries = @(Get-RequiredProperty -InputObject $manifestValue -Name "runs" -Path "manifest")
    Assert-Condition -Condition ($entries.Count -eq 6) -Message "The explicit manifest must list exactly six runs."
    $resolved = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt 6; $index++) {
        $entry = $entries[$index]
        $ordinal = Get-RequiredUInt64 -InputObject $entry -Name "ordinal" -Path "manifest.runs[$index]"
        $route = Get-RequiredString -InputObject $entry -Name "presentation_route" -Path "manifest.runs[$index]"
        $relativePath = Get-RequiredString -InputObject $entry -Name "summary_path" -Path "manifest.runs[$index]"
        Assert-Condition -Condition ($ordinal -eq ($index + 1)) -Message "Manifest ordinals must be exactly 1..6."
        Assert-Condition -Condition ([string]::Equals($route, $script:ExpectedRoutes[$index], [StringComparison]::Ordinal)) -Message "Manifest run $ordinal does not follow Auto/FIFO/FIFO/Auto/Auto/FIFO."
        Assert-Condition -Condition (-not [IO.Path]::IsPathRooted($relativePath)) -Message "Manifest summary paths must be relative to CampaignRoot."
        $path = [IO.Path]::GetFullPath((Join-Path $root $relativePath))
        Assert-Condition -Condition ((Test-PathInsideRoot -Root $root -Candidate $path) -and [IO.File]::Exists($path)) -Message "Manifest summary path is absent or escapes CampaignRoot: $relativePath"
        Assert-Condition -Condition ([IO.Path]::GetFileName($path) -ceq "diagnostic-summary.json") -Message "Manifest input must be named diagnostic-summary.json: $relativePath"
        $resolved.Add([pscustomobject]@{ Ordinal = [int]$ordinal; DeclaredRoute = $route; Path = $path })
    }
    return @($resolved)
}

function Write-JsonAtomically {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($directory)) -Message "OutputPath must include a directory."
    if (-not [IO.Directory]::Exists($directory)) {
        [void][IO.Directory]::CreateDirectory($directory)
    }
    $temporaryPath = Join-Path $directory (".{0}.{1}.tmp" -f [IO.Path]::GetFileName($fullPath), [Guid]::NewGuid().ToString("N"))
    try {
        $json = $Value | ConvertTo-Json -Depth 64
        [IO.File]::WriteAllText($temporaryPath, $json + "`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $fullPath, $true)
    }
    finally {
        if ([IO.File]::Exists($temporaryPath)) {
            [IO.File]::Delete($temporaryPath)
        }
    }
}

Invoke-InMemorySelfTest
if ($PSCmdlet.ParameterSetName -eq "SelfTest") {
    Write-Output "FORGE_GATE1_PRESENTATION_AB_SELF_TEST_PASS fixtures=in_memory_and_schema_v2 negative_cases=10 legacy_zero_not_displayed_policy=fail_closed_migration not_displayed_rows=diagnostic qualifying=false"
    return
}

$inputs = @(Resolve-SummaryInputs)
Assert-Condition -Condition ($inputs.Count -eq 6) -Message "Exactly six diagnostic summaries are required."
$uniqueInputPaths = @($inputs | ForEach-Object { $_.Path.ToLowerInvariant() } | Sort-Object -Unique)
Assert-Condition -Condition ($uniqueInputPaths.Count -eq 6) -Message "The six diagnostic-summary.json inputs must be distinct files."
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
Assert-Condition -Condition (@($inputs | Where-Object { [string]::Equals($_.Path, $outputFullPath, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) -Message "OutputPath cannot overwrite an input summary."

$normalizedRuns = [Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt 6; $index++) {
    $input = $inputs[$index]
    $sourceSha256 = (Get-FileHash -LiteralPath $input.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $summary = Get-Content -Raw -LiteralPath $input.Path | ConvertFrom-Json -Depth 64
    $run = ConvertFrom-Gate1DiagnosticSummary -Summary $summary -Ordinal $input.Ordinal -SourcePath $input.Path -SourceSha256 $sourceSha256
    Assert-Condition -Condition ([string]::Equals($run.route, $input.DeclaredRoute, [StringComparison]::Ordinal)) -Message "Summary route at ordinal $($input.Ordinal) differs from the explicit campaign route."
    $normalizedRuns.Add($run)
}

$analysis = Invoke-Gate1PresentationAnalysis -Runs @($normalizedRuns)
Write-JsonAtomically -Path $outputFullPath -Value $analysis
Write-Output ("FORGE_GATE1_PRESENTATION_AB_COMPLETE verdict={0} qualifying=false runs=6 pairs=3 output={1}" -f $analysis.verdict, $outputFullPath)
