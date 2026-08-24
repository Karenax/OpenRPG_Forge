@tool
class_name ForgeForestMap
extends Node3D

## Forge-owned map adapter built on the extracted GamesNotDeveloped scatter core.
## The PNG and the versioned JSON definition are the durable map data; generated
## MultiMesh nodes remain disposable views.

enum PaintLayer {
    FOREST,
    CLEARING,
    FLOWERS,
    ROCKS,
    FORBIDDEN,
}

const FORMAT_VERSION := 1
const DEFAULT_MASK_RESOLUTION := 512
const DEFAULT_MASK_PATH := "res://forest_maps/forest-map-mask.png"
const DEFAULT_DEFINITION_PATH := "res://forest_maps/forest-map.json"

@export_group("Forge map")
@export var map_id := "forest-demo"
@export var display_name := "Forêt — démonstration"
@export var area_size := Vector2(128.0, 128.0):
    set(value):
        area_size = Vector2(maxf(value.x, 1.0), maxf(value.y, 1.0))
        _configure_scatter_layers()
@export var seed := 7:
    set(value):
        seed = value
        _configure_scatter_layers()
@export_file("*.png") var mask_path := DEFAULT_MASK_PATH
@export_file("*.json") var definition_path := DEFAULT_DEFINITION_PATH
@export var mask_texture: Texture2D:
    set(value):
        mask_texture = value
        _mask_image_cache = null
        _apply_mask_to_scatter_layers()

@export_group("Scatter nodes")
@export_node_path("Node3D") var forest_scatter_path := NodePath("Forest")
@export_node_path("Node3D") var flowers_scatter_path := NodePath("Flowers")
@export_node_path("Node3D") var rocks_scatter_path := NodePath("Rocks")

var active_paint_layer := PaintLayer.FOREST
var _mask_image_cache: Image


func _ready() -> void:
    _ensure_working_mask()
    _configure_scatter_layers()


func get_layer_display_name(layer: int) -> String:
    match layer:
        PaintLayer.FOREST:
            return "Forêt"
        PaintLayer.CLEARING:
            return "Clairière"
        PaintLayer.FLOWERS:
            return "Fleurs"
        PaintLayer.ROCKS:
            return "Rochers"
        PaintLayer.FORBIDDEN:
            return "Zone interdite"
    return "Inconnue"


func set_active_paint_layer(layer: int) -> void:
    active_paint_layer = clampi(layer, PaintLayer.FOREST, PaintLayer.FORBIDDEN)


func local_to_mask_uv(local_position: Vector3) -> Vector2:
    if area_size.x <= 0.0 or area_size.y <= 0.0:
        return Vector2(-1.0, -1.0)
    return Vector2(
        (local_position.x / area_size.x) + 0.5,
        (local_position.z / area_size.y) + 0.5
    )


func get_mask_texture_path() -> String:
    if not mask_path.is_empty() and FileAccess.file_exists(mask_path):
        return mask_path
    if mask_texture != null and not mask_texture.resource_path.is_empty():
        return mask_texture.resource_path
    return ""


func get_mask_image_copy() -> Image:
    _ensure_working_mask()
    return _mask_image_cache.duplicate()


func create_mask_texture_file(path: String, resolution: int) -> bool:
    if path.is_empty() or resolution <= 0:
        return false
    var image := _create_default_mask(resolution)
    _ensure_parent_directory(path)
    if image.save_png(path) != OK:
        return false
    mask_path = path
    if definition_path.is_empty() or definition_path == DEFAULT_DEFINITION_PATH:
        definition_path = path.get_basename() + ".json"
    _mask_image_cache = image
    mask_texture = ImageTexture.create_from_image(image)
    _apply_mask_to_scatter_layers()
    save_map_definition(definition_path)
    return true


func preview_mask_image(image: Image) -> void:
    if image == null or image.is_empty():
        return
    _mask_image_cache = _normalize_image(image)
    for scatter in _scatter_layers():
        scatter.preview_mask_image(_mask_image_cache)


func set_mask_image(image: Image, save_to_disk := true) -> void:
    if image == null or image.is_empty():
        return
    _mask_image_cache = _normalize_image(image)
    if save_to_disk and not mask_path.is_empty():
        _ensure_parent_directory(mask_path)
        _mask_image_cache.save_png(mask_path)
    preview_mask_image(_mask_image_cache)
    if save_to_disk:
        save_map_definition(definition_path)


func paint_mask_circle_on_image(
    image: Image,
    local_position: Vector3,
    radius_world: float,
    value: float,
    hardness: float = 0.35,
    opacity: float = 1.0
) -> bool:
    if image == null or image.is_empty():
        return false
    var uv := local_to_mask_uv(local_position)
    if uv.x < 0.0 or uv.x > 1.0 or uv.y < 0.0 or uv.y > 1.0:
        return false

    var width := image.get_width()
    var height := image.get_height()
    var center := Vector2i(
        int(round(uv.x * float(width - 1))),
        int(round(uv.y * float(height - 1)))
    )
    var pixel_radius := maxi(1, int(ceil(maxf(
        radius_world / area_size.x * float(width),
        radius_world / area_size.y * float(height)
    ))))
    var inner_radius := float(pixel_radius) * clampf(hardness, 0.0, 1.0)
    var changed := false
    for y in range(maxi(0, center.y - pixel_radius), mini(height, center.y + pixel_radius + 1)):
        for x in range(maxi(0, center.x - pixel_radius), mini(width, center.x + pixel_radius + 1)):
            var distance := Vector2(float(x - center.x), float(y - center.y)).length()
            if distance > float(pixel_radius):
                continue
            var influence := 1.0
            if distance > inner_radius:
                influence = 1.0 - smoothstep(
                    inner_radius,
                    maxf(float(pixel_radius), inner_radius + 0.0001),
                    distance
                )
            influence *= clampf(opacity, 0.0, 1.0)
            if influence <= 0.0:
                continue
            var current := image.get_pixel(x, y)
            var next := _paint_color(current, clampf(value, 0.0, 1.0), influence)
            if next != current:
                image.set_pixel(x, y, next)
                changed = true
    return changed


func sample_forbidden_at_world_position(world_position: Vector3) -> float:
    var image := get_mask_image_copy()
    var uv := local_to_mask_uv(to_local(world_position))
    if uv.x < 0.0 or uv.x > 1.0 or uv.y < 0.0 or uv.y > 1.0:
        return 1.0
    var x := clampi(int(floor(uv.x * float(image.get_width()))), 0, image.get_width() - 1)
    var y := clampi(int(floor(uv.y * float(image.get_height()))), 0, image.get_height() - 1)
    return image.get_pixel(x, y).a


func set_layer_resource(layer: int, resource: Resource) -> bool:
    var scatter := _scatter_for_paint_layer(layer)
    if scatter == null or resource == null:
        return false
    var entry := ForgeBiomeScatterEntry.new()
    entry.probability = 1.0
    entry.scale_min = Vector3(0.85, 0.85, 0.85)
    entry.scale_max = Vector3(1.2, 1.35, 1.2)
    if resource is PackedScene:
        entry.mesh_scene = resource
    elif resource is Mesh:
        entry.mesh = resource
    else:
        return false
    scatter.entries = [entry]
    save_map_definition(definition_path)
    return true


func get_layer_resource_path(layer: int) -> String:
    var scatter := _scatter_for_paint_layer(layer)
    if scatter == null or scatter.entries.is_empty():
        return ""
    var entry: ForgeBiomeScatterEntry = scatter.entries.front()
    if entry.mesh_scene != null:
        return entry.mesh_scene.resource_path
    if entry.mesh != null:
        return entry.mesh.resource_path
    return ""


func save_map_definition(path := "") -> bool:
    var target := definition_path if path.is_empty() else path
    if target.is_empty():
        return false
    _ensure_parent_directory(target)
    var definition := {
        "format_version": FORMAT_VERSION,
        "map_id": map_id,
        "display_name": display_name,
        "area_size": {"x": area_size.x, "y": area_size.y},
        "seed": seed,
        "mask": {
            "path": mask_path,
            "channels": {
                "red": "forest-density",
                "green": "flowers-density",
                "blue": "rocks-density",
                "alpha": "forbidden-zone",
                "clearing": "erase-red-green-blue"
            }
        },
        "asset_slots": {
            "forest": get_layer_resource_path(PaintLayer.FOREST),
            "flowers": get_layer_resource_path(PaintLayer.FLOWERS),
            "rocks": get_layer_resource_path(PaintLayer.ROCKS)
        },
        "generator": {
            "implementation": "forge_forest_map",
            "upstream": "GamesNotDeveloped/godot-forest-demo@git:623212ffeb4efbf4c95323a4a6a57e726be4f6e9"
        }
    }
    var file := FileAccess.open(target, FileAccess.WRITE)
    if file == null:
        return false
    file.store_string(JSON.stringify(definition, "  ") + "\n")
    file.close()
    definition_path = target
    return true


func _paint_color(color: Color, value: float, influence: float) -> Color:
    match active_paint_layer:
        PaintLayer.FOREST:
            color.r = lerpf(color.r, value, influence)
        PaintLayer.CLEARING:
            if value >= 0.5:
                color.r = lerpf(color.r, 0.0, influence)
                color.g = lerpf(color.g, 0.0, influence)
                color.b = lerpf(color.b, 0.0, influence)
            else:
                color.r = lerpf(color.r, 1.0, influence)
        PaintLayer.FLOWERS:
            color.g = lerpf(color.g, value, influence)
        PaintLayer.ROCKS:
            color.b = lerpf(color.b, value, influence)
        PaintLayer.FORBIDDEN:
            color.a = lerpf(color.a, value, influence)
    return color


func _ensure_working_mask() -> void:
    if _mask_image_cache != null and not _mask_image_cache.is_empty():
        return
    if mask_texture != null:
        var image := mask_texture.get_image()
        if image != null and not image.is_empty():
            _mask_image_cache = _normalize_image(image)
            return
    if not mask_path.is_empty() and FileAccess.file_exists(mask_path):
        var disk_image := Image.new()
        var load_error := disk_image.load_png_from_buffer(FileAccess.get_file_as_bytes(mask_path))
        if load_error == OK and not disk_image.is_empty():
            _mask_image_cache = _normalize_image(disk_image)
            mask_texture = ImageTexture.create_from_image(_mask_image_cache)
            return
    _mask_image_cache = _create_default_mask(DEFAULT_MASK_RESOLUTION)
    mask_texture = ImageTexture.create_from_image(_mask_image_cache)


func _create_default_mask(resolution: int) -> Image:
    var image := Image.create(resolution, resolution, false, Image.FORMAT_RGBA8)
    image.fill(Color(1.0, 0.0, 0.0, 0.0))
    return image


func _normalize_image(image: Image) -> Image:
    var normalized := image.duplicate()
    if normalized.is_compressed():
        normalized.decompress()
    if normalized.get_format() != Image.FORMAT_RGBA8:
        normalized.convert(Image.FORMAT_RGBA8)
    return normalized


func _configure_scatter_layers() -> void:
    var layers := _scatter_layers()
    for index in range(layers.size()):
        var scatter := layers[index]
        scatter.area_size = area_size
        scatter.seed = seed + index * 101
        scatter.mask_enabled = true
        scatter.mask_affects_density = true
        scatter.mask_threshold = 0.02
        scatter.exclusion_mask_enabled = true
        scatter.exclusion_mask_channel = ForgeBiomeScatter.MaskChannel.ALPHA
        scatter.exclusion_mask_threshold = 0.02
    var forest := _forest_scatter()
    var flowers := _flowers_scatter()
    var rocks := _rocks_scatter()
    if forest != null:
        forest.mask_channel = ForgeBiomeScatter.MaskChannel.RED
    if flowers != null:
        flowers.mask_channel = ForgeBiomeScatter.MaskChannel.GREEN
    if rocks != null:
        rocks.mask_channel = ForgeBiomeScatter.MaskChannel.BLUE
    _apply_mask_to_scatter_layers()


func _apply_mask_to_scatter_layers() -> void:
    if mask_texture == null:
        return
    for scatter in _scatter_layers():
        scatter.assign_mask_texture(mask_texture, true)
        if _mask_image_cache != null and not _mask_image_cache.is_empty():
            scatter.preview_mask_image(_mask_image_cache)


func _scatter_layers() -> Array[ForgeBiomeScatter]:
    var result: Array[ForgeBiomeScatter] = []
    for scatter in [_forest_scatter(), _flowers_scatter(), _rocks_scatter()]:
        if scatter != null:
            result.append(scatter)
    return result


func _scatter_for_paint_layer(layer: int) -> ForgeBiomeScatter:
    match layer:
        PaintLayer.FOREST:
            return _forest_scatter()
        PaintLayer.FLOWERS:
            return _flowers_scatter()
        PaintLayer.ROCKS:
            return _rocks_scatter()
    return null


func _forest_scatter() -> ForgeBiomeScatter:
    return get_node_or_null(forest_scatter_path) as ForgeBiomeScatter


func _flowers_scatter() -> ForgeBiomeScatter:
    return get_node_or_null(flowers_scatter_path) as ForgeBiomeScatter


func _rocks_scatter() -> ForgeBiomeScatter:
    return get_node_or_null(rocks_scatter_path) as ForgeBiomeScatter


func _ensure_parent_directory(path: String) -> void:
    var absolute := ProjectSettings.globalize_path(path)
    DirAccess.make_dir_recursive_absolute(absolute.get_base_dir())
