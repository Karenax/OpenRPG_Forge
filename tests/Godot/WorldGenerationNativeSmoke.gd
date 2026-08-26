extends SceneTree

const BackendScript := preload("res://integrations/godot/addons/openrpg_world_generation/openrpg_world_generation_backend.gd")
const RunnerScript := preload("res://integrations/godot/addons/openrpg_world_generation/world_generation_job_runner.gd")

func _initialize() -> void:
    var scene := Node3D.new()
    scene.name = "WorldGenerationSmoke"
    root.add_child(scene)
    current_scene = scene
    var backend: Node = BackendScript.new()
    backend.name = "OpenRPGWorldGenerationBackend"
    root.add_child(backend)
    var runner: Node = RunnerScript.new()
    root.add_child(runner)
    await process_frame

    var job := _job_protocol_vector()
    var job_validation: Dictionary = runner.call("_validate_job", job)
    if not bool(job_validation.get("succeeded", false)):
        _fail("Godot rejected the shared C#/GDScript fingerprint vector: %s" % job_validation)
        return
    var tampered_job := job.duplicate(true)
    tampered_job.plan.steps[0].operation = "os.execute"
    var tampered_validation: Dictionary = runner.call("_validate_job", tampered_job)
    if bool(tampered_validation.get("succeeded", true)):
        _fail("Godot accepted a tampered execution job.")
        return
    var resumed_job := job.duplicate(true)
    resumed_job.status = "running"
    resumed_job.audit.append({
        "action": "running",
        "occurredAtUtc": "2026-08-26T00:00:01Z",
        "succeeded": true,
        "detail": "Interrupted worker",
    })
    var resume_path := "user://openrpg-world-generation-resume-smoke.json"
    var resume_file := FileAccess.open(resume_path, FileAccess.WRITE)
    if resume_file == null:
        _fail("Could not create the durable resume smoke fixture.")
        return
    resume_file.store_string(JSON.stringify({ "formatVersion": "1.0.0", "job": resumed_job }, "  "))
    resume_file.close()
    var resume_outcome: Dictionary = runner.call("_process_job_file", ProjectSettings.globalize_path(resume_path))
    var resumed_wrapper: Dictionary = runner.call("_read_json_object", ProjectSettings.globalize_path(resume_path))
    DirAccess.remove_absolute(ProjectSettings.globalize_path(resume_path))
    var resumed_persisted: Dictionary = resumed_wrapper.get("job", {})
    if not bool(resume_outcome.get("succeeded", false)) or \
            str(resumed_persisted.get("status", "")) != "succeeded" or \
            not (resumed_persisted.get("audit", []) as Array).any(
                func(entry: Variant) -> bool: return typeof(entry) == TYPE_DICTIONARY and str((entry as Dictionary).get("action", "")) == "resumed"):
        _fail("A durable running job did not resume to completion: %s" % resume_outcome)
        return

    var native_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("godotNative", "godotnative.terrain")))
    if not bool(native_result.get("succeeded", false)):
        _fail("Native terrain generation failed: %s" % native_result)
        return
    var generated := root.find_child("Native_Terrain", true, false)
    if generated == null or generated.get_node_or_null("Heightfield") == null or \
            generated.get_node_or_null("Collision/Shape") == null:
        _fail("Native terrain did not create mesh and collision artifacts: %s" % native_result)
        return
    generated.get_node("Heightfield").set_meta("openrpg_manual_lock", true)
    var preserved_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("godotNative", "godotnative.terrain")))
    if not bool(preserved_result.get("succeeded", false)) or \
            not bool(preserved_result.get("preservedManualEdits", false)):
        _fail("A descendant manual lock was not preserved: %s" % preserved_result)
        return
    var validation_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("godotNative", "godot.validate_generated_world", "validation")))
    if not bool(validation_result.get("succeeded", false)):
        _fail("Native generated-world validation failed: %s" % validation_result)
        return

    var fallback_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("terrain3D", "terrain3d.generate_heightfield")))
    if not bool(fallback_result.get("succeeded", false)) or not bool(fallback_result.get("fallbackUsed", false)):
        _fail("Terrain3D absence did not activate the native fallback: %s" % fallback_result)
        return
    var pcg_fallback_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("pcgGodot", "pcgodot.generate_biomes", "biomes")))
    if not bool(pcg_fallback_result.get("succeeded", false)) or \
            not bool(pcg_fallback_result.get("fallbackUsed", false)) or \
            root.find_child("Native_Biomes", true, false) == null:
        _fail("Godot Flow absence did not activate the native fallback: %s" % pcg_fallback_result)
        return

    var rejected: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("godotNative", "os.execute")))
    if bool(rejected.get("succeeded", true)):
        _fail("A non-allowlisted backend operation was accepted.")
        return

    print("WORLD_GENERATION_GODOT_SMOKE_PASS native_mesh=1 collision=1 terrain3d_fallback=1 pcg_fallback=1 manual_lock=1 protocol_vector=1 resume=1 validation=1 tamper_rejected=1")
    quit(0)

func _request(backend: String, operation: String, stage: String = "terrain") -> Dictionary:
    var recipe := {
        "id": "f0540000-0000-4000-8000-000000000001",
        "areaId": "f0540000-0000-4000-8000-000000000002",
        "seed": 20260826,
        "bounds": {
            "widthMeters": 256.0,
            "depthMeters": 256.0,
            "maxElevationMeters": 48.0,
        },
        "terrain": {
            "archetype": "temperate-valley",
            "roughness": 0.45,
        },
        "rivers": [],
        "regeneration": {
            "preserveManualEdits": true,
            "protectedNodePaths": [],
        },
    }
    return {
        "protocolVersion": "1.0.0",
        "planId": "f0540000-0000-4000-8000-000000000003",
        "transactionId": "f0540000-0000-4000-8000-000000000004",
        "projectRevision": 1,
        "recipeRevision": 1,
        "recipeId": recipe.id,
        "areaId": recipe.areaId,
        "recipeFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "planFingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "seed": recipe.seed,
        "dryRun": false,
        "step": {
            "order": 0,
            "stage": stage,
            "backend": backend,
            "operation": operation,
            "parameters": {},
            "dependsOn": [],
        },
        "recipe": recipe,
    }

func _job_protocol_vector() -> Dictionary:
    var recipe := {
        "id": "f0540000-0000-4000-8000-000000000001",
        "schemaVersion": 1,
        "areaId": "f0540000-0000-4000-8000-000000000002",
        "title": "Protocol vector",
        "seed": 20260826,
        "provenance": "test:godot-smoke",
        "bounds": {
            "widthMeters": 256.0,
            "depthMeters": 256.0,
            "maxElevationMeters": 48.0,
        },
        "terrain": {
            "archetype": "temperate-valley",
            "roughness": 0.45,
            "erosionPasses": 2,
        },
        "rivers": [],
        "biomes": [],
        "roads": [],
        "settlements": [],
        "scatter": [],
        "regeneration": {
            "preserveManualEdits": true,
            "protectedNodePaths": [],
        },
        "backends": {
            "terrain": "godotNative",
        },
    }
    var plan := {
        "planId": "f0540000-0000-4000-8000-000000000003",
        "transactionId": "f0540000-0000-4000-8000-000000000004",
        "projectRevision": 3,
        "recipeRevision": { "value": 2 },
        "recipeId": recipe.id,
        "areaId": recipe.areaId,
        "seed": recipe.seed,
        "recipeFingerprint": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "planFingerprint": "b32a985db8eb0dadb5befde7dce8191753e4a73853cd992696ce21c8dd37186b",
        "steps": [{
            "order": 0,
            "stage": "terrain",
            "backend": "godotNative",
            "operation": "godotnative.terrain",
            "parameters": {},
            "dependsOn": [],
        }],
        "diagnostics": [],
        "readyToApply": true,
    }
    return {
        "schemaVersion": 1,
        "jobId": "f0540000-0000-4000-8000-000000000005",
        "planId": plan.planId,
        "projectRevision": plan.projectRevision,
        "recipeRevision": plan.recipeRevision,
        "recipeFingerprint": plan.recipeFingerprint,
        "status": "queued",
        "plan": plan,
        "recipe": recipe,
        "diagnostics": [],
        "audit": [{
            "action": "queued",
            "occurredAtUtc": "2026-08-26T00:00:00Z",
            "succeeded": true,
            "detail": "Protocol vector",
        }],
    }

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
