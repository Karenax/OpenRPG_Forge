@tool
extends RefCounted

const FORMAT_VERSION := "1.0.0"
const CANONICAL_TERRAIN_BAKE_LOD := 0
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
    # Canonical promotion is the editable/runtime terrain authority. Baking Terrain3D at
    # LOD 2 silently collapsed a 2 km production landscape from 257 to 65 samples (32 m
    # cells), even though generation itself had authored the higher-resolution surface.
    # Keep the provider's full deterministic grid here; runtime chunking handles display LOD.
    var provider_mesh := terrain.call("bake_mesh", CANONICAL_TERRAIN_BAKE_LOD, 0) as Mesh
    if provider_mesh == null or provider_mesh.get_surface_count() == 0:
        return _failure("world_generation.bake.mesh_failed", "Terrain provider returned no bakeable surface.")
    var terrain_mesh := provider_mesh.duplicate(true) as Mesh
    if terrain_mesh == null:
        return _failure("world_generation.bake.mesh_duplicate_failed", "Neutral terrain mesh could not be detached.")
    var terrain_surface_result := _terrain_surface_evidence(terrain_mesh)
    if not bool(terrain_surface_result.get("succeeded", false)):
        return _failure(
            str(terrain_surface_result.get("code", "world_generation.bake.terrain_surface_invalid")),
            str(terrain_surface_result.get("message", "Neutral terrain has no deterministic placement surface.")))
    var terrain_surface: Dictionary = terrain_surface_result.get("evidence", {})

    var baked_root := Node3D.new()
    baked_root.name = "OpenRPGBakedWorld"
    baked_root.set_meta("openrpg_bake_format", FORMAT_VERSION)
    baked_root.set_meta("openrpg_recipe_fingerprint", generated_root.get_meta("openrpg_recipe_fingerprint", ""))

    var terrain_root := Node3D.new()
    terrain_root.name = "Terrain"
    terrain_root.set_meta("openrpg_visual_tier", "technical_proxy")
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
        var environment_category := str(source_instance.get_meta(
            "openrpg_environment_category", _environment_category(artistic_role))).to_lower()
        neutral_instance.name = (
            "BiomeProxies" if artistic_role == "biomes" and not generated_content.has_node("BiomeProxies") else
            "Artistic_%s_%03d" % [artistic_role.capitalize(), copied_instances]
            if not artistic_role.is_empty() else "GeneratedInstances_%03d" % copied_instances)
        neutral_instance.multimesh = _copy_multimesh(source_instance.multimesh)
        neutral_instance.transform = generated_root.global_transform.affine_inverse() * source_instance.global_transform
        if source_instance.material_override != null:
            neutral_instance.material_override = source_instance.material_override.duplicate(true)
        generated_content.add_child(neutral_instance)
        if not artistic_role.is_empty():
            neutral_instance.set_meta("openrpg_artistic_role", artistic_role)
            neutral_instance.set_meta("openrpg_environment_category", environment_category)
            neutral_instance.set_meta("openrpg_recipe_driven", true)
            neutral_instance.set_meta("openrpg_visual_tier", "technical_proxy")
            artistic_instances += neutral_instance.multimesh.instance_count
            artistic_roles[artistic_role] = int(artistic_roles.get(artistic_role, 0)) + \
                neutral_instance.multimesh.instance_count
        copied_instances += neutral_instance.multimesh.instance_count
    if copied_instances == 0:
        baked_root.free()
        return _failure("world_generation.bake.generated_content_missing", "Graph PCG produced no neutral instances to bake.")

    var environment_occupancy := _environment_occupancy_evidence(generated_content, mesh_bounds)

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
        "terrainSurface": terrain_surface,
        "environmentOccupancy": environment_occupancy,
        "contentFingerprint": content_fingerprint,
        "sha256": FileAccess.get_sha256(safe_path),
    }

func _terrain_surface_evidence(mesh: Mesh) -> Dictionary:
    var heights_by_point := {}
    var x_by_key := {}
    var z_by_key := {}
    var edges := {}
    for surface_index in mesh.get_surface_count():
        if mesh.surface_get_primitive_type(surface_index) != Mesh.PRIMITIVE_TRIANGLES:
            continue
        var arrays := mesh.surface_get_arrays(surface_index)
        var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
        var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
        for vertex in vertices:
            var x_key := "%.6f" % _clean_float(vertex.x)
            var z_key := "%.6f" % _clean_float(vertex.z)
            var point_key := "%s|%s" % [x_key, z_key]
            if heights_by_point.has(point_key) and \
                    absf(float(heights_by_point[point_key]) - vertex.y) > 0.0001:
                return _surface_failure("Terrain mesh contains conflicting heights at one horizontal sample.")
            heights_by_point[point_key] = _clean_float(vertex.y)
            x_by_key[x_key] = _clean_float(vertex.x)
            z_by_key[z_key] = _clean_float(vertex.z)
        var triangle_indices := PackedInt32Array()
        if indices.is_empty():
            triangle_indices.resize(vertices.size())
            for index in vertices.size():
                triangle_indices[index] = index
        else:
            triangle_indices = indices
        if triangle_indices.size() % 3 != 0:
            return _surface_failure("Terrain mesh triangle indices are incomplete.")
        for triangle_index in range(0, triangle_indices.size(), 3):
            var triangle := [
                vertices[triangle_indices[triangle_index]],
                vertices[triangle_indices[triangle_index + 1]],
                vertices[triangle_indices[triangle_index + 2]],
            ]
            for edge_index in 3:
                edges[_horizontal_edge_key(triangle[edge_index], triangle[(edge_index + 1) % 3])] = true

    var x_values: Array = x_by_key.values()
    var z_values: Array = z_by_key.values()
    x_values.sort()
    z_values.sort()
    if x_values.size() < 2 or x_values.size() > 257 or z_values.size() < 2 or z_values.size() > 257 or \
            heights_by_point.size() != x_values.size() * z_values.size():
        return _surface_failure("Terrain mesh must be a complete regular heightfield of 2 to 257 samples per axis.")
    if not _regular_axis(x_values) or not _regular_axis(z_values):
        return _surface_failure("Terrain placement samples are not regularly spaced.")

    var main_diagonal_cells := 0
    var anti_diagonal_cells := 0
    for z_index in z_values.size() - 1:
        for x_index in x_values.size() - 1:
            var top_left := Vector3(x_values[x_index], 0, z_values[z_index])
            var top_right := Vector3(x_values[x_index + 1], 0, z_values[z_index])
            var bottom_left := Vector3(x_values[x_index], 0, z_values[z_index + 1])
            var bottom_right := Vector3(x_values[x_index + 1], 0, z_values[z_index + 1])
            var has_main := edges.has(_horizontal_edge_key(top_left, bottom_right))
            var has_anti := edges.has(_horizontal_edge_key(top_right, bottom_left))
            if has_main == has_anti:
                return _surface_failure("Terrain cells do not expose one unambiguous triangle diagonal.")
            if has_main:
                main_diagonal_cells += 1
            else:
                anti_diagonal_cells += 1
    if main_diagonal_cells > 0 and anti_diagonal_cells > 0:
        return _surface_failure("Terrain cells use mixed triangle diagonals and cannot be sampled deterministically.")
    var diagonal := "main" if main_diagonal_cells > 0 else "anti"

    var width_value := float(x_values[-1]) - float(x_values[0])
    var depth_value := float(z_values[-1]) - float(z_values[0])
    var width := int(round(width_value))
    var depth := int(round(depth_value))
    if width < 4 or width > 4096 or depth < 4 or depth > 4096 or \
            absf(width_value - width) > 0.01 or absf(depth_value - depth) > 0.01:
        return _surface_failure("Terrain placement bounds must resolve to supported whole-meter area dimensions.")

    var height_scale := 0.0
    var raw_heights := PackedFloat32Array()
    for z_value in z_values:
        for x_value in x_values:
            var point_key := "%.6f|%.6f" % [_clean_float(x_value), _clean_float(z_value)]
            var height := float(heights_by_point[point_key])
            raw_heights.append(height)
            height_scale = maxf(height_scale, absf(height))
    height_scale = _clean_float(height_scale)
    var normalized_heights: Array = []
    for height in raw_heights:
        normalized_heights.append(_clean_float(height / height_scale) if height_scale > 0.000001 else 0.0)
    var evidence := {
        "schemaVersion": 1,
        "width": width,
        "depth": depth,
        "samplesX": x_values.size(),
        "samplesZ": z_values.size(),
        "heightScale": height_scale,
        "heights": normalized_heights,
        "cellDiagonal": diagonal,
    }
    evidence["fingerprint"] = _terrain_surface_fingerprint(evidence)
    return { "succeeded": true, "evidence": evidence }

func _environment_occupancy_evidence(generated_content: Node3D, mesh_bounds: AABB) -> Dictionary:
    # Four-metre cells retain enough precision to keep buildings clear of
    # individual vegetation clusters while keeping large-world reports compact.
    var cell_size := 4.0
    var cells_by_key := {}
    for generated_node in generated_content.find_children("*", "MultiMeshInstance3D", true, false):
        var instance := generated_node as MultiMeshInstance3D
        if instance == null or instance.multimesh == null:
            continue
        var role := str(instance.get_meta("openrpg_artistic_role", "")).to_lower()
        if role.is_empty():
            continue
        var category := str(instance.get_meta(
            "openrpg_environment_category", _environment_category(role))).to_lower()
        for instance_index in instance.multimesh.instance_count:
            var placement := instance.transform * instance.multimesh.get_instance_transform(instance_index)
            var local := placement.origin
            var cell_x := clampi(int(floor((local.x - mesh_bounds.position.x) / cell_size)), 0,
                maxi(0, int(ceil(mesh_bounds.size.x / cell_size)) - 1))
            var cell_z := clampi(int(floor((local.z - mesh_bounds.position.z) / cell_size)), 0,
                maxi(0, int(ceil(mesh_bounds.size.z / cell_size)) - 1))
            var key := "%s|%s|%d|%d" % [category, role, cell_x, cell_z]
            cells_by_key[key] = int(cells_by_key.get(key, 0)) + 1
    var cells: Array = []
    for key_variant in cells_by_key.keys():
        var key := str(key_variant)
        var parts := key.split("|")
        var cell_x := int(parts[2])
        var cell_z := int(parts[3])
        var center_x := minf(mesh_bounds.position.x + ((cell_x + 0.5) * cell_size), mesh_bounds.end.x)
        var center_z := minf(mesh_bounds.position.z + ((cell_z + 0.5) * cell_size), mesh_bounds.end.z)
        cells.append({
            "category": parts[0],
            "role": parts[1],
            "x": _clean_float(center_x - mesh_bounds.get_center().x),
            "z": _clean_float(center_z - mesh_bounds.get_center().z),
            "count": int(cells_by_key[key]),
        })
    cells.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
        if str(left.category) != str(right.category):
            return str(left.category) < str(right.category)
        if str(left.role) != str(right.role):
            return str(left.role) < str(right.role)
        if float(left.z) != float(right.z):
            return float(left.z) < float(right.z)
        return float(left.x) < float(right.x))
    var evidence := {
        "schemaVersion": 1,
        "width": int(round(mesh_bounds.size.x)),
        "depth": int(round(mesh_bounds.size.z)),
        "cellSizeMeters": cell_size,
        "cells": cells,
    }
    evidence["fingerprint"] = _environment_occupancy_fingerprint(evidence)
    return evidence

func _terrain_surface_fingerprint(evidence: Dictionary) -> String:
    var context := HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    _hash_text(context, "openrpg-placement-surface-v2\n")
    _hash_text(context, "width=%d;depth=%d;samplesX=%d;samplesZ=%d;heightScaleMicrounits=%d;diagonal=%s\n" % [
        int(evidence.width), int(evidence.depth), int(evidence.samplesX), int(evidence.samplesZ),
        _microunits(float(evidence.heightScale)), str(evidence.cellDiagonal)])
    var heights: Array = evidence.heights
    _hash_text(context, "heights=%d\n" % heights.size())
    for height in heights:
        _hash_text(context, "%d\n" % _microunits(float(height)))
    return context.finish().hex_encode()

func _environment_occupancy_fingerprint(evidence: Dictionary) -> String:
    var context := HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    _hash_text(context, "openrpg-environment-occupancy-v2\n")
    _hash_text(context, "width=%d;depth=%d;cellSizeMicrounits=%d\n" % [
        int(evidence.width), int(evidence.depth), _microunits(float(evidence.cellSizeMeters))])
    var cells: Array = evidence.cells
    _hash_text(context, "cells=%d\n" % cells.size())
    for cell in cells:
        _hash_text(context, "%s|%s|%d|%d|%d\n" % [
            str(cell.category), str(cell.role), _microunits(float(cell.x)),
            _microunits(float(cell.z)), int(cell.count)])
    return context.finish().hex_encode()

func _microunits(value: float) -> int:
    return int(round(value * 1000000.0))

func _horizontal_edge_key(first: Vector3, second: Vector3) -> String:
    var first_key := "%.6f|%.6f" % [_clean_float(first.x), _clean_float(first.z)]
    var second_key := "%.6f|%.6f" % [_clean_float(second.x), _clean_float(second.z)]
    return "%s>%s" % [first_key, second_key] if first_key < second_key else \
        "%s>%s" % [second_key, first_key]

func _regular_axis(values: Array) -> bool:
    var expected_step := (float(values[-1]) - float(values[0])) / (values.size() - 1)
    if expected_step <= 0:
        return false
    for index in values.size():
        if absf(float(values[index]) - (float(values[0]) + (index * expected_step))) > 0.001:
            return false
    return true

func _environment_category(role: String) -> String:
    return "vegetation" if role == "biomes" else role

func _clean_float(value: float) -> float:
    var snapped := snappedf(value, 0.000001)
    return 0.0 if absf(snapped) < 0.0000005 else snapped

func _surface_failure(message: String) -> Dictionary:
    return {
        "succeeded": false,
        "code": "world_generation.bake.terrain_surface_invalid",
        "message": message,
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
    # ResourceSaver serializes the packed MultiMesh buffer. Re-read that buffer
    # from the destination after the per-instance copies: some providers expose
    # correct transforms through get_instance_transform() while retaining a
    # stale source buffer, which would otherwise collapse every instance on
    # provider-free reopen.
    var serialized_buffer := result.buffer.duplicate()
    if not serialized_buffer.is_empty():
        result.buffer = serialized_buffer
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
