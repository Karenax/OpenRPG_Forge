extends SceneTree

const GENERATED_SCENE_PATH := "res://qualification-output/generated_world.tscn"
const CAPTURE_DIRECTORY := "res://captures"
const CAPTURE_WIDTH := 1280
const CAPTURE_HEIGHT := 720

func _initialize() -> void:
    if not ResourceLoader.exists(GENERATED_SCENE_PATH):
        _fail("Neutral generated scene is missing for capture qualification.")
        return
    var packed := load(GENERATED_SCENE_PATH) as PackedScene
    var scene := packed.instantiate() as Node3D if packed != null else null
    if scene == null:
        _fail("Neutral generated scene could not be instantiated for captures.")
        return
    root.add_child(scene)
    current_scene = scene
    await process_frame

    var terrain := scene.get_node_or_null("Terrain/Mesh") as MeshInstance3D
    if terrain == null or terrain.mesh == null:
        _fail("Neutral terrain mesh is missing for captures.")
        return
    var bounds := terrain.mesh.get_aabb()
    var center := terrain.global_transform * bounds.get_center()
    var extent := maxf(bounds.size.x, bounds.size.z)

    var environment_node := WorldEnvironment.new()
    var environment := Environment.new()
    environment.background_mode = Environment.BG_COLOR
    environment.background_color = Color("#9eb8ca")
    environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    environment.ambient_light_color = Color("#dce7ee")
    environment.ambient_light_energy = 0.8
    environment_node.environment = environment
    scene.add_child(environment_node)

    var sunlight := DirectionalLight3D.new()
    sunlight.rotation_degrees = Vector3(-52.0, -32.0, 0.0)
    sunlight.light_energy = 1.25
    sunlight.shadow_enabled = true
    scene.add_child(sunlight)

    var camera := Camera3D.new()
    camera.current = true
    scene.add_child(camera)
    await process_frame
    root.content_scale_size = Vector2i(CAPTURE_WIDTH, CAPTURE_HEIGHT)
    var directory_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(CAPTURE_DIRECTORY))
    if directory_error != OK:
        _fail("Capture directory could not be created.")
        return

    var viewpoints := [
        { "name": "aerial", "offset": Vector3(0.0, extent * 1.05, 0.01), "orthographic": true },
        { "name": "north", "offset": Vector3(0.0, extent * 0.34, extent * 0.82), "orthographic": false },
        { "name": "south", "offset": Vector3(0.0, extent * 0.34, -extent * 0.82), "orthographic": false },
        { "name": "east", "offset": Vector3(extent * 0.82, extent * 0.34, 0.0), "orthographic": false },
        { "name": "west", "offset": Vector3(-extent * 0.82, extent * 0.34, 0.0), "orthographic": false },
    ]
    var hashes := PackedStringArray()
    for viewpoint in viewpoints:
        camera.projection = Camera3D.PROJECTION_ORTHOGONAL if viewpoint.orthographic else Camera3D.PROJECTION_PERSPECTIVE
        camera.size = extent * 1.18
        camera.fov = 58.0
        camera.position = center + viewpoint.offset
        camera.look_at(center, Vector3.FORWARD if viewpoint.orthographic else Vector3.UP)
        for _settle_frame in 3:
            await process_frame
        await RenderingServer.frame_post_draw
        var image := root.get_texture().get_image()
        if image == null or image.is_empty() or image.get_width() != CAPTURE_WIDTH or image.get_height() != CAPTURE_HEIGHT:
            _fail("Renderer did not return a normalized %dx%d capture." % [CAPTURE_WIDTH, CAPTURE_HEIGHT])
            return
        var output_path := "%s/%s.png" % [CAPTURE_DIRECTORY, viewpoint.name]
        var save_error := image.save_png(output_path)
        if save_error != OK:
            _fail("Capture '%s' could not be saved (error %d)." % [viewpoint.name, save_error])
            return
        hashes.append(FileAccess.get_sha256(output_path))

    var unique_hashes := {}
    for capture_hash in hashes:
        unique_hashes[capture_hash] = true
    if unique_hashes.size() != viewpoints.size():
        _fail("Normalized viewpoints did not produce distinct render captures.")
        return
    print("WORLD_GENERATION_CAPTURE_PASS captures=%d width=%d height=%d fingerprint=%s" % [
        viewpoints.size(), CAPTURE_WIDTH, CAPTURE_HEIGHT, "|".join(hashes).sha256_text()])
    quit(0)

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
