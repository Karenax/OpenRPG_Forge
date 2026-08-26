extends Node

const PROTOCOL_VERSION := "1.0.0"
const GRAPH_REGISTRY_PATH := "res://addons/openrpg_world_generation/graph_registry.json"
const GENERATED_ROOT_NAME := "OpenRPGGeneratedWorld"

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
    var backend := str(step.get("backend", "godotNative"))
    match backend:
        "terrain3D", "terrain3d":
            return _execute_terrain3d(request)
        "pcgGodot", "pcgodot":
            return _execute_pcg(request)
        _:
            return _execute_native(request)

func _execute_terrain3d(request: Dictionary) -> Dictionary:
    if not ClassDB.class_exists("Terrain3D"):
        return _failure("world_generation.terrain3d.unavailable", "Terrain3D is not installed or did not load.")
    var generated_root := _generated_root(request)
    var terrain := generated_root.get_node_or_null("Terrain3D")
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
        previous.queue_free()
    var executor := ClassDB.instantiate("FlowGraphNode3D")
    if executor == null or not (executor is Node3D):
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
        executor.call_deferred("regenerate")
    executor.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    executor.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    return _success([executor.get_path(), graph_path])

func _execute_native(request: Dictionary) -> Dictionary:
    var generated_root := _generated_root(request)
    var step: Dictionary = request.get("step", {})
    var stage := str(step.get("stage", "unknown"))
    var marker_name := "Native_%s" % stage.capitalize()
    var marker := generated_root.get_node_or_null(marker_name)
    if marker == null:
        marker = Node3D.new()
        marker.name = marker_name
        generated_root.add_child(marker)
        _assign_owner(marker)
    marker.set_meta("openrpg_operation", str(step.get("operation", "")))
    marker.set_meta("openrpg_recipe_id", str(request.get("recipeId", "")))
    marker.set_meta("openrpg_recipe_fingerprint", str(request.get("recipeFingerprint", "")))
    marker.set_meta("openrpg_parameters", step.get("parameters", {}))
    return _success([marker.get_path()])

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

func _build_height_image(recipe: Dictionary) -> Image:
    const SAMPLE_COUNT := 257
    var image := Image.create(SAMPLE_COUNT, SAMPLE_COUNT, false, Image.FORMAT_RF)
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
    for z in SAMPLE_COUNT:
        for x in SAMPLE_COUNT:
            var nx := (float(x) / float(SAMPLE_COUNT - 1)) * 2.0 - 1.0
            var nz := (float(z) / float(SAMPLE_COUNT - 1)) * 2.0 - 1.0
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
        "diagnostics": [{ "code": code, "message": message }]
    }
