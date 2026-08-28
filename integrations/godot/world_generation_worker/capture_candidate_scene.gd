extends SceneTree

const CAPTURE_WIDTH := 1280
const CAPTURE_HEIGHT := 720

func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var scene_path := OS.get_environment("OPENRPG_WORLD_GENERATION_CAPTURE_SCENE")
    var output_directory := OS.get_environment("OPENRPG_WORLD_GENERATION_CAPTURE_OUTPUT")
    if not scene_path.begins_with("res://") or not output_directory.begins_with("res://"):
        _fail("Candidate capture paths must be confined res:// paths.")
        return
    var packed := load(scene_path) as PackedScene
    var scene := packed.instantiate() as Node3D if packed != null else null
    if scene == null:
        _fail("Provider-neutral candidate scene could not be instantiated.")
        return
    root.add_child(scene)
    current_scene = scene
    for _frame_index in 3:
        await process_frame

    var terrain := scene.get_node_or_null("Terrain/Mesh") as MeshInstance3D
    if terrain == null or terrain.mesh == null:
        _fail("Candidate terrain mesh is missing.")
        return
    var terrain_bounds := terrain.mesh.get_aabb()
    var terrain_center := terrain.global_transform * terrain_bounds.get_center()
    var terrain_extent := maxf(terrain_bounds.size.x, terrain_bounds.size.z)

    var environment_node := WorldEnvironment.new()
    var environment := Environment.new()
    environment.background_mode = Environment.BG_COLOR
    environment.background_color = Color("#91abc0")
    environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    environment.ambient_light_color = Color("#e3edf2")
    environment.ambient_light_energy = 0.9
    environment_node.environment = environment
    scene.add_child(environment_node)

    var sunlight := DirectionalLight3D.new()
    sunlight.rotation_degrees = Vector3(-50.0, -28.0, 0.0)
    sunlight.light_energy = 1.35
    sunlight.shadow_enabled = true
    scene.add_child(sunlight)
    var camera := Camera3D.new()
    camera.current = true
    camera.fov = 55.0
    scene.add_child(camera)
    root.content_scale_size = Vector2i(CAPTURE_WIDTH, CAPTURE_HEIGHT)
    if DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_directory)) != OK:
        _fail("Candidate capture output directory could not be created.")
        return

    var viewpoints: Array[Dictionary] = [{
        "name": "overview",
        "center": terrain_center,
        "offset": Vector3(terrain_extent * 0.72, terrain_extent * 0.66, terrain_extent * 0.72),
    }]
    var role_distances := {
        "biomes": 100.0,
        "roads": 90.0,
        "settlements": 140.0,
        "scatter": 65.0,
    }
    var content := scene.get_node_or_null("GeneratedContent")
    if content == null:
        _fail("Candidate generated-content root is missing.")
        return
    for role in ["biomes", "roads", "settlements", "scatter"]:
        var instance := _find_role_instance(content, role)
        if instance == null or instance.multimesh == null or instance.multimesh.instance_count == 0:
            _fail("Candidate artistic role '%s' has no instances." % role)
            return
        var sample_index := instance.multimesh.instance_count / 2
        var sample_transform := instance.multimesh.get_instance_transform(sample_index)
        var center := instance.global_transform * sample_transform.origin
        var distance := float(role_distances[role])
        viewpoints.append({
            "name": role,
            "center": center + Vector3(0.0, 2.0, 0.0),
            "offset": Vector3(distance * 0.72, distance * 0.46, distance * 0.72),
        })

    var hashes := PackedStringArray()
    for viewpoint in viewpoints:
        camera.projection = Camera3D.PROJECTION_PERSPECTIVE
        camera.position = viewpoint.center + viewpoint.offset
        camera.look_at(viewpoint.center, Vector3.UP)
        for _settle_frame in 4:
            await process_frame
        await RenderingServer.frame_post_draw
        var image := root.get_texture().get_image()
        if image == null or image.is_empty() or image.get_size() != Vector2i(CAPTURE_WIDTH, CAPTURE_HEIGHT):
            _fail("Renderer did not return a normalized candidate capture.")
            return
        var output_path := "%s/%s.png" % [output_directory, viewpoint.name]
        if image.save_png(output_path) != OK:
            _fail("Candidate capture '%s' could not be saved." % viewpoint.name)
            return
        hashes.append(FileAccess.get_sha256(output_path))

    print("OPENRPG_FORGE_WORLD_GENERATION_CANDIDATE_CAPTURE_PASS captures=%d fingerprint=%s" % [
        hashes.size(), "|".join(hashes).sha256_text()])
    quit(0)

func _find_role_instance(root_node: Node, role: String) -> MultiMeshInstance3D:
    for candidate in root_node.find_children("*", "MultiMeshInstance3D", true, false):
        var instance := candidate as MultiMeshInstance3D
        if instance != null and str(instance.get_meta("openrpg_artistic_role", "")).to_lower() == role:
            return instance
    return null

func _fail(message: String) -> void:
    push_error(message)
    print("OPENRPG_FORGE_WORLD_GENERATION_CANDIDATE_CAPTURE_FAILED reason=%s" % message.replace(" ", "_"))
    quit(1)
