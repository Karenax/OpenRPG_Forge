extends SceneTree

const GENERATED_SCENE_PATH := "res://qualification-output/generated_world.tscn"

func _initialize() -> void:
    if not ResourceLoader.exists(GENERATED_SCENE_PATH):
        _fail("Neutral generated scene was not preserved for the reopen process.")
        return
    var source_text := FileAccess.get_file_as_string(GENERATED_SCENE_PATH)
    for forbidden_dependency in ["addons/terrain_3d", "addons/flow_nodes_editor", "FlowGraphNode3D", "Terrain3D"]:
        if source_text.contains(forbidden_dependency):
            _fail("Neutral scene still references provider-specific dependency '%s'." % forbidden_dependency)
            return

    var packed := load(GENERATED_SCENE_PATH) as PackedScene
    var scene := packed.instantiate() as Node3D if packed != null else null
    if scene == null:
        _fail("Neutral generated scene could not be instantiated.")
        return
    root.add_child(scene)
    current_scene = scene
    for _frame_index in 4:
        await physics_frame

    var terrain_mesh := scene.get_node_or_null("Terrain/Mesh") as MeshInstance3D
    var collision := scene.get_node_or_null("Terrain/Collision/Shape") as CollisionShape3D
    var navigation := scene.get_node_or_null("Navigation") as NavigationRegion3D
    var scatter := scene.get_node_or_null("GeneratedContent/BiomeProxies") as MultiMeshInstance3D
    if terrain_mesh == null or terrain_mesh.mesh == null or collision == null or collision.shape == null:
        _fail("Neutral scene lost its baked terrain mesh or collision.")
        return
    if navigation == null or navigation.navigation_mesh == null or \
            navigation.navigation_mesh.get_polygon_count() < 1:
        _fail("Neutral scene lost its baked navigation mesh.")
        return
    if scatter == null or scatter.multimesh == null or scatter.multimesh.instance_count != 16:
        _fail("Neutral scene lost its provider-generated instances.")
        return
    if _distinct_serialized_positions(scatter.multimesh) <= 1:
        _fail("Neutral scene collapsed its provider-generated instance transforms.")
        return

    var space := scene.get_world_3d().direct_space_state
    var ray := PhysicsRayQueryParameters3D.create(Vector3(0.0, 128.0, 0.0), Vector3(0.0, -128.0, 0.0))
    var hit := space.intersect_ray(ray)
    if hit.is_empty():
        _fail("Neutral terrain collision did not answer a vertical physics ray.")
        return

    # Register the serialized NavigationMesh explicitly. This isolates the proof
    # from SceneTree auto-registration timing while exercising the same runtime
    # NavigationServer3D path used by an exported game.
    var navigation_map := NavigationServer3D.map_create()
    NavigationServer3D.map_set_cell_size(navigation_map, navigation.navigation_mesh.cell_size)
    NavigationServer3D.map_set_cell_height(navigation_map, navigation.navigation_mesh.cell_height)
    NavigationServer3D.map_set_active(navigation_map, true)
    var navigation_region := NavigationServer3D.region_create()
    NavigationServer3D.region_set_enabled(navigation_region, true)
    NavigationServer3D.region_set_navigation_layers(navigation_region, 1)
    NavigationServer3D.region_set_map(navigation_region, navigation_map)
    NavigationServer3D.region_set_navigation_mesh(navigation_region, navigation.navigation_mesh)
    NavigationServer3D.region_set_transform(navigation_region, navigation.global_transform)
    NavigationServer3D.map_force_update(navigation_map)
    var region_ready := false
    for _navigation_frame in 120:
        await physics_frame
        if NavigationServer3D.region_get_iteration_id(navigation_region) > 0:
            region_ready = true
            break
    if not region_ready:
        NavigationServer3D.free_rid(navigation_region)
        NavigationServer3D.free_rid(navigation_map)
        _fail("Neutral navigation region did not synchronize within 120 physics frames.")
        return
    var post_region_iteration := NavigationServer3D.map_get_iteration_id(navigation_map)
    NavigationServer3D.map_force_update(navigation_map)
    var map_ready := false
    for _map_frame in 120:
        await physics_frame
        if NavigationServer3D.map_get_iteration_id(navigation_map) > post_region_iteration and \
                NavigationServer3D.map_get_regions(navigation_map).size() == 1:
            map_ready = true
            break
    if not map_ready:
        NavigationServer3D.free_rid(navigation_region)
        NavigationServer3D.free_rid(navigation_map)
        _fail("Neutral navigation map did not synchronize the ready region within 120 physics frames.")
        return
    var nav_vertices := navigation.navigation_mesh.get_vertices()
    var reference_polygon := navigation.navigation_mesh.get_polygon(0)
    if reference_polygon.size() < 2:
        _fail("Neutral navigation mesh has no usable reference polygon.")
        return
    var from := NavigationServer3D.map_get_closest_point(
        navigation_map, nav_vertices[reference_polygon[0]])
    var to := NavigationServer3D.map_get_closest_point(
        navigation_map, nav_vertices[reference_polygon[reference_polygon.size() - 1]])
    var path := NavigationServer3D.map_get_path(navigation_map, from, to, true)
    if path.size() < 2:
        NavigationServer3D.free_rid(navigation_region)
        NavigationServer3D.free_rid(navigation_map)
        _fail("Neutral navigation mesh did not provide a traversable reference path (regions=%d region_iteration=%d bounds=%s region_point=%s from=%s to=%s path=%s)." % [
            NavigationServer3D.map_get_regions(navigation_map).size(),
            NavigationServer3D.region_get_iteration_id(navigation_region),
            NavigationServer3D.region_get_bounds(navigation_region),
            NavigationServer3D.region_get_closest_point(navigation_region, nav_vertices[reference_polygon[0]]),
            from,
            to,
            path])
        return

    NavigationServer3D.free_rid(navigation_region)
    NavigationServer3D.free_rid(navigation_map)
    print("WORLD_GENERATION_REOPEN_PASS provider_dependencies=0 terrain_mesh=1 collision_hit=1 navigation_polygons=%d navigation_path=%d pcg_instances=16 scene_sha256=%s" % [
        navigation.navigation_mesh.get_polygon_count(),
        path.size(),
        FileAccess.get_sha256(GENERATED_SCENE_PATH)])
    quit(0)

func _distinct_serialized_positions(multimesh: MultiMesh) -> int:
    if multimesh.transform_format != MultiMesh.TRANSFORM_3D:
        return 0
    var stride := 12 + (4 if multimesh.use_colors else 0) + (4 if multimesh.use_custom_data else 0)
    var serialized := multimesh.buffer
    if serialized.size() != multimesh.instance_count * stride:
        return 0
    var positions := {}
    for instance_index in multimesh.instance_count:
        var offset := instance_index * stride
        positions["%.3f,%.3f" % [serialized[offset + 3], serialized[offset + 11]]] = true
    return positions.size()

func _fail(message: String) -> void:
    push_error(message)
    quit(1)
