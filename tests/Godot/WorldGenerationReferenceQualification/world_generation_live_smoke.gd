extends SceneTree

const BackendScript := preload("res://addons/openrpg_world_generation/openrpg_world_generation_backend.gd")
const RunnerScript := preload("res://addons/openrpg_world_generation/world_generation_job_runner.gd")
const RUNS := 3
const TOTAL_EXPECTED_INSTANCES := 5904
const STAGES := ["biomes", "roads", "settlements", "scatter"]
const EXPECTED_INSTANCES := {
    "biomes": 1600,
    "roads": 183,
    "settlements": 25,
    "scatter": 4096,
}
const OPERATIONS := {
    "biomes": "pcgodot.generate_biomes",
    "roads": "pcgodot.generate_road_network",
    "settlements": "pcgodot.generate_settlements",
    "scatter": "pcgodot.scatter_environment",
}
const NATIVE_OPERATIONS := {
    "gameplay": "godot.generate_gameplay_anchors",
    "navigation": "godot.bake_navigation",
    "validation": "godot.validate_generated_world",
}
const PLAN_STAGE_ORDER := {
    "terrain": 0,
    "hydrology": 1,
    "biomes": 2,
    "roads": 3,
    "settlements": 4,
    "scatter": 5,
    "gameplay": 6,
    "navigation": 7,
    "validation": 8,
}

func _initialize() -> void:
    if not Engine.is_editor_hint():
        _fail("The 2 km graph qualification must run in editor mode before neutral baking.")
        return
    var provider_ready := false
    for _frame_index in 600:
        await process_frame
        if EditorInterface.is_plugin_enabled("flow_nodes_editor") and \
                ResourceLoader.exists("res://addons/flow_nodes_editor/flow_node.gd"):
            provider_ready = true
            break
    if not provider_ready:
        _fail("The Graph PCG editor plugin did not become ready within 600 frames.")
        return

    var scene := Node3D.new()
    scene.name = "WorldGenerationReference2Km"
    root.add_child(scene)
    current_scene = scene
    var backend: Node = BackendScript.new()
    backend.name = "OpenRPGWorldGenerationBackend"
    scene.add_child(backend)
    await process_frame

    var manual_lock_proof := await _prove_manual_lock(backend, scene)
    if not bool(manual_lock_proof.get("succeeded", false)):
        _fail("Manual-lock regeneration proof failed: %s" % [manual_lock_proof])
        return

    var terrain_fingerprints: Array[String] = []
    var graph_fingerprints: Array[String] = []
    var scene_content_fingerprints: Array[String] = []
    var final_bake: Dictionary = {}
    for run_index in RUNS:
        var terrain_result := await _execute_terrain(backend, scene)
        if not bool(terrain_result.get("succeeded", false)):
            _fail("Terrain3D reference run %d failed: %s" % [run_index, terrain_result])
            return
        var terrain_data: Object = terrain_result.data
        terrain_fingerprints.append(_terrain_fingerprint(terrain_data))

        var stage_fingerprints := {}
        for stage in STAGES:
            var stage_result := await _execute_graph_stage(backend, scene, terrain_data, stage)
            if not bool(stage_result.get("succeeded", false)):
                _fail("Graph stage '%s' run %d failed: %s" % [stage, run_index, stage_result])
                return
            stage_fingerprints[stage] = str(stage_result.fingerprint)
        graph_fingerprints.append(_combined_stage_fingerprint(stage_fingerprints))
        for native_stage in NATIVE_OPERATIONS:
            var native_result := _execute_native_stage(backend, native_stage)
            if not bool(native_result.get("succeeded", false)):
                _fail("Native stage '%s' run %d failed: %s" % [native_stage, run_index, native_result])
                return

        var output_path := "res://qualification-output/reference_run_%d.tscn" % run_index
        var bake_result: Dictionary = backend.call(
            "bake_forge_generated_world",
            "f0540000-0000-4000-8000-000000000201",
            output_path)
        var bake_validation := _validate_bake(bake_result)
        if not bake_validation.is_empty():
            _fail("Neutral 2 km bake %d failed validation: %s result=%s" % [
                run_index, bake_validation, bake_result])
            return
        scene_content_fingerprints.append(str(bake_result.contentFingerprint))
        final_bake = bake_result

    if terrain_fingerprints.any(func(value: String) -> bool: return value != terrain_fingerprints[0]):
        _fail("Terrain3D produced non-deterministic 2 km fingerprints: %s" % [terrain_fingerprints])
        return
    if graph_fingerprints.any(func(value: String) -> bool: return value != graph_fingerprints[0]):
        _fail("Graph PCG produced non-deterministic layered fingerprints: %s" % [graph_fingerprints])
        return
    if scene_content_fingerprints.any(
            func(value: String) -> bool: return value != scene_content_fingerprints[0]):
        _fail("Neutral 2 km scene content was non-deterministic: %s" % [scene_content_fingerprints])
        return

    var partial_proof := await _prove_partial_regeneration(backend, scene)
    if not bool(partial_proof.get("succeeded", false)):
        _fail("Partial-regeneration proof failed: %s" % [partial_proof])
        return
    var final_result: Dictionary = backend.call(
        "bake_forge_generated_world",
        "f0540000-0000-4000-8000-000000000201",
        "res://qualification-output/generated_world.tscn")
    var final_validation := _validate_bake(final_result)
    if not final_validation.is_empty() or \
            str(final_result.get("contentFingerprint", "")) != scene_content_fingerprints[0]:
        _fail("Final partial-regeneration bake changed canonical content: validation=%s result=%s" % [
            final_validation, final_result])
        return
    final_bake = final_result

    var terrain_aabb_size: Array = final_bake.get("terrainAabbSize", [])
    var promotion_candidate := _create_promotion_candidate(backend)
    if not bool(promotion_candidate.get("succeeded", false)) or \
            str(promotion_candidate.get("contentFingerprint", "")) != scene_content_fingerprints[0]:
        _fail("Canonical 2 km promotion candidate generation failed: %s" % [promotion_candidate])
        return
    current_scene = null
    scene.free()
    await process_frame
    for plugin_name in ["openrpg_world_generation", "flow_nodes_editor", "terrain_3d"]:
        if EditorInterface.is_plugin_enabled(plugin_name):
            EditorInterface.set_plugin_enabled(plugin_name, false)
    for _cleanup_frame in 3:
        await process_frame
    print("WORLD_GENERATION_LIVE_SMOKE_PASS runs=3 terrain3d_regions=1 pcg_instances=%d stages=4 fallback=0 collision=1 navigation_polygons=%d terrain_width=%.3f terrain_depth=%.3f manual_lock=1 partial_regeneration=1 promotion_candidate=1 baked_sha256=%s terrain_fingerprint=%s graph_fingerprint=%s content_fingerprint=%s" % [
        TOTAL_EXPECTED_INSTANCES,
        int(final_bake.get("navigationPolygons", 0)),
        float(terrain_aabb_size[0]),
        float(terrain_aabb_size[2]),
        str(final_bake.get("sha256", "")),
        terrain_fingerprints[0],
        graph_fingerprints[0],
        scene_content_fingerprints[0]])
    quit(0)

func _create_promotion_candidate(backend: Node) -> Dictionary:
    var request := _request("terrain3D", "terrain3d.generate_heightfield", "terrain")
    var steps: Array = [
        { "order": 0, "stage": "terrain", "backend": "terrain3D" },
        { "order": 1, "stage": "hydrology", "backend": "terrain3D" },
    ]
    for stage in STAGES:
        steps.append({
            "order": int(PLAN_STAGE_ORDER[stage]),
            "stage": stage,
            "backend": "pcgGodot",
        })
    for native_stage in NATIVE_OPERATIONS:
        steps.append({
            "order": int(PLAN_STAGE_ORDER[native_stage]),
            "stage": native_stage,
            "backend": "godotNative",
        })
    var runner: Node = RunnerScript.new()
    var providers: Array = []
    for step in steps:
        var evidence: Dictionary = runner.call(
            "_provider_evidence_for_step", step, { "succeeded": true })
        if evidence.is_empty():
            runner.free()
            return { "succeeded": false, "phase": "provider-evidence", "step": step }
        providers.append(evidence)
    var job := {
        "jobId": "f0540000-0000-4000-8000-000000000205",
        "planId": request.planId,
        "projectRevision": request.projectRevision,
        "recipeRevision": { "value": request.recipeRevision },
        "recipeFingerprint": request.recipeFingerprint,
        "plan": {
            "planFingerprint": request.planFingerprint,
            "steps": steps,
        },
        "recipe": request.recipe,
    }
    var candidate: Dictionary = runner.call(
        "_create_promotion_candidate", job, backend, providers)
    runner.free()
    if not bool(candidate.get("succeeded", false)):
        return candidate
    var report_variant: Variant = JSON.parse_string(
        FileAccess.get_file_as_string(str(candidate.get("validationReport", ""))))
    if typeof(report_variant) != TYPE_DICTIONARY:
        return { "succeeded": false, "phase": "report-read", "candidate": candidate }
    var report := report_variant as Dictionary
    if str(report.get("derivedSceneSha256", "")) != str(candidate.get("sha256", "")) or \
            str(report.get("contentFingerprint", "")) != str(candidate.get("contentFingerprint", "")) or \
            (report.get("providers", []) as Array).size() != steps.size():
        return { "succeeded": false, "phase": "report-validation", "report": report }
    return candidate

func _prove_manual_lock(backend: Node, scene: Node3D) -> Dictionary:
    var terrain_result := await _execute_terrain(backend, scene)
    if not bool(terrain_result.get("succeeded", false)):
        return { "succeeded": false, "phase": "terrain", "result": terrain_result }
    var terrain_data: Object = terrain_result.data
    for stage in STAGES:
        var stage_result := await _execute_graph_stage(backend, scene, terrain_data, stage)
        if not bool(stage_result.get("succeeded", false)):
            return { "succeeded": false, "phase": stage, "result": stage_result }

    var roads := scene.find_child("PCG_Roads", true, false) as Node3D
    var roads_multimesh := _find_multimesh(roads)
    if roads == null or roads_multimesh == null or roads_multimesh.multimesh == null:
        return { "succeeded": false, "phase": "roads-missing" }
    var edited_transform := roads_multimesh.multimesh.get_instance_transform(0)
    edited_transform.origin.y += 7.0
    roads_multimesh.multimesh.set_instance_transform(0, edited_transform)
    roads.set_meta("openrpg_manual_lock", true)
    var edited_fingerprint := _multimesh_fingerprint(roads_multimesh.multimesh)
    var roads_instance_id := roads.get_instance_id()

    var preserved: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("pcgGodot", OPERATIONS.roads, "roads")))
    if not bool(preserved.get("succeeded", false)) or \
            not bool(preserved.get("preservedManualEdits", false)):
        return { "succeeded": false, "phase": "preserve-result", "result": preserved }
    var preserved_roads := scene.find_child("PCG_Roads", true, false) as Node3D
    var preserved_multimesh := _find_multimesh(preserved_roads)
    if preserved_roads == null or preserved_roads.get_instance_id() != roads_instance_id or \
            preserved_multimesh == null or \
            _multimesh_fingerprint(preserved_multimesh.multimesh) != edited_fingerprint:
        return { "succeeded": false, "phase": "preserve-identity" }

    preserved_roads.remove_meta("openrpg_manual_lock")
    var restored := await _execute_graph_stage(backend, scene, terrain_data, "roads")
    if not bool(restored.get("succeeded", false)) or str(restored.fingerprint) == edited_fingerprint:
        return { "succeeded": false, "phase": "unlock-regenerate", "result": restored }
    return { "succeeded": true }

func _prove_partial_regeneration(backend: Node, scene: Node3D) -> Dictionary:
    var before := {}
    for stage in STAGES:
        var executor := scene.find_child("PCG_%s" % stage.capitalize(), true, false) as Node3D
        var multimesh := _find_multimesh(executor)
        if executor == null or multimesh == null or multimesh.multimesh == null:
            return { "succeeded": false, "phase": "snapshot", "stage": stage }
        before[stage] = {
            "instanceId": executor.get_instance_id(),
            "fingerprint": _multimesh_fingerprint(multimesh.multimesh),
        }

    var terrain := scene.find_child("Terrain3D", true, false)
    var terrain_data: Object = terrain.get("data") if terrain != null else null
    if terrain_data == null:
        return { "succeeded": false, "phase": "terrain-data" }
    var scatter_result := await _execute_graph_stage(backend, scene, terrain_data, "scatter")
    if not bool(scatter_result.get("succeeded", false)):
        return { "succeeded": false, "phase": "scatter", "result": scatter_result }

    for stage in STAGES:
        var executor := scene.find_child("PCG_%s" % stage.capitalize(), true, false) as Node3D
        var multimesh := _find_multimesh(executor)
        if executor == null or multimesh == null:
            return { "succeeded": false, "phase": "after", "stage": stage }
        var same_identity := executor.get_instance_id() == int(before[stage].instanceId)
        var same_fingerprint := _multimesh_fingerprint(multimesh.multimesh) == str(before[stage].fingerprint)
        if stage == "scatter":
            if same_identity or not same_fingerprint:
                return { "succeeded": false, "phase": "scatter-scope" }
        elif not same_identity or not same_fingerprint:
            return { "succeeded": false, "phase": "unrelated-stage-changed", "stage": stage }
    return { "succeeded": true }

func _execute_terrain(backend: Node, scene: Node3D) -> Dictionary:
    var result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("terrain3D", "terrain3d.generate_heightfield", "terrain")))
    if not bool(result.get("succeeded", false)) or bool(result.get("fallbackUsed", false)):
        return { "succeeded": false, "bridge": result }
    var hydrology_result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("terrain3D", "terrain3d.carve_hydrology", "hydrology")))
    if not bool(hydrology_result.get("succeeded", false)) or \
            bool(hydrology_result.get("fallbackUsed", false)):
        return { "succeeded": false, "hydrology": hydrology_result }
    for _physics_index in 4:
        await physics_frame
        await process_frame
    var terrain := scene.find_child("Terrain3D", true, false)
    if terrain == null or terrain.get("data") == null:
        return { "succeeded": false, "reason": "terrain-data-missing" }
    var data: Object = terrain.get("data")
    if not data.has_method("get_region_count") or int(data.call("get_region_count")) != 1:
        return { "succeeded": false, "reason": "terrain-region-count", "count": data.call("get_region_count") }
    return { "succeeded": true, "data": data, "bridge": result }

func _execute_native_stage(backend: Node, stage: String) -> Dictionary:
    var result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("godotNative", str(NATIVE_OPERATIONS[stage]), stage)))
    if not bool(result.get("succeeded", false)) or bool(result.get("fallbackUsed", false)):
        return { "succeeded": false, "bridge": result }
    return { "succeeded": true, "bridge": result }

func _execute_graph_stage(
        backend: Node,
        scene: Node3D,
        terrain_data: Object,
        stage: String) -> Dictionary:
    var result: Dictionary = backend.call(
        "execute_forge_world_generation_step",
        JSON.stringify(_request("pcgGodot", str(OPERATIONS[stage]), stage)))
    if not bool(result.get("succeeded", false)) or bool(result.get("fallbackUsed", false)):
        return { "succeeded": false, "bridge": result }
    for _projection_frame in 3:
        await process_frame
    var executor := scene.find_child("PCG_%s" % stage.capitalize(), true, false) as Node3D
    var multimesh := _find_multimesh(executor)
    var expected := int(EXPECTED_INSTANCES[stage])
    if executor == null or multimesh == null or multimesh.multimesh == null or \
            multimesh.multimesh.instance_count != expected:
        return { "succeeded": false, "reason": "instance-count", "expected": expected }
    var unique_positions := _unique_horizontal_position_count(multimesh.multimesh)
    var aligned_positions := _surface_alignment_count(multimesh.multimesh, terrain_data)
    if unique_positions != expected or aligned_positions != expected:
        return {
            "succeeded": false,
            "reason": "placement",
            "expected": expected,
            "unique": unique_positions,
            "aligned": aligned_positions,
            "bridge": result,
        }
    return {
        "succeeded": true,
        "executor": executor,
        "fingerprint": _multimesh_fingerprint(multimesh.multimesh),
    }

func _validate_bake(result: Dictionary) -> String:
    if not bool(result.get("succeeded", false)):
        return "bake-failed"
    if int(result.get("collisionShapes", 0)) != 1:
        return "collision-count"
    if int(result.get("navigationPolygons", 0)) < 1:
        return "navigation-empty"
    if int(result.get("generatedInstances", 0)) != TOTAL_EXPECTED_INSTANCES:
        return "instance-count"
    if str(result.get("contentFingerprint", "")).length() != 64:
        return "content-fingerprint"
    var placement_surface: Dictionary = result.get("terrainSurface", {})
    var environment_occupancy: Dictionary = result.get("environmentOccupancy", {})
    if int(placement_surface.get("schemaVersion", 0)) != 1 or \
            int(placement_surface.get("width", 0)) != 2048 or \
            int(placement_surface.get("depth", 0)) != 2048 or \
            (placement_surface.get("heights", []) as Array).is_empty() or \
            str(placement_surface.get("fingerprint", "")).length() != 64:
        return "terrain-placement-evidence"
    if int(environment_occupancy.get("schemaVersion", 0)) != 1 or \
            (environment_occupancy.get("cells", []) as Array).is_empty() or \
            str(environment_occupancy.get("fingerprint", "")).length() != 64:
        return "environment-occupancy-evidence"
    var terrain_size: Array = result.get("terrainAabbSize", [])
    if terrain_size.size() != 3 or absf(float(terrain_size[0]) - 2048.0) > 1.0 or \
            absf(float(terrain_size[2]) - 2048.0) > 1.0:
        return "terrain-extent-%s" % [terrain_size]
    return ""

func _terrain_fingerprint(data: Object) -> String:
    var values := PackedStringArray()
    for z in [128.0, 512.0, 1024.0, 1536.0, 1920.0]:
        for x in [128.0, 512.0, 1024.0, 1536.0, 1920.0]:
            var height := float(data.call("get_height", Vector3(x, 0.0, z)))
            if not is_finite(height):
                height = -999999.0
            values.append("%.5f" % height)
    return "|".join(values).sha256_text()

func _find_multimesh(root_node: Node) -> MultiMeshInstance3D:
    if root_node == null:
        return null
    for child in root_node.get_children():
        if child is MultiMeshInstance3D:
            return child as MultiMeshInstance3D
        var nested := _find_multimesh(child)
        if nested != null:
            return nested
    return null

func _multimesh_fingerprint(multimesh: MultiMesh) -> String:
    var values := PackedStringArray()
    for index in multimesh.instance_count:
        var transform := multimesh.get_instance_transform(index)
        values.append("%.5f,%.5f,%.5f" % [
            transform.origin.x, transform.origin.y, transform.origin.z])
    values.sort()
    return "|".join(values).sha256_text()

func _combined_stage_fingerprint(stage_fingerprints: Dictionary) -> String:
    var values := PackedStringArray()
    for stage in STAGES:
        values.append("%s=%s" % [stage, stage_fingerprints[stage]])
    return "|".join(values).sha256_text()

func _surface_alignment_count(multimesh: MultiMesh, terrain_data: Object) -> int:
    var aligned := 0
    for index in multimesh.instance_count:
        var position := multimesh.get_instance_transform(index).origin
        var terrain_height := float(terrain_data.call("get_height", position))
        if is_finite(terrain_height) and absf(position.y - terrain_height) <= 0.05:
            aligned += 1
    return aligned

func _unique_horizontal_position_count(multimesh: MultiMesh) -> int:
    var positions := {}
    for index in multimesh.instance_count:
        var position := multimesh.get_instance_transform(index).origin
        positions["%.4f,%.4f" % [position.x, position.z]] = true
    return positions.size()

func _request(backend: String, operation: String, stage: String) -> Dictionary:
    var recipe := {
        "id": "f0540000-0000-4000-8000-000000000201",
        "areaId": "f0540000-0000-4000-8000-000000000202",
        "seed": 20260827,
        "bounds": {
            "widthMeters": 2048.0,
            "depthMeters": 2048.0,
            "maxElevationMeters": 320.0,
        },
        "terrain": {
            "archetype": "temperate-valley",
            "roughness": 0.58,
        },
        "rivers": [
            { "id": "reference-river-main", "widthMeters": 26.0 },
            { "id": "reference-river-tributary", "widthMeters": 12.0 },
        ],
        "biomes": [
            { "id": "temperate-forest", "weight": 0.58 },
            { "id": "open-meadow", "weight": 0.27 },
            { "id": "rocky-ridge", "weight": 0.15 },
        ],
        "roads": [{ "id": "reference-east-west-road", "widthMeters": 8.0 }],
        "settlements": [
            { "id": "reference-village", "kind": "village" },
            { "id": "reference-castle", "kind": "castle" },
        ],
        "scatter": [
            { "id": "reference-trees", "density": 0.72 },
            { "id": "reference-rocks", "density": 0.28 },
        ],
        "regeneration": {
            "preserveManualEdits": true,
            "protectedNodePaths": [],
        },
    }
    return {
        "protocolVersion": "1.0.0",
        "planId": "f0540000-0000-4000-8000-000000000203",
        "transactionId": "f0540000-0000-4000-8000-000000000204",
        "projectRevision": 1,
        "recipeRevision": 1,
        "recipeId": recipe.id,
        "areaId": recipe.areaId,
        "recipeFingerprint": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
        "planFingerprint": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
        "seed": recipe.seed,
        "dryRun": false,
        "step": {
            "order": int(PLAN_STAGE_ORDER.get(stage, -1)),
            "stage": stage,
            "backend": backend,
            "operation": operation,
            "parameters": {},
            "dependsOn": [],
        },
        "recipe": recipe,
    }

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
