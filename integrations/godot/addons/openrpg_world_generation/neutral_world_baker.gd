@tool
extends RefCounted

const FORMAT_VERSION := "1.0.0"
const FORBIDDEN_SCENE_DEPENDENCIES := [
    "addons/terrain_3d",
    "addons/flow_nodes_editor",
    "FlowGraphNode3D",
    "Terrain3D",
]

func bake(generated_root: Node3D, output_path: String) -> Dictionary:
    if generated_root == null:
        return _failure("world_generation.bake.root_missing", "Generated world root was not found.")
    var safe_path := output_path.simplify_path()
    if not safe_path.begins_with("res://") or not safe_path.ends_with(".tscn") or \
            safe_path.contains("/../"):
        return _failure("world_generation.bake.path_invalid", "Bake output must be a confined res:// .tscn path.")

    var terrain := generated_root.get_node_or_null("Terrain3D")
    if terrain == null or not terrain.has_method("bake_mesh"):
        return _failure("world_generation.bake.terrain_missing", "Terrain provider cannot produce a neutral mesh.")
    var provider_mesh := terrain.call("bake_mesh", 2, 0) as Mesh
    if provider_mesh == null or provider_mesh.get_surface_count() == 0:
        return _failure("world_generation.bake.mesh_failed", "Terrain provider returned no bakeable surface.")
    var terrain_mesh := provider_mesh.duplicate(true) as Mesh
    if terrain_mesh == null:
        return _failure("world_generation.bake.mesh_duplicate_failed", "Neutral terrain mesh could not be detached.")

    var baked_root := Node3D.new()
    baked_root.name = "OpenRPGBakedWorld"
    baked_root.set_meta("openrpg_bake_format", FORMAT_VERSION)
    baked_root.set_meta("openrpg_recipe_fingerprint", generated_root.get_meta("openrpg_recipe_fingerprint", ""))

    var terrain_root := Node3D.new()
    terrain_root.name = "Terrain"
    baked_root.add_child(terrain_root)
    var mesh_instance := MeshInstance3D.new()
    mesh_instance.name = "Mesh"
    mesh_instance.mesh = terrain_mesh
    var material := StandardMaterial3D.new()
    material.albedo_color = Color("#607a45")
    material.roughness = 0.92
    mesh_instance.material_override = material
    terrain_root.add_child(mesh_instance)

    var collision_body := StaticBody3D.new()
    collision_body.name = "Collision"
    var collision_shape := CollisionShape3D.new()
    collision_shape.name = "Shape"
    collision_shape.shape = terrain_mesh.create_trimesh_shape()
    if collision_shape.shape == null:
        baked_root.free()
        return _failure("world_generation.bake.collision_failed", "Neutral terrain collision could not be built.")
    collision_body.add_child(collision_shape)
    terrain_root.add_child(collision_body)

    var navigation := NavigationRegion3D.new()
    navigation.name = "Navigation"
    var navigation_mesh := NavigationMesh.new()
    var mesh_bounds := terrain_mesh.get_aabb()
    var horizontal_extent := maxf(mesh_bounds.size.x, mesh_bounds.size.z)
    navigation_mesh.cell_size = clampf(horizontal_extent / 1024.0, 0.25, 4.0)
    navigation_mesh.cell_height = maxf(navigation_mesh.cell_size * 0.5, 0.25)
    navigation_mesh.agent_radius = 0.5
    navigation_mesh.agent_height = 1.8
    navigation_mesh.agent_max_climb = maxf(navigation_mesh.cell_height * 2.0, 0.5)
    navigation_mesh.agent_max_slope = 50.0
    var navigation_bounds := AABB(
        mesh_bounds.position - Vector3(2.0, 32.0, 2.0),
        mesh_bounds.size + Vector3(4.0, 64.0, 4.0))
    navigation_mesh.filter_baking_aabb = navigation_bounds
    var source_geometry := NavigationMeshSourceGeometryData3D.new()
    var navigation_faces := _mesh_faces(terrain_mesh)
    if navigation_faces.is_empty():
        baked_root.free()
        return _failure("world_generation.bake.navigation_source_failed", "Terrain provider returned no navigation faces.")
    source_geometry.add_faces(navigation_faces, Transform3D.IDENTITY)
    NavigationServer3D.bake_from_source_geometry_data(navigation_mesh, source_geometry)
    if navigation_mesh.get_polygon_count() == 0:
        baked_root.free()
        return _failure("world_generation.bake.navigation_failed", "Neutral navigation bake produced no polygons.")
    navigation.navigation_mesh = navigation_mesh
    baked_root.add_child(navigation)

    var generated_content := Node3D.new()
    generated_content.name = "GeneratedContent"
    baked_root.add_child(generated_content)
    var copied_instances := 0
    var artistic_instances := 0
    var artistic_roles := {}
    var generated_multimeshes := generated_root.find_children("*", "MultiMeshInstance3D", true, false)
    for source_node in generated_multimeshes:
        var source_instance := source_node as MultiMeshInstance3D
        if source_instance == null or source_instance.multimesh == null:
            continue
        var neutral_instance := MultiMeshInstance3D.new()
        var artistic_role := str(source_instance.get_meta("openrpg_artistic_role", "")).to_lower()
        neutral_instance.name = (
            "Artistic_%s_%03d" % [artistic_role.capitalize(), copied_instances]
            if not artistic_role.is_empty() else "GeneratedInstances_%03d" % copied_instances)
        neutral_instance.multimesh = _copy_multimesh(source_instance.multimesh)
        if source_instance.material_override != null:
            neutral_instance.material_override = source_instance.material_override.duplicate(true)
        generated_content.add_child(neutral_instance)
        if not artistic_role.is_empty():
            neutral_instance.set_meta("openrpg_artistic_role", artistic_role)
            neutral_instance.set_meta("openrpg_recipe_driven", true)
            artistic_instances += neutral_instance.multimesh.instance_count
            artistic_roles[artistic_role] = int(artistic_roles.get(artistic_role, 0)) + \
                neutral_instance.multimesh.instance_count
        copied_instances += neutral_instance.multimesh.instance_count
    if copied_instances == 0:
        baked_root.free()
        return _failure("world_generation.bake.generated_content_missing", "Graph PCG produced no neutral instances to bake.")

    var content_fingerprint := _content_fingerprint(terrain_mesh, navigation_mesh, generated_content)
    _assign_owner_recursive(baked_root, baked_root)
    var packed := PackedScene.new()
    var pack_error := packed.pack(baked_root)
    if pack_error != OK:
        baked_root.free()
        return _failure("world_generation.bake.pack_failed", "Neutral scene packing failed with error %d." % pack_error)
    var absolute_directory := ProjectSettings.globalize_path(safe_path.get_base_dir())
    var directory_error := DirAccess.make_dir_recursive_absolute(absolute_directory)
    if directory_error != OK:
        baked_root.free()
        return _failure("world_generation.bake.directory_failed", "Neutral scene directory could not be created.")
    var save_error := ResourceSaver.save(
        packed,
        safe_path,
        ResourceSaver.FLAG_RELATIVE_PATHS | ResourceSaver.FLAG_BUNDLE_RESOURCES)
    baked_root.free()
    if save_error != OK:
        return _failure("world_generation.bake.save_failed", "Neutral scene save failed with error %d." % save_error)

    var scene_text := FileAccess.get_file_as_string(safe_path)
    for forbidden_dependency in FORBIDDEN_SCENE_DEPENDENCIES:
        if scene_text.contains(forbidden_dependency):
            return _failure(
                "world_generation.bake.provider_dependency",
                "Neutral scene still references provider-specific dependency '%s'." % forbidden_dependency)

    return {
        "succeeded": true,
        "artifacts": [safe_path],
        "diagnostics": [],
        "formatVersion": FORMAT_VERSION,
        "terrainSurfaces": terrain_mesh.get_surface_count(),
        "terrainAabbPosition": [mesh_bounds.position.x, mesh_bounds.position.y, mesh_bounds.position.z],
        "terrainAabbSize": [mesh_bounds.size.x, mesh_bounds.size.y, mesh_bounds.size.z],
        "collisionShapes": 1,
        "navigationPolygons": navigation_mesh.get_polygon_count(),
        "navigationCellSize": navigation_mesh.cell_size,
        "navigationCellHeight": navigation_mesh.cell_height,
        "generatedInstances": copied_instances,
        "artisticInstances": artistic_instances,
        "artisticRoles": artistic_roles,
        "contentFingerprint": content_fingerprint,
        "sha256": FileAccess.get_sha256(safe_path),
    }

func _copy_multimesh(source: MultiMesh) -> MultiMesh:
    var result := MultiMesh.new()
    result.transform_format = source.transform_format
    result.use_colors = source.use_colors
    result.use_custom_data = source.use_custom_data
    result.mesh = source.mesh.duplicate(true) if source.mesh != null else null
    result.instance_count = source.instance_count
    result.visible_instance_count = source.visible_instance_count
    for index in source.instance_count:
        result.set_instance_transform(index, source.get_instance_transform(index))
        if source.use_colors:
            result.set_instance_color(index, source.get_instance_color(index))
        if source.use_custom_data:
            result.set_instance_custom_data(index, source.get_instance_custom_data(index))
    # ResourceSaver serializes the packed MultiMesh buffer, not the transient
    # per-instance mutation calls reliably. Assigning the complete buffer makes
    # the provider-neutral scene preserve every generated transform on disk.
    if not source.buffer.is_empty():
        result.buffer = source.buffer.duplicate()
    return result

func _mesh_faces(mesh: Mesh) -> PackedVector3Array:
    var faces := PackedVector3Array()
    for surface_index in mesh.get_surface_count():
        if mesh.surface_get_primitive_type(surface_index) != Mesh.PRIMITIVE_TRIANGLES:
            continue
        var arrays := mesh.surface_get_arrays(surface_index)
        var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
        var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
        if not indices.is_empty():
            for vertex_index in indices:
                faces.append(vertices[vertex_index])
        else:
            faces.append_array(vertices)
    return faces

func _content_fingerprint(
        terrain_mesh: Mesh,
        navigation_mesh: NavigationMesh,
        generated_content: Node3D) -> String:
    var context := HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    _hash_text(context, "openrpg-neutral-world-content-v1\n")
    _hash_text(context, "terrain-surfaces=%d\n" % terrain_mesh.get_surface_count())
    for surface_index in terrain_mesh.get_surface_count():
        _hash_text(context, "surface=%d;primitive=%d\n" % [
            surface_index, terrain_mesh.surface_get_primitive_type(surface_index)])
        var arrays := terrain_mesh.surface_get_arrays(surface_index)
        var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
        var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
        _hash_vector_array(context, vertices)
        _hash_int_array(context, indices)

    _hash_text(context, "navigation-polygons=%d\n" % navigation_mesh.get_polygon_count())
    _hash_vector_array(context, navigation_mesh.get_vertices())
    for polygon_index in navigation_mesh.get_polygon_count():
        _hash_int_array(context, navigation_mesh.get_polygon(polygon_index))

    var generated_instances := generated_content.find_children("*", "MultiMeshInstance3D", true, false)
    generated_instances.sort_custom(func(left: Node, right: Node) -> bool:
        return String(left.name) < String(right.name))
    _hash_text(context, "multimeshes=%d\n" % generated_instances.size())
    for generated_node in generated_instances:
        var instance := generated_node as MultiMeshInstance3D
        if instance == null or instance.multimesh == null:
            continue
        _hash_text(context, "name=%s;instances=%d\n" % [
            instance.name, instance.multimesh.instance_count])
        for instance_index in instance.multimesh.instance_count:
            _hash_transform(context, instance.multimesh.get_instance_transform(instance_index))
    return context.finish().hex_encode()

func _hash_text(context: HashingContext, value: String) -> void:
    context.update(value.to_utf8_buffer())

func _hash_vector_array(context: HashingContext, values: PackedVector3Array) -> void:
    _hash_text(context, "vectors=%d\n" % values.size())
    for value in values:
        _hash_text(context, "%.6f,%.6f,%.6f\n" % [value.x, value.y, value.z])

func _hash_int_array(context: HashingContext, values: PackedInt32Array) -> void:
    _hash_text(context, "ints=%d:" % values.size())
    for value in values:
        _hash_text(context, "%d," % value)
    _hash_text(context, "\n")

func _hash_transform(context: HashingContext, value: Transform3D) -> void:
    _hash_text(context, "%.6f,%.6f,%.6f;%.6f,%.6f,%.6f;%.6f,%.6f,%.6f;%.6f,%.6f,%.6f\n" % [
        value.basis.x.x, value.basis.x.y, value.basis.x.z,
        value.basis.y.x, value.basis.y.y, value.basis.y.z,
        value.basis.z.x, value.basis.z.y, value.basis.z.z,
        value.origin.x, value.origin.y, value.origin.z])

func _assign_owner_recursive(node: Node, scene_owner: Node) -> void:
    for child in node.get_children():
        child.owner = scene_owner
        _assign_owner_recursive(child, scene_owner)

func _failure(code: String, message: String) -> Dictionary:
    return {
        "succeeded": false,
        "artifacts": [],
        "diagnostics": [{ "code": code, "message": message, "path": "world-generation/bake" }],
    }
