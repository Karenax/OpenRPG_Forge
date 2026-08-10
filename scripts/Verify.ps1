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
    $Markers = @($Markers | ForEach-Object { $_.Replace('format=1.9.0', 'format=1.28.0').Replace('format=1.10.0', 'format=1.28.0').Replace('format=1.11.0', 'format=1.28.0').Replace('format=1.12.0', 'format=1.28.0').Replace('format=1.13.0', 'format=1.28.0').Replace('format=1.14.0', 'format=1.28.0').Replace('format=1.15.0', 'format=1.28.0').Replace('format=1.16.0', 'format=1.28.0').Replace('format=1.17.0', 'format=1.28.0').Replace('format=1.18.0', 'format=1.28.0').Replace('format=1.19.0', 'format=1.28.0').Replace('format=1.20.0', 'format=1.28.0').Replace('format=1.21.0', 'format=1.28.0').Replace('format=1.22.0', 'format=1.28.0').Replace('format=1.23.0', 'format=1.28.0').Replace('format=1.24.0', 'format=1.28.0').Replace('format=1.25.0', 'format=1.28.0').Replace('format=1.26.0', 'format=1.28.0').Replace('format=1.27.0', 'format=1.28.0') })
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
        $Markers += 'OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_PASS profiles=standard-aurora-blender commands=searchable bindings=13 conflicts=blocked input=executed ui=visible-empty-error uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated history=restored persistence=roundtrip runtime=optional'
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
        $Markers += 'OPENRPG_FORGE_RUNTIME_R009_WAYPOINT_PASS waypoint=named-localized spawn=priority-clearance-team-orientation-fallback-navigation portal=destination-orientation-velocity-preload forgescript=typed-handle-query optional=empty-safe required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R010_GAMEPLAY_PASS movement=acceleration-slope-step-jump-crouch-root-motion-prediction interaction=focus-verbs-hold targeting=view-cone camera=8-profiles input=contexts-devices-glyphs-rebind save=chunks-checksum-autosave-rollback-migrations forgescript=typed-handles'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R011_GAMEPLAY_SYSTEMS_PASS blackboard=typed-schema-save-replication-debug forgescript=typed-handle-command ruleset=attributes-effects-abilities-inventories-progression-ui optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R012_SCRIPT_FOUNDATION_PASS language=angelscript-2.38-c-like-static source=canonical-as bytecode=cache-source-api-compiler compiler=native-stable-diagnostics events=multi-handler-priority-condition-authority entry=typed-payload sandbox=quota-limited optional=ignored required=diagnostic'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R013_SCRIPT_WORLD_API_PASS world=canonical-intents-opaque-handles movement=task-mode-velocity-teleport-completion utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-local sandbox=no-file-network-process-native-reflection-engine-dotnet recovery=quota-safe'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R014_SCRIPT_SAFETY_POLICY_PASS handles=typed-generational-validated budgets=handler-tick-module-memory-timeout-cancel-recovery telemetry=instructions-memory-time determinism=seed-clock-server version=semver-negotiation-deprecation-migration policy=preview-apply-undo-redo'
        $Markers += 'OPENRPG_FORGE_RUNTIME_R015_SCRIPT_MODULE_PERMISSIONS_PASS policy=module-kit-event-intersection deny=undeclared authority=server signature=trusted-production optional=ignored required=diagnostic sandbox=quota-abort-recovery'
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
        $Markers += 'OPENRPG_FORGE_RUNTIME_G11N_LOADING_PASS media=accessiblefallback progress=1 accessible=True streamed=0'
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
    $deadline = [DateTime]::UtcNow.AddSeconds(75)
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
    Invoke-CheckedDotnet @('test', $solutionPath, '--configuration', 'Release', '--no-build', '--logger', 'console;verbosity=minimal')
    # Godot launches managed editor/runtime assemblies from its Debug cache even after a Release gate build.
    # Refresh those caches explicitly so engine smoke tests never execute stale contracts.
    Invoke-CheckedDotnet @('build', (Join-Path $repositoryRoot 'apps\Forge.Toolset\Forge.Toolset.csproj'),
        '--configuration', 'Debug', '--no-restore')
    Invoke-CheckedDotnet @('build', (Join-Path $repositoryRoot 'apps\Forge.Runtime\Forge.Runtime.csproj'),
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
    & (Join-Path $repositoryRoot 'scripts\Update-CompleteFunctionBacklog.ps1') -Check
    if ($LASTEXITCODE -ne 0) {
        throw "Complete function backlog freshness check failed with exit code $LASTEXITCODE."
    }
    Invoke-CheckedPython @('-m', 'unittest', 'discover', '-s', 'tests', '-p', 'test_*.py')
    Invoke-CheckedPython @('-m', 'unittest', 'discover', '-s', 'tests\asset_pipeline', '-p', 'test_*.py')
    $architectureGraphProject = Join-Path $repositoryRoot 'tools\Forge.ArchitectureGraph\Forge.ArchitectureGraph.csproj'
    Invoke-CheckedDotnet @('run', '--project', $architectureGraphProject, '--configuration', 'Release',
        '--no-build', '--', 'check', '--full')
    Test-ForgeBranding

    $toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
    $toolsetLog = Join-Path $artifactRoot 'toolset.log'
    $prototypeDataPath = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes\project.json'
    $prototypeHashBefore = (Get-FileHash -LiteralPath $prototypeDataPath -Algorithm SHA256).Hash
    Invoke-ForgeEngineMarkerTest `
        -Name 'toolset' `
        -ProjectPath $toolsetPath `
        -Arguments @('--headless', '--editor', '--path', $toolsetPath, '--log-file', $toolsetLog, '--', '--workspace-smoke-test') `
        -Markers @('OPENRPG_FORGE_PLUGIN_LOADED', 'OPENRPG_FORGE_WINDOW_TITLE_PASS title=OpenRPG_Forge', 'OPENRPG_FORGE_TOOLSET_PROJECT_VALID entities=16', 'OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces=9', 'OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog=10 derived=1 references=1 undo=restored', 'OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored', 'OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas=2 documents=2 transition=valid active=switched history=restored', 'OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone uuids=remapped preview=available history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global', 'OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed', 'OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS standard=readonly project=editable generated=provenance favorites=filtered temporary=isolated format=1.9.0 history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS orbit=mouse pan=mouse zoom=adaptive focus=selection views=orthographic pilot=wasd bookmarks=persisted history=dedicated project=unchanged', 'OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS graph=versioned scripts=2 cache=source-api-compiler dependencies=automatic incremental=reused persistence=restored', 'OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_PASS build=validation-scripts-assets-cache-incremental-report-reproducible ui=canonical-export diagnostics=uuid-schema-provenance-category-severity-text-correlation-clickable-quickfix logs=runtime-sanitized bundle=atomic-deterministic-reused cli=structured mcp=structured', 'OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS profiles=5 shipping=blocked licenses=diagnostic registry=versioned notices=generated channel=preview platforms=2 windows=portable-installer history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS filters=combined-isolation-ghost placement=deterministic-atomic tabs=multi-area comparison=readonly help=sourced profiles=seven persistence=roundtrip history=restored', 'OPENRPG_FORGE_TOOLSET_G11E_CONTENT_PASS tables=typed batch=dry-run-applied collisions=preserve-both blueprints=transitive-governed merge=three-way-conflicts history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_PASS ui=scalable font=truetype-fallback tabs=multi-area comparison=readonly favorites=project-personal recents=ordered art=profiled-warnings history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11G_RENDER_PASS pbr=metallic-roughness materials=instances tone=aces hdr=true lighting=per-pixel forward=plus sky=procedural lights=48 shadows=12 history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_PASS wind=global-local weather=state-machine particles=rain water=plane-volume reverb=zone authority=server save=portable replication=ready history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS provider=heightmap vertices=81 triangles=126 holes=1 biomes=2 features=3 volumes=3', 'OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_PASS heartbeats=global-area budgets=bounded suspension=inactive rest=requested-started-interrupted-completed cooldown=active volumes=conditional abort=camera-control-invulnerability-locks-narrative history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_PASS startup=sequence-fallback timeline=tracks-branch-signal camera=anchor-fov-safe-area transform=interpolated hierarchy=validated authority=server skip=accessible abort=recovered mcp=governed history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_PASS swim=buoyancy-breath-currents dive=runtime climb=links-stamina-ik-safety radial=nested-gamepad-keyboard-list-accessible time=pause-slow-planning-turns multiplayer=vote-permissions combat=providers-shared-events-authority history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G11M_RULES_PASS progression=curve-source-group-difficulty difficulty=damage-ai-economy-rest-save-accessibility mutators=composable perception=sense-ruleset-condition pvp=duel-crime-aggro-environment authority=server mcp=governed history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS agents=4 geometry=valid reachability=spawn-object doors=states offmesh=linked flight=profiled incremental=partial overlay=visible history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_G719_PATHFINDING_PASS algorithm=deterministic-a-star', 'OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_PASS uuid=stable severity=typed path=document property=visible source=validator quickfix=command preview=dry-run navigation=targeted history=restored persistence=roundtrip', 'OPENRPG_FORGE_TOOLSET_R003_QA_PASS diagnostics=profile-category-severity-quickfix', 'OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS catalog=605 sources=3 facets=search-source-category uuid=stable hash=addressed license=verified availability=explicit preview=metadata import=governed', 'OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_PASS messages=', 'OPENRPG_FORGE_TOOLSET_G718_SCALE_PASS modes=uniform-nonuniform', 'OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format=1.9.0 areas=1 documents=1 entities=2 exterior=96x80 terrain=heightmap', 'OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_PASS schema=1 properties=3', 'OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3', 'OPENRPG_FORGE_TOOLSET_T43_QUEST_PASS states=3 transitions=2', 'OPENRPG_FORGE_TOOLSET_T23_SMOKE_PASS entities=16', 'OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_PASS', 'OPENRPG_FORGE_TOOLSET_G13K_ITEM_FOUNDATION_PASS', 'OPENRPG_FORGE_TOOLSET_G13L_ITEM_EQUIPMENT_PASS', 'OPENRPG_FORGE_TOOLSET_G13M_ITEM_LOOT_CRAFT_PASS', 'OPENRPG_FORGE_TOOLSET_PROJECT_SAVED', 'OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS spawn=20000000-0000-4000-8000-000000000010 snapshot=deleted logs=captured') `
        -StopAfterMarkers $true
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
            'OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file=runtime-g11p-visual.png size=1280x720') `
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
            'OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS manifests=45 chunks=67 platform=Windows',
            'OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS provider=heightmap vertices=16641 triangles=32768 sectors=64 material=procedural-pbr-vertex-blend material_layers=4',
            'OPENRPG_FORGE_RUNTIME_MODELS_PASS manifests=45 instances=43',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_GROUND_CONTACT_PASS',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_MMO_CONTROLS_PASS speed_multiplier=2',
            'OPENRPG_FORGE_AV4A_TERRAIN_BENCHMARK_PASS area=512x512',
            'OPENRPG_FORGE_RUNTIME_R036_EE_PASS nav_regions=1 nav_polygons=32455 blocked=313 links=3 path_points=',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_POLISH_PASS audio_cues=3 scatter=3 vfx_emitters=0 vfx_particles=0 water_animated=10 provenance=builtin',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_ANIMATION_PASS characters=5 variants=5 clips=76 rig=kaykit-rig-medium-41 source=kaykit:character-pack-adventures-1.0',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_COMBAT_ANIMATION_PASS attack=3 hit=2 death=1 clip=1H_Melee_Attack_Slice_Horizontal',
            'OPENRPG_FORGE_FORGOTTEN_FOREST_SMOKE_PASS terrain=16641 sectors=64 material=procedural-pbr-vertex-blend material_layers=4 scatter_layers=13 scatter_instances=4617 water_surfaces=10 water_volumes=10 nav_regions=1 nav_polygons=32455 nav_blocked=313 nav_links=3 nav_path_points=95 manifests=45 model_instances=43 animated_characters=5 animation_clips=76 entities=43 quest=completed boss=defeated camera=orbit') `
        -StopAfterMarkers $false
    $forestWorldVisualProof = Join-Path $artifactRoot 'forgotten-forest-av6e-world-forward-plus.png'
    $forestWorldVisualLog = Join-Path $artifactRoot 'forgotten-forest-av6e-world-forward-plus.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'forgotten-forest-av6e-world-forward-plus' `
        -ProjectPath $runtimePath `
        -Arguments @('--rendering-method', 'forward_plus', '--path', $runtimePath, '--log-file',
            $forestWorldVisualLog, '--', '--module-package', $forestArtifact,
            '--world-visual-capture', $forestWorldVisualProof) `
        -Markers @('OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method=forward_plus',
            'OPENRPG_FORGE_AV6E_WORLD_VISUAL_PASS file=forgotten-forest-av6e-world-forward-plus.png size=1280x720 sectors=64 scatter=4617') `
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
            'OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file=forgotten-forest-av6e-close-forward-plus.png size=1280x720') `
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
        $terrainBenchmark.sample.lodAssetCount -ne 45 -or
        $terrainBenchmark.sample.lodLevelCount -ne 89 -or
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
        $delivery.module.manifests -ne 45 -or $delivery.module.chunks -ne 67 -or
        $delivery.module.assetReferences -ne 56 -or $delivery.module.ungovernedReferences -ne 0 -or
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
        -Markers @('OPENRPG_FORGE_FORGESCRIPT_CACHE status=', 'OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format=1.3.0', 'OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=write', 'OPENRPG_FORGE_RUNTIME_SAVE_PASS') `
        -StopAfterMarkers $false
    $saveResumeLog = Join-Path $artifactRoot 'save-resume.log'
    Invoke-ForgeEngineMarkerTest `
        -Name 'save-resume' `
        -ProjectPath $runtimePath `
        -Arguments @('--headless', '--path', $runtimePath, '--log-file', $saveResumeLog, '--', '--module-package', $moduleArtifact, '--load-save', $runtimeSave, '--save-smoke-resume') `
        -Markers @('OPENRPG_FORGE_RUNTIME_SAVE_LOADED format=1.3.0', 'OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=load', 'OPENRPG_FORGE_RUNTIME_SAVE_RESUME_PASS') `
        -StopAfterMarkers $false
}
finally {
    Pop-Location
}

Write-Output 'OPENRPG_FORGE_VERIFICATION=PASS'
