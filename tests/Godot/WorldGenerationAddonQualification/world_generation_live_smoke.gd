extends SceneTree

const BackendScript := preload("res://addons/openrpg_world_generation/openrpg_world_generation_backend.gd")
const RUNS := 3

func _initialize() -> void:
    if not Engine.is_editor_hint():
        _fail("Graph PCG qualification must run in editor mode before baking neutral runtime artifacts.")
        return
    var provider_ready := false
    for _frame_index in 600:
        for _projection_frame in 3:
            await process_frame
        if EditorInterface.is_plugin_enabled("flow_nodes_editor") and \
                ResourceLoader.exists("res://addons/flow_nodes_editor/flow_node.gd"):
            provider_ready = true
            break
    if not provider_ready:
        _fail("Godot Flow editor plugin did not become ready within 600 frames.")
        return

    var scene := Node3D.new()
    scene.name = "WorldGenerationLiveQualification"
    root.add_child(scene)
    current_scene = scene
    var backend: Node = BackendScript.new()
    backend.name = "OpenRPGWorldGenerationBackend"
    scene.add_child(backend)
    await process_frame

    var terrain_fingerprints: Array[String] = []
    var graph_fingerprints: Array[String] = []
    var scene_content_fingerprints: Array[String] = []
    var bake_result: Dictionary = {}
    for run_index in RUNS:
        var terrain_result: Dictionary = backend.call(
            "execute_forge_world_generation_step",
            JSON.stringify(_request("terrain3D", "terrain3d.generate_heightfield", "terrain")))
        if not bool(terrain_result.get("succeeded", false)) or bool(terrain_result.get("fallbackUsed", false)):
            _fail("Terrain3D run %d failed or used fallback: %s" % [run_index, terrain_result])
            return
        var terrain := scene.find_child("Terrain3D", true, false)
        if terrain == null or terrain.get("data") == null:
            _fail("Terrain3D run %d produced no terrain data." % run_index)
            return
        var terrain_data: Object = terrain.get("data")
        if not terrain_data.has_method("get_region_count") or int(terrain_data.call("get_region_count")) < 1:
            _fail("Terrain3D run %d produced no terrain region." % run_index)
            return
        terrain_fingerprints.append(_terrain_fingerprint(terrain_data))
        for _physics_index in 3:
            await physics_frame
            await process_frame

        var graph_result: Dictionary = backend.call(
            "execute_forge_world_generation_step",
            JSON.stringify(_request("pcgGodot", "pcgodot.generate_biomes", "biomes")))
        if not bool(graph_result.get("succeeded", false)) or bool(graph_result.get("fallbackUsed", false)):
            _fail("Graph PCG run %d failed or used fallback: %s" % [run_index, graph_result])
            return
        await process_frame
        var executor := scene.find_child("PCG_Biomes", true, false) as Node3D
        if executor == null:
            _fail("Graph PCG run %d produced no executor." % run_index)
            return
        var multimesh := _find_multimesh(executor)
        if multimesh == null or multimesh.multimesh == null or multimesh.multimesh.instance_count != 16:
            _fail("Graph PCG run %d did not generate the expected 16 instances." % run_index)
            return
        var unique_horizontal_positions := _unique_horizontal_position_count(multimesh.multimesh)
        if unique_horizontal_positions != multimesh.multimesh.instance_count:
            _fail("Graph PCG run %d produced only %d/%d distinct horizontal positions (bridge=%s)." % [
                run_index, unique_horizontal_positions, multimesh.multimesh.instance_count, graph_result])
            return
        var surface_aligned_count := _surface_alignment_count(multimesh.multimesh, terrain_data)
        if surface_aligned_count != multimesh.multimesh.instance_count:
            _fail("Graph PCG run %d aligned only %d/%d instances to Terrain3D (%s)." % [
                run_index, surface_aligned_count, multimesh.multimesh.instance_count,
                _surface_alignment_diagnostic(multimesh.multimesh, terrain_data, executor)])
            return
        graph_fingerprints.append(_multimesh_fingerprint(multimesh.multimesh))

        var output_path := "res://qualification-output/generated_world.tscn" if run_index == RUNS - 1 else \
            "res://qualification-output/generated_world_run_%d.tscn" % run_index
        var run_bake_result: Dictionary = backend.call(
            "bake_forge_generated_world",
            "f0540000-0000-4000-8000-000000000101",
            output_path)
        if not bool(run_bake_result.get("succeeded", false)) or \
                int(run_bake_result.get("collisionShapes", 0)) != 1 or \
                int(run_bake_result.get("navigationPolygons", 0)) < 1 or \
                int(run_bake_result.get("generatedInstances", 0)) != 16 or \
                str(run_bake_result.get("contentFingerprint", "")).length() != 64:
            _fail("Provider-neutral world bake %d failed: %s" % [run_index, run_bake_result])
            return
        var terrain_aabb_size: Array = run_bake_result.get("terrainAabbSize", [])
        if terrain_aabb_size.size() != 3 or \
                absf(float(terrain_aabb_size[0]) - 256.0) > 1.0 or \
                absf(float(terrain_aabb_size[2]) - 256.0) > 1.0:
            _fail("Provider-neutral terrain extent does not match the 256 m recipe: %s" % [terrain_aabb_size])
            return
        scene_content_fingerprints.append(str(run_bake_result.contentFingerprint))
        bake_result = run_bake_result

    if terrain_fingerprints.any(func(value: String) -> bool: return value != terrain_fingerprints[0]):
        _fail("Terrain3D generated non-deterministic fingerprints: %s" % terrain_fingerprints)
        return
    if graph_fingerprints.any(func(value: String) -> bool: return value != graph_fingerprints[0]):
        _fail("Graph PCG generated non-deterministic fingerprints: %s" % graph_fingerprints)
        return
    if scene_content_fingerprints.any(func(value: String) -> bool: return value != scene_content_fingerprints[0]):
        _fail("Neutral scene content was non-deterministic: %s" % scene_content_fingerprints)
        return

    var terrain_fingerprint := terrain_fingerprints[0]
    var graph_fingerprint := graph_fingerprints[0]
    current_scene = null
    scene.free()
    await process_frame
    for plugin_name in ["openrpg_world_generation", "flow_nodes_editor", "terrain_3d"]:
        if EditorInterface.is_plugin_enabled(plugin_name):
            EditorInterface.set_plugin_enabled(plugin_name, false)
    for _cleanup_frame in 3:
        await process_frame
    var terrain_aabb_size: Array = bake_result.get("terrainAabbSize", [])
    print("WORLD_GENERATION_LIVE_SMOKE_PASS runs=3 terrain3d_regions=1 pcg_instances=16 fallback=0 collision=1 navigation_polygons=%d terrain_width=%.3f terrain_depth=%.3f baked_sha256=%s terrain_fingerprint=%s graph_fingerprint=%s content_fingerprint=%s" % [
        int(bake_result.get("navigationPolygons", 0)),
        float(terrain_aabb_size[0]),
        float(terrain_aabb_size[2]),
        str(bake_result.get("sha256", "")),
        terrain_fingerprint,
        graph_fingerprint,
        scene_content_fingerprints[0]])
    quit(0)

func _terrain_fingerprint(data: Object) -> String:
    var values := PackedStringArray()
    for z in [-48.0, -24.0, 0.0, 24.0, 48.0]:
        for x in [-48.0, -24.0, 0.0, 24.0, 48.0]:
            var height := float(data.call("get_height", Vector3(x, 0.0, z)))
            if not is_finite(height):
                height = -999999.0
            values.append("%.5f" % height)
    return "|".join(values).sha256_text()

func _find_multimesh(root_node: Node) -> MultiMeshInstance3D:
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

func _surface_alignment_diagnostic(multimesh: MultiMesh, terrain_data: Object, executor: Node) -> String:
    var values := PackedStringArray()
    for index in mini(multimesh.instance_count, 4):
        var position := multimesh.get_instance_transform(index).origin
        var terrain_height := float(terrain_data.call("get_height", position))
        values.append("%s=>%s" % [position, terrain_height])
    return "samples=[%s] queued_aligned=%s queued_failures=%s" % [
        ",".join(values),
        executor.get_meta("openrpg_surface_aligned_instances", "missing"),
        executor.get_meta("openrpg_surface_projection_failures", "missing")]

func _request(backend: String, operation: String, stage: String) -> Dictionary:
    var recipe := {
        "id": "f0540000-0000-4000-8000-000000000101",
        "areaId": "f0540000-0000-4000-8000-000000000102",
        "seed": 20260827,
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
        "biomes": [{ "id": "temperate", "weight": 1.0 }],
        "roads": [],
        "settlements": [],
        "scatter": [],
        "regeneration": {
            "preserveManualEdits": true,
            "protectedNodePaths": [],
        },
    }
    return {
        "protocolVersion": "1.0.0",
        "planId": "f0540000-0000-4000-8000-000000000103",
        "transactionId": "f0540000-0000-4000-8000-000000000104",
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

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
