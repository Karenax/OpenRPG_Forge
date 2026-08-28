@tool
extends Node

const FORMAT_VERSION := "1.0.0"
const PROTOCOL_VERSION := "1.0.0"
const WORKSPACE_SETTING := "openrpg/world_generation/workspace_root"
const WORKSPACE_ENVIRONMENT := "OPENRPG_WORLD_GENERATION_WORKSPACE"
const AUTO_PROCESS_SETTING := "openrpg/world_generation/auto_process_jobs"
const POLL_SECONDS_SETTING := "openrpg/world_generation/poll_seconds"
const JOBS_RELATIVE_DIRECTORY := "world/generation/jobs"
const ARTIFACTS_RELATIVE_DIRECTORY := "world/generation/artifacts"
const PROVIDER_RECEIPT_RELATIVE_PATH := ".openrpg/world-generation/provider-installation.json"
const BACKEND_SCRIPT_PATH := "res://addons/openrpg_world_generation/openrpg_world_generation_backend.gd"
const SOURCE_BACKEND_SCRIPT_PATH := "res://integrations/godot/addons/openrpg_world_generation/openrpg_world_generation_backend.gd"
const ALLOWED_OPERATIONS := {
    "terrain3d:terrain": "terrain3d.generate_heightfield",
    "terrain3d:hydrology": "terrain3d.carve_hydrology",
    "pcggodot:biomes": "pcgodot.generate_biomes",
    "pcggodot:roads": "pcgodot.generate_road_network",
    "pcggodot:settlements": "pcgodot.generate_settlements",
    "pcggodot:scatter": "pcgodot.scatter_environment",
    "godotnative:terrain": "godotnative.terrain",
    "godotnative:hydrology": "godotnative.hydrology",
    "godotnative:biomes": "godotnative.biomes",
    "godotnative:roads": "godotnative.roads",
    "godotnative:settlements": "godotnative.settlements",
    "godotnative:scatter": "godotnative.scatter",
    "godotnative:gameplay": "godot.generate_gameplay_anchors",
    "godotnative:navigation": "godot.bake_navigation",
    "godotnative:validation": "godot.validate_generated_world",
}
const CANONICAL_PROMOTION_STAGES := [
    "terrain",
    "hydrology",
    "biomes",
    "roads",
    "settlements",
    "scatter",
    "gameplay",
    "navigation",
    "validation",
]

var _elapsed := 0.0
var _processing := false

func _ready() -> void:
    set_process(bool(ProjectSettings.get_setting(AUTO_PROCESS_SETTING, false)))

func _process(delta: float) -> void:
    if _processing:
        return
    _elapsed += delta
    var poll_seconds := maxf(float(ProjectSettings.get_setting(POLL_SECONDS_SETTING, 1.0)), 0.25)
    if _elapsed < poll_seconds:
        return
    _elapsed = 0.0
    process_pending_jobs()

func process_pending_jobs() -> Dictionary:
    if _processing:
        return { "succeeded": false, "processed": 0, "diagnostic": "A world-generation queue scan is already running." }
    _processing = true
    var processed := 0
    var failures := 0
    var directory_path := _jobs_directory()
    var directory := DirAccess.open(directory_path)
    if directory == null:
        _processing = false
        return { "succeeded": true, "processed": 0, "failed": 0, "directory": directory_path }

    var file_names := PackedStringArray()
    directory.list_dir_begin()
    var file_name := directory.get_next()
    while not file_name.is_empty():
        if not directory.current_is_dir() and file_name.ends_with(".json"):
            file_names.append(file_name)
        file_name = directory.get_next()
    directory.list_dir_end()
    file_names.sort()

    for queued_file in file_names:
        var outcome := await _process_job_file(directory_path.path_join(queued_file))
        if bool(outcome.get("processed", false)):
            processed += 1
        if not bool(outcome.get("succeeded", true)):
            failures += 1

    _processing = false
    return {
        "succeeded": failures == 0,
        "processed": processed,
        "failed": failures,
        "directory": directory_path,
    }

func _process_job_file(path: String) -> Dictionary:
    var wrapper := _read_json_object(path)
    if wrapper.is_empty():
        return { "succeeded": false, "processed": false }
    if str(wrapper.get("formatVersion", "")) != FORMAT_VERSION:
        return { "succeeded": false, "processed": false }
    var job_variant: Variant = wrapper.get("job", {})
    if typeof(job_variant) != TYPE_DICTIONARY:
        return { "succeeded": false, "processed": false }
    var job := job_variant as Dictionary
    var initial_status := str(job.get("status", "")).to_lower()
    if initial_status != "queued" and initial_status != "running":
        return { "succeeded": true, "processed": false }

    var validation := _validate_job(job)
    if not bool(validation.get("succeeded", false)):
        _finish_job(wrapper, job, "failed", validation.get("diagnostics", []), path)
        return { "succeeded": false, "processed": true }

    job["status"] = "running"
    job["updatedAtUtc"] = _utc_now()
    _append_audit(job, "resumed" if initial_status == "running" else "running", true,
        "Godot worker resumed the durable execution plan after interruption." if initial_status == "running" else
        "Godot worker accepted the validated execution plan.")
    wrapper["job"] = job
    if not _write_json_atomic(path, wrapper):
        return { "succeeded": false, "processed": false }

    var backend := get_node_or_null("/root/OpenRPGWorldGenerationBackend")
    if backend == null or not backend.has_method("execute_forge_world_generation_step"):
        _finish_job(wrapper, job, "failed", [{
            "code": "world_generation.worker.backend_missing",
            "message": "OpenRPGWorldGenerationBackend is not available.",
            "path": "world-generation/job/backend"
        }], path)
        return { "succeeded": false, "processed": true }

    var plan: Dictionary = job.get("plan", {})
    var recipe: Dictionary = job.get("recipe", {})
    var diagnostics: Array = []
    var provider_evidence: Array = []
    var steps: Array = plan.get("steps", [])
    steps.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
        return int(left.get("order", 0)) < int(right.get("order", 0)))
    for step_variant in steps:
        if _job_status(path) == "cancelled":
            return { "succeeded": true, "processed": true, "cancelled": true }
        if typeof(step_variant) != TYPE_DICTIONARY:
            diagnostics.append(_diagnostic("world_generation.worker.step_invalid", "Execution step is not an object.", "world-generation/job/steps"))
            break
        var step := step_variant as Dictionary
        var request := {
            "protocolVersion": PROTOCOL_VERSION,
            "planId": str(plan.get("planId", "")),
            "transactionId": str(plan.get("transactionId", "")),
            "projectRevision": int(plan.get("projectRevision", 0)),
            "recipeRevision": _revision_value(plan.get("recipeRevision", 0)),
            "recipeId": str(plan.get("recipeId", "")),
            "areaId": str(plan.get("areaId", "")),
            "recipeFingerprint": str(plan.get("recipeFingerprint", "")),
            "planFingerprint": str(plan.get("planFingerprint", "")),
            "seed": int(plan.get("seed", 0)),
            "dryRun": false,
            "step": step,
            "recipe": recipe,
        }
        var result_variant: Variant = backend.call("execute_forge_world_generation_step", JSON.stringify(request))
        if typeof(result_variant) != TYPE_DICTIONARY:
            diagnostics.append(_diagnostic("world_generation.worker.result_invalid", "Backend returned no structured result.", "world-generation/job/steps/%s" % step.get("order", "?")))
            break
        var result := result_variant as Dictionary
        if not bool(result.get("succeeded", false)):
            var step_diagnostics: Array = result.get("diagnostics", [])
            if step_diagnostics.is_empty():
                diagnostics.append(_diagnostic("world_generation.worker.step_failed", "Backend step failed without diagnostics.", "world-generation/job/steps/%s" % step.get("order", "?")))
            else:
                diagnostics.append_array(step_diagnostics)
            break
        if bool(result.get("fallbackUsed", false)):
            _append_audit(job, "fallback", true,
                "Stage '%s' used the native Godot fallback because backend '%s' was unavailable." % [
                    str(step.get("stage", "unknown")), str(result.get("requestedBackend", "unknown"))])
        var evidence := _provider_evidence_for_step(step, result)
        if evidence.is_empty():
            diagnostics.append(_diagnostic(
                "world_generation.worker.provider_evidence_missing",
                "No verified provider receipt was available for stage '%s'." % str(step.get("stage", "unknown")),
                "world-generation/job/providerEvidence"))
            break
        provider_evidence.append(evidence)
        if str(step.get("backend", "")).to_lower() == "pcggodot":
            for _settle_index in 3:
                await get_tree().physics_frame
                await get_tree().process_frame

    var succeeded := diagnostics.is_empty()
    if _job_status(path) == "cancelled":
        return { "succeeded": true, "processed": true, "cancelled": true }
    if succeeded and _requires_provider_candidate(steps):
        if not _can_create_promotion_candidate(steps, provider_evidence):
            diagnostics.append(_diagnostic(
                "world_generation.worker.promotion_providers_invalid",
                "The complete provider-backed plan did not retain verified Terrain3D and graph PCG evidence.",
                "world-generation/job/promotion"))
            succeeded = false
        else:
            var candidate := _create_promotion_candidate(job, backend, provider_evidence)
            if not bool(candidate.get("succeeded", false)):
                var candidate_diagnostics_variant: Variant = candidate.get("diagnostics", [])
                if typeof(candidate_diagnostics_variant) == TYPE_ARRAY:
                    diagnostics.append_array(candidate_diagnostics_variant as Array)
                if diagnostics.is_empty():
                    diagnostics.append(_diagnostic(
                        "world_generation.worker.promotion_candidate_failed",
                        "Provider-neutral canonical-promotion candidate could not be created.",
                        "world-generation/job/promotion"))
                succeeded = false
            elif bool(candidate.get("created", false)):
                _append_audit(job, "promotion-candidate", true,
                    "Provider-neutral derived scene and validation report were persisted for explicit promotion.")
    _finish_job(wrapper, job, "succeeded" if succeeded else "failed", diagnostics, path)
    return { "succeeded": succeeded, "processed": true }

func _validate_job(job: Dictionary) -> Dictionary:
    var diagnostics: Array = []
    if int(job.get("schemaVersion", 0)) != 1:
        diagnostics.append(_diagnostic("world_generation.worker.schema_unsupported", "World-generation job schema is unsupported.", "world-generation/job/schemaVersion"))
    var plan_variant: Variant = job.get("plan", {})
    var recipe_variant: Variant = job.get("recipe", {})
    if typeof(plan_variant) != TYPE_DICTIONARY or typeof(recipe_variant) != TYPE_DICTIONARY:
        diagnostics.append(_diagnostic("world_generation.worker.payload_missing", "Job plan or recipe payload is missing.", "world-generation/job"))
        return { "succeeded": false, "diagnostics": diagnostics }
    var plan := plan_variant as Dictionary
    var recipe := recipe_variant as Dictionary
    if not _is_guid(str(job.get("jobId", ""))) or not _is_guid(str(job.get("planId", ""))):
        diagnostics.append(_diagnostic("world_generation.worker.identifier_invalid", "Job identifiers are invalid.", "world-generation/job"))
    if not bool(plan.get("readyToApply", false)):
        diagnostics.append(_diagnostic("world_generation.worker.plan_not_ready", "Execution plan is not approved for application.", "world-generation/job/plan"))
    var plan_diagnostics_variant: Variant = plan.get("diagnostics", [])
    if typeof(plan_diagnostics_variant) != TYPE_ARRAY or not (plan_diagnostics_variant as Array).is_empty():
        diagnostics.append(_diagnostic("world_generation.worker.plan_diagnostics_invalid", "An approved execution plan cannot contain diagnostics.", "world-generation/job/plan/diagnostics"))
    if str(job.get("planId", "")) != str(plan.get("planId", "")):
        diagnostics.append(_diagnostic("world_generation.worker.plan_mismatch", "Job and execution-plan IDs differ.", "world-generation/job/planId"))
    if int(job.get("projectRevision", -1)) < 0 or int(job.get("projectRevision", -1)) != int(plan.get("projectRevision", -2)):
        diagnostics.append(_diagnostic("world_generation.worker.project_revision_mismatch", "Job and plan project revisions differ.", "world-generation/job/projectRevision"))
    if _revision_value(job.get("recipeRevision", -1)) < 0 or \
            _revision_value(job.get("recipeRevision", -1)) != _revision_value(plan.get("recipeRevision", -2)):
        diagnostics.append(_diagnostic("world_generation.worker.recipe_revision_mismatch", "Job and plan recipe revisions differ.", "world-generation/job/recipeRevision"))
    if str(job.get("recipeFingerprint", "")) != str(plan.get("recipeFingerprint", "")):
        diagnostics.append(_diagnostic("world_generation.worker.fingerprint_mismatch", "Job and plan fingerprints differ.", "world-generation/job/recipeFingerprint"))
    if not _is_lower_sha256(str(job.get("recipeFingerprint", ""))) or \
            not _is_lower_sha256(str(plan.get("planFingerprint", ""))):
        diagnostics.append(_diagnostic("world_generation.worker.fingerprint_invalid", "Job fingerprints must be lowercase SHA-256 values.", "world-generation/job/planFingerprint"))
    if str(plan.get("recipeId", "")) != str(recipe.get("id", "")):
        diagnostics.append(_diagnostic("world_generation.worker.recipe_mismatch", "Plan and recipe IDs differ.", "world-generation/job/recipe"))
    if str(plan.get("areaId", "")) != str(recipe.get("areaId", "")):
        diagnostics.append(_diagnostic("world_generation.worker.area_mismatch", "Plan and recipe area IDs differ.", "world-generation/job/recipe"))
    if int(plan.get("seed", 0)) != int(recipe.get("seed", 0)):
        diagnostics.append(_diagnostic("world_generation.worker.seed_mismatch", "Plan and recipe seeds differ.", "world-generation/job/recipe/seed"))
    _validate_recipe(recipe, diagnostics)
    var steps_variant: Variant = plan.get("steps", [])
    if typeof(steps_variant) != TYPE_ARRAY:
        diagnostics.append(_diagnostic("world_generation.worker.steps_invalid", "Execution steps are not an array.", "world-generation/job/plan/steps"))
    else:
        _validate_steps(steps_variant as Array, diagnostics)
    if diagnostics.is_empty() and str(plan.get("planFingerprint", "")) != _compute_plan_fingerprint(plan):
        diagnostics.append(_diagnostic("world_generation.worker.plan_fingerprint_mismatch", "Execution-plan fingerprint does not match its content.", "world-generation/job/planFingerprint"))
    var audit_variant: Variant = job.get("audit", [])
    if typeof(audit_variant) != TYPE_ARRAY or (audit_variant as Array).is_empty():
        diagnostics.append(_diagnostic("world_generation.worker.audit_missing", "Job audit history is missing.", "world-generation/job/audit"))
    else:
        var previous_timestamp := ""
        for entry_variant in audit_variant as Array:
            if typeof(entry_variant) != TYPE_DICTIONARY:
                diagnostics.append(_diagnostic("world_generation.worker.audit_invalid", "Job audit entry is not an object.", "world-generation/job/audit"))
                break
            var entry := entry_variant as Dictionary
            var action := str(entry.get("action", ""))
            var detail := str(entry.get("detail", ""))
            var occurred := str(entry.get("occurredAtUtc", ""))
            if action.is_empty() or action.length() > 64 or detail.is_empty() or detail.length() > 4096 or \
                    occurred.is_empty() or (not previous_timestamp.is_empty() and occurred < previous_timestamp):
                diagnostics.append(_diagnostic("world_generation.worker.audit_invalid", "Job audit history is malformed or not chronological.", "world-generation/job/audit"))
                break
            previous_timestamp = occurred
    return { "succeeded": diagnostics.is_empty(), "diagnostics": diagnostics }

func _validate_recipe(recipe: Dictionary, diagnostics: Array) -> void:
    if int(recipe.get("schemaVersion", 0)) != 1 or not _is_guid(str(recipe.get("id", ""))) or \
            not _is_guid(str(recipe.get("areaId", ""))):
        diagnostics.append(_diagnostic("world_generation.worker.recipe_invalid", "Recipe schema or identifiers are invalid.", "world-generation/job/recipe"))
    var title := str(recipe.get("title", ""))
    var provenance := str(recipe.get("provenance", ""))
    if title.is_empty() or title.length() > 160 or provenance.is_empty() or provenance.length() > 512:
        diagnostics.append(_diagnostic("world_generation.worker.recipe_text_invalid", "Recipe title or provenance is invalid.", "world-generation/job/recipe"))
    var bounds_variant: Variant = recipe.get("bounds", {})
    if typeof(bounds_variant) != TYPE_DICTIONARY:
        diagnostics.append(_diagnostic("world_generation.worker.bounds_invalid", "Recipe bounds are missing.", "world-generation/job/recipe/bounds"))
    else:
        var bounds := bounds_variant as Dictionary
        if not _finite_positive(bounds.get("widthMeters", 0), 32768.0) or \
                not _finite_positive(bounds.get("depthMeters", 0), 32768.0) or \
                not _finite_positive(bounds.get("maxElevationMeters", 0), 8192.0):
            diagnostics.append(_diagnostic("world_generation.worker.bounds_invalid", "Recipe bounds exceed supported limits.", "world-generation/job/recipe/bounds"))
    var terrain_variant: Variant = recipe.get("terrain", {})
    if typeof(terrain_variant) != TYPE_DICTIONARY:
        diagnostics.append(_diagnostic("world_generation.worker.terrain_invalid", "Recipe terrain is missing.", "world-generation/job/recipe/terrain"))
    else:
        var terrain := terrain_variant as Dictionary
        var roughness_variant: Variant = terrain.get("roughness", -1)
        var roughness := float(roughness_variant)
        if str(terrain.get("archetype", "")).is_empty() or not is_finite(roughness) or roughness < 0.0 or \
                roughness > 1.0 or int(terrain.get("erosionPasses", 0)) < 0 or \
                int(terrain.get("erosionPasses", 0)) > 64:
            diagnostics.append(_diagnostic("world_generation.worker.terrain_invalid", "Recipe terrain is outside supported limits.", "world-generation/job/recipe/terrain"))
    var collection_limits := {
        "rivers": 128,
        "biomes": 128,
        "roads": 512,
        "settlements": 256,
        "scatter": 512,
    }
    for key: Variant in collection_limits.keys():
        var items_variant: Variant = recipe.get(str(key), [])
        if typeof(items_variant) != TYPE_ARRAY or (items_variant as Array).size() > int(collection_limits[key]):
            diagnostics.append(_diagnostic("world_generation.worker.collection_invalid", "Recipe collection '%s' exceeds its contract." % key, "world-generation/job/recipe/%s" % key))
    var regeneration_variant: Variant = recipe.get("regeneration", {})
    if typeof(regeneration_variant) != TYPE_DICTIONARY:
        diagnostics.append(_diagnostic("world_generation.worker.regeneration_invalid", "Recipe regeneration policy is missing.", "world-generation/job/recipe/regeneration"))
    else:
        var policy := regeneration_variant as Dictionary
        var protected_variant: Variant = policy.get("protectedNodePaths", [])
        if typeof(protected_variant) != TYPE_ARRAY or (protected_variant as Array).size() > 128:
            diagnostics.append(_diagnostic("world_generation.worker.protected_paths_invalid", "Protected node paths exceed their contract.", "world-generation/job/recipe/regeneration/protectedNodePaths"))
        else:
            for path_variant in protected_variant as Array:
                if not _safe_relative_node_path(str(path_variant)):
                    diagnostics.append(_diagnostic("world_generation.worker.protected_path_invalid", "Protected node path is unsafe.", "world-generation/job/recipe/regeneration/protectedNodePaths"))
                    break
    var backends_variant: Variant = recipe.get("backends", {})
    if typeof(backends_variant) != TYPE_DICTIONARY:
        diagnostics.append(_diagnostic("world_generation.worker.backends_invalid", "Recipe backends are missing.", "world-generation/job/recipe/backends"))
    else:
        for stage_variant: Variant in (backends_variant as Dictionary).keys():
            var stage := str(stage_variant).to_lower()
            var backend := str((backends_variant as Dictionary)[stage_variant]).to_lower()
            if not _backend_supports(backend, stage):
                diagnostics.append(_diagnostic("world_generation.worker.backend_stage_invalid", "Recipe backend cannot execute its stage.", "world-generation/job/recipe/backends/%s" % stage))

func _finite_positive(value: Variant, maximum: float) -> bool:
    if typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT:
        return false
    var numeric := float(value)
    return is_finite(numeric) and numeric > 0.0 and numeric <= maximum

func _backend_supports(backend: String, stage: String) -> bool:
    match backend:
        "auto": return ALLOWED_OPERATIONS.keys().any(func(key: Variant) -> bool: return str(key).ends_with(":" + stage))
        "terrain3d": return stage == "terrain" or stage == "hydrology"
        "pcggodot": return stage in ["biomes", "roads", "settlements", "scatter"]
        "godotnative": return stage in ["terrain", "hydrology", "biomes", "roads", "settlements", "scatter", "gameplay", "navigation", "validation"]
        _: return false

func _safe_relative_node_path(value: String) -> bool:
    if value.is_empty() or value.length() > 256 or value.begins_with("/") or value.begins_with("\\") or value.contains(":"):
        return false
    var segments := value.replace("\\", "/").split("/", false)
    if segments.is_empty():
        return false
    for segment in segments:
        if segment == "." or segment == "..":
            return false
        for character in segment:
            if "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.".find(str(character)) < 0:
                return false
    return true

func _validate_steps(steps: Array, diagnostics: Array) -> void:
    if steps.is_empty() or steps.size() > 9:
        diagnostics.append(_diagnostic("world_generation.worker.steps_invalid", "Execution plan must contain between one and nine steps.", "world-generation/job/plan/steps"))
        return
    for index in range(steps.size()):
        var step_variant: Variant = steps[index]
        if typeof(step_variant) != TYPE_DICTIONARY:
            diagnostics.append(_diagnostic("world_generation.worker.step_invalid", "Execution step is not an object.", "world-generation/job/plan/steps/%s" % index))
            continue
        var step := step_variant as Dictionary
        var stage := str(step.get("stage", "")).to_lower()
        var backend := str(step.get("backend", "")).to_lower()
        var operation_key := "%s:%s" % [backend, stage]
        if int(step.get("order", -1)) != index:
            diagnostics.append(_diagnostic("world_generation.worker.step_order_invalid", "Execution-step order must be contiguous.", "world-generation/job/plan/steps/%s/order" % index))
        if not ALLOWED_OPERATIONS.has(operation_key) or str(step.get("operation", "")) != str(ALLOWED_OPERATIONS[operation_key]):
            diagnostics.append(_diagnostic("world_generation.worker.operation_invalid", "Execution-step operation is not allowlisted.", "world-generation/job/plan/steps/%s/operation" % index))
        var dependencies_variant: Variant = step.get("dependsOn", [])
        if typeof(dependencies_variant) != TYPE_ARRAY:
            diagnostics.append(_diagnostic("world_generation.worker.dependencies_invalid", "Execution-step dependencies are not an array.", "world-generation/job/plan/steps/%s/dependsOn" % index))
        else:
            var dependencies := dependencies_variant as Array
            if (index == 0 and not dependencies.is_empty()) or \
                    (index > 0 and (dependencies.size() != 1 or int(dependencies[0]) != index - 1)):
                diagnostics.append(_diagnostic("world_generation.worker.dependencies_invalid", "Execution-step dependency chain is invalid.", "world-generation/job/plan/steps/%s/dependsOn" % index))
        var parameters_variant: Variant = step.get("parameters", {})
        if typeof(parameters_variant) != TYPE_DICTIONARY or (parameters_variant as Dictionary).size() > 64:
            diagnostics.append(_diagnostic("world_generation.worker.parameters_invalid", "Execution-step parameters are invalid.", "world-generation/job/plan/steps/%s/parameters" % index))

func _compute_plan_fingerprint(plan: Dictionary) -> String:
    var canonical := ""
    canonical = _append_fingerprint(canonical, str(int(plan.get("projectRevision", 0))))
    canonical = _append_fingerprint(canonical, str(_revision_value(plan.get("recipeRevision", 0))))
    canonical = _append_fingerprint(canonical, str(plan.get("recipeId", "")))
    canonical = _append_fingerprint(canonical, str(plan.get("areaId", "")))
    canonical = _append_fingerprint(canonical, str(int(plan.get("seed", 0))))
    canonical = _append_fingerprint(canonical, str(plan.get("recipeFingerprint", "")))
    canonical = _append_fingerprint(canonical, "True" if bool(plan.get("readyToApply", false)) else "False")
    var steps: Array = plan.get("steps", [])
    for step_variant in steps:
        var step := step_variant as Dictionary
        canonical = _append_fingerprint(canonical, str(int(step.get("order", 0))))
        canonical = _append_fingerprint(canonical, _enum_pascal(str(step.get("stage", ""))))
        canonical = _append_fingerprint(canonical, _backend_pascal(str(step.get("backend", ""))))
        canonical = _append_fingerprint(canonical, str(step.get("operation", "")))
        var parameters: Dictionary = step.get("parameters", {})
        var keys := PackedStringArray()
        for key: Variant in parameters.keys():
            keys.append(str(key))
        keys.sort()
        for key in keys:
            canonical = _append_fingerprint(canonical, key)
            canonical = _append_fingerprint(canonical, str(parameters[key]))
        var dependencies: Array = step.get("dependsOn", [])
        dependencies.sort()
        canonical = _append_fingerprint(canonical, ",".join(dependencies.map(func(value: Variant) -> String: return str(int(value)))))
    return canonical.sha256_text()

func _append_fingerprint(target: String, value: String) -> String:
    return target + "%s:%s;" % [value.length(), value]

func _enum_pascal(value: String) -> String:
    return value.substr(0, 1).to_upper() + value.substr(1)

func _backend_pascal(value: String) -> String:
    match value.to_lower():
        "terrain3d": return "Terrain3D"
        "pcggodot": return "PcgGodot"
        "godotnative": return "GodotNative"
        _: return _enum_pascal(value)

func _finish_job(wrapper: Dictionary, job: Dictionary, status: String, diagnostics: Array, path: String) -> void:
    job["status"] = status
    job["updatedAtUtc"] = _utc_now()
    job["diagnostics"] = diagnostics
    _append_audit(job, status, status != "failed",
        "Job completed successfully." if diagnostics.is_empty() else _diagnostics_detail(diagnostics))
    wrapper["job"] = job
    _write_json_atomic(path, wrapper)

func _build_promotion_validation_report(
        job: Dictionary,
        bake_result: Dictionary,
        providers: Array) -> Dictionary:
    var plan_variant: Variant = job.get("plan", {})
    var recipe_variant: Variant = job.get("recipe", {})
    if typeof(plan_variant) != TYPE_DICTIONARY or typeof(recipe_variant) != TYPE_DICTIONARY:
        return {}
    var plan := plan_variant as Dictionary
    var recipe := recipe_variant as Dictionary
    var asset_selection_source := {
        "terrain": recipe.get("terrain", {}),
        "rivers": recipe.get("rivers", []),
        "biomes": recipe.get("biomes", []),
        "roads": recipe.get("roads", []),
        "settlements": recipe.get("settlements", []),
        "scatter": recipe.get("scatter", []),
        "gameplay": recipe.get("gameplay", {}),
    }
    var asset_selection_fingerprint := (
        "openrpg-world-generation-asset-selection-v1|" +
        _canonical_json(asset_selection_source)).sha256_text()
    var bake_diagnostics_variant: Variant = bake_result.get("diagnostics", [])
    var bake_diagnostics: Array = \
        bake_diagnostics_variant as Array if typeof(bake_diagnostics_variant) == TYPE_ARRAY else []
    var regeneration_variant: Variant = recipe.get("regeneration", {})
    var regeneration: Dictionary = \
        regeneration_variant as Dictionary if typeof(regeneration_variant) == TYPE_DICTIONARY else {}
    return {
        "schemaVersion": 1,
        "jobId": str(job.get("jobId", "")),
        "planId": str(job.get("planId", "")),
        "recipeId": str(recipe.get("id", "")),
        "areaId": str(recipe.get("areaId", "")),
        "projectRevision": int(job.get("projectRevision", -1)),
        "recipeRevision": job.get("recipeRevision", { "value": -1 }),
        "recipeFingerprint": str(job.get("recipeFingerprint", "")),
        "planFingerprint": str(plan.get("planFingerprint", "")),
        "derivedSceneSha256": str(bake_result.get("sha256", "")),
        "contentFingerprint": str(bake_result.get("contentFingerprint", "")),
        "assetSelectionFingerprint": asset_selection_fingerprint,
        "validationSucceeded": bool(bake_result.get("succeeded", false)) and bake_diagnostics.is_empty(),
        "collisionValidated": int(bake_result.get("collisionShapes", 0)) > 0,
        "navigationValidated": int(bake_result.get("navigationPolygons", 0)) > 0,
        "manualEditsPreserved": bool(regeneration.get("preserveManualEdits", true)),
        "artisticInstances": int(bake_result.get("artisticInstances", 0)),
        "artisticRoles": bake_result.get("artisticRoles", {}),
        "providers": providers,
        "diagnostics": bake_diagnostics,
    }

func _can_create_promotion_candidate(steps: Array, providers: Array) -> bool:
    if steps.size() != CANONICAL_PROMOTION_STAGES.size() or providers.size() != steps.size():
        return false
    var stages := {}
    for step_variant in steps:
        if typeof(step_variant) == TYPE_DICTIONARY:
            stages[str((step_variant as Dictionary).get("stage", "")).to_lower()] = true
    if stages.size() != CANONICAL_PROMOTION_STAGES.size() or CANONICAL_PROMOTION_STAGES.any(
            func(stage: String) -> bool: return not stages.has(stage)):
        return false
    var provider_keys := providers.map(func(item: Variant) -> String:
        return str((item as Dictionary).get("providerKey", "")) if typeof(item) == TYPE_DICTIONARY else "")
    return provider_keys.has("terrain3d") and (
        provider_keys.has("godot-flow") or provider_keys.has("gf-pcgodot"))

func _requires_provider_candidate(steps: Array) -> bool:
    if steps.size() != CANONICAL_PROMOTION_STAGES.size():
        return false
    for step_variant in steps:
        if typeof(step_variant) != TYPE_DICTIONARY:
            continue
        var backend := str((step_variant as Dictionary).get("backend", "")).to_lower()
        if backend == "terrain3d" or backend == "pcggodot":
            return true
    return false

func _create_promotion_candidate(job: Dictionary, backend: Node, providers: Array) -> Dictionary:
    var plan_id := str(job.get("planId", ""))
    var recipe_variant: Variant = job.get("recipe", {})
    if not _is_guid(plan_id) or typeof(recipe_variant) != TYPE_DICTIONARY:
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": [_diagnostic(
                "world_generation.worker.promotion_identity_invalid",
                "Promotion candidate identifiers are invalid.",
                "world-generation/job/promotion")],
        }
    var recipe := recipe_variant as Dictionary
    var compact_plan_id := plan_id.replace("-", "").to_lower()
    var temporary_scene := "res://.openrpg/world-generation/derived/%s.tscn" % compact_plan_id
    var bake_result_variant: Variant = backend.call(
        "bake_forge_generated_world", str(recipe.get("id", "")), temporary_scene)
    if typeof(bake_result_variant) != TYPE_DICTIONARY:
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": [_diagnostic(
                "world_generation.worker.promotion_bake_invalid",
                "Provider-neutral baker returned no structured result.",
                "world-generation/job/promotion")],
        }
    var bake_result := bake_result_variant as Dictionary
    if not bool(bake_result.get("succeeded", false)):
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": bake_result.get("diagnostics", []),
        }
    var source_file := FileAccess.open(temporary_scene, FileAccess.READ)
    if source_file == null:
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": [_diagnostic(
                "world_generation.worker.promotion_scene_missing",
                "Provider-neutral baked scene could not be reopened.",
                "world-generation/job/promotion")],
        }
    var scene_bytes := source_file.get_buffer(source_file.get_length())
    source_file.close()
    var artifact_directory := _workspace_root().path_join(
        "%s/%s" % [ARTIFACTS_RELATIVE_DIRECTORY, compact_plan_id])
    var derived_path := artifact_directory.path_join("derived-world.tscn")
    var report_path := artifact_directory.path_join("validation-report.json")
    if not _write_bytes_atomic(derived_path, scene_bytes):
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": [_diagnostic(
                "world_generation.worker.promotion_scene_write_failed",
                "Provider-neutral derived scene could not be persisted atomically.",
                "world-generation/job/promotion")],
        }
    bake_result["sha256"] = FileAccess.get_sha256(derived_path)
    var report := _build_promotion_validation_report(job, bake_result, providers)
    if report.is_empty() or not _write_json_atomic(report_path, report):
        DirAccess.remove_absolute(derived_path)
        return {
            "succeeded": false,
            "created": false,
            "diagnostics": [_diagnostic(
                "world_generation.worker.promotion_report_write_failed",
                "Canonical-promotion validation report could not be persisted atomically.",
                "world-generation/job/promotion")],
        }
    DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary_scene))
    return {
        "succeeded": true,
        "created": true,
        "derivedScene": derived_path,
        "validationReport": report_path,
        "contentFingerprint": str(bake_result.get("contentFingerprint", "")),
        "sha256": str(bake_result.get("sha256", "")),
        "diagnostics": [],
    }

func _provider_evidence_for_step(step: Dictionary, result: Dictionary) -> Dictionary:
    var requested_backend := str(step.get("backend", ""))
    var requested_key := requested_backend.to_lower()
    if bool(result.get("fallbackUsed", false)) or requested_key == "godotnative":
        var native_path := BACKEND_SCRIPT_PATH if FileAccess.file_exists(BACKEND_SCRIPT_PATH) else \
            SOURCE_BACKEND_SCRIPT_PATH
        var native_sha := FileAccess.get_sha256(native_path)
        if not _is_lower_sha256(native_sha):
            return {}
        return {
            "stage": str(step.get("stage", "")),
            "requestedBackend": requested_backend,
            "providerKey": "openrpg-native",
            "providerVersion": FORMAT_VERSION,
            "evidenceSha256": native_sha,
        }

    var receipt := _provider_receipt()
    if receipt.is_empty() or int(receipt.get("schemaVersion", 0)) != 1:
        return {}
    var backend_key := "pcgodot" if requested_key == "pcggodot" else requested_key
    var providers_variant: Variant = receipt.get("providers", [])
    if typeof(providers_variant) != TYPE_ARRAY:
        return {}
    for provider_variant in providers_variant as Array:
        if typeof(provider_variant) != TYPE_DICTIONARY:
            continue
        var provider := provider_variant as Dictionary
        if str(provider.get("backend", "")).to_lower() != backend_key:
            continue
        var evidence_variant: Variant = provider.get("evidence", [])
        if typeof(evidence_variant) != TYPE_ARRAY or (evidence_variant as Array).is_empty():
            return {}
        var marker_variant: Variant = (evidence_variant as Array)[0]
        if typeof(marker_variant) != TYPE_DICTIONARY:
            return {}
        var marker := marker_variant as Dictionary
        var relative_path := str(marker.get("relativePath", ""))
        var expected_sha := str(marker.get("sha256", ""))
        var marker_path := "res://%s" % relative_path
        if relative_path.is_empty() or relative_path.contains("..") or \
                not FileAccess.file_exists(marker_path):
            return {}
        var actual_sha := FileAccess.get_sha256(marker_path)
        if not _is_lower_sha256(expected_sha) or actual_sha != expected_sha:
            return {}
        return {
            "stage": str(step.get("stage", "")),
            "requestedBackend": requested_backend,
            "providerKey": str(provider.get("providerKey", "")),
            "providerVersion": str(provider.get("versionOrRevision", "")),
            "evidenceSha256": actual_sha,
        }
    return {}

func _provider_receipt() -> Dictionary:
    var project_receipt := "res://%s" % PROVIDER_RECEIPT_RELATIVE_PATH
    if FileAccess.file_exists(project_receipt):
        return _read_json_object(ProjectSettings.globalize_path(project_receipt))
    return _read_json_object(_workspace_root().path_join(PROVIDER_RECEIPT_RELATIVE_PATH))

func _canonical_json(value: Variant) -> String:
    match typeof(value):
        TYPE_DICTIONARY:
            var dictionary := value as Dictionary
            var keys := PackedStringArray()
            for key: Variant in dictionary.keys():
                keys.append(str(key))
            keys.sort()
            var fields := PackedStringArray()
            for key in keys:
                fields.append("%s:%s" % [JSON.stringify(key), _canonical_json(dictionary.get(key))])
            return "{%s}" % ",".join(fields)
        TYPE_ARRAY:
            var entries := PackedStringArray()
            for entry: Variant in value as Array:
                entries.append(_canonical_json(entry))
            return "[%s]" % ",".join(entries)
        _:
            return JSON.stringify(value)

func _jobs_directory() -> String:
    return _workspace_root().path_join(JOBS_RELATIVE_DIRECTORY)

func _workspace_root() -> String:
    var environment_root := OS.get_environment(WORKSPACE_ENVIRONMENT).strip_edges()
    if not environment_root.is_empty():
        return environment_root
    var configured := str(ProjectSettings.get_setting(WORKSPACE_SETTING, "res://"))
    return ProjectSettings.globalize_path(configured) \
        if configured.begins_with("res://") or configured.begins_with("user://") else configured

func _read_json_object(path: String) -> Dictionary:
    if not FileAccess.file_exists(path):
        return {}
    var file := FileAccess.open(path, FileAccess.READ)
    if file == null:
        return {}
    var parsed: Variant = JSON.parse_string(file.get_as_text())
    return parsed as Dictionary if typeof(parsed) == TYPE_DICTIONARY else {}

func _job_status(path: String) -> String:
    var wrapper := _read_json_object(path)
    var job_variant: Variant = wrapper.get("job", {})
    return str((job_variant as Dictionary).get("status", "")) if typeof(job_variant) == TYPE_DICTIONARY else ""

func _write_json_atomic(path: String, value: Dictionary) -> bool:
    var directory_error := DirAccess.make_dir_recursive_absolute(path.get_base_dir())
    if directory_error != OK:
        return false
    var temporary_path := path + ".tmp"
    var backup_path := path + ".bak"
    var temporary := FileAccess.open(temporary_path, FileAccess.WRITE)
    if temporary == null:
        return false
    # Godot parses every JSON number as a float. Normalize mathematically integral
    # values before persistence so .NET int/long contracts can reopen worker receipts.
    temporary.store_string(JSON.stringify(_normalize_integral_json_numbers(value), "  "))
    temporary.flush()
    temporary.close()
    if FileAccess.file_exists(backup_path):
        DirAccess.remove_absolute(backup_path)
    if FileAccess.file_exists(path) and DirAccess.rename_absolute(path, backup_path) != OK:
        DirAccess.remove_absolute(temporary_path)
        return false
    if DirAccess.rename_absolute(temporary_path, path) != OK:
        if FileAccess.file_exists(backup_path):
            DirAccess.rename_absolute(backup_path, path)
        return false
    if FileAccess.file_exists(backup_path):
        DirAccess.remove_absolute(backup_path)
    return true

func _normalize_integral_json_numbers(value: Variant) -> Variant:
    match typeof(value):
        TYPE_FLOAT:
            var number := float(value)
            return int(number) if is_finite(number) and number == floorf(number) else number
        TYPE_ARRAY:
            var normalized_array: Array = []
            for item in value as Array:
                normalized_array.append(_normalize_integral_json_numbers(item))
            return normalized_array
        TYPE_DICTIONARY:
            var normalized_dictionary := {}
            for key in (value as Dictionary).keys():
                normalized_dictionary[key] = _normalize_integral_json_numbers(
                    (value as Dictionary).get(key))
            return normalized_dictionary
        _:
            return value

func _write_bytes_atomic(path: String, content: PackedByteArray) -> bool:
    var directory_error := DirAccess.make_dir_recursive_absolute(path.get_base_dir())
    if directory_error != OK:
        return false
    var temporary_path := path + ".tmp"
    var backup_path := path + ".bak"
    var temporary := FileAccess.open(temporary_path, FileAccess.WRITE)
    if temporary == null:
        return false
    temporary.store_buffer(content)
    temporary.flush()
    temporary.close()
    if FileAccess.file_exists(backup_path):
        DirAccess.remove_absolute(backup_path)
    if FileAccess.file_exists(path) and DirAccess.rename_absolute(path, backup_path) != OK:
        DirAccess.remove_absolute(temporary_path)
        return false
    if DirAccess.rename_absolute(temporary_path, path) != OK:
        if FileAccess.file_exists(backup_path):
            DirAccess.rename_absolute(backup_path, path)
        return false
    if FileAccess.file_exists(backup_path):
        DirAccess.remove_absolute(backup_path)
    return true

func _revision_value(value: Variant) -> int:
    if typeof(value) == TYPE_DICTIONARY:
        return int((value as Dictionary).get("value", 0))
    return int(value)

func _append_audit(job: Dictionary, action: String, succeeded: bool, detail: String) -> void:
    var audit_variant: Variant = job.get("audit", [])
    var audit: Array = audit_variant as Array if typeof(audit_variant) == TYPE_ARRAY else []
    audit.append({
        "action": action,
        "occurredAtUtc": _utc_now(),
        "succeeded": succeeded,
        "detail": detail.left(4096),
    })
    job["audit"] = audit

func _diagnostics_detail(diagnostics: Array) -> String:
    var parts := PackedStringArray()
    for item_variant in diagnostics:
        if typeof(item_variant) == TYPE_DICTIONARY:
            var item := item_variant as Dictionary
            parts.append("%s: %s" % [str(item.get("code", "unknown")), str(item.get("message", ""))])
    return " | ".join(parts)

func _is_lower_sha256(value: String) -> bool:
    if value.length() != 64:
        return false
    for character in value:
        if "0123456789abcdef".find(str(character)) < 0:
            return false
    return true

func _is_guid(value: String) -> bool:
    if value.length() != 36 or value[8] != "-" or value[13] != "-" or value[18] != "-" or value[23] != "-":
        return false
    return _is_lower_sha256(value.replace("-", "") + "00000000000000000000000000000000")

func _diagnostic(code: String, message: String, path: String) -> Dictionary:
    return { "code": code, "message": message, "path": path }

func _utc_now() -> String:
    return Time.get_datetime_string_from_system(true, false) + "Z"
