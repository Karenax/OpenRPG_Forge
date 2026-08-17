[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'OpenRPGForge.sln'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$enginePath = Get-ForgeEnginePath
$blenderPath = Get-ForgeBlenderPath -Optional

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

function Invoke-CheckedDotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-CheckedPython {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & python @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "python $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Test-NuGetVulnerabilities {
    $output = & dotnet package list --project $solutionPath --vulnerable --include-transitive --no-restore --format json 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet vulnerability audit failed with exit code $LASTEXITCODE.`n$($output -join "`n")"
    }
    $document = ($output -join "`n") | ConvertFrom-Json
    $vulnerable = @($document.projects | ForEach-Object {
        $frameworksProperty = $_.PSObject.Properties['frameworks']
        if ($null -ne $frameworksProperty) {
            $frameworksProperty.Value
        }
    } | ForEach-Object {
        $packages = @()
        $topLevelProperty = $_.PSObject.Properties['topLevelPackages']
        $transitiveProperty = $_.PSObject.Properties['transitivePackages']
        if ($null -ne $topLevelProperty) { $packages += @($topLevelProperty.Value) }
        if ($null -ne $transitiveProperty) { $packages += @($transitiveProperty.Value) }
        $packages
    } | Where-Object {
        $vulnerabilitiesProperty = $_.PSObject.Properties['vulnerabilities']
        $null -ne $vulnerabilitiesProperty -and @($vulnerabilitiesProperty.Value).Count -gt 0
    })
    if ($vulnerable.Count -gt 0) {
        throw "NuGet vulnerability audit found $($vulnerable.Count) vulnerable package entries."
    }
    Write-Output "dependency vulnerability audit passed: $(@($document.projects).Count) projects, 0 vulnerable packages."
}

function Invoke-ForgeCliMarkerTest {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Marker
    )

    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | Write-Output
    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }

    if (($output -join "`n") -notmatch [regex]::Escape($Marker)) {
        throw "Forge CLI did not emit required marker '$Marker'."
    }
}

function Test-ForgeBranding {
    $projects = @(
        (Join-Path $repositoryRoot 'apps\Forge.Runtime'),
        (Join-Path $repositoryRoot 'apps\Forge.Toolset')
    )
    $expectedSignature = '137,80,78,71,13,10,26,10'

    foreach ($project in $projects) {
        $settingsPath = Join-Path $project 'project.godot'
        $imagePath = Join-Path $project 'branding\openrpg_forge_splash.png'
        $settings = Get-Content -LiteralPath $settingsPath -Raw
        if ($settings -notmatch 'boot_splash/image="res://branding/openrpg_forge_splash.png"') {
            throw "Forge branding is not configured in $settingsPath."
        }

        if (-not (Test-Path -LiteralPath $imagePath)) {
            throw "Forge splash image is missing: $imagePath."
        }

        $bytes = [System.IO.File]::ReadAllBytes($imagePath)
        if ($bytes.Length -lt 8 -or ($bytes[0..7] -join ',') -ne $expectedSignature) {
            throw "Forge splash image is not a valid PNG: $imagePath."
        }
    }

    Write-Output 'branding passed.'
}

function Invoke-ForgeEngineMarkerTest {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string[]]$Markers,
        [Parameter(Mandatory)][bool]$StopAfterMarkers
    )

    # Keep the long-lived smoke inventories aligned with the current workspace schema and
    # append lot markers here so every engine invocation uses one canonical gate contract.
    $Markers = @($Markers | ForEach-Object { $_.Replace('format=1.9.0', 'format=1.30.0').Replace('format=1.10.0', 'format=1.30.0').Replace('format=1.11.0', 'format=1.30.0').Replace('format=1.12.0', 'format=1.30.0').Replace('format=1.13.0', 'format=1.30.0').Replace('format=1.14.0', 'format=1.30.0').Replace('format=1.15.0', 'format=1.30.0').Replace('format=1.16.0', 'format=1.30.0').Replace('format=1.17.0', 'format=1.30.0').Replace('format=1.18.0', 'format=1.30.0').Replace('format=1.19.0', 'format=1.30.0').Replace('format=1.20.0', 'format=1.30.0').Replace('format=1.21.0', 'format=1.30.0').Replace('format=1.22.0', 'format=1.30.0').Replace('format=1.23.0', 'format=1.30.0').Replace('format=1.24.0', 'format=1.30.0').Replace('format=1.25.0', 'format=1.30.0').Replace('format=1.26.0', 'format=1.30.0').Replace('format=1.27.0', 'format=1.30.0').Replace('format=1.28.0', 'format=1.30.0').Replace('format=1.29.0', 'format=1.30.0') })
    $Markers = @($Markers | ForEach-Object { $_.Replace('catalog=605 sources=3', 'catalog=665 sources=5') })
    if ($Name -eq 'toolset') {
        $Markers += 'OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_PASS models=1 manifests=1 scatter_layers=1 scatter_instances='
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_PASS scatter=volume-surface-nav-biome-seed voice=localized-phoneme-subtitle-tts-consent audio=started-completed-interrupted-music-snapshot patrol=cross-area-streaming-owner-resume loading=image-video-tip-progress-accessible-prefetch mcp=governed history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R004_AUDIO_PASS object=definition-instance clips=weighted-licensed loop=seamless-positioned validation=files-license-loudness-range-memory script=typed-handle mcp=filtered-dry-run history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R005_DIALOGUE_PASS graph=schema-participants-entries-edges roles=explicit branches=unbounded-paged subgraphs=grouped cycle=protected reference=multiple-incoming reorder=drag-priority-diff script=typed-handle mcp=filtered-dry-run history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R006_DIALOGUE_PASS localization=language-gender-plural-fallback-pseudo conditions=all-any-not-preview-explanation actions=typed-transactional simulation=sandbox-state-journal lint=entry-orphan-cycle-end-types-reference-contradiction-localization script=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R007_QUEST_PASS metadata=category-priority-owner-localized states=uuid-order-legacy-index terminals=completed-failed-abandoned-custom-reactivation journal=summary-details-objectives-context-links instances=player-party-world-history-variables-time ui=visible-empty-error-minimap-validation-simulation forgescript=typed-handle mcp=filtered-dry-run transaction=identified audit=generated history=undo-redo persistence=project-workspace-roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R008_QUEST_PASS transition=schema-event-priority-idempotence-order conditions=typed actions=typed simulation=persisted-events-effects-deadlock-step-limit lint=references-unreachable-deadlock-cycle-conflict-reward-objective ui=editor-minimap-trace forgescript=typed-handle-sandbox mcp=filtered-dry-run-simulation transaction=identified audit=transition-trace history=undo-redo persistence=project-workspace-roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_PASS anchors=uuid-schema-provenance-localized-transform-tags-scope-overrides spawn=profile-clearance-priority-team-orientation-fallback-navigation portal=cross-zone-orientation-velocity-preload validation=identity-references-bounds-clearance ui=navigation-tab forgescript=typed-handle-query-runtime mcp=filtered-dry-run history=undo-redo persistence=project-workspace-roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R010_GAMEPLAY_PASS movement=ground-profile-validated interaction=focus-verbs-hold camera=8-profiles input=contexts-devices-glyphs-rebind save=chunks-sha256-autosave-rollback-migrations-dependencies ui=five-panels command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R011_GAMEPLAY_SYSTEMS_PASS blackboard=typed-save-replication-debug forgescript=typed-handles ruleset=attributes-effects-abilities-inventories-progression-ui ui=blackboard-rules-plugin command=dry-run-apply history=undo-redo persistence=roundtrip mcp=filtered-read-plan'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_PASS language=angelscript-2.38-c-like-static api=1.0-versioned source=canonical-as-uuid-schema-provenance bytecode=cache-source-api-compiler compiler=incremental-parallel-stable-diagnostics events=per-component-multi-handler-priority-condition-authority entry=typed-payload ui=editor-console-clickable-diagnostics command=preview-apply-undo-redo mcp=api-compile-diagnostics-tests-assign-event'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_PASS world=area-query-transform-spawn-destroy movement=task-mode-velocity-teleport-completion utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-blackboard sandbox=allowlist-no-file-network-process-native-reflection-engine-dotnet ui=catalog-editor-console-diagnostics command=preview-apply-undo-redo'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_PASS handles=typed-generational-stale-validated budgets=handler-tick-module-memory-timeout-cancel determinism=seed-clock-server version=semver-negotiation-deprecation-migration ui=presets-summary-telemetry command=preview-apply-undo-redo mcp=version-capabilities'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R015_SCRIPT_MODULE_PERMISSIONS_PASS policy=module-kit-event-deny-default authority=server signature=trusted-production-dev-explicit ui=empty-error-capabilities-diagnostics-console command=preview-apply-undo-redo persistence=canonical-json packaging=validated mcp=documented'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R097_SCRIPT_AUTHORING_GAMEPLAY_PASS editor=syntax-highlighting-completion-signature-docs-snippets-symbols-formatting wizard=typed-ast-reviewed-source actions=queue-priority-cancel-complete-authority combat=damage-attack-context-effects-attribution-seed-transaction-rollback effects=definition-spec-six-stacking-duration-period-source-dispel-serialization ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-apis mcp=get-api-compile-diagnostics-tests-sandbox runtime=optional-safe-required-diagnostic rpgkit=services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R098_SCRIPT_WORLD_APIS_PASS inventory=transaction-ownership-query-capacity-rollback-audit-revision quest=transition-objectives-history-cause-instance-scope-revision dialogue=graph-participants-variables-complete-interrupt-safe faction=membership-reputation-cause-scope-authority-bounds-revision perception=sense-los-range-stimulus-confidence-seed-trace ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-handles mcp=get-api-compile-diagnostics-tests-sandbox runtime=optional-safe-required-diagnostic rpgkit=world-service-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R099_SCRIPT_RUNTIME_TOOLS_PASS audio=cues-state-fades-snapshots-listener-completion time=clock-timers-schedules-simulation-calendar-delay events=namespaced-schema-version-subscription-trace hot_reload=handler-boundary-migration-invalidation-restart debugger=breakpoints-step-stack-locals-watches-events-entities-remote-permissions ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-handles mcp=get-api-compile-diagnostics-tests-assign-event runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R100_SCRIPT_QUALITY_PASS tests=testworld-fixtures-mocks-assertions-ci-deterministic profiler=per-handler-cpu-instructions-allocations-hotspots-call-graph-alerts docs=generated-versioned-examples-availability-mcp-resource analysis=heartbeat-query-null-deprecated-authority-localized state=registered-components-no-vm-pointers-migration-validation-quota ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-tooling mcp=get-api-compile-diagnostics-tests-assign-event runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R101_SCRIPT_INTEROP_PASS tools=get-api-create-update-compile-diagnostics-run-tests-assign-event identity=uuid-schema-provenance-api-event-binding validation=required-unique-resolved-schema-compile-api-quotas-tests-localized ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip editor=canonical-command-parity mcp=versioned-transactional-no-external-execution runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R102_NETWORK_FOUNDATION_PASS dedicated=headless-service-health authority=intents-server-replicated-audited party=invitations-roles-leader-loot-quests-markers-separation replication=components-delta-ownership-prediction-reconciliation interest=area-distance-visibility-party-ownership-explicit identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R103_NETWORK_OPERATIONS_PASS content=manifest-chunks-hashes-cache-resume-delta-trust version=lockfile-fingerprint-before-join-readable-mismatch roles=player-moderator-builder-gm-admin-spectator-custom-abac chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges events=typed-subscriptions-priority-trace-replay-backpressure identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R104_NETWORK_ASSURANCE_PASS logs=json-text-categories-correlation-privacy-rotation-remote-sinks anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R105_LIVE_DIRECTOR_ACCESS_PASS auth=gm-role-mfa-expiry-revoke-audit inspect=components-stats-variables-scripts-ownership-history-read-only players=identity-character-zone-latency-party-permissions-voice-moderation-refresh audit=actor-command-targets-before-after-reason-correlation-hash-chain chat=local-party-tell-server-gm-faction-impersonation-moderation-logs identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R125_LIVE_DIRECTOR_WORKSPACE_PASS client=separate-engine-independent-auth-mfa-expiry-revoke-audit creator=allowlist-package-text-type-cost-zone-permission-thumbnails spawn=bounded-single-formation-scatter-coordinates-preview-atomic-stable-undo chooser=hierarchy-zones-instances-filters-diagnostics-privacy focus=spectator-camera-cross-zone-permission-readonly-audit identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R126_LIVE_DIRECTOR_ACTIONS_PASS relocate=navigation-consent-party-orientation-preview destroy=cause-confirm-loot-quest-rollback heal=health-resources-effects-all-reason-preview-adapter limbo=identity-ai-movement-collision-visibility-adapter possess=bounded-input-return-consent-expiry-adapter identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R127_LIVE_DIRECTOR_AUTHORITY_PASS impersonate=chat-dialogue-voice-animation-disclosure protect=bounded-damage-effects-all-indicator currency=capped-wallet-persistent-preview-audit xp=capped-ruleset-level-persistent-preview-audit door=locked-unlocked-open-repaired-relinked-runtime identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R128_LIVE_DIRECTOR_GROUP_PASS trigger=enter-exit-actor-trace selection=bounded-permission-stable-lasso move=formations-path-preview-partial-adapter attack=faction-ack-objective-adapter travel=spectate-teleport-summon-player-party-confirm-consent-camera identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R129_LIVE_DIRECTOR_OPERATIONS_PASS search=index-next-saved-deeplink-focus script=registered-signed-no-shell-process-filesystem-network-server-adapter variables=bool-number-enum-string-history-persistent scenes=spawn-dialogue-music-weather-triggers-rollback-adapters visibility=hidden-secrets-navigation-encounters-player-overlay identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R130_LIVE_DIRECTOR_NARRATIVE_PASS quests=inspect-all-selected-consequence-preview-runtime encounters=activate-pause-wave-scale-despawn-bounded-spawn-ai identity=uuid-schema-provenance-scope-revision-audit validation=localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R106_BUILD_COLLABORATION_PASS bulk=glob-type-filter-dry-run-conflicts-provenance-summary-atomic merge=three-way-uuid-lists-graphs-conflict-ui-preserve-both-deterministic windows=portable-installer-optional-signing-prerequisites-logs-uninstaller-reproducible docs=feature-script-schema-cli-package-coverage-generated-check tasks=goal-scope-forbidden-tests-owner-dependencies-machine-readable identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized ui=visible-empty-error command=domain-parity history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=docs-project-config runtime=build-only-no-gameplay'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_PASS bulk=filters-collisions-provenance-dry-run-report blueprints=selected-transitive-dependencies-licenses-migrations-previews manifest=uuid-schema-content-addressed-chunks-compression-platform-variants-provenance font=ttf-fallback-shaping-dpi-text-scale-localization-tests-governed-chunks spatial=separate-navigation-collision-generation-manual-materials-platform-cooking identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized ui=asset-browser-content-workflow-workspace-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-content-tools mcp=asset-install-plan runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R109_CONTENT_ROUNDTRIP_PASS animation=clips-library-retarget-root-motion-events-compression-preview tables=typed-schema-references-grid-migrations-package-overrides external=open-tool-file-watcher-controlled-reimport-metadata-diff blender=typed-primitives-validation-lod-collision-export-no-arbitrary-code platform=build-profile-variants-any-fallback-required-role-parity identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized ui=asset-browser-content-workflow-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-worker mcp=typed-no-shell runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_PASS migration=trusted-host-exact-step-dry-run-backup-changelog-compatibility-apply-restore-rollback uninstall=impact-dependents-replacement-backup-refuse-unresolved identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized ui=asset-browser-visible-empty-error command=dry-run-apply history=restore-backup persistence=filesystem-roundtrip cli=structured-no-shell mcp=typed-no-shell runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R114_EE_RENDERING_PASS tabs=eight-persistent-active-two-viewports-split-readonly-shared-selection-safe pbr=metallic-roughness-import-channels-material-instances-validation-profiles color=agx-aces-filmic-linear-exposure-whitepoint-lut-loaded-hdr-viewport-test-scene lighting=forward-plus-per-pixel-quality-shadow-cluster-budgets-debug identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized ui=productivity-environment-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell runtime=engine-renderer-optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R115_EE_PERFORMANCE_PASS lights=data-driven-platform-dynamic-shadow-atlas-threshold-profiler-warning-no-parity-cap lod=asset-hlod-import-generation-hooks-thresholds-crossfade-popping-performance-validation rules=classes-progression-weapons-vfx-casting-json-schemas-no-engine-recompile nui=declarative-reactive-bindings-events-themes-permissions gui=typed-commands-focus-input-contexts-test-automation identity=uuid-schema-provenance-license-hash validation=required-unique-resolved-schema-localized ui=environment-area-assets-quality-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell runtime=engine-renderer-ui-optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R116_EE_RUNTIME_DATA_PASS json=typed-versioned-schema-validation-quota-pointer-safe snapshot=components-references-inventory-blackboard-schema-save-restore copy=uuid-owner-references-state-transaction-authority aoe=mesh-targets-cover-height-colorblind-server-confirmation loading=async-prefetch-progress-cache-configurable-cancellation-sequential-fallback-benchmark-gate-stable-diagnostic identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized ui=runtime-extensions-terrain-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell runtime=engine-adapter-optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R117_EE_UI_TOOLING_PASS nui=versioned-layout-forms-lists-canvas-accessibility-localization-inspector font=system-truetype-fallback-shaping-dpi-user-text-scale-reflow-controller-focus-screenshot-suite transform=tween-transform-material-state-easing-cancellation-authority-save compiler=conformance-stable-diagnostics-incremental-telemetry identity=uuid-schema-provenance validation=required-unique-resolved-schema-localized ui=runtime-extensions-creator-quality-visible-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip cli=structured-no-shell mcp=typed-no-shell runtime=ui-transform-compiler-optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R121_NETWORK_PLAYER_EXPERIENCE_PASS discovery=direct-lan-directory-favorites-recents-invites lobby=rules-content-slots-teams-characters-permissions-ready builder=ruleset-templates-validation-preview-import-server-approval characters=local-signed-server-account-encryption-migration combat=global-area-party-consent-duel-crime-safe-zone-server-override identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R122_NETWORK_WORLD_ADMIN_PASS instances=lifecycle-party-persistence-capacity-templates-garbage-collection-scene dynamic=template-clone-overrides-bounded-resources-server-authority auth=local-oauth-oidc-platform-anonymous-stable-private-hashed admin=authenticated-api-cli-rate-audit-health-kick-ban-config-safe moderation=account-ip-device-mute-timeout-kick-ban-reports-evidence-appeals-retention identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R123_NETWORK_PERSISTENCE_OBSERVABILITY_PASS persistence=embedded-relational-transactions-migrations-backups-failover sql=server-only-parameterized-allowlisted-domain-services-no-raw metrics=prometheus-opentelemetry-tps-tick-memory-connections-queues-scripts-health profiler=sampling-instrumentation-traces-tick-script-db-network-bounded reconnection=grace-token-entity-party-transactions-fallback-single-use identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R124_NETWORK_RESILIENCE_CAPACITY_PASS backup=snapshot-wal-rehearsal-migrations-pitr-retention-confirmation capacity=configurable-caps-queue-reserved-slots-load-shedding-position-metrics identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_PASS package=uuid-schema-namespace-semver-license-hash priority=explicit-sealed-conflict-preview dependencies=required-optional-peer-offline-lock sources=text-one-resource-per-file compiler=validate-resolve-script-hash-compress-reproducible ui=visible-empty-error forgescript=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_PASS localization=icu-fallback-pseudo-translator texture=png-tiff-exr-ktx2-pbr-compression-texel model=gltf-glb-rig-animation-collision-sockets-lod namespaces=package-type-path-uuid-aliases browser=facets-preview-references-package-license-status-actions ui=visible-empty-error forgescript=typed-handles mcp=filtered-dry-run history=undo-redo persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R018_ASSET_GOVERNANCE_PASS provenance=visible quarantine=pending ip=classified dry_run=true persistence=atomic undo_redo=true distribution=guarded'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R019_QA_FOUNDATIONS_PASS licenses=spdx-provenance-notice-block domain=covered headless=scenarios save=roundtrip-legacy-partial-missing-rollback-deterministic logs=severity-category-object-source-transaction-privacy-retention dry_run=true persistence=atomic undo_redo=true ui=configurable'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R020_AGENT_WORKFLOW_PASS context=10-structured graph=reverse-impact-typed tools=semantic-domain plan=goal-commands-assumptions-objects-risks-tests dry_run=revision-batch-diff-no-write permission=bounded history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R021_AGENT_GOVERNANCE_PASS transaction=begin-preview-commit-rollback-atomic diff=objects-references-states-assets-generated permission=read-propose-confirmed-autonomous-bounded audit=client-model-user-plan-commands-hashes-tests-provenance-rollback privacy=classification-opt-in-redaction-no-secret budget=provider-token-cost history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R022_AGENT_SAFETY_PASS output=json-schema-tool-calls-bounded-repair ambiguity=rejected injection=data-provenance-allowlist-no-escalation-confirmation offline=manual-local-optional-metadata-optional-build-runtime-independent network=disabled python=allowlisted-no-exec history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R023_BUILD_COLLABORATION_PASS cache=content-addressed-disposable-rebuildable normalize=json-utf8-lf-order-numbers-atomic migration=dry-run-backup-idempotent-downgrade-explicit references=forward-reverse-rebuildable diff=area-graph-entity-package-script-text-json cli=structured mcp=shell-free history=undo-redo persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_PASS wizard=template-kits-platform-rules-render-network-summary metadata=localized identity=uuid-slug-namespace-revision-format area=rpg-required-core-optional spawn=zone-waypoint-orientation-mode-class-player-rule mcp=dry-run history=undo-redo persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_PASS lifecycle=editor-client-server-ordered blackboard=strict-schema-legacy-bounded packages=version-order-conflict-lock-provenance cache=source-api-compiler-reproducible targets=editor-client-server-test-shipping-secrets-separated ui=visible-summary'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R026_PROJECT_COMPATIBILITY_PASS profile=uuid-schema-provenance runtime-min=manifest project-format=versioned migration=automatic-dry-run-backup-idempotent runtime=required-diagnostic-optional-safe ui=visible-command-history forgescript=typed-handle mcp=filtered-plan-apply-revert persistence=legacy-roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R027_QA_VALIDATION_PASS schemas=versioned references=typed graphs=generic geometry=scale-polygon navigation=agents-doors-links-flight ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip cli=structured mcp=structured build=preflight diagnostics=code-path-uuid'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_PASS wizard=kit-terrain-lighting-navigation-streaming-biome-seed-budget-preview document=uuid-schema-provenance-environment-instances-volumes-navigation-dependencies tiles=grid-sockets-rules-groups-brushes-runtime rotation=kit-policy-mirror-semantic-variant-raccord-preview lifecycle=entered-exited-actor-transition-context-authority-script-api ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip forgescript=typed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_PASS portal=schema-provenance-orientation-velocity-hooks-preview navigation=surface-agents-links-polygons-overlay play_from_here=spawn-camera-waypoint-character-profile-quest-manifest validation=9-categories-ui command=preview-apply history=undo-redo persistence=roundtrip mcp=filtered-plan-apply-revert forgescript=typed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R036_EE_PASS scale=gizmo-x-y-z-uniform-constraints-multiedit-command-history-persistence collision=preview-rebuilt navigation=canonical-fingerprint-overlay-agent-profiles-dynamic-obstacles-recovery benchmark='
        $Markers += 'OPENRPG_FORGE_TOOLSET_R037_COMPANION_PASS validation=identity-recruitment-ownership-commands-equipment-death-dialogue-contexts-cross-zone ui=sections-preview-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_PASS ui=sections-preview-empty-error identity=localized resources=generic movement=six-modes spawn=validated-after-construction command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_PASS rules=class-progression-weapon-vfx-casting ui=declarative-reactive-themed-permission events=typed-focus-input-testable json=schema-quota-safe-api snapshots=components-references-inventory-blackboard history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11P_MODERN_PASS copy=uuid-owner-references-state-authority aoe=mesh-targets-cover-height-colorblind-confirmation nui=2.0-forms-lists-canvas-accessibility-localization-inspector compiler=incremental-stable-telemetry-conformance docs=metadata-generated-cli-mcp history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11Q_RESILIENCE_PASS tasks=goal-scope-forbidden-tests-owner-dependencies crash=sanitized-dump-version-fingerprint-reproduction health=domains-tests-performance-licenses-migrations-coverage recovery=journal-reopen-quarantine-compare sync=resume-dedup-hash-trust-storage history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11R_QUALITY_PASS localization=coverage-placeholders-plurals-overflow-pseudo scripts=sandbox-quota-mock-coverage dialogues=branches-conditions-actions quests=events-transitions-save-scope encounters=profiles-seeds-nav-cleanup'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_PASS network=latency-loss-reconnect-mismatch-authority visual=fixtures-tolerance-quality benchmarks=interior-exterior-stress-load-frame-memory-nav-package budgets=frame-memory-texture-draw-light-script-network profiler=editor-runtime-server-script-database-network'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_PASS editor=keyboard-labels-scale-contrast-shortcuts runtime=subtitles-captions-color-input-motion-difficulty fuzz=112-cases-7-boundaries-no-crash-no-escape dependencies=approval-risk-review-audit-lifecycle'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_PASS ui=context-selection-transaction providers=offline-openai-compatible-ollama prompts=versioned-tested plan=dry-run-bound permission=configure secrets=environment-only offline=deterministic cost=visible history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_PASS transports=openai-compatible-ollama network=explicit-loopback timeout=bounded cancellation=propagated lore=world-faction-character-timeline-terminology sources=resolved retrieval=hashed-bounded plan=dry-run-transaction permission=configure history=restored persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_PASS archetypes=crypt-village topology=connected-deterministic kits=resolved constraints=bounded objectives=explicit lore=sourced provider=fake-offline cost=visible geometry=not-applied plan=dry-run-transaction permission=configure-approve history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_PASS placements=batch-manual-commands seed=preserved geometry=validated navigation=no-regression lighting=validated approval=required plan=dry-run-transaction permission=apply-approved atomic=true history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_PASS graph=canonical-minimap nodes=4 conditions=typed actions=typed style=cited localization=placeholders lint=clean simulation=entry-rendered provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12F_QUEST_PASS participants=resolved states=4 objectives=bounded transitions=3 rewards=progression failure=explicit tests=5-scenarios style=cited lint=clean provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_PASS api=1.0.0 vm=2.38.0 source=generated-editable binding=typed compile=native diagnostics=structured tests=compile-api-sandbox path=confined provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12H_BOSS_PASS creature=canonical encounter=bounded abilities=typed phases=scripted dialogue=typed rewards=loot balance=32-runs dependencies=resolved provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved batch=atomic history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12I_ABILITY_PASS targeting=typed cost=bounded effects=canonical vfx=resolved audio=resolved ai=tagged script=resolved item=optional tests=5-combat balance=visible provider=fake-offline costusd=visible plan=dry-run-transaction permission=apply-approved batch=atomic history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12J_BLENDER_PASS recipe=canonical primitives=typed transforms=bounded material=pbr uv=typed lod=generated collision=generated validation=five-tests glb=export-request provider=fake-offline cost=visible python=forbidden plan=dry-run-transaction permission=apply-approved history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_PASS provider=interchangeable offline=deterministic trellis=loopback triposr=loopback input=text-image license=cc0 staging=hashed topology=bounded provenance=receipt acceptance=human blender=typed tests=five plan=dry-run-transaction permission=apply-approved history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_PASS policy=canonical uuid=stable schema=v1 python=forbidden exec=forbidden eval=forbidden shell=forbidden paths=confined network=denied operations=typed timeout=bounded output=bounded ui=visible-empty-error cost=zero transaction=identified plan=dry-run-bound permission=configure audit=generated history=restored persistence=roundtrip mcp=governed'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_PASS profiles=standard-aurora-blender commands=searchable bindings=14 conflicts=blocked input=executed ui=visible-empty-error uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=roundtrip runtime=optional'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_PASS identity=gender-body-pronouns portrait=hashed-multires-ready fallback=3d body=modular-rig-layer-clipping materials=colored attachments=wings-tail voice=bank-language-rights ui=visible-empty-error preview=3d-and-portrait uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=project-roundtrip package=canonical runtime=optional-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_PASS tracks=multiclass-levels disposition=project-axes skills=rank-training-expertise-checks talents=prerequisites-ranks-passive-hooks loadout=learned-prepared-deck-cooldown-charges-resource-modifiers ruleset=data-driven ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_PASS entries=item-slot-creature probability=bounded ownership=resolved instancing=shared-player-party theft=skill-perception-server-consent-crime-cooldown-audit corpse=container-ragdoll-lifetime-resurrection-ownership-cleanup ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=legacy-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_PASS role=ruleset-lore-tags-no-implicit-mechanics ambient=idle-schedule-interaction-waypoint-conversation-interruption-resume autolevel=deterministic-seed-preview-diff-apply-manual-lock ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_PASS combat=tick-or-turn inventory=accessed-transferred-cause-actor-result rest=duration-resources-interruptions heartbeat=cadence-suspension-budget-backlog custom=namespaced-typed ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked forgescript=api-1.0-server transaction=identified dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R039_CREATURE_DAMAGE_DEATH_PASS damage=before-restricted-after-transaction death=killer-contributors-cause-location-loot-policy validation=rig-rules-equipment-capabilities-scripts-faction-resources-budgets ui=sections-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=ordered-authoritative'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R040_DOOR_FOUNDATION_PASS placement=socket-or-free states=open-closed-opening-closing-locked-jammed-broken-disabled-custom interaction=range-side-clearance-animation-collision-authority-queue-feedback portal=typed-anchor-orientation-preload-permissions links=oneway-or-atomic-pair ui=section-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip runtime=resolved'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R041_DOOR_NAVIGATION_VALIDATION_PASS navigation=obstacle-link-animation-state-visualization blocking=tested validation=link-destination-animation-collision-nav-scripts-loop diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip runtime=synchronized'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FOUNDATION_PASS volume=2d-3d-filters-los-threat-script candidates=blueprint-weight-role-cost-conditions-variants-limits validation=geometry-references-budget-anchors-rewards-network-cap ui=visible transaction=dry-run-reject persistence=roundtrip runtime=nominal-failure'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R043_FACTION_VALIDATION_PASS validation=inheritance-cycle-parent-orphan-band-overlap-member-orphan diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_PASS type=immutable-reference-migration-preview-explicit-accept blueprint=versioned-instance-separated-overrides-bounded identity=uuid-slug-localized-name-short-long-unknown-label-notes validation=identity-type-slots-rig-values-effects-scripts-localization-asset-license-economy ui=visible-empty-error-sections-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R045_MERCHANT_TRANSACTIONS_PASS transaction=buy-sell-barter-aggregate-atomic-rollback validation=vendor-currency-treasury-stock-price-restrictions-localization-scripts ui=visible-sections-summary command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-authoritative'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FOUNDATION_PASS blueprint=composable-visual-collision-interaction-health-container-lock-trap-physics-scripts static=batching-instancing-occluder-merge interaction=verbs-distance-angle-focus-prompt-duration-authority-conditions-event identity=uuid-slug-localized-visual-icon-portrait-tags-notes transform=position-rotation-scale-pivot-snap-parent-surface-animation-overrides ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_VALIDATION_PASS onused=typed-event-verb-actor-target-result-provenance-api-before-after validation=collision-navigation-animation-locks-traps-inventory-scripts-physics-budgets diagnostics=stable-path-uuid ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R048_TRIGGER_FOUNDATION_PASS volume=2d-extruded-or-3d-vertices-holes-height-snap-transform-overlay generic=filters-conditions-cooldown-fire-policy-actions portal=typed-destination-anchor-orientation-preload-permissions events=entered-entity-point-direction-layers-state-exited-duration-reason-api-scripts ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R049_TRIGGER_VALIDATION_PASS validation=geometry-self-intersection-filters-actions-targets-portal-cooldown-scripts-accessibility diagnostics=stable-path-uuid ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R050_THIRD_PARTY_NOTICES_PASS aggregation=module-blueprints-data-tables-content-packages-sources-assets-dependencies spdx=approved-required-unknown-blocked notice=deterministic-preview-export-recalculated-mismatch-rejected diagnostics=stable-path-uuid ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip cli=structured mcp=bounded-export build=preflight-before-write'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R120_AI_TEST_AUTOMATION_PASS profile=uuid-schema-provenance-bounds-allowlist agent=in-process-gameplay-actions-blockers-coverage-reproduction dialogue=paths-contradictions-tone-outcomes quest=unreachable-loops-rewards-multiplayer-save-resume narrative=citations-suggestions-no-auto-rewrite ui=visible-empty-error-results transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip mcp=bounded-shell-free build=preflight-before-write'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R051_PROJECT_EVENTS_PASS profile=uuid-schema-provenance bindings=14-server families=connection-inventory-use-death-respawn-equipment-progression ui=visible-empty-error command=dry-run-apply history=undo-redo persistence=project-package-roundtrip forgescript=typed-handle runtime=optional-safe-required-diagnostic mcp=filtered-plan-apply-revert'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R052_PROJECT_WORLD_PASS custom=namespaced-schema-payload-version-server clock=speed-calendar-zone-pause-astronomical-save ui=visible-empty-error command=dry-run-apply history=undo-redo persistence=project-package-roundtrip forgescript=typed-handle runtime=optional-safe-required-diagnostic mcp=filtered-plan-apply-revert'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R052_PROJECT_DOCS_PASS pages=8-localized synopsis-installation-rules-controls-credits-licenses-warnings-compatibility ui=visible-empty-error-tabs command=dry-run-apply history=undo-redo persistence=project-package-roundtrip mcp=filtered-plan-apply-revert build=preflight'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R053_AREA_TILE_EDITING_PASS groups=parameters-footprint-sockets-variants-convert-editable tools=erase-replace-remap-fill-select-preserve-attached autofit=socket-seed-manual-lock-ambiguity-report properties=state-animation-material-light-collision-navigation-sockets-overrides resize=anchor-loss-preview-reposition-migrate ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=project-workspace-roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R054_AREA_ENVIRONMENT_PASS clone=full-template-new-uuids-remap-shared-references-diff terrain=navigation-lighting-preserved classification=extensible-render-audio-navigation-weather-camera-gameplay daynight=clock-curves-sun-moon-transitions-events-volume-overrides lighting=physical-presets-temperature-intensity-hdr-shadows-probes-simple-advanced fog=distance-volumetric-density-height-color-noise-local-volumes-quality ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R055_AREA_EXPERIENCE_PASS audio=day-night-combat-ambience-snapshots-adaptive-reverb-transitions events=area-namespaced-payload-api-subscriptions-runtime map=generated-2d-3d-player-party-world-pins-floors-fog-reveal visibility=rooms-portals-occluders-culling-distance-streaming-cell-visualizer lod=area-asset-import-generated-hybrid-thresholds-budgets-popping-crossfade ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R056_AREA_LAYERS_PASS layers=nested-drag-drop visibility=inherited locking=inherited ownership=explicit selection=group-descendants-viewport export=partial-layers-entities-blueprints-preview identity=uuid-schema-provenance ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R057_PALETTE_PASS blueprint=instance-diff-selected-overrides-semver-impact-preview instances=assisted-migration-pins-exclusions-rollback-overrides-preserved taxonomy=hierarchical-custom-multitag-virtual-folders-facets preview=thumbnail-turntable-variants-animations-metrics-license search=text-tags-dimensions-style-skeleton-material-polycount-license-author-compatibility-variant ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R058_PREFAB_PASS variant=inherited-component-material-stat-script-overrides composition=hierarchy-sockets-rules-parameters-scripts collections=author-persistent placement=atomic-group budget=triangles-materials-textures-memory-animations-lod-collision-platform-gates identity=uuid-schema-provenance-manifest-spdx-sha256 ui=visible-empty-error-preview command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert build=preflight runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R059_SHELL_PRODUCTIVITY_PASS filters=layers-volumes-profiled placement=brush-repeat-distribution-surface-scatter-atomic tabs=split-two-viewports-comparison-copy-protected help=docs-properties-forgescript-dependencies-cited profiles=seven-panels-permissions mcp=filtered-plan-apply-revert persistence=roundtrip history=restored'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R060_RECOVERY_PASS policy=uuid-schema-provenance-configurable autosave=versioned-atomic-retained session=unclean-detected-clean-close snapshot=hash-journal-quarantine-confined compare=before-restore selective=module-blueprints-areas-dialogues-quests transaction=dry-run-apply-undo-redo persistence=roundtrip mcp=resource-plan-apply-revert runtime=optional-safe-required-diagnostic history=restored'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R061_AUDIO_PASS browser=search-tags-transcript-waveform-audition-batch-governed selection=sequential-random-shuffle-weighted-seeded-no-repeat modulation=ranges-distributions-curves-tempo-statistics mix=lufs-bus-ducking-distance-cap-meter ambient=2d-area-listener-personalized command=dry-run-apply-undo-redo persistence=roundtrip mcp=filtered-plan-apply-revert runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R062_AUDIO_PASS range=inner-outer-curve-air-debug-spheres script=typed-handle-start-stop-pause-resume-fade-authority-events-api-v1 ambience=time-weather-location-danger-indoor-volume-crossfade music=day-night-exploration-tension-combat-boss-victory-stems-cooldown-beat-sync mix=hierarchy-snapshot-platform-dynamic-accessibility-live-monitor command=dry-run-apply-undo-redo persistence=roundtrip mcp=filtered-plan-apply-revert runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_PASS metadata=dialogue-effect-ambient-locales-fallback-importance-direction-duration identity=uuid-schema-provenance-manifest-spdx-sha256 ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-plan-apply-revert distribution=ungoverned-blocked runtime=optional-safe-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_PASS appearance=icon-color-layer-visible-constant-screen-size map=text-icon-floor-category-precision-policy-tracking reveal=player-party-world-event-animation-persistence patrol=ordered-wait-facing-speed-action-branch-weight-uuid-edges traversal=loop-pingpong-once-random-weighted-scripted ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=authoritative-hud-patrol optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_PASS arrival=direction-target-animation-wait-interact-callback-authority locator=audio-vfx-spawn-objective-gameplay-purpose-tags-assets-overrides collection=query-tags-purpose-stable-nearest-random-deterministic-preview route=segments-direction-costs-actions-broken-links-nav-projection-timing ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=arrival-locators-route-diagnostics optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_PASS flight=3d-input-altitude-banking-lift-landing-volumetric-navigation projectile=ballistic-homing-hitscan-collision-prediction-vfx-authoritative-impact-revision-audit physics=rigid-kinematic-static-joints-triggers-character-bounds-deterministic-network targeting=self-entity-point-direction-area-cone-line-chain-preview-validity-input world=clock-calendar-local-scales-pause-domains-schedules-save ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_PASS save=player-game-server-authority-conflict-revision-cloud-privacy-encryption checkpoint=waypoint-cost-party-corpse-safe-spawn-hooks-authority shell=kit-data-driven-skinnable-scalable-input-agnostic-map-journal-hud control=owner-cursor-camera-actions-stack-guaranteed-restoration replay=tick-input-event-seed-hash-headless-divergence-minimal-redacted-package ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-shell-control-save-replay optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_PASS tokens=typed-autocomplete-preview-escaped participants=role-query-tag-entity-node-speaker-fallback evaluation=first-all-weighted-random-deterministic-manual wizard=visual-typed-source notes=intent-emotion-source-status-todo-author ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_PASS lifecycle=completed-aborted-reason-rollback-resume search=multi-dialogue-safe-regex-speaker-condition-action-preview review=condition-action-voice-localization-error-link-legend-filter memory=dialogue-participant-player-party-world-duration-typed skill=difficulty-opponent-advantage-result-visibility-explanation-deterministic ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_PASS subgraph=versioned-input-output-variable-contract-isolated-test consequences=quest-faction-item-world-impact coverage=paths-required-state-impossible-test-session-ratio collaboration=canonical-node-json-stable-id-revision-lock-comment-semantic-diff-conflict-resolution ai=brief-citations-style-preview-lint-authorization-plan-transaction-semantic-diff-explicit-apply ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-plan-apply-revert runtime=engine-adapters optional=ignored-required-diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_PASS objectives=counter-boolean-collection-destination-escort-survival-choice-custom rewards=atomic-progression-items-currency-reputation-unlocks-choices notes=design-canon-owner-status-risks-todo-sources-review-history cast=role-blueprint-instance-query-presence-required wizard=zones-cast-props-dialogues-objectives-transitions-five-tests-no-opaque-code ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=engine-adapters optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_PASS resources=item-placeable-door-zone-asset-ownership-resolved steps=template-version-objectives-transitions-editable-provenance visibility=hidden-rumor-discovered-tracked-secret-completed-conditions history=timestamp-transition-cause-actor-player-debug markers=zone-entity-query-waypoint-visibility-precision-tracking-fallback ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history-edit=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=engine-adapters optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R073_QUEST_QUALITY_PASS tests=nominal-failure-repetition-multiplayer-save-resume-seed-fixtures-nodes-edges-assertions-runner ai=synopsis-states-participants-dependencies-citations-style-lint-human-approval-transaction-plan-semantic-diff-authorization ui=visible-empty-error-minimap-validation-simulation command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=engine-adapters optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R074_COMPANION_FOUNDATION_PASS recruitable=component-policy dialogue=not-recruited-owner membership=preview-apply-dismiss-capacity-ownership-inventory-position-persistence-atomic party=companions-summons-pets-allies-roles-server-limit ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-engine-nodes'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R075_COMPANION_CONTROL_PASS ownership=individual-party-shared-ai-only-gm-transfer-audit-revision movement=move-follow-hold-rally-guard-interact-formation-waypoint-priority-authority tactical=attack-defend-focus-fire-protect-disengage-use-ability-stance-target-priority ai=autonomy-risk-resources-friendly-fire-role-distance-abilities-plan-transaction-authorization equipment=permission-locks-loadout-compare-ownership-semantic-diff ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-engine-nodes'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R076_COMPANION_EXPERIENCE_PASS death=downed-permanent-corpse-resurrection-replacement-memorial-inventory-events cohesion=navigate-portal-safe-teleport-combat-visible-audit hud=portraits-health-resources-statuses-quick-commands-reorder-keyboard-gamepad-screen-reader-high-contrast ui=visible-sections-summary-empty-error command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-engine-nodes-visible-hud'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_PASS ui=wizard-sections-empty-error-preview-graph-simulation role=encounter-threat-tags species=inheritance-traits-variants-skeleton appearance=archetype-mesh-rig-morph-material-palette-preset animation=graph-uuid-ordered-locomotion-combat-emote-additive-retarget command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_PASS ui=sections-empty-error-preview-damage-simulation initial=alive-unconscious-dead-sleeping-seated-frozen-loop-pose-invisible attributes=ruleset-base-modifiers-derived-inspectable defenses=avoidance-armor-resistances-saves-immunities-mitigation abilities=level-charges-cooldown-conditions-ai-tags-script inventory=containers-slots-weight-volume-ownership-equipment-persistence command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R079_CREATURE_INTERACTION_PASS ui=sections-empty-error-preview-dialogue-simulation factions=multiple-weighted-primary-rank-reputation-inheritance perception=vision-hearing-smell-magic-range-cone-occlusion-stimuli-memory dialogue=context-priority-interlocutor-state-fallback-selection-refusal protection=invulnerability-narrative-death-interruption-gm-permissions onconversation=interaction-requested-dialogue-starting-server-binding command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_PASS ui=sections-empty-error-preview-decision-simulation onperception=stimulus-detected-lost-sense-confidence-position-memory onphysicalattacked=declared-resolved-attacker-weapon-target-result-network onspellcastat=ability-targeted-impacted-source-target-tags-result-magnitude onblocked=obstacle-cause-nav-status-position-wait-repath-avoid-teleport-abort ai=state-machine-behavior-tree-utility-script-typed-inspectable-blackboard-bounded-actions command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R081_DOOR_ACCESS_EVENTS_PASS lock=credentials-skill-relock-consume rejection=reason-localized-sound-animation-script trap=detect-disarm-payload-reset-owner-side-event portal=requested-actor-destination-cause-conditions-veto-completed state=opened-closed-terminal ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-adapter'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R082_DOOR_LOCK_EVENTS_PASS events=locked-unlocked payload=method-actor-credential-state-provenance terminal=guarded ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-view mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R083_ENCOUNTER_COMPOSITION_PASS wizard=role-theme-zone-difficulty-groups-budget-activation-spawn difficulty=ruleset-party-estimator-multipliers count=min-max-budget-server-cap selection=provider-challenge-seeded-preview activation=once-repeat-timed-wave-persistent-population-script ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-authoring-composition-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R084_ENCOUNTER_SCENARIO_PASS anchors=transform-capacity-navigation-visibility-fallback-distribution activator=players-only-summons-companions-gm-spectators-authority lifecycle=entered-exited-duration-exhausted-full-payload waves=uuid-order-conditions-groups-delay-objective-voice-transition ui=visible-empty-error-overlay-graph-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-composition-lifecycle-wave-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R085_ENCOUNTER_STATISTICS_PASS simulation=n-runs-composition-cost-duration-rewards variances=count-cost-duration-reward seeds=reproducible-consecutive percentiles=50-90-99 estimators=typed-data-driven ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-wave-simulation-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R086_FACTION_FOUNDATION_PASS lifecycle=create-archive-safe-remove-member-migration matrix=directional-heatmap-presets asymmetry=a-to-b-b-to-a-independent-explained bands=ruleset-configurable-result-overrides personal=actor-player-group-duration-cause ui=visible-empty-error-heatmap-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-definition-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-authoring-reputation-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R087_FACTION_REPUTATION_PASS global=world-server-campaign-group-player-transaction-inverse tags=semantic-effective-query matrix=editable-compare-simulate-import-export-history reaction=reputation-disguise-crime-dialogue-zone-gm ui=visible-empty-error-heatmap-history-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-profile-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-reputation-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R088_ITEM_CREATION_PASS wizard=archetype-appearance-slots-quality-level-property-provenance-generation-rules appearance=meshes-materials-sockets-icon-vfx-equipped-preview inventory=mass-volume-grid-stack-contribution stack=maximum-key-atomic-merge-split-replication usage=charges-rest-cooldown-durability-consumable-recharge ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-profile-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-inventory-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_PASS critical=destruction-sale-drop-theft-consumption-cleanup-gm-audit transfer=drop-trade-store-container-mail-destroy-consume-gm-permissions modifiers=data-driven-parameters-stacking-conditions-ui-hooks ability=definition-targeting-costs-charges-cooldown-events equipment=ruleset-rig-multislot-layer-conflicts-preview ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-profile-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-rpgkit-gameplay-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_PASS loot=weighted-groups-conditions-quantity-quality-seed-uniqueness-statistical-preview inspector=drag-drop-containers-slots-filters-validation-visual-preview-comparison-multi-edit events=created-acquired-transferred-equipped-unequipped-used-consumed-broken-destroyed forgescript=typed-profile-handle mcp=filtered-dry-run ui=visible-empty-error-sections-live-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip runtime=optional-safe-required-diagnostic-rpgkit-loot-inventory-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_PASS store=independent-multi-vendor-interface-channel wizard=store-vendor-dialogue-interaction-tests stock=finite-infinite-quantity-variant-supplier-restock-order-hidden pricing=buy-sell-curves-currency-reputation-scarcity-condition-negotiation restrictions=whitelist-blacklist-type-tags-quality-provenance-condition-owner-custom-rule ui=visible-empty-error-sections-summary command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=merchant-services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_PASS stock=infinite-unbounded-quantity-stable sessions=opened-closed-customer-vendor-price-context-transaction-summary-api-1.0 ui=keyboard-gamepad-compare-filters-cart-confirmation-screen-reader-high-contrast-columns command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=store-session-ui-services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_PASS container=capacity-ownership-access-persistence-loot-ui-replication-inventory state=idle-open-closed-broken-active-custom-transitions-preview damage=health-armor-resistances-stages-destruction-debris-repair protection=permissions-feedback-gm-override-audit lock=states-credentials-skill-code-hacking-relock-jam-permissions ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=interaction-security-services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_PASS trap=trigger-payload-detect-disarm-reset-owner-vfx-friendly-one-shot loot=table-init-open-reset-seed-persisted-state container=opened-closed-actor-result-previous-authority lock=changing-changed-method-credential-actor-veto disturbed=transactional-delta-cause-revision-actor ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=security-event-services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_PASS damage=applied-destroyed-uniform-source-amount-type-health-context sockets=typed-transform-tags-snap-capacity-blueprint-prefab-reversible verbs=inspect-use-open-talk-take-push-climb-custom-context-priority ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=event-attachment-verb-services-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_PASS trap=detection-disarm-payload-reset-ownership-gm-visualization query=players-tags-factions-components-owner-team-sandbox volumes=damage-heal-gravity-water-climb-nobuild-camera-audio-weather-checkpoint debug=occupants-last-fires-cooldowns-filters-event-trace ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=advanced-trigger-service-reused'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_PASS hierarchy=parents-tags-policies-relations-no-cycles witnesses=perception-memory-communication-evidence memberships=multiple-rank-cover-loyalty dynamic=temporary-runtime-alliance-war-truce-expiration ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked forgescript=typed-handle-server mcp=filtered-dry-run transaction=identified audit=generated history=restored persistence=project-workspace-package runtime=optional-safe-authoritative-replicated'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_PASS animations=timing-blend-gesture-lookat-interrupt-fallback voice=locale-take-subtitle-lipsync-emotion-duration-license-fallback camera=automatic-anchor-framing-player-priority-accessible bookmarks=personal-shared-comment-deeplink interjections=personality-presence-relation-priority-cooldown-conflicts-authority ui=visible-empty-error-summary forgescript=typed-handle mcp=filtered-dry-run transaction=identified audit=generated history=restored persistence=project-workspace-package runtime=optional-safe'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13I_ADAPTIVE_DIALOGUE_PASS timed=optional-default-accessibility-pause-server-revision-timeout-script variants=conditional-weighted-seeded-anti-repeat-localized ui=visible-empty-error-summary-simulation forgescript=typed-handle mcp=filtered-dry-run transaction=identified audit=generated history=restored persistence=project-workspace-package'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_PASS ui=tabs-8-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-dry-run persistence=project-workspace-package runtime=resolved'
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_PASS ui=tabs-6-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-preview persistence=project-workspace-package runtime=domain-director-network-qa'
        $Markers += 'streaming=async-cache-cancel sculpt=raise-'
        $Markers += 'paint=material-'
        $Markers += 'brush_history=restored history=restored persistence=roundtrip'
    }
    elseif ($Name -eq 'runtime-plain-launch') {
        $Markers += 'OPENRPG_FORGE_RUNTIME_R004_AUDIO_PASS object=audio-stream-player-3d position=canonical loop=wav-region optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R005_DIALOGUE_PASS roles=explicit order=priority-list reference=reused optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R006_DIALOGUE_PASS localization=variant-fallback conditions=all-any-not actions=transactional simulation=sandbox-journal lint=structural optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R007_QUEST_PASS metadata=localized-category-priority-owner states=uuid-order-legacy-index terminal=typed-reactivation instance=player-history-variables-time journal=summary-details-objectives-links optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R008_QUEST_PASS transition=event-priority-idempotence-order simulation=events-effects-audit-terminal lint=clean optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R071_QUEST_AUTHORING_PASS objectives=counter-boolean-collection-destination-escort-survival-choice-custom rewards=atomic-progression-items-currency-reputation-unlocks-choices notes=design-canon-owner-status-risks-todo-sources-review-history cast=role-blueprint-instance-query-presence-required wizard=zones-cast-props-dialogues-objectives-transitions-five-tests-no-opaque-code forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R072_QUEST_EXPERIENCE_PASS resources=item-placeable-door-zone-asset-ownership-resolved steps=template-version-objectives-transitions-editable-provenance visibility=hidden-rumor-discovered-tracked-secret-completed-conditions history=timestamp-transition-cause-actor-player-debug markers=zone-entity-query-waypoint-visibility-precision-tracking-fallback forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R073_QUEST_QUALITY_PASS tests=nominal-failure-repetition-multiplayer-save-resume-seed-fixtures-nodes-edges-assertions-runner ai=synopsis-states-participants-dependencies-citations-style-lint-human-approval-transaction-plan-semantic-diff-authorization forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R074_COMPANION_FOUNDATION_PASS recruitable=component-policy dialogue=not-recruited-owner membership=preview-apply-dismiss-capacity-ownership-inventory-position-persistence-atomic party=companions-summons-pets-allies-roles-server-limit forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R075_COMPANION_CONTROL_PASS ownership=individual-party-shared-ai-only-gm-transfer-audit-revision movement=move-follow-hold-rally-guard-interact-formation-waypoint-priority-authority tactical=attack-defend-focus-fire-protect-disengage-use-ability-stance-target-priority ai=autonomy-risk-resources-friendly-fire-role-distance-abilities-plan-transaction-authorization equipment=permission-locks-loadout-compare-ownership-semantic-diff forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R076_COMPANION_EXPERIENCE_PASS death=downed-permanent-corpse-resurrection-replacement-memorial-inventory-events cohesion=navigate-portal-safe-teleport-combat-visible-audit hud=portraits-health-resources-statuses-quick-commands-reorder-keyboard-gamepad-screen-reader-high-contrast forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes-visible-hud'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R077_CREATURE_AUTHORING_PASS role=encounter-threat-tags species=inheritance-traits-variants-skeleton appearance=archetype-mesh-rig-morph-material-palette-preset-visible-model animation=graph-states-transitions-locomotion-combat-emote-additive-retarget forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=role-species-model-animation-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R078_CREATURE_RULES_PASS initial=alive-unconscious-dead-sleeping-seated-frozen-loop-pose-invisible attributes=ruleset-base-modifiers-derived-inspectable defenses=avoidance-armor-resistances-saves-immunities-mitigation abilities=level-charges-cooldown-conditions-ai-tags-script inventory=containers-slots-weight-volume-ownership-equipment-persistence forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=state-attributes-defenses-abilities-inventory-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R079_CREATURE_INTERACTION_PASS factions=multiple-weighted-primary-rank-reputation-inheritance perception=vision-hearing-smell-magic-range-cone-occlusion-stimuli-memory dialogue=context-priority-interlocutor-state-fallback-selection-refusal protection=invulnerability-narrative-death-interruption-gm-permissions onconversation=interaction-requested-dialogue-starting-server-binding forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=factions-perception-dialogue-protection-event-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R080_CREATURE_EVENT_AI_PASS onperception=stimulus-detected-lost-sense-confidence-position-memory onphysicalattacked=declared-resolved-attacker-weapon-target-result-network onspellcastat=ability-targeted-impacted-source-target-tags-result-magnitude onblocked=obstacle-cause-nav-status-position-wait-repath-avoid-teleport-abort ai=state-machine-behavior-tree-utility-script-typed-inspectable-blackboard-bounded-actions forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=perception-attack-ability-blocked-ai-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R081_DOOR_ACCESS_EVENTS_PASS lock=credentials-skill-relock-consume rejection=reason-localized-sound-animation-script trap=detect-disarm-payload-reset-owner-side-event portal=requested-actor-destination-cause-conditions-veto-completed state=opened-closed-terminal forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=lock-rejection-trap-portal-state-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R082_DOOR_LOCK_EVENTS_PASS events=locked-unlocked payload=method-actor-credential-state-provenance terminal=guarded forgescript=typed-view mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=reused engine=lock-lifecycle-node'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R083_ENCOUNTER_COMPOSITION_PASS wizard=role-theme-zone-difficulty-groups-budget-activation-spawn difficulty=ruleset-party-estimator-multipliers count=min-max-budget-server-cap selection=provider-challenge-seeded-preview activation=once-repeat-timed-wave-persistent-population-script forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=authoring-composition-reused engine=wizard-difficulty-count-selection-activation-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R084_ENCOUNTER_SCENARIO_PASS anchors=transform-capacity-navigation-visibility-fallback-distribution activator=players-only-summons-companions-gm-spectators-authority lifecycle=entered-exited-duration-exhausted-full-payload waves=uuid-order-conditions-groups-delay-objective-voice-transition forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=composition-lifecycle-wave-reused engine=anchor-activator-lifecycle-wavegraph-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R085_ENCOUNTER_STATISTICS_PASS simulation=n-runs-composition-cost-duration-rewards variances=count-cost-duration-reward seeds=reproducible-consecutive percentiles=50-90-99 estimators=typed-data-driven forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=wave-simulation-reused engine=statistics-node'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R086_FACTION_FOUNDATION_PASS lifecycle=create-archive-safe-remove-member-migration matrix=directional-heatmap-presets asymmetry=a-to-b-b-to-a-independent-explained bands=ruleset-configurable-result-overrides personal=actor-player-group-duration-cause forgescript=typed-definition-handles mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=authoring-reputation-reused engine=faction-relationship-personal-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R087_FACTION_REPUTATION_PASS global=world-server-campaign-group-player-transaction-inverse tags=semantic-effective-query matrix=editable-compare-simulate-import-export-history reaction=reputation-disguise-crime-dialogue-zone-gm forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic engine=transaction-heatmap-reaction-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R088_ITEM_CREATION_PASS wizard=archetype-appearance-slots-quality-level-property-provenance-generation-rules appearance=meshes-materials-sockets-icon-vfx-equipped-preview inventory=mass-volume-grid-stack-contribution stack=maximum-key-atomic-merge-split-replication usage=charges-rest-cooldown-durability-consumable-recharge forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=inventory-reused engine=wizard-appearance-inventory-stack-usage-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R089_ITEM_GAMEPLAY_PASS critical=destruction-sale-drop-theft-consumption-cleanup-gm-audit transfer=drop-trade-store-container-mail-destroy-consume-gm-permissions modifiers=data-driven-parameters-stacking-conditions-ui-hooks ability=definition-targeting-costs-charges-cooldown-events equipment=ruleset-rig-multislot-layer-conflicts-preview forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=gameplay-reused engine=critical-transfer-modifier-ability-equipment-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R090_ITEM_OPERATIONS_PASS loot=weighted-groups-conditions-quantity-quality-seed-uniqueness-statistical-preview inspector=drag-drop-containers-slots-filters-validation-visual-preview-comparison-multi-edit events=created-acquired-transferred-equipped-unequipped-used-consumed-broken-destroyed forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=loot-inventory-reused engine=loot-inspector-events-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R091_MERCHANT_FOUNDATION_PASS store=independent-multi-vendor-interface-channel wizard=store-vendor-dialogue-interaction-tests stock=finite-infinite-quantity-variant-supplier-restock-order-hidden pricing=buy-sell-curves-currency-reputation-scarcity-condition-negotiation restrictions=whitelist-blacklist-type-tags-quality-provenance-condition-owner-custom-rule forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=merchant-services-reused engine=store-wizard-stock-pricing-query-nodes'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R092_MERCHANT_EXPERIENCE_PASS stock=infinite-unbounded-quantity-stable sessions=opened-closed-customer-vendor-price-context-transaction-summary-api-1.0 ui=keyboard-gamepad-compare-filters-cart-confirmation-screen-reader-high-contrast-columns forgescript=typed-handle-events mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=store-session-ui-services-reused engine=accessible-store-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R093_PLACEABLE_OPERATIONS_PASS container=capacity-ownership-access-persistence-loot-ui-replication-inventory state=idle-open-closed-broken-active-custom-transitions-preview damage=health-armor-resistances-stages-destruction-debris-repair protection=permissions-feedback-gm-override-audit lock=states-credentials-skill-code-hacking-relock-jam-permissions forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=interaction-security-services-reused engine=container-state-damage-protection-lock-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R094_PLACEABLE_SECURITY_EVENTS_PASS trap=trigger-payload-detect-disarm-reset-owner-vfx-friendly-one-shot loot=table-init-open-reset-seed-persisted-state container=opened-closed-actor-result-previous-authority lock=changing-changed-method-credential-actor-veto disturbed=transactional-delta-cause-revision-actor ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=security-event-services-reused engine=trap-loot-container-lock-disturbed-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R095_PLACEABLE_COMPOSITION_EVENTS_PASS damage=applied-destroyed-uniform-source-amount-type-health-context sockets=typed-transform-tags-snap-capacity-blueprint-prefab-reversible verbs=inspect-use-open-talk-take-push-climb-custom-context-priority ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=event-attachment-verb-services-reused engine=damage-socket-attachment-contextual-verb-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R096_TRIGGER_ADVANCED_PASS trap=detection-disarm-payload-reset-ownership-gm-visualization query=players-tags-factions-components-owner-team-sandbox volumes=damage-heal-gravity-water-climb-nobuild-camera-audio-weather-checkpoint debug=occupants-last-fires-cooldowns-filters-event-trace ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run runtime=optional-safe-required-diagnostic rpgkit=advanced-trigger-service-reused engine=area3d-trap-ten-gameplay-volumes-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R009_WAYPOINT_PASS waypoint=named-localized spawn=priority-clearance-team-orientation-fallback-navigation portal=destination-orientation-velocity-preload forgescript=typed-handle-query optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R010_GAMEPLAY_PASS movement=acceleration-slope-step-jump-crouch-root-motion-prediction interaction=focus-verbs-hold targeting=view-cone camera=8-profiles input=contexts-devices-glyphs-rebind save=chunks-checksum-autosave-rollback-migrations forgescript=typed-handles'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R011_GAMEPLAY_SYSTEMS_PASS blackboard=typed-schema-save-replication-debug forgescript=typed-handle-command ruleset=attributes-effects-abilities-inventories-progression-ui optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R012_SCRIPT_FOUNDATION_PASS language=angelscript-2.38-c-like-static source=canonical-as bytecode=cache-source-api-compiler compiler=native-stable-diagnostics events=multi-handler-priority-condition-authority entry=typed-payload sandbox=quota-limited optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R013_SCRIPT_WORLD_API_PASS world=canonical-intents-opaque-handles movement=task-mode-velocity-teleport-completion utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-local sandbox=no-file-network-process-native-reflection-engine-dotnet recovery=quota-safe'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R014_SCRIPT_SAFETY_POLICY_PASS handles=typed-generational-validated budgets=handler-tick-module-memory-timeout-cancel-recovery telemetry=instructions-memory-time determinism=seed-clock-server version=semver-negotiation-deprecation-migration policy=preview-apply-undo-redo'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R015_SCRIPT_MODULE_PERMISSIONS_PASS policy=module-kit-event-intersection deny=undeclared authority=server signature=trusted-production optional=ignored required=diagnostic sandbox=quota-abort-recovery'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R097_SCRIPT_AUTHORING_GAMEPLAY_PASS editor=syntax-highlighting-completion-signature-docs-snippets-symbols-formatting wizard=typed-ast-reviewed-source actions=queue-priority-cancel-complete-authority combat=damage-attack-context-effects-attribution-seed-transaction-rollback effects=definition-spec-six-stacking-duration-period-source-dispel-serialization ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-apis mcp=get-api-compile-diagnostics-tests-sandbox runtime=optional-safe-required-diagnostic rpgkit=services-reused engine=typed-intents-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R098_SCRIPT_WORLD_APIS_PASS inventory=transaction-ownership-query-capacity-rollback-audit-revision quest=transition-objectives-history-cause-instance-scope-revision dialogue=graph-participants-variables-complete-interrupt-safe faction=membership-reputation-cause-scope-authority-bounds-revision perception=sense-los-range-stimulus-confidence-seed-trace ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-handles mcp=get-api-compile-diagnostics-tests-sandbox runtime=optional-safe-required-diagnostic rpgkit=world-service-reused engine=typed-world-intents-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R099_SCRIPT_RUNTIME_TOOLS_PASS audio=cues-state-fades-snapshots-listener-completion time=clock-timers-schedules-simulation-calendar-delay events=namespaced-schema-version-subscription-trace hot_reload=handler-boundary-migration-invalidation-restart debugger=breakpoints-step-stack-locals-watches-events-entities-remote-permissions ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-native-handles mcp=get-api-compile-diagnostics-tests-assign-event runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery engine=audio-clock-event-reload-debug-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R100_SCRIPT_QUALITY_PASS tests=testworld-fixtures-mocks-assertions-ci-deterministic profiler=per-handler-cpu-instructions-allocations-hotspots-call-graph-alerts docs=generated-versioned-examples-availability-mcp-resource analysis=heartbeat-query-null-deprecated-authority-localized state=registered-components-no-vm-pointers-migration-validation-quota ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=versioned-tooling mcp=get-api-compile-diagnostics-tests-assign-event runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery engine=quality-services-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R101_SCRIPT_INTEROP_PASS tools=get-api-create-update-compile-diagnostics-run-tests-assign-event identity=uuid-schema-provenance-api-event-binding validation=required-unique-resolved-schema-compile-api-quotas-tests-localized ui=visible-empty-error-console-clickable command=preview-apply-reject history=undo-redo persistence=roundtrip editor=canonical-command-parity mcp=versioned-transactional-no-external-execution runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery engine=interop-services-accessible-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R102_NETWORK_FOUNDATION_PASS dedicated=headless-service-health authority=intents-server-replicated-audited party=invitations-roles-leader-loot-quests-markers-separation replication=components-delta-ownership-prediction-reconciliation interest=area-distance-visibility-party-ownership-explicit identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit engine=authoritative-session-party-replication-interest-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R103_NETWORK_OPERATIONS_PASS content=manifest-chunks-hashes-cache-resume-delta-trust version=lockfile-fingerprint-before-join-readable-mismatch roles=player-moderator-builder-gm-admin-spectator-custom-abac chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges events=typed-subscriptions-priority-trace-replay-backpressure identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-resolved-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit engine=content-access-chat-events-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R104_NETWORK_ASSURANCE_PASS logs=json-text-categories-correlation-privacy-rotation-remote-sinks anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit engine=logs-authority-determinism-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R105_LIVE_DIRECTOR_ACCESS_PASS auth=gm-role-mfa-expiry-revoke-audit inspect=components-stats-variables-scripts-ownership-history-read-only players=identity-character-zone-latency-party-permissions-voice-moderation-refresh audit=actor-command-targets-before-after-reason-correlation-hash-chain chat=local-party-tell-server-gm-faction-impersonation-moderation-logs identity=uuid-schema-provenance-scope-revision-audit validation=required-unique-schema-permission-scope-revision-audit-localized ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit engine=auth-inspection-players-audit-chat-panel'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R016_CONTENT_PACKAGES_PASS package=versioned-manifest priority=deterministic-sealed dependencies=offline-lock optional=ignored required=diagnostic sources=text-safe compiler=packaged'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R017_CONTENT_AUTHORING_PASS localization=icu-fallback resource=namespaced-alias references=resolved texture=pbr-profile model=glb-profile browser=unified optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R018_ASSET_GOVERNANCE_PASS provenance=validated quarantine=approved ip=proprietary-blocked optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R019_QA_FOUNDATIONS_PASS licenses=spdx-provenance-notice-block domain=covered headless=scenarios save=roundtrip-legacy-partial-missing-rollback-deterministic logs=severity-category-object-source-transaction-privacy-retention'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R020_AGENT_WORKFLOW_PASS context=10 graph=typed tools=5 plan=structured dry_run=no-write optional=default-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R021_AGENT_GOVERNANCE_PASS transaction=atomic-preview-required rollback=canonical diff=typed-hashed permission=confirmed-autonomous-bounded audit=redacted budget=provider-token-cost privacy=classification-opt-in-no-secret optional=default-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R022_AGENT_SAFETY_PASS output=validated injection=data-boundary permission=no-escalation offline=manual-build-runtime network=not-required python=no-public-exec optional=default-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R023_BUILD_COLLABORATION_PASS cache=ignored-generated normalize=tooling-only migration=versioned references=rebuildable diff=structured optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R024_PROJECT_FOUNDATION_PASS optional=ignored required=diagnostic identity=stable area=target-aware spawn=player-context'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R025_PROJECT_OPERATIONS_PASS lifecycle=editor-client-server-ordered blackboard=strict-schema-legacy-bounded packages=lock-provenance cache=source-api-compiler-hit targets=five-secrets-separated optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R026_PROJECT_COMPATIBILITY_PASS profile=uuid-schema-provenance runtime-min=manifest project-format=versioned migration=automatic-versioned runtime=required-diagnostic-optional-safe forgescript=typed-handle'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R027_QA_VALIDATION_PASS categories=5 schemas=versioned references=typed graphs=generic geometry=scale-polygon navigation=agents-doors-links-flight optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R028_AREA_FOUNDATION_PASS document=canonical-independent tiles=grid-sockets-rules-variants-runtime rotation=policy-mirror lifecycle=entered-exited-context-authority optional=ignored required=diagnostic forgescript=typed'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R029_AREA_RUNTIME_VALIDATION_PASS portal=orientation-velocity-bidirectional-hooks lifecycle=streaming-exit-enter navigation=surface-agents-links-polygons play_from_here=spawn-camera-waypoint-manifest-quest validation=9-categories optional=ignored required=diagnostic forgescript=typed'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11N_EXPERIENCE_PASS cues=0 scatter=0 voices=0 patrols=0 loading=accessible-progress-prefetch audio_api=started-completed-interrupted-music-snapshot patrol_api=streaming-owner-resume'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11N_LOADING_PASS media=accessiblefallback progress=1 accessible=True prefetch=true streamed=0'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11O_EXTENSIONS_PASS rules=4 ui=1 json=1 snapshot=4 api=1.0 quotas=bounded'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11P_MODERN_PASS copy=uuid-owner-references-state-authority aoe=mesh-targets-cover-height-colorblind-confirmation nui=2.0-forms-lists-canvas-accessibility-localization-inspector compiler=incremental-stable-telemetry-conformance docs=metadata-generated'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11Q_RESILIENCE_PASS tasks=bounded-machine-readable crash=sanitized-version-fingerprint-reproduction health=6-domains-healthy recovery=journal-reopen-quarantine-compare sync=resume-dedup-hash-trust-storage'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11R_QUALITY_PASS localization=coverage-placeholders-plurals-overflow-pseudo scripts=2-sandbox-quota-mock-coverage dialogues=1-branches-actions quests=1-events-save-scope encounters=9-seeds-nav-cleanup'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11S_PERFORMANCE_PASS network=9-latency-loss-reconnect-mismatch-authority visual=8-fixtures-tolerance-quality benchmarks=9-interior-exterior-stress-load-frame-memory-nav-package budgets=3-frame-memory-texture-draw-light-script-network profiler=editor-runtime-server-script-database-network'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11T_GOVERNANCE_PASS editor=7-keyboard-labels-scale-contrast runtime=7-subtitles-captions-color-input-motion-difficulty fuzz=112-cases-7-boundaries-no-crash-no-escape dependencies=0-approved-risk-review-audit-lifecycle'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G13N_AB_WORLD_FEATURES_PASS slices=15 catalog=canonical runtime=pure-domain'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R037_COMPANION_PASS policy=canonical recruitment=dialogue-script commands=4 navigation=bounded optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R038_CREATURE_FOUNDATION_PASS identity=localized resources=health-stamina movement=grounded spawn=validated-after-construction forgescript=safe-handle optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_G13AC_AL_POST_LAUNCH_PASS slices=10 profile=canonical runtime=domain-application-network-qa'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R051_PROJECT_EVENTS_PASS bindings=14 authority=server families=connection-inventory-use-death-respawn-equipment-progression transaction=veto-journal pipeline=configurable forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R052_PROJECT_WORLD_PASS custom=namespaced-typed-server clock=speed-calendar-zone-pause-astronomical-save forgescript=typed-handle docs=8-localized-pages optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_UX0G_GLOBAL_EVENTS_PASS equipment_progression=typed-server rest=four-stage-resolved heartbeat=bounded-script-path user_event=namespaced-typed-script-path forgescript=compiled-executed'
        $Markers += 'OPENRPG_FORGE_RUNTIME_UX0H_WORLD_TRANSITIONS_PASS sequence_abort=restored-script-executed world_clock=calendar-zone-astronomy-save progression=curve-source-group-difficulty runtime=resolved'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R053_AREA_TILE_EDITING_PASS groups=editable parameters=typed tools=canonical autofit=deterministic-lock-ambiguity properties=state-animation-material-light-collision-navigation-sockets-overrides resize=loss-migration forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R054_AREA_ENVIRONMENT_PASS classification=extensible-subsystems daynight=curves-events-volume-overrides lighting=physical-probes fog=noise-local-volumes-quality forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R055_AREA_EXPERIENCE_PASS audio=snapshots-adaptive-reverb events=namespaced-typed-payload-live-forgescript map=2d-3d-exploration-fog-reveal visibility=rooms-portals-occluders-streaming lod=thresholds-budgets-popping forgescript=custom-event-handle source=module optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R056_AREA_LAYERS_PASS hierarchy=nested visibility=inherited locking=inherited selection=group-descendants export=partial-governed forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R057_PALETTE_PASS blueprint=versioned-promoted-defaults instances=pins-exclusions-overrides taxonomy=hierarchical-multitag preview=turntable-thumbnail-variants-animations-metrics-license search=advanced-facets forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R058_PREFAB_PASS variant=inherited-typed-overrides composition=hierarchy-sockets-rules-parameters-scripts collections=author placement=deterministic-group budget=seven-metrics-platform-pass-warn-fail forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R059_SHELL_PRODUCTIVITY_PASS filters=layers-volumes placement=brush-surface-atomic split=two-viewports-cross-area help=integrated-forgescript-dependencies-citations profiles=seven-panels-permissions optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R060_RECOVERY_PASS policy=uuid-schema-provenance autosave=versioned-bounded session=unclean-diagnostic snapshot=hash-journal-quarantine compare=required selective=five-sections transaction=dry-run-apply-undo-redo optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R061_AUDIO_PASS selection=sequential-random-shuffle-weighted-seeded-no-repeat modulation=ranges-distributions-curves-tempo-statistics mix=lufs-bus-ducking-distance-cap-meter ambient=2d-area-listener-personalized-player playback=audio-stream-player optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R062_AUDIO_PASS range=inner-outer-curve-air-debug-spheres-engine script=typed-handle-start-stop-pause-resume-fade-authority-events-api-v1-player ambience=time-weather-location-danger-indoor-volume-crossfade-players music=day-night-states-stems-cooldown-beat-sync mix=hierarchy-snapshot-platform-dynamic-accessibility-live-engine-monitor optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R063_AUDIO_CAPTION_PASS metadata=dialogue-effect-ambient-locales-fallback-importance-direction-duration hud=visible-directional-accessible engine=audio-caption-overlay forgescript=typed-handle optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R064_WAYPOINT_PATROL_PASS appearance=icon-color-layer-visible-constant-screen-size map=text-icon-floor-category-precision-policy-tracking-hud reveal=player-party-world-event-animation-persistence patrol=ordered-wait-facing-speed-action-branch-weight-engine-actor traversal=loop-pingpong-once-random-weighted-scripted forgescript=typed-handles authority=server optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R065_WAYPOINT_LOCATOR_PASS arrival=direction-target-animation-wait-interact-callback-authority-engine locator=audio-vfx-spawn-objective-gameplay-purpose-tags-engine-nodes collection=query-tags-purpose-stable-nearest-random-deterministic route=segments-direction-costs-actions-broken-links-nav-projection-timing-engine-overlay forgescript=typed-handles authority=server optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R066_GAMEPLAY_SIMULATION_PASS flight=character-body-volumetric-banking-lift-landing projectile=ballistic-homing-hitscan-collision-prediction-vfx-authority physics=rigid-static-area-joint-character-bounds-network targeting=8-modes-engine-preview-validity-input world=clock-calendar-local-scales-pause-domains-schedules-save authority=server forgescript=typed-handles optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R067_GAMEPLAY_SESSION_PASS save=player-game-server-authority-conflict-revision-cloud-privacy-engine checkpoint=waypoint-cost-party-corpse-safe-spawn-hooks-area3d-authority shell=kit-data-driven-skinnable-scalable-input-agnostic-map-journal-hud-canvas control=owner-cursor-camera-actions-stack-restoration-engine replay=tick-input-event-seed-hash-headless-divergence-minimal-redacted-package forgescript=typed-handles authority=server optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R068_DIALOGUE_AUTHORING_PASS tokens=typed-autocomplete-preview-escaped participants=role-query-tag-entity-node-speaker-fallback evaluation=first-all-weighted-random-deterministic-manual wizard=visual-typed-source notes=intent-emotion-source-status-todo-author forgescript=typed-handles engine=nodes persistence=roundtrip optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R069_DIALOGUE_EXPERIENCE_PASS lifecycle=completed-aborted-reason-rollback-resume-actions search=multi-dialogue-safe-regex-speaker-condition-action-preview-only review=condition-action-voice-localization-error-link-filter memory=dialogue-participant-player-party-world-explicit-duration-typed-engine skill=difficulty-opponent-advantage-result-visibility-explanation-deterministic-engine forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R070_DIALOGUE_PRODUCTION_PASS subgraph=versioned-input-output-variable-contract-isolated-test-engine consequences=quest-faction-item-world-impact-engine coverage=paths-required-state-impossible-test-session-ratio-engine collaboration=canonical-node-json-stable-id-revision-lock-comment-semantic-diff-conflict-resolution-engine ai=brief-citations-style-preview-lint-authorization-plan-transaction-semantic-diff-explicit-apply-engine forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic'
    }

    $logPath = Join-Path $artifactRoot "$Name.log"
    if (Test-Path -LiteralPath $logPath) {
        Clear-Content -LiteralPath $logPath
    }

    $effectiveArguments = @($Arguments)
    if ($effectiveArguments -contains '--headless' -and
        $effectiveArguments -notcontains '--rendering-method') {
        $effectiveArguments = @('--rendering-method', 'gl_compatibility') + $effectiveArguments
    }

    $process = Start-Process `
        -FilePath $enginePath `
        -ArgumentList $effectiveArguments `
        -WindowStyle Hidden `
        -PassThru
    # The Toolset and Studio exercise their complete authoring inventories before their final markers.
    # Keep those integration gates bounded, while allowing slower Windows runners to finish them.
    $timeoutSeconds = switch ($Name) {
        'toolset' { 180 }
        'studio' { 240 }
        default { 75 }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    $passed = $false

    do {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $logPath) {
            $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
            $passed = @($Markers | Where-Object { $log -notmatch [regex]::Escape($_) }).Count -eq 0
            if ($passed -or $log -match 'OPENRPG_FORGE_.*_(FAILED|INVALID)') {
                break
            }
        }

        if ($process.HasExited -and -not $passed) {
            Start-Sleep -Milliseconds 500
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($StopAfterMarkers -or -not $process.HasExited) {
        $matchingProcesses = Get-CimInstance Win32_Process | Where-Object {
            $_.Name -like 'Godot_v4.7.1-stable_mono_win64*' -and
            $_.CommandLine -like "*$ProjectPath*"
        }
        foreach ($matchingProcess in $matchingProcesses) {
            Stop-Process -Id $matchingProcess.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }

    $log = if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Raw
    }
    else {
        ''
    }

    if (-not $passed) {
        $missingMarkers = @($Markers | Where-Object { $log -notmatch [regex]::Escape($_) })
        throw "$Name did not emit all expected markers: $($missingMarkers -join '; '). See $logPath."
    }

    if ($log -match '(?m)^ERROR:|OPENRPG_FORGE_.*_(FAILED|INVALID)') {
        throw "$Name emitted an engine or startup error. See $logPath."
    }

    Write-Output "$Name passed."
}

Push-Location $repositoryRoot
try {
    Invoke-CheckedDotnet @('restore', $solutionPath, '--locked-mode')
    Test-NuGetVulnerabilities
    Invoke-CheckedDotnet @('format', $solutionPath, '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
    Invoke-CheckedDotnet @('build', $solutionPath, '--configuration', 'Release', '--no-restore')
    # Keep project-level test hosts sequential: the full solution otherwise exceeds the workstation memory budget
    # when VSTest starts every assembly concurrently, which can kill healthy hosts before protocol negotiation.
    Invoke-CheckedDotnet @('test', $solutionPath, '--configuration', 'Release', '--no-build',
        '--maxcpucount:1', '--logger', 'console;verbosity=minimal')
    # Godot launches managed editor/runtime assemblies from its Debug cache even after a Release gate build.
    # Refresh those caches explicitly so engine smoke tests never execute stale contracts.
    Invoke-CheckedDotnet @('build', (Join-Path $repositoryRoot 'apps\Forge.Toolset\Forge.Toolset.csproj'),
        '--configuration', 'Debug', '--no-restore')
    Invoke-CheckedDotnet @('build', (Join-Path $repositoryRoot 'apps\Forge.Runtime\Forge.Runtime.csproj'),
        '--configuration', 'Debug', '--no-restore')
    Invoke-CheckedDotnet @('build', (Join-Path $repositoryRoot 'apps\Forge.Studio\Forge.Studio.csproj'),
        '--configuration', 'Debug', '--no-restore')
    $documentationCliProject = Join-Path $repositoryRoot 'tools\Forge.Cli\Forge.Cli.csproj'
    Invoke-CheckedDotnet @('run', '--project', $documentationCliProject, '--configuration', 'Release',
        '--no-build', '--', 'docs', 'check')
    Invoke-CheckedDotnet @('run', '--project', $documentationCliProject, '--configuration', 'Release',
        '--no-build', '--', 'quality', 'acceptance', $repositoryRoot,
        (Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes'))
    & (Join-Path $repositoryRoot 'scripts\Update-AuroraParityTracker.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Aurora parity tracker freshness check failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $repositoryRoot 'scripts\Test-AuroraEvidencePaths.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "Aurora evidence path check failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $repositoryRoot 'scripts\Update-CompleteFunctionBacklog.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Complete function backlog freshness check failed with exit code $LASTEXITCODE."
    }
    & (Join-Path $repositoryRoot 'scripts\Update-StudioAuroraAccessibilityPlan.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Studio Aurora accessibility matrix freshness check failed with exit code $LASTEXITCODE."
    }
    Invoke-CheckedPython @('-m', 'unittest', 'discover', '-s', 'tests', '-p', 'test_*.py')
    Invoke-CheckedPython @('-m', 'unittest', 'discover', '-s', 'tests\asset_pipeline', '-p', 'test_*.py')
    $architectureGraphProject = Join-Path $repositoryRoot 'tools\Forge.ArchitectureGraph\Forge.ArchitectureGraph.csproj'
    Invoke-CheckedDotnet @('run', '--project', $architectureGraphProject, '--configuration', 'Release',
        '--no-build', '--', 'check', '--full')
    Test-ForgeBranding

    $studioPath = Join-Path $repositoryRoot 'apps\Forge.Studio'
    $studioLog = Join-Path $artifactRoot 'studio.log'
    $studioWorkspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\forgotten-forest'
    Invoke-ForgeEngineMarkerTest `
        -Name 'studio' `
        -ProjectPath $studioPath `
        -Arguments @(
            '--headless', '--path', $studioPath, '--log-file', $studioLog, '--',
            '--studio-smoke-test', '--workspace', $studioWorkspace, '--page', 'zones') `
        -Markers @(
            'OPENRPG_FORGE_STUDIO_PROJECT_OPEN title=Forgotten Forest areas=6',
            'OPENRPG_FORGE_STUDIO_EDIT_PASS',
            'OPENRPG_FORGE_STUDIO_UX0A_PASS',
            'OPENRPG_FORGE_STUDIO_UX0B_PASS',
            'OPENRPG_FORGE_STUDIO_UX0C_PASS',
            'OPENRPG_FORGE_STUDIO_UX0D_PASS',
            'OPENRPG_FORGE_STUDIO_UX0E_PASS',
            'OPENRPG_FORGE_STUDIO_UX0F_PASS',
            'OPENRPG_FORGE_STUDIO_UX0G_PASS',
            'OPENRPG_FORGE_STUDIO_UX0H_PASS',
            'OPENRPG_FORGE_STUDIO_UX0I_PASS',
            'OPENRPG_FORGE_STUDIO_UX0J_PASS',
            'OPENRPG_FORGE_STUDIO_UX1A_PASS',
            'OPENRPG_FORGE_STUDIO_UX1B_PASS',
            'OPENRPG_FORGE_STUDIO_UX1C_PASS',
            'OPENRPG_FORGE_STUDIO_UX1D_PASS',
            'OPENRPG_FORGE_STUDIO_UX1E_PASS',
            'OPENRPG_FORGE_STUDIO_UX1F_PASS',
            'OPENRPG_FORGE_STUDIO_UX1G_PASS',
            'OPENRPG_FORGE_STUDIO_UX1H_PASS',
            'OPENRPG_FORGE_STUDIO_UX1I_PASS',
            'OPENRPG_FORGE_STUDIO_UX1J_PASS',
            'OPENRPG_FORGE_STUDIO_UX1K_PASS',
            'OPENRPG_FORGE_STUDIO_UX1L_PASS',
            'OPENRPG_FORGE_STUDIO_UX1M_PASS',
            'OPENRPG_FORGE_STUDIO_UX1N_PASS',
            'OPENRPG_FORGE_STUDIO_UX1O_PASS',
            'OPENRPG_FORGE_STUDIO_UX1P_PASS',
            'OPENRPG_FORGE_STUDIO_UX1Q_PASS',
            'OPENRPG_FORGE_STUDIO_UX1R_PASS',
            'OPENRPG_FORGE_STUDIO_UX1S_PASS',
            'OPENRPG_FORGE_STUDIO_UX1T_PASS',
            'OPENRPG_FORGE_STUDIO_UX1U_PASS',
            'OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1 module=forgotten-forest areas=6') `
        -StopAfterMarkers $false

    $toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
    $toolsetLog = Join-Path $artifactRoot 'toolset.log'
    $prototypeDataPath = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes\project.json'
    $prototypeHashBefore = (Get-FileHash -LiteralPath $prototypeDataPath -Algorithm SHA256).Hash
    Invoke-ForgeEngineMarkerTest `
        -Name 'toolset' `
        -ProjectPath $toolsetPath `
        -Arguments @('--headless', '--editor', '--path', $toolsetPath, '--log-file', $toolsetLog, '--', '--workspace-smoke-test') `
        -Markers @('OPENRPG_FORGE_PLUGIN_LOADED', 'OPENRPG_FORGE_WINDOW_TITLE_PASS title=OpenRPG_Forge', 'OPENRPG_FORGE_TOOLSET_PROJECT_VALID entities=16', 'OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces=9', 'OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog=10 derived=1 references=1 undo=restored', 'OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored', 'OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas=2 documents=2 transition=valid active=switched history=restored', 'OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone uuids=remapped preview=available history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global', 'OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed', 'OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS standard=readonly project=editable generated=provenance favorites=filtered temporary=isolated format=1.9.0 history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS orbit=mouse pan=mouse zoom=adaptive focus=selection views=orthographic pilot=wasd bookmarks=persisted history=dedicated project=unchanged', 'OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS graph=versioned scripts=2 cache=source-api-compiler dependencies=automatic incremental=reused persistence=restored', 'OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_PASS build=validation-scripts-assets-cache-incremental-report-reproducible ui=canonical-export diagnostics=uuid-schema-provenance-category-severity-text-correlation-clickable-quickfix logs=runtime-sanitized bundle=atomic-deterministic-reused cli=structured mcp=structured', 'OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS profiles=5 shipping=blocked licenses=diagnostic registry=versioned notices=generated channel=preview platforms=3 matrix=targets5-platforms3 windows=portable-installer unix=verified-install-uninstall promotion=sequential-compatibility-rollback-publisher-adapter launcher=profile-roundtrip-local-host history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_R132_CONTENT_DELIVERY_PASS vfx=provider-particles-decals-lights-audio-lod-budgets signatures=trusted-rotation-overlap-revocation-policy delta=content-addressed-range-resume-retry-dedup-hash-rollback', 'OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS filters=combined-isolation-ghost placement=deterministic-atomic tabs=multi-area comparison=readonly help=sourced profiles=seven persistence=roundtrip history=restored', 'OPENRPG_FORGE_TOOLSET_G11E_CONTENT_PASS tables=typed batch=dry-run-applied collisions=preserve-both blueprints=transitive-governed merge=three-way-conflicts history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_PASS ui=scalable font=truetype-fallback tabs=multi-area comparison=readonly favorites=project-personal recents=ordered art=profiled-warnings history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11G_RENDER_PASS pbr=metallic-roughness materials=instances tone=aces hdr=true lighting=per-pixel forward=plus sky=procedural lights=48 shadows=12 history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_PASS wind=global-local weather=state-machine particles=rain water=plane-volume reverb=zone authority=server save=portable replication=ready history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS provider=heightmap vertices=81 triangles=126 holes=1 biomes=2 features=3 volumes=3', 'OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_PASS terrain=flat-heightmap-voxel-mesh-sculpt-paint features=spline-tessellation-intersections roads-rivers-bridges-walls wind=global-local-vegetation-shader-audio-gameplay weather=server-state-precipitation-audio-script-surface-replication sky=physical-procedural-panorama-temporal-transition water=plane-river-volume-spline-shader-depth-flow-buoyancy-navigation-audio-quality volumetric=flight-swim-space-voxel-astar-cost-steering-crowd-dynamic-avoidance-debug', 'OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_PASS heartbeats=global-area budgets=bounded suspension=inactive rest=requested-started-interrupted-completed cooldown=active volumes=conditional abort=camera-control-invulnerability-locks-narrative history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_PASS startup=sequence-fallback timeline=tracks-branch-signal camera=anchor-fov-safe-area transform=interpolated hierarchy=validated authority=server skip=accessible abort=recovered mcp=governed history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_PASS swim=buoyancy-breath-currents dive=runtime climb=links-stamina-ik-safety radial=nested-gamepad-keyboard-list-accessible time=pause-slow-planning-turns multiplayer=vote-permissions combat=providers-shared-events-authority history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11M_RULES_PASS progression=curve-source-group-difficulty difficulty=damage-ai-economy-rest-save-accessibility mutators=composable perception=sense-ruleset-condition pvp=duel-crime-aggro-environment authority=server mcp=governed history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS agents=4 geometry=valid reachability=spawn-object doors=states offmesh=linked flight=profiled incremental=partial overlay=visible history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G719_PATHFINDING_PASS algorithm=deterministic-a-star', 'OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_PASS uuid=stable severity=typed path=document property=visible source=validator quickfix=command preview=dry-run navigation=targeted history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_R003_QA_PASS diagnostics=profile-category-severity-quickfix', 'OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS catalog=605 sources=3 facets=search-source-category uuid=stable hash=addressed license=verified availability=explicit preview=metadata import=governed', 'OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_PASS messages=', 'OPENRPG_FORGE_TOOLSET_G718_SCALE_PASS modes=uniform-nonuniform', 'OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format=1.9.0 areas=1 documents=1 entities=2 exterior=96x80 terrain=heightmap', 'OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_PASS schema=1 properties=3', 'OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3', 'OPENRPG_FORGE_TOOLSET_T43_QUEST_PASS states=3 transitions=2', 'OPENRPG_FORGE_TOOLSET_T23_SMOKE_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_PASS', 'OPENRPG_FORGE_TOOLSET_G13K_ITEM_FOUNDATION_PASS', 'OPENRPG_FORGE_TOOLSET_G13L_ITEM_EQUIPMENT_PASS', 'OPENRPG_FORGE_TOOLSET_G13M_ITEM_LOOT_CRAFT_PASS', 'OPENRPG_FORGE_TOOLSET_PROJECT_SAVED', 'OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS spawn=20000000-0000-4000-8000-000000000010 snapshot=deleted logs=captured') `
        -StopAfterMarkers $true
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_CREATOR_PRODUCT_JOURNEY_PASS project=new-module' -Quiet)) {
        throw 'toolset did not emit the complete Creator product journey marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_PASS collections=named-task-project-personal-ordered-bounded metrics=automatic-glb-bounds-png-texel-shader style=scale-density-shader-era-realism-palette-validation persistence=roundtrip' -Quiet)) {
        throw 'toolset did not emit the R138 palette production acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R139_REVERB_PRODUCTION_PASS zones=spatial-blended impulse=spdx-provenance-sha256 convolution=provider-capability-fallback sends=per-source-ordered-bounded runtime=applied persistence=roundtrip' -Quiet)) {
        throw 'toolset did not emit the R139 reverb production acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R140_FLIGHT_ROUTE_PASS routes=linear-catmullrom-bezier speed=node-interpolated bank=animated orientation=forward obstacles=avoid-stop-ignore lifecycle=open-complete-closed-loop runtime=stepped persistence=roundtrip' -Quiet)) {
        throw 'toolset did not emit the R140 flight route acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R142_WEATHER_RUNTIME_PASS authority=server state=deterministic-persisted replication=integrity-revision-network forgescript=typed-weather-changed gameplay=effect-payload runtime=applied' -Quiet)) {
        throw 'toolset did not emit the R142 weather runtime acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R143_COMBAT_SIMULATION_PASS profile=uuid-schema-provenance-seeds-budgets-archetypes phases=thresholds comparison=win-rate-duration-resource-pathing-phase-triggers deterministic=fingerprint ui=visible-configure-run-empty-error command=plan-dry-run-apply permission=bounded history=undo-redo persistence=roundtrip mcp=plan-transaction-permission-read-only' -Quiet)) {
        throw 'toolset did not emit the R143 combat simulation acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R030_BLUEPRINT_PALETTES_PASS' -Quiet)) {
        throw 'toolset did not emit the R030 blueprint and palettes acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_PASS' -Quiet)) {
        throw 'toolset did not emit the R031 palette integration and governance acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_PASS' -Quiet)) {
        throw 'toolset did not emit the R032 shell foundation acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_PASS' -Quiet)) {
        throw 'toolset did not emit the R033 direct manipulation acceptance marker.'
    }
    if (-not (Select-String -LiteralPath $toolsetLog -SimpleMatch 'OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_PASS' -Quiet)) {
        throw 'toolset did not emit the R034 content operations acceptance marker.'
    }
    $prototypeHashAfter = (Get-FileHash -LiteralPath $prototypeDataPath -Algorithm SHA256).Hash
    if ($prototypeHashBefore -ne $prototypeHashAfter) {
        throw 'The toolset smoke test did not restore the prototype project content.'
    }

    Write-Output 'toolset sample restored.'

    $toolsetForwardLog = Join-Path $artifactRoot 'toolset-forward-plus.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'toolset-forward-plus' `
        -ProjectPath $toolsetPath `
        -Arguments @('--rendering-method', 'forward_plus', '--editor', '--path', $toolsetPath, '--log-file', $toolsetForwardLog) `
        -Markers @('OPENRPG_FORGE_PLUGIN_LOADED', 'OPENRPG_FORGE_TOOLSET_RENDER_BACKEND method=forward_plus') `
        -StopAfterMarkers $true

    $forestMapLog = Join-Path $artifactRoot 'forest-map.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'forest-map' `
        -ProjectPath $toolsetPath `
        -Arguments @('--headless', '--path', $toolsetPath, '--log-file', $forestMapLog, 'res://addons/forge_forest_map/scenes/forge_forest_map_demo.tscn', '--', '--forest-map-smoke-test') `
        -Markers @('OPENRPG_FORGE_FOREST_MAP_SMOKE_PASS') `
        -StopAfterMarkers $false

    $moduleArtifact = Join-Path $artifactRoot 'crypt-of-ashes.orpmod'
    $moduleWorkspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes'
    $cliProject = Join-Path $repositoryRoot 'tools\Forge.Cli\Forge.Cli.csproj'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $moduleWorkspace, $moduleArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $moduleArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'analyze', $moduleWorkspace)
    Invoke-ForgeCliMarkerTest `
        -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'validate', $moduleWorkspace) `
        -Marker 'OPENRPG_FORGE_R027_CANONICAL_VALIDATION_CLI_PASS categories=5 diagnostics=0'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'normalize', $moduleWorkspace)
    Write-Output 'R023_CLI_NORMALIZE_PASS mode=preview structured=json source_write=false'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'migrate', $moduleWorkspace)
    Write-Output 'R023_CLI_MIGRATE_PASS mode=preview backup=governed idempotent=true downgrade=explicit'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'references', $moduleWorkspace)
    Write-Output 'R023_CLI_REFERENCES_PASS graph=stable directions=forward-reverse structured=json'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'diff', $moduleWorkspace)
    Write-Output 'R023_CLI_DIFF_PASS domains=area-graph-entity-package-script outputs=text-json'
    $contentPackArtifact = Join-Path $artifactRoot 'crypt-of-ashes-content-pack.orpmod'
    Invoke-ForgeCliMarkerTest `
        -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'pack', $moduleWorkspace, $contentPackArtifact) `
        -Marker 'OPENRPG_FORGE_CONTENT_PACKAGE_CLI_PASS operation=pack packages=0 validation=domain-sources-scripts lock=generated compression=orpmod'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'diagnose', $moduleWorkspace)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'readiness', 'validate', $moduleWorkspace)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'readiness', 'health', $moduleWorkspace, '--tests-passed', 'true')
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'readiness', 'recovery', $moduleWorkspace)
    Invoke-ForgeCliMarkerTest `
        -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'quality', 'run', $moduleWorkspace) `
        -Marker 'OPENRPG_FORGE_QUALITY_LAB_PASS locales=2 scripts=2 dialogues=1 quests=1 encounter_runs=9 pseudo=snapshot'
    Invoke-ForgeCliMarkerTest `
        -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'performance', 'run', $moduleWorkspace) `
        -Marker 'OPENRPG_FORGE_PERFORMANCE_LAB_PASS network_scenarios=9 visual_cases=8 benchmarks=9 budgets=9 spans=5 artifacts=written'
    Invoke-ForgeCliMarkerTest `
        -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'governance', 'run', $moduleWorkspace) `
        -Marker 'OPENRPG_FORGE_GOVERNANCE_LAB_PASS editor=7 runtime=7 fuzz_cases=112 boundaries=7 dependencies=0 artifact=written'
    $releaseSmoke = Join-Path $artifactRoot 'distribution-release-smoke'
    if (Test-Path -LiteralPath $releaseSmoke) {
        Remove-Item -LiteralPath $releaseSmoke -Recurse -Force
    }
    [System.IO.Directory]::CreateDirectory($releaseSmoke) | Out-Null
    $releasePrivateKey = Join-Path $releaseSmoke 'signing-private.pem'
    $releasePublicKey = Join-Path $releaseSmoke 'signing-public.pem'
    $releaseSignature = Join-Path $releaseSmoke 'crypt-of-ashes.signature.json'
    $releaseDelta = Join-Path $releaseSmoke 'crypt-of-ashes.orpdelta'
    $releaseRebuilt = Join-Path $releaseSmoke 'crypt-of-ashes-rebuilt.orpmod'
    $releaseResumeCache = Join-Path $releaseSmoke 'resume-cache'
    $releaseBundle = Join-Path $releaseSmoke 'crypt-of-ashes-development-windows.zip'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'keygen', $releasePrivateKey, $releasePublicKey)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'sign', $moduleArtifact, $releasePrivateKey, $releaseSignature)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'verify-signature', $moduleWorkspace, $moduleArtifact, $releaseSignature)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'delta', $moduleWorkspace, $moduleArtifact, $moduleArtifact, $releaseDelta)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'apply-delta', $moduleArtifact, $releaseDelta, $releaseRebuilt,
        '--resume-cache', $releaseResumeCache)
    if ((Get-FileHash -LiteralPath $moduleArtifact -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $releaseRebuilt -Algorithm SHA256).Hash) {
        throw 'Delta reconstruction did not reproduce the exact target module package.'
    }
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'bundle', $moduleWorkspace, $moduleArtifact, $releaseBundle,
        '--target', 'editor', '--platform', 'windows-x64',
        '--signature', $releaseSignature, '--delta', $releaseDelta)
    $releaseExtract = Join-Path $releaseSmoke 'bundle'
    $releaseInstall = Join-Path $releaseSmoke 'installed-modules'
    Expand-Archive -LiteralPath $releaseBundle -DestinationPath $releaseExtract -Force
    foreach ($required in @('release-manifest.json', 'install.ps1', 'uninstall.ps1', 'packages\crypt-of-ashes.orpmod')) {
        if (-not (Test-Path -LiteralPath (Join-Path $releaseExtract $required))) {
            throw "Release bundle is missing '$required'."
        }
    }
    & (Join-Path $releaseExtract 'install.ps1') -Destination $releaseInstall
    $installedModule = Join-Path $releaseInstall 'crypt-of-ashes.orpmod'
    if (-not (Test-Path -LiteralPath $installedModule -PathType Leaf)) {
        throw 'Windows release installer did not install the module package.'
    }
    & (Join-Path $releaseExtract 'uninstall.ps1') -Destination $releaseInstall
    if (Test-Path -LiteralPath $installedModule) {
        throw 'Windows release uninstaller did not remove the installed module package.'
    }
    Write-Output 'OPENRPG_FORGE_G11D_RELEASE_SMOKE_PASS signature=ecdsa-p256 delta=resume-dedup-rollback windows=install-uninstall channel=development'
    $contentSmoke = Join-Path $artifactRoot 'content-workflow-smoke'
    if (Test-Path -LiteralPath $contentSmoke) {
        Remove-Item -LiteralPath $contentSmoke -Recurse -Force
    }
    [System.IO.Directory]::CreateDirectory($contentSmoke) | Out-Null
    $contentBatchDestination = Join-Path $contentSmoke 'batch'
    $blueprintPack = Join-Path $contentSmoke 'crypt-blueprints.orpblueprints'
    $contentSource = Join-Path $repositoryRoot 'docs\aurora-parity\tasks'
    $contentGlob = 'G11-D-SIGNED-DELTA-DISTRIBUTION.md'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'content', 'batch', 'export', $contentSource, $contentBatchDestination,
        '--glob', $contentGlob, '--extensions', '.md', '--collision', 'preserve-both',
        '--provenance', 'g11e-gate', '--license', 'CC0-1.0', '--dry-run', 'true')
    if (Test-Path -LiteralPath (Join-Path $contentBatchDestination $contentGlob)) {
        throw 'Content batch dry-run wrote a destination file.'
    }
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'content', 'batch', 'export', $contentSource, $contentBatchDestination,
        '--glob', $contentGlob, '--extensions', '.md', '--collision', 'preserve-both',
        '--provenance', 'g11e-gate', '--license', 'CC0-1.0', '--dry-run', 'false')
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'content', 'batch', 'export', $contentSource, $contentBatchDestination,
        '--glob', $contentGlob, '--extensions', '.md', '--collision', 'preserve-both',
        '--provenance', 'g11e-gate', '--license', 'CC0-1.0', '--dry-run', 'false')
    $preservedFiles = @(Get-ChildItem -LiteralPath $contentBatchDestination -File)
    if ($preservedFiles.Count -ne 2 -or -not ($preservedFiles.Name -match '~[0-9a-f]{8}\.md$')) {
        throw 'Content batch preserve-both collision policy did not retain both files.'
    }
    $projectDocument = Get-Content -LiteralPath $prototypeDataPath -Raw | ConvertFrom-Json
    $blueprintId = $projectDocument.project.blueprints[0].id
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'content', 'export-blueprints', $moduleWorkspace, $blueprintPack,
        '--ids', $blueprintId, '--previews', 'true')
    $blueprintExtract = Join-Path $contentSmoke 'blueprints'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($blueprintPack, $blueprintExtract)
    foreach ($required in @('manifest.json', 'licenses\index.json', 'migrations\blueprint-schema-v1.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $blueprintExtract $required))) {
            throw "Blueprint export is missing '$required'."
        }
    }
    Write-Output 'OPENRPG_FORGE_G11E_CONTENT_SMOKE_PASS batch=dry-run-apply collisions=preserve-both blueprints=governed'
    & (Join-Path $repositoryRoot 'scripts\Invoke-AssistedBlenderCreationSmoke.ps1') `
        -RepositoryRoot $repositoryRoot `
        -BlenderPath $blenderPath
    & (Join-Path $repositoryRoot 'scripts\Invoke-AssistedModel3DGenerationSmoke.ps1') `
        -RepositoryRoot $repositoryRoot `
        -BlenderPath $blenderPath
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'assets', 'list', $repositoryRoot, '--query', 'apple', '--source', 'ambientcg')

    $assetVault = if ([string]::IsNullOrWhiteSpace($env:OPENRPG_ASSET_VAULT)) {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '..\..\OpenRPG_AssetVault'))
    } else {
        [System.IO.Path]::GetFullPath($env:OPENRPG_ASSET_VAULT)
    }
    $assetImportSmoke = Join-Path $artifactRoot 'asset-import-smoke'
    $modelSource = Join-Path $assetVault 'normalized\kenney\castle-kit\Models\GLB format\bridge-draw.glb'
    $textureSource = Join-Path $assetVault 'normalized\ambientcg\3DApple001\lq-1k-jpg\3DApple001.png'
    $roundTripSource = Join-Path $assetVault 'normalized\ambientcg\3DApple001\lq-1k-jpg\3DApple001_LQ-1K-JPG.obj'
    if ((Test-Path -LiteralPath $modelSource) -and
        (Test-Path -LiteralPath $textureSource) -and
        (Test-Path -LiteralPath $roundTripSource)) {
        if (Test-Path -LiteralPath $assetImportSmoke) {
            Remove-Item -LiteralPath $assetImportSmoke -Recurse -Force
        }
        [System.IO.Directory]::CreateDirectory($assetImportSmoke) | Out-Null
        $modelSourceHash = (Get-FileHash -LiteralPath $modelSource -Algorithm SHA256).Hash
        $textureSourceHash = (Get-FileHash -LiteralPath $textureSource -Algorithm SHA256).Hash
        $roundTripSourceHash = (Get-FileHash -LiteralPath $roundTripSource -Algorithm SHA256).Hash
        Invoke-CheckedDotnet @(
            'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'assets', 'prepare', $repositoryRoot, $assetImportSmoke,
            '--id', 'kenney:castle-kit', '--kind', 'model', '--vfx', 'true', '--vault', $assetVault)
        Invoke-CheckedDotnet @(
            'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'assets', 'prepare', $repositoryRoot, $assetImportSmoke,
            '--id', 'ambientcg:3DApple001', '--kind', 'texture', '--vault', $assetVault)
        Invoke-CheckedDotnet @(
            'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'assets', 'prepare', $repositoryRoot, $assetImportSmoke,
            '--id', 'ambientcg:3DApple001', '--kind', 'model', '--vault', $assetVault)
        Invoke-CheckedDotnet @(
            'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'assets', 'roundtrip', $repositoryRoot, $assetImportSmoke,
            '--id', 'ambientcg:3DApple001', '--watch', 'true', '--vault', $assetVault)
        $roundTripRequest = Join-Path `
            $assetImportSmoke `
            'assets\roundtrip\96ce5ad8-a1e9-8255-99ce-11cf8d593506.worker-request.json'
        $roundTripResponse = Join-Path $assetImportSmoke 'assets\roundtrip\worker-response.json'
        $workerArguments = @(
            (Join-Path $repositoryRoot 'tools\asset_pipeline\blender_worker.py'),
            '--request', $roundTripRequest,
            '--response', $roundTripResponse,
            '--source-root', $assetVault,
            '--workspace-root', $assetImportSmoke)
        if ($blenderPath) {
            $workerArguments += @('--blender', $blenderPath)
        }
        else {
            $workerArguments += '--validate-only'
        }
        Invoke-CheckedPython $workerArguments
        $roundTripResult = Get-Content -LiteralPath $roundTripResponse -Raw | ConvertFrom-Json
        if (-not $roundTripResult.valid) {
            throw 'The Blender worker did not return a valid governed response.'
        }
        if ($blenderPath) {
            $roundTripOutput = Join-Path $assetImportSmoke 'assets\imported\ambientcg-3dapple001\model.glb'
            $roundTripPreview = Join-Path $assetImportSmoke 'assets\imported\ambientcg-3dapple001\model.preview.png'
            $roundTripQuality = Join-Path $assetImportSmoke 'assets\imported\ambientcg-3dapple001\model.quality.json'
            if (-not $roundTripResult.executed -or
                -not (Test-Path -LiteralPath $roundTripOutput -PathType Leaf) -or
                -not (Test-Path -LiteralPath $roundTripPreview -PathType Leaf) -or
                -not (Test-Path -LiteralPath $roundTripQuality -PathType Leaf)) {
                throw 'The installed Blender worker did not produce the governed GLB, thumbnail and quality report.'
            }
            $roundTripOutputHash = (Get-FileHash -LiteralPath $roundTripOutput -Algorithm SHA256).Hash
            $roundTripOutputBytes = [System.IO.File]::ReadAllBytes($roundTripOutput)
            $roundTripMagic = [System.Text.Encoding]::ASCII.GetString($roundTripOutputBytes, 0, 4)
            $roundTripDeclaredLength = [BitConverter]::ToUInt32($roundTripOutputBytes, 8)
            if ($roundTripMagic -ne 'glTF' -or
                $roundTripDeclaredLength -ne $roundTripOutputBytes.Length -or
                $roundTripOutputHash -ne $roundTripResult.outputSha256) {
                throw 'The Blender output is not a self-consistent GLB 2 artifact.'
            }
            $roundTripPreviewHash = (Get-FileHash -LiteralPath $roundTripPreview -Algorithm SHA256).Hash
            $roundTripPreviewBytes = [System.IO.File]::ReadAllBytes($roundTripPreview)
            $roundTripPngMagic = -join ($roundTripPreviewBytes[0..7] | ForEach-Object { '{0:X2}' -f $_ })
            $roundTripQualityResult = Get-Content -LiteralPath $roundTripQuality -Raw | ConvertFrom-Json
            if ($roundTripPngMagic -ne '89504E470D0A1A0A' -or
                $roundTripPreviewHash -ne $roundTripResult.preview.sha256 -or
                $roundTripResult.preview.width -ne 512 -or
                $roundTripResult.preview.height -ne 512 -or
                -not $roundTripQualityResult.passed -or
                -not $roundTripResult.quality.passed -or
                $roundTripQualityResult.metrics.triangleCount -le 0 -or
                $roundTripQualityResult.scores.overall -le 0) {
                throw 'The Blender thumbnail or quality report is invalid.'
            }
            Invoke-ForgeCliMarkerTest `
                -Arguments @('run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
                    'assets', 'finalize-roundtrip', $repositoryRoot, $assetImportSmoke,
                    '--id', 'ambientcg:3DApple001', '--vault', $assetVault) `
                -Marker 'OPENRPG_FORGE_ASSET_ROUNDTRIP_FINALIZE_PASS asset=ambientcg:3DApple001 triangles=500 variants=1 score=99'
            $manifestDocument = Get-Content `
                -LiteralPath (Join-Path $assetImportSmoke 'assets\asset-manifests.json') `
                -Raw | ConvertFrom-Json
            $appleManifest = @($manifestDocument.manifests | Where-Object {
                $_.assetId -eq '96ce5ad8-a1e9-8255-99ce-11cf8d593506'
            })
            if ($appleManifest.Count -ne 1 -or
                $appleManifest[0].modelMetadata.metrics.triangleCount -ne 500 -or
                $appleManifest[0].modelMetadata.metrics.qualityScore -ne 99 -or
                $appleManifest[0].modelMetadata.variants.Count -ne 1 -or
                $appleManifest[0].modelMetadata.compatibility.shaderFamily -ne 'pbr-metallic-roughness' -or
                $appleManifest[0].chunks[0].relativePath -ne 'assets/imported/ambientcg-3dapple001/model.glb') {
                throw 'The finalized asset manifest did not persist the governed 3D metadata.'
            }
            Write-Output (
                "OPENRPG_FORGE_G11C_BLENDER_SMOKE_PASS bytes=$($roundTripOutputBytes.Length) " +
                "sha256=$($roundTripResult.outputSha256)")
            Write-Output (
                "OPENRPG_FORGE_AV2_NORMALIZATION_SMOKE_PASS preview=$($roundTripPreviewBytes.Length) " +
                "triangles=$($roundTripQualityResult.metrics.triangleCount) " +
                "score=$($roundTripQualityResult.scores.overall)")
            Write-Output (
                "OPENRPG_FORGE_AV3_MANIFEST_METADATA_SMOKE_PASS styles=$($appleManifest[0].modelMetadata.styles.Count) " +
                "variants=$($appleManifest[0].modelMetadata.variants.Count) triangles=500 score=99")
        }
        if ($modelSourceHash -ne (Get-FileHash -LiteralPath $modelSource -Algorithm SHA256).Hash -or
            $textureSourceHash -ne (Get-FileHash -LiteralPath $textureSource -Algorithm SHA256).Hash -or
            $roundTripSourceHash -ne (Get-FileHash -LiteralPath $roundTripSource -Algorithm SHA256).Hash -or
            -not (Test-Path -LiteralPath (Join-Path $assetImportSmoke 'assets\imported\kenney-castle-kit\model.glb')) -or
            -not (Test-Path -LiteralPath (Join-Path $assetImportSmoke 'assets\imported\ambientcg-3dapple001\import-receipt.json')) -or
            -not (Test-Path -LiteralPath (Join-Path $assetImportSmoke 'assets\asset-manifests.json')) -or
            -not (Test-Path -LiteralPath (Join-Path $assetImportSmoke 'assets\external-edits.json')) -or
            -not (Test-Path -LiteralPath $roundTripRequest) -or
            -not (Test-Path -LiteralPath $roundTripResponse)) {
            throw 'Asset preparation did not preserve Vault sources or produce the governed outputs.'
        }
        $workerMode = if ($blenderPath) { "Blender executable: $blenderPath" } else { 'validation-only' }
        Write-Output "asset-import-smoke passed ($workerMode)."
    } else {
        Write-Output 'asset-import-smoke skipped: external Vault fixtures unavailable.'
    }

    $retargetTargetSource = Join-Path `
        $assetVault `
        'normalized\kaykit\character-pack-adventures-1.0\KayKit-Character-Pack-Adventures-1.0-672074b73ba276876a19e8816ecdc5241817ab47\addons\kaykit_character_pack_adventures\Characters\gltf\Knight.glb'
    $retargetAnimationSource = Join-Path `
        $assetVault `
        'normalized\kenney\animated-characters-protagonists\Animations\run.fbx'
    $retargetRequest = Join-Path `
        $repositoryRoot `
        'asset_manifests\retarget\kenney-run-to-kaykit-knight.json'
    if ((Test-Path -LiteralPath $retargetTargetSource -PathType Leaf) -and
        (Test-Path -LiteralPath $retargetAnimationSource -PathType Leaf) -and
        (Test-Path -LiteralPath $retargetRequest -PathType Leaf)) {
        $retargetTargetHash = (Get-FileHash -LiteralPath $retargetTargetSource -Algorithm SHA256).Hash
        $retargetAnimationHash = (Get-FileHash -LiteralPath $retargetAnimationSource -Algorithm SHA256).Hash
        $retargetResponse = Join-Path $artifactRoot 'retarget\worker-response.json'
        $retargetWorkerArguments = @(
            (Join-Path $repositoryRoot 'tools\asset_pipeline\blender_worker.py'),
            '--request', $retargetRequest,
            '--response', $retargetResponse,
            '--source-root', $assetVault,
            '--workspace-root', $repositoryRoot)
        if ($blenderPath) {
            $retargetWorkerArguments += @('--blender', $blenderPath)
        }
        else {
            $retargetWorkerArguments += '--validate-only'
        }
        Invoke-CheckedPython $retargetWorkerArguments
        $retargetResult = Get-Content -LiteralPath $retargetResponse -Raw | ConvertFrom-Json
        if (-not $retargetResult.valid) {
            throw 'The governed AV6-F retarget request is invalid.'
        }
        if ($blenderPath) {
            $retargetOutput = Join-Path $repositoryRoot 'artifacts\verification\retarget\kenney-run-to-kaykit-knight.glb'
            $retargetQuality = Join-Path $repositoryRoot 'artifacts\verification\retarget\kenney-run-to-kaykit-knight.quality.json'
            if (-not $retargetResult.executed -or
                -not (Test-Path -LiteralPath $retargetOutput -PathType Leaf) -or
                -not (Test-Path -LiteralPath $retargetQuality -PathType Leaf)) {
                throw 'The AV6-F Blender worker did not produce its governed retarget artifacts.'
            }
            $retargetAudit = @($retargetResult.quality.metrics.retargetAudits)
            $retargetFailedChecks = @($retargetResult.quality.checks | Where-Object { $_.status -eq 'fail' })
            if (-not $retargetResult.quality.passed -or
                $retargetFailedChecks.Count -ne 0 -or
                $retargetAudit.Count -ne 1 -or
                $retargetAudit[0].sourceBoneCount -ne 58 -or
                $retargetAudit[0].targetBoneCount -ne 41 -or
                $retargetAudit[0].mappedBoneCount -ne 19 -or
                $retargetAudit[0].sampledFrames.Count -ne 9 -or
                $retargetAudit[0].sampledVertexCount -le 0 -or
                $retargetAudit[0].invalidCoordinateCount -ne 0 -or
                $retargetAudit[0].maximumNormalizedTemporalDisplacement -lt 0.001 -or
                $retargetAudit[0].maximumBoundsExpansion -gt 3.0) {
                throw 'The AV6-F deformation audit did not prove a stable animated inter-rig transfer.'
            }
            $retargetRoundTripOutput = & $blenderPath `
                '--background' `
                '--factory-startup' `
                '--disable-autoexec' `
                '--python' (Join-Path $repositoryRoot 'tools\asset_pipeline\blender_retarget_validator.py') `
                '--' $retargetOutput 'run' '41' 2>&1
            $retargetRoundTripExit = $LASTEXITCODE
            $retargetRoundTripOutput | Write-Output
            if ($retargetRoundTripExit -ne 0 -or
                ($retargetRoundTripOutput -join "`n") -notmatch 'OPENRPG_FORGE_AV6F_RETARGET_GLB_PASS') {
                throw 'The exported AV6-F GLB failed its Blender round-trip animation validation.'
            }
            if ($retargetTargetHash -ne (Get-FileHash -LiteralPath $retargetTargetSource -Algorithm SHA256).Hash -or
                $retargetAnimationHash -ne (Get-FileHash -LiteralPath $retargetAnimationSource -Algorithm SHA256).Hash) {
                throw 'The AV6-F worker modified an immutable Vault source.'
            }
            Write-Output (
                "OPENRPG_FORGE_AV6F_RETARGET_PASS source=58 target=41 mapped=19 " +
                "frames=$($retargetAudit[0].sampledFrames.Count) vertices=$($retargetAudit[0].sampledVertexCount) " +
                "temporal=$($retargetAudit[0].maximumNormalizedTemporalDisplacement) " +
                "bounds=$($retargetAudit[0].maximumBoundsExpansion) roundtrip=3-poses")
        }
        else {
            Write-Output 'AV6-F retarget request validated; Blender execution skipped because Blender is unavailable.'
        }
    }
    else {
        Write-Output 'AV6-F retarget smoke skipped: external CC0 rig fixtures unavailable.'
    }

    $runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
    $plainRuntimeLog = Join-Path $artifactRoot 'runtime-plain-launch.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'runtime-plain-launch' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $plainRuntimeLog, '--', '--smoke-test') `
        -Markers @('OPENRPG_FORGE_RUNTIME_WORKSPACE_LOADED format=1.9.0 slug=crypt-of-ashes', 'OPENRPG_FORGE_CREATURE_AMBIENT_READY blueprint=ash-warden-boss roles=2 lore=2 set=idle.ritual animation=warm-hands autolevel_changes=0', 'OPENRPG_FORGE_CREATURE_EVENTS_READY blueprint=ash-warden-boss bindings=6 heartbeat_ms=1500 budget_ms=8 custom=1', 'OPENRPG_FORGE_FACTIONS_READY definitions=3 inheritance=1 witness=1 memberships=3 dynamic=1 authority=server inherited_tags=4', 'OPENRPG_FORGE_RUNTIME_RENDER_PROFILE_PASS pbr=metallicroughness tone=agx hdr=true perpixel=true sky=procedural lights=8/32 shadows=2/8', 'OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method=gl_compatibility', 'OPENRPG_FORGE_RUNTIME_G11H_ENVIRONMENT_PASS', 'OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS', 'OPENRPG_FORGE_RUNTIME_G11J_POLICY_PASS', 'OPENRPG_FORGE_RUNTIME_G11K_CINEMATIC_PASS startup=sequence', 'OPENRPG_FORGE_RUNTIME_G11L_GAMEPLAY_PASS swim=true climb=true radial=6 accessible=true pause=true planning=true combat=realtime-pause authority=true', 'OPENRPG_FORGE_RUNTIME_G11M_RULES_PASS progression=forge.generic difficulty=standard mutators=0 perception=2:20 pvp=disabled authority=true', 'OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities=16 collisions=10', 'OPENRPG_FORGE_RUNTIME_G13F_CREATURE_EVENTS_PASS combat=3 inventory=2 rest=1 heartbeat=1 custom=1 cadence_ms=1500 budget_ms=8', 'OPENRPG_FORGE_RUNTIME_G13G_ADVANCED_FACTIONS_PASS hierarchy_tags=4 memberships=2 witness=1 diplomacy=1 expiration=1 authority=server replication=revisioned', 'OPENRPG_FORGE_RUNTIME_G13H_DIALOGUE_PRESENTATION_PASS animations=1 voices=1 shots=1 bookmarks=1 interjections=1 subtitle_fallback=true authority=server accessibility=player-priority', 'OPENRPG_FORGE_RUNTIME_G13I_ADAPTIVE_DIALOGUE_PASS timed=1', 'OPENRPG_FORGE_RUNTIME_SMOKE_PASS quest=completed door=unlocked boss=defeated') `
        -StopAfterMarkers $false
    foreach ($marker in @(
        'OPENRPG_FORGE_CRYPT_ART_PASS models=74 floors=35 walls=24 props=15 source=kaykit-dungeon-remastered license=CC0-1.0 collisions=gameplay-layer',
        'OPENRPG_FORGE_RUNTIME_R039_CREATURE_DAMAGE_DEATH_PASS',
        'OPENRPG_FORGE_RUNTIME_G13J_QUEST_OPERATIONS_PASS',
        'OPENRPG_FORGE_RUNTIME_G13K_ITEM_FOUNDATION_PASS',
        'OPENRPG_FORGE_RUNTIME_G13L_ITEM_EQUIPMENT_PASS',
        'OPENRPG_FORGE_RUNTIME_G13M_ITEM_LOOT_CRAFT_PASS')) {
        if (-not (Select-String -LiteralPath $plainRuntimeLog -SimpleMatch $marker -Quiet)) {
            throw "runtime-plain-launch did not emit required marker: $marker"
        }
    }
    $crashArtifactRoot = Join-Path $artifactRoot 'g11q-crash-artifact'
    if (Test-Path -LiteralPath $crashArtifactRoot) {
        Remove-Item -LiteralPath $crashArtifactRoot -Recurse -Force
    }
    $moduleFingerprint = (Get-FileHash -LiteralPath $moduleArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'readiness', 'crash', $moduleWorkspace, $plainRuntimeLog, $crashArtifactRoot,
        '--version', '0.6.20', '--package-sha', $moduleFingerprint,
        '--reproduction-id', 'e0250000-0000-4000-8000-000000000001')
    if (@(Get-ChildItem -LiteralPath $crashArtifactRoot -Filter '*.zip' -File).Count -ne 1) {
        throw 'G11-Q crash artifact was not produced.'
    }
    $runtimeVisualLog = Join-Path $artifactRoot 'runtime-g11p-visual.log'
    $runtimeVisualProof = Join-Path $artifactRoot 'runtime-g11p-visual.png'
    Invoke-ForgeEngineMarkerTest `
        -Name 'runtime-g11p-visual' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'gl_compatibility', '--path', $runtimePath, '--log-file',
            $runtimeVisualLog, '--', '--visual-capture', $runtimeVisualProof) `
        -Markers @('OPENRPG_FORGE_RUNTIME_G11P_MODERN_PASS',
            'OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file=runtime-g11p-visual.png size=1600x900') `
        -StopAfterMarkers $false
    if (-not (Test-Path -LiteralPath $runtimeVisualProof) -or
        [System.IO.File]::ReadAllBytes($runtimeVisualProof).Length -lt 8) {
        throw 'G11-P visual proof PNG is missing or empty.'
    }
    $runtimeVisualSuiteLog = Join-Path $artifactRoot 'runtime-g11s-visual.log'
    $runtimeVisualSuite = Join-Path $artifactRoot 'runtime-g11s-visual'
    if (Test-Path -LiteralPath $runtimeVisualSuite) {
        Remove-Item -LiteralPath $runtimeVisualSuite -Recurse -Force
    }
    Invoke-ForgeEngineMarkerTest `
        -Name 'runtime-g11s-visual' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'gl_compatibility', '--path', $runtimePath, '--log-file',
            $runtimeVisualSuiteLog, '--', '--g11s-visual-suite', $runtimeVisualSuite) `
        -Markers @('OPENRPG_FORGE_RUNTIME_G11S_PERFORMANCE_PASS',
            'OPENRPG_FORGE_RUNTIME_G11S_VISUAL_PASS cases=8 resolutions=2 cameras=2 quality=2') `
        -StopAfterMarkers $false
    if (@(Get-ChildItem -LiteralPath $runtimeVisualSuite -Filter '*.reference.png' -File).Count -ne 8 -or
        @(Get-ChildItem -LiteralPath $runtimeVisualSuite -Filter '*.candidate.png' -File).Count -ne 8 -or
        @(Get-ChildItem -LiteralPath $runtimeVisualSuite -Filter '*.review.json' -File).Count -ne 8 -or
        -not (Test-Path -LiteralPath (Join-Path $runtimeVisualSuite 'visual-suite-report.json') -PathType Leaf)) {
        throw 'G11-S visual regression suite did not produce the complete review artifact set.'
    }
    $runtimeForwardLog = Join-Path $artifactRoot 'runtime-forward-plus.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'runtime-forward-plus' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'forward_plus', '--path', $runtimePath, '--log-file', $runtimeForwardLog) `
        -Markers @('OPENRPG_FORGE_RUNTIME_RENDER_PROFILE_PASS pbr=metallicroughness tone=agx hdr=true perpixel=true sky=procedural lights=8/32 shadows=2/8', 'OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method=forward_plus') `
        -StopAfterMarkers $true
    $starterWorkspace = Join-Path $runtimePath 'content\broken-isles-starter'
    $starterArtifact = Join-Path $artifactRoot 'broken-isles-starter.orpmod'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $starterWorkspace, $starterArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $starterArtifact)
    $starterLog = Join-Path $artifactRoot 'generic-starter.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'generic-starter' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $starterLog, '--', '--module-package', $starterArtifact, '--generic-smoke-test') `
        -Markers @('OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug=les-iles-brisees', 'OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS events=3 order=ProjectLoaded>AreaEntered>WorldStarted authority=SinglePlayer', 'OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities=2 collisions=1', 'OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug=les-iles-brisees entities=2 collisions=1 combat=false interaction=empty') `
        -StopAfterMarkers $false

    $g8Workspace = Join-Path $runtimePath 'content\la-veillee-des-eclats'
    $g8Artifact = Join-Path $artifactRoot 'la-veillee-des-eclats.orpmod'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $g8Workspace, $g8Artifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $g8Artifact)
    $g8RuntimeLog = Join-Path $artifactRoot 'g8-original-module.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'g8-original-module' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $g8RuntimeLog, '--', '--module-package', $g8Artifact, '--rpg-kit-smoke-test') `
        -Markers @('OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug=la-veillee-des-eclats', 'OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS events=3 order=ProjectLoaded>AreaEntered>WorldStarted authority=SinglePlayer', 'OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities=8 collisions=3', 'OPENRPG_FORGE_RUNTIME_G8_MODULE_PASS slug=la-veillee-des-eclats entities=8 collisions=3 combat=2 dialogues=1 quests=1') `
        -StopAfterMarkers $false

    $forestWorkspace = Join-Path $runtimePath 'content\forgotten-forest'
    $forestArtifact = Join-Path $artifactRoot 'forgotten-forest.orpmod'
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'export', $forestWorkspace, $forestArtifact)
    Invoke-CheckedDotnet @(
        'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
        'package', 'validate', $forestArtifact)
    $forestRuntimeLog = Join-Path $artifactRoot 'forgotten-forest.log'
    $forestTerrainBenchmark = Join-Path $artifactRoot 'forgotten-forest-terrain-benchmark.json'
    if (Test-Path -LiteralPath $forestTerrainBenchmark) {
        Remove-Item -LiteralPath $forestTerrainBenchmark
    }
    Invoke-ForgeEngineMarkerTest `
        -Name 'forgotten-forest' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $forestRuntimeLog, '--',
            '--module-package', $forestArtifact, '--forgotten-forest-smoke-test',
            '--terrain-benchmark', $forestTerrainBenchmark) `
        -Markers @(
            'OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug=forgotten-forest',
            'OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS manifests=129',
            'OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS provider=heightmap vertices=16641 triangles=32768 sectors=64 material=procedural-pbr-vertex-blend material_layers=4',
            'OPENRPG_FORGE_RUNTIME_MODELS_PASS manifests=129 instances=',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_GROUND_CONTACT_PASS',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_MMO_CONTROLS_PASS speed_multiplier=2.75',
            'OPENRPG_FORGE_AV4A_TERRAIN_BENCHMARK_PASS area=512x512',
            'OPENRPG_FORGE_RUNTIME_R036_EE_PASS nav_regions=1 nav_polygons=32455 blocked=313 links=3 path_points=',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_POLISH_PASS audio_cues=3 scatter=3 vfx_emitters=0 vfx_particles=0 water_animated=10 provenance=builtin',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_ANIMATION_PASS characters=6 variants=6 clips=81 rig=kaykit-rig-medium-41+quaternius-dragon-35 source=kaykit:character-pack-adventures-1.0+quaternius:lowpoly-animated-monsters',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_COMBAT_ANIMATION_PASS attack=3 hit=2 death=1 clip=1H_Melee_Attack_Slice_Horizontal',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_SMOKE_PASS terrain=16641 sectors=64 material=procedural-pbr-vertex-blend material_layers=4 scatter_layers=13') `
        -StopAfterMarkers $false

    $combatFeedbackMarkers = @(
        'OPENRPG_FORGE_COMBAT_FEEDBACK_VISUAL_PASS',
        'OPENRPG_FORGE_COMBAT_FEEDBACK_FIXTURES_PASS',
        'OPENRPG_FORGE_COMBAT_FEEDBACK_PERFORMANCE_PASS',
        'OPENRPG_FORGE_COMBAT_FEEDBACK_AUDIO_ROUTE_PASS',
        'OPENRPG_FORGE_COMBAT_FEEDBACK_SFX_MUTE_PASS',
        'OPENRPG_FORGE_COMBAT_FEEDBACK_ACCESSIBILITY_PASS'
    )
    foreach ($combatFeedbackProfile in @(
        [pscustomobject]@{ Name = 'normal'; Arguments = @() },
        [pscustomobject]@{ Name = 'reduced'; Arguments = @('--reduce-motion') },
        [pscustomobject]@{ Name = 'text-scale-2'; Arguments = @('--combat-feedback-text-scale', '2.0') }
    )) {
        $combatFeedbackLog = Join-Path $artifactRoot "runtime-combat-feedback-$($combatFeedbackProfile.Name).log"
        $profileMarkers = @($combatFeedbackMarkers)
        if ($combatFeedbackProfile.Name -eq 'reduced') {
            $profileMarkers += 'OPENRPG_FORGE_COMBAT_FEEDBACK_REDUCED_MOTION_PASS'
        }
        Invoke-ForgeEngineMarkerTest `
            -Name "runtime-combat-feedback-$($combatFeedbackProfile.Name)" `
            -ProjectPath $runtimePath `
            -Arguments (@(
                '--headless', '--rendering-method', 'forward_plus',
                '--path', $runtimePath, '--log-file', $combatFeedbackLog, '--',
                '--workspace', $forestWorkspace,
                '--spawn-id', '594a888d-da8c-5054-b546-a376cfae3ef1',
                '--combat-feedback-smoke-test') + $combatFeedbackProfile.Arguments) `
            -Markers $profileMarkers `
            -StopAfterMarkers $false
    }
    $forestWorldVisualProof = Join-Path $artifactRoot 'forgotten-forest-av6e-world-forward-plus.png'
    $forestWorldVisualLog = Join-Path $artifactRoot 'forgotten-forest-av6e-world-forward-plus.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'forgotten-forest-av6e-world-forward-plus' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'forward_plus', '--path', $runtimePath, '--log-file',
            $forestWorldVisualLog, '--', '--module-package', $forestArtifact,
            '--spawn-id', '594a888d-da8c-5054-b546-a376cfae3ef1',
            '--world-visual-capture', $forestWorldVisualProof) `
        -Markers @('OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method=forward_plus',
            'OPENRPG_FORGE_AV6E_WORLD_VISUAL_PASS file=forgotten-forest-av6e-world-forward-plus.png size=1600x900 area=forgotten-forest sectors=64 scatter=') `
        -StopAfterMarkers $false
    if (-not (Test-Path -LiteralPath $forestWorldVisualProof) -or
        [System.IO.File]::ReadAllBytes($forestWorldVisualProof).Length -lt 50000) {
        throw 'AV6-E world visual proof PNG is missing or too small.'
    }
    $forestCloseVisualProof = Join-Path $artifactRoot 'forgotten-forest-av6e-close-forward-plus.png'
    $forestCloseVisualLog = Join-Path $artifactRoot 'forgotten-forest-av6e-close-forward-plus.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'forgotten-forest-av6e-close-forward-plus' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'forward_plus', '--path', $runtimePath, '--log-file',
            $forestCloseVisualLog, '--', '--module-package', $forestArtifact,
            '--visual-capture', $forestCloseVisualProof) `
        -Markers @('OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method=forward_plus',
            'OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file=forgotten-forest-av6e-close-forward-plus.png size=1600x900') `
        -StopAfterMarkers $false
    if (-not (Test-Path -LiteralPath $forestCloseVisualProof) -or
        [System.IO.File]::ReadAllBytes($forestCloseVisualProof).Length -lt 50000) {
        throw 'AV6-E close visual proof PNG is missing or too small.'
    }
    for ($attempt = 0; $attempt -lt 10 -and -not (Test-Path -LiteralPath $forestTerrainBenchmark); $attempt++) {
        Start-Sleep -Milliseconds 250
    }
    if (-not (Test-Path -LiteralPath $forestTerrainBenchmark)) {
        throw 'Forgotten Forest terrain benchmark report was not written.'
    }
    $terrainBenchmark = Get-Content -LiteralPath $forestTerrainBenchmark -Raw | ConvertFrom-Json
    if ($terrainBenchmark.schemaVersion -ne 1 -or -not $terrainBenchmark.passed -or
        $terrainBenchmark.sample.moduleSlug -ne 'forgotten-forest' -or
        $terrainBenchmark.sample.widthMeters -ne 512 -or $terrainBenchmark.sample.depthMeters -ne 512 -or
        $terrainBenchmark.sample.heightSamples -ne 16641 -or
        $terrainBenchmark.sample.terrainVertices -ne 16641 -or
        $terrainBenchmark.sample.terrainTriangles -ne 32768 -or
        $terrainBenchmark.sample.navigationPolygons -ne 32455 -or
        $terrainBenchmark.sample.lodAssetCount -lt 122 -or
        $terrainBenchmark.sample.lodLevelCount -lt 300 -or
        $terrainBenchmark.sample.cullingGroupCount -lt 3 -or
        @($terrainBenchmark.checks).Count -ne 16 -or
        @($terrainBenchmark.checks | Where-Object { -not $_.passed }).Count -ne 0) {
        throw 'Forgotten Forest terrain benchmark report is incomplete or exceeds its budgets.'
    }
    Write-Output (
        'OPENRPG_FORGE_AV4A_TERRAIN_BENCHMARK_ARTIFACT_PASS ' +
        "rebuild_ms=$($terrainBenchmark.sample.reconstructionMilliseconds) " +
        "collision_ms=$($terrainBenchmark.sample.collisionMilliseconds) " +
        "navigation_ms=$($terrainBenchmark.sample.navigationMilliseconds) " +
        "sculpture_ms=$($terrainBenchmark.sample.sculptureMilliseconds) " +
        "working_set_bytes=$($terrainBenchmark.sample.processWorkingSetDeltaBytes)")

    $assetDeliveryReport = Join-Path $artifactRoot 'asset-delivery-report.json'
    $assetDeliveryMarkdown = Join-Path $artifactRoot 'asset-delivery-report.md'
    foreach ($deliveryArtifact in @($assetDeliveryReport, $assetDeliveryMarkdown)) {
        if (Test-Path -LiteralPath $deliveryArtifact) {
            Remove-Item -LiteralPath $deliveryArtifact
        }
    }
    Invoke-ForgeCliMarkerTest `
        -Arguments @(
            'run', '--project', $cliProject, '--configuration', 'Release', '--no-build', '--',
            'assets', 'delivery-report', $repositoryRoot, $forestWorkspace,
            '--module', $forestArtifact,
            '--benchmark', $forestTerrainBenchmark,
            '--output', $assetDeliveryReport,
            '--markdown', $assetDeliveryMarkdown,
            '--vault', $assetVault) `
        -Marker 'OPENRPG_FORGE_AV5_DELIVERY_REPORT_PASS catalog=665 local=617'
    if (-not (Test-Path -LiteralPath $assetDeliveryReport) -or
        -not (Test-Path -LiteralPath $assetDeliveryMarkdown)) {
        throw 'AV5 delivery reports were not written.'
    }
    $delivery = Get-Content -LiteralPath $assetDeliveryReport -Raw | ConvertFrom-Json
    if ($delivery.schemaVersion -ne 1 -or -not $delivery.passed -or
        $delivery.catalog.assets -ne 665 -or $delivery.catalog.localAssets -ne 617 -or
        $delivery.module.manifests -ne 129 -or $delivery.module.chunks -ne 186 -or
        $delivery.module.assetReferences -ne 809 -or $delivery.module.ungovernedReferences -ne 0 -or
        $delivery.module.chunkIntegrityFailures -ne 0 -or $delivery.performance.checks -ne 16 -or
        @($delivery.checks).Count -ne 16 -or
        @($delivery.checks | Where-Object { -not $_.passed }).Count -ne 0) {
        throw 'AV5 delivery report is incomplete or failed its frozen-catalog policy.'
    }
    Write-Output (
        'OPENRPG_FORGE_AV5_DELIVERY_ARTIFACT_PASS ' +
        "vault_files=$($delivery.vault.total.files) vault_bytes=$($delivery.vault.total.bytes) " +
        "workspace_files=$($delivery.module.workspaceFiles) workspace_bytes=$($delivery.module.workspaceBytes) " +
        "module_bytes=$($delivery.module.packageBytes) checks=$(@($delivery.checks).Count)")

    $mcpHostProject = Join-Path $repositoryRoot 'apps\Forge.McpHost\Forge.McpHost.csproj'
    $serverProject = Join-Path $repositoryRoot 'apps\Forge.Server\Forge.Server.csproj'
    Invoke-CheckedDotnet @('run', '--project', $serverProject, '--configuration', 'Release', '--no-build', '--', '--live-director-smoke-test')
    Invoke-CheckedDotnet @('run', '--project', $serverProject, '--configuration', 'Release', '--no-build', '--', '--live-director-access-smoke-test')
    Invoke-CheckedDotnet @(
        'run', '--project', $serverProject, '--configuration', 'Release', '--no-build', '--',
        '--network-beta-smoke-test')
    Invoke-CheckedDotnet @(
        'run', '--project', $mcpHostProject, '--configuration', 'Release', '--no-build', '--',
        '--workspace', $moduleWorkspace, '--self-test')
    $mcpScenarioLog = Join-Path $artifactRoot 'mcp-scenario.log'
    Invoke-CheckedDotnet @(
        'run', '--project', $mcpHostProject, '--configuration', 'Release', '--no-build', '--',
        '--workspace', $moduleWorkspace, '--scenario-test', '--engine', $enginePath,
        '--runtime', $runtimePath, '--module', $moduleArtifact, '--artifact', $mcpScenarioLog)

    $runtimeSave = Join-Path $artifactRoot 'crypt-of-ashes.orpsave'
    $saveCreateLog = Join-Path $artifactRoot 'save-create.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'save-create' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $saveCreateLog, '--', '--module-package', $moduleArtifact, '--save-file', $runtimeSave, '--save-smoke-create') `
        -Markers @('OPENRPG_FORGE_FORGESCRIPT_CACHE status=', 'OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format=1.4.0', 'OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=write', 'OPENRPG_FORGE_RUNTIME_SAVE_PASS') `
        -StopAfterMarkers $false
    $saveResumeLog = Join-Path $artifactRoot 'save-resume.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'save-resume' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $saveResumeLog, '--', '--module-package', $moduleArtifact, '--load-save', $runtimeSave, '--save-smoke-resume') `
        -Markers @('OPENRPG_FORGE_RUNTIME_SAVE_LOADED format=1.4.0', 'OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=load', 'OPENRPG_FORGE_RUNTIME_SAVE_RESUME_PASS') `
        -StopAfterMarkers $false
}
finally {
    Pop-Location
}

Write-Output 'OPENRPG_FORGE_VERIFICATION=PASS'
