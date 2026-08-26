@tool
extends Node

const FORMAT_VERSION := "1.0.0"
const PROTOCOL_VERSION := "1.0.0"
const WORKSPACE_SETTING := "openrpg/world_generation/workspace_root"
const AUTO_PROCESS_SETTING := "openrpg/world_generation/auto_process_jobs"
const POLL_SECONDS_SETTING := "openrpg/world_generation/poll_seconds"
const JOBS_RELATIVE_DIRECTORY := "world/generation/jobs"

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
        var outcome := _process_job_file(directory_path.path_join(queued_file))
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
    if str(job.get("status", "")).to_lower() != "queued":
        return { "succeeded": true, "processed": false }

    var validation := _validate_job(job)
    if not bool(validation.get("succeeded", false)):
        _finish_job(wrapper, job, "failed", validation.get("diagnostics", []), path)
        return { "succeeded": false, "processed": true }

    job["status"] = "running"
    job["updatedAtUtc"] = _utc_now()
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
    var steps: Array = plan.get("steps", [])
    steps.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
        return int(left.get("order", 0)) < int(right.get("order", 0)))
    for step_variant in steps:
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

    var succeeded := diagnostics.is_empty()
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
    if not bool(plan.get("readyToApply", false)):
        diagnostics.append(_diagnostic("world_generation.worker.plan_not_ready", "Execution plan is not approved for application.", "world-generation/job/plan"))
    if str(job.get("recipeFingerprint", "")) != str(plan.get("recipeFingerprint", "")):
        diagnostics.append(_diagnostic("world_generation.worker.fingerprint_mismatch", "Job and plan fingerprints differ.", "world-generation/job/recipeFingerprint"))
    if str(plan.get("recipeId", "")) != str(recipe.get("id", "")):
        diagnostics.append(_diagnostic("world_generation.worker.recipe_mismatch", "Plan and recipe IDs differ.", "world-generation/job/recipe"))
    if str(plan.get("areaId", "")) != str(recipe.get("areaId", "")):
        diagnostics.append(_diagnostic("world_generation.worker.area_mismatch", "Plan and recipe area IDs differ.", "world-generation/job/recipe"))
    return { "succeeded": diagnostics.is_empty(), "diagnostics": diagnostics }

func _finish_job(wrapper: Dictionary, job: Dictionary, status: String, diagnostics: Array, path: String) -> void:
    job["status"] = status
    job["updatedAtUtc"] = _utc_now()
    job["diagnostics"] = diagnostics
    wrapper["job"] = job
    _write_json_atomic(path, wrapper)

func _jobs_directory() -> String:
    var configured := str(ProjectSettings.get_setting(WORKSPACE_SETTING, "res://"))
    var root := ProjectSettings.globalize_path(configured) if configured.begins_with("res://") or configured.begins_with("user://") else configured
    return root.path_join(JOBS_RELATIVE_DIRECTORY)

func _read_json_object(path: String) -> Dictionary:
    if not FileAccess.file_exists(path):
        return {}
    var file := FileAccess.open(path, FileAccess.READ)
    if file == null:
        return {}
    var parsed: Variant = JSON.parse_string(file.get_as_text())
    return parsed as Dictionary if typeof(parsed) == TYPE_DICTIONARY else {}

func _write_json_atomic(path: String, value: Dictionary) -> bool:
    var temporary_path := path + ".tmp"
    var backup_path := path + ".bak"
    var temporary := FileAccess.open(temporary_path, FileAccess.WRITE)
    if temporary == null:
        return false
    temporary.store_string(JSON.stringify(value, "  "))
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

func _diagnostic(code: String, message: String, path: String) -> Dictionary:
    return { "code": code, "message": message, "path": path }

func _utc_now() -> String:
    return Time.get_datetime_string_from_system(true, false) + "Z"
