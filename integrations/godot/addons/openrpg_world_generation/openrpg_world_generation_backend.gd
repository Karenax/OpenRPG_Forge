extends Node

const PROTOCOL_VERSION := "1.0.0"
const GRAPH_REGISTRY_PATH := "res://addons/openrpg_world_generation/graph_registry.json"
const GENERATED_ROOT_NAME := "OpenRPGGeneratedWorld"
const MANUAL_LOCK_META := "openrpg_manual_lock"

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

var _graph_registry: Dictionary = {}

func _ready() -> void:
    add_to_group("forge_world_generation_terrain3d")
    add_to_group("forge_world_generation_pcgodot")
    add_to_group("forge_world_generation_native")
    _graph_registry = _load_graph_registry()

func execute_forge_world_generation_step(request_json: String) -> Dictionary:
    var request_variant: Variant = JSON.parse_string(request_json)
    if typeof(request_variant) != TYPE_DICTIONARY:
        return _failure("world_generation.bridge.invalid_json", "The bridge request is not a JSON object.")
    var request := request_variant as Dictionary
    if str(request.get("protocolVersion", "")) != PROTOCOL_VERSION:
        return _failure("world_generation.bridge.protocol_unsupported", "Unsupported bridge protocol.")
    var step: Dictionary = request.get("step", {})
    var validation := _validate_request(request, step)
    if not bool(validation.get("succeeded", false)):
        return validation
    var backend := str(step.get("backend", "godotNative"))
    var result: Dictionary
    match backend:
        "terrain3D", "terrain3d":
            result = _execute_terrain3d(request)
        "pcgGodot", "pcgodot":
            result = _execute_pcg(request)
        _:
            result = _execute_native(request)
    if not bool(result.get("succeeded", false)) and backend.to_lower() != "godotnative":
        var fallback := _execute_native(request)
        if bool(fallback.get("succeeded", false)):
            fallback["fallbackUsed"] = true
            fallback["requestedBackend"] = backend
            fallback["warnings"] = result.get("diagnostics", [])
            return fallback
    return result

func _execute_terrain3d(request: Dictionary) -> Dictionary:
    if not ClassDB.class_exists("Terrain3D"):
        return _failure("world_generation.terrain3d.unavailable", "Terrain3D is not installed or did not load.")
    var generated_root := _generated_root(request)
    var terrain := generated_root.get_node_or_null("Terrain3D")
    if terrain != null and _is_protected(terrain, request, "Terrain3D"):
        return _preserved(terrain, "Terrain3D")
    if terrain == null:
        terrain = ClassDB.instantiate("Terrain3D")
        if terrain == null or not (terrain is Node3D):
            return _failure("world_generation.terrain3d.instantiate_failed", "Terrain3D could not be instantiated.")
        terrain.name = "Terrain3D"
        generated_root.add_child(terrain)
        _assign_owner(terrain)
    var data: Variant = terrain.get("data")
    if data == null and ClassDB.class_exists("Terrain3DData"):
        data = ClassDB.instantiate("Terrain3DData")
        terrain.set("data", data)
    if data == null or not data.has_method("import_images"):
        return _failure("world_generation.terrain3d.data_unavailable", "Terrain3D data import API is unavailable.")

    var recipe: Dictionary = request.get("recipe", {})
    var image := _build_height_image(recipe)
    var images: Array[Image] = [image]
    var bounds: Dictionary = recipe.get("bounds", {})
    var scale := maxf(float(bounds.get("maxElevationMeters", 320.0)), 1.0)
    data.call("import_images", images, Vector3.ZERO, 0.0, scale)
    if data.has_method("calc_height_range"):
        data.call("calc_height_range", true)
    terrain.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    terrain.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    return _success([terrain.get_path()])

func _execute_pcg(request: Dictionary) -> Dictionary:
    if not ClassDB.class_exists("FlowGraphNode3D"):
        return _failure("world_generation.pcg.unavailable", "Flow Nodes Editor/PCGODOT is not installed or did not load.")
    var step: Dictionary = request.get("step", {})
    var stage := str(step.get("stage", "")).to_lower()
    var graph_path := str(_graph_registry.get(stage, ""))
    if graph_path.is_empty() or not ResourceLoader.exists(graph_path):
        return _failure("world_generation.pcg.graph_missing", "No reusable PCG graph is registered for stage '%s'." % stage)
    var graph := load(graph_path)
    if graph == null:
        return _failure("world_generation.pcg.graph_load_failed", "PCG graph '%s' could not be loaded." % graph_path)

    var generated_root := _generated_root(request)
    var node_name := "PCG_%s" % stage.capitalize()
    var previous := generated_root.get_node_or_null(node_name)
    if previous != null:
        if _is_protected(previous, request, node_name):
            return _preserved(previous, node_name)
        previous.free()
    var executor: Node3D = ClassDB.instantiate("FlowGraphNode3D") as Node3D
    if executor == null:
        return _failure("world_generation.pcg.instantiate_failed", "FlowGraphNode3D could not be instantiated.")
    executor.name = node_name
    executor.set("graph", graph)
    generated_root.add_child(executor)
    _assign_owner(executor)

    var parameters: Dictionary = step.get("parameters", {})
    if executor.has_method("get_or_create_override"):
        for key: Variant in parameters.keys():
            var override: Variant = executor.call("get_or_create_override", StringName(str(key)))
            if override != null:
                override.set("enabled", true)
                override.set("value", _coerce_parameter(parameters[key]))
    if executor.has_method("regenerate"):
        executor.call("regenerate")
    executor.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    executor.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    return _success([executor.get_path(), graph_path])

func _execute_native(request: Dictionary) -> Dictionary:
    var step: Dictionary = request.get("step", {})
    var stage := str(step.get("stage", "unknown")).to_lower()
    if stage == "terrain":
        return _execute_native_terrain(request)
    if stage == "validation":
        return _execute_native_validation(request)

    var generated_root := _generated_root(request)
    var node_name := "Native_%s" % stage.capitalize()
    var previous := generated_root.get_node_or_null(node_name)
    if previous != null:
        if _is_protected(previous, request, node_name):
            return _preserved(previous, node_name)
        previous.free()
    var container := Node3D.new()
    container.name = node_name
    generated_root.add_child(container)
    _assign_owner(container)
    container.set_meta("openrpg_operation", str(step.get("operation", "")))
    container.set_meta("openrpg_requested_backend", str(step.get("backend", "")))
    container.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    container.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    container.set_meta("openrpg_plan_fingerprint", str(request.get("planFingerprint", "")))
    container.set_meta("openrpg_parameters", step.get("parameters", {}))
    _add_native_stage_anchors(container, request, stage)
    return _success([container.get_path()])

func _execute_native_validation(request: Dictionary) -> Dictionary:
    var generated_root := _generated_root(request)
    var terrain := generated_root.get_node_or_null("Native_Terrain")
    if terrain == null:
        terrain = generated_root.get_node_or_null("Terrain3D")
    if terrain == null:
        return _failure("world_generation.validation.terrain_missing", "Generated world has no terrain artifact.")
    if generated_root.get_child_count() > 4096:
        return _failure("world_generation.validation.node_budget_exceeded", "Generated world exceeds the bridge node budget.")
    generated_root.set_meta("openrpg_validation_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    generated_root.set_meta("openrpg_validation_plan_fingerprint", str(request.get("planFingerprint", "")))
    return _success([generated_root.get_path(), terrain.get_path()])

func _execute_native_terrain(request: Dictionary) -> Dictionary:
    const SAMPLE_COUNT := 65
    var generated_root := _generated_root(request)
    var node_name := "Native_Terrain"
    var previous := generated_root.get_node_or_null(node_name)
    if previous != null:
        if _is_protected(previous, request, node_name):
            return _preserved(previous, node_name)
        previous.free()

    var recipe: Dictionary = request.get("recipe", {})
    var bounds: Dictionary = recipe.get("bounds", {})
    var width := maxf(float(bounds.get("widthMeters", 2048.0)), 1.0)
    var depth := maxf(float(bounds.get("depthMeters", 2048.0)), 1.0)
    var elevation := maxf(float(bounds.get("maxElevationMeters", 320.0)), 1.0)
    var image := _build_height_image(recipe, SAMPLE_COUNT)
    var vertices := PackedVector3Array()
    var normals := PackedVector3Array()
    var uvs := PackedVector2Array()
    var indices := PackedInt32Array()
    vertices.resize(SAMPLE_COUNT * SAMPLE_COUNT)
    normals.resize(SAMPLE_COUNT * SAMPLE_COUNT)
    uvs.resize(SAMPLE_COUNT * SAMPLE_COUNT)
    for z in range(SAMPLE_COUNT):
        for x in range(SAMPLE_COUNT):
            var index := z * SAMPLE_COUNT + x
            var u := float(x) / float(SAMPLE_COUNT - 1)
            var v := float(z) / float(SAMPLE_COUNT - 1)
            vertices[index] = Vector3((u - 0.5) * width, image.get_pixel(x, z).r * elevation, (v - 0.5) * depth)
            normals[index] = Vector3.UP
            uvs[index] = Vector2(u, v)
    for z in range(SAMPLE_COUNT - 1):
        for x in range(SAMPLE_COUNT - 1):
            var top_left := z * SAMPLE_COUNT + x
            var bottom_left := top_left + SAMPLE_COUNT
            indices.append_array(PackedInt32Array([
                top_left, bottom_left, top_left + 1,
                top_left + 1, bottom_left, bottom_left + 1,
            ]))
    var arrays := []
    arrays.resize(Mesh.ARRAY_MAX)
    arrays[Mesh.ARRAY_VERTEX] = vertices
    arrays[Mesh.ARRAY_NORMAL] = normals
    arrays[Mesh.ARRAY_TEX_UV] = uvs
    arrays[Mesh.ARRAY_INDEX] = indices
    var mesh := ArrayMesh.new()
    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
    var material := StandardMaterial3D.new()
    material.albedo_color = Color("#607a45")
    material.roughness = 0.92
    mesh.surface_set_material(0, material)

    var container := Node3D.new()
    container.name = node_name
    generated_root.add_child(container)
    _assign_owner(container)
    var mesh_instance := MeshInstance3D.new()
    mesh_instance.name = "Heightfield"
    mesh_instance.mesh = mesh
    container.add_child(mesh_instance)
    _assign_owner(mesh_instance)
    var static_body := StaticBody3D.new()
    static_body.name = "Collision"
    var collision := CollisionShape3D.new()
    collision.name = "Shape"
    collision.shape = mesh.create_trimesh_shape()
    static_body.add_child(collision)
    container.add_child(static_body)
    _assign_owner(static_body)
    _assign_owner(collision)
    container.set_meta("openrpg_operation", "godotnative.terrain")
    container.set_meta("openrpg_requested_backend", str((request.get("step", {}) as Dictionary).get("backend", "")))
    container.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    container.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    container.set_meta("openrpg_plan_fingerprint", str(request.get("planFingerprint", "")))
    return _success([container.get_path(), mesh_instance.get_path(), collision.get_path()])

func _add_native_stage_anchors(container: Node3D, request: Dictionary, stage: String) -> void:
    var recipe: Dictionary = request.get("recipe", {})
    var source: Array = []
    match stage:
        "hydrology": source = recipe.get("rivers", [])
        "biomes": source = recipe.get("biomes", [])
        "roads": source = recipe.get("roads", [])
        "settlements": source = recipe.get("settlements", [])
        "scatter": source = recipe.get("scatter", [])
        _:
            var marker := Marker3D.new()
            marker.name = "StageContract"
            container.add_child(marker)
            _assign_owner(marker)
            return
    var bounds: Dictionary = recipe.get("bounds", {})
    var width := maxf(float(bounds.get("widthMeters", 2048.0)), 1.0)
    var depth := maxf(float(bounds.get("depthMeters", 2048.0)), 1.0)
    var random := RandomNumberGenerator.new()
    random.seed = int(recipe.get("seed", 1)) + _stage_seed(stage)
    for index in range(mini(source.size(), 256)):
        var marker := Marker3D.new()
        marker.name = "%s_%03d" % [stage.capitalize(), index]
        marker.position = Vector3(random.randf_range(-width * 0.45, width * 0.45), 0.0,
            random.randf_range(-depth * 0.45, depth * 0.45))
        marker.set_meta("openrpg_definition", source[index])
        container.add_child(marker)
        _assign_owner(marker)

func _stage_seed(stage: String) -> int:
    match stage:
        "hydrology": return 101
        "biomes": return 211
        "roads": return 307
        "settlements": return 401
        "scatter": return 503
        "gameplay": return 601
        "navigation": return 701
        _: return 809

func _generated_root(request: Dictionary) -> Node3D:
    var scene_root := get_tree().edited_scene_root if Engine.is_editor_hint() else get_tree().current_scene
    if scene_root == null:
        scene_root = self
    var recipe_id := str(request.get("recipeId", "unknown")).replace("-", "")
    var container_name := "%s_%s" % [GENERATED_ROOT_NAME, recipe_id]
    var existing := scene_root.get_node_or_null(container_name)
    if existing is Node3D:
        return existing
    var root := Node3D.new()
    root.name = container_name
    scene_root.add_child(root)
    _assign_owner(root)
    return root

func _build_height_image(recipe: Dictionary, sample_count: int = 257) -> Image:
    var image := Image.create(sample_count, sample_count, false, Image.FORMAT_RF)
    var seed_value := int(recipe.get("seed", 1))
    var terrain: Dictionary = recipe.get("terrain", {})
    var roughness := clampf(float(terrain.get("roughness", 0.45)), 0.0, 1.0)
    var archetype := str(terrain.get("archetype", "temperate-valley")).to_lower()
    var noise := FastNoiseLite.new()
    noise.seed = seed_value
    noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
    noise.frequency = lerpf(0.0025, 0.012, roughness)
    noise.fractal_type = FastNoiseLite.FRACTAL_FBM
    noise.fractal_octaves = 5
    var rivers: Array = recipe.get("rivers", [])
    for z in range(sample_count):
        for x in range(sample_count):
            var nx := (float(x) / float(sample_count - 1)) * 2.0 - 1.0
            var nz := (float(z) / float(sample_count - 1)) * 2.0 - 1.0
            var height := noise.get_noise_2d(float(x), float(z)) * (0.16 + roughness * 0.32)
            if archetype.contains("valley"):
                height += absf(nx) * 0.28 - 0.12
            elif archetype.contains("mountain"):
                height += pow(absf(noise.get_noise_2d(float(x) * 0.35, float(z) * 0.35)), 1.7) * 0.48
            for river_index in rivers.size():
                var river: Dictionary = rivers[river_index]
                var width := clampf(float(river.get("widthMeters", 12.0)) / 220.0, 0.015, 0.18)
                var phase := float((seed_value + river_index * 97) % 997) / 997.0 * TAU
                var center := sin(nz * 3.1 + phase) * 0.22
                var distance := absf(nx - center)
                if distance < width:
                    height -= smoothstep(width, 0.0, distance) * 0.22
            image.set_pixel(x, z, Color(clampf(height, -1.0, 1.0), 0.0, 0.0, 1.0))
    return image

func _validate_request(request: Dictionary, step: Dictionary) -> Dictionary:
    var recipe_variant: Variant = request.get("recipe", {})
    if typeof(recipe_variant) != TYPE_DICTIONARY:
        return _failure("world_generation.bridge.recipe_missing", "The bridge recipe is not an object.")
    var recipe := recipe_variant as Dictionary
    if str(request.get("recipeId", "")) != str(recipe.get("id", "")) or \
            str(request.get("areaId", "")) != str(recipe.get("areaId", "")) or \
            int(request.get("seed", 0)) != int(recipe.get("seed", 0)):
        return _failure("world_generation.bridge.metadata_mismatch", "Bridge metadata does not match its recipe.")
    if not _is_lower_sha256(str(request.get("recipeFingerprint", ""))) or \
            not _is_lower_sha256(str(request.get("planFingerprint", ""))):
        return _failure("world_generation.bridge.fingerprint_invalid", "Bridge fingerprints must be lowercase SHA-256 values.")
    var backend := str(step.get("backend", "")).to_lower()
    var stage := str(step.get("stage", "")).to_lower()
    var key := "%s:%s" % [backend, stage]
    if not ALLOWED_OPERATIONS.has(key) or str(step.get("operation", "")) != str(ALLOWED_OPERATIONS[key]):
        return _failure("world_generation.bridge.operation_invalid", "The requested backend/stage operation is not allowlisted.")
    return { "succeeded": true }

func _is_lower_sha256(value: String) -> bool:
    if value.length() != 64:
        return false
    for character in value:
        if "0123456789abcdef".find(str(character)) < 0:
            return false
    return true

func _is_protected(node: Node, request: Dictionary, relative_path: String) -> bool:
    var recipe: Dictionary = request.get("recipe", {})
    var policy: Dictionary = recipe.get("regeneration", {})
    if not bool(policy.get("preserveManualEdits", true)):
        return false
    var root := node
    while root.get_parent() != null and not root.name.begins_with(GENERATED_ROOT_NAME):
        root = root.get_parent()
    if _contains_manual_lock(node) or bool(root.get_meta(MANUAL_LOCK_META, false)):
        return true
    var protected_paths: Array = policy.get("protectedNodePaths", [])
    return protected_paths.any(func(path: Variant) -> bool:
        var protected_path := str(path)
        return protected_path == relative_path or protected_path.begins_with(relative_path + "/"))

func _contains_manual_lock(node: Node) -> bool:
    if bool(node.get_meta(MANUAL_LOCK_META, false)):
        return true
    for child in node.get_children():
        if child is Node and _contains_manual_lock(child):
            return true
    return false

func _preserved(node: Node, relative_path: String) -> Dictionary:
    return {
        "succeeded": true,
        "artifacts": [node.get_path()],
        "diagnostics": [],
        "preservedManualEdits": true,
        "protectedPath": relative_path,
    }

func _load_graph_registry() -> Dictionary:
    if not FileAccess.file_exists(GRAPH_REGISTRY_PATH):
        return {}
    var file := FileAccess.open(GRAPH_REGISTRY_PATH, FileAccess.READ)
    var parsed: Variant = JSON.parse_string(file.get_as_text())
    if typeof(parsed) != TYPE_DICTIONARY:
        return {}
    return (parsed as Dictionary).get("graphs", {})

func _assign_owner(node: Node) -> void:
    if Engine.is_editor_hint() and get_tree().edited_scene_root != null:
        node.owner = get_tree().edited_scene_root

func _coerce_parameter(value: Variant) -> Variant:
    if typeof(value) != TYPE_STRING:
        return value
    var text := str(value)
    if text.to_lower() == "true": return true
    if text.to_lower() == "false": return false
    if text.is_valid_int(): return text.to_int()
    if text.is_valid_float(): return text.to_float()
    return text

func _success(artifacts: Array) -> Dictionary:
    return { "succeeded": true, "artifacts": artifacts, "diagnostics": [] }

func _failure(code: String, message: String) -> Dictionary:
    return {
        "succeeded": false,
        "artifacts": [],
        "diagnostics": [{ "code": code, "message": message, "path": "world-generation/bridge" }]
    }
